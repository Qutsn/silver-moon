using Middleton.Desktop;

static class OfficialUpgradeChecks
{
 public static async Task Run()
 {
  if(TestFirmware.SkipMaterialChecks){Console.WriteLine("SKIP: official package upgrade checks");return;}
  var bytes=await File.ReadAllBytesAsync(TestFirmware.Official);
  var link=new FakeUpgradeLink(bytes.Length);
  var coordinator=new OfficialUpgradeCoordinator();
  var evidence=await coordinator.RunAsync(bytes,"test-serial",link,default);
  if(!evidence.IsSuccessful || coordinator.State!=InstallationState.Complete || !link.Codes.SequenceEqual(new byte[]{19,1,21,4,22,12,19,1,14,16}))throw new Exception("Official handshake must reach independent readback");
  foreach(var mode in new[]{"early-complete","wrong-trial","not-idle","wrong-id","negative-ack"})
  {
   link=new FakeUpgradeLink(bytes.Length){Mode=mode};coordinator=new OfficialUpgradeCoordinator();
   try{await coordinator.RunAsync(bytes,"test-serial",link,default);throw new Exception($"Accepted {mode}");}catch(UpgradeProtocolException){}
   if(coordinator.State==InstallationState.Complete || link.Codes.Contains((byte)7))throw new Exception("Failure cannot claim completion or auto-abort");
  }
  Console.WriteLine("PASS: official handshake, premature completion, identity, readback and rejection gates");
 }
 sealed class FakeUpgradeLink : IOfficialUpgradeLink
 {
  readonly Queue<UpgradeMessage> messages=new();
  public string Mode="";
  public List<byte> Codes=new();
  public FakeUpgradeLink(int size)
  {
   messages.Enqueue(new(20,Convert.FromHexString("009140830703")));
   messages.Enqueue(new(2,new byte[]{0,6,102}));
   byte[] request=new byte[8];System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(request,1);
   System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(request.AsSpan(4),(uint)(size-1));
   messages.Enqueue(new(3,request));messages.Enqueue(new(11,Array.Empty<byte>()));
   messages.Enqueue(new(20,Convert.FromHexString("039140830703")));
   messages.Enqueue(new(2,new byte[]{0,6,102}));messages.Enqueue(new(15,Array.Empty<byte>()));messages.Enqueue(new(18,Array.Empty<byte>()));
  }
  public Task<byte> SendAcknowledgedAsync(ReadOnlyMemory<byte> frame,CancellationToken ct)
  {
   ct.ThrowIfCancellationRequested();if(frame.Span[3]==0x42)Codes.Add(frame.Span[4]);
   return Task.FromResult((byte)(Mode=="negative-ack"?5:0));
  }
  public Task<UpgradeMessage> ReceiveAsync(CancellationToken ct)
  {
   ct.ThrowIfCancellationRequested();var next=messages.Dequeue();
   if(Mode=="early-complete" && next.Code==3)next=new(11,Array.Empty<byte>());
   if(Mode=="wrong-id" && next.Code==20)next=new(20,Convert.FromHexString("000000000003"));
   return Task.FromResult(next);
  }
  public Task<UpgradeDeviceReadback> ReconnectAfterTrialAsync(CancellationToken ct)=>Task.FromResult(new UpgradeDeviceReadback("MIDDLETON","test-serial",Mode=="wrong-trial"?"25.2.52":"17.0.37",false));
  public Task<UpgradeDeviceReadback> ReadCurrentAsync(CancellationToken ct)=>Task.FromResult(new UpgradeDeviceReadback("MIDDLETON","test-serial","25.2.52",true));
  public Task<UpgradeDeviceReadback> ReadIndependentlyAsync(CancellationToken ct)=>Task.FromResult(new UpgradeDeviceReadback("MIDDLETON","test-serial","17.0.37",Mode!="not-idle"));
 }
}
