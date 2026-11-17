using Middleton.Desktop;
if(args.Contains("--replacement-completion-check")){await ResumableUpgradeChecks.CheckReplacementWithOldCompletion();return;}
{
 var transport=new FakeTransport{UnknownEq=true};
 await using var control=new DeviceConnection(()=>transport);
 var initialControl=await control.ConnectAsync(new("test","MIDDLETON","test"),default);
 Check(!initialControl.Capabilities.Eq && initialControl.Bass is null,"Initial invalid EQ must not become ready");
 transport.UnknownEq=false;
 var refreshed=await control.RefreshControlsAsync(default);
 Check(refreshed.SessionId==initialControl.SessionId && refreshed.Capabilities.Eq && refreshed.Bass==0 && !transport.Disposed && transport.Writes.Count==0,"Refresh must recover EQ without reconnect or writes");
 transport.Changed?.Invoke(DeviceIds.Eq,new byte[8]);
 Check(!control.Current!.Capabilities.Eq && control.Current.Bass is null,"Invalid EQ notification must revoke readiness");
 transport.Changed?.Invoke(DeviceIds.Eq,new byte[]{2,255,255,255,8,0,0,0});
 Check(control.Current!.Capabilities.Eq && control.Current.Bass==2,"Valid notification must restore readiness");
 transport.Changed?.Invoke(DeviceIds.Battery,new byte[]{80});
 int readsBefore=transport.ReadCount;
 await control.RefreshControlsAsync(default,onlyMissing:true);
 Check(transport.ReadCount==readsBefore,"Ready controls must not incur redundant reads");
 transport.Changed?.Invoke(DeviceIds.Eq,new byte[8]);
 await control.RefreshControlsAsync(default,onlyMissing:true);
 Check(transport.ReadCount==readsBefore+1 && control.Current!.Capabilities.Eq,"Only missing EQ should be refreshed");
 transport.Disconnected?.Invoke();
 try{await control.RefreshControlsAsync(default);throw new Exception("Disconnected refresh accepted");}catch(DeviceConnectionException){}
 Console.WriteLine("PASS: same-session read-only refresh, EQ capability recovery and disconnect rejection");
}
if(args.Contains("--control-refresh-check"))return;
if(args.Contains("--journal-check")){JournalStoreChecks.Run();return;}
await PostInstallConnectionChecks.Run();
if(args.Contains("--post-install-check"))return;
if(args.Contains("--stale-transfer-check")){await ResumableUpgradeChecks.CheckStaleTransfer();return;}
if(args.Contains("--final-identity-check")){await ResumableUpgradeChecks.CheckFinalIdentity();return;}

if(args.Contains("--reset-reconnect-check")){await ResumableUpgradeChecks.CheckResetReconnect();return;}

if(args.Contains("--firmware-preparation-check")){await FirmwarePreparationChecks.Run();return;}
if(args.Contains("--firmware-download-check"))
{
 using var client=FirmwarePreparation.CreateClient();
 var cache=args.Length>1?Path.GetFullPath(args[1]):FirmwarePreparation.DefaultCache;
 var bytes=await new FirmwarePreparation(cache,client).PrepareAsync(true,Console.WriteLine,default);
 Console.WriteLine("Prepared SHA256: "+Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)));return;
}
await FirmwarePreparationChecks.Run();

if(args.Contains("--stalled-commit-check")){await ResumableUpgradeChecks.CheckStalledCommit();return;}

