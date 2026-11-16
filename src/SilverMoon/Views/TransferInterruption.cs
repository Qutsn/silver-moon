using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
namespace GlassPlayground;
public sealed partial class ConnectionPreviewWindow
{
 void SetTransferText(string name,string value)
 {
  var text=this.FindControl<TextBlock>(name)!;
  if(PreviewMode)text.Text=value;else TextMotion.Set(text,value);
 }
 // Preview-only inputs. Production recovery must reconcile SYNC/device state first.
 readonly Stopwatch interruptionClock=new();
 bool interrupted,recovering,retrying;
 double frozenTransferSeconds,rotationOrigin,recoveryX=1200,recoverySplit=1;
 static double OutCubic(double t)=>1-Math.Pow(1-Math.Clamp(t,0,1),3);
 void ResetInterruption()
 {
  interrupted=recovering=retrying=false;interruptionClock.Reset();
  this.FindControl<Button>("RetryTransferButton")!.IsVisible=false;
  this.FindControl<TextBlock>("SpeakerDetailsLabel")!.Text=UiText.Get("TransferInterruption.Text.1049");
  ToolTip.SetTip(this.FindControl<Button>("SpeakerDetailsButton")!,UiText.Get("TransferInterruption.Text.1049"));
 }
 async Task ReturnFromTransfer()
 {
  if(PageTransitionBusy || upgradeBusy)return;
  StopUpgradeFrames();
  await ReturnInterruptedTransfer(false,true);
 }
 bool returningFromInterruption;
 async Task ReturnInterruptedTransfer(bool fromDetails=false,bool fromProgress=false)
 {
  if(PageTransitionBusy || (!interrupted&&!fromDetails&&!fromProgress))return;
  returningFromInterruption=true;transferTimer.Stop();transferClock.Stop();
  var selection=this.FindControl<Viewbox>("SelectionPage")!;
  var page=this.FindControl<Viewbox>(fromDetails?"DevicePage":"TransferPage")!;
  var chooser=this.FindControl<FirmwareChooser>("FirmwareChooser")!;
  var button=fromDetails?this.FindControl<DeviceDetailsView>("DeviceDetails")!.ReturnButton:this.FindControl<Button>(fromProgress?"TransferBack":"SpeakerDetailsButton")!;
  var retry=this.FindControl<Button>("RetryTransferButton")!;
  var layer=this.FindControl<Canvas>("ConnectionMotionLayer")!;
  static Rect BoundsIn(Control control,Control root)
  {
   var a=control.TranslatePoint(new Point(),root)??throw new InvalidOperationException("Return geometry unavailable");
   var b=control.TranslatePoint(new Point(control.Bounds.Width,control.Bounds.Height),root)!.Value;
   return new Rect(a,b);
  }
  UpdateLayout();
  var source=BoundsIn(button,this);
  selection.IsVisible=true;selection.Opacity=0;selection.IsHitTestVisible=false;
  chooser.SetActionVisible(false);UpdateLayout();
  var target=BoundsIn(chooser.ActionTarget,this);
  var label=new TextBlock{Text=UiText.Get("TransferInterruption.Text.10655"),FontSize=19*source.Height/64,Foreground=Brush.Parse("#343A33"),HorizontalAlignment=Avalonia.Layout.HorizontalAlignment.Center,VerticalAlignment=Avalonia.Layout.VerticalAlignment.Center};
  var proxy=new Border{Child=label,Background=Brush.Parse("#C4EFF0E9"),BorderBrush=Brush.Parse("#48FFFFFF"),BorderThickness=new Thickness(source.Height/64),CornerRadius=new CornerRadius(source.Height/2),Width=source.Width,Height=source.Height};
  Canvas.SetLeft(proxy,source.X);Canvas.SetTop(proxy,source.Y);layer.Children.Add(proxy);layer.IsVisible=true;
  button.Opacity=0;button.IsHitTestVisible=false;retry.IsHitTestVisible=false;
  try
  {
   await Animate(360,t=>{
    double e=OutCubic(t),scale=(source.Height+(target.Height-source.Height)*e)/64;
    Canvas.SetLeft(proxy,source.X+(target.X-source.X)*e);Canvas.SetTop(proxy,source.Y+(target.Y-source.Y)*e);
    proxy.Width=source.Width+(target.Width-source.Width)*e;proxy.Height=64*scale;
    proxy.CornerRadius=new CornerRadius(32*scale);proxy.BorderThickness=new Thickness(scale);label.FontSize=19*scale;
    proxy.Background=new SolidColorBrush(Color.FromArgb((byte)Math.Round(196+20*e),239,240,233));
    proxy.BorderBrush=new SolidColorBrush(Color.FromArgb((byte)Math.Round(72+88*e),255,255,255));
    label.Text=t<.45?UiText.Get("TransferInterruption.Text.10655"):UiText.Get("TransferInterruption.Text.3897");label.Opacity=Math.Abs(t-.45)/ (t<.45?.45:.55);
    page.Opacity=Math.Clamp(1-t/.45,0,1);selection.Opacity=Math.Clamp((t-.45)/.55,0,1);
   });
  }
  finally
  {
   layer.Children.Remove(proxy);layer.IsVisible=false;
   page.IsVisible=false;page.Opacity=1;selection.Opacity=1;selection.IsHitTestVisible=true;
   chooser.SetActionVisible(true);button.Opacity=1;button.IsHitTestVisible=true;ResetInterruption();returningFromInterruption=false;
  }
 }
 async Task CheckInterruptedReturn()
 {
  interrupted=true;recovering=retrying=false;frozenTransferSeconds=12;
  this.FindControl<Viewbox>("TransferPage")!.IsVisible=true;
  this.FindControl<Viewbox>("SelectionPage")!.IsVisible=false;
  UpdateInterruption(31);
  var button=this.FindControl<Button>("SpeakerDetailsButton")!;
  int hidden=0;
  void Changed(object? sender,AvaloniaPropertyChangedEventArgs e){if(e.Property==IsVisibleProperty && !button.IsVisible)hidden++;}
  button.PropertyChanged+=Changed;
  for(int i=0;i<5;i++)UpdateInterruption(31+i*.02);
  button.PropertyChanged-=Changed;
  if(hidden!=0 || !button.IsHitTestVisible)throw new InvalidOperationException("Timeout button is reset between pointer down/up");
  await ReturnInterruptedTransfer();
  if(!this.FindControl<Viewbox>("SelectionPage")!.IsVisible || this.FindControl<Viewbox>("TransferPage")!.IsVisible || interrupted || !this.FindControl<FirmwareChooser>("FirmwareChooser")!.ActionTarget.IsHitTestVisible)
   throw new InvalidOperationException("Return-to-selection handoff failed");
 }
 void SimulateTransferDisconnect()
 {
  if(interrupted || !transferTimer.IsEnabled || transferClock.Elapsed.TotalSeconds>=30 || !this.FindControl<Viewbox>("TransferPage")!.IsVisible)return;
  frozenTransferSeconds=transferClock.Elapsed.TotalSeconds;transferClock.Stop();
  interrupted=true;recovering=retrying=false;rotationOrigin=0;interruptionClock.Restart();
  UpdateInterruption(0);
 }
 void RetryInterruptedTransfer()
 {
  if(!interrupted || recovering || interruptionClock.Elapsed.TotalSeconds<30.48)return;
  rotationOrigin=this.FindControl<ConnectionFocus>("PhaseSpinner")!.Angle;
  retrying=true;interruptionClock.Restart();UpdateInterruption(0);
 }
 void SimulateTransferRecovery()
 {
  if(!interrupted || recovering || interruptionClock.Elapsed.TotalSeconds>=30)return;
  // F9 represents a successful reconnect AND a matching resumable device state.
  var current=this.FindControl<ConnectionFocus>("PhaseSpinner")!;
  recoveryX=current.Center.X;recoverySplit=current.Radius/36;rotationOrigin=current.Angle;
  recovering=true;retrying=false;interruptionClock.Restart();
 }
 void UpdateInterruption(double elapsed,string? liveMessage=null)
 {
  UpdateTransfer(frozenTransferSeconds,false,PreviewMode?null:liveMotionClock.Elapsed.TotalSeconds);
  // Only a DATA-stage outage morphs back into the progress track. Validation
  // and reboot already use the phase surface and must keep it across reconnects.
  bool transferInterrupted=frozenTransferSeconds<30;
  double e=OutCubic(elapsed/.65);
  double ringProgress=transferInterrupted?(recovering?recoverySplit*(1-e):retrying?1:e):1;
  double expiry=OutCubic((elapsed-30)/.48);
  bool timedOut=!recovering && elapsed>=30;
  var ring=this.FindControl<ConnectionFocus>("PhaseSpinner")!;
  ring.IsVisible=!timedOut || expiry<1;
  double sourceX=110+1220*Math.Clamp(frozenTransferSeconds/30,0,1);
  ring.Center=new Point(recovering?recoveryX+(sourceX-recoveryX)*e:sourceX+(1212-sourceX)*ringProgress,recovering?585.5+(603-585.5)*e:603+(585.5-603)*ringProgress);
  if(!transferInterrupted)ring.Center=new Point(1212,585.5);
  double split=ringProgress;
  double size=22*(1-split);
  ring.Plate=new Rect(ring.Center.X-size/2,ring.Center.Y-size/2,size,size);
  ring.PlateBrush=Brush.Parse("#F4F3EC");ring.EdgeBrush=Brushes.Transparent;
  ring.PlateAlpha=1-split;ring.DotAlpha=split*(1-expiry);
  if(retrying)ring.DotAlpha*=OutCubic(elapsed/.48);
  ring.Radius=36*split*(1-.35*expiry);ring.DotSize=10;ring.Angle=rotationOrigin+elapsed*4;ring.LabelAlpha=0;ring.InvalidateVisual();
  this.FindControl<Ellipse>("TransferTip")!.IsVisible=false;
  foreach(string name in new[]{"TransferPercent","TransferPercentSign","TransferRemainingPanel","TransferSpeedPanel","TransferTrack","TransferFill"})
  {
   var control=this.FindControl<Control>(name)!;control.IsVisible=transferInterrupted;control.Opacity=transferInterrupted?1-ringProgress:0;
  }
  this.FindControl<TextBlock>("TransferSpeed")!.Text="—";
  this.FindControl<TextBlock>("TransferRemaining")!.Text="—";
  SetTransferText("TransferTitle",recovering?UiText.Get("TransferInterruption.Text.8209"):UiText.Get("TransferInterruption.Text.8218"));
  var info=this.FindControl<StackPanel>("PhaseInformation")!;info.IsVisible=true;info.Opacity=ringProgress;
  SetTransferText("PhaseHeadline",recovering?UiText.Get("TransferInterruption.Text.8385"):timedOut?UiText.Get("TransferInterruption.Text.8402"):UiText.Get("TransferInterruption.Text.8413"));
  SetTransferText("PhaseExplanation",liveMessage??(recovering?UiText.Get("TransferInterruption.Text.8494"):timedOut?UiText.Get("TransferInterruption.Text.8523"):UiText.Get("TransferInterruption.Text.8547")));
  this.FindControl<TextBlock>("TransferStatus")!.IsVisible=false;
  this.FindControl<StackPanel>("PhaseEvidence")!.IsVisible=false;
  this.FindControl<Button>("TransferBack")!.IsVisible=false;
  var button=this.FindControl<Button>("SpeakerDetailsButton")!;
  double buttonAmount=timedOut?expiry:retrying?1-OutCubic(elapsed/.48):0;
  button.IsVisible=buttonAmount>0;button.IsHitTestVisible=timedOut&&expiry>=1;
  button.Width=72+181*buttonAmount;button.Height=72-8*buttonAmount;
  Canvas.SetLeft(button,1212-button.Width/2);Canvas.SetTop(button,585.5-button.Height/2);button.Opacity=buttonAmount;
  var label=this.FindControl<TextBlock>("SpeakerDetailsLabel")!;
  label.Text=UiText.Get("TransferInterruption.Text.10655");label.Opacity=timedOut?Math.Clamp(((elapsed-30)/.48-.45)/.55,0,1):buttonAmount;
  ToolTip.SetTip(button,null);
  var retry=this.FindControl<Button>("RetryTransferButton")!;
  retry.IsVisible=timedOut;retry.Opacity=expiry;retry.IsHitTestVisible=expiry>=1;
  int activeStage=transferInterrupted?0:frozenTransferSeconds<34?1:2;
  double pulse=timedOut?.55:.36+.64*(.5-.5*Math.Cos(elapsed*2*Math.PI/2.8));
  for(int i=0;i<3;i++)this.FindControl<StackPanel>("TransferStage"+i)!.Opacity=i<activeStage?.85:i==activeStage?pulse:.35;
  if(recovering&&elapsed>=.65)
  {
   ResetInterruption();phaseCopyKey=pendingPhaseKey="";
   this.FindControl<Button>("TransferBack")!.IsVisible=true;
   UpdateTransfer(frozenTransferSeconds,false,PreviewMode?null:liveMotionClock.Elapsed.TotalSeconds);transferClock.Start();
  }
 }
 void CheckInterruption()
 {
  interrupted=true;recovering=retrying=false;frozenTransferSeconds=12;
  UpdateInterruption(.3);
  var ring=this.FindControl<ConnectionFocus>("PhaseSpinner")!;
  if(ring.Center.Y>=603 || ring.Center.Y<=585.5 || this.FindControl<Border>("TransferFill")!.Width!=488)throw new InvalidOperationException("Interrupted progress moved or was not frozen");
  UpdateInterruption(29.99);
  if(this.FindControl<Button>("SpeakerDetailsButton")!.IsVisible)throw new InvalidOperationException("Timeout button appeared early");
  UpdateInterruption(30.48);CaptureLocalization("interrupted");
  if(!this.FindControl<Button>("RetryTransferButton")!.IsHitTestVisible || this.FindControl<TextBlock>("SpeakerDetailsLabel")!.Text!=UiText.Get("TransferInterruption.Text.10655"))throw new InvalidOperationException("Timeout actions missing");
  retrying=true;UpdateInterruption(0);UpdateInterruption(1);
  if(!ring.IsVisible || this.FindControl<Button>("SpeakerDetailsButton")!.IsVisible)throw new InvalidOperationException("Retry did not return to waiting");
  recovering=true;retrying=false;UpdateInterruption(.3);
  if(ring.Center.Y<585.5 || ring.Center.Y>603)throw new InvalidOperationException("Recovery left aligned endpoint range");
  UpdateInterruption(.65);transferClock.Stop();
  if(interrupted || !this.FindControl<Ellipse>("TransferTip")!.IsVisible)throw new InvalidOperationException("Recovery did not restore endpoint");
 }
}
