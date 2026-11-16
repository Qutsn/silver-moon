using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Fluid.Avalonia.Acrylic;

namespace GlassPlayground;

public sealed class MaterialTunerWindow : Window
{
    public MaterialTunerWindow(GlassPreviewWindow preview)
    {
        Title = "材质调节 · 第二版基准";
        Width=350; Height=720; MinWidth=320; MinHeight=480;
        Background=Brush.Parse("#252B32");
        var panel=new StackPanel { Margin=new Thickness(22), Spacing=9 };
        Content=new ScrollViewer { Content=panel };
        panel.Children.Add(new TextBlock { Text="材质调节", FontSize=24 });
        panel.Children.Add(new TextBlock { Text="实时预览 · 第二版配色与样式\n不读取电源状态，不连接音箱。", TextWrapping=TextWrapping.Wrap });
        var card=preview.FindControl<Border>("GlassCard")!;
        var original=card.Child!;
        // Keep the v2 border, padding and shadow. Internal blur is optional and off at baseline.
        var surface=new AcrylicSurface { CornerRadius=new CornerRadius(16), BlurRadius=0, RefractionAmount=0,
            HighlightEnabled=false, ShadowEnabled=false, TintColor=Colors.Transparent, Vibrancy=1, IsHitTestVisible=false, IsVisible=false };
        var layers=new Grid { Height=58 };card.Child=null;layers.Children.Add(surface);
        var grain=new Image { Source=new Avalonia.Media.Imaging.Bitmap(Avalonia.Platform.AssetLoader.Open(new Uri("avares://SilverMoon/Assets/fine-dither.png"))), Stretch=Stretch.Fill, Opacity=.35, IsHitTestVisible=false };
        layers.Children.Add(grain);layers.Children.Add(original);card.Child=layers;
        var sliders=new Dictionary<string,Slider>();var defaults=new Dictionary<string,double>();
        void Add(string key,string name,double min,double max,double value,Action<double> apply)
        {
            var label=new TextBlock();var slider=new Slider { Minimum=min,Maximum=max,Value=value };
            sliders[key]=slider;defaults[key]=value;
            void Changed(){label.Text=$"{name}  {slider.Value:0.00}";apply(slider.Value);}
            slider.ValueChanged+=(_,_)=>Changed();panel.Children.Add(label);panel.Children.Add(slider);Changed();
        }
        Add("staticOpacity","静态材质浓度",0,1,1,v=>preview.FindControl<Grid>("StaticMaterial")!.Opacity=v);
        Add("brightness","静态材质亮度",-.25,.25,0,v=>{var b=preview.FindControl<Border>("BrightnessLayer")!;b.Background=v<0?Brushes.Black:Brushes.White;b.Opacity=Math.Abs(v);});
        Add("light","柔光强度",0,1,1,v=>preview.FindControl<Border>("SoftLight")!.Opacity=v);
        var fill=(LinearGradientBrush)card.Background!;
        Add("glassTint","卡片染色浓度",0,1,1,v=>fill.Opacity=Math.Min(v,1));
        Add("edge","卡片边缘反光",0,1,1,v=>((LinearGradientBrush)card.BorderBrush!).Opacity=v);
        Add("dither","抗色带（建议小值）",0,1,.35,v=>{preview.FindControl<Image>("DitherLayer")!.Opacity=v;grain.Opacity=v;});
        Add("blur","卡片内部模糊半径",0,24,0,v=>{surface.BlurRadius=v;surface.IsVisible=v>0;});
        panel.Children.Add(new TextBlock { Text="内部模糊只处理卡片下方的应用内容，平滑背景上变化较弱。Windows 原生桌面模糊半径不由这些滑条控制。", FontSize=12,TextWrapping=TextWrapping.Wrap });
        var mode=new ComboBox { ItemsSource=new[]{"系统 Acrylic（默认）","纯静态预览","系统 Mica"},SelectedIndex=0 };
        mode.SelectionChanged+=(_,_)=>{preview.TransparencyLevelHint=mode.SelectedIndex switch {1=>new[]{WindowTransparencyLevel.None},2=>new[]{WindowTransparencyLevel.Mica,WindowTransparencyLevel.None},_=>new[]{WindowTransparencyLevel.AcrylicBlur,WindowTransparencyLevel.None}};};
        panel.Children.Add(mode);
        var reset=new Button { Content="恢复第二版基准" };
        reset.Click+=(_,_)=>{foreach(var p in defaults)sliders[p.Key].Value=p.Value;mode.SelectedIndex=0;};panel.Children.Add(reset);
        var save=new Button { Content="保存这组参数" };var status=new TextBlock { TextWrapping=TextWrapping.Wrap,FontSize=12 };
        save.Click+=(_,_)=>{try{var path=Path.Combine(AppContext.BaseDirectory,"material-settings.json");File.WriteAllText(path,JsonSerializer.Serialize(new{basis="v2",mode=mode.SelectedIndex,values=sliders.ToDictionary(p=>p.Key,p=>p.Value.Value)},new JsonSerializerOptions{WriteIndented=true}));status.Text="已保存："+path;}catch(Exception e){status.Text="保存失败："+e.Message;}};
        panel.Children.Add(save);panel.Children.Add(status);
        if(Environment.GetCommandLineArgs().Contains("--preview-smoke"))
        {
            foreach(var item in sliders){item.Value.Value=item.Value.Minimum;item.Value.Value=item.Value.Maximum;item.Value.Value=defaults[item.Key];}
            if(surface.IsVisible || surface.BlurRadius!=0 || preview.FindControl<Image>("DitherLayer")!.Opacity!=.35)
                throw new InvalidOperationException("Material baseline restoration failed");
        }
    }
}