static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
Check(DeviceValues.Volume(new byte[]{0}) == 0, "Zero volume must survive decoding");
Check(DeviceValues.Volume(new byte[]{33}) == null, "Reject volume out of range");
Check(DeviceValues.Battery(new byte[]{101}) == null, "Reject invalid battery");
Check(DeviceValues.Volume(new byte[]{1,2}) == null, "Reject malformed volume");
Check(DeviceValues.Eq(new byte[]{0,255,255,255,10,0,0,0}) == (0,10), "Keep EQ zero");
Check(DeviceValues.Eq(new byte[]{1,255,255,255,11,0,0,0}) == null, "Reject invalid EQ");
Check(DeviceValues.Eq(new byte[]{1,255}) == null, "Reject truncated EQ");
Check(SoundCommands.Volume(0).SequenceEqual(new byte[]{0}), "Encode protocol volume zero");
Check(SoundCommands.Volume(32).SequenceEqual(new byte[]{32}), "Encode protocol volume max");
Check(SoundCommands.Eq(2,8).SequenceEqual(new byte[]{2,255,255,255,8,0,0,0}), "Encode EQ layout");
Check(SoundCommands.TryVolume(33) is null && SoundCommands.TryEq(0,11) is null, "Reject write values outside protocol range");
var preservedEq=SoundCommands.UpdateEq(new byte[]{2,255,255,255,8,0,0,0}, bass:5, treble:null);
Check(preservedEq is not null && preservedEq.SequenceEqual(new byte[]{5,255,255,255,8,0,0,0}), "EQ write must preserve the other band");
Check(!new InstallationEvidence(true,true,true,false).IsSuccessful, "No success without independent idle readback");
var scan=new DiscoveryCandidates();var now=DateTimeOffset.UtcNow;
scan.Observe("old","Random","MIDDLETON [LE]",true,now);
scan.Observe("new","Random","MIDDLETON [LE]",true,now.AddSeconds(6));
Check(scan.Snapshot(now.AddSeconds(8)).Count==1 && scan.Snapshot(now.AddSeconds(8))[0].Id=="new","Expired rotating address must disappear");
scan.Observe("second","Public","MIDDLETON [LE]",true,now.AddSeconds(7));
Check(scan.Snapshot(now.AddSeconds(8)).Count==2,"Do not merge two live devices solely by name");
scan.Observe("second","Public","",false,now.AddSeconds(8));
Check(scan.Snapshot(now.AddSeconds(8))[1].Name=="MIDDLETON [LE]","Nameless scan responses must preserve name");
scan.Observe("beacon","Random","MIDDLETON [LE]",false,now.AddSeconds(8));
Check(scan.Snapshot(now.AddSeconds(8)).Count==2,"Nonconnectable advertising must not create a candidate");
Check(!PairingOutcome.Accepted("AccessDenied") && !PairingOutcome.Accepted("Failed") && PairingOutcome.Accepted("AlreadyPaired") && PairingOutcome.Accepted("Paired"),"Pair failure must stop service discovery");
var protections=new List<PairingProtection>();
var negotiation=await PairingNegotiation.RunAsync((level,ct)=>{protections.Add(level);return Task.FromResult(level==PairingProtection.Default?"Paired":"ProtectionLevelCouldNotBeMet");},CancellationToken.None);
Check(negotiation=="Paired" && protections.SequenceEqual(new[]{PairingProtection.EncryptionAndAuthentication,PairingProtection.Encryption,PairingProtection.Default}),"Negotiate the complete supported protection sequence");
protections.Clear();
Check(await PairingNegotiation.RunAsync((level,ct)=>{protections.Add(level);return Task.FromResult("AccessDenied");},CancellationToken.None)=="AccessDenied" && protections.Count==1,"Never downgrade after a permission denial");
using(var stopped=new CancellationTokenSource()){
 stopped.Cancel();try{await PairingNegotiation.RunAsync((level,ct)=>throw new Exception("Cancelled negotiation reached OS"),stopped.Token);throw new Exception("Ignored cancelled pairing");}catch(OperationCanceledException){}
}
scan.Observe("renamed","Random","客厅音箱",true,now.AddSeconds(8));
scan.Observe("anonymous","Random","",true,now.AddSeconds(8));
Check(scan.Snapshot(now.AddSeconds(8)).Any(c=>c.Name=="客厅音箱"),"Renamed devices must be discoverable");
Check(scan.Snapshot(now.AddSeconds(8)).Any(c=>c.Id=="anonymous"),"Unnamed devices must be discoverable");
var sorted=scan.Snapshot(now.AddSeconds(8));
Check(sorted.Take(2).All(c=>c.Name.Contains("Middleton",StringComparison.OrdinalIgnoreCase)),"Middleton names must sort before renamed and unnamed devices");
scan.Observe("mixed","Public","My middleTON speaker",true,now.AddSeconds(8));
Check(scan.Snapshot(now.AddSeconds(8)).Take(3).All(c=>c.Name.Contains("Middleton",StringComparison.OrdinalIgnoreCase)),"Priority must ignore name casing and accept LE suffixes");
Check(DeviceProfiles.Identify("MIDDLETON",null,id=>true).Supported,"Supported profile identified without advertised name");
Check(!DeviceProfiles.Identify("MIDDLETON",null,id=>false).Supported,"Model alone is insufficient");
Check(DeviceProfiles.Identify("ACTON III","Marshall",id=>true).IsMarshall && !DeviceProfiles.Identify("ACTON III","Marshall",id=>true).Supported,"Other Marshall models remain unsupported");
Check(!DeviceProfiles.Identify("OTHER","Other brand",id=>true).IsMarshall,"Shared GATT services do not establish brand");

