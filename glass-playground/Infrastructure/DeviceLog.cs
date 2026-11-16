using System;
using System.IO;
using System.Text.Json;

namespace Middleton.Desktop;
// Local diagnostic evidence only; there is no automatic upload. Callers may include
// speaker serials, Bluetooth addresses and filesystem paths. Redact these before
// attaching logs to a public issue; logs are not a safe-to-publish telemetry format.
public static class DeviceLog
{
 static readonly object Sync=new();
 // Explicit test-run override; normal installations always use the user's app-data directory.
 public static string DirectoryPath => Environment.GetEnvironmentVariable("MIDDLETON_LOG_DIRECTORY") is {Length:>0} path ? Path.GetFullPath(path) : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"MiddletonDesktop","logs");
 static readonly string RunId=Guid.NewGuid().ToString("N");
 public static void Write(string stage,object? data=null)
 {
  try {lock(Sync){Directory.CreateDirectory(DirectoryPath);File.AppendAllText(Path.Combine(DirectoryPath,$"readonly-{RunId}.jsonl"),JsonSerializer.Serialize(new{time=DateTimeOffset.UtcNow,session=RunId,stage,data})+Environment.NewLine);}}
  catch(IOException) {} catch(UnauthorizedAccessException) {}
 }
}
