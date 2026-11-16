using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace GlassPlayground;

// Interactive visual prototype. All device states and transfer numbers are examples.
public sealed class ConceptWindow : Window
{
    readonly Canvas page = new() { Width=1440,Height=900 };
    readonly IBrush ink=Brush.Parse("#F4F3EC"), muted=Brush.Parse("#CCCEC8");
    int stage;
    string selectedFirmware="休眠策略修改固件 · 25.2.52";
    public ConceptWindow()
    {
        Title="Middleton · 实时桌面玻璃预览（演示）";Width=1080;Height=675;MinWidth=800;MinHeight=500;
        Background=Brushes.Transparent;
        TransparencyLevelHint=new[]{WindowTransparencyLevel.AcrylicBlur};
        TransparencyBackgroundFallback=Brush.Parse("#353B39");
        WindowDecorations=WindowDecorations.None;
        Content=new Viewbox { Child=page,Stretch=Stretch.Uniform };
        KeyDown+=(_,e)=>{if(e.Key==Avalonia.Input.Key.Right)ShowPage((stage+1)%3);if(e.Key==Avalonia.Input.Key.Left)ShowPage((stage+2)%3);};
        ShowPage(0);
    }
    void Put(Control c,double x,double y){Canvas.SetLeft(c,x);Canvas.SetTop(c,y);page.Children.Add(c);}
    void Text(string s,double x,double y,double size=18,bool serif=false,IBrush? color=null)
        =>Put(new TextBlock {Text=s,FontSize=size,Foreground=color??ink,FontFamily=new FontFamily(serif?"Georgia, SimSun":"Microsoft YaHei"),TextWrapping=TextWrapping.NoWrap},x,y);
    void Rule(double y,double x=28,double width=1384)=>Put(new Border {Width=width,Height=1,Background=Brush.Parse("#858982")},x,y);
    void Button(string label,double x,double y,double width,Action action,bool primary=true)
    {
        var b=new Button {Content=label,Width=width,Height=64,CornerRadius=new CornerRadius(32),FontSize=20,
            Background=primary?Brush.Parse("#EFF0E9"):Brushes.Transparent,Foreground=primary?Brush.Parse("#343A33"):ink,
            BorderBrush=Brush.Parse("#AAB3A3"),BorderThickness=new Thickness(primary?0:1),HorizontalContentAlignment=Avalonia.Layout.HorizontalAlignment.Center,VerticalContentAlignment=Avalonia.Layout.VerticalAlignment.Center};
        b.Click+=(_,_)=>action();Put(b,x,y);
    }
    public void ShowPage(int value)
    {
        stage=value;page.Children.Clear();
        // Keep the scene translucent: the Windows compositor supplies the live backdrop.
        // No wallpaper image or desktop capture is used here.
        Put(new Border {Width=1440,Height=900,Background=Brush.Parse("#301E2221")},0,0);
        Put(new Border {Width=1386,Height=854,CornerRadius=new CornerRadius(32),Background=Brush.Parse("#301E2221"),BorderBrush=Brush.Parse("#64E0E5DC"),BorderThickness=new Thickness(1)},27,23);
        // The draggable title strip touches the top edge; its translucent fill
        // lets the native acrylic backdrop remain visible beneath it.
        var drag=new Border {Width=1440,Height=42,Background=Brush.Parse("#08FFFFFF")};drag.PointerPressed+=(_,e)=>{if(e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)BeginMoveDrag(e);};Put(drag,0,0);
        Button("−",1270,43,55,()=>WindowState=WindowState.Minimized,false);
        Button("×",1330,43,55,Close,false);
        Text($"0{stage+1} / "+new[]{"连接设备","固件选择","安装进度"}[stage],80,830,14,false,muted);
        Text("视觉演示 · ← → 切换页面",1015,830,14,false,muted);
        if(stage==0)
        {
            Rule(262);Text("连接您的",111,329,48,true);Text("Middleton",108,391,92,true);
            Text("请将音箱设为蓝牙配对模式。",116,535,19);Text("软件将自动查找并连接音箱。",116,572,17,false,muted);
            Button("开始连接   →",114,649,290,()=>ShowPage(1));
            Text("预览模式：不会建立蓝牙连接",116,755,14,false,muted);
        }
        else if(stage==1)
        {
            Text("选择要刷入的固件",100,154,46,true);Text("内置固件可直接安装，也可导入本地固件文件。",104,220,17,false,muted);Rule(278);
            Put(new Border {Width=1268,Height=288,CornerRadius=new CornerRadius(26),Background=Brush.Parse("#22E5EBDD"),BorderBrush=Brush.Parse("#5AE8EDE0"),BorderThickness=new Thickness(1)},86,314);
            Text("01",120,345,75,true,Brush.Parse("#D0D8C4"));Put(new Border {Width=1,Height=216,Background=Brush.Parse("#8C9687")},244,350);
            Text("休眠策略修改固件",279,350,36,true);Text("停用 AUX 休眠",281,412,20);Text("停用 10 分钟自动关机",281,453,20);Text("内置固件 / 25.2.52",282,535,15,false,muted);
            Button("刷入此固件   →",1055,496,253,()=>{selectedFirmware="休眠策略修改固件 · 25.2.52";ShowPage(2);});
            Text("02",116,649,39,true,muted);Text("恢复官方固件",222,642,26,true);Text("还原官方休眠与自动关机策略 · 17.0.37",223,686,15,false,muted);
            Button("选择官方固件",1126,647,182,()=>{selectedFirmware="官方固件 · 17.0.37";ShowPage(2);},false);Rule(734,104,1232);Text("＋ 导入其他固件（演示）",108,757,15,false,muted);
        }
        else
        {
            Text("正在传输固件",105,159,46,true);Text(selectedFirmware,109,225,17,false,muted);Rule(278);
            Text("64",101,320,159,true);Text("%",339,427,42,true);
            Text("固件传输中 · 演示数据",111,537,18);
            string[] names={"预计传输剩余","传输速度","已用时间"},values={"00:42","18.4 KB/s","01:15"};double[] xs={698,925,1181};
            for(int i=0;i<3;i++){Text(names[i],xs[i],406,15,false,muted);Text(values[i],xs[i],441,32);}
            Put(new Border {Width=1220,Height=10,CornerRadius=new CornerRadius(5),Background=Brush.Parse("#798274")},110,598);
            Put(new Border {Width=781,Height=10,CornerRadius=new CornerRadius(5),Background=Brush.Parse("#E0E7D4")},110,598);
            Put(new Border {Width=22,Height=22,CornerRadius=new CornerRadius(11),Background=ink},883,592);
            Text("请保持音箱开启及蓝牙连接稳定。",111,638,16,false,muted);
            string[] steps={"固件传输","固件验证","音箱重启"};for(int i=0;i<3;i++){double x=111+i*423;Rule(715,x,362);Text($"0{i+1}",x,740,14,true,muted);Text(steps[i],x+39,732,23,true,i==0?ink:muted);}
        }
    }
    public async Task Smoke()
    {
        await Task.Delay(500);
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"native-concept-status.txt"),
            $"Requested: AcrylicBlur\nReported: {ActualTransparencyLevel}\nBackground: transparent; no backdrop image.\nRenderTargetBitmap captures application content only; it does not validate desktop blur.\n");
        for(int i=0;i<3;i++){ShowPage(i);await Task.Delay(350);using var image=new RenderTargetBitmap(new PixelSize((int)Bounds.Width,(int)Bounds.Height));image.Render(this);image.Save(Path.Combine(AppContext.BaseDirectory,$"concept-{i+1}.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);}
    }
}
