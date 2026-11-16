using Middleton.Desktop;

static class FirmwareChecks
{
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 public static async Task Run()
 {
  using var empty=new MemoryStream();
  try{await FirmwareImport.AnalyzeAsync(empty,default);throw new Exception("Empty file accepted");}catch(InvalidDataException){}
  using var stream=new MemoryStream(new byte[]{1,2,3});
  Check((await FirmwareImport.AnalyzeAsync(stream,default)).Known is null,"Unknown file must remain unrecognized");
  using var oversized=new MemoryStream(new byte[FirmwareImport.MaximumLength+1]);
  try{await FirmwareImport.AnalyzeAsync(oversized,default);throw new Exception("Oversized import accepted");}catch(InvalidDataException){}
  using var cancelled=new CancellationTokenSource();cancelled.Cancel();
  try{await FirmwareImport.AnalyzeAsync(stream,cancelled.Token);throw new Exception("Cancelled import accepted");}catch(OperationCanceledException){}
  if(TestFirmware.SkipMaterialChecks){Console.WriteLine("SKIP: real firmware identity checks (explicit source-only mode)");return;}
  string official=TestFirmware.Official, r2=TestFirmware.R2;
  foreach(var (file,strategy) in new[]{(official,FirmwareStrategy.OfficialFull),(r2,FirmwareStrategy.MiddletonR2Staged)})
  {
   await using var actual=File.OpenRead(file);var analysis=await FirmwareImport.AnalyzeAsync(actual,default);
   Check(analysis.Known?.Strategy==strategy && analysis.CanInstallOn("middleton"),"Recompute actual package identity");
   Check(analysis.StructureError is null,"Known container structure");
   Check(!analysis.CanInstallOn("acton"),"Known package must not accept other device profiles");
   byte[] changed=await File.ReadAllBytesAsync(file);
   Check(FirmwareContainer.CheckMiddletonV1(changed.AsSpan(0,changed.Length-1)) is not null,"Reject truncated footer");
   Check(FirmwareContainer.CheckMiddletonV1(changed.Concat(new byte[]{0}).ToArray()) is not null,"Reject trailing data");
   var bad=(byte[])changed.Clone();bad[8]=255;
   Check(FirmwareContainer.CheckMiddletonV1(bad) is not null,"Reject oversized record length");
   bad=(byte[])changed.Clone();bad[75]=99;
   Check(FirmwareContainer.CheckMiddletonV1(bad) is not null,"Reject partition mismatch");
   changed[changed.Length/2]^=1;
   Check(FirmwareContainer.CheckMiddletonV1(changed) is null,"Payload changes can be structural but untrusted");
   Check(FirmwareCatalog.Analyze(changed).Known is null,"One changed byte must invalidate trusted strategy");
  }
  Console.WriteLine("PASS: real official/r2 identities, changed/unknown files, cancelled import");
 }
}
