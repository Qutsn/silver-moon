using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace GlassPlayground;
public sealed partial class ConnectionPreviewWindow
{
 int scrollGeneration;
 bool scrollAnimating;
 double scrollDestination;
 void InitializeDiscoveryScrolling()
 {
  var viewer=this.FindControl<ScrollViewer>("DiscoveryScroll")!;
  // Handle wheel before the default ScrollViewer jump; keep thumb dragging direct.
  viewer.AddHandler(InputElement.PointerWheelChangedEvent,(_,e)=>
  {
   double max=Math.Max(0,viewer.Extent.Height-viewer.Viewport.Height);
   if(max<=0 || Math.Abs(e.Delta.Y)<.001)return;
   double start=viewer.Offset.Y;
   scrollDestination=Math.Clamp((scrollAnimating?scrollDestination:start)-e.Delta.Y*76,0,max);
   double destination=scrollDestination;
   int generation=++scrollGeneration;
   scrollAnimating=true;e.Handled=true;
   var watch=Stopwatch.StartNew();
   void Frame(TimeSpan _)
   {
    if(generation!=scrollGeneration || motionClosed)return;
    double t=Math.Clamp(watch.Elapsed.TotalMilliseconds/220,0,1);
    double ease=1-Math.Pow(1-t,3);
    viewer.Offset=new Vector(0,Math.Clamp(start+(destination-start)*ease,0,Math.Max(0,viewer.Extent.Height-viewer.Viewport.Height)));
    if(t<1)RequestAnimationFrame(Frame);else scrollAnimating=false;
   }
   RequestAnimationFrame(Frame);
  },RoutingStrategies.Tunnel);
  viewer.AddHandler(InputElement.PointerPressedEvent,(_,_)=>
  {
   ++scrollGeneration;scrollAnimating=false;
  },RoutingStrategies.Tunnel);
 }
}
