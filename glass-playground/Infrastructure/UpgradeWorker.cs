using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Middleton.Desktop;
// Private parent/child stdio protocol, not a network API or sandbox boundary.
// The coordinator must issue one request at a time; there are no RPC correlation IDs.
// Device bytes are validated by UpgradeWire/UpgradeMailbox before being returned.
internal sealed record UpgradeRpcRequest(string Operation,DeviceCandidate? Candidate=null,string? Serial=null,ushort Command=0,string? Payload=null,double TimeoutSeconds=30);
internal sealed record UpgradeRpcReply(JsonElement? Value,string? Error=null,bool ProtocolError=false);
internal sealed record UpgradeWorkerConnected(UpgradeIdentity Identity,DeviceCandidate Route);

public static class UpgradeWorker
{
 public static async Task<int> RunAsync()
 {
  WindowsUpgradeLink? link=null;
  try
  {
   while(await Console.In.ReadLineAsync() is { } line)
   {
    bool quit=false;UpgradeRpcReply reply;
    try
    {
     var request=JsonSerializer.Deserialize<UpgradeRpcRequest>(line)??throw new InvalidDataException("Invalid worker request.");
     using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(request.TimeoutSeconds+5));
     object value;
     switch(request.Operation)
     {
      case "open":
       if(link is not null)throw new InvalidOperationException("Worker already owns a connection.");
       link=new WindowsUpgradeLink(request.Candidate!,request.Serial!);
       var identity=await link.ReconnectAsync(timeout.Token);value=new UpgradeWorkerConnected(identity,link.CurrentRoute);break;
      case "command":
       var ack=await link!.CommandAsync(request.Command,Convert.FromBase64String(request.Payload!),timeout.Token);
       value=new{ack.Command,ack.Status,Data=Convert.ToBase64String(ack.Data.Span)};break;
      case "receive":
       var message=await link!.ReceiveAsync(TimeSpan.FromSeconds(request.TimeoutSeconds),timeout.Token);
       value=new{message.Code,Body=Convert.ToBase64String(message.Body.Span)};break;
      case "wait-disconnect":await link!.WaitForDeviceDisconnectAsync(timeout.Token);value=true;break;
      case "close":if(link is not null)await link.DisposeAsync();link=null;value=true;quit=true;break;
      default:throw new UpgradeProtocolException("Unknown worker operation.");
     }
     reply=new(JsonSerializer.SerializeToElement(value));
    }
    catch(Exception e){reply=new(null,e.Message,e is UpgradeProtocolException);}
    await Console.Out.WriteLineAsync(JsonSerializer.Serialize(reply));await Console.Out.FlushAsync();
    if(quit)break;
   }
   return 0;
  }
  finally{if(link is not null)await link.DisposeAsync();}
 }
}

// Each reconnect starts a fresh process. The parent owns the journal and UI;
// the child owns only one GATT session and exits before another child opens it.
public sealed class WindowsUpgradeProcessLink : IUpgradeLink
{
 DeviceCandidate route;
 readonly string serial;
 Process? worker;
 Task? stderr;
 public WindowsUpgradeProcessLink(DeviceCandidate route,string serial){this.route=route;this.serial=serial;}
 public async Task<UpgradeIdentity> ReconnectAsync(CancellationToken ct)
 {
  await DisposeAsync();
  string executable=Environment.ProcessPath??throw new InvalidOperationException("Missing executable path.");
  var start=new ProcessStartInfo(executable){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=AppContext.BaseDirectory};
  if(string.Equals(Path.GetFileNameWithoutExtension(executable),"dotnet",StringComparison.OrdinalIgnoreCase))start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory,"SilverMoon.dll"));
  start.ArgumentList.Add("--upgrade-worker");
  worker=Process.Start(start)??throw new UpgradeLinkLostException(AppText.Get("Device.Message.42"));
  stderr=worker.StandardError.ReadToEndAsync();
  var opened=(await Request(new("open",route,serial),ct)).Deserialize<UpgradeWorkerConnected>()??throw new UpgradeLinkLostException(AppText.Get("Device.Message.43"));
  route=opened.Route;return opened.Identity;
 }
 public async Task<GaiaAcknowledgement> CommandAsync(ushort command,ReadOnlyMemory<byte> payload,CancellationToken ct)
 {
  var result=await Request(new("command",Command:command,Payload:Convert.ToBase64String(payload.Span),TimeoutSeconds:5),ct);
  return new(result.GetProperty("Command").GetUInt16(),result.GetProperty("Status").GetByte(),Convert.FromBase64String(result.GetProperty("Data").GetString()!));
 }
 public async Task<UpgradeMessage> ReceiveAsync(TimeSpan timeout,CancellationToken ct)
 {
  var result=await Request(new("receive",TimeoutSeconds:timeout.TotalSeconds),ct);
  return new(result.GetProperty("Code").GetByte(),Convert.FromBase64String(result.GetProperty("Body").GetString()!));
 }
 public async Task WaitForDeviceDisconnectAsync(CancellationToken ct)=>await Request(new("wait-disconnect",TimeoutSeconds:180),ct);
 async Task<JsonElement> Request(UpgradeRpcRequest request,CancellationToken ct)
 {
  var child=worker??throw new UpgradeLinkLostException(AppText.Get("Device.Message.44"));
  using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(request.TimeoutSeconds+10));
  try
  {
   await child.StandardInput.WriteLineAsync(JsonSerializer.Serialize(request).AsMemory(),timeout.Token);
   await child.StandardInput.FlushAsync(timeout.Token);
   string? line=await child.StandardOutput.ReadLineAsync(timeout.Token);
   var reply=line is null?null:JsonSerializer.Deserialize<UpgradeRpcReply>(line);
   if(reply is null)throw new UpgradeLinkLostException(AppText.Get("Device.Message.45"));
   if(reply.Error is not null)
   {
    // Preserve which operation failed; UI phase changes alone cannot distinguish
    // stale GATT state, a lost ACK, and a missing upgrade notification.
    DeviceLog.Write("upgrade-worker-failed",new{request.Operation,request.Command,request.TimeoutSeconds,reply.Error,reply.ProtocolError});
    if(reply.ProtocolError)throw new UpgradeProtocolException(reply.Error);
    throw new UpgradeLinkLostException(reply.Error);
   }
   return reply.Value??throw new UpgradeLinkLostException(AppText.Get("Device.Message.46"));
  }
  catch(Exception e) when(e is IOException or OperationCanceledException)
  {
   // A timeout leaves RPC/ACK correlation uncertain: never reuse this worker.
   if(!child.HasExited)child.Kill(true);
   if(ct.IsCancellationRequested)throw;
   throw new UpgradeLinkLostException(AppText.Format("Upgrade.WorkerInterrupted",e.Message));
  }
 }
 public async ValueTask DisposeAsync()
 {
  var child=worker;if(child is null)return;
  try
  {
   if(!child.HasExited)
   {
    using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(5));
    try{await Request(new("close",TimeoutSeconds:3),timeout.Token);await child.WaitForExitAsync(timeout.Token);}
    catch(Exception){if(!child.HasExited)child.Kill(true);}
   }
   if(stderr is not null)await stderr;
  }
  finally{worker=null;stderr=null;child.Dispose();}
 }
}

