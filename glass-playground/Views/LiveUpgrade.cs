using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Threading;
using Middleton.Desktop;

namespace GlassPlayground;
public sealed partial class ConnectionPreviewWindow
{
 bool upgradeBusy;
 Task? upgradeTask;
 byte[]? upgradeImage;
 DeviceSnapshot? upgradeDevice;
 UpgradeProgress? upgradeProgress;
 readonly Stopwatch upgradeElapsed=new();
 bool upgradeDisplayRunning;
 int upgradeFrameGeneration;
 readonly Stopwatch liveMotionClock=Stopwatch.StartNew();
 InstallationState? renderedPhase;
 double renderedPhaseStart;
 void StartUpgradeFrames()
 {
  upgradeDisplayRunning=true;int generation=++upgradeFrameGeneration;
  void Frame(TimeSpan _)
  {
   if(motionClosed || !upgradeDisplayRunning || generation!=upgradeFrameGeneration)return;
   RenderLiveUpgrade();if(upgradeDisplayRunning)RequestAnimationFrame(Frame);
  }
  RequestAnimationFrame(Frame);
 }
 void StopUpgradeFrames(){upgradeDisplayRunning=false;upgradeFrameGeneration++;}
 readonly MetricTransition speedTransition=new(),remainingTransition=new();
 readonly TransferRate transferRate=new();
 InstallationState lastUpgradePhase=InstallationState.Transferring;
 bool liveWaiting;
 readonly Stopwatch liveWaitClock=new();
 double nextMetricRefresh;
 string displayedSpeed="—",displayedRemaining="—";
 public async Task BeginLiveUpgradeAsync(byte[] image,FirmwareRiskWindow dialog)
 {
  if(PreviewMode || upgradeBusy)return;
  var snapshot=liveConnection.Current??upgradeDevice;
  if(snapshot is null || selectedCandidate is null || string.IsNullOrWhiteSpace(snapshot.Serial))throw new InvalidOperationException(UiText.Get("LiveUpgrade.Text.1688"));
  var descriptor=FirmwareCatalog.Analyze(image).Installable??throw new InvalidOperationException(UiText.Get("LiveUpgrade.Text.1797"));
  upgradeDevice=snapshot;upgradeImage=(byte[])image.Clone();
  // A new install must not inherit the completed phase or counters of the last one.
  upgradeProgress=null;lastUpgradePhase=InstallationState.Transferring;upgradeElapsed.Reset();
  await ShowTransfer($"{descriptor.DisplayName} · {descriptor.DisplayVersion}",dialog,descriptor.Strategy==FirmwareStrategy.OfficialFull?1:0);
  StartLiveUpgrade();
 }
 void StartLiveUpgrade()
 {
  if(upgradeBusy || upgradeImage is null || upgradeDevice is null || selectedCandidate is null)return;
  upgradeBusy=true;renderedPhase=null;transferRate.Reset();nextMetricRefresh=0;displayedSpeed=displayedRemaining="—";liveWaiting=false;ResetInterruption();if(!upgradeElapsed.IsRunning)upgradeElapsed.Start();
  
  this.FindControl<FirmwareChooser>("FirmwareChooser")!.LiveReady=false;
  this.FindControl<Button>("RetryTransferButton")!.IsVisible=false;
  this.FindControl<Button>("TransferBack")!.IsEnabled=false;
  this.FindControl<DeviceDetailsView>("DeviceDetails")!.SetSnapshot(null);
  var savedProgress=upgradeProgress;
  upgradeProgress=new(InstallationState.ResultUnknown,UiText.Get("LiveUpgrade.Text.2748"),savedProgress?.Cursor??0,savedProgress?.Total??upgradeImage.Length,savedProgress?.AcknowledgedBytes??0,savedProgress?.SkippedBytes??0);
  RenderLiveUpgrade();StartUpgradeFrames();
  upgradeTask=RunLiveUpgradeAsync();
 }
 async Task RunLiveUpgradeAsync()
 {
  bool success=false;
  try
  {
   // Drain/cancel daily writes and release all its subscriptions before opening the upgrade owner.
   await liveConnection.DisposeAsync();
   var image=upgradeImage!;var device=upgradeDevice!;var candidate=selectedCandidate!;
   await Task.Run(async()=>
   {
    var descriptor=FirmwareCatalog.Analyze(image).Installable!;
    using var store=new UpgradeJournalStore(device.Serial!,descriptor.Sha256);
    var journal=store.Load(device.Serial!,descriptor.Sha256);store.Save(journal);
    upgradeProgress=new(InstallationState.ResultUnknown,UiText.Get("LiveUpgrade.Text.3589"),journal.ConfirmedFrontier,image.Length,journal.AcknowledgedBytes,0);
    DeviceLog.Write("upgrade-start",new{journal.OperationId,journal.Serial,journal.Sha256,journal.ConfirmedFrontier});
    await using var link=new WindowsUpgradeProcessLink(candidate,device.Serial!);
    var coordinator=new ResumableUpgrade(image,link,journal,store.Save,progress=>
    {
     var previous=Interlocked.Exchange(ref upgradeProgress,progress);
     if(previous?.State!=progress.State)DeviceLog.Write("upgrade-stage",new{journal.OperationId,progress});
    });
    await coordinator.RunAsync(lifetime.Token);
    DeviceLog.Write("upgrade-complete",journal);
   });
   success=true;
  }
  catch(Exception e)
  {
   DeviceLog.Write("upgrade-stopped",new{error=e.ToString()});
   var prior=upgradeProgress;
   upgradeProgress=new(InstallationState.ResultUnknown,e is OperationCanceledException?UiText.Get("LiveUpgrade.Text.4475"):e.Message,prior?.Cursor??0,prior?.Total??upgradeImage!.Length,prior?.AcknowledgedBytes??0,prior?.SkippedBytes??0);
  }
  finally
  {
   upgradeBusy=false;if(!success)StopUpgradeFrames();upgradeElapsed.Stop();
   if(!motionClosed)
   {
    RenderLiveUpgrade();
    this.FindControl<Button>("TransferBack")!.IsEnabled=true;
    this.FindControl<Button>("RetryTransferButton")!.IsVisible=!success;
    this.FindControl<Button>("RetryTransferButton")!.IsEnabled=true;
    this.FindControl<FirmwareChooser>("FirmwareChooser")!.LiveReady=true;
   }
  }
  if(success && !motionClosed)
  {
   try
   {
    liveConnection=new(()=>new WindowsBleTransport(async action=>await Dispatcher.UIThread.InvokeAsync(action)),officialSoundTrial:OfficialSoundTrial);
    liveConnection.Updated+=_=>QueueSnapshotRefresh();
    using var timeout=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);timeout.CancelAfter(TimeSpan.FromSeconds(45));
    var snapshot=await Task.Run(()=>liveConnection.ConnectAsync(selectedCandidate!,timeout.Token));
    this.FindControl<DeviceDetailsView>("DeviceDetails")!.SetSnapshot(snapshot);
   }
   catch(Exception e){this.FindControl<FirmwareChooser>("FirmwareChooser")!.SetStatus(UiText.Format("Upgrade.DetailsReconnectFailed",e.Message));}
  }
 }
 void RenderLiveUpgrade()
 {
  if(motionClosed || upgradeProgress is not { } p)return;
  double fraction=p.Total>0?Math.Clamp((double)p.Cursor/p.Total,0,1):0;
  if(p.State!=InstallationState.ResultUnknown)lastUpgradePhase=p.State;
  var shown=p.State==InstallationState.ResultUnknown?lastUpgradePhase:p.State;
  double motion=liveMotionClock.Elapsed.TotalSeconds;
  // Transient link loss does not restart the same phase's entrance animation.
  if(p.State!=InstallationState.ResultUnknown && renderedPhase!=p.State){renderedPhase=p.State;renderedPhaseStart=motion;}
  double phaseAge=motion-renderedPhaseStart;
  double visual=shown switch{InstallationState.Transferring=>Math.Min(29.99,30*fraction),InstallationState.Validating=>33,InstallationState.Rebooting=>36,InstallationState.Committing=>39,InstallationState.Complete=>42.5,_=>41};
  if(p.State==InstallationState.ResultUnknown)
  {
   if(!liveWaiting){liveWaiting=true;liveWaitClock.Restart();frozenTransferSeconds=visual;interrupted=true;recovering=retrying=false;rotationOrigin=0;}
   // Reuse the approved interruption surface rather than combining phase panels
   // with the still-visible transfer percentage and track.
   UpdateInterruption(upgradeBusy?Math.Min(29,liveWaitClock.Elapsed.TotalSeconds):31,p.Message);

   this.FindControl<TextBlock>("TransferElapsed")!.Text=upgradeElapsed.Elapsed.ToString(@"hh\:mm\:ss");
   transferRate.Reset();nextMetricRefresh=0;displayedSpeed=displayedRemaining="—";
   return;
  }
  if(liveWaiting)
  {
   if(visual>=30)
   {
    // Reconnection during validation/boot is not a return to file transfer.
    // Keep the existing phase layout; only its live explanation changes.
    liveWaiting=false;liveWaitClock.Reset();ResetInterruption();
    this.FindControl<Button>("TransferBack")!.IsVisible=true;
   }
   else
   {
   if(!recovering){recovering=true;retrying=false;recoveryX=this.FindControl<ConnectionFocus>("PhaseSpinner")!.Center.X;recoverySplit=this.FindControl<ConnectionFocus>("PhaseSpinner")!.Radius/36;liveWaitClock.Restart();frozenTransferSeconds=visual;}
   UpdateInterruption(liveWaitClock.Elapsed.TotalSeconds);
   if(liveWaitClock.Elapsed.TotalSeconds<.65)return;
   liveWaiting=false;liveWaitClock.Reset();ResetInterruption();this.FindControl<Button>("TransferBack")!.IsVisible=true;
   }
  }
  if(p.State==InstallationState.Validating)visual=30+Math.Min(3,phaseAge);
  if(p.State==InstallationState.Complete)visual=42+Math.Min(.5,phaseAge);
  UpdateTransfer(visual,p.State==InstallationState.Complete,motion);
  if(p.State==InstallationState.Complete && phaseAge>=.5)StopUpgradeFrames();
  this.FindControl<TextBlock>("TransferElapsed")!.Text=upgradeElapsed.Elapsed.ToString(@"hh\:mm\:ss");
  double? rate=transferRate.Observe(upgradeElapsed.Elapsed.TotalSeconds,p.AcknowledgedBytes,p.State==InstallationState.Transferring && upgradeBusy);
  if(p.State!=InstallationState.Transferring){displayedSpeed=displayedRemaining="—";nextMetricRefresh=0;}
  else if(rate is not null && upgradeElapsed.Elapsed.TotalSeconds>=nextMetricRefresh)
  {
   displayedSpeed=$"{rate.Value/1024:F2} KB/s";
   displayedRemaining=rate>0?TimeSpan.FromSeconds(Math.Min(359999,Math.Max(0,p.Total-p.Cursor)/rate.Value)).ToString(@"hh\:mm\:ss"):"—";
   nextMetricRefresh=upgradeElapsed.Elapsed.TotalSeconds+2;
  }
  speedTransition.Render(this.FindControl<TextBlock>("TransferSpeed")!,displayedSpeed,motion);
  remainingTransition.Render(this.FindControl<TextBlock>("TransferRemaining")!,displayedRemaining,motion);
  this.FindControl<StackPanel>("PhaseInformation")!.Opacity=1;
  if(p.State!=InstallationState.Complete)this.FindControl<StackPanel>("PhaseEvidence")!.Opacity=1;
  TextMotion.Set(this.FindControl<TextBlock>("TransferTitle")!,p.State==InstallationState.Complete?UiText.Get("LiveUpgrade.Text.9078"):p.State==InstallationState.Transferring?UiText.Get("LiveUpgrade.Text.9125"):p.State==InstallationState.Validating?UiText.Get("LiveUpgrade.Text.9172"):UiText.Get("LiveUpgrade.Text.9181"));
  // Retry may resume from a device checkpoint. Do not use total historical ACK bytes as current throughput.
  TextMotion.Set(this.FindControl<TextBlock>("PhaseHeadline")!,p.State==InstallationState.Transferring?UiText.Get("LiveUpgrade.Text.9125"):p.State==InstallationState.Complete?UiText.Get("LiveUpgrade.Text.9450"):upgradeBusy?UiText.Get("LiveUpgrade.Text.9472"):UiText.Get("LiveUpgrade.Text.9481"));
  TextMotion.Set(this.FindControl<TextBlock>("PhaseExplanation")!,p.Message);
  TextMotion.Set(this.FindControl<TextBlock>("PhaseNext")!,p.State==InstallationState.Complete?UiText.Get("Upgrade.CompleteEvidence"):UiText.Format("Upgrade.TransferredEvidence",p.AcknowledgedBytes,p.SkippedBytes));
  this.FindControl<Button>("TransferBack")!.Content=UiText.Get("LiveUpgrade.Text.9815");
  if(p.State==InstallationState.Transferring)
   this.FindControl<TextBlock>("TransferPercent")!.Text=((int)(fraction*100)).ToString();
  if(!upgradeBusy && p.State!=InstallationState.Complete)
  {
   this.FindControl<ConnectionFocus>("PhaseSpinner")!.IsVisible=false;
   this.FindControl<Button>("SpeakerDetailsButton")!.IsVisible=false;
   this.FindControl<StackPanel>("PhaseInformation")!.Opacity=1;
   this.FindControl<StackPanel>("PhaseEvidence")!.Opacity=1;
  }
  this.FindControl<Button>("TransferBack")!.IsEnabled=!upgradeBusy;
 }
 async Task CheckLiveUpgradePresentationAsync()
 {
  ShowOnlyPage("TransferPage");
  upgradeBusy=true;upgradeElapsed.Restart();
  upgradeProgress=new(InstallationState.Transferring,UiText.Get("LiveUpgrade.Text.10545"),100,1000,100,0);RenderLiveUpgrade();
  await Task.Delay(1100);
  upgradeProgress=upgradeProgress with{Cursor=200,AcknowledgedBytes=200};RenderLiveUpgrade();
  await Task.Delay(300);RenderLiveUpgrade();
  string? speed=this.FindControl<TextBlock>("TransferSpeed")!.Text;
  string? remaining=this.FindControl<TextBlock>("TransferRemaining")!.Text;
  upgradeProgress=upgradeProgress with{Cursor=900,AcknowledgedBytes=900};RenderLiveUpgrade();
  if(speed=="—" || this.FindControl<TextBlock>("TransferSpeed")!.Text!=speed || this.FindControl<TextBlock>("TransferRemaining")!.Text!=remaining)
   throw new InvalidOperationException("Transfer metrics must hold between two-second refreshes.");
  upgradeProgress=upgradeProgress with{State=InstallationState.ResultUnknown,Message=UiText.Get("LiveUpgrade.Text.11322")};RenderLiveUpgrade();
  await Task.Delay(700);RenderLiveUpgrade();
  foreach(string name in new[]{"TransferPercent","TransferPercentSign","TransferTrack","TransferFill"})
   if(this.FindControl<Control>(name)!.Opacity!=0)throw new InvalidOperationException("Interruption overlaps transfer values: "+name);
  if(this.FindControl<TextBlock>("TransferStatus")!.IsVisible || this.FindControl<StackPanel>("PhaseEvidence")!.IsVisible || !this.FindControl<ConnectionFocus>("PhaseSpinner")!.IsVisible)
   throw new InvalidOperationException("Live interruption diverges from the approved waiting layout.");
  upgradeBusy=false;RenderLiveUpgrade();
  if(!this.FindControl<Button>("RetryTransferButton")!.IsHitTestVisible)throw new InvalidOperationException("Stopped transfer cannot retry.");
  upgradeBusy=true;upgradeProgress=upgradeProgress with{State=InstallationState.Transferring};RenderLiveUpgrade();
  await Task.Delay(700);RenderLiveUpgrade();
  if(interrupted || this.FindControl<StackPanel>("PhaseInformation")!.IsVisible || !this.FindControl<Ellipse>("TransferTip")!.IsVisible)
   throw new InvalidOperationException("Recovered transfer did not restore original layout.");
  double breath=this.FindControl<StackPanel>("TransferStage0")!.Opacity;
  await Task.Delay(150);RenderLiveUpgrade();
  bool breathChanged=Math.Abs(breath-this.FindControl<StackPanel>("TransferStage0")!.Opacity)>=.001;
  await Task.Delay(150);RenderLiveUpgrade();
  breathChanged|=Math.Abs(breath-this.FindControl<StackPanel>("TransferStage0")!.Opacity)>=.001;
  if(!breathChanged)throw new InvalidOperationException("Live stage breathing is frozen.");
  upgradeProgress=upgradeProgress with{State=InstallationState.Rebooting};RenderLiveUpgrade();
  var phaseRing=this.FindControl<ConnectionFocus>("PhaseSpinner")!;
  if(Math.Abs(phaseRing.Center.X-1212)>.01 || Math.Abs(phaseRing.Center.Y-585.5)>.01)throw new InvalidOperationException("Phase ring alignment mismatch.");
  double angle=phaseRing.Angle;
  await Task.Delay(100);RenderLiveUpgrade();
  if(angle==this.FindControl<ConnectionFocus>("PhaseSpinner")!.Angle || this.FindControl<Button>("SpeakerDetailsButton")!.IsVisible)throw new InvalidOperationException("Reboot spinner frozen or completion button shown early.");
  // Repeated final-stage link losses must never resurrect the transfer surface.
  foreach(var phase in new[]{InstallationState.Rebooting,InstallationState.Committing,InstallationState.ReadingBack})
  {
   upgradeProgress=upgradeProgress with{State=phase};RenderLiveUpgrade();
   upgradeProgress=upgradeProgress with{State=InstallationState.ResultUnknown};RenderLiveUpgrade();
   AssertFinalStage();
   await Task.Delay(150);RenderLiveUpgrade();AssertFinalStage();
   upgradeProgress=upgradeProgress with{State=phase};RenderLiveUpgrade();AssertFinalStage();
   await Task.Delay(700);RenderLiveUpgrade();AssertFinalStage();
  }
  void AssertFinalStage()
  {
   foreach(string name in new[]{"TransferPercent","TransferPercentSign","TransferTrack","TransferFill"})
   {
    var control=this.FindControl<Control>(name)!;
    if(control.IsVisible && control.Opacity>.001)throw new InvalidOperationException("Final-stage reconnect exposed transfer UI: "+name);
   }
   for(int i=0;i<2;i++)
    if(Math.Abs(this.FindControl<StackPanel>("TransferStage"+i)!.Opacity-.85)>.001)
     throw new InvalidOperationException("Completed stage must remain steadily lit during reconnect.");
   if(this.FindControl<Button>("SpeakerDetailsButton")!.IsVisible)throw new InvalidOperationException("Reconnect exposed completion action.");
  }
  upgradeProgress=upgradeProgress with{State=InstallationState.Complete};RenderLiveUpgrade();
  if(this.FindControl<Button>("SpeakerDetailsButton")!.Opacity!=0)throw new InvalidOperationException("Completion button skipped its entry animation.");
  await Task.Delay(550);RenderLiveUpgrade();
  if(!this.FindControl<Button>("SpeakerDetailsButton")!.IsHitTestVisible)throw new InvalidOperationException("Completion button never settled.");
  for(int i=0;i<3;i++)
   if(Math.Abs(this.FindControl<StackPanel>("TransferStage"+i)!.Opacity-.85)>.001)throw new InvalidOperationException("Finished stages must all remain lit.");
  // Check all three phase indicators with deterministic animation times.
  foreach(double seconds in new[]{12d,33d,39d})
  {
   int active=seconds<30?0:seconds<34?1:2;
   UpdateTransfer(seconds,false,.4);
   double first=this.FindControl<StackPanel>("TransferStage"+active)!.Opacity;
   UpdateTransfer(seconds,false,1.2);
   if(Math.Abs(first-this.FindControl<StackPanel>("TransferStage"+active)!.Opacity)<.1)throw new InvalidOperationException("Phase breathing did not advance.");
   interrupted=true;recovering=retrying=false;frozenTransferSeconds=seconds;
   UpdateInterruption(.4);first=this.FindControl<StackPanel>("TransferStage"+active)!.Opacity;
   UpdateInterruption(1.2);
   if(Math.Abs(first-this.FindControl<StackPanel>("TransferStage"+active)!.Opacity)<.1)throw new InvalidOperationException("Waiting phase breathing did not advance.");
   for(int i=0;i<3;i++)
    if(i!=active && Math.Abs(this.FindControl<StackPanel>("TransferStage"+i)!.Opacity-(i<active?.85:.35))>.001)throw new InvalidOperationException("Waiting changed a completed/future stage.");
   ResetInterruption();
  }
  upgradeBusy=false;upgradeElapsed.Reset();upgradeProgress=null;liveWaiting=false;ResetInterruption();
 }
}