JournalStoreChecks.Run();
LocalizationChecks.Run();
SoundStepperChecks.Run();
await UpgradeChecks.Run();
await FirmwareChecks.Run();
await ThirdPartyFirmwareChecks.Run();
await OfficialUpgradeChecks.Run();
await UpgradeRecoveryChecks.Run();
if(!TestFirmware.SkipMaterialChecks)await ResumableUpgradeChecks.CheckReplacementWithOldCompletion();
await ResumableUpgradeChecks.Run();
await SoundQueueChecks.Run();
var transports = new List<FakeTransport>();
await using var connection = new DeviceConnection(() => { var t = new FakeTransport(); transports.Add(t); return t; });
IReadOnlyList<DeviceCandidate>? earlyCandidates=null;
var candidates = await connection.ScanAsync(CancellationToken.None,items=>earlyCandidates=items);
Check(earlyCandidates is not null && earlyCandidates.SequenceEqual(candidates),"Progressive scan forwards candidates before completion");
Check(candidates.Count == 1, "Scan candidates");
var first = await connection.ConnectAsync(candidates[0], CancellationToken.None);
Check(first.Connected && first.Volume == 0 && first.Model == "MIDDLETON", "Confirmed snapshot");
var writtenVolume=await connection.SetVolumeAsync(17,CancellationToken.None);
Check(writtenVolume.Volume==17 && transports[^1].Writes.Any(w=>w.id==DeviceIds.Volume && w.value.SequenceEqual(new byte[]{17})),"Volume write must confirm and update snapshot");
var writtenEq=await connection.SetEqAsync(5,null,CancellationToken.None);
Check(writtenEq.Bass==5 && writtenEq.Treble==10 && transports[^1].Writes.Any(w=>w.id==DeviceIds.Eq && w.value.SequenceEqual(new byte[]{5,255,255,255,10,0,0,0})),"EQ write must preserve treble and confirm");
transports[^1].RejectWrite=true;
try{await connection.SetVolumeAsync(18,default);throw new Exception("Rejected write accepted");}catch(DeviceConnectionException){}
Check(connection.Current?.Volume==17,"Rejected write must retain actual readback");
transports[^1].RejectWrite=false;
Check((await connection.SetVolumeAsync(0,default)).Volume==0,"Volume zero must be writable");
await connection.SetVolumeAsync(17,default);
try{await connection.SetEqAsync(0,null,default);throw new Exception("EQ accepted zero target");}catch(ArgumentOutOfRangeException){}
transports[^1].MismatchWrite=true;
try{await connection.SetVolumeAsync(18,default);throw new Exception("Mismatched readback accepted");}catch(DeviceConnectionException){}
Check(connection.Current?.Volume==17,"Mismatch must show actual device value");
transports[^1].MismatchWrite=false;
transports[^1].UnknownEq=true;var countBefore=transports[^1].Writes.Count;
try{await connection.SetEqAsync(4,null,default);throw new Exception("Unknown EQ allowed write");}catch(DeviceConnectionException){}
Check(transports[^1].Writes.Count==countBefore,"Unknown other band must block EQ write");transports[^1].UnknownEq=false;
transports[^1].DelayedWrite=true;countBefore=transports[^1].Writes.Count;
var delayed=await connection.SetVolumeAsync(19,default);
Check(delayed.Volume==19 && transports[^1].Writes.Count==countBefore+1 && transports[^1].DelayedReads==2,"Delayed confirmation must reread without resending write");
transports[^1].DelayedWrite=false;
var old = transports[^1]; var oldCallback = old.Changed;
var second = await connection.ConnectAsync(candidates[0], CancellationToken.None);
oldCallback?.Invoke(DeviceIds.Volume, new byte[]{31});
Check(connection.Current?.SessionId == second.SessionId && connection.Current?.Volume == 0, "Late notification must not replace current session");
transports[^1].Changed?.Invoke(DeviceIds.Volume, new byte[]{12});
Check(connection.Current?.Volume == 12, "Current notification applied");
transports[^1].Disconnected?.Invoke();
Check(connection.Current?.Connected == false, "Disconnect clears connected state");
countBefore=transports[^1].Writes.Count;try{await connection.SetVolumeAsync(12,default);throw new Exception("Disconnected write accepted");}catch(DeviceConnectionException){}
Check(transports[^1].Writes.Count==countBefore,"Disconnected session must not write");
Check(old.Disposed, "Previous connection released");

