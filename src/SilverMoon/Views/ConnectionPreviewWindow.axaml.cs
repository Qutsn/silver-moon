using System;
using System.IO;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Middleton.Desktop;
namespace GlassPlayground;
public sealed partial class ConnectionPreviewWindow : Window
{
 private (int,int,int,int,int) lastRegion;
 private bool regionApplied;
 [DllImport("gdi32.dll")] private static extern IntPtr CreateRoundRectRgn(int left,int top,int right,int bottom,int width,int height);
 [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr handle);
 [DllImport("user32.dll")] private static extern int SetWindowRgn(IntPtr hwnd,IntPtr region,bool redraw);
 private void UpdateWindowOutline()
 {
  if(!OperatingSystem.IsWindows() || TryGetPlatformHandle() is not { } handle)return;
  // Match the concept border in the uniformly scaled Viewbox exactly, including
  // letterboxing after resize. Clip the native backdrop too, not only XAML.
  if(Bounds.Width<8 || Bounds.Height<8)return;
  var scale=Math.Min(Bounds.Width/1440,Bounds.Height/900);
  var x=(Bounds.Width-1440*scale)/2+27*scale;
  var y=(Bounds.Height-900*scale)/2+23*scale;
  var w=1386*scale;var h=854*scale;var dpi=RenderScaling;
  var outline=this.FindControl<Border>("WindowOutline")!;
  // Keep the antialiased stroke inside the integer-pixel native region.
  // A one-physical-pixel stroke must not straddle that hard clipping edge.
  var inset=1.0/dpi;
  var left=Math.Round(x*dpi)/dpi+inset;var top=Math.Round(y*dpi)/dpi+inset;
  var right=Math.Round((x+w)*dpi)/dpi-inset;var bottom=Math.Round((y+h)*dpi)/dpi-inset;
  outline.UseLayoutRounding=false;
  outline.Margin=new Thickness(left,top,0,0);outline.Width=right-left;outline.Height=bottom-top;
  outline.BorderThickness=new Thickness(1/dpi);outline.CornerRadius=new CornerRadius(Math.Max(0,32*scale-inset));
  this.FindControl<Grid>("TopDragArea")!.Margin=new Thickness(x,y,Bounds.Width-x-w,0);
  var key=((int)Math.Round(x*dpi),(int)Math.Round(y*dpi),(int)Math.Round((x+w)*dpi),(int)Math.Round((y+h)*dpi),(int)Math.Round(64*scale*dpi));
  if(regionApplied && key==lastRegion)return;
  var region=CreateRoundRectRgn(key.Item1,key.Item2,key.Item3+1,key.Item4+1,key.Item5,key.Item5);
  if(region==IntPtr.Zero)return;
  if(SetWindowRgn(handle.Handle,region,true)==0){DeleteObject(region);return;}
  // Windows owns the region after success.
  lastRegion=key;regionApplied=true;
 }
 public ConnectionPreviewWindow()
 {
  AvaloniaXamlLoader.Load(this);
  // Keep a 32-unit gap before the retry button; animate only the divider,
  // leaving evidence text and button geometry unchanged.
  this.FindControl<Button>("RetryTransferButton")!.PropertyChanged+=(_,e)=>{
   if(e.Property==IsVisibleProperty)this.FindControl<Border>("PhaseEvidenceLine")!.Width=(bool)e.NewValue!?758:980;
  };
  InitializeLiveMode();
  Opened+=async(_,_)=>{UpdateWindowOutline();if(PreviewMode && Array.Exists(Environment.GetCommandLineArgs(),a=>a=="--selection"))ShowSelection(true);if(PreviewMode && Array.Exists(Environment.GetCommandLineArgs(),a=>a=="--device")){this.FindControl<Viewbox>("ConnectionPage")!.IsVisible=false;await ShowDeviceDetails();}};
  this.FindControl<Button>("ConnectButton")!.Click+=async(_,_)=>await ConnectionActionAsync();
  Closing+=async(_,e)=>{
   if(releasingForClose)return;
   e.Cancel=true;transferTimer.Stop();motionClosed=true;activeAnimation?.TrySetResult();
   await CloseLiveAsync();releasingForClose=true;Close();
  };
  this.FindControl<Button>("TransferBack")!.Click+=async(_,_)=>{if(!upgradeBusy)await ReturnFromTransfer();};
  this.FindControl<Button>("RetryTransferButton")!.Click+=(_,_)=>{if(PreviewMode)RetryInterruptedTransfer();else if(upgradeProgress?.State==InstallationState.Complete)upgradeTask=RestorePostInstallControlAsync();else StartLiveUpgrade();};
  this.FindControl<Button>("SpeakerDetailsButton")!.Click+=async(_,_)=>{if(interrupted)await ReturnInterruptedTransfer();else await ShowDeviceDetails(this.FindControl<Button>("SpeakerDetailsButton")!);};
  this.FindControl<Button>("SelectionDetailsButton")!.Click+=async(_,_)=>await ShowDeviceDetails(this.FindControl<FirmwareChooser>("FirmwareChooser")!.ActionTarget,true);
  KeyDown+=(_,e)=>{
   if(PreviewMode && e.Key==Avalonia.Input.Key.F8){SimulateTransferDisconnect();e.Handled=true;}
   if(PreviewMode && e.Key==Avalonia.Input.Key.F9){SimulateTransferRecovery();e.Handled=true;}
  };
  this.FindControl<DeviceDetailsView>("DeviceDetails")!.BackRequested+=async(_,_)=>await ReturnInterruptedTransfer(true);
  this.FindControl<Button>("BackButton")!.Click+=async(_,_)=>await ReturnToConnectionAsync();
  LayoutUpdated+=(_,_)=>UpdateWindowOutline();
  this.FindControl<Button>("CloseButton")!.Click+=(_,_)=>Close();
  this.FindControl<Button>("MinimizeButton")!.Click+=(_,_)=>WindowState=WindowState.Minimized;
  this.FindControl<Grid>("TopDragArea")!.PointerPressed+=(_,e)=>{if(!e.Handled && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)BeginMoveDrag(e);};
 }
 internal bool PageTransitionBusy=>motionBusy || openingDeviceDetails || returningFromInterruption;
 void ShowOnlyPage(string name)
 {
  foreach(string page in new[]{"ConnectionPage","SelectionPage","TransferPage","DevicePage"})
  {
   var control=this.FindControl<Viewbox>(page)!;
   control.IsVisible=page==name;control.Opacity=1;control.IsHitTestVisible=page==name;
  }
 }
 private void ShowSelection(bool selected)
 {
  if(PageTransitionBusy)return;
  ShowOnlyPage(selected?"SelectionPage":"ConnectionPage");
 }
 internal void CaptureLocalization(string page)
 {
  if(!Array.Exists(Environment.GetCommandLineArgs(),a=>a=="--localization-snapshots"))return;
  UpdateLayout();Directory.CreateDirectory(Middleton.Desktop.DeviceLog.DirectoryPath);
  using var bitmap=new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize((int)Bounds.Width,(int)Bounds.Height));bitmap.Render(this);
  bitmap.Save(Path.Combine(Middleton.Desktop.DeviceLog.DirectoryPath,$"{UiText.Language}-{page}.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
 }
 public async Task Smoke()
 {
  await Task.Delay(1800);CaptureLocalization("connection");
  if(OperatingSystem.IsWindows() && !regionApplied)throw new InvalidOperationException("Native rounded outline was not applied");
  if(this.FindControl<Grid>("StaticMaterial")!.Opacity!=0.9087078651685393 || this.FindControl<Image>("DitherLayer")!.Opacity!=0.40)throw new InvalidOperationException("Material baseline mismatch");
  await PlayConnection();
  if(motionBusy || this.FindControl<Canvas>("ConnectionMotionLayer")!.IsVisible)throw new InvalidOperationException("Connection morph did not finish");
  await Task.Delay(300);
  if(!this.FindControl<Viewbox>("SelectionPage")!.IsVisible || this.FindControl<Viewbox>("ConnectionPage")!.IsVisible)throw new InvalidOperationException("Selection navigation failed");
  await ReturnToConnectionAsync();
  if(!this.FindControl<Viewbox>("ConnectionPage")!.IsVisible || this.FindControl<Viewbox>("SelectionPage")!.IsVisible)throw new InvalidOperationException("Return connection morph failed.");
  ShowSelection(true);UpdateLayout();
  await this.FindControl<FirmwareChooser>("FirmwareChooser")!.CheckAnimation();
  CaptureLocalization("firmware");await this.FindControl<FirmwareChooser>("FirmwareChooser")!.CheckConfirmation();
  await CheckTransfer();
  ShowSelection(true);UpdateLayout();
  await ShowDeviceDetails(this.FindControl<FirmwareChooser>("FirmwareChooser")!.ActionTarget,true);
  if(!this.FindControl<Viewbox>("DevicePage")!.IsVisible || this.FindControl<DeviceDetailsView>("DeviceDetails")!.ReturnButton.Opacity!=1)throw new InvalidOperationException("Selection-to-details handoff failed");
  this.FindControl<Viewbox>("DevicePage")!.IsVisible=false;
  this.FindControl<Viewbox>("TransferPage")!.IsVisible=true;UpdateTransfer(42.5,false);UpdateLayout();
  await ShowDeviceDetails(this.FindControl<Button>("SpeakerDetailsButton")!);
  if(this.FindControl<Canvas>("ConnectionMotionLayer")!.IsVisible || this.FindControl<Button>("SpeakerDetailsButton")!.Opacity!=1)throw new InvalidOperationException("Completion-to-details handoff failed");
  this.FindControl<DeviceDetailsView>("DeviceDetails")!.CheckControls();CaptureLocalization("details");
  // Race a second navigation against the in-flight return; it must not start.
  var returning=ReturnInterruptedTransfer(true);
  await ShowDeviceDetails(null,true);
  ShowSelection(false);
  await returning;
  if(!this.FindControl<Viewbox>("SelectionPage")!.IsVisible || this.FindControl<Viewbox>("DevicePage")!.IsVisible || this.FindControl<Viewbox>("ConnectionPage")!.IsVisible)
   throw new InvalidOperationException("Rapid navigation left overlapping pages.");
  ShowOnlyPage("TransferPage");this.FindControl<StackPanel>("PhaseInformation")!.IsVisible=true;
  var text=this.FindControl<TextBlock>("PhaseExplanation")!;
  text.Text=UiText.Get("ConnectionPreviewWindow.axaml.Text.7884");TextMotion.Set(text,UiText.Get("ConnectionPreviewWindow.axaml.Text.7910"));
  await Task.Delay(70);if(text.Opacity>=1)throw new InvalidOperationException("Text did not animate.");TextMotion.Set(text,UiText.Get("ConnectionPreviewWindow.axaml.Text.8042"));
  await Task.Delay(350);
  if(text.Text!=UiText.Get("ConnectionPreviewWindow.axaml.Text.8042") || text.Opacity!=1)throw new InvalidOperationException("Text transition did not settle on latest state.");
  var metric=new MetricTransition();
  var metricText=new TextBlock{Foreground=Avalonia.Media.Brushes.White};
  metric.Render(metricText,"23",0);metric.Render(metricText,"25",1);
  var unchanged=((Avalonia.Controls.Documents.InlineUIContainer)metricText.Inlines![0]).Child;
  var cell=(Grid)((Avalonia.Controls.Documents.InlineUIContainer)metricText.Inlines[1]).Child!;
  var old=(TextBlock)cell.Children[0];var next=(TextBlock)cell.Children[1];
  if(old.Text!="3" || next.Text!="5" || old.Opacity!=1 || next.Opacity!=0)throw new InvalidOperationException("Old glyph was replaced before transition.");
  metric.Render(metricText,"25",1.15);
  if(Math.Abs(old.Opacity-.125)>.001 || Math.Abs(next.Opacity-.875)>.001)throw new InvalidOperationException("Metric crossfade midpoint missing.");
  if(!ReferenceEquals(unchanged,((Avalonia.Controls.Documents.InlineUIContainer)metricText.Inlines[0]).Child))throw new InvalidOperationException("Unchanged digit was rebuilt mid-animation.");
  metric.Render(metricText,"25",1.31);
  if(next.Opacity!=1 || old.Opacity!=0)throw new InvalidOperationException("Metric transition did not settle.");
  metric.Render(metricText,"—",2);metric.Render(metricText,"1.00 KB/s",3);metric.Render(metricText,"1.00 KB/s",4);
  if(metricText.Inlines.Count!=9)throw new InvalidOperationException("Metric length change lost characters.");
  metricText.Text="—";metric.Render(metricText,"1.00 KB/s",5);
  if(metricText.Inlines.Count!=9 || metricText.Inlines[0] is not Avalonia.Controls.Documents.InlineUIContainer)
   throw new InvalidOperationException("Overwritten metric did not rebuild its glyphs.");
  var stableMetric=new MetricTransition();var stableText=new TextBlock{FontSize=32};
  double? fixedWidth=null,fixedHeight=null;
  foreach(string digits in new[]{"11","88","81","11"})
  {
   stableMetric.Render(stableText,digits,10);
   foreach(var inline in stableText.Inlines!)
   {
    var box=(Grid)((Avalonia.Controls.Documents.InlineUIContainer)inline).Child!;
    fixedWidth??=box.Width;fixedHeight??=box.Height;
    if(box.Width!=fixedWidth || box.Height!=fixedHeight)throw new InvalidOperationException("Metric digit cell dimensions shifted.");
   }
  }
  await CheckLiveUpgradePresentationAsync();
  this.FindControl<Viewbox>("TransferPage")!.IsVisible=false;
  this.FindControl<Viewbox>("DevicePage")!.IsVisible=false;
  ShowSelection(false);
  if(!this.FindControl<Viewbox>("ConnectionPage")!.IsVisible)throw new InvalidOperationException("Return navigation failed");
  File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"connection-preview-check.txt"),$"Saved v2 tint and brightness applied; original image dither restored; Windows-style caption buttons. Actual transparency: {ActualTransparencyLevel}. Desktop appearance requires visual verification.");
 }
}
