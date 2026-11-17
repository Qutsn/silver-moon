using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Middleton.Desktop;

namespace GlassPlayground;
public sealed partial class ConnectionPreviewWindow
{
 public bool PreviewMode {get;} = Environment.GetCommandLineArgs().Any(a=>a=="--preview" || a=="--connection-smoke");
 DeviceConnection liveConnection=null!;
 readonly CancellationTokenSource lifetime=new();
 bool liveError;
 bool scanning;
 DeviceCandidate? selectedCandidate;
 bool releasingForClose;
 bool OfficialSoundTrial=>!Environment.GetCommandLineArgs().Contains("--confirmed-sound");
 void InitializeLiveMode()
 {
  liveConnection=new(()=>new WindowsBleTransport(async action=>await Dispatcher.UIThread.InvokeAsync(action)),officialSoundTrial:OfficialSoundTrial);
  Title=PreviewMode?UiText.Get("LiveConnection.Text.884"):"Silver Moon";
  this.FindControl<FirmwareChooser>("FirmwareChooser")!.PreviewMode=PreviewMode;
  if(PreviewMode)return;
  this.FindControl<Border>("DiscoveryPanel")!.IsVisible=false;
  this.FindControl<TextBlock>("ConnectionInstruction")!.Width=640;
  this.FindControl<TextBlock>("ConnectionInstruction")!.TextWrapping=TextWrapping.Wrap;
  SetConnectionMessage(UiText.Get("LiveConnection.Text.1269"));
  this.FindControl<Button>("ConnectButton")!.Content=UiText.Get("LiveConnection.Text.1354");
  InitializeDiscoveryScrolling();
  this.FindControl<DeviceDetailsView>("DeviceDetails")!.SetSnapshot(null);
  this.FindControl<DeviceDetailsView>("DeviceDetails")!.ConfigureSoundControl(WriteSoundFromUiAsync,OfficialSoundTrial);
  liveConnection.Updated+=_=>QueueSnapshotRefresh();
 }
 int snapshotRefreshQueued;
 void QueueSnapshotRefresh()
 {
  if(Interlocked.Exchange(ref snapshotRefreshQueued,1)!=0)return;
  // Event-triggered batching, not a periodic poll. At most one pending UI refresh.
  Dispatcher.UIThread.Post(()=>DispatcherTimer.RunOnce(()=>
  {
   Interlocked.Exchange(ref snapshotRefreshQueued,0);
   if(motionClosed)return;
   var latest=liveConnection.Current;
   if(upgradeBusy)return;
   this.FindControl<DeviceDetailsView>("DeviceDetails")!.SetSnapshot(latest);
   if(Environment.GetEnvironmentVariable("MIDDLETON_TRACE_SOUND")=="1")DeviceLog.Write("sound-ui-snapshot",new{latest?.SessionId,latest?.Connected,latest?.Volume,latest?.Bass,latest?.Treble});
   if(latest is {Connected:false})
   {
    if(upgradeProgress?.State==InstallationState.Complete)RenderLiveUpgrade();
    this.FindControl<FirmwareChooser>("FirmwareChooser")!.LiveReady=false;
    SetConnectionMessage(UiText.Get("LiveConnection.Text.2504"));
    this.FindControl<FirmwareChooser>("FirmwareChooser")!.SetStatus(UiText.Get("LiveConnection.Text.2504"));
   }
  },TimeSpan.FromMilliseconds(50)));
 }
 async Task WriteSoundFromUiAsync(SoundSetting setting,int value)
 {
  if(upgradeBusy)throw new DeviceConnectionException(UiText.Get("LiveConnection.Text.2790"));
  var sessionId=liveConnection.Current?.SessionId;
  try
  {
   // These methods enqueue immediately and run Bluetooth work on the queue's worker.
   if(setting==SoundSetting.Volume)await liveConnection.SetVolumeAsync(value,lifetime.Token);
   else await liveConnection.SetEqAsync(setting==SoundSetting.Bass?value:null,setting==SoundSetting.Treble?value:null,lifetime.Token);
  }
  finally
  {
   if(!motionClosed && liveConnection.Current?.SessionId==sessionId)
    this.FindControl<DeviceDetailsView>("DeviceDetails")!.SetSnapshot(liveConnection.Current);
  }
 }
 void SetConnectionMessage(string text)=>TextMotion.Set(this.FindControl<TextBlock>("ConnectionInstruction")!,text);
 async Task<bool> ConnectLiveAsync()
 {
  try
  {
   liveError=false;
   var candidate=selectedCandidate;
   if(candidate is null){SetConnectionMessage(UiText.Get("LiveConnection.Text.3663"));return false;}
   this.FindControl<Border>("DiscoveryPanel")!.IsEnabled=false;
   SetConnectionMessage(UiText.Get("LiveConnection.Text.3786"));
   using var timeout=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);timeout.CancelAfter(TimeSpan.FromSeconds(60));
   var snapshot=await Task.Run(()=>liveConnection.ConnectAsync(candidate,timeout.Token));
   if(motionClosed)return false;
   this.FindControl<DeviceDetailsView>("DeviceDetails")!.SetSnapshot(snapshot);
   this.FindControl<FirmwareChooser>("FirmwareChooser")!.LiveReady=true;
   this.FindControl<FirmwareChooser>("FirmwareChooser")!.SetStatus("");
   DeviceLog.Write("identity-verified",new{snapshot.SessionId,snapshot.Model,snapshot.Firmware,snapshot.Hardware,snapshot.Battery,snapshot.Volume,snapshot.Bass,snapshot.Treble});
   return true;
  }
  catch(OperationCanceledException)
  {
   if(!motionClosed){liveError=true;SetConnectionMessage(UiText.Get("LiveConnection.Text.4642"));}
   return false;
  }
  catch(Exception e)
  {
   liveError=true;DeviceLog.Write("connection-failed",new{error=e.GetType().Name,hresult=e.HResult});
   if(!motionClosed)SetConnectionMessage(e is DeviceConnectionException?e.Message:UiText.Format("Connection.Failed",e.HResult));
   return false;
  }
  finally{this.FindControl<Border>("DiscoveryPanel")!.IsEnabled=true;}
 }
 async Task CloseLiveAsync()
 {
  lifetime.Cancel();
  if(upgradeTask is not null)try{await upgradeTask;}catch(Exception){}
  try{await liveConnection.DisposeAsync();}catch(Exception e){DeviceLog.Write("release-failed",new{error=e.GetType().Name});}
 }
 public async Task ReadOnlySmokeAsync()
 {
  if(PreviewMode)throw new InvalidOperationException("Read-only smoke cannot run in preview mode");
  await Task.Delay(500);
  while(scanning)await Task.Delay(100);
  var args=Environment.GetCommandLineArgs();var index=Array.IndexOf(args,"--smoke-device-name");
  if(index<0 || index+1>=args.Length)throw new InvalidOperationException("Specify --smoke-device-name for a real-device UI test");
  var matches=lastCandidates.Where(c=>c.Name==args[index+1]).ToArray();
  if(matches.Length!=1)throw new InvalidOperationException("Test device is absent or ambiguous");
  selectedCandidate=matches[0];
  var details=this.FindControl<DeviceDetailsView>("DeviceDetails")!;
  new DeviceDetailsView().CheckReadOnlyControls();
  await PlayConnection();
  if(liveConnection.Current is not {Connected:true} snapshot || !this.FindControl<Viewbox>("SelectionPage")!.IsVisible)throw new InvalidOperationException("Real connection did not reach selection");
  var chooser=this.FindControl<FirmwareChooser>("FirmwareChooser")!;
  if(!chooser.ActionTarget.IsEnabled)throw new InvalidOperationException("Verified connection did not enable the real firmware confirmation entry");
  chooser.Select(1);await Task.Delay(400);
  if(!chooser.ActionTarget.IsEnabled)throw new InvalidOperationException("Selection animation lost the real confirmation entry");
  await ShowDeviceDetails(chooser.ActionTarget,true);
  if(!this.FindControl<Viewbox>("DevicePage")!.IsVisible)throw new InvalidOperationException("Real details navigation failed");
  details.SetSnapshot(snapshot);
  var folder=System.IO.Path.Combine(DeviceLog.DirectoryPath,"ui-smoke");System.IO.Directory.CreateDirectory(folder);
  using(var bitmap=new Avalonia.Media.Imaging.RenderTargetBitmap(new Avalonia.PixelSize((int)Bounds.Width,(int)Bounds.Height)))
  {bitmap.Render(this);bitmap.Save(System.IO.Path.Combine(folder,"device-details.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);}
  await ReturnInterruptedTransfer(true);
  if(!this.FindControl<Viewbox>("SelectionPage")!.IsVisible)throw new InvalidOperationException("Return from live details failed");
  DeviceLog.Write("readonly-ui-smoke-passed",new{snapshot.SessionId,snapshot.Firmware});
  // Release before application shutdown, since desktop lifetime does not await Closed handlers.
  await liveConnection.DisposeAsync();
 }
}


