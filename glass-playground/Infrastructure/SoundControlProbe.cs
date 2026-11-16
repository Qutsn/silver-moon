using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Middleton.Desktop;
// Explicit local acceptance tool; never called by opening a page or starting the app normally.
public static class SoundControlProbe
{
 public static async Task<int> RunAsync(string[] args)
 {
  var index=Array.IndexOf(args,"--probe-report");
  if(index<0 || index+1>=args.Length)return 2;
  var rows=new List<object>();bool success=false,restored=false;
  void Log(string stage,object data){rows.Add(new{time=DateTimeOffset.UtcNow,stage,data});}
  await using var connection=new DeviceConnection(()=>new WindowsBleTransport());
  DeviceSnapshot? original=null;bool attempted=false;
  using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(100));
  try
  {
   var paired=await WindowsBleTransport.PairedCandidatesAsync(deadline.Token);
   var candidates=paired.Any(c=>c.Name.Contains("MIDDLETON",StringComparison.OrdinalIgnoreCase))?paired:await connection.ScanAsync(deadline.Token);
   var matches=candidates.Where(c=>c.Name.Contains("MIDDLETON",StringComparison.OrdinalIgnoreCase)).ToArray();
   if(matches.Length!=1)throw new Exception("No unique test candidate; do not choose automatically.");
   original=await connection.ConnectAsync(matches[0],deadline.Token);
   if(original.Model!="MIDDLETON" || original.Serial!="0187891")throw new Exception("Not the authorized test speaker.");
   if(original.Volume is null || original.Bass is null || original.Treble is null)throw new Exception("Original settings unavailable.");
   if(original.Volume==0 || original.Bass==0 || original.Treble==0)throw new Exception("Zero original needs a dedicated restoration path; no writes performed.");
   Log("before",new{original.Volume,original.Bass,original.Treble,original.Firmware});
   // Prefer a one-step decrease in volume; never test at the maximum volume.
   int volume=original.Volume>1?original.Volume.Value-1:2;
   int bass=original.Bass>1?original.Bass.Value-1:2;
   int treble=original.Treble>1?original.Treble.Value-1:2;
   attempted=true;
   var v=await connection.SetVolumeAsync(volume,deadline.Token);Log("volume-confirmed",new{v.Volume});
   var b=await connection.SetEqAsync(bass,null,deadline.Token);Log("bass-confirmed",new{b.Bass,b.Treble});
   if(b.Treble!=original.Treble)throw new Exception("Treble preservation failed");
   var t=await connection.SetEqAsync(null,treble,deadline.Token);Log("treble-confirmed",new{t.Bass,t.Treble});
   if(t.Bass!=bass)throw new Exception("Bass preservation failed");
   success=true;
  }
  catch(Exception e){Log("failure",new{e.Message});}
  finally
  {
   if(attempted && original is not null)
   {
    using var restore=new CancellationTokenSource(TimeSpan.FromSeconds(35));
    // Separate attempts so one failure does not prevent restoring the other setting.
    bool volumeOk=false,eqOk=false;
    try{var v=await connection.SetVolumeAsync(original.Volume!.Value,restore.Token);volumeOk=v.Volume==original.Volume;Log("volume-restored",new{v.Volume});}catch(Exception e){Log("volume-restore-failed",new{e.Message});}
    try{var eq=await connection.SetEqAsync(original.Bass,original.Treble,restore.Token);eqOk=eq.Bass==original.Bass && eq.Treble==original.Treble;Log("eq-restored",new{eq.Bass,eq.Treble});}catch(Exception e){Log("eq-restore-failed",new{e.Message});}
    restored=volumeOk && eqOk;
   }
   Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[index+1]))!);
   await File.WriteAllTextAsync(args[index+1],JsonSerializer.Serialize(new{success=success && restored,restored,rows},new JsonSerializerOptions{WriteIndented=true}));
  }
  return success && restored?0:1;
 }
}

