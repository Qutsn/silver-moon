using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Middleton.Desktop;

public sealed record UpgradeDeviceReadback(string Model,string Serial,string Firmware,bool UpgradeIdle);

// Infrastructure must own the BLE session exclusively, buffer upgrade notifications
// while waiting for a matching ACK, reject late ACKs after a timeout, and honor cancellation.
// No live adapter is wired until these guarantees are verified on Windows.
public interface IOfficialUpgradeLink
{
 Task<UpgradeDeviceReadback> ReadCurrentAsync(CancellationToken cancellationToken);
 Task<byte> SendAcknowledgedAsync(ReadOnlyMemory<byte> frame,CancellationToken cancellationToken);
 Task<UpgradeMessage> ReceiveAsync(CancellationToken cancellationToken);
 // Dispose the old session, reconnect the same identity and read uncached device information.
 Task<UpgradeDeviceReadback> ReconnectAfterTrialAsync(CancellationToken cancellationToken);
 // A separate reopened session: read identity/version and the complete PS4 idle context.
 Task<UpgradeDeviceReadback> ReadIndependentlyAsync(CancellationToken cancellationToken);
}

// Initial official install only. Interrupted/resumed sessions deliberately require
// a separate recovery policy; this coordinator never sends ABORT or writes PS keys.
public sealed class OfficialUpgradeCoordinator
{
 int started;
 public InstallationState State {get;private set;}=InstallationState.Unavailable;
 public UpgradeTransferProgress? Progress=>transfer?.Progress;
 UpgradeDataTransfer? transfer;

