using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;

namespace GlassPlayground;
public sealed partial class ConnectionPreviewWindow
{
 async Task ReturnToConnectionAsync()
 {
  if(PageTransitionBusy || upgradeBusy || motionClosed)return;
  motionBusy=true;
  var selection=this.FindControl<Viewbox>("SelectionPage")!;
  var connection=this.FindControl<Viewbox>("ConnectionPage")!;
  var chooser=this.FindControl<FirmwareChooser>("FirmwareChooser")!;
  var button=this.FindControl<Button>("ConnectButton")!;
  var layer=this.FindControl<Canvas>("ConnectionMotionLayer")!;
  Rect BoundsOf(Control c){var a=c.TranslatePoint(new Point(),this)!.Value;var b=c.TranslatePoint(new Point(c.Bounds.Width,c.Bounds.Height),this)!.Value;return new Rect(a,b);}
  var source=BoundsOf(chooser.ActionTarget);
  connection.IsVisible=true;connection.Opacity=0;connection.IsHitTestVisible=false;
  selection.IsHitTestVisible=false;UpdateLayout();
  var target=BoundsOf(button);
  var focus=new ConnectionFocus{Width=Bounds.Width,Height=Bounds.Height,Plate=source,Label=UiText.Get("ReturnConnectionMotion.Text.1082"),LabelAlpha=1,FontSize=19,Scale=source.Height/64};
  layer.Children.Add(focus);layer.IsVisible=true;chooser.SetActionVisible(false);button.Opacity=0;
  try
  {
   await Animate(420,t=>{
    double e=1-Math.Pow(1-t,3);
    focus.Plate=new Rect(source.X+(target.X-source.X)*e,source.Y+(target.Y-source.Y)*e,source.Width+(target.Width-source.Width)*e,source.Height+(target.Height-source.Height)*e);
    focus.Label=t<.45?UiText.Get("ReturnConnectionMotion.Text.1082"):UiText.Get("ReturnConnectionMotion.Text.1515");focus.LabelAlpha=t<.45?1-t/.45:(t-.45)/.55;
    focus.InvalidateVisual();selection.Opacity=Math.Clamp(1-t/.45,0,1);connection.Opacity=Math.Clamp((t-.45)/.55,0,1);
   });
  }
  finally
  {
   layer.Children.Remove(focus);layer.IsVisible=false;chooser.SetActionVisible(true);button.Opacity=1;
   ShowOnlyPage("ConnectionPage");motionBusy=false;
  }
 }
}
