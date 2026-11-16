using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Middleton.Desktop;
namespace GlassPlayground;
public sealed partial class ConnectionPreviewWindow
{
 CancellationTokenSource? activeScan;
 int scanGeneration;
 TaskCompletionSource<DeviceCandidate>? deviceChoice;
 IReadOnlyList<DeviceCandidate> lastCandidates=Array.Empty<DeviceCandidate>();
 async Task ConnectionActionAsync()
 {
  if(PreviewMode){await PlayConnection();return;}
  if(scanning || motionBusy)return;
  selectedCandidate=null;
  await PlayConnection();
 }
 async Task<bool> ChooseAndConnectAsync()
 {
  var panel=this.FindControl<Border>("DiscoveryPanel")!;
  deviceChoice=new(TaskCreationOptions.RunContinuationsAsynchronously);
  try
  {
   panel.IsVisible=true;
   SetConnectionMessage(UiText.Get("DeviceDiscovery.Text.923"));
   await ScanDevicesAsync();
   if(lastCandidates.Count==0)return false;
   selectedCandidate=await deviceChoice.Task.WaitAsync(lifetime.Token);
   return await ConnectLiveAsync();
  }
  catch(OperationCanceledException){return false;}
  finally{deviceChoice=null;panel.IsVisible=false;}
 }
 async Task ScanDevicesAsync()
 {
  if(scanning || (motionBusy && deviceChoice is null) || motionClosed)return;
  scanning=true;selectedCandidate=null;
  var button=this.FindControl<Button>("ConnectButton")!;button.IsEnabled=false;
  lastCandidates=Array.Empty<DeviceCandidate>();
  SetConnectionMessage(UiText.Get("DeviceDiscovery.Text.1561"));
  this.FindControl<StackPanel>("DiscoveryItems")!.Children.Clear();
  try
  {
   using var timeout=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);timeout.CancelAfter(TimeSpan.FromSeconds(20));activeScan=timeout;
   int generation=++scanGeneration;
   var devices=await Task.Run(()=>liveConnection.ScanAsync(timeout.Token,devices=>Avalonia.Threading.Dispatcher.UIThread.Post(()=>
   {
    if(motionClosed || generation!=scanGeneration || !scanning || deviceChoice?.Task.IsCompleted==true)return;
    if(!lastCandidates.SequenceEqual(devices))RenderCandidates(devices);
   })));
   if(motionClosed || deviceChoice?.Task.IsCompletedSuccessfully==true)return;
   RenderCandidates(devices);
   SetConnectionMessage(devices.Count==0?UiText.Get("DeviceDiscovery.Text.2292"):UiText.Get("DeviceDiscovery.Text.2319"));
  }
  catch(OperationCanceledException) when(deviceChoice?.Task.IsCompletedSuccessfully==true){}
  catch(Exception e){lastCandidates=Array.Empty<DeviceCandidate>();if(!motionClosed)SetConnectionMessage(e is OperationCanceledException?UiText.Get("DeviceDiscovery.Text.2535"):e.Message);}
  finally{activeScan=null;scanGeneration++;scanning=false;button.IsEnabled=!motionBusy;}
 }
 void RenderCandidates(IReadOnlyList<DeviceCandidate> candidates)
 {
  lastCandidates=candidates;
  var scroll=this.FindControl<ScrollViewer>("DiscoveryScroll")!;
  // Keep this subtree unrounded: per-row DPI rounding accumulates and clips the last outline.
  // Six 66-unit rows, five 10-unit gaps, plus two-unit edge clearance at both ends.
  scroll.Height=Math.Max(0,Math.Min(6,candidates.Count)*76-6);
  scroll.VerticalScrollBarVisibility=candidates.Count>6?Avalonia.Controls.Primitives.ScrollBarVisibility.Visible:Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled;
  // Retain scroll position and existing row instances during incremental discovery.

  var items=this.FindControl<StackPanel>("DiscoveryItems")!;
  var wanted=candidates.Select(c=>(c.Id,c.AddressType)).ToHashSet();
  foreach(var stale in items.Children.OfType<Button>().Where(b=>b.Tag is DeviceCandidate c && !wanted.Contains((c.Id,c.AddressType))).ToArray())items.Children.Remove(stale);
  foreach(var candidate in candidates)
  {
   var existing=items.Children.OfType<Button>().FirstOrDefault(b=>b.Tag is DeviceCandidate c && c.Id==candidate.Id && c.AddressType==candidate.AddressType);
   if(existing is not null)
   {
    existing.Tag=candidate;((TextBlock)existing.Content!).Text=candidate.DisplayLabel;ToolTip.SetTip(existing,candidate.DisplayLabel);continue;
   }
   var label=new TextBlock{Text=candidate.DisplayLabel,FontSize=18,MaxWidth=322,TextWrapping=TextWrapping.NoWrap,TextTrimming=TextTrimming.CharacterEllipsis,Foreground=Brush.Parse("#F4F3EC")};
   var row=new Button{Content=label,Height=66,HorizontalAlignment=Avalonia.Layout.HorizontalAlignment.Stretch,HorizontalContentAlignment=Avalonia.Layout.HorizontalAlignment.Left,Padding=new Thickness(20,0),Background=Brushes.Transparent};row.Tag=candidate;row.Classes.Add("secondary");row.Classes.Add("deviceCandidate");ToolTip.SetTip(row,candidate.DisplayLabel);
   row.Click+=(_,_)=>{
    var chosen=(DeviceCandidate)row.Tag!;
    selectedCandidate=chosen;
    foreach(var control in items.Children.OfType<Button>())control.Classes.Remove("selected");
    row.Classes.Add("selected");
    this.FindControl<Button>("ConnectButton")!.Content=UiText.Get("DeviceDiscovery.Text.4869");
    if(deviceChoice?.TrySetResult(chosen)==true)activeScan?.Cancel();
   };
   items.Children.Add(row);AnimateDiscoveredRow(row);
  }
 }
 void AnimateDiscoveredRow(Button row)
 {

  var offset=new TranslateTransform(0,10);
  row.RenderTransformOrigin=new RelativePoint(.5,.5,RelativeUnit.Relative);
  row.RenderTransform=offset;row.Opacity=0;
  var watch=new System.Diagnostics.Stopwatch();
  void Frame(TimeSpan _)
  {
   if(motionClosed || !this.FindControl<StackPanel>("DiscoveryItems")!.Children.Contains(row))return;
   if(!watch.IsRunning)watch.Start();
   double t=Math.Clamp(watch.Elapsed.TotalMilliseconds/700,0,1);
   // Smooth acceleration/deceleration, no spring or overshoot.
   double fade=t*t*(3-2*t),rise=1-Math.Pow(1-t,3);
   offset.Y=10*(1-rise);row.Opacity=fade;
   if(t<1)RequestAnimationFrame(Frame);
   else {row.RenderTransform=null;row.Opacity=1;}
  }
  RequestAnimationFrame(Frame);
 }
 public async Task DiscoverySmokeAsync()
 {
  while(scanning)await Task.Delay(100);
  if(this.FindControl<Border>("DiscoveryPanel")!.IsVisible)throw new InvalidOperationException("Picker visible before start");
  this.FindControl<Border>("DiscoveryPanel")!.IsVisible=true;
  RenderCandidates(new[]{new DeviceCandidate("test-a",UiText.Get("DeviceDiscovery.Text.6095"),UiText.Get("DeviceDiscovery.Text.6103")),new DeviceCandidate("test-b","",UiText.Get("DeviceDiscovery.Text.6151")),new DeviceCandidate("test-c","Other","Other · 9999")});
  var items=this.FindControl<StackPanel>("DiscoveryItems")!;
  if(items.Children.Count!=3 || !this.FindControl<Border>("DiscoveryPanel")!.IsVisible)throw new InvalidOperationException("Inline candidates unavailable");
  var firstRow=items.Children[0];
  RenderCandidates(lastCandidates.Concat(new[]{new DeviceCandidate("test-d","New","New")}).ToArray());
  if(!ReferenceEquals(firstRow,items.Children[0]) || items.Children.Count!=4)throw new InvalidOperationException("Incremental scan rebuilt existing rows.");
  UpdateLayout();
  for(int attempt=0;attempt<20 && items.Children.Any(c=>c.Opacity!=1 || c.RenderTransform is not null);attempt++)await Task.Delay(100);
  if(items.Children.Any(c=>c.Opacity!=1 || c.RenderTransform is not null))throw new InvalidOperationException("Discovery entry animation did not settle.");
  ((Button)items.Children[0]).RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
  if(selectedCandidate?.Id!="test-a")throw new InvalidOperationException("Selection failed");
  SetConnectionMessage(UiText.Get("DeviceDiscovery.Text.2319"));
  await Task.Delay(250);UpdateLayout();
  System.IO.Directory.CreateDirectory(DeviceLog.DirectoryPath);
  using var bitmap=new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize((int)Bounds.Width,(int)Bounds.Height));bitmap.Render(this);bitmap.Save(System.IO.Path.Combine(DeviceLog.DirectoryPath,"inline-discovery.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
  var scroll=this.FindControl<ScrollViewer>("DiscoveryScroll")!;
  foreach(int count in new[]{1,3,5,6,9})
  {
   RenderCandidates(Enumerable.Range(0,count).Select(i=>new DeviceCandidate($"test-{i}",$"Device {i}",$"Device {i}")).ToArray());
   UpdateLayout();
   for(int attempt=0;attempt<30 && items.Children.Any(c=>c.Opacity!=1 || c.RenderTransform is not null);attempt++)await Task.Delay(100);
   var lastRow=items.Children[Math.Min(6,count)-1];
   var rowTop=lastRow.TranslatePoint(new Point(),scroll)!.Value.Y;
   DeviceLog.Write("picker-layout",new{count,viewport=scroll.Bounds.Height,rowTop,rowHeight=lastRow.Bounds.Height,bottom=rowTop+lastRow.Bounds.Height});
   double height=Math.Min(6,count)*76-6;
   if(rowTop+lastRow.Bounds.Height>scroll.Bounds.Height-1.9)throw new InvalidOperationException("Last visible outline is clipped");
   if(Math.Abs(scroll.Bounds.Height-height)>1.5)throw new InvalidOperationException("Incorrect six-row viewport");
   if(Math.Abs(scroll.Bounds.Y-(566-height)/2)>1.5)throw new InvalidOperationException("Device list is not vertically centered");
   if((scroll.Extent.Height>scroll.Viewport.Height+1.5)!=(count>6))throw new InvalidOperationException("Unexpected device list overflow");
  }
  using(var overflowBitmap=new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize((int)Bounds.Width,(int)Bounds.Height)))
  {overflowBitmap.Render(this);overflowBitmap.Save(System.IO.Path.Combine(DeviceLog.DirectoryPath,"discovery-overflow.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);}
  scroll.Offset=new Vector(0,scroll.Extent.Height-scroll.Viewport.Height);UpdateLayout();await Task.Delay(150);
  if(scroll.Offset.Y<=0)throw new InvalidOperationException("Overflow scrolling did not move");
  using(var bottomBitmap=new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize((int)Bounds.Width,(int)Bounds.Height)))
  {bottomBitmap.Render(this);bottomBitmap.Save(System.IO.Path.Combine(DeviceLog.DirectoryPath,"discovery-overflow-bottom.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);}
  DeviceLog.Write("discovery-ui-smoke-passed",new{counts=new[]{1,3,5,6,9},maxRows=6});
 }
}

