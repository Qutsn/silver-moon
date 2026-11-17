using Middleton.Desktop;
using System.Buffers.Binary;

static class ResumableUpgradeChecks
{
 public static async Task CheckReplacementWithOldCompletion()
 {
  var bytes=File.ReadAllBytes(TestFirmware.R2);var descriptor=FirmwareCatalog.Analyze(bytes).Installable!;
  foreach(int mode in new[]{0,1,2,3})
  {
   var link=new FakeLink(descriptor,false){AbortMode=mode};link.Orphan((byte)(mode==3?2:0));
   var journal=new UpgradeJournal{Serial="test",Sha256=descriptor.Sha256,Started=true,CompleteInd=true,FinalRestartObserved=true,Validated=true,PartitionBoundary=true};
   var oldId=journal.OperationId;
   try{await new ResumableUpgrade(bytes,link,journal,_=>{},_=>{},_=>Task.CompletedTask).RunAsync(default);if(mode!=0)throw new Exception("Unsafe replacement accepted");}
   catch(UpgradeProtocolException){if(mode==0)throw;}
   if(mode==0 && (link.Aborts!=1 || !journal.Finished || journal.OperationId==oldId))throw new Exception("Old completion prevented replacement");
   if(mode!=0 && (link.DataWrites!=0 || journal.OperationId!=oldId || (mode==3 && link.Aborts!=0)))throw new Exception("Failed abort lost evidence or sent data");
  }
  Console.WriteLine("PASS: old completion cannot bypass replacement; abort failure and late stages stay protected");
 }
 public static async Task CheckStaleTransfer()
 {
  var r2bytes=File.ReadAllBytes(TestFirmware.R2);var r2descriptor=FirmwareCatalog.Analyze(r2bytes).Installable!;
  foreach(bool proof in new[]{false,true})
  {
   var r2link=new FakeLink(r2descriptor,false,"17.0.37");
   var old=new UpgradeJournal{Serial="test",Sha256=r2descriptor.Sha256,CompleteInd=true,FinalRestartObserved=true,Validated=true,Started=true};
   string operation=old.OperationId;int archives=0;
   try{await new ResumableUpgrade(r2bytes,r2link,old,_=>{},_=>{},_=>Task.CompletedTask,(_,_)=>{archives++;return proof;}).RunAsync(default);if(!proof)throw new Exception("Unproven journal supersession accepted");}
   catch(UpgradeProtocolException){if(proof)throw;}
   if(archives!=1 || (proof && (!old.Finished || old.OperationId==operation)) || (!proof && (r2link.DataWrites!=0 || old.OperationId!=operation)))throw new Exception("Supersession policy failed");
  }
  var bytes=File.ReadAllBytes(TestFirmware.Official);var descriptor=FirmwareCatalog.Analyze(bytes).Installable!;
  var link=new FakeLink(descriptor,false,"25.2.52");
  var journal=new UpgradeJournal{Serial="test",Sha256=descriptor.Sha256,Started=true,ConfirmedFrontier=23216,AcknowledgedBytes=23216};
  var previous=journal.OperationId;
  await new ResumableUpgrade(bytes,link,journal,_=>{},_=>{},_=>Task.CompletedTask).RunAsync(default);
  if(!journal.Finished || journal.OperationId==previous || journal.AcknowledgedBytes!=bytes.Length || link.Aborts!=0 || link.ResetCommands!=0)
   throw new Exception("Stale transfer journal did not start a new full installation without abort/reset");
  foreach(bool validated in new[]{false,true})
  {
   var guarded=new FakeLink(descriptor,false);
   var late=new UpgradeJournal{Serial="test",Sha256=descriptor.Sha256,Started=true,ConfirmedFrontier=23216,PartitionBoundary=!validated,Validated=validated};
   try{await new ResumableUpgrade(bytes,guarded,late,_=>{},_=>{}).RunAsync(default);throw new Exception("Late-stage journal discarded");}catch(UpgradeProtocolException){}
   if(guarded.DataWrites!=0 || guarded.Aborts!=0 || guarded.ResetCommands!=0)throw new Exception("Late-stage mismatch touched firmware");
  }
  Console.WriteLine("PASS: stale transfer on initial idle starts anew; late-stage journal stays protected");
 }
 public static async Task CheckFinalIdentity()
 {
  var bytes=File.ReadAllBytes(TestFirmware.R2);var descriptor=FirmwareCatalog.Analyze(bytes).Installable!;
  foreach(int badReads in new[]{1,6,999})
  {
   var link=new FakeLink(descriptor,false,descriptor.DisplayVersion){BadHardwareReads=badReads};
   var journal=new UpgradeJournal{Serial="test",Sha256=descriptor.Sha256,CompleteInd=true,FinalRestartObserved=true};
   try{await new ResumableUpgrade(bytes,link,journal,_=>{},_=>{},_=>Task.CompletedTask,finalReadbackBudget:TimeSpan.FromMilliseconds(500),finalReadbackDelay:ct=>Task.Delay(10,ct)).RunAsync(default);if(badReads==999)throw new Exception("Persistent hardware mismatch accepted");}
   catch(UpgradeProtocolException){if(badReads!=999)throw;}
   if(journal.Finished!=(badReads!=999) || link.Connections>60 || link.ResetCommands!=0 || link.Aborts!=0 || link.DataWrites!=0 || link.PsWrites!=0)
    throw new Exception("Final identity recovery mutated device or exceeded retry limit");
  }
  var initial=new FakeLink(descriptor,false){BadHardwareReads=1};
  try{await new ResumableUpgrade(bytes,initial,new(){Serial="test",Sha256=descriptor.Sha256},_=>{},_=>{}).RunAsync(default);throw new Exception("Initial unsupported hardware admitted");}catch(UpgradeProtocolException){}
  if(initial.Connections!=1 || initial.DataWrites!=0)throw new Exception("Initial compatibility gate changed");
  Console.WriteLine("PASS: final identity transient recovery, persistent rejection, initial gate and no device writes");
 }
 public static async Task CheckResetReconnect()
 {
  var bytes=File.ReadAllBytes(TestFirmware.R2);
  var descriptor=FirmwareCatalog.Analyze(bytes).Installable!;
  var link=new FakeLink(descriptor,false){FailAfterReset=true};
  var journal=new UpgradeJournal{Serial="test",Sha256=descriptor.Sha256};
  await new ResumableUpgrade(bytes,link,journal,_=>{},_=>{},_=>Task.CompletedTask).RunAsync(default);
  if(!journal.Finished || link.ResetCommands!=1 || !link.ResetReconnectFailed)
   throw new Exception($"Reset recovery must reconnect without resetting again: resets={link.ResetCommands}");
  Console.WriteLine("PASS: acknowledged reset survives reconnect failure without a second reset");
 }
 public static Task CheckStalledCommit()=>Task.WhenAll(CheckStalledCommit(false),CheckStalledCommit(true));
 static async Task CheckStalledCommit(bool stalledNotification)
 {
  var bytes=File.ReadAllBytes(TestFirmware.R2);
  var descriptor=FirmwareCatalog.Analyze(bytes).Installable!;
  var journal=new UpgradeJournal{Serial="test",Sha256=descriptor.Sha256,Started=true,Validated=true,PartitionBoundary=true,ResetComplete=true};
  var link=new FakeLink(descriptor,false,descriptor.DisplayVersion){StallCommit=true,StallCommitNotification=stalledNotification};link.TrialBoot();
  using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(40));
  try
  {
   await new ResumableUpgrade(bytes,link,journal,_=>{},_=>{},_=>Task.CompletedTask).RunAsync(deadline.Token);
   throw new Exception("Stalled commit reported success");
  }
  catch(UpgradeLinkLostException){}
  catch(OperationCanceledException){throw new Exception("Repeated readable context reset recovery deadline; stalled commit never stopped");}
  if(link.Connections<2 || journal.Finished || link.Aborts!=0 || link.DataWrites!=0)
   throw new Exception("Stalled commit recovery changed firmware or lost retry behavior");
  Console.WriteLine($"PASS: stalled commit {(stalledNotification?"notification":"ACK")} obeys recovery deadline; no abort or retransmission");
 }
 public static async Task Run()
 {
  if(TestFirmware.SkipMaterialChecks){Console.WriteLine("SKIP: real package resume checks");return;}
  await CheckStaleTransfer();
  await CheckFinalIdentity();
  await CheckResetReconnect();
  var files=new[]{TestFirmware.Official,TestFirmware.R2};
  var images=files.Select(File.ReadAllBytes).ToList();
  // A package with its own image ID must execute the complete staged path and
  // resume after lost ACKs, rather than silently borrowing the built-in r2 ID.
  var thirdParty=(byte[])images[1].Clone();thirdParty[ThirdPartyFirmware.PayloadStart+256]^=1;images.Add(thirdParty);
  foreach(var bytes in images)
  {
   var descriptor=FirmwareCatalog.Analyze(bytes).Installable!;
   foreach(var invalid in new[]{new UpgradeJournal{ConfirmedFrontier=-1},new UpgradeJournal{ConfirmedFrontier=bytes.Length+1},new UpgradeJournal{AcknowledgedBytes=-1}})
   {
    invalid.Serial="test";invalid.Sha256=descriptor.Sha256;
    var untouched=new FakeLink(descriptor,false);
    try{_ =new ResumableUpgrade(bytes,untouched,invalid,_=>{},_=>{});throw new Exception("Invalid journal accepted");}
    catch(UpgradeProtocolException){}
    if(untouched.Connections!=0 || untouched.PsWrites!=0)throw new Exception("Invalid journal touched device");
   }
   var link=new FakeLink(descriptor,true);
   var orphan=new FakeLink(descriptor,false);orphan.Orphan(0);
   var replacement=new UpgradeJournal{Serial="test",Sha256=descriptor.Sha256};
   await new ResumableUpgrade(bytes,orphan,replacement,_=>{},_=>{},_=>Task.CompletedTask).RunAsync(default);
   if(orphan.Aborts!=1 || !replacement.Finished || replacement.ReplacementAbortPending)throw new Exception("Orphan transfer not replaced");
   var idleSession=new FakeLink(descriptor,false);idleSession.LeaveIdleSessionOpen();
   var idleJournal=new UpgradeJournal{Serial="test",Sha256=descriptor.Sha256};
   await new ResumableUpgrade(bytes,idleSession,idleJournal,_=>{},_=>{},_=>Task.CompletedTask).RunAsync(default);
   if(!idleJournal.Finished || idleSession.Aborts!=0)throw new Exception("Idle stale session recovery failed");
   foreach(int mode in new[]{1,2,3})
   {
    var blocked=new FakeLink(descriptor,false){AbortMode=mode};blocked.Orphan((byte)(mode==3?2:0));
    try{await new ResumableUpgrade(bytes,blocked,new(){Serial="test",Sha256=descriptor.Sha256},_=>{},_=>{},_=>Task.CompletedTask).RunAsync(default);throw new Exception("Unsafe replacement accepted");}
    catch(UpgradeProtocolException){}
    if(blocked.DataWrites!=0 || (mode==3 && blocked.Aborts!=0))throw new Exception("Failed cleanup reached transfer");
   }
   var journal=new UpgradeJournal{Serial="test",Sha256=descriptor.Sha256};int saves=0;
   await new ResumableUpgrade(bytes,link,journal,_=>saves++,_=>{},_=>Task.CompletedTask).RunAsync(default);
   if(!journal.Finished || !journal.CompleteInd || !journal.FinalRestartObserved || !link.WaitedForRestart || link.Connections<3 || saves==0 || !link.FaultInjected)throw new Exception("Resume did not complete independently after final restart");
   if(link.FirstResumedByte!=453)throw new Exception("Resume ignored device checkpoint beyond last host ACK");
   if(descriptor.Strategy==FirmwareStrategy.MiddletonR2Staged && (!journal.PartitionBoundary || !journal.ResetComplete || link.PsWrites!=1 || link.DataLastFlag))throw new Exception("r2 boundary/reset protocol violated");
   if(descriptor.Strategy==FirmwareStrategy.OfficialFull && (link.PsWrites!=0 || !link.DataLastFlag))throw new Exception("Official strategy used r2 operations");
   var unobserved=new UpgradeJournal{Serial="test",Sha256=descriptor.Sha256,CompleteInd=true};
   var noRestart=new FakeLink(descriptor,false){RejectRestartWait=true};
   try{await new ResumableUpgrade(bytes,noRestart,unobserved,_=>{},_=>{},_=>Task.CompletedTask).RunAsync(default);throw new Exception("Completion accepted without observed restart");}
   catch(UpgradeProtocolException){}
   if(unobserved.Finished || unobserved.FinalRestartObserved)throw new Exception("Missing restart created completion evidence");
   // App may have missed the automatic restart while closed. Preserve the journal,
   // then permit a user power cycle observed during Retry to finish confirmation.
   var manualRestart=new FakeLink(descriptor,false,descriptor.DisplayVersion);
   await new ResumableUpgrade(bytes,manualRestart,unobserved,_=>{},_=>{},_=>Task.CompletedTask).RunAsync(default);
   if(!unobserved.Finished || !manualRestart.WaitedForRestart || manualRestart.PsWrites!=0)
    throw new Exception("Committed journal could not recover through an observed manual restart");
  }
  Console.WriteLine("PASS: full official/r2 fake transfers, injected disconnect, device-selected resume offset, final readback");
  await CheckStalledCommit();
 }
 sealed class FakeLink : IUpgradeLink
 {
  readonly FirmwareDescriptor firmware;
  readonly Queue<UpgradeMessage> messages=new();
  byte[] ps=new byte[38];long received;readonly int limit;readonly bool inject;
  string version="17.0.37";
  public bool FaultInjected,DataLastFlag;
  public bool WaitedForRestart;
  public bool RejectRestartWait;
  public bool StallCommit;
  public bool StallCommitNotification;
  public bool FailAfterReset,ResetReconnectFailed;
  public int ResetCommands, BadHardwareReads;
  int commitAttempts;
  public void TrialBoot(){SetIdentity();ps[27]=3;ps[29]=10;}
  public async Task WaitForDeviceDisconnectAsync(CancellationToken ct){if(RejectRestartWait)throw new TimeoutException("No device reboot");await Task.Delay(20,ct);WaitedForRestart=true;}
  public int Connections,PsWrites;
  public int Aborts,AbortMode,DataWrites;
  bool sessionOpen;
  public void LeaveIdleSessionOpen()=>sessionOpen=true;
  public void Orphan(byte resume){SetIdentity();ps[24]^=1;ps[27]=resume;received=120;}
  public long FirstResumedByte=-1;
  public FakeLink(FirmwareDescriptor firmware,bool inject,string initialVersion="17.0.37")
  {
   version=initialVersion;this.firmware=firmware;this.inject=inject;limit=firmware.Strategy==FirmwareStrategy.MiddletonR2Staged?3266040:firmware.Length;
   ps[0]=0x80;ps[1]=3;ps[3]=5;ps[5]=2;ps[7]=1;
  }
  void SetIdentity(){var id=Convert.FromHexString(firmware.UpgradeId);ps[22]=id[2];ps[23]=id[3];ps[24]=id[0];ps[25]=id[1];ps[15]=5;ps[17]=2;}
  void Message(byte code,byte[]? data=null)=>messages.Enqueue(new(code,data??Array.Empty<byte>()));
  public Task<UpgradeIdentity> ReconnectAsync(CancellationToken ct){Connections++;messages.Clear();if(FailAfterReset && ResetCommands>0 && !ResetReconnectFailed){ResetReconnectFailed=true;throw new UpgradeLinkLostException("Injected GAIA discovery AccessDenied after reset ACK");}return Task.FromResult(new UpgradeIdentity("MIDDLETON","test",version,Connections<=BadHardwareReads?"2.0.0":"3.0.2"));}
  public Task<GaiaAcknowledgement> CommandAsync(ushort command,ReadOnlyMemory<byte> payload,CancellationToken ct)
  {
   ct.ThrowIfCancellationRequested();byte[] response=Array.Empty<byte>();
   if(command==0x0640){byte status=sessionOpen?(byte)6:(byte)0;sessionOpen=true;return Task.FromResult(new GaiaAcknowledgement(command,status,response));}
   if(command==0x0641){sessionOpen=false;return Task.FromResult(new GaiaAcknowledgement(command,0,response));}
   if(command==0x0710){response=new byte[40];response[1]=4;ps.CopyTo(response,2);}
   else if(command==0x0202){ResetCommands++;}
   else if(command==0x0712){PsWrites++;if(payload.Length!=78)throw new Exception("Unpadded PS4 write");ps=payload.Slice(2,38).ToArray();}
   else if(command==0x0642)
   {
    byte code=payload.Span[0];var body=payload.Slice(3);
    switch(code)
    {
     case 7:Aborts++;if(AbortMode!=2){Array.Clear(ps,14,18);received=0;}Message(AbortMode==1?(byte)11:(byte)8);break;
     case 19:SetIdentity();Message(20,new[]{ps[27]}.Concat(Convert.FromHexString(firmware.UpgradeId)).Concat(new byte[]{3}).ToArray());break;
     case 1:Message(2,new byte[]{0,6,102});break;
     case 21:
      byte[] request=new byte[8];BinaryPrimitives.WriteUInt32BigEndian(request,(uint)(limit-received));BinaryPrimitives.WriteUInt32BigEndian(request.AsSpan(4),(uint)received);
      if(FaultInjected)FirstResumedByte=received;Message(3,request);break;
     case 4:
      DataWrites++;
      if(inject && !FaultInjected && received==450){FaultInjected=true;received+=3;throw new UpgradeLinkLostException("Device retained bytes but ACK was lost");}
      received+=body.Length-1;DataLastFlag|=body.Span[0]==1;
      if(received==limit){ps[29]=10;if(limit<firmware.Length){byte[] next=new byte[8];next[3]=12;Message(3,next);}}
      break;
     case 22:ps[27]=2;Message(11);break;
     case 12:ps[27]=3;version=firmware.DisplayVersion;break;
     case 14:
      commitAttempts++;
      if(StallCommit && (!StallCommitNotification || commitAttempts==1))throw new UpgradeLinkLostException("Injected commit ACK timeout after successful context read");
      Message(15);break;
     case 16:Message(18);Array.Clear(ps,14,18);break;
     default:throw new Exception($"Unexpected upgrade code {code}");
    }
   }
   else if(command!=0x4001 && command!=0x0640 && command!=0x0202)throw new Exception($"Unexpected command {command}");
   return Task.FromResult(new GaiaAcknowledgement(command,(byte)(command==0x0640 && FaultInjected?6:0),response));
  }
  public async Task<UpgradeMessage> ReceiveAsync(TimeSpan timeout,CancellationToken ct)
  {
   if(StallCommitNotification && commitAttempts>1)await Task.Delay(Timeout.InfiniteTimeSpan,ct);
   return messages.Dequeue();
  }
  public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
 }
}

