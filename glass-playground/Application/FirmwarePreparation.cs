using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Middleton.Desktop;

/// <summary>
/// Downloads only the reviewed production image, then applies a deterministic
/// local patch. No firmware is hosted here. API reachability is not a license.
/// Network metadata cannot expand the supported hashes or patch instructions.
/// </summary>
public sealed class FirmwarePreparation(string cacheDirectory,HttpClient client)
{
 public const string OfficialSha="db74c3468bd08d61f3a4e4e4e9e6eea9b19d95d6e852b9fd5e9cbac3e91c9472";
 public const string ModifiedSha="80ed2088247976f10968f5d4aefb3cabf367ff0b0a1f774ac6e62a292c459024";
 const int Size=3266308;
 const string DownloadPath="/devices/Lennox/prod/Lennox_FW_V17.0.37.bin";
 public static string DefaultCache=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"MiddletonDesktop","firmware-cache");
 public static HttpClient CreateClient()
 {
  var client=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromMinutes(3)};
  client.DefaultRequestHeaders.UserAgent.ParseAdd("SilveredMoon/1.0");
  return client;
 }
 static bool Matches(byte[] data,string hash)=>Convert.ToHexString(SHA256.HashData(data)).Equals(hash,StringComparison.OrdinalIgnoreCase);
 static InvalidDataException Rejected()=>new(AppText.Get("Firmware.PreparationRejected"));
 public static byte[] BuildModified(byte[] original)
 {
  if(original.Length!=Size || !Matches(original,OfficialSha))throw Rejected();
  var result=(byte[])original.Clone();
  // Exact 18-byte delta from the reviewed builder: two AUX calls, two idle
  // shutdown stores and the display marker. Never pattern-match unknown images.
  (int offset,byte before,byte after)[] edits={
   (2501504,1,0),(2501505,240,191),(2501506,224,0),(2501507,255,191),
   (2524294,16,0),(2524295,112,191),(2629194,4,0),(2629195,240,191),
   (2629196,192,0),(2629197,250,191),(2638570,16,0),(2638571,112,191),
   (2799268,17,1),(2799270,40,52),(2799271,130,64),(2799278,48,82),
   (2799279,207,224),(2799282,81,89)};
  foreach(var e in edits){if(result[e.offset]!=e.before)throw Rejected();result[e.offset]=e.after;}
  if(!Matches(result,ModifiedSha))throw Rejected();
  return result;
 }
 public async Task<byte[]> PrepareAsync(bool modified,Action<string>? status,CancellationToken ct)
 {
  ct.ThrowIfCancellationRequested();
  using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);deadline.CancelAfter(TimeSpan.FromMinutes(3));ct=deadline.Token;
  string cache=Path.Combine(cacheDirectory,"official-17.0.37.bin");
  byte[]? original=null;
  if(File.Exists(cache))
  {
   status?.Invoke(AppText.Get("Firmware.CheckingCache"));
   try
   {
    await using var input=new FileStream(cache,FileMode.Open,FileAccess.Read,FileShare.Read);
    var candidate=await ReadBounded(input,Size,ct);
    if(candidate.Length==Size && Matches(candidate,OfficialSha))original=candidate;
   }
   catch(IOException){/* A corrupt cache is not trusted; retry the official source. */}
  }
  if(original is null)
  {
   status?.Invoke(AppText.Get("Firmware.Downloading"));
   // Historical production API contract. A placeholder avoids transmitting the
   // user's device serial; this is not an assertion of official client status.
   using var request=new HttpRequestMessage(HttpMethod.Post,"https://fota.marshall.software/msdk/firmware")
   {Content=JsonContent.Create(new{channel="prod",device=new{codename="Lennox",serial_number="x",firmware_version="0.0.1"},client=new{type="marshall-app",platform="android",version="3.9.2"}})};
   using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,ct);
   response.EnsureSuccessStatusCode();
   await using var metadataStream=await response.Content.ReadAsStreamAsync(ct);
   using var metadata=JsonDocument.Parse(await ReadBounded(metadataStream,65536,ct));
   var root=metadata.RootElement;
   if(root.GetProperty("version").GetString()!="17.0.37")throw new InvalidDataException(AppText.Get("Firmware.UnsupportedDownload"));
   var fw=root.GetProperty("firmware");
   if(fw.GetProperty("name").GetString()!="Lennox_FW_V17.0.37.bin" || fw.GetProperty("size").GetInt64()!=Size)throw Rejected();
   if(!Uri.TryCreate(fw.GetProperty("url").GetString(),UriKind.Absolute,out var uri) || uri.Scheme!="https" || uri.Host!="fota.marshall.software" || !uri.IsDefaultPort || uri.UserInfo!="" || uri.AbsolutePath!=DownloadPath || uri.Fragment!="")throw Rejected();
   // Redirects are disabled in the production client: a server response cannot
   // send the downloader to an unrelated host or local network endpoint.
   using var download=await client.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,ct);download.EnsureSuccessStatusCode();
   if(download.Content.Headers.ContentLength is long length && length!=Size)throw Rejected();
   await using var stream=await download.Content.ReadAsStreamAsync(ct);
   original=await ReadBounded(stream,Size,ct);
   if(original.Length!=Size || !Matches(original,OfficialSha))throw Rejected();
   Directory.CreateDirectory(cacheDirectory);
   string temp=Path.Combine(cacheDirectory,Guid.NewGuid().ToString("N")+".tmp");
   try{await File.WriteAllBytesAsync(temp,original,ct);ct.ThrowIfCancellationRequested();File.Move(temp,cache,true);}
   finally{if(File.Exists(temp))File.Delete(temp);}
  }
  ct.ThrowIfCancellationRequested();status?.Invoke(AppText.Get("Firmware.Preparing"));
  return modified?BuildModified(original):original;
 }
 static async Task<byte[]> ReadBounded(Stream stream,int maximum,CancellationToken ct)
 {
  using var result=new MemoryStream();var buffer=new byte[65536];
  int count;while((count=await stream.ReadAsync(buffer,ct))!=0)
  {if(result.Length+count>maximum)throw Rejected();result.Write(buffer,0,count);}
  return result.ToArray();
 }
}
