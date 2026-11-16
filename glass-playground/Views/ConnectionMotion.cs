using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System.Globalization;

namespace GlassPlayground;
// One persistent drawing surface: animation never changes child layout per frame.
public sealed class ConnectionFocus : Control
{
 public Rect Plate; public double PlateAlpha=1,DotAlpha,Radius,Angle,DotSize,LabelAlpha,Scale=1;
 public Point Center; public string Label="";
 public IBrush PlateBrush=Brush.Parse("#D8EFF0E9");
 public IBrush EdgeBrush=Brush.Parse("#A0FFFFFF");
 public double FontSize=19;
 readonly IBrush light=Brush.Parse("#EFF0E9");
 string cached=""; double cachedSize; FormattedText? text;
 public override void Render(DrawingContext c)
 {
  if(Plate.Width>Scale && Plate.Height>Scale)
  using(c.PushOpacity(PlateAlpha))c.DrawRectangle(PlateBrush,new Pen(EdgeBrush,Scale),Plate.Deflate(Scale/2),Plate.Height/2,Plate.Height/2);
  for(int i=0;i<8;i++)
  {
   double a=Angle+i*Math.PI/4;
   using(c.PushOpacity(DotAlpha*(.35+.65*i/7)))c.DrawEllipse(light,null,new Point(Center.X+Math.Cos(a)*Radius,Center.Y+Math.Sin(a)*Radius),DotSize/2,DotSize/2);
  }
  if(LabelAlpha>0)
  {
   if(cached!=Label||cachedSize!=FontSize*Scale||text==null){cached=Label;cachedSize=FontSize*Scale;text=new FormattedText(Label,CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface("Microsoft YaHei UI"),FontSize*Scale,Brush.Parse("#343A33"));}
   using(c.PushOpacity(LabelAlpha))c.DrawText(text,new Point(Plate.Center.X-text.Width/2,Plate.Center.Y-text.Height/2));
  }
 }
}
public sealed partial class ConnectionPreviewWindow
{
 bool motionBusy,motionClosed;
 TaskCompletionSource? activeAnimation;
 Task Animate(int milliseconds,Action<double> frame)
 {
  var done=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);activeAnimation=done;
  var watch=Stopwatch.StartNew();
  void Tick(TimeSpan time)
  {
   if(motionClosed){done.TrySetResult();return;}
   try{double t=Math.Min(1,watch.Elapsed.TotalMilliseconds/milliseconds);frame(t);if(t>=1)done.TrySetResult();else RequestAnimationFrame(Tick);}
   catch(Exception e){done.TrySetException(e);}
  }
  RequestAnimationFrame(Tick);return done.Task;
 }
 static double Smooth(double t){t=Math.Clamp(t,0,1);return t*t*t*(t*(t*6-15)+10);}
 async Task PlayConnection()
 {
  if(motionBusy||motionClosed)return;motionBusy=true;
  var button=this.FindControl<Button>("ConnectButton")!;
  var layer=this.FindControl<Canvas>("ConnectionMotionLayer")!;
  var welcome=this.FindControl<StackPanel>("WelcomeTitle")!;
  var connecting=this.FindControl<TextBlock>("ConnectingTitle")!;
  var first=this.FindControl<Viewbox>("ConnectionPage")!;
  var next=this.FindControl<Viewbox>("SelectionPage")!;
  var chooser=this.FindControl<FirmwareChooser>("FirmwareChooser")!;
  var divider=this.FindControl<Border>("SelectionDivider")!;
  var start=button.TranslatePoint(new Point(),this)??new Point(80,440);
  double scale=Math.Min(Bounds.Width/1440,Bounds.Height/900),sw=290*scale,sh=64*scale,d=80*scale;
  var center=new Point(start.X+sw/2,start.Y+sh/2);
  var focus=new ConnectionFocus{Width=Bounds.Width,Height=Bounds.Height,Scale=scale,DotSize=9*scale,Center=center,Label=UiText.Get("ConnectionMotion.Text.3292"),LabelAlpha=1,Plate=new Rect(start,new Size(sw,sh))};
  layer.Children.Clear();layer.Children.Add(focus);layer.IsVisible=true;
  button.Opacity=0;button.IsEnabled=false;
  // Reserve the exact original heading height to prevent the button jumping.
  connecting.Height=welcome.Bounds.Height;welcome.IsVisible=false;connecting.IsVisible=true;
  var spin=Stopwatch.StartNew();
  try
  {
   await Animate(280,t=>{
    double e=Smooth(t);focus.Plate=new Rect(center.X-(sw+(d-sw)*e)/2,center.Y-(sh+(d-sh)*e)/2,sw+(d-sw)*e,sh+(d-sh)*e);
    focus.PlateAlpha=1-Smooth(t/.55);focus.LabelAlpha=1-Smooth(t*2);focus.DotAlpha=Smooth((t-.15)/.65);focus.Radius=d*.38*e;focus.Angle=spin.Elapsed.TotalSeconds*4;focus.InvalidateVisual();
   });
   if(PreviewMode)await Animate(1100,t=>{focus.Angle=spin.Elapsed.TotalSeconds*4;focus.InvalidateVisual();});
   else
   {
    var connection=selectedCandidate is null?ChooseAndConnectAsync():ConnectLiveAsync();
    while(!connection.IsCompleted && !motionClosed)await Animate(80,t=>{focus.Angle=spin.Elapsed.TotalSeconds*4;focus.InvalidateVisual();});
    if(!await connection || liveConnection.Current is not {Connected:true})return;
   }
   if(motionClosed)return;
   divider.Width=0;
   chooser.SetActionVisible(false);next.IsVisible=true;next.Opacity=0;next.IsHitTestVisible=false;
   await Animate(32,t=>{focus.Angle=spin.Elapsed.TotalSeconds*4;focus.InvalidateVisual();});
   UpdateLayout();
   var target=chooser.ActionTarget.TranslatePoint(new Point(),this)??throw new InvalidOperationException("Target layout unavailable");
   if(chooser.ActionTarget.Bounds.Width<=0)throw new InvalidOperationException("Target button has not been arranged");
   double tw=253*scale,th=64*scale;var end=new Point(target.X+tw/2,target.Y+th/2);focus.Label=UiText.Get("ConnectionMotion.Text.5098");
   // Draw the divider in the transition layer so the page fade cannot hide it.
   var lineOrigin=divider.TranslatePoint(new Point(),this)??throw new InvalidOperationException("Divider is not arranged");
   var sweep=new Border{Height=scale,Width=0,Background=divider.Background,IsHitTestVisible=false};
   Canvas.SetLeft(sweep,lineOrigin.X);Canvas.SetTop(sweep,lineOrigin.Y);layer.Children.Add(sweep);
   await Animate(480,t=>{
    double e=1-Math.Pow(1-t,3),expand=Smooth((t-.28)/.72),x=center.X+(end.X-center.X)*e,y=center.Y+(end.Y-center.Y)*e;
    double w=d+(tw-d)*expand,h=d+(th-d)*expand;
    focus.Center=new Point(x,y);focus.Plate=new Rect(x-w/2,y-h/2,w,h);focus.Angle=spin.Elapsed.TotalSeconds*4;
    focus.PlateAlpha=expand;focus.DotAlpha=1-Smooth((t-.35)/.5);focus.Radius=d*.38*(1-expand*.7);focus.LabelAlpha=Smooth((t-.62)/.38);
    sweep.Width=1384*scale*(1-Math.Pow(1-t,3));
    first.Opacity=1-Smooth(t/.65);next.Opacity=Smooth((t-.2)/.8);focus.InvalidateVisual();
   });
   if(motionClosed)return;
   if(!PreviewMode && liveConnection.Current is not {Connected:true}){ShowOnlyPage("ConnectionPage");return;}
   ShowOnlyPage("SelectionPage");
   divider.Width=1384;
   layer.IsVisible=false;layer.Children.Clear();chooser.SetActionVisible(true);
   next.Opacity=1;
  }
  finally
  {
   divider.Width=1384;
   layer.IsVisible=false;layer.Children.Clear();chooser.SetActionVisible(true);next.Opacity=first.Opacity=1;next.IsHitTestVisible=true;button.Opacity=1;button.IsEnabled=true;
   welcome.IsVisible=true;connecting.IsVisible=false;motionBusy=false;activeAnimation=null;
   if(!PreviewMode)button.Content=liveError?UiText.Get("ConnectionMotion.Text.6765"):UiText.Get("ConnectionMotion.Text.6776");
  }
 }
}
