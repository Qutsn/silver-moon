using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
namespace GlassPlayground;
static class Program {
 [STAThread] public static void Main(string[] args) {
  if(args.Length==2 && args[0]=="--portable-smoke"){Environment.ExitCode=System.Threading.Tasks.Task.Run(()=>Middleton.Desktop.PortableValidation.RunAsync(args[1])).GetAwaiter().GetResult();return;}
  if(args.Contains("--upgrade-resume-probe")){Environment.ExitCode=System.Threading.Tasks.Task.Run(()=>Middleton.Desktop.UpgradeResumeProbe.RunAsync(args)).GetAwaiter().GetResult();return;}
  if(args.Contains("--upgrade-worker")){Environment.ExitCode=System.Threading.Tasks.Task.Run(()=>Middleton.Desktop.UpgradeWorker.RunAsync()).GetAwaiter().GetResult();return;}
  if(args.Contains("--upgrade-readonly-probe")){Environment.ExitCode=System.Threading.Tasks.Task.Run(()=>Middleton.Desktop.UpgradeReadOnlyProbe.RunAsync(args)).GetAwaiter().GetResult();return;}
  Middleton.Desktop.DeviceLog.Write("startup",new{process=Environment.ProcessPath,baseDirectory=AppContext.BaseDirectory});
  if(args.Contains("--sound-control-probe")){Environment.ExitCode=System.Threading.Tasks.Task.Run(()=>Middleton.Desktop.SoundControlProbe.RunAsync(args)).GetAwaiter().GetResult();return;}
  if(args.Contains("--ble-readonly-probe")){Environment.ExitCode=System.Threading.Tasks.Task.Run(()=>Middleton.Desktop.ReadOnlyProbe.RunAsync(args)).GetAwaiter().GetResult();return;}
  AppDomain.CurrentDomain.UnhandledException+=(_,e)=>Middleton.Desktop.DeviceLog.Write("unhandled-error",e.ExceptionObject.ToString());
  try { var b=AppBuilder.Configure<App>().UsePlatformDetect();
   if(args.Contains("--software")) b=b.With(new Win32PlatformOptions{RenderingMode=new[]{Win32RenderingMode.Software}});
   else if(args.Contains("--redirection")) b=b.With(new Win32PlatformOptions{CompositionMode=new[]{Win32CompositionMode.RedirectionSurface}});
   b.StartWithClassicDesktopLifetime(args);
  }catch(Exception e){Middleton.Desktop.DeviceLog.Write("startup-error",e.ToString());Environment.ExitCode=1;}
 }
}
public sealed class App:Application {
 public override void Initialize()=>AvaloniaXamlLoader.Load(this);
 public override void OnFrameworkInitializationCompleted(){
  if(ApplicationLifetime is IClassicDesktopStyleApplicationLifetime d){
   if(d.Args is {Length:0} || d.Args?.Contains("--connection")==true || d.Args?.Contains("--preview")==true || d.Args?.Contains("--readonly-smoke")==true || d.Args?.Contains("--discovery-smoke")==true){var connection=new ConnectionPreviewWindow();d.MainWindow=connection;if(d.Args?.Contains("--sound-ui-smoke")==true)connection.Opened+=async(_,_)=>{try{await new DeviceDetailsView().CheckLiveControlsAsync();d.Shutdown(0);}catch(Exception e){Middleton.Desktop.DeviceLog.Write("sound-ui-smoke-failed",e.ToString());d.Shutdown(1);}};if(d.Args?.Contains("--live-sound-observe")==true)connection.Opened+=async(_,_)=>{try{await connection.ObserveLiveSoundAsync();}catch(Exception e){Middleton.Desktop.DeviceLog.Write("sound-ui-observe-failed",e.ToString());d.Shutdown(1);}};if(d.Args?.Contains("--discovery-smoke")==true)connection.Opened+=async(_,_)=>{try{await connection.DiscoverySmokeAsync();d.Shutdown(0);}catch(Exception e){Middleton.Desktop.DeviceLog.Write("discovery-ui-smoke-failed",e.ToString());d.Shutdown(1);}};if(d.Args?.Contains("--readonly-smoke")==true)connection.Opened+=async(_,_)=>{try{await connection.ReadOnlySmokeAsync();d.Shutdown(0);}catch(Exception e){Middleton.Desktop.DeviceLog.Write("readonly-ui-smoke-failed",e.ToString());d.Shutdown(1);}};if(d.Args?.Contains("--connection-smoke")==true)connection.Opened+=async(_,_)=>{try{await connection.Smoke();d.Shutdown(0);}catch(Exception e){File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"connection-error.txt"),e.ToString());d.Shutdown(1);}};base.OnFrameworkInitializationCompleted();return;}
   if(d.Args?.Contains("--concept")==true){var concept=new ConceptWindow();d.MainWindow=concept;if(d.Args.Contains("--concept-smoke"))concept.Opened+=async(_,_)=>{try{await concept.Smoke();d.Shutdown(0);}catch(Exception e){File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"concept-error.txt"),e.ToString());d.Shutdown(1);}};base.OnFrameworkInitializationCompleted();return;}
   if(d.Args?.Contains("--glass-preview")==true){
    var preview=new GlassPreviewWindow();
    if(d.Args.Contains("--preview-solid"))preview.TransparencyLevelHint=new[]{Avalonia.Controls.WindowTransparencyLevel.None};
    if(d.Args.Contains("--preview-black")){preview.TransparencyLevelHint=new[]{Avalonia.Controls.WindowTransparencyLevel.None};preview.Background=Avalonia.Media.Brushes.Black;}
    if(d.Args.Contains("--preview-smoke"))preview.Opened+=async(_,_)=>{try{await System.Threading.Tasks.Task.Delay(1200);using var bitmap=new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(960,680));bitmap.Render(preview);bitmap.Save(Path.Combine(AppContext.BaseDirectory,d.Args.Contains("--preview-black")?"preview-black.png":d.Args.Contains("--preview-solid")?"preview-solid.png":"preview-native-content.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);d.Shutdown(0);}catch(Exception e){File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"preview-error.txt"),e.ToString());d.Shutdown(1);}};
    d.MainWindow=preview;base.OnFrameworkInitializationCompleted();return;
   }
   var w=new MainWindow();d.MainWindow=w;
   if(d.Args?.Contains("--smoke")==true)w.Opened+=async(_,_)=>{try{await w.Smoke();d.Shutdown(0);}catch(Exception e){File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"test-error.txt"),e.ToString());d.Shutdown(1);}};
   else if(d.Args?.Contains("--native")==true)w.Opened+=(_,_)=>w.OpenNative();
  }base.OnFrameworkInitializationCompleted();
 }
}