await using var wrong = new DeviceConnection(() => new FakeTransport { Model = "OTHER" });
try { await wrong.ConnectAsync(candidates[0], CancellationToken.None); throw new Exception("Accepted wrong model"); }
catch (DeviceConnectionException) { }
await using var noNotify = new DeviceConnection(() => new FakeTransport { RejectResponse = true });
try { await noNotify.ConnectAsync(candidates[0], CancellationToken.None); throw new Exception("Accepted failed required subscription"); }
catch (DeviceConnectionException) { }
using var cancel = new CancellationTokenSource(); cancel.Cancel();
try { await connection.ConnectAsync(candidates[0], cancel.Token); throw new Exception("Ignored cancellation"); }
catch (OperationCanceledException) { }
var interruptedTransport=new FakeTransport {WaitOnOpen=true};
await using var interruptedConnection=new DeviceConnection(()=>interruptedTransport);
using var midCancel=new CancellationTokenSource();
var pending=interruptedConnection.ConnectAsync(candidates[0],midCancel.Token);
await interruptedTransport.OpenStarted.Task;midCancel.Cancel();
try {await pending;throw new Exception("In-flight cancellation ignored");}catch(OperationCanceledException){}
Check(interruptedTransport.Disposed && interruptedConnection.Current is null,"Cancelled connection must release GATT and expose no success");
await using var optional=new DeviceConnection(()=>new FakeTransport {MissingOptional=true});
var limited=await optional.ConnectAsync(candidates[0],CancellationToken.None);
Check(limited.Connected && limited.Volume is null && limited.Battery is null && !limited.Capabilities.Eq,"Optional data must degrade to unknown");
var failedTransport=new FakeTransport {RejectResponse=true};
await using var failed=new DeviceConnection(()=>failedTransport);
try {await failed.ConnectAsync(candidates[0],CancellationToken.None);}catch(DeviceConnectionException){}
Check(failedTransport.Disposed && failed.Current is null,"Failed required subscription must release session");
await using var duringRead=new DeviceConnection(()=>new FakeTransport{NotifyDuringRead=true});
var updatedDuringRead=await duringRead.ConnectAsync(candidates[0],CancellationToken.None);
Check(updatedDuringRead.Volume==7,"Notification arriving during initial reads must not be lost");
Console.WriteLine("PASS: decoding, zero values, identity, required notifications, session isolation, disposal, disconnect, cancellation, installation evidence");

// Official-style trial: local target immediately, no write-confirmation read loop.
var trialTransport=new FakeTransport{MismatchWrite=true};
await using var trial=new DeviceConnection(()=>trialTransport,officialSoundTrial:true);
await trial.ConnectAsync(candidates[0],default);
trialTransport.ReadCount=0;
var trialWrite=trial.SetVolumeAsync(5,default);
Check(trial.Current?.Volume==5,"Trial target must be visible immediately");
await trialWrite;
Check(trialTransport.ReadCount==0 && trial.Current?.Volume==5,"Trial must not replace target with immediate stale readback");
trialTransport.Changed?.Invoke(DeviceIds.Volume,new byte[]{0});
Check(trial.Current?.Volume==0,"Later device zero must still override trial target");
await trial.SetEqAsync(3,null,default);
Check(trialTransport.Writes[^1].value.SequenceEqual(SoundCommands.Eq(3,10)),"Trial EQ preserves local other band");
await trial.SetEqAsync(null,8,default);
Check(trialTransport.Writes[^1].value.SequenceEqual(SoundCommands.Eq(3,8)),"Trial EQ retains previous optimistic band");
Check(trialTransport.ReadCount==0,"Trial EQ must use local state rather than pre-read");
trialTransport.RejectWrite=true;
try{await trial.SetVolumeAsync(6,default);throw new Exception("Trial rejection ignored");}catch(DeviceConnectionException){}
Check(trial.Current?.Volume is null,"Failed trial target must not remain as successful state");
Console.WriteLine("PASS: official trial optimistic values, notification override, EQ preservation and write rejection");

