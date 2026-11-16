using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Fluid.Avalonia.Acrylic;
namespace GlassPlayground;
public sealed partial class MainWindow:Window {
 readonly List<AcrylicSurface> glass=new();readonly Dictionary<string,Slider> sliders=new();
 readonly DispatcherTimer animation=new(){Interval=TimeSpan.FromMilliseconds(33)};
 readonly DispatcherTimer monitor=new(){Interval=TimeSpan.FromSeconds(1)};
 readonly Stopwatch clock=Stopwatch.StartNew();readonly Process process=Process.GetCurrentProcess();
 TimeSpan lastCpu;double lastTime;long lastRenders;bool moving;NativeWindow? native;
 TestPattern pattern=null!;TextBlock metrics=null!;ComboBox tint=null!;CheckBox highlight=null!,shadow=null!,grid=null!,motion=null!;
 public MainWindow(){AvaloniaXamlLoader.Load(this);pattern=this.FindControl<TestPattern>("Pattern")!;metrics=this.FindControl<TextBlock>("Telemetry")!;
  this.FindControl<Button>("NativeButton")!.Click+=(_,_)=>OpenNative();
  this.FindControl<Button>("RefractionPreset")!.Click+=(_,_)=>ApplyRefractionPreset();
  this.FindControl<Button>("SmokedPreset")!.Click+=(_,_)=>ApplySmokedPreset();
  this.FindControl<Button>("ThemeButton")!.Click+=(_,_)=>{bool light=RequestedThemeVariant!=ThemeVariant.Light;RequestedThemeVariant=light?ThemeVariant.Light:ThemeVariant.Dark;Background=Brush.Parse(light?"#E8ECE5":"#202724");};
  var panel=this.FindControl<StackPanel>("Parameters")!;
  void AddSlider(string key,string name,double min,double max,double initial){var label=new TextBlock();var s=new Slider{Minimum=min,Maximum=max,Value=initial};sliders[key]=s;void Changed(){label.Text=name+"  "+s.Value.ToString("0.00");Apply();}s.ValueChanged+=(_,_)=>Changed();panel.Children.Add(label);panel.Children.Add(s);Changed();}
  AddSlider("blur","Blur Radius",0,32,8);AddSlider("opacity","Backdrop Opacity",0,1,1);AddSlider("alpha","Tint Opacity",0,1,.18);AddSlider("refract","Refraction Amount",0,60,12);AddSlider("height","Refraction Height",0,50,24);AddSlider("vibrancy","Vibrancy",0,2,1);AddSlider("highlight","Highlight Opacity",0,1,.45);
  panel.Children.Add(new TextBlock{Text="Tint Color"});tint=new ComboBox{ItemsSource=new[]{"烟灰 #3C453D","蓝灰 #486779","暖灰 #80664E","白色 #FFFFFF"},SelectedIndex=0};tint.SelectionChanged+=(_,_)=>Apply();panel.Children.Add(tint);
  CheckBox Check(string title,bool initial,Action<bool> change){var c=new CheckBox{Content=title,IsChecked=initial};c.IsCheckedChanged+=(_,_)=>change(c.IsChecked==true);panel.Children.Add(c);return c;}
  highlight=Check("Highlight / 边缘高光",true,_=>Apply());shadow=Check("Shadow / 柔和阴影",true,_=>Apply());grid=Check("纯网格（折射验证）",false,v=>{pattern.GridOnly=v;pattern.InvalidateVisual();});motion=Check("背景动态（默认关闭）",false,v=>{moving=v;if(v)animation.Start();else animation.Stop();});
  var save=new Button{Content="保存当前参数"};save.Click+=(_,_)=>{try{File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"parameters.json"),JsonSerializer.Serialize(new{values=sliders.ToDictionary(x=>x.Key,x=>x.Value.Value),tint=tint.SelectedItem,highlight=highlight.IsChecked,shadow=shadow.IsChecked,grid=grid.IsChecked,motion=motion.IsChecked},new JsonSerializerOptions{WriteIndented=true}));save.Content="已保存 parameters.json";}catch(Exception e){save.Content="保存失败："+e.Message;}};panel.Children.Add(save);
  var canvas=this.FindControl<Canvas>("Surfaces")!;
  AcrylicSurface Surface(double x,double y,double w,double h,Control content){var a=new AcrylicSurface{Width=w,Height=h,CornerRadius=new CornerRadius(28),Content=new Border{Padding=new Thickness(24),Child=content}};Canvas.SetLeft(a,x);Canvas.SetTop(a,y);glass.Add(a);canvas.Children.Add(a);return a;}
  var nav=new StackPanel{Orientation=Orientation.Horizontal,Spacing=28};foreach(var s in new[]{"概览","固件","设置"})nav.Children.Add(new Button{Content=s,Background=Brushes.Transparent,BorderThickness=new Thickness(0)});Surface(30,30,600,78,nav);
  var card=new StackPanel{Spacing=18};card.Children.Add(new TextBlock{Text="Smoked glass",FontSize=32,FontFamily=new FontFamily("Georgia"),Foreground=Brushes.White});card.Children.Add(new TextBlock{Text="玻璃卡片 / 应用内部背景",FontSize=18,Foreground=Brushes.White});card.Children.Add(new TextBlock{Text="折射应改变边缘网格线的位置。\n把模糊设为 0，再比较折射 0 与 36。",Foreground=Brushes.White,TextWrapping=TextWrapping.Wrap});Surface(60,170,510,255,card);
  var button=new Button{Content="玻璃按钮 · 点击测试",Background=Brushes.Transparent,BorderThickness=new Thickness(0),HorizontalAlignment=HorizontalAlignment.Stretch,Foreground=Brushes.White};int clicks=0;button.Click+=(_,_)=>button.Content="已点击 "+(++clicks)+" 次";Surface(230,465,330,85,button);
  Apply();animation.Tick+=(_,_)=>{pattern.Phase+=.04;pattern.InvalidateVisual();};monitor.Tick+=(_,_)=>UpdateMetrics();Opened+=(_,_)=>{lastCpu=process.TotalProcessorTime;lastTime=clock.Elapsed.TotalSeconds;monitor.Start();};Closed+=(_,_)=>{animation.Stop();monitor.Stop();native?.Close();process.Dispose();};
 }
 double V(string key,double fallback=0)=>sliders.TryGetValue(key,out var s)?s.Value:fallback;
 void Apply(){if(tint==null||highlight==null||shadow==null)return;string[] colors={"#3C453D","#486779","#80664E","#FFFFFF"};var c=Color.Parse(colors[Math.Max(0,tint.SelectedIndex)]);
  foreach(var a in glass){a.BlurRadius=V("blur");a.BackdropOpacity=V("opacity");a.TintColor=Color.FromArgb((byte)(V("alpha")*255),c.R,c.G,c.B);a.RefractionAmount=V("refract");a.RefractionHeight=V("height");a.Vibrancy=V("vibrancy",1);a.HighlightEnabled=highlight.IsChecked==true;a.HighlightOpacity=V("highlight");a.ShadowEnabled=shadow.IsChecked==true;}
 }
 public void ApplyRefractionPreset(){motion.IsChecked=false;grid.IsChecked=true;sliders["blur"].Value=0;sliders["alpha"].Value=0;sliders["opacity"].Value=1;sliders["vibrancy"].Value=1;sliders["height"].Value=32;sliders["refract"].Value=36;highlight.IsChecked=false;shadow.IsChecked=false;Apply();}
 public void ApplySmokedPreset(){grid.IsChecked=false;sliders["blur"].Value=8;sliders["alpha"].Value=.18;sliders["opacity"].Value=1;sliders["refract"].Value=12;highlight.IsChecked=true;shadow.IsChecked=true;Apply();}
 public void OpenNative(){if(native==null||!native.IsVisible){native=new NativeWindow();native.Show();}else native.Activate();}
 void UpdateMetrics(){double now=clock.Elapsed.TotalSeconds,dt=now-lastTime;var cpu=process.TotalProcessorTime;double percent=(cpu-lastCpu).TotalSeconds/dt/Environment.ProcessorCount*100;double rate=(pattern.Renders-lastRenders)/dt;metrics.Text=$"模式：{(Environment.GetCommandLineArgs().Contains("--software")?"软件诊断":"Avalonia 默认平台配置")} · CPU {percent:0.0}%（全核归一） · 内存 {process.WorkingSet64/1048576} MiB · 背景 Render 调用 {rate:0.0}/s（不是 GPU FPS） · 玻璃 {glass.Count} 个";lastCpu=cpu;lastTime=now;lastRenders=pattern.Renders;}
 void Capture(string name){using var b=new RenderTargetBitmap(new PixelSize((int)Bounds.Width,(int)Bounds.Height));b.Render(this);b.Save(Path.Combine(AppContext.BaseDirectory,"evidence",name+".png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);}
 public async Task Smoke(){Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory,"evidence"));await Task.Delay(2000);Capture("smoked");ApplyRefractionPreset();sliders["refract"].Value=0;await Task.Delay(700);Capture("refraction-0");sliders["refract"].Value=36;await Task.Delay(700);Capture("refraction-36");sliders["refract"].Value=0;sliders["blur"].Value=16;await Task.Delay(700);Capture("blur-16");ApplySmokedPreset();motion.IsChecked=true;var before=process.TotalProcessorTime;var sw=Stopwatch.StartNew();await Task.Delay(5000);motion.IsChecked=false;var cpu=(process.TotalProcessorTime-before).TotalSeconds/sw.Elapsed.TotalSeconds/Environment.ProcessorCount*100;
  OpenNative();var modes=new List<string>();for(int i=0;i<4;i++){native!.Apply(i);await Task.Delay(600);modes.Add(i+":"+native.ActualTransparencyLevel);}native!.Close();native=null;
  File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"evidence/results.json"),JsonSerializer.Serialize(new{os=Environment.OSVersion.ToString(),renderer=Environment.GetCommandLineArgs().Contains("--software")?"software":"default",nativeModes=modes,cpuPercentNormalized=cpu,workingSetBytes=process.WorkingSet64,glassCount=glass.Count,note="Lifecycle and captures only. Native appearance requires desktop observation; image comparison verifies refraction separately."},new JsonSerializerOptions{WriteIndented=true}));
 }
}
