using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Middleton.Desktop;
// Offline packaging check: deliberately never opens a BLE link.
public static class PortableValidation
{
 public static async Task<int> RunAsync(string reportPath)
 {
  try
  {
   int bundledFirmwareCount=0;
   foreach(var file in new[]{"official.bin","r2.bin"})
   {
    if(!File.Exists(Path.Combine(AppContext.BaseDirectory,"Firmware",file)))continue;
    bundledFirmwareCount++;
    var bytes=await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory,"Firmware",file));
    if(FirmwareCatalog.Analyze(bytes).Known is null)throw new InvalidDataException("Bundled firmware hash mismatch: "+file);
   }
   var licenseDirectory=Path.Combine(AppContext.BaseDirectory,"licenses");
   if(!File.Exists(Path.Combine(licenseDirectory,"PROJECT-LICENSE.txt")) || !File.Exists(Path.Combine(licenseDirectory,"packages.json")))throw new InvalidDataException("Missing bundled license notices.");
   var assembly=typeof(PortableValidation).Assembly;
   foreach(var culture in new[]{"en","zh"})
   {
    using var resource=assembly.GetManifestResourceStream($"GlassPlayground.Localization.{culture}.json")??throw new InvalidDataException("Missing localization resource: "+culture);
    using var document=await JsonDocument.ParseAsync(resource);
   }
   var executable=Environment.ProcessPath??throw new InvalidOperationException("Missing process path.");
   if(string.Equals(Path.GetFileNameWithoutExtension(executable),"dotnet",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Portable validation must run the EXE directly.");
   var start=new ProcessStartInfo(executable){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
   start.ArgumentList.Add("--upgrade-worker");
   using var child=Process.Start(start)??throw new InvalidOperationException("Cannot launch worker.");
   try
   {
    using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));
    var errors=child.StandardError.ReadToEndAsync(timeout.Token);
    await child.StandardInput.WriteLineAsync("{\"Operation\":\"close\"}");await child.StandardInput.FlushAsync();
    var line=await child.StandardOutput.ReadLineAsync(timeout.Token)??throw new InvalidDataException("Worker returned no reply.");
    using var reply=JsonDocument.Parse(line);
    if(reply.RootElement.GetProperty("Error").ValueKind!=JsonValueKind.Null || reply.RootElement.GetProperty("Value").ValueKind!=JsonValueKind.True)throw new InvalidDataException("Worker close failed: "+line);
    await child.WaitForExitAsync(timeout.Token);
    if(child.ExitCode!=0)throw new InvalidOperationException("Worker failed: "+await errors);
   }
   finally{if(!child.HasExited)child.Kill(true);}
   await File.WriteAllTextAsync(reportPath,JsonSerializer.Serialize(new{passed=true,process=executable,extractionDirectory=AppContext.BaseDirectory,bundledFirmwareCount,firmwareHashes=true,localizationResources=true,licenseNotices=true,workerStdio=true,bleOperations=0}));
   return 0;
  }
  catch(Exception error){await File.WriteAllTextAsync(reportPath,JsonSerializer.Serialize(new{passed=false,error=error.ToString()}));return 1;}
 }
}

