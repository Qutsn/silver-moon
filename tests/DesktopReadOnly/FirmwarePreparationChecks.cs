using System.Net;
using Middleton.Desktop;

static class FirmwarePreparationChecks
{
 public static async Task Run()
 {
  if(TestFirmware.SkipMaterialChecks)return;
  byte[] original=await File.ReadAllBytesAsync(TestFirmware.Official), expected=await File.ReadAllBytesAsync(TestFirmware.R2);
  if(!FirmwarePreparation.BuildModified(original).SequenceEqual(expected))throw new Exception("Local patch differs from accepted r2");
  var damaged=(byte[])original.Clone();damaged[20]^=1;
  try{FirmwarePreparation.BuildModified(damaged);throw new Exception("Unknown input accepted");}catch(InvalidDataException){}
  string dir=Path.Combine(Path.GetTempPath(),"firmware-preparation-"+Guid.NewGuid().ToString("N"));
  try
  {
   var handler=new Handler(original);using var client=new HttpClient(handler);
   var service=new FirmwarePreparation(dir,client);
   if(!(await service.PrepareAsync(true,null,default)).SequenceEqual(expected) || handler.Calls!=2)throw new Exception("Download/patch failed");
   handler.Fail=true;
   if(!(await service.PrepareAsync(false,null,default)).SequenceEqual(original) || handler.Calls!=2)throw new Exception("Offline cache failed");
   await File.WriteAllBytesAsync(Path.Combine(dir,"official-17.0.37.bin"),damaged);
   try{await service.PrepareAsync(false,null,default);throw new Exception("Corrupted cache accepted");}catch(HttpRequestException){}
   handler.Fail=false;handler.Version="99.0.0";
   try{await service.PrepareAsync(false,null,default);throw new Exception("Unknown server version accepted");}catch(InvalidDataException){}
   handler.Version="17.0.37";handler.Url="https://example.com/firmware.bin";
   try{await service.PrepareAsync(false,null,default);throw new Exception("Foreign download accepted");}catch(InvalidDataException){}
   handler.Url=Handler.OfficialUrl;handler.Image=damaged;
   try{await service.PrepareAsync(false,null,default);throw new Exception("Wrong download hash accepted");}catch(InvalidDataException){}
   handler.Image=new byte[original.Length+1];
   try{await service.PrepareAsync(false,null,default);throw new Exception("Oversize download accepted");}catch(InvalidDataException){}
   using var cancelled=new CancellationTokenSource();cancelled.Cancel();int calls=handler.Calls;
   try{await service.PrepareAsync(false,null,cancelled.Token);throw new Exception("Cancelled request accepted");}catch(OperationCanceledException){}
   if(handler.Calls!=calls)throw new Exception("Cancelled request reached network");
   Console.WriteLine("PASS: exact local r2 generation, download whitelist, offline cache, corruption, size and cancellation");
  }
  finally{if(Directory.Exists(dir))Directory.Delete(dir,true);}
 }
 sealed class Handler(byte[] image):HttpMessageHandler
 {
  public const string OfficialUrl="https://fota.marshall.software/devices/Lennox/prod/Lennox_FW_V17.0.37.bin?Signature=test";
  public int Calls;public bool Fail;public string Version="17.0.37",Url=OfficialUrl;public byte[] Image=image;
  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
  {
   Calls++;if(Fail)throw new HttpRequestException("Offline");
   HttpContent content=request.Method==HttpMethod.Post?new StringContent(System.Text.Json.JsonSerializer.Serialize(new{version=Version,firmware=new{name="Lennox_FW_V17.0.37.bin",size=3266308,url=Url}})):new ByteArrayContent(Image);
   return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=content});
  }
 }
}
