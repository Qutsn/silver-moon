using Middleton.Desktop;

static class UpgradeChecks
{
 static void Check(bool ok,string reason){if(!ok)throw new Exception(reason);}
 static void Reject(Action action){try{action();}catch(UpgradeProtocolException){return;}throw new Exception("Invalid protocol accepted");}
 public static async Task Run()
 {
  Check(Convert.ToHexString(UpgradeWire.Control(19,Convert.FromHexString("6e0b0188")))=="000A06421300046E0B0188","SYNC wire vector");
  Reject(()=>UpgradeWire.Parse(new byte[]{0,10,0}));
  Reject(()=>UpgradeWire.Parse(Convert.FromHexString("000B864200")));
  Reject(()=>UpgradeWire.Parse(Convert.FromHexString("000A40031203000800000001")));
  Reject(()=>UpgradeWire.Parse(Convert.FromHexString("000A400312120000FF")));
  Check(UpgradeWire.Parse(Convert.FromHexString("000A864200AA")) is GaiaAcknowledgement{Command:0x0642,Status:0},"ACK may carry extra bytes");
  Check(UpgradeWire.Parse(Convert.FromHexString("000A40031217000203E8")) is UpgradeNotification{Message.Code:23} n && UpgradeWire.ValidationDelay(n.Message)==TimeSpan.FromSeconds(1),"Validation value is milliseconds");
  // Deterministic hostile-input smoke: protocol rejection is expected, runtime
  // slicing/overflow exceptions are bugs. This is bounded sampling, not full fuzzing.
  var random=new Random(20261115);
  for(int i=0;i<5000;i++)
  {
   var raw=new byte[random.Next(0,512)];random.NextBytes(raw);
   if(raw.Length>=4 && i%2==0){raw[0]=0;raw[1]=10;raw[2]=0x40;raw[3]=3;}
   try{UpgradeWire.Parse(raw);}catch(UpgradeProtocolException){}
   FirmwareContainer.CheckMiddletonV1(raw);
  }
  var image=Enumerable.Range(0,100).Select(i=>(byte)i).ToArray();
  var pump=new UpgradeDataTransfer(image);
  var frames=new List<byte[]>();
  await pump.SendRequestedAsync(50,10,(f,ct)=>{frames.Add(f.ToArray());return Task.FromResult((byte)0);},default);
  Check(frames.Count==2 && frames[0].Length==53 && frames[1].Length==13,"45 byte content limit");
  Check(frames[0][7]==0 && frames[0][8]==10,"Skip affects cursor, not payload prefix");
  Check(pump.Progress==new UpgradeTransferProgress(60,50,50,10),"Separate cursor, sent, acked and skipped");
  await pump.SendRequestedAsync(40,0,(f,ct)=>{frames.Add(f.ToArray());return Task.FromResult((byte)0);},default);
  Check(frames[^1][7]==1 && pump.Progress.Cursor==100,"Only EOF sets last");
  try{await pump.SendRequestedAsync(uint.MaxValue,1,(f,ct)=>throw new Exception("Invalid request reached transport"),default);throw new Exception("Accepted overflow");}catch(UpgradeProtocolException){}
  var failed=new UpgradeDataTransfer(image);
  try{await failed.SendRequestedAsync(50,0,(f,ct)=>Task.FromResult((byte)5),default);throw new Exception("Accepted negative ACK");}catch(UpgradeProtocolException){}
  Check(failed.Progress.SentBytes==45 && failed.Progress.AcknowledgedBytes==0 && failed.Progress.Cursor==0,"Rejected data cannot advance confirmed progress");
  try{await failed.SendRequestedAsync(50,0,(f,ct)=>throw new Exception("Ambiguous session reused"),default);throw new Exception("Failed session reused");}catch(UpgradeProtocolException){}
  var timeout=new UpgradeDataTransfer(image);
  try{await timeout.SendRequestedAsync(45,0,(f,ct)=>throw new TimeoutException(),default);throw new Exception("Timeout swallowed");}catch(TimeoutException){}
  Check(timeout.Progress.AcknowledgedBytes==0,"Timeout does not confirm delivery");
  var blocked=new TaskCompletionSource<byte>(TaskCreationOptions.RunContinuationsAsynchronously);
  var concurrent=new UpgradeDataTransfer(image);
  var first=concurrent.SendRequestedAsync(45,0,(f,ct)=>blocked.Task,default);
  try{await concurrent.SendRequestedAsync(45,0,(f,ct)=>Task.FromResult((byte)0),default);throw new Exception("Concurrent request accepted");}catch(UpgradeProtocolException){}
  blocked.SetResult(0);await first;
  var unknown=FirmwareCatalog.Analyze(image);
  Check(unknown.Known is null && !unknown.CanInstallOn("middleton"),"Unknown bytes never inherit r2 strategy");
  Console.WriteLine("Upgrade protocol and data transfer checks passed.");
 }
}