// Authentication denial is fatal, not an optional empty sound field.
int authAttempts=0,protectedOperations=0;
try{
 await ProtectedGatt.RunAsync(()=>{protectedOperations++;return Task.FromResult(("ProtocolError",(byte?)15));},()=>{authAttempts++;return Task.FromException<bool>(new DeviceConnectionException("pair denied"));},r=>r.Item1,r=>r.Item2);
 throw new Exception("Authentication failure was swallowed");
}catch(DeviceConnectionException){}
Check(authAttempts==1 && protectedOperations==1,"Pair rejection must stop protected operation");
protectedOperations=0;
var protectedResult=await ProtectedGatt.RunAsync(()=>Task.FromResult(++protectedOperations==1?("ProtocolError",(byte?)15):("Success",(byte?)null)),()=>Task.FromResult(true),r=>r.Item1,r=>r.Item2);
Check(protectedResult.Item1=="Success" && protectedOperations==2,"Successful pairing must retry protected operation once");
try{await ProtectedGatt.RunAsync(()=>Task.FromResult(("ProtocolError",(byte?)15)),()=>Task.FromResult(false),r=>r.Item1,r=>r.Item2);throw new Exception("Unresolved encryption accepted");}catch(DeviceConnectionException){}
var unboundTransport=new FakeTransport{DenyAuthentication=true};
await using var unbound=new DeviceConnection(()=>unboundTransport,officialSoundTrial:true);
try{await unbound.ConnectAsync(candidates[0],default);throw new Exception("Unbound connection entered details");}catch(DeviceConnectionException){}
Check(unbound.Current is null && unboundTransport.Disposed,"Authentication failure must release the session without a connected snapshot");
Console.WriteLine("PASS: protected read/write/subscribe authentication gate");

sealed class FakeTransport : ISoundControlTransport,IProgressiveDiscovery
{
 public bool DenyAuthentication;
 public int ReadCount;
 public string Model = "MIDDLETON";
 public bool RejectResponse, RejectWrite, MismatchWrite, UnknownEq, Disposed, WaitOnOpen, MissingOptional, NotifyDuringRead;
 public Dictionary<Guid,byte[]> Values=new(){{DeviceIds.Volume,new byte[]{0}},{DeviceIds.Eq,new byte[]{0,255,255,255,10,0,0,0}}};
 public List<(Guid id,byte[] value)> Writes=new();
 public bool DelayedWrite;public int DelayedReads;Guid delayedId;byte[]? delayedValue;
 public TaskCompletionSource OpenStarted=new(TaskCreationOptions.RunContinuationsAsynchronously);
 public Action<Guid,byte[]>? Changed {get;set;}
 public Action? Disconnected {get;set;}
 public Action<IReadOnlyList<DeviceCandidate>>? CandidatesChanged {get;set;}
 public Task<IReadOnlyList<DeviceCandidate>> ScanAsync(CancellationToken ct)
 {
  IReadOnlyList<DeviceCandidate> devices=new[]{new DeviceCandidate("test", "MIDDLETON", "候选 1")};
  CandidatesChanged?.Invoke(devices);return Task.FromResult(devices);
 }
 public async Task OpenAsync(DeviceCandidate candidate, CancellationToken ct) { ct.ThrowIfCancellationRequested();OpenStarted.TrySetResult();if(WaitOnOpen)await Task.Delay(Timeout.Infinite,ct); }
 public string DeviceName => "MIDDLETON";
 public bool Has(Guid characteristic) => !MissingOptional || characteristic==DeviceIds.GaiaWrite || characteristic==DeviceIds.GaiaResponse || characteristic==DeviceIds.Model;
 public Task<byte[]?> ReadAsync(Guid id, CancellationToken ct) {
  ReadCount++;
  if(DenyAuthentication && id==DeviceIds.Volume)throw new DeviceConnectionException("LE encryption required");
  if(delayedValue is not null && id==delayedId && ++DelayedReads>=2){Values[id]=delayedValue;delayedValue=null;}
  if(UnknownEq && id==DeviceIds.Eq)return Task.FromResult<byte[]?>(null);
  if(NotifyDuringRead && id==DeviceIds.Eq)Changed?.Invoke(DeviceIds.Volume,new byte[]{7});
  return Task.FromResult<byte[]?>(!Has(id)?null:id == DeviceIds.Model ? System.Text.Encoding.UTF8.GetBytes(Model) : Values.TryGetValue(id,out var value) ? value.ToArray() : System.Text.Encoding.UTF8.GetBytes("test"));
 }
 public Task<bool> SubscribeAsync(Guid id,CancellationToken ct) => Task.FromResult(!(RejectResponse && id == DeviceIds.GaiaResponse));
 public Task<bool> WriteAsync(Guid id,byte[] value,CancellationToken ct) { if(RejectWrite)return Task.FromResult(false);Writes.Add((id,value.ToArray()));if(DelayedWrite){delayedId=id;delayedValue=value.ToArray();DelayedReads=0;}else if(!MismatchWrite)Values[id]=value.ToArray();return Task.FromResult(true); }
 public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
}




