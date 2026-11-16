using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
namespace GlassPlayground;
public sealed partial class FirmwareRiskWindow : UserControl
{
 readonly TaskCompletionSource<bool> result=new();
 const int MotionDurationMs=220;
 bool closing,opening=true,detached;
 readonly Rect? source;
 Rect? destination;
 Rect lastFlight;
 readonly Action<bool>? sourceVisibility;
 public Task<bool> Result=>result.Task;
 public FirmwareRiskWindow():this("",false){}
 public FirmwareRiskWindow(string firmware,bool custom,Rect? source=null,Action<bool>? sourceVisibility=null)
 {
  this.source=source;this.sourceVisibility=sourceVisibility;
  AvaloniaXamlLoader.Load(this);
  if(this.FindControl<Border>("DialogCard")!.Opacity!=0 || this.FindControl<Border>("Shade")!.Opacity!=0)
   throw new InvalidOperationException("Dialog must start hidden before attachment");
  this.FindControl<TextBlock>("FirmwareName")!.Text=firmware;
  this.FindControl<TextBlock>("CustomWarning")!.IsVisible=custom;
  this.FindControl<Button>("CancelButton")!.Click+=async(_,_)=>await Finish(false);
  this.FindControl<Button>("ConfirmButton")!.Click+=async(_,_)=>await Finish(true);
  this.FindControl<Button>("RiskDetailsButton")!.Click+=async(_,_)=>await ToggleDetails("RiskDetailsButton","RiskDetailsFrame");
  this.FindControl<Button>("TimeDetailsButton")!.Click+=async(_,_)=>await ToggleDetails("TimeDetailsButton","TimeDetailsFrame");
  this.FindControl<Grid>("DismissArea")!.PointerPressed+=async(_,e)=>{if(ReferenceEquals(e.Source,this.FindControl<Grid>("DismissArea"))){e.Handled=true;await Finish(false);}};
  KeyDown+=async(_,e)=>{if(e.Key==Key.Escape){e.Handled=true;await Finish(false);}};
  AttachedToVisualTree+=async(_,_)=>{await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(()=>{},Avalonia.Threading.DispatcherPriority.Loaded);await Motion(true);opening=false;if(!closing)this.FindControl<Button>("CancelButton")!.Focus();};
  DetachedFromVisualTree+=(_,_)=>{detached=true;sourceVisibility?.Invoke(true);result.TrySetResult(false);};
 }
 async Task ToggleDetails(string buttonName,string frameName)
 {
  var frame=this.FindControl<Border>(frameName)!;
  var button=this.FindControl<Button>(buttonName)!;
  if(!button.IsEnabled)return;
  button.IsEnabled=false;
  bool opening=frame.Height<1;
  var child=frame.Child!;
  child.Measure(new Size(Math.Max(1,frame.Bounds.Width),double.PositiveInfinity));
  double start=frame.Height;
  double end=opening?child.DesiredSize.Height:0;
  var top=TopLevel.GetTopLevel(this);
  if(top==null)return;
  var arrow=this.FindControl<Avalonia.Controls.Shapes.Path>(buttonName.StartsWith("Risk")?"RiskChevron":"TimeChevron")!;
  var rotation=new RotateTransform(opening?0:180);arrow.RenderTransform=rotation;
  var reveal=new LinearGradientBrush{StartPoint=new RelativePoint(0,0,RelativeUnit.Relative),EndPoint=new RelativePoint(0,1,RelativeUnit.Relative)};
  var solid=new GradientStop(Colors.White,0);var clear=new GradientStop(Colors.Transparent,.1);
  reveal.GradientStops.Add(solid);reveal.GradientStops.Add(clear);child.OpacityMask=reveal;
  child.VerticalAlignment=Avalonia.Layout.VerticalAlignment.Top;
  child.Height=child.DesiredSize.Height;
  var watch=Stopwatch.StartNew();
  var done=new TaskCompletionSource();
  void Tick(TimeSpan _)
  {
   double t=Math.Clamp(watch.Elapsed.TotalMilliseconds/220,0,1);
   double e=1-Math.Pow(1-t,3);
   frame.Height=start+(end-start)*e;
   double visible=opening?e:1-e;
   frame.Opacity=1;
   solid.Offset=Math.Max(0,visible*1.2-.2);clear.Offset=Math.Min(1,visible*1.2);
   rotation.Angle=180*visible;
   InvalidateMeasure();
   if(t>=1){frame.Height=end;frame.Opacity=opening?1:0;child.OpacityMask=null;rotation.Angle=opening?180:0;done.TrySetResult();}
   else top.RequestAnimationFrame(Tick);
  }
  top.RequestAnimationFrame(Tick);
  await done.Task;
  button.IsEnabled=true;
 }
 public async Task CheckConfirm()
 {
  for(int i=0;opening && i<100;i++)await Task.Delay(20);
  if(opening)throw new InvalidOperationException("Confirmation entry did not settle");
  await Finish(true);
 }
 public async Task CheckCancel()
 {
  for(int i=0;opening && i<100;i++)await Task.Delay(20);
  if(opening || destination is not Rect target || target.Width<=0)throw new InvalidOperationException("Dialog layout/entry did not settle");
  await ToggleDetails("RiskDetailsButton","RiskDetailsFrame");
  if(TopLevel.GetTopLevel(this) is ConnectionPreviewWindow preview)preview.CaptureLocalization("risk");
  if(this.FindControl<Border>("RiskDetailsFrame")!.Height<24)throw new InvalidOperationException("Risk detail did not expand");
  await ToggleDetails("RiskDetailsButton","RiskDetailsFrame");
  await ToggleDetails("TimeDetailsButton","TimeDetailsFrame");
  if(TopLevel.GetTopLevel(this) is ConnectionPreviewWindow timePreview)timePreview.CaptureLocalization("time");
  await ToggleDetails("TimeDetailsButton","TimeDetailsFrame");
  await Finish(false);
  if(source is not Rect saved || Math.Abs(lastFlight.X-saved.X)>.1 || Math.Abs(lastFlight.Y-saved.Y)>.1 || Math.Abs(lastFlight.Width-saved.Width)>.1)
   throw new InvalidOperationException("Dialog flight did not return to captured button bounds");
 }
 async Task Finish(bool confirmed)
 {
  if(closing||opening)return;
  closing=true;
  if(!confirmed)await Motion(false,true);
  result.TrySetResult(confirmed);
 }
 public Task DismissForProgress()=>Motion(false,false,false);
 async Task Motion(bool entering,bool returnToSource=true,bool animateButton=true)
 {
  var top=TopLevel.GetTopLevel(this);if(top==null)return;
  var card=this.FindControl<Border>("DialogCard")!;var shade=this.FindControl<Border>("Shade")!;
  var confirm=this.FindControl<Button>("ConfirmButton")!;var layer=this.FindControl<Canvas>("ButtonFlight")!;
  var glass=this.FindControl<Fluid.Avalonia.Acrylic.AcrylicSurface>("DialogGlass")!;
  var shift=new TranslateTransform();card.RenderTransform=shift;
  if(entering){card.Opacity=shade.Opacity=0;}
  UpdateLayout();
  // Capture destination once after layout; cancellation must reverse the same path.
  var offset=this.TranslatePoint(new Point(),top)??throw new InvalidOperationException("Overlay is not arranged");
  var dest=confirm.TranslatePoint(new Point(),this)??throw new InvalidOperationException("Confirm button is not arranged");
  destination=new Rect(dest,confirm.Bounds.Size);
  var b=destination.Value;
  var a=source is Rect saved ? new Rect(saved.X-offset.X,saved.Y-offset.Y,saved.Width,saved.Height) : b;
  var flightLabel=new TextBlock{Text=entering?UiText.Get("FirmwareRiskWindow.axaml.Text.6410"):UiText.Get("FirmwareRiskWindow.axaml.Text.6418"),Foreground=Brush.Parse("#343A33"),HorizontalAlignment=Avalonia.Layout.HorizontalAlignment.Center,VerticalAlignment=Avalonia.Layout.VerticalAlignment.Center};
  var flight=new Border{Child=flightLabel,IsHitTestVisible=false,UseLayoutRounding=false};
  var initial=entering?a:b;
  Canvas.SetLeft(flight,initial.X);Canvas.SetTop(flight,initial.Y);
  flight.Width=initial.Width;flight.Height=initial.Height;flight.CornerRadius=new CornerRadius(initial.Height/2);
  flight.BorderThickness=new Thickness(entering?a.Width/253:1);
  flight.Background=Brush.Parse(entering?"#D8EFF0E9":"#DFEFF0E9");
  flight.BorderBrush=Brush.Parse(entering?"#A0FFFFFF":"#50FFFFFF");
  flightLabel.FontSize=entering?19*a.Width/253:15;
  if(entering)shift.Y=18;
  if(animateButton){layer.Children.Add(flight);sourceVisibility?.Invoke(false);confirm.Opacity=0;}
  var edgeBlur=new BlurEffect{Radius=0};flight.Effect=edgeBlur;
  // Prepare the first backdrop capture before starting the visible timeline.
  // A costly first capture must not consume most of the entrance animation.
  if(entering)
  {
   card.Opacity=.001;
   for(int i=0;i<2;i++)
   {
    var ready=new TaskCompletionSource();
    top.RequestAnimationFrame(_=>ready.TrySetResult());await ready.Task;
    if(detached)return;
   }
  }
  var done=new TaskCompletionSource();var watch=Stopwatch.StartNew();
  double previousMs=0,elapsedMs=0;
  void Frame(TimeSpan time)
  {
   if(detached){done.TrySetResult();return;}
   double nowMs=watch.Elapsed.TotalMilliseconds;
   // Bound visual advancement after a slow capture instead of skipping the fade.
   elapsedMs+=Math.Min(24,nowMs-previousMs);previousMs=nowMs;
   double t=Math.Min(1,elapsedMs/MotionDurationMs);
   double e=1-Math.Pow(1-t,3),v=entering?e:1-e;
   card.Opacity=shade.Opacity=v;shift.Y=(1-v)*18;
   // Drive the native Skia backdrop pass explicitly, not only its parent's opacity.
   glass.BackdropOpacity=v;
   glass.SurfaceColor=Color.FromArgb((byte)Math.Round(96*v),50,60,69);
   Rect start=entering?a:b,end=entering?b:(returnToSource?a:b);
   var plate=new Rect(start.X+(end.X-start.X)*e,start.Y+(end.Y-start.Y)*e,start.Width+(end.Width-start.Width)*e,start.Height+(end.Height-start.Height)*e);
   lastFlight=plate;
   if(animateButton){Canvas.SetLeft(flight,plate.X);Canvas.SetTop(flight,plate.Y);flight.Width=plate.Width;flight.Height=plate.Height;}
   double destinationWeight=entering?e:1-e;
   double scale=a.Width/253+(1-a.Width/253)*destinationWeight;
   flightLabel.FontSize=(19+(15-19)*destinationWeight)*scale;
   flight.BorderThickness=new Thickness(scale);flight.CornerRadius=new CornerRadius(plate.Height/2);
   flight.Background=new SolidColorBrush(Color.FromArgb((byte)Math.Round(216+7*destinationWeight),239,240,233));
   flight.BorderBrush=new SolidColorBrush(Color.FromArgb((byte)Math.Round(160-80*destinationWeight),255,255,255));
   // Soften the travelling focus where it crosses the panel edge; both ends
   // retain the exact stationary material for a seamless hand-off.
   double travelling=Math.Sin(Math.PI*e);
   flight.Opacity=(!entering&&!returnToSource)?1-e:1-.75*travelling;
   edgeBlur.Radius=1.2*travelling;
   flightLabel.Text=t<.5?(entering?UiText.Get("FirmwareRiskWindow.axaml.Text.6410"):UiText.Get("FirmwareRiskWindow.axaml.Text.6418")):(entering?UiText.Get("FirmwareRiskWindow.axaml.Text.6418"):UiText.Get("FirmwareRiskWindow.axaml.Text.6410"));flightLabel.Opacity=Math.Abs(t-.5)*2;
   if(t>=1)done.TrySetResult();else top.RequestAnimationFrame(Frame);
  }
  top.RequestAnimationFrame(Frame);await done.Task;
  // Swap in one UI update: no double-painted alpha and no empty frame.
  if(animateButton)layer.Children.Remove(flight);confirm.Opacity=1;
  if(!entering&&animateButton)sourceVisibility?.Invoke(true);
 }
}
