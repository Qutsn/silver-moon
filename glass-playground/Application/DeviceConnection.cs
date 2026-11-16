using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Middleton.Desktop;

// This contract deliberately has no firmware or sound-setting write operation.
public interface IReadOnlyBleTransport : IAsyncDisposable
{
 Action<Guid,byte[]>? Changed {get;set;}
 Action? Disconnected {get;set;}
 string DeviceName {get;}
 Task<IReadOnlyList<DeviceCandidate>> ScanAsync(CancellationToken ct);
 Task OpenAsync(DeviceCandidate candidate,CancellationToken ct);
 bool Has(Guid characteristic);
 Task<byte[]?> ReadAsync(Guid characteristic,CancellationToken ct);
 Task<bool> SubscribeAsync(Guid characteristic,CancellationToken ct);
}

public interface IProgressiveDiscovery
{
 Action<IReadOnlyList<DeviceCandidate>>? CandidatesChanged {get;set;}
}

public interface ISoundControlTransport : IReadOnlyBleTransport
{
 Task<bool> WriteAsync(Guid characteristic,byte[] value,CancellationToken ct);
}

public sealed partial class DeviceConnection : IAsyncDisposable
{
 readonly Func<IReadOnlyBleTransport> factory;
 readonly SemaphoreSlim serial = new(1,1);
 readonly SoundWriteQueue writeQueue;
 readonly bool officialSoundTrial;
 readonly object sync = new();
 IReadOnlyBleTransport? transport;
 long generation;
 bool disposed;
 DeviceSnapshot? current;
 public DeviceSnapshot? Current {get {lock(sync)return current;}}
 public event Action<DeviceSnapshot>? Updated;
 public DeviceConnection(Func<IReadOnlyBleTransport> factory,bool officialSoundTrial=false){this.factory=factory;this.officialSoundTrial=officialSoundTrial;writeQueue=new(ExecuteSoundWriteAsync,officialSoundTrial?100:0);}

 public async Task<IReadOnlyList<DeviceCandidate>> ScanAsync(CancellationToken ct,Action<IReadOnlyList<DeviceCandidate>>? progress=null)
 {
  await serial.WaitAsync(ct);
  try {ObjectDisposedException.ThrowIf(disposed,this);await ReleaseAsync();transport=factory();if(transport is IProgressiveDiscovery progressive)progressive.CandidatesChanged=progress;return await transport.ScanAsync(ct);}
  finally {serial.Release();}
 }
 public async Task<DeviceSnapshot> ConnectAsync(DeviceCandidate candidate,CancellationToken ct)
 {
  await serial.WaitAsync(ct);
  try
  {
   ObjectDisposedException.ThrowIf(disposed,this);
   await ReleaseAsync();var active=factory();transport=active;
   long epoch;lock(sync)epoch=++generation;
   bool lost=false;
   var pending=new Dictionary<Guid,byte[]?>();
   active.Changed=(id,bytes)=>{lock(sync){if(epoch==generation && (id==DeviceIds.Battery || id==DeviceIds.Volume || id==DeviceIds.Eq))pending[id]=bytes;}};
   active.Disconnected=()=>{lock(sync){if(epoch!=generation)return;lost=true;if(current is not null){current=current with {Connected=false};Updated?.Invoke(current);}}};
   await active.OpenAsync(candidate,ct);
   var model=DeviceValues.Text(await active.ReadAsync(DeviceIds.Model,ct));
   var manufacturer=DeviceValues.Text(await active.ReadAsync(DeviceIds.Manufacturer,ct));
   var identification=DeviceProfiles.Identify(model,manufacturer,active.Has);
   if(!identification.Supported)throw new DeviceConnectionException(identification.Message);
   // Authenticate protected sound access before subscriptions and before showing a usable connection.
   // An unsolicited notification is not evidence that protected reads or writes are authorized.
   if(active.Has(DeviceIds.Volume))await active.ReadAsync(DeviceIds.Volume,ct);
   if(active.Has(DeviceIds.Eq))await active.ReadAsync(DeviceIds.Eq,ct);
   if(!active.Has(DeviceIds.GaiaWrite) || !await active.SubscribeAsync(DeviceIds.GaiaResponse,ct))throw new DeviceConnectionException(AppText.Get("Device.Message.3"));
   // Buffer notifications during initial reads, then atomically hand off to the live snapshot.
   foreach(var id in new[]{DeviceIds.Battery,DeviceIds.Volume,DeviceIds.Eq})
    if(active.Has(id))await active.SubscribeAsync(id,ct);
   var serialNumber=DeviceValues.Text(await active.ReadAsync(DeviceIds.Serial,ct));
   var firmware=DeviceValues.Text(await active.ReadAsync(DeviceIds.Firmware,ct));
   var hardware=DeviceValues.Text(await active.ReadAsync(DeviceIds.Hardware,ct));
   async Task<byte[]?> ReadFreshAsync(Guid id)
   {
    lock(sync)pending.Remove(id);
    return await active.ReadAsync(id,ct);
   }
   var batteryBytes=await ReadFreshAsync(DeviceIds.Battery);
   var volumeBytes=await ReadSoundFreshAsync(DeviceIds.Volume);
   var eqBytes=await ReadSoundFreshAsync(DeviceIds.Eq);
   ct.ThrowIfCancellationRequested();
   lock(sync)
   {
    if(lost)throw new DeviceConnectionException(AppText.Get("Device.Message.4"));
    var battery=DeviceValues.Battery(pending.GetValueOrDefault(DeviceIds.Battery,batteryBytes));
    var volume=DeviceValues.Volume(pending.GetValueOrDefault(DeviceIds.Volume,volumeBytes));
    var eq=DeviceValues.Eq(pending.GetValueOrDefault(DeviceIds.Eq,eqBytes));
    current=new DeviceSnapshot(Guid.NewGuid(),true,active.DeviceName,model!,serialNumber,firmware,hardware,battery,volume,eq?.Bass,eq?.Treble,new(battery.HasValue,volume.HasValue,eq.HasValue,true));
    active.Changed=(id,bytes)=>ApplyNotification(epoch,id,bytes);
   Updated?.Invoke(current);return current;
   }
   async Task<byte[]?> ReadSoundFreshAsync(Guid id)
   {
    var first=await ReadFreshAsync(id);
    await Task.Delay(180,ct);
    var second=await ReadFreshAsync(id);
    return second ?? first;
   }
  }
  catch {await ReleaseAsync();throw;}
  finally {serial.Release();}
 }
 void ApplyNotification(long epoch,Guid id,byte[] bytes)
 {
  lock(sync)
  {
   if(epoch!=generation || current is not {Connected:true})return;
   var previous=current;
   if(id==DeviceIds.Battery)current=current with {Battery=DeviceValues.Battery(bytes)};
   else if(id==DeviceIds.Volume)current=current with {Volume=DeviceValues.Volume(bytes)};
   else if(id==DeviceIds.Eq){var eq=DeviceValues.Eq(bytes);current=current with {Bass=eq?.Bass,Treble=eq?.Treble};}
   else return;
   if(current!=previous)Updated?.Invoke(current);
  }
 }
 async ValueTask ReleaseAsync()
 {
  lock(sync){generation++;current=null;}
  if(transport is not null){transport.Changed=null;transport.Disconnected=null;await transport.DisposeAsync();transport=null;}
 }
 public async ValueTask DisposeAsync()
 {
  await writeQueue.DisposeAsync();await serial.WaitAsync();try{disposed=true;await ReleaseAsync();}finally{serial.Release();}
 }
}


