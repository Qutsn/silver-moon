using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Threading;
namespace GlassPlayground;
public sealed partial class ConnectionPreviewWindow
{
 readonly DispatcherTimer transferTimer=new(){Interval=TimeSpan.FromMilliseconds(33)};
 readonly Stopwatch transferClock=new();
 int transferFrameGeneration;
 int transferFlow;
 string phaseCopyKey="",pendingPhaseKey="";
 double phaseCopyStarted;
 bool phaseCopySwapped,phaseCopyWasTransfer;

 public async Task ShowTransfer(string firmware,FirmwareRiskWindow? dialog=null,int flow=0)
 {
  ResetInterruption();
  this.FindControl<Button>("TransferBack")!.IsVisible=true;
  transferFlow=flow;phaseCopyKey=pendingPhaseKey="";
  var page=this.FindControl<Viewbox>("TransferPage")!;
  var selection=this.FindControl<Viewbox>("SelectionPage")!;
  this.FindControl<TextBlock>("TransferFirmware")!.Text=firmware;
  UpdateTransfer(0);page.Opacity=0;page.IsVisible=true;page.IsHitTestVisible=false;
  var tip=this.FindControl<Ellipse>("TransferTip")!;
  UpdateLayout();
  var tipPoint=tip.TranslatePoint(new Point(),this)??throw new InvalidOperationException("Progress point not arranged");
  var tipEnd=tip.TranslatePoint(new Point(tip.Bounds.Width,tip.Bounds.Height),this)!.Value;
  var target=new Rect(tipPoint,tipEnd);
  var confirm=dialog?.FindControl<Button>("ConfirmButton");
  var origin=confirm?.TranslatePoint(new Point(),this);
  var source=origin is Point pos?new Rect(pos,confirm!.Bounds.Size):target;
  var layer=new Canvas{IsHitTestVisible=false};
  var label=new TextBlock{Text=UiText.Get("TransferPreview.Text.1673"),FontSize=15,Foreground=Brush.Parse("#343A33"),HorizontalAlignment=Avalonia.Layout.HorizontalAlignment.Center,VerticalAlignment=Avalonia.Layout.VerticalAlignment.Center};
  var focus=new Border{Child=label,Background=Brush.Parse("#DFEFF0E9"),BorderBrush=Brush.Parse("#50FFFFFF"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(21),Width=source.Width,Height=source.Height};
  Canvas.SetLeft(focus,source.X);Canvas.SetTop(focus,source.Y);layer.Children.Add(focus);
  var host=this.FindControl<Grid>("ModalHost")!;host.Children.Add(layer);host.IsVisible=true;
  tip.Opacity=0;if(confirm!=null)confirm.Opacity=0;
  try
  {
   var dialogExit=dialog?.DismissForProgress();
   await Animate(360,t=>{
    double e=1-Math.Pow(1-t,3);
    page.Opacity=Math.Clamp((t-.45)/.55,0,1);selection.Opacity=Math.Clamp(1-t/.45,0,1);
    double w=source.Width+(target.Width-source.Width)*e,h=source.Height+(target.Height-source.Height)*e;
    Canvas.SetLeft(focus,source.X+(target.X-source.X)*e);Canvas.SetTop(focus,source.Y+(target.Y-source.Y)*e);
    focus.Width=w;focus.Height=h;focus.CornerRadius=new CornerRadius(h/2);focus.BorderThickness=new Thickness(1-e);
    label.Opacity=Math.Max(0,1-t*3);
    focus.Background=new SolidColorBrush(Color.FromArgb((byte)(223+32*e),(byte)(239+5*e),(byte)(240+3*e),(byte)(233+3*e)));
   });
   if(dialogExit!=null)await dialogExit;
  }
  finally
  {
   tip.Opacity=1;host.Children.Remove(layer);if(dialog==null)host.IsVisible=false;
  }
  if(motionClosed)return;
  selection.IsVisible=false;selection.Opacity=1;page.Opacity=1;page.IsHitTestVisible=true;
  transferClock.Restart();
  if(!PreviewMode){this.FindControl<TextBlock>("TransferSpeed")!.Text="—";this.FindControl<TextBlock>("TransferRemaining")!.Text="—";return;}
  transferTimer.Start();
  int generation=++transferFrameGeneration;
  void Frame(TimeSpan _)
  {
   if(motionClosed || !transferTimer.IsEnabled || generation!=transferFrameGeneration)return;
   if(interrupted)UpdateInterruption(interruptionClock.Elapsed.TotalSeconds);
   else UpdateTransfer(transferClock.Elapsed.TotalSeconds);
   if(transferTimer.IsEnabled)RequestAnimationFrame(Frame);
  }
  RequestAnimationFrame(Frame);
 }
 void UpdateTransfer(double seconds,bool animate=true,double? animationSeconds=null)
 {
  // Accelerated visual demonstration only; no firmware or transport operations.
  double fraction=Math.Clamp(seconds/30,0,1);
  int stage=seconds<30?0:seconds<34?1:2;bool complete=seconds>=42;
  this.FindControl<TextBlock>("TransferPercent")!.Text=((int)(fraction*100)).ToString();
  this.FindControl<Border>("TransferFill")!.Width=1220*fraction;
  Canvas.SetLeft(this.FindControl<Ellipse>("TransferTip")!,99+1220*fraction);
  if(PreviewMode && animationSeconds is null)
  {
  this.FindControl<TextBlock>("TransferRemaining")!.Text=stage==0?TimeSpan.FromSeconds(30-seconds).ToString(@"mm\:ss"):"—";
  this.FindControl<TextBlock>("TransferElapsed")!.Text=TimeSpan.FromSeconds(Math.Min(seconds,42)).ToString(@"mm\:ss");
  this.FindControl<TextBlock>("TransferSpeed")!.Text=stage==0?"106 KB/s":"—";
  }
  bool transfer=stage==0;
  double morph=Math.Clamp((seconds-30)/.65,0,1);
  double morphEase=1-Math.Pow(1-morph,3);
  foreach(string name in new[]{"TransferPercent","TransferPercentSign","TransferRemainingPanel","TransferSpeedPanel","TransferTrack","TransferFill"})
  {
   var control=this.FindControl<Control>(name)!;
   control.IsVisible=transfer || morph<1;control.Opacity=transfer?1:1-morphEase;
  }
  var tip=this.FindControl<Ellipse>("TransferTip")!;
  tip.IsVisible=transfer;
  var spinner=this.FindControl<ConnectionFocus>("PhaseSpinner")!;
  spinner.IsVisible=!transfer&&seconds<42.48;
  // Single endpoint travels left into the marked area, then fans out into eight dots.
  spinner.Center=new Point(1330+(1212-1330)*morphEase,603+(585.5-603)*morphEase);
  double split=Math.Clamp((morph-.15)/.85,0,1);split=1-Math.Pow(1-split,3);
  double diameter=22*(1-split);
  spinner.Plate=new Rect(spinner.Center.X-diameter/2,spinner.Center.Y-diameter/2,diameter,diameter);
  spinner.PlateBrush=Brush.Parse("#F4F3EC");spinner.EdgeBrush=Brushes.Transparent;
  spinner.PlateAlpha=1-split;spinner.DotAlpha=split;spinner.LabelAlpha=0;
  spinner.Radius=36*split;spinner.DotSize=10;spinner.Angle=(animationSeconds??(seconds-30))*4;
  var details=this.FindControl<Button>("SpeakerDetailsButton")!;
  double endProgress=complete?(animate?Math.Clamp((seconds-42)/.48,0,1):1):0;
  double endEase=1-Math.Pow(1-endProgress,3);
  if(!interrupted)
  {
  details.IsVisible=complete;details.IsHitTestVisible=complete&&endProgress>=1;
  details.Width=72+(253-72)*endEase;details.Height=72+(64-72)*endEase;
  Canvas.SetLeft(details,1212-details.Width/2);Canvas.SetTop(details,585.5-details.Height/2);
  details.Opacity=endEase;
  // Keep the same center and use the final button itself, avoiding a proxy swap.
  this.FindControl<TextBlock>("SpeakerDetailsLabel")!.Opacity=Math.Clamp((endProgress-.45)/.55,0,1);
  }
  if(complete){spinner.DotAlpha=1-endEase;spinner.Radius=36*(1-.35*endEase);spinner.PlateAlpha=0;}
  spinner.InvalidateVisual();
  this.FindControl<StackPanel>("PhaseInformation")!.IsVisible=!transfer;
  this.FindControl<StackPanel>("PhaseEvidence")!.IsVisible=!transfer;
  this.FindControl<TextBlock>("TransferStageLabel0")!.Text=UiText.Get("TransferPreview.Text.7067");
  this.FindControl<TextBlock>("TransferStageLabel1")!.Text=UiText.Get("TransferPreview.Text.11479");
  ((TextBlock)this.FindControl<StackPanel>("TransferStage2")!.Children[1]).Text=UiText.Get("TransferPreview.Text.7234");
  bool recovery=stage==1 && transferFlow!=1 && seconds<32;
  bool reconnect=stage==2 && seconds<38;
  bool commit=stage==2 && seconds>=38 && seconds<40;
  string headline=complete?UiText.Get("TransferPreview.Text.7432"):recovery?UiText.Get("TransferPreview.Text.7451"):stage==1?UiText.Get("TransferPreview.Text.7469"):reconnect?UiText.Get("TransferPreview.Text.11935"):commit?UiText.Get("TransferPreview.Text.12097"):UiText.Get("TransferPreview.Text.7515");
  string explanation=complete?UiText.Get("TransferPreview.Text.7558"):recovery?UiText.Get("TransferPreview.Text.7591"):stage==1?(transferFlow==1?UiText.Get("TransferPreview.Text.7644"):UiText.Get("TransferPreview.Text.7663")):
   reconnect?UiText.Get("TransferPreview.Text.7709"):commit?UiText.Get("TransferPreview.Text.7752"):UiText.Get("TransferPreview.Text.7778");
  string next=complete?(this.FindControl<TextBlock>("PhaseNext")!.Text??""):recovery?UiText.Get("TransferPreview.Text.7889"):stage==1?UiText.Get("TransferPreview.Text.7924"):reconnect?UiText.Get("TransferPreview.Text.7959"):commit?UiText.Get("TransferPreview.Text.7996"):UiText.Get("TransferPreview.Text.8014");
  string title=complete?UiText.Get("TransferPreview.Text.13195"):transfer?UiText.Get("TransferPreview.Text.10586"):stage==1?UiText.Get("TransferPreview.Text.13897"):UiText.Get("TransferPreview.Text.8112");
  if(animationSeconds is null)UpdatePhaseCopy(seconds,animate,transfer,title,headline,explanation,next);
  if(complete)
  {
   // Share the button morph timeline; do not re-fade this outgoing content
   // with the new completion headline.
   var evidence=this.FindControl<StackPanel>("PhaseEvidence")!;
   evidence.Opacity=1-endEase;evidence.RenderTransform=null;
   evidence.IsVisible=endProgress<1;
  }
  var status=this.FindControl<TextBlock>("TransferStatus")!;
  status.IsVisible=transfer;
  status.Text=transferFlow==2?UiText.Get("TransferPreview.Text.8661"):UiText.Get("TransferPreview.Text.8677");
  // 2.8-second continuous breathing; completed stages stay steady, future stages dim.
  double stageStart=stage==0?0:stage==1?30:34;
  double pulse=.36+.64*(.5-.5*Math.Cos(2*Math.PI*(animationSeconds??(seconds-stageStart))/2.8));
  for(int i=0;i<3;i++)this.FindControl<StackPanel>("TransferStage"+i)!.Opacity=complete?.85:i<stage?.85:i==stage?pulse:.35;
  if(complete && endProgress>=1 && (pendingPhaseKey=="" || !animate))transferTimer.Stop();
 }
 void UpdatePhaseCopy(double seconds,bool animate,bool transfer,string title,string headline,string explanation,string next)
 {
  var info=this.FindControl<StackPanel>("PhaseInformation")!;
  var evidence=this.FindControl<StackPanel>("PhaseEvidence")!;
  var heading=this.FindControl<TextBlock>("TransferTitle")!;
  string key=title+headline+explanation;
  void Apply()
  {
   heading.Text=title;
   this.FindControl<TextBlock>("PhaseHeadline")!.Text=headline;
   this.FindControl<TextBlock>("PhaseExplanation")!.Text=explanation;
   this.FindControl<TextBlock>("PhaseNext")!.Text=next;
  }
  if(!animate || phaseCopyKey=="" || transfer)
  {
   Apply();phaseCopyKey=key;pendingPhaseKey="";phaseCopyWasTransfer=transfer;
   heading.Opacity=info.Opacity=evidence.Opacity=1;
   info.RenderTransform=evidence.RenderTransform=null;return;
  }
  if(key!=phaseCopyKey && pendingPhaseKey!=key)
  {
   pendingPhaseKey=key;phaseCopyStarted=seconds;phaseCopySwapped=false;
  }
  if(pendingPhaseKey=="")return;
  double t=Math.Clamp((seconds-phaseCopyStarted)/.32,0,1);
  double opacity,offset;
  if(t<.35){opacity=1-t/.35;offset=-4*(t/.35);}
  else
  {
   if(!phaseCopySwapped){Apply();phaseCopySwapped=true;}
   double e=1-Math.Pow(1-(t-.35)/.65,3);opacity=e;offset=8*(1-e);
  }
  // New phase panels were not present during transfer: keep them invisible
  // until their text is replaced at the midpoint.
  bool fromTransfer=phaseCopyWasTransfer;
  heading.Opacity=opacity;info.Opacity=evidence.Opacity=fromTransfer&&t<.35?0:opacity;
  info.RenderTransform=new TranslateTransform(0,offset);evidence.RenderTransform=new TranslateTransform(0,offset);
  if(t>=1){phaseCopyWasTransfer=transfer;phaseCopyKey=key;pendingPhaseKey="";heading.Opacity=info.Opacity=evidence.Opacity=1;}
 }
 async Task CheckTransfer()
 {
  await this.FindControl<FirmwareChooser>("FirmwareChooser")!.CheckConfirmedTransfer();transferTimer.Stop();
  if(!this.FindControl<Viewbox>("TransferPage")!.IsVisible || this.FindControl<Grid>("ModalHost")!.IsVisible)throw new InvalidOperationException("Confirm-to-progress handoff failed");
  UpdateTransfer(15,false);CaptureLocalization("transfer");
  if(this.FindControl<Border>("TransferFill")!.Width!=610)throw new InvalidOperationException("Progress geometry mismatch");
  UpdateTransfer(32,false);
  if(this.FindControl<TextBlock>("TransferStageLabel1")!.Text!=UiText.Get("TransferPreview.Text.11479") || this.FindControl<TextBlock>("TransferPercent")!.IsVisible)throw new InvalidOperationException("Preparation must show state, not transfer percent");
  transferFlow=1;UpdateTransfer(32,false);
  if(this.FindControl<TextBlock>("PhaseExplanation")!.Text!=UiText.Get("TransferPreview.Text.7644"))throw new InvalidOperationException("Official verification explanation mismatch");
  UpdateTransfer(36,false);
  if(this.FindControl<TextBlock>("PhaseHeadline")!.Text!=UiText.Get("TransferPreview.Text.11935"))throw new InvalidOperationException("Reconnect state missing");
  UpdateTransfer(39,false);
  if(this.FindControl<TextBlock>("PhaseHeadline")!.Text!=UiText.Get("TransferPreview.Text.12097"))throw new InvalidOperationException("Commit state missing");
  double before=this.FindControl<StackPanel>("TransferStage2")!.Opacity;UpdateTransfer(39.5,false);
  if(Math.Abs(before-this.FindControl<StackPanel>("TransferStage2")!.Opacity)<.01)throw new InvalidOperationException("Active stage is not breathing");
  transferFlow=0;
  UpdateTransfer(41.9,false);UpdateTransfer(42);
  if(this.FindControl<StackPanel>("PhaseEvidence")!.Opacity!=1 || this.FindControl<Button>("SpeakerDetailsButton")!.Opacity!=0)throw new InvalidOperationException("Completion animations did not start together");
  UpdateTransfer(42.24);
  if(Math.Abs(this.FindControl<StackPanel>("PhaseEvidence")!.Opacity+this.FindControl<Button>("SpeakerDetailsButton")!.Opacity-1)>.001)throw new InvalidOperationException("Completion fade timeline mismatch");
  UpdateTransfer(42.5,false);CaptureLocalization("complete");
  if(this.FindControl<StackPanel>("PhaseEvidence")!.IsVisible)throw new InvalidOperationException("Completion evidence remained visible");
  if(transferTimer.IsEnabled || this.FindControl<TextBlock>("TransferTitle")!.Text!=UiText.Get("TransferPreview.Text.13195"))throw new InvalidOperationException("Completion did not settle");
  UpdateTransfer(29.9,false);UpdateTransfer(30);
  if(this.FindControl<TextBlock>("TransferTitle")!.Text!=UiText.Get("TransferPreview.Text.10586"))throw new InvalidOperationException("Phase copy changed before fade-out");
  UpdateTransfer(30.2);
  var ring=this.FindControl<ConnectionFocus>("PhaseSpinner")!;
  if(!ring.IsVisible || ring.Radius<=0 || ring.Radius>=36 || this.FindControl<Border>("TransferTrack")!.Opacity>=1)throw new InvalidOperationException("Endpoint split/track fade missing");
  UpdateTransfer(30.7);
  if(ring.Radius!=36 || this.FindControl<Border>("TransferTrack")!.IsVisible || this.FindControl<TextBlock>("TransferTitle")!.Text!=UiText.Get("TransferPreview.Text.13897"))throw new InvalidOperationException("Preparation transition did not settle");
  UpdateTransfer(42,false);
  CheckInterruption();
  await CheckInterruptedReturn();
  this.FindControl<Viewbox>("TransferPage")!.IsVisible=false;
 }
}
