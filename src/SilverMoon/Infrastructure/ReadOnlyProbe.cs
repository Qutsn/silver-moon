using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Middleton.Desktop;
public static class ReadOnlyProbe
{
 public static async Task<int> RunAsync(string[] args)
 {
  var reportIndex=Array.IndexOf(args,"--probe-report");
  string path=reportIndex>=0 && reportIndex+1<args.Length?args[reportIndex+1]:Path.Combine(DeviceLog.DirectoryPath,"probe.json");
  object report;int exitCode;
  try
  {
   using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(90));
   await using var connection=new DeviceConnection(()=>new WindowsBleTransport());
   var candidates=await connection.ScanAsync(timeout.Token);
   var nameIndex=Array.IndexOf(args,"--probe-name");
   if(nameIndex>=0 && nameIndex+1<args.Length)candidates=candidates.Where(c=>c.Name==args[nameIndex+1]).ToArray();
   if(candidates.Count!=1)throw new DeviceConnectionException(AppText.Format("Probe.Candidates",candidates.Count));
   var snapshot=await connection.ConnectAsync(candidates.Single(),timeout.Token);
   // Observe notifications briefly without sending settings or upgrade commands.
   await Task.Delay(2000,timeout.Token);
   if(connection.Current is not {Connected:true})throw new DeviceConnectionException(AppText.Get("Device.Message.21"));
   report=new{time=DateTimeOffset.UtcNow,success=true,mode="read-only",snapshot.SessionId,snapshot.Model,snapshot.Firmware,snapshot.Hardware,snapshot.Battery,snapshot.Volume,snapshot.Bass,snapshot.Treble,serialAvailable=!string.IsNullOrEmpty(snapshot.Serial),snapshot.Capabilities};exitCode=0;
  }
  catch(Exception e){report=new{time=DateTimeOffset.UtcNow,success=false,mode="read-only",error=e.Message,type=e.GetType().Name,hresult=e.HResult};exitCode=1;}
  Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
  await File.WriteAllTextAsync(path,JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
  return exitCode;
 }
}
