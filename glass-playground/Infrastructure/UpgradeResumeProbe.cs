using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Middleton.Desktop;
public static class UpgradeResumeProbe
{
 public static async Task<int> RunAsync(string[] args)
 {
  var events=new List<object>();string path=Path.Combine(AppContext.BaseDirectory,"upgrade-resume-probe.json");
  try
  {
   int index=Array.IndexOf(args,"--serial");if(index<0 || index+1>=args.Length)throw new ArgumentException("Specify --serial.");
   string serial=args[index+1];using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(60));var ct=timeout.Token;
   foreach(var candidate in await WindowsBleTransport.PairedCandidatesAsync(ct))
   {
    await using var link=new WindowsUpgradeProcessLink(candidate,serial);
    UpgradeIdentity identity;
    try{identity=await link.ReconnectAsync(ct);}catch(UpgradeProtocolException){continue;}
    events.Add(identity);
    async Task<GaiaAcknowledgement> Command(ushort command,byte[] data)
    {
     var ack=await link.CommandAsync(command,data,ct);events.Add(new{command=command.ToString("X4"),request=Convert.ToHexString(data),status=ack.Status,response=Convert.ToHexString(ack.Data.Span)});return ack;
    }
    var ps=await Command(0x0710,new byte[]{0,4});
    if(ps.Status!=0 || ps.Data.Length!=40)throw new UpgradeProtocolException("Cannot inspect PS4.");
    var context=UpgradeContext.Decode(ps.Data.Span.Slice(2));
    if(context.ImageId!="6e0b0188" || context.Resume!=0)throw new UpgradeProtocolException("Probe requires currently staged r2 in START.");
    if((await Command(0x4001,new byte[]{18})).Status!=0)throw new UpgradeProtocolException("Registration rejected.");
    var opened=await Command(0x0640,Array.Empty<byte>());
    if(opened.Status!=0 && opened.Status!=6)throw new UpgradeProtocolException("Unexpected connect result.");
    foreach(byte code in new byte[]{19,1,21})
    {
     var frame=UpgradeWire.Control(code,code==19?Convert.FromHexString(context.ImageId):Array.Empty<byte>());
     if((await Command(0x0642,frame.AsSpan(4).ToArray())).Status!=0)throw new UpgradeProtocolException("Control rejected.");
     var message=await link.ReceiveAsync(TimeSpan.FromSeconds(10),ct);
     events.Add(new{message.Code,body=Convert.ToHexString(message.Body.Span)});
     if(code==19)UpgradeWire.Require(message,20,6);
     if(code==1 && (message.Code!=2 || message.Body.Length==0 || message.Body.Span[0]!=0))throw new UpgradeProtocolException("Start rejected.");
     if(code==21)
     {
      var image=File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Firmware","r2.bin"));
      if(FirmwareCatalog.Analyze(image).Known?.Id!="middleton-r2")throw new UpgradeProtocolException("Diagnostic package mismatch.");
      int cursor=0,totalSent=0;
      for(int i=0;i<16;i++)
      {
       var request=UpgradeWire.DataRequest(message);events.Add(new{cursor,request.Count,request.Skip});
       if(request.Count>512 || totalSent+request.Count>1024 || (long)cursor+request.Skip>119381)break;
       cursor=checked(cursor+(int)request.Skip);
       int end=cursor+(int)request.Count;
       while(cursor<end)
       {
        int count=Math.Min(45,end-cursor);var payload=new byte[count+1];Array.Copy(image,cursor,payload,1,count);
        var dataFrame=UpgradeWire.Control(4,payload);
        if((await Command(0x0642,dataFrame.AsSpan(4).ToArray())).Status!=0)throw new UpgradeProtocolException("Header rejected.");
        cursor+=count;totalSent+=count;
       }
       message=await link.ReceiveAsync(TimeSpan.FromSeconds(10),ct);events.Add(new{message.Code,body=Convert.ToHexString(message.Body.Span)});
      }
     }
    }
    // Only replay bounded, identical container headers; no partition payload, ABORT or PS writes.
    File.WriteAllText(path,JsonSerializer.Serialize(new{success=true,events}));return 0;
   }
   throw new InvalidOperationException("Device unavailable.");
  }
  catch(Exception e){File.WriteAllText(path,JsonSerializer.Serialize(new{success=false,error=e.ToString(),events}));return 1;}
 }
}
