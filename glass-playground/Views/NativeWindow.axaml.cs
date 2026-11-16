using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
namespace GlassPlayground;
public sealed partial class NativeWindow:Window {
 bool systemMaterial;
 public NativeWindow(){AvaloniaXamlLoader.Load(this);
  if(Environment.GetCommandLineArgs().Contains("--system-decorations"))WindowDecorations=WindowDecorations.Full;
  this.FindControl<Border>("DragArea")!.PointerPressed+=(_,e)=>{if(e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)BeginMoveDrag(e);};
  this.FindControl<Button>("CloseButton")!.Click+=(_,_)=>Close();
  this.FindControl<ComboBox>("Material")!.SelectionChanged+=(_,_)=>Apply(this.FindControl<ComboBox>("Material")!.SelectedIndex);
  PropertyChanged+=(_,e)=>{if(e.Property.Name=="ActualTransparencyLevel")UpdateStatus();};
  Opened+=(_,_)=>{if(Environment.GetCommandLineArgs().Contains("--reapply-host"))File.AppendAllText(Path.Combine(AppContext.BaseDirectory,"native-results.txt"),WindowsBackdropProbe.ReapplyHost(this)+Environment.NewLine);if(Environment.GetCommandLineArgs().Contains("--dwm-acrylic"))this.FindControl<ComboBox>("Material")!.SelectedIndex=4;UpdateStatus();};
 }
 public void Apply(int index){
  if(systemMaterial && index!=4){WindowsBackdropProbe.SetSystemMaterial(this,false);systemMaterial=false;}
  if(index==4){TransparencyLevelHint=new[]{WindowTransparencyLevel.Transparent};Background=Brushes.Transparent;systemMaterial=true;File.AppendAllText(Path.Combine(AppContext.BaseDirectory,"native-results.txt"),WindowsBackdropProbe.SetSystemMaterial(this,true)+Environment.NewLine);UpdateStatus();return;}
  TransparencyLevelHint=index switch{0=>new[]{WindowTransparencyLevel.AcrylicBlur,WindowTransparencyLevel.None},1=>new[]{WindowTransparencyLevel.Mica,WindowTransparencyLevel.None},2=>new[]{WindowTransparencyLevel.Transparent,WindowTransparencyLevel.None},_=>new[]{WindowTransparencyLevel.None}};
  Background=index==3?Brush.Parse("#252A27"):Brushes.Transparent;
  UpdateStatus();
 }
 void UpdateStatus(){var text="请求："+string.Join(", ",TransparencyLevelHint)+"  /  实际："+ActualTransparencyLevel+"  /  "+(ActualTransparencyLevel==WindowTransparencyLevel.None?"实体背景回退":"原生级别已接受，需观察桌面效果");
  var label=this.FindControl<TextBlock>("NativeStatus");if(label!=null)label.Text=systemMaterial?"实验：DWM Desktop Acrylic（独立于 Avalonia AcrylicBlur），调用结果见 native-results.txt":text;
  File.AppendAllText(Path.Combine(AppContext.BaseDirectory,"native-results.txt"),DateTimeOffset.Now.ToString("O")+" "+text+Environment.NewLine);
 }
}
