using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Middleton.Desktop;
namespace GlassPlayground;
// Preserve preview appearance; live control uses confirmed device snapshots.
public sealed partial class DeviceDetailsView : Canvas
{
 public event EventHandler? BackRequested;
 public Button ReturnButton {get;}
 readonly DemoStepper volume,bass,treble;
 readonly TextBlock[] deviceValues=new TextBlock[4];
 readonly TextBlock connectionState;
 static readonly IBrush Ink=Brush.Parse("#F4F3EC"),Muted=Brush.Parse("#CCCEC8");
 public DeviceDetailsView()
 {
  Add(Text(UiText.Get("DeviceDetailsView.Text.772"),46),100,154);
  Add(new Border{Width=1384,Height=1,Background=Brush.Parse("#858982")},28,278);
  Add(Text(UiText.Get("DeviceDetailsView.Text.887"),28),110,326);
  connectionState=Text(UiText.Get("DeviceDetailsView.Text.2626"),16,Muted);Add(connectionState,110,370);
  string[] names={UiText.Get("DeviceDetailsView.Text.1008"),UiText.Get("DeviceDetailsView.Text.1015"),UiText.Get("DeviceDetailsView.Text.1020"),UiText.Get("DeviceDetailsView.Text.1026")};
  for(int i=0;i<names.Length;i++)
  {
   double y=432+i*62;
   Add(Text(names[i],20,Muted),110,y);deviceValues[i]=Text("—",21);deviceValues[i].MaxWidth=165;deviceValues[i].TextWrapping=TextWrapping.NoWrap;deviceValues[i].MaxLines=1;deviceValues[i].TextTrimming=TextTrimming.CharacterEllipsis;Add(deviceValues[i],382,y);
   Add(new Border{Width=380,Height=1,Background=Brush.Parse("#267F8B91")},110,y+43);
  }
  Add(new Border{Width=1,Height=400,Background=Brush.Parse("#407F8B91")},560,326);
  Add(Text(UiText.Get("DeviceDetailsView.Text.1546"),28),632,326);
  volume=new DemoStepper(UiText.Get("DeviceDetailsView.Text.1593"),16,32,0);bass=new DemoStepper(UiText.Get("DeviceDetailsView.Text.1628"),5,10);treble=new DemoStepper(UiText.Get("DeviceDetailsView.Text.1662"),5,10);
  Add(volume,632,425);Add(bass,632,529);Add(treble,632,633);
  var back=new Button{Content=UiText.Get("DeviceDetailsView.Text.10761"),Width=253,Height=64,FontSize=19};back.Classes.Add("connect");ReturnButton=back;
  back.Click+=(_,_)=>BackRequested?.Invoke(this,EventArgs.Empty);Add(back,1069,796);
 }
 static TextBlock Text(string value,double size,IBrush? brush=null)=>new(){Text=value,FontSize=size,Foreground=brush??Ink};
 void Add(Control c,double x,double y){SetLeft(c,x);SetTop(c,y);Children.Add(c);}
 public void SetSnapshot(Middleton.Desktop.DeviceSnapshot? snapshot)
 {
  if(liveSnapshot?.SessionId!=snapshot?.SessionId || snapshot?.Connected!=true){foreach(var control in new[]{volume,bass,treble})control.LiveState.ResetPending();soundError=null;}
  liveSnapshot=snapshot;
  bool connected=snapshot?.Connected==true;
  string?[] fields={snapshot?.Name,snapshot?.Battery is int battery?$"{battery}%":null,snapshot?.Serial,snapshot?.Firmware};
  for(int i=0;i<fields.Length;i++){deviceValues[i].Text=connected && !string.IsNullOrWhiteSpace(fields[i])?fields[i]:"—";ToolTip.SetTip(deviceValues[i],deviceValues[i].Text);}
  bool writable=connected && soundWriter is not null;
  volume.ShowLive(connected?snapshot?.Volume:null,writable);
  bool eqWritable=writable && snapshot?.Bass is not null && snapshot?.Treble is not null;
  bass.ShowLive(connected?snapshot?.Bass:null,eqWritable);treble.ShowLive(connected?snapshot?.Treble:null,eqWritable);
  UpdateSoundStatus();
 }
 public void CheckControls()
 {
  foreach(var control in new[]{volume,bass,treble})
  {
   int original=control.Value;
   int expectedMinimum=ReferenceEquals(control,volume)?0:1;
   control.Change(1);if(control.Value!=original+1)throw new InvalidOperationException("Stepper increment failed");
   control.Change(-1);if(control.Value!=original)throw new InvalidOperationException("Stepper decrement failed");
   control.Change(100);if(control.Value!=control.Maximum||control.Plus.IsEnabled)throw new InvalidOperationException("Stepper upper boundary failed");
   control.Change(-100);if(control.Value!=expectedMinimum||control.Minus.IsEnabled)throw new InvalidOperationException("Stepper lower boundary failed");
   if(control.Maximum==10 && control.Level(0)!=1 || control.Level(1)>=1)throw new InvalidOperationException("Segment level mismatch");
   control.Change(original-expectedMinimum);
  }
 }
 public void CheckReadOnlyControls()
 {
  var zero=new Middleton.Desktop.DeviceSnapshot(Guid.NewGuid(),true,"Test","MIDDLETON",null,null,null,0,0,0,0,new(true,true,true,true));
  SetSnapshot(zero);
  foreach(var control in new[]{volume,bass,treble})
   if(control.Value!=0 || control.Minus.IsEnabled || control.Plus.IsEnabled || control.Level(0)!=0)throw new InvalidOperationException("Read-only zero incorrectly displayed or writable");
  SetSnapshot(null);
  if(deviceValues[0].Text!="—" || deviceValues[1].Text!="—")throw new InvalidOperationException("Unknown details were fabricated");
 }
 sealed class DemoStepper : Border
 {
  public int Value{get;private set;}
  public Button Minus{get;} public Button Plus{get;}
  readonly TextBlock value;
  readonly Border[] segments=new Border[10];
  public int Maximum{get;}
  public SoundStepperState LiveState {get;}
  public Action<int>? Requested {get;set;}
  bool live;
  public double Level(int i)=>Math.Clamp(Value*10.0/Maximum-i,0,1);
  public DemoStepper(string title,int initial,int maximum,int minimum=1)
  {
   Maximum=maximum;LiveState=new(maximum,minimum);
   Width=690;Height=96;CornerRadius=new CornerRadius(22);Background=Brush.Parse("#12E5EBDD");BorderBrush=Brush.Parse("#40D9DED6");BorderThickness=new Thickness(1);
   var grid=new Grid{ColumnDefinitions=new ColumnDefinitions("*,64,68,64"),Margin=new Thickness(24,12),ColumnSpacing=12};Child=grid;
   var label=new StackPanel{Spacing=8,VerticalAlignment=Avalonia.Layout.VerticalAlignment.Center};grid.Children.Add(label);
   label.Children.Add(Text(title,22));
   var ticks=new StackPanel{Orientation=Avalonia.Layout.Orientation.Horizontal,Spacing=9};
   var track=new Border{Padding=new Thickness(10,7),CornerRadius=new CornerRadius(14),Background=Brush.Parse("#16000000"),BorderBrush=Brush.Parse("#30D9DED6"),BorderThickness=new Thickness(1),HorizontalAlignment=Avalonia.Layout.HorizontalAlignment.Left,Child=ticks};
   for(int i=0;i<10;i++)
   {
    var cell=new Grid{Width=7,Height=16};
    cell.Children.Add(new Border{CornerRadius=new CornerRadius(2),Background=Brush.Parse("#28414B50")});
    segments[i]=new Border{CornerRadius=new CornerRadius(2),Background=Brush.Parse("#F0E4ECE2")};
    cell.Children.Add(segments[i]);ticks.Children.Add(cell);
   }
   label.Children.Add(track);
   Minus=MakeButton("−",title+UiText.Get("DeviceDetailsView.Text.6611"));Plus=MakeButton("＋",title+UiText.Get("DeviceDetailsView.Text.6643"));
   value=Text("",26);value.HorizontalAlignment=Avalonia.Layout.HorizontalAlignment.Center;value.VerticalAlignment=Avalonia.Layout.VerticalAlignment.Center;
   Grid.SetColumn(Minus,1);Grid.SetColumn(value,2);Grid.SetColumn(Plus,3);grid.Children.Add(Minus);grid.Children.Add(value);grid.Children.Add(Plus);
   Minus.Click+=(_,_)=>Change(-1);Plus.Click+=(_,_)=>Change(1);
   Value=initial;Change(0);
   foreach(var segment in segments)segment.Transitions=new Transitions{new DoubleTransition{Property=OpacityProperty,Duration=TimeSpan.FromMilliseconds(220),Easing=new CubicEaseOut()}};
  }
  static Button MakeButton(string symbol,string name)
  {
   var b=new Button{Content=symbol,Width=64,Height=64,FontSize=28,Padding=new Thickness(0)};b.Classes.Add("secondary");AutomationProperties.SetName(b,name);return b;
  }
  public void ShowLive(int? raw,bool enabled)
  {
   live=true;LiveState.Observe(raw,enabled);RenderLive();
  }
  public bool ImmediateFeedback {get;set;}
  public void RenderLive()
  {
   int? raw=ImmediateFeedback?LiveState.Target??LiveState.Confirmed:LiveState.Confirmed;Value=raw??0;
   string label=raw?.ToString()??"—";if(value.Text!=label)value.Text=label;
   for(int i=0;i<10;i++){double opacity=raw.HasValue?Level(i):0;if(segments[i].Opacity!=opacity)segments[i].Opacity=opacity;}
   Minus.IsEnabled=LiveState.CanDecrease;Plus.IsEnabled=LiveState.CanIncrease;
   ToolTip.SetTip(this,LiveState.Target is int target?UiText.Format("Sound.PendingValue",target):null);
  }
  public void ShowReadOnly(int? raw)
  {
   Value=raw??0;value.Text=raw?.ToString()??"—";
   for(int i=0;i<10;i++)segments[i].Opacity=raw.HasValue?Level(i):0;
   Minus.IsEnabled=Plus.IsEnabled=false;
  }
  public void Change(int delta)
  {
   if(live){Requested?.Invoke(delta);return;}
   Value=Math.Clamp(Value+delta,LiveState.Minimum,Maximum);value.Text=Value.ToString();
   for(int i=0;i<10;i++)segments[i].Opacity=Level(i);
   Minus.IsEnabled=Value>1;Plus.IsEnabled=Value<Maximum;
  }
 }
}
public sealed partial class ConnectionPreviewWindow
{
 bool openingDeviceDetails;
 async Task ShowDeviceDetails(Control? source=null,bool fromSelection=false)
 {
  if(!PreviewMode && (upgradeBusy || controlReconnectBusy))return;
  if(!PreviewMode && liveConnection.Current is not {Connected:true})
  {
   var button=this.FindControl<Button>("SelectionDetailsButton")!;
   var caption=button.Content;button.IsEnabled=false;button.Content=UiText.Get("Upgrade.ControlConnecting");
   try{upgradeTask=RestorePostInstallControlAsync();await upgradeTask;}
   finally{button.Content=caption;button.IsEnabled=true;}
   if(liveConnection.Current is not {Connected:true})
   {
    this.FindControl<FirmwareChooser>("FirmwareChooser")!.SetStatus(controlReconnectError??UiText.Get("Upgrade.ControlDisconnected"));
    return;
   }
  }
  if(PageTransitionBusy || motionClosed)return;openingDeviceDetails=true;
  transferTimer.Stop();
  var device=this.FindControl<Viewbox>("DevicePage")!;
  var progress=this.FindControl<Viewbox>("TransferPage")!;
  var selection=this.FindControl<Viewbox>("SelectionPage")!;
  var chooser=this.FindControl<FirmwareChooser>("FirmwareChooser")!;
  var target=this.FindControl<DeviceDetailsView>("DeviceDetails")!.ReturnButton;
  var layer=this.FindControl<Canvas>("ConnectionMotionLayer")!;
  device.Opacity=0;device.IsVisible=true;device.IsHitTestVisible=false;
  UpdateLayout();
  Rect BoundsOf(Control c){var a=c.TranslatePoint(new Point(),this)!.Value;var b=c.TranslatePoint(new Point(c.Bounds.Width,c.Bounds.Height),this)!.Value;return new Rect(a,b);}
  var end=BoundsOf(target);var start=source==null?end:BoundsOf(source);
  var label=new TextBlock{Text=fromSelection?UiText.Get("DeviceDetailsView.Text.10743"):UiText.Get("DeviceDetailsView.Text.10751"),Foreground=Brush.Parse("#343A33"),HorizontalAlignment=Avalonia.Layout.HorizontalAlignment.Center,VerticalAlignment=Avalonia.Layout.VerticalAlignment.Center};
  var proxy=new Border{Child=label,Background=Brush.Parse(fromSelection?"#D8EFF0E9":"#C4EFF0E9"),BorderBrush=Brush.Parse("#48FFFFFF")};
  if(source!=null){target.Opacity=0;source.Opacity=0;if(fromSelection)chooser.SetActionVisible(false);layer.Children.Add(proxy);layer.IsVisible=true;}

  try
  {
   await Animate(360,t=>{
    double e=1-Math.Pow(1-t,3);device.Opacity=Math.Clamp((t-.45)/.55,0,1);progress.Opacity=selection.Opacity=Math.Clamp(1-t/.45,0,1);
    if(source!=null)
    {
     double h=start.Height+(end.Height-start.Height)*e;
     Canvas.SetLeft(proxy,start.X+(end.X-start.X)*e);Canvas.SetTop(proxy,start.Y+(end.Y-start.Y)*e);
     proxy.Width=start.Width+(end.Width-start.Width)*e;proxy.Height=h;proxy.CornerRadius=new CornerRadius(h/2);proxy.BorderThickness=new Thickness(h/64);
     label.FontSize=19*h/64;label.Text=t<.45?(fromSelection?UiText.Get("DeviceDetailsView.Text.10743"):UiText.Get("DeviceDetailsView.Text.10751")):UiText.Get("DeviceDetailsView.Text.10761");
     label.Opacity=t<.45?1-t/.45:(t-.45)/.55;
     proxy.Background=new SolidColorBrush(Color.FromArgb((byte)Math.Round((fromSelection?216:196)+(fromSelection?-20:0)*e),239,240,233));
    }
   });
  }
  finally
  {
   layer.Children.Remove(proxy);layer.IsVisible=false;target.Opacity=1;if(source!=null)source.Opacity=1;if(fromSelection)chooser.SetActionVisible(true);
   progress.IsVisible=selection.IsVisible=false;progress.Opacity=selection.Opacity=1;
   device.Opacity=1;device.IsHitTestVisible=true;openingDeviceDetails=false;
  }
 }
}


