using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace GlassPlayground;

// Crossfade actual glyphs, never interpolate fictional speed/ETA values.
// Equal characters use a single stationary glyph. Changed characters retain both
// old and new glyphs for 300 ms; complementary opacity avoids a brightness dip.
sealed class MetricTransition
{
 string? target;
 double started;
 readonly List<(TextBlock Old,TextBlock New)> changing=new();
 public void Render(TextBlock text,string value,double now)
 {
  // Preview/interruption rendering can replace Text and clear the inline tree.
  // Rebuild even when the requested value is unchanged after that replacement.
  if(target!=value || text.Text is not null || text.Inlines!.Count==0)
  {
   string? previous=target;target=value;started=now;changing.Clear();
   text.Text=null;text.Inlines!.Clear();text.Opacity=1;
   // Identical cells for stationary and changing glyphs avoid baseline changes.
   // Reserve the widest digit even when the current value contains narrow ones.
   double digitWidth=0,lineHeight=0;
   foreach(char digit in "0123456789")
   {
    var probe=Glyph(text,digit);probe.Measure(Size.Infinity);
    digitWidth=Math.Max(digitWidth,probe.DesiredSize.Width);
    lineHeight=Math.Max(lineHeight,probe.DesiredSize.Height);
   }
   for(int i=0;i<value.Length;i++)
   {
    var next=Glyph(text,value[i]);
    next.Measure(Size.Infinity);
    var cell=new Grid{Width=char.IsDigit(value[i])?digitWidth:next.DesiredSize.Width,Height=lineHeight};
    bool changed=previous is not null && previous!="—" && value!="—" && i<previous.Length && previous[i]!=value[i];
    if(!changed){cell.Children.Add(next);text.Inlines.Add(new InlineUIContainer(cell));continue;}
    var old=Glyph(text,previous![i]);
    next.Opacity=0;

    cell.Children.Add(old);cell.Children.Add(next);
    text.Inlines.Add(new InlineUIContainer(cell));changing.Add((old,next));
   }
  }
  double t=Math.Clamp((now-started)/.3,0,1),ease=1-Math.Pow(1-t,3); // easeOutCubic
  foreach(var (old,next) in changing)
  {
   old.Opacity=1-ease;next.Opacity=ease;


  }
  if(t>=1)changing.Clear();
 }
 static TextBlock Glyph(TextBlock source,char value)=>new()
 {
  Text=value.ToString(),FontFamily=source.FontFamily,FontSize=source.FontSize,
  HorizontalAlignment=Avalonia.Layout.HorizontalAlignment.Center,
  FontWeight=source.FontWeight,FontStyle=source.FontStyle,Foreground=source.Foreground
 };
}
