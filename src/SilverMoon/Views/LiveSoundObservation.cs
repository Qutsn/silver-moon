using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Middleton.Desktop;

namespace GlassPlayground;
public sealed partial class ConnectionPreviewWindow
{
 public async Task ObserveLiveSoundAsync()
 {
  var args=Environment.GetCommandLineArgs();var nameIndex=Array.IndexOf(args,"--smoke-device-name");
  if(nameIndex<0 || nameIndex+1>=args.Length)throw new InvalidOperationException("Specify test speaker name");
  var paired=await WindowsBleTransport.PairedCandidatesAsync(lifetime.Token);
  var matches=paired.Where(c=>c.Name.Contains("MIDDLETON",StringComparison.OrdinalIgnoreCase)).ToArray();
  if(matches.Length!=1)throw new InvalidOperationException("No unique paired test speaker");
  selectedCandidate=matches[0];
  if(!await ConnectLiveAsync())throw new InvalidOperationException("Live connection failed");
  if(liveConnection.Current?.Model!="MIDDLETON" || liveConnection.Current?.Firmware is null)throw new InvalidOperationException("Not a verified Middleton device");
  ShowSelection(true);await ShowDeviceDetails(null,true);
  var snapshot=liveConnection.Current!;
  DeviceLog.Write("sound-ui-ready",new{snapshot.SessionId,snapshot.Volume,snapshot.Bass,snapshot.Treble});
 }
}


