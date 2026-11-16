using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Middleton.Desktop;
public sealed partial class DeviceConnection
{
 public Task<DeviceSnapshot> SetVolumeAsync(int value,CancellationToken ct)
 {
  if(value is <0 or >32)throw new ArgumentOutOfRangeException(nameof(value));
  return QueueSoundAsync(SoundWriteKind.Volume,value,null,null,ct);
 }
 public Task<DeviceSnapshot> SetEqAsync(int? bass,int? treble,CancellationToken ct)
 {
  if(bass is <1 or >10 || treble is <1 or >10 || (bass is null && treble is null))throw new ArgumentOutOfRangeException();
  return QueueSoundAsync(SoundWriteKind.Eq,null,bass,treble,ct);
 }
 async Task<DeviceSnapshot> QueueSoundAsync(SoundWriteKind kind,int? volume,int? bass,int? treble,CancellationToken ct)
 {
  ct.ThrowIfCancellationRequested();
  Guid sessionId;
  lock(sync){ObjectDisposedException.ThrowIf(disposed,this);sessionId=current is {Connected:true} snapshot?snapshot.SessionId:throw new DeviceConnectionException(AppText.Get("Device.Message.5"));}
  if(officialSoundTrial)
  {
   lock(sync)
   {
    if(current is not {Connected:true} snapshot || snapshot.SessionId!=sessionId)throw new DeviceConnectionException(AppText.Get("Device.Message.6"));
    if(kind==SoundWriteKind.Eq)
    {
     bass??=snapshot.Bass;treble??=snapshot.Treble;
     if(bass is null || treble is null)throw new DeviceConnectionException(AppText.Get("Device.Message.7"));
    }
    current=kind==SoundWriteKind.Volume?snapshot with {Volume=volume}:snapshot with {Bass=bass,Treble=treble};
    Updated?.Invoke(current);
   }
  }
  await writeQueue.EnqueueAsync(new(sessionId,kind,volume,bass,treble),ct);
  lock(sync){return current is {Connected:true} snapshot && snapshot.SessionId==sessionId?snapshot:throw new DeviceConnectionException(AppText.Get("Device.Message.6"));}
 }
 async Task ExecuteSoundWriteAsync(SoundWriteRequest request,CancellationToken ct)
 {
  await serial.WaitAsync(ct);
  try
  {
   lock(sync){ObjectDisposedException.ThrowIf(disposed,this);if(current is not {Connected:true} snapshot || snapshot.SessionId!=request.SessionId)throw new DeviceConnectionException(AppText.Get("Device.Message.8"));}
   var active=transport as ISoundControlTransport ?? throw new DeviceConnectionException(AppText.Get("Device.Message.9"));
   var id=request.Kind==SoundWriteKind.Volume?DeviceIds.Volume:DeviceIds.Eq;
   if(officialSoundTrial)
   {
    var payload=request.Kind==SoundWriteKind.Volume?SoundCommands.Volume(request.Volume!.Value):SoundCommands.Eq(request.Bass!.Value,request.Treble!.Value);
    try
    {
     if(!await active.WriteAsync(id,payload,ct))throw new DeviceConnectionException(AppText.Get("Device.Message.10"));
    }
    catch
    {
     lock(sync)
     {
      if(current is {Connected:true} latest && latest.SessionId==request.SessionId)
      {
       // Invalidate only this target; a newer click or device notification owns newer state.
       if(id==DeviceIds.Volume && latest.Volume==request.Volume)current=latest with {Volume=null};
       else if(id==DeviceIds.Eq && latest.Bass==request.Bass && latest.Treble==request.Treble)current=latest with {Bass=null,Treble=null};
       Updated?.Invoke(current!);
      }
     }
     throw;
    }
    return;
   }
   var before=request.Kind==SoundWriteKind.Volume?null:await active.ReadAsync(id,ct);
   byte[] expected;
   if(request.Kind==SoundWriteKind.Volume)expected=SoundCommands.Volume(request.Volume!.Value);
   else expected=SoundCommands.UpdateEq(before,request.Bass,request.Treble)??throw new DeviceConnectionException(AppText.Get("Device.Message.11"));
   ct.ThrowIfCancellationRequested();
   lock(sync){if(current is not {Connected:true})throw new DeviceConnectionException(AppText.Get("Device.Message.12"));}
   bool accepted=await active.WriteAsync(id,expected,ct);
   var actual=await active.ReadAsync(id,ct);
   for(int attempt=0;accepted && attempt<4 && (actual is null || !actual.SequenceEqual(expected));attempt++)
   {
    await Task.Delay(250,ct);
    actual=await active.ReadAsync(id,ct);
   }
   // Report what the device confirms, even when it differs from our requested value.
   lock(sync)
   {
    if(current is not {Connected:true} snapshot || snapshot.SessionId!=request.SessionId)throw new DeviceConnectionException(AppText.Get("Device.Message.13"));
    if(id==DeviceIds.Volume)current=snapshot with {Volume=DeviceValues.Volume(actual)};
    else {var eq=DeviceValues.Eq(actual);current=snapshot with {Bass=eq?.Bass,Treble=eq?.Treble};}
    Updated?.Invoke(current);
   }
   if(!accepted || actual is null || !actual.SequenceEqual(expected))throw new DeviceConnectionException(AppText.Get("Device.Message.14"));
  }
  finally{serial.Release();}
 }
}
