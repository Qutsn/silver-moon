using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
namespace GlassPlayground;
public sealed class TestPattern:Control {
 public double Phase;public bool GridOnly;public long Renders;
 public override void Render(DrawingContext c){base.Render(c);Renders++;c.FillRectangle(Brush.Parse("#32443B"),new Rect(Bounds.Size));
  if(!GridOnly){string[] colors={"#B3C4AA","#718E91","#AD866D"};for(int i=0;i<7;i++)c.FillRectangle(Brush.Parse(colors[i%3]),new Rect(i*110-80+Math.Sin(Phase+i)*25,50+(i%3)*130,140,210));}
  for(int i=0;i<40;i++){double x=i*24+Math.Sin(Phase)*12;c.DrawLine(new Pen(Brush.Parse("#DEE5D6"),1),new Point(x,0),new Point(x,Bounds.Height));c.DrawLine(new Pen(Brush.Parse("#DEE5D6"),1),new Point(0,i*24),new Point(Bounds.Width,i*24));}
 }
}
