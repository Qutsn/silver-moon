using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia.Controls;

namespace GlassPlayground;

static class TextMotion
{
 sealed class State {public string? Target;public int Generation;}
 static readonly ConditionalWeakTable<TextBlock,State> States=new();
 public static void Set(TextBlock text,string value)
 {
  var state=States.GetOrCreateValue(text);
  if(state.Target==value)return;
  state.Target=value;int generation=++state.Generation;
  var top=TopLevel.GetTopLevel(text);
  if(top is null || string.IsNullOrEmpty(text.Text) || !text.IsEffectivelyVisible)
  {text.Text=value;text.Opacity=1;return;}
  var watch=Stopwatch.StartNew();double initial=text.Opacity;
  void Frame(TimeSpan _)
  {
   // A newer target invalidates earlier frame callbacks; stale text must never win.
   if(generation!=state.Generation)return;
   double t=Math.Clamp(watch.Elapsed.TotalMilliseconds/280,0,1);
   if(t<.4)text.Opacity=initial*(1-t/.4);
   else {text.Text=state.Target;text.Opacity=1-Math.Pow(1-(t-.4)/.6,3);}
   if(t<1 && TopLevel.GetTopLevel(text)==top)top.RequestAnimationFrame(Frame);
   else {text.Text=state.Target;text.Opacity=1;}
  }
  top.RequestAnimationFrame(Frame);
 }
}
