using System;
using System.Threading;
using System.Threading.Tasks;

namespace Middleton.Desktop;
public sealed class WindowsUpgradeLink : IUpgradeLink
{
 DeviceCandidate route;
 readonly string serial;
 WindowsBleTransport? transport;
 UpgradeMailbox? mailbox;
 TaskCompletionSource disconnected=new(TaskCreationOptions.RunContinuationsAsynchronously);
 readonly SemaphoreSlim commands=new(1,1);
 public WindowsUpgradeLink(DeviceCandidate candidate,string serial){route=candidate;this.serial=serial;}
 internal DeviceCandidate CurrentRoute=>route;
 public async Task<UpgradeIdentity> ReconnectAsync(CancellationToken ct)
 {
  await DisposeConnectionAsync();
  using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);deadline.CancelAfter(TimeSpan.FromSeconds(30));
  var token=deadline.Token;
  try
  {
   var active=new WindowsBleTransport();transport=active;
   var inbox=new UpgradeMailbox();mailbox=inbox;
   disconnected=new(TaskCreationOptions.RunContinuationsAsynchronously);var lost=disconnected;
   active.Changed=(id,bytes)=>{if(id==DeviceIds.GaiaResponse || id==DeviceIds.GaiaData)inbox.Accept(bytes);};
   active.Disconnected=()=>{lost.TrySetResult();inbox.Break(new UpgradeLinkLostException(AppText.Get("Device.Message.22")));};
   await active.OpenAsync(route,token);
   async Task<string> Read(Guid id)=>DeviceValues.Text(await active.ReadAsync(id,token))??throw new UpgradeLinkLostException(AppText.Get("Device.Message.23"));
   var identity=new UpgradeIdentity(await Read(DeviceIds.Model),await Read(DeviceIds.Serial),await Read(DeviceIds.Firmware),await Read(DeviceIds.Hardware));
   if(identity.Model!="MIDDLETON" || identity.Serial!=serial)throw new UpgradeProtocolException(AppText.Get("Device.Message.24"));
   if(!await active.SubscribeAsync(DeviceIds.GaiaResponse,token) || !await active.SubscribeAsync(DeviceIds.GaiaData,token))throw new UpgradeLinkLostException(AppText.Get("Device.Message.25"));
   route=active.PairedRoute;
   DeviceLog.Write("upgrade-link-open",identity);return identity;
  }
  catch(UpgradeProtocolException){await DisposeConnectionAsync();throw;}
  catch(Exception e) when(!ct.IsCancellationRequested){await DisposeConnectionAsync();throw new UpgradeLinkLostException(e.Message);}
 }
 public async Task<GaiaAcknowledgement> CommandAsync(ushort command,ReadOnlyMemory<byte> payload,CancellationToken ct)
 {
  await commands.WaitAsync(ct);
  try
  {
   var active=transport??throw new UpgradeLinkLostException(AppText.Get("Device.Message.26"));
   var inbox=mailbox??throw new UpgradeLinkLostException(AppText.Get("Device.Message.26"));
   using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(4));
   var pending=inbox.Expect(command);
   try
   {
    await active.WriteUpgradeFrameAsync(UpgradeWire.Command(command,payload.Span),timeout.Token);
    var ack=await pending.WaitAsync(timeout.Token);
    return ack;
   }
   catch(Exception e)
   {
    inbox.Break(e);_ = pending.Exception;
    if(e is UpgradeProtocolException || ct.IsCancellationRequested)throw;
    throw new UpgradeLinkLostException(AppText.Format("Upgrade.AckInterrupted",e.Message));
   }
  }
  finally{commands.Release();}
 }
 public async Task<UpgradeMessage> ReceiveAsync(TimeSpan timeout,CancellationToken ct)
 {
  var inbox=mailbox??throw new UpgradeLinkLostException(AppText.Get("Device.Message.26"));
  using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);deadline.CancelAfter(timeout);
  try
  {
   var message=await inbox.ReceiveAsync(deadline.Token);
   if(message.Code==17)throw new UpgradeProtocolException(AppText.Format("Upgrade.DeviceError",Convert.ToHexString(message.Body.Span)));
   return message;
  }
  catch(OperationCanceledException) when(!ct.IsCancellationRequested){inbox.Break(new UpgradeLinkLostException(AppText.Get("Device.Message.27")));throw new UpgradeLinkLostException(AppText.Get("Device.Message.27"));}
 }
 public Task WaitForDeviceDisconnectAsync(CancellationToken ct)=>disconnected.Task.WaitAsync(ct);
 async Task DisposeConnectionAsync()
 {
  mailbox?.Break(new UpgradeLinkLostException(AppText.Get("Device.Message.28")));mailbox=null;
  if(transport is not null){await transport.DisposeAsync();transport=null;}
 }
 public async ValueTask DisposeAsync()=>await DisposeConnectionAsync();
}
