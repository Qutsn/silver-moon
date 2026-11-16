using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Interactivity;
using Avalonia.Controls;
using Middleton.Desktop;

namespace GlassPlayground;
public sealed partial class DeviceDetailsView
{
 public async Task CheckLiveControlsAsync()
 {
  static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
  var snapshot=new DeviceSnapshot(Guid.NewGuid(),true,"Test","MIDDLETON",null,null,null,80,17,2,8,new(true,true,true,true));
  var requests=new List<(SoundSetting Kind,int Value,TaskCompletionSource Done)>();
  ConfigureSoundControl((kind,value)=>{var done=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);requests.Add((kind,value,done));return done.Task;});
  SetSnapshot(snapshot);
  volume.Plus.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));volume.Plus.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  Check(requests.Count==2 && requests[0].Value==18 && requests[1].Value==19 && volume.Value==17,"Real buttons must accumulate targets without fabricating device values");
  SetSnapshot(snapshot with {Volume=18});requests[0].Done.SetResult();await Task.Delay(25);
  Check(volume.LiveState.Target==19,"First completion erased second click");
  snapshot=snapshot with {Volume=19};SetSnapshot(snapshot);requests[1].Done.SetResult();await Task.Delay(25);
  Check(volume.Value==19 && volume.LiveState.Target is null,"Confirmed button result missing");
  var count=requests.Count;snapshot=snapshot with {Volume=0,Bass=0,Treble=10};SetSnapshot(snapshot);
  Check(volume.Value==0 && volume.Level(0)==0 && !volume.Minus.IsEnabled && volume.Plus.IsEnabled && bass.Value==0 && treble.Value==10,"External zero and boundaries must update");
  Check(requests.Count==count,"Notification echoed a device write");
  volume.Plus.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(requests[^1].Value==1,"Zero increment must target one");
  requests[^1].Done.SetException(new DeviceConnectionException("test rejected"));await Task.Delay(25);
  Check(volume.Value==0 && volume.LiveState.Target is null && soundError is not null,"Failed write must retain actual value and show error");
  snapshot=snapshot with {Volume=17,Bass=2,Treble=8};SetSnapshot(snapshot);
  bass.Plus.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(requests[^1].Kind==SoundSetting.Bass && requests[^1].Value==3,"Bass button routing failed");
  var stale=requests[^1].Done;SetSnapshot(snapshot with {Connected=false});stale.SetResult();await Task.Delay(25);
  Check(!volume.Plus.IsEnabled && !bass.Plus.IsEnabled && volume.LiveState.Target is null,"Disconnect must disable controls and discard targets");
  var newer=snapshot with {SessionId=Guid.NewGuid(),Volume=4};SetSnapshot(newer);
  treble.Minus.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(requests[^1].Kind==SoundSetting.Treble && requests[^1].Value==7,"Treble button routing failed");
  requests[^1].Done.SetResult();await Task.Delay(25);
  Check(volume.Value==4,"Old callback changed new session");
  // Trial mode is opt-in at the view boundary; retain confirmed-mode checks above.
  ConfigureSoundControl((kind,target)=>{
   snapshot=snapshot with {Volume=target};SetSnapshot(snapshot);return Task.CompletedTask;
  },officialSoundTrial:true);
  snapshot=snapshot with {SessionId=Guid.NewGuid(),Volume=4,Connected=true};SetSnapshot(snapshot);
  volume.Plus.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(25);
  Check(volume.Value==5,"Trial button must show target immediately");
  await Task.Delay(350);
  Check(connectionState.Text=="","Removed trial hint must stay absent after snapshot callbacks");
  SetSnapshot(snapshot with {Volume=0});
  Check(volume.Value==0,"Device zero notification must replace trial target");
  snapshot=snapshot with {Volume=1,Bass=1,Treble=1};SetSnapshot(snapshot);
  Check(volume.Minus.IsEnabled && !bass.Minus.IsEnabled && !treble.Minus.IsEnabled,"Only volume may decrease from one");
  volume.Minus.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(25);
  Check(volume.Value==0 && !volume.Minus.IsEnabled,"Actual volume button must reach and stop at zero");
  DeviceLog.Write("sound-ui-smoke-passed",new{requestCount=requests.Count});
 }
}
