using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Middleton.Desktop;
public static class UpgradeReadOnlyProbe
{
 public static async Task<int> RunAsync(string[] args)
 {
  string report=Path.Combine(AppContext.BaseDirectory,"upgrade-readonly-probe.json");
  try
  {
   int index=Array.IndexOf(args,"--serial");if(index<0 || index+1>=args.Length)throw new ArgumentException("Specify --serial.");
   string serial=args[index+1];using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(90));
   Exception? last=null;
   foreach(var candidate in await WindowsBleTransport.PairedCandidatesAsync(deadline.Token))
   {
    await using var link=new WindowsUpgradeProcessLink(candidate,serial);
    try
    {
     var identity=await link.ReconnectAsync(deadline.Token);
     var ack=await link.CommandAsync(0x0710,new byte[]{0,4},deadline.Token);
     if(ack.Data.Length!=40 || ack.Data.Span[0]!=0 || ack.Data.Span[1]!=4)throw new UpgradeProtocolException("Invalid PS4 response.");
     var context=UpgradeContext.Decode(ack.Data.Span.Slice(2));
     await link.ReconnectAsync(deadline.Token);
     var independent=await link.CommandAsync(0x0710,new byte[]{0,4},deadline.Token);
     if(!independent.Data.Span.SequenceEqual(ack.Data.Span))throw new UpgradeProtocolException("Independent PS4 changed.");
     File.WriteAllText(report,JsonSerializer.Serialize(new{success=true,identity,context.IsIdle,context.ImageId,context.Resume,context.LastClosed,ps4=Convert.ToHexString(ack.Data.Span),independentReadback=true}));return 0;
    }
    catch(Exception e){last=e;}
   }
   throw last??new InvalidOperationException("No paired devices found.");
  }
  catch(Exception e){File.WriteAllText(report,JsonSerializer.Serialize(new{success=false,error=e.ToString()}));return 1;}
 }
}
