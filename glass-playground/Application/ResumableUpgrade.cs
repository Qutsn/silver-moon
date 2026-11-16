using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Middleton.Desktop;

// Persistent evidence, not a trusted device checkpoint. Every reconnect rechecks
// serial/model, image ID and PS4 before using these fields (see docs/17).
// ConfirmedFrontier is a high-water mark; AcknowledgedBytes includes retransmits.
public sealed class UpgradeJournal
{
 public string OperationId {get;set;}=Guid.NewGuid().ToString("N");
 public string Serial {get;set;}="";
 public string Sha256 {get;set;}="";
 public long ConfirmedFrontier {get;set;}
 public long AcknowledgedBytes {get;set;}
 public bool Started {get;set;}
 public bool PartitionBoundary {get;set;}
 public string? OriginalPs4 {get;set;}
 public bool ResetComplete {get;set;}
 public bool Validated {get;set;}
 public bool CompleteInd {get;set;}
 public bool FinalRestartObserved {get;set;}
 public bool Finished {get;set;}
 public bool ReplacementAbortPending {get;set;}
}
public sealed record UpgradeProgress(InstallationState State,string Message,long Cursor,long Total,long AcknowledgedBytes,long SkippedBytes);

/// <summary>
/// Serial upgrade coordinator for the hash-pinned images and admitted fixed-layout MCU packages.
/// Transport loss permits reconnect; protocol/identity failures stop without guessing.
/// Completion requires COMPLETE_IND, observed disconnect and fresh version/idle reads.
/// This does not prove that the startup sound has finished (docs/17).
/// </summary>
public sealed class ResumableUpgrade
{
 readonly IUpgradeLink link;
 readonly UpgradeJournal journal;
 readonly Action<UpgradeJournal> save;
 readonly Action<UpgradeProgress> update;
 readonly Func<CancellationToken,Task> pace;
 readonly byte[] bytes;
 readonly FirmwareDescriptor firmware;
 readonly bool r2;
 readonly int limit;
 long cursor,skipped;
 readonly Stopwatch recoveryOutage=new();
 CancellationTokenSource? recoveryAttempt;
 string? recoveryCheckpoint;
 string recoveryError="";
 void RecoveryProgress()
 {
  recoveryOutage.Reset();
  recoveryAttempt?.CancelAfter(Timeout.InfiniteTimeSpan);
 }
 public ResumableUpgrade(byte[] image,IUpgradeLink link,UpgradeJournal journal,Action<UpgradeJournal> save,
  Action<UpgradeProgress> update,Func<CancellationToken,Task>? pace=null)
 {
  bytes=(byte[])image.Clone();firmware=FirmwareCatalog.Analyze(bytes).Installable??throw new UpgradeProtocolException(AppText.Get("Upgrade.Message.1"));
  this.link=link;this.journal=journal;this.save=save;this.update=update;this.pace=pace??(ct=>Task.Delay(2,ct));
  if(journal.Sha256!=firmware.Sha256 || string.IsNullOrWhiteSpace(journal.Serial))throw new UpgradeProtocolException(AppText.Get("Upgrade.Message.2"));
  r2=firmware.Strategy==FirmwareStrategy.MiddletonR2Staged;limit=r2?ThirdPartyFirmware.TransferEnd:bytes.Length;
  if(journal.ConfirmedFrontier<0 || journal.ConfirmedFrontier>limit || journal.AcknowledgedBytes<0)
   throw new UpgradeProtocolException(AppText.Get("Upgrade.Message.3"));
 }
 void Report(InstallationState state,string message)=>update(new(state,message,cursor,limit,journal.AcknowledgedBytes,skipped));
 public async Task RunAsync(CancellationToken cancellationToken)
 {
  using var overall=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);overall.CancelAfter(TimeSpan.FromMinutes(75));
  recoveryOutage.Reset();recoveryCheckpoint=null;
  try
  {
   while(true)
   {
    overall.Token.ThrowIfCancellationRequested();
    using var attempt=CancellationTokenSource.CreateLinkedTokenSource(overall.Token);
    recoveryAttempt=attempt;
    if(recoveryOutage.IsRunning)
    {
     var remaining=TimeSpan.FromSeconds(30)-recoveryOutage.Elapsed;
     if(remaining<=TimeSpan.Zero)throw new UpgradeLinkLostException(AppText.Format("Upgrade.ReconnectPending",recoveryError));
     attempt.CancelAfter(remaining);
    }
    var ct=attempt.Token;
    try
    {
     var identity=await link.ReconnectAsync(ct);
     if(identity.Serial!=journal.Serial || identity.Model!="MIDDLETON")throw new UpgradeProtocolException(AppText.Get("Upgrade.Message.4"));
     if(r2 && (identity.Hardware!="3.0.2" || (identity.Firmware!="17.0.37" && identity.Firmware!="25.2.52")))
      throw new UpgradeProtocolException(AppText.Get("Upgrade.Message.5"));
     var state=await Context(ct);
     // Reading the same PS4 again proves reachability, not forward progress.
     // Keep one recovery deadline across repeated handshake/commit failures.
     string checkpoint=$"{state.Resume}:{journal.ConfirmedFrontier}:{journal.PartitionBoundary}:{journal.ResetComplete}:{journal.CompleteInd}:{journal.FinalRestartObserved}";
     if(recoveryCheckpoint!=checkpoint){recoveryCheckpoint=checkpoint;RecoveryProgress();}
     if(journal.CompleteInd)
     {
      if(!journal.FinalRestartObserved)
      {
       Report(InstallationState.Rebooting,AppText.Get("Upgrade.AwaitObservedRestart"));
       await WaitFinalRestart(ct);continue;
      }
      Report(InstallationState.ReadingBack,AppText.Get("Upgrade.Message.7"));
      if(!state.IsIdle || identity.Firmware!=firmware.DisplayVersion)throw new UpgradeProtocolException(AppText.Get("Upgrade.Message.8"));
      journal.Finished=true;save(journal);Report(InstallationState.Complete,AppText.Get("Upgrade.Message.9"));return;
     }
     if(state.IsIdle)
     {
      if(journal.ReplacementAbortPending)ResetAfterAbort();
      if(journal.ConfirmedFrontier>0 || journal.PartitionBoundary || journal.Validated)
       throw new UpgradeProtocolException(AppText.Get("Upgrade.Message.10"));
     }
     else if(journal.ReplacementAbortPending || !journal.Started || state.ImageId!=firmware.UpgradeId)
     {
      // A confirmed install authorizes replacing an orphaned DATA-stage upload.
      // Never abort validation/trial/commit: those can already modify active state.
      if(state.Resume!=0)throw new UpgradeProtocolException(AppText.Get("Upgrade.ReplaceUnsafe"));
      Report(InstallationState.ResultUnknown,AppText.Get("Upgrade.ReplaceOld"));
      journal.ReplacementAbortPending=true;save(journal);
      RequireAck(await link.CommandAsync(0x4001,new byte[]{18},ct));
      var attach=await link.CommandAsync(0x0640,Array.Empty<byte>(),ct);
      if(attach.Status!=0 && attach.Status!=6)RequireAck(attach);
      await Send(7,Array.Empty<byte>(),ct);
      UpgradeWire.Require(await link.ReceiveAsync(TimeSpan.FromSeconds(30),ct),8,0);
      if(!(await Context(ct)).IsIdle)throw new UpgradeProtocolException(AppText.Get("Upgrade.AbortNotIdle"));
      // ABORT clears the image, not the GAIA upgrade session. BLE disposal alone
      // does not replace UPGRADE_DISCONNECT (see tools/abort_active_upgrade.py).
      RequireAck(await link.CommandAsync(0x0641,Array.Empty<byte>(),ct));
      ResetAfterAbort();
      // Reopen an isolated transport so ABORT-era events cannot satisfy new commands.
      continue;
     }
     if(state.Resume>4)throw new UpgradeProtocolException(AppText.Format("Upgrade.ResumeUnsupported",state.Resume));
     if(r2 && state.Resume>0 && (!journal.PartitionBoundary || !state.StagedPartitionsClosed(firmware.UpgradeId)))
      throw new UpgradeProtocolException(AppText.Get("Upgrade.Message.12"));
     if(r2 && state.Resume==1 && !journal.ResetComplete)
     {
      CheckPrepared(state);Report(InstallationState.Rebooting,AppText.Get("Upgrade.Message.13"));
      RequireAck(await link.CommandAsync(0x0202,Array.Empty<byte>(),ct));await Task.Delay(5000,ct);
      var resetIdentity=await link.ReconnectAsync(ct);
      if(resetIdentity.Serial!=journal.Serial)throw new UpgradeProtocolException(AppText.Get("Upgrade.Message.14"));
      CheckPrepared(await Context(ct));journal.ResetComplete=true;save(journal);continue;
     }
     RequireAck(await link.CommandAsync(0x4001,new byte[]{18},ct));
     var open=await link.CommandAsync(0x0640,Array.Empty<byte>(),ct);
     if(open.Status==6 && state.IsIdle)
     {
      // Recover a session left open by an earlier client after successful ABORT.
      // Re-read idle before closing it; never apply this to an active image.
      if(!(await Context(ct)).IsIdle)throw new UpgradeProtocolException(AppText.Get("Upgrade.AbortNotIdle"));
      RequireAck(await link.CommandAsync(0x0641,Array.Empty<byte>(),ct));
      open=await link.CommandAsync(0x0640,Array.Empty<byte>(),ct);
      RequireAck(open); // One retry only; do not mask a continuing status 06.
     }
     if(open.Status!=0 && !(open.Status==6 && journal.Started && state.ImageId==firmware.UpgradeId))RequireAck(open);
     journal.Started=true;save(journal);
     byte resume=await SyncStart(state.IsIdle?(byte)0:(byte)state.Resume,ct);
     if(resume==0)
     {
      await Transfer(ct);
      if(r2)
      {
       state=await Context(ct);
       var payload=state.PrepareR2Validation(firmware.UpgradeId);
       journal.PartitionBoundary=true;journal.OriginalPs4=Convert.ToHexString(state.Value);save(journal);
       Report(InstallationState.Validating,AppText.Get("Upgrade.Message.15"));
       RequireAck(await link.CommandAsync(0x0712,payload,ct));
       CheckPrepared(await Context(ct));continue;
      }
      await Validate(ct);resume=2;
     }
     else if(resume==1){await Validate(ct);resume=2;}
     if(resume==2)
     {
      state=await Context(ct);
      if(state.ImageId!=firmware.UpgradeId || state.Resume!=2)throw new UpgradeProtocolException(AppText.Get("Upgrade.Message.16"));
      journal.Validated=true;save(journal);Report(InstallationState.Rebooting,AppText.Get("Upgrade.Message.17"));
      await Send(12,new byte[]{0},ct);await Task.Delay(5000,ct);continue;
     }
     if(resume is 3 or 4)
     {
      if(identity.Firmware!=firmware.DisplayVersion)throw new UpgradeProtocolException(AppText.Get("Upgrade.Message.18"));
      Report(InstallationState.Committing,AppText.Get("Upgrade.Message.19"));
      if(resume==3)
      {
       await Send(14,new byte[]{0},ct);
       UpgradeWire.Require(await link.ReceiveAsync(TimeSpan.FromSeconds(60),ct),15,0);
      }
      await Send(16,new byte[]{0},ct);
      var complete=await link.ReceiveAsync(TimeSpan.FromSeconds(300),ct);
      UpgradeWire.Require(complete,18,0);
      journal.CompleteInd=true;save(journal);
      Report(InstallationState.Rebooting,AppText.Get("Upgrade.Message.6"));
      await WaitFinalRestart(ct);continue;
     }
    }
    catch(UpgradeLinkLostException e)
    {
     save(journal);recoveryError=e.Message;if(!recoveryOutage.IsRunning)recoveryOutage.Start();
     Report(InstallationState.ResultUnknown,AppText.Get("Upgrade.Message.20"));
     if(recoveryOutage.Elapsed>=TimeSpan.FromSeconds(30))throw new UpgradeLinkLostException(AppText.Format("Upgrade.ReconnectPending",e.Message));
     // Backoff is included in the next attempt's remaining recovery budget.
     await Task.Delay(2000,overall.Token);
    }
    catch(OperationCanceledException) when(!overall.IsCancellationRequested && attempt.IsCancellationRequested)
    {throw new UpgradeLinkLostException(AppText.Format("Upgrade.ReconnectPending",recoveryError));}
   }
  }
  catch{save(journal);throw;}
  finally{recoveryAttempt=null;}
 }
 void ResetAfterAbort()
 {
  journal.OperationId=Guid.NewGuid().ToString("N");
  journal.ConfirmedFrontier=journal.AcknowledgedBytes=0;
  journal.Started=journal.PartitionBoundary=journal.ResetComplete=journal.Validated=false;
  journal.CompleteInd=journal.FinalRestartObserved=journal.Finished=false;
  journal.OriginalPs4=null;journal.ReplacementAbortPending=false;
  cursor=skipped=0;save(journal);
 }
 void CheckPrepared(UpgradeContext state)
 {
  if(journal.OriginalPs4 is null)throw new UpgradeProtocolException(AppText.Get("Upgrade.Message.21"));
  var expected=Convert.FromHexString(journal.OriginalPs4);expected[26]=0;expected[27]=1;
  if(!state.Value.SequenceEqual(expected))throw new UpgradeProtocolException(AppText.Get("Upgrade.Message.22"));
 }
 async Task WaitFinalRestart(CancellationToken ct)
 {
  using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);deadline.CancelAfter(TimeSpan.FromSeconds(180));
  try{await link.WaitForDeviceDisconnectAsync(deadline.Token);}
  catch(Exception e) when(!ct.IsCancellationRequested)
  {throw new UpgradeProtocolException(AppText.Format("Upgrade.RestartUnobserved",e.Message));}
  journal.FinalRestartObserved=true;save(journal);
  Report(InstallationState.Rebooting,AppText.Get("Upgrade.Message.23"));
  await Task.Delay(3000,ct);
 }
 public async Task<UpgradeContext> Context(CancellationToken ct)
 {
  var ack=await link.CommandAsync(0x0710,new byte[]{0,4},ct);
  RequireAck(ack);
  if(ack.Data.Length!=40 || ack.Data.Span[0]!=0 || ack.Data.Span[1]!=4)throw new UpgradeProtocolException(AppText.Get("Upgrade.Message.24"));
  return UpgradeContext.Decode(ack.Data.Span.Slice(2));
 }
 async Task<byte> SyncStart(byte expectedResume,CancellationToken ct)
 {
  await Send(19,Convert.FromHexString(firmware.UpgradeId),ct);
  var sync=await link.ReceiveAsync(TimeSpan.FromSeconds(30),ct);UpgradeWire.Require(sync,20,6);
  if(sync.Body.Span[0]!=expectedResume || sync.Body.Span[5]!=3 || Convert.ToHexString(sync.Body.Span.Slice(1,4)).ToLowerInvariant()!=firmware.UpgradeId)
   throw new UpgradeProtocolException(AppText.Get("Upgrade.Message.25"));
  await Send(1,Array.Empty<byte>(),ct);var start=await link.ReceiveAsync(TimeSpan.FromSeconds(30),ct);
  if(start.Code!=2 || start.Body.Length<1 || start.Body.Span[0]!=0)throw new UpgradeProtocolException(AppText.Get("Upgrade.Message.26"));
  return expectedResume;
 }
 async Task Transfer(CancellationToken ct)
 {
  cursor=0;skipped=0;await Send(21,Array.Empty<byte>(),ct);
  while(true)
  {
   Report(InstallationState.Transferring,AppText.Get("Upgrade.Message.27"));
   if(!r2 && cursor==limit)return;
   var request=UpgradeWire.DataRequest(await link.ReceiveAsync(TimeSpan.FromSeconds(30),ct));
   if(r2 && cursor==limit)
   {
    if(request.Count!=12 || request.Skip!=0)throw new UpgradeProtocolException(AppText.Get("Upgrade.Message.28"));
    return;
   }
   long start=cursor+request.Skip;
   if(request.Count==0 || start>limit || request.Count>limit-start)throw new UpgradeProtocolException(AppText.Get("Upgrade.Message.29"));
   // SYNC has already matched this operation's image. The device checkpoint is
   // authoritative after loss: a persisted flash word may outlive its lost ACK.
   // Skips are accounted separately, never reclassified as acknowledged bytes.
   skipped+=request.Skip;cursor=start;long end=start+request.Count;
   try
   {
    while(cursor<end)
    {
     int size=(int)Math.Min(45,end-cursor);byte[] payload=new byte[size+1];
     payload[0]=(byte)(cursor+size==bytes.Length?1:0);Array.Copy(bytes,(int)cursor,payload,1,size);
     await Send(4,payload,ct);cursor+=size;journal.AcknowledgedBytes+=size;
     if(cursor>journal.ConfirmedFrontier)RecoveryProgress();
     journal.ConfirmedFrontier=Math.Max(journal.ConfirmedFrontier,cursor);
     Report(InstallationState.Transferring,AppText.Get("Upgrade.Message.27"));await pace(ct);
    }
   }
   finally{save(journal);}
  }
 }
 async Task Validate(CancellationToken ct)
 {
  Report(InstallationState.Validating,r2?AppText.Get("Upgrade.Message.30"):AppText.Get("Upgrade.Message.31"));
  using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);deadline.CancelAfter(TimeSpan.FromSeconds(180));
  while(true)
  {
   await Send(22,Array.Empty<byte>(),deadline.Token);
   var message=await link.ReceiveAsync(TimeSpan.FromSeconds(180),deadline.Token);
   if(message.Code==11){UpgradeWire.Require(message,11,0);journal.Validated=true;save(journal);return;}
   await Task.Delay(UpgradeWire.ValidationDelay(message),deadline.Token);
  }
 }
 async Task Send(byte code,byte[] body,CancellationToken ct)
 {
  var frame=UpgradeWire.Control(code,body);RequireAck(await link.CommandAsync(0x0642,frame.AsMemory(4),ct));
 }
 static void RequireAck(GaiaAcknowledgement ack)
 {
  if(ack.Status!=0)throw new UpgradeProtocolException(AppText.Format("Upgrade.CommandRejected",ack.Command,ack.Status));
 }
}
