using System;
using System.Threading.Tasks;
using Middleton.Desktop;

namespace GlassPlayground;
public sealed partial class DeviceDetailsView
{
 Func<SoundSetting,int,Task>? soundWriter;
 DeviceSnapshot? liveSnapshot;
 string? soundError;
 bool officialSoundTrial;
 public void ConfigureSoundControl(Func<SoundSetting,int,Task> writer,bool officialSoundTrial=false)
 {
  soundWriter=writer;this.officialSoundTrial=officialSoundTrial;
  volume.ImmediateFeedback=bass.ImmediateFeedback=treble.ImmediateFeedback=officialSoundTrial;
  volume.Requested=delta=>_ = RequestSoundAsync(volume,SoundSetting.Volume,delta);
  bass.Requested=delta=>_ = RequestSoundAsync(bass,SoundSetting.Bass,delta);
  treble.Requested=delta=>_ = RequestSoundAsync(treble,SoundSetting.Treble,delta);
  SetSnapshot(liveSnapshot);
 }
 async Task RequestSoundAsync(DemoStepper control,SoundSetting setting,int delta)
 {
  if(soundWriter is null || liveSnapshot is not {Connected:true} snapshot)return;
  var request=control.LiveState.Request(delta);if(request is null)return;
  soundError=null;control.RenderLive();UpdateSoundStatus();
  try{await soundWriter(setting,request.Value.Target);}
  catch(Exception e)
  {
   if(liveSnapshot?.SessionId==snapshot.SessionId && control.LiveState.Revision==request.Value.Revision)
    soundError=e is DeviceConnectionException?e.Message:AppText.Get("Device.Message.15");
   DeviceLog.Write("sound-ui-write-failed",new{setting=setting.ToString(),error=e.GetType().Name});
  }
  finally
  {
   if(liveSnapshot?.SessionId==snapshot.SessionId && control.LiveState.Complete(request.Value.Revision))
   {control.RenderLive();UpdateSoundStatus();}
  }
 }
 void UpdateSoundStatus()
 {
  string message=liveSnapshot is not {Connected:true}
   ?liveSnapshot is null?AppText.Get("DeviceDetailsView.Text.2626"):AppText.Get("DeviceDetailsView.Text.2643")
   :soundError ?? (volume.LiveState.Target.HasValue || bass.LiveState.Target.HasValue || treble.LiveState.Target.HasValue
    ?(officialSoundTrial?UiText.Get("Sound.Sending"):AppText.Get("Device.Message.16"))
    :liveSnapshot.Volume is null || liveSnapshot.Bass is null || liveSnapshot.Treble is null?AppText.Get("DeviceDetailsView.Text.2576"):officialSoundTrial?"":AppText.Get("DeviceDetailsView.Text.2593"));
  TextMotion.Set(connectionState,message);
  Avalonia.Controls.ToolTip.SetTip(connectionState,message);
 }
}
