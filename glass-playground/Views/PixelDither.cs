using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace GlassPlayground;

// Static, deterministic grain generated at physical pixel resolution. It is not
// stretched from a large texture, so interpolation cannot erase the dithering.
public sealed class PixelDither : Control
{
    WriteableBitmap? bitmap;
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var scale=TopLevel.GetTopLevel(this)?.RenderScaling??1;
        var size=new PixelSize(Math.Max(1,(int)Math.Ceiling(Bounds.Width*scale)),Math.Max(1,(int)Math.Ceiling(Bounds.Height*scale)));
        if(bitmap==null || bitmap.PixelSize!=size)
        {
            bitmap?.Dispose();
            bitmap=new WriteableBitmap(size,new Vector(96*scale,96*scale),PixelFormat.Bgra8888,AlphaFormat.Unpremul);
            using var fb=bitmap.Lock();
            var data=new byte[fb.RowBytes*size.Height];var random=new Random(1742);
            for(int y=0;y<size.Height;y++)for(int x=0;x<size.Width;x++)
            {
                int i=y*fb.RowBytes+x*4;
                byte value=(byte)(random.Next(2)*255);
                data[i]=data[i+1]=data[i+2]=value;
                data[i+3]=(byte)random.Next(2,7);
            }
            Marshal.Copy(data,0,fb.Address,data.Length);
        }
        context.DrawImage(bitmap,new Rect(Bounds.Size));
    }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    { bitmap?.Dispose();bitmap=null;base.OnDetachedFromVisualTree(e); }
}