 public async Task<InstallationEvidence> RunAsync(ReadOnlyMemory<byte> firmware,string serial,
  IOfficialUpgradeLink link,CancellationToken cancellationToken)
 {
  if(Interlocked.Exchange(ref started,1)!=0)throw new InvalidOperationException("An upgrade coordinator is single-use.");
  // Snapshot caller memory before hashing, so a later caller mutation cannot change installed bytes.
  byte[] image=firmware.ToArray();
  var descriptor=FirmwareCatalog.Analyze(image).Known;
  if(descriptor?.Strategy!=FirmwareStrategy.OfficialFull || string.IsNullOrWhiteSpace(serial))
   throw new UpgradeProtocolException("Official installation requires a known official image and device serial.");
  using var overall=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
  overall.CancelAfter(TimeSpan.FromMinutes(70));
  var ct=overall.Token;
  try
  {
   var before=await Bounded(token=>link.ReadCurrentAsync(token),TimeSpan.FromSeconds(30),ct).ConfigureAwait(false);
   VerifyDevice(before,serial,null,true);
   await Open(link,ct).ConfigureAwait(false);
   await SyncStart(link,descriptor.UpgradeId,0,ct).ConfigureAwait(false);
   State=InstallationState.Transferring;transfer=new UpgradeDataTransfer(image);
   await Send(link,21,Array.Empty<byte>(),ct).ConfigureAwait(false);
   using(var dataDeadline=CancellationTokenSource.CreateLinkedTokenSource(ct))
   {
    dataDeadline.CancelAfter(TimeSpan.FromHours(1));
    while(transfer.Progress.Cursor<image.Length)
    {
     var message=await Receive(link,TimeSpan.FromSeconds(30),dataDeadline.Token).ConfigureAwait(false);
     var request=UpgradeWire.DataRequest(message);
     await transfer.SendRequestedAsync(request.Count,request.Skip,
      (frame,token)=>Ack(link,frame,token),dataDeadline.Token).ConfigureAwait(false);
    }
   }
   State=InstallationState.Validating;
   using(var validation=CancellationTokenSource.CreateLinkedTokenSource(ct))
   {
    validation.CancelAfter(TimeSpan.FromSeconds(180));
    while(true)
    {
     await Send(link,22,Array.Empty<byte>(),validation.Token).ConfigureAwait(false);
     var message=await Receive(link,TimeSpan.FromSeconds(180),validation.Token).ConfigureAwait(false);
     if(message.Code==11){UpgradeWire.Require(message,11,0);break;}
     var delay=UpgradeWire.ValidationDelay(message);
     await Task.Delay(delay,validation.Token).ConfigureAwait(false);
    }
   }
   State=InstallationState.Rebooting;
   await Send(link,12,new byte[]{0},ct).ConfigureAwait(false);
   var trial=await Bounded(token=>link.ReconnectAfterTrialAsync(token),TimeSpan.FromSeconds(120),ct).ConfigureAwait(false);
   VerifyDevice(trial,serial,descriptor.DisplayVersion,false);
   await Open(link,ct).ConfigureAwait(false);
   await SyncStart(link,descriptor.UpgradeId,3,ct).ConfigureAwait(false);
   State=InstallationState.Committing;
   using(var commit=CancellationTokenSource.CreateLinkedTokenSource(ct))
   {
    commit.CancelAfter(TimeSpan.FromSeconds(300));
    await Send(link,14,new byte[]{0},commit.Token).ConfigureAwait(false);
    UpgradeWire.Require(await Receive(link,TimeSpan.FromSeconds(60),commit.Token).ConfigureAwait(false),15,0);
    await Send(link,16,new byte[]{0},commit.Token).ConfigureAwait(false);
    UpgradeWire.Require(await Receive(link,TimeSpan.FromSeconds(300),commit.Token).ConfigureAwait(false),18,0);
   }
   State=InstallationState.ReadingBack;
   var final=await Bounded(token=>link.ReadIndependentlyAsync(token),TimeSpan.FromSeconds(60),ct).ConfigureAwait(false);
   VerifyDevice(final,serial,descriptor.DisplayVersion,true);
   State=InstallationState.Complete;
   return new(true,true,true,true);
  }
  catch {State=InstallationState.ResultUnknown;throw;}
 }
 static void VerifyDevice(UpgradeDeviceReadback value,string serial,string? firmware,bool requireIdle)
 {
  if(!string.Equals(value.Model.Trim(),"MIDDLETON",StringComparison.OrdinalIgnoreCase) || value.Serial!=serial ||
   (firmware is not null && value.Firmware!=firmware) || (requireIdle && !value.UpgradeIdle))
   throw new UpgradeProtocolException("Device identity, version or idle-state verification failed.");
 }
 static async Task Open(IOfficialUpgradeLink link,CancellationToken ct)
 {
  await Ack(link,UpgradeWire.Command(0x4001,new byte[]{18}),ct).ConfigureAwait(false);
  await Ack(link,UpgradeWire.Command(0x0640,Array.Empty<byte>()),ct).ConfigureAwait(false);
 }
 static async Task SyncStart(IOfficialUpgradeLink link,string id,byte resume,CancellationToken ct)
 {
  await Send(link,19,Convert.FromHexString(id),ct).ConfigureAwait(false);
  var sync=await Receive(link,TimeSpan.FromSeconds(30),ct).ConfigureAwait(false);
  UpgradeWire.Require(sync,20,6);
  if(sync.Body.Span[0]!=resume || sync.Body.Span[5]!=3 || !sync.Body.Slice(1,4).Span.SequenceEqual(Convert.FromHexString(id)))
   throw new UpgradeProtocolException("Unexpected upgrade identity, protocol or resume point.");
  await Send(link,1,Array.Empty<byte>(),ct).ConfigureAwait(false);
  var start=await Receive(link,TimeSpan.FromSeconds(30),ct).ConfigureAwait(false);
  if(start.Code!=2 || start.Body.Length<1 || start.Body.Span[0]!=0)throw new UpgradeProtocolException("Upgrade START rejected.");
 }
 static Task<byte> Send(IOfficialUpgradeLink link,byte code,byte[] body,CancellationToken ct)=>Ack(link,UpgradeWire.Control(code,body),ct);
 static async Task<byte> Ack(IOfficialUpgradeLink link,ReadOnlyMemory<byte> frame,CancellationToken ct)
 {
  byte status=await Bounded(token=>link.SendAcknowledgedAsync(frame,token),TimeSpan.FromSeconds(4),ct).ConfigureAwait(false);
  if(status!=0)throw new UpgradeProtocolException($"GAIA rejected command: 0x{status:X2}.");
  return status;
 }
 static async Task<UpgradeMessage> Receive(IOfficialUpgradeLink link,TimeSpan timeout,CancellationToken ct)
 {
  var message=await Bounded(token=>link.ReceiveAsync(token),timeout,ct).ConfigureAwait(false);
  if(message.Code==17)throw new UpgradeProtocolException($"Device upgrade error: {Convert.ToHexString(message.Body.Span)}.");
  return message;
 }
 static async Task<T> Bounded<T>(Func<CancellationToken,Task<T>> action,TimeSpan timeout,CancellationToken ct)
 {
  using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);deadline.CancelAfter(timeout);
  return await action(deadline.Token).WaitAsync(deadline.Token).ConfigureAwait(false);
 }
}
