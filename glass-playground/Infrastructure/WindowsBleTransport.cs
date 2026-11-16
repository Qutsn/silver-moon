using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Radios;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;

namespace Middleton.Desktop;

public sealed class WindowsBleTransport : ISoundControlTransport,IProgressiveDiscovery
{
 public Action<IReadOnlyList<DeviceCandidate>>? CandidatesChanged {get;set;}
 readonly Func<Func<Task>,Task> consent;
 public WindowsBleTransport(Func<Func<Task>,Task>? consent=null)=>this.consent=consent??(action=>action());
 BluetoothLEDevice? device;
 GattSession? session;
 readonly List<GattDeviceService> services=new();
 readonly Dictionary<Guid,GattCharacteristic> characteristics=new();
 readonly List<GattCharacteristic> subscriptions=new();
 bool disposed, pairingAttempted;
 async Task<bool> EnsureAuthenticationAsync(CancellationToken ct)
 {
  if(pairingAttempted || device is null)return false;pairingAttempted=true;
  var info=await DeviceInformation.CreateFromIdAsync(device.DeviceInformation.Id).AsTask(ct);
  if(info.Pairing.IsPaired || !info.Pairing.CanPair)return false;
  try{await consent(async()=>{
   var access=await device.RequestAccessAsync().AsTask(ct);
   DeviceLog.Write("device-access",new{status=access.ToString()});
   if(access!=DeviceAccessStatus.Allowed)throw new DeviceConnectionException(AppText.Format("Bluetooth.Access",access));
   await PairAsync(info,ct);
  });return true;}
  catch(DeviceConnectionException e){DeviceLog.Write("authentication-unavailable",new{reason=e.Message});throw;}
 }
 public Action<Guid,byte[]>? Changed {get;set;}
 public Action? Disconnected {get;set;}
 public string DeviceName => device?.Name ?? "";
 internal DeviceCandidate PairedRoute=>new(device!.DeviceInformation.Id,DeviceName,DeviceName,"Paired");
 internal async Task WriteUpgradeFrameAsync(byte[] frame,CancellationToken ct)
 {
  if(frame.Length<4 || frame[0]!=0 || frame[1]!=10)throw new UpgradeProtocolException("Invalid GAIA frame.");
  if(!characteristics.TryGetValue(DeviceIds.GaiaWrite,out var characteristic))throw new UpgradeLinkLostException(AppText.Get("Device.Message.32"));
  if(session is null || frame.Length>session.MaxPduSize-3)throw new UpgradeProtocolException(AppText.Get("Device.Message.33"));
  using var writer=new DataWriter();writer.WriteBytes(frame);
  var buffer=writer.DetachBuffer();
  var result=await ProtectedGatt.RunAsync(()=>characteristic.WriteValueWithResultAsync(buffer,GattWriteOption.WriteWithResponse).AsTask(ct),()=>EnsureAuthenticationAsync(ct),r=>r.Status.ToString(),r=>r.ProtocolError);
  if(result.Status!=GattCommunicationStatus.Success)throw new UpgradeLinkLostException(AppText.Format("Bluetooth.Write",result.Status));
 }
 public bool Has(Guid id) => characteristics.ContainsKey(id);
 public async Task<IReadOnlyList<DeviceCandidate>> ScanAsync(CancellationToken ct)
 {
  var adapter=await BluetoothAdapter.GetDefaultAsync().AsTask(ct);
  if(adapter is null || !adapter.IsLowEnergySupported)throw new DeviceConnectionException(AppText.Get("Device.Message.34"));
  var radio=await adapter.GetRadioAsync().AsTask(ct);
  if(radio is not null && radio.State!=RadioState.On)throw new DeviceConnectionException(AppText.Get("Device.Message.35"));
  var found=new DiscoveryCandidates();var gate=new object();
  BluetoothError error=BluetoothError.Success;
  var watcher=new BluetoothLEAdvertisementWatcher {ScanningMode=BluetoothLEScanningMode.Active};
  void Received(BluetoothLEAdvertisementWatcher sender,BluetoothLEAdvertisementReceivedEventArgs args)
  {
   string name=args.Advertisement.LocalName;
   lock(gate)found.Observe(args.BluetoothAddress.ToString("X12"),args.BluetoothAddressType.ToString(),name,args.IsConnectable,DateTimeOffset.UtcNow);
  }
  void Stopped(BluetoothLEAdvertisementWatcher sender,BluetoothLEAdvertisementWatcherStoppedEventArgs args){lock(gate)error=args.Error;}
  watcher.Received+=Received;watcher.Stopped+=Stopped;
  try
  {
   DeviceLog.Write("scan-start");watcher.Start();
   bool firstReported=false;
   for(int tick=0;tick<16;tick++)
   {
    await Task.Delay(500,ct);
    IReadOnlyList<DeviceCandidate> partial;
    lock(gate)partial=found.Snapshot(DateTimeOffset.UtcNow);
    if(partial.Count>0){if(!firstReported){DeviceLog.Write("scan-first-candidates",new{count=partial.Count,scanMilliseconds=(tick+1)*500});firstReported=true;}CandidatesChanged?.Invoke(partial);}
   }
   IReadOnlyList<DeviceCandidate> advertised;
   lock(gate){if(error!=BluetoothError.Success)throw new DeviceConnectionException(AppText.Format("Bluetooth.Scan",error));advertised=found.Snapshot(DateTimeOffset.UtcNow);}
   watcher.Stop();
   var candidates=(await PairedCandidatesAsync(ct)).ToList();
   foreach(var candidate in advertised)
   {
    // Resolve Windows identity without opening GATT; never merge devices by name.
    using var route=await BluetoothLEDevice.FromBluetoothAddressAsync(Convert.ToUInt64(candidate.Id,16),Enum.Parse<BluetoothAddressType>(candidate.AddressType)).AsTask(ct);
    if(route is not null && candidates.Any(c=>c.Id==route.DeviceInformation.Id))continue;
    candidates.Add(candidate);
   }
   candidates=candidates.OrderByDescending(c=>c.Name.Contains("Middleton",StringComparison.OrdinalIgnoreCase)).ThenBy(c=>c.Id,StringComparer.Ordinal).ToList();
   DeviceLog.Write("scan-complete",new{count=candidates.Count,addressTypes=candidates.Select(c=>c.AddressType).ToArray()});return candidates;
  }
  finally{watcher.Stop();watcher.Received-=Received;watcher.Stopped-=Stopped;}
 }
 public async Task OpenAsync(DeviceCandidate candidate,CancellationToken ct)
 {
  if(candidate.AddressType=="Paired")
  {
   var known=await DeviceInformation.CreateFromIdAsync(candidate.Id).AsTask(ct);
   if(!known.Pairing.IsPaired)throw new DeviceConnectionException(AppText.Get("Device.Message.36"));
   device=await BluetoothLEDevice.FromIdAsync(candidate.Id).AsTask(ct);
  }
  else
  {
  var address=Convert.ToUInt64(candidate.Id,16);
  device=Enum.TryParse<BluetoothAddressType>(candidate.AddressType,out var type) && type!=BluetoothAddressType.Unspecified
   ? await BluetoothLEDevice.FromBluetoothAddressAsync(address,type).AsTask(ct)
   : await BluetoothLEDevice.FromBluetoothAddressAsync(address).AsTask(ct);
  }
  if(device is null)throw new DeviceConnectionException(AppText.Get("Device.Message.37"));
  var info=await DeviceInformation.CreateFromIdAsync(device.DeviceInformation.Id).AsTask(ct);
  DeviceLog.Write("pairing-state",new{paired=info.Pairing.IsPaired,canPair=info.Pairing.CanPair,addressType=device.BluetoothAddressType.ToString()});
  session=await GattSession.FromDeviceIdAsync(device.BluetoothDeviceId).AsTask(ct);
  if(session is null || !session.CanMaintainConnection)throw new DeviceConnectionException(AppText.Get("Device.Message.38"));
  session.MaintainConnection=true;
  device.ConnectionStatusChanged+=OnConnectionStatus;
  GattDeviceServicesResult discovered;
  for(int attempt=0;;attempt++)
  {
   discovered=await device.GetGattServicesAsync(BluetoothCacheMode.Uncached).AsTask(ct);
   DeviceLog.Write("service-discovery-attempt",new{attempt,status=discovered.Status.ToString()});
   if(discovered.Status!=GattCommunicationStatus.Unreachable || attempt>=2)break;
   foreach(var service in discovered.Services)service.Dispose();
   await Task.Delay(1000,ct);
  }
  // Own every returned service, including on failure, so disposal never leaves stale GATT handles.
  services.AddRange(discovered.Services);
  DeviceLog.Write("service-discovery",new{status=discovered.Status.ToString(),protocolError=discovered.ProtocolError});
  if(discovered.Status!=GattCommunicationStatus.Success)throw new DeviceConnectionException(discovered.Status==GattCommunicationStatus.Unreachable?AppText.Get("Device.Message.39"):AppText.Format("Bluetooth.Discovery",discovered.Status));
  while(session.SessionStatus!=GattSessionStatus.Active){ct.ThrowIfCancellationRequested();await Task.Delay(50,ct);}
  DeviceLog.Write("gatt-session-active",new{mtu=session.MaxPduSize});
  foreach(var service in services.Where(s=>s.Uuid==DeviceIds.GaiaService || s.Uuid==DeviceIds.Standard("180a") || s.Uuid==DeviceIds.Standard("180f") || s.Uuid==DeviceIds.Standard("aa00")))
  {
   var result=await service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached).AsTask(ct);
   if(result.Status!=GattCommunicationStatus.Success && result.Status is GattCommunicationStatus.AccessDenied or GattCommunicationStatus.Unreachable)
   {
    DeviceLog.Write("characteristics-cache-fallback",new{service=service.Uuid,status=result.Status.ToString()});
    result=await service.GetCharacteristicsAsync(BluetoothCacheMode.Cached).AsTask(ct);
   }
   if(result.Status!=GattCommunicationStatus.Success){DeviceLog.Write("characteristics-unavailable",new{service=service.Uuid,status=result.Status.ToString()});continue;}
   foreach(var characteristic in result.Characteristics)characteristics.TryAdd(characteristic.Uuid,characteristic);
  }
  DeviceLog.Write("services-discovered",new{count=services.Count,characteristics=characteristics.Count});
 }
 public static async Task<IReadOnlyList<DeviceCandidate>> PairedCandidatesAsync(CancellationToken ct)
 {
  var devices=await DeviceInformation.FindAllAsync(BluetoothLEDevice.GetDeviceSelectorFromPairingState(true)).AsTask(ct);
  return devices.Select(d=>new DeviceCandidate(d.Id,d.Name,AppText.Format("Device.Paired",string.IsNullOrWhiteSpace(d.Name)?AppText.Get("Device.Message.40"):d.Name), "Paired")).ToArray();
 }
 static async Task PairAsync(DeviceInformation info,CancellationToken ct)
 {
  DeviceLog.Write("pairing-call-context",new{thread=Thread.CurrentThread.GetApartmentState().ToString(),context=SynchronizationContext.Current?.GetType().FullName});
  var custom=info.Pairing.Custom;
  void Confirm(DeviceInformationCustomPairing sender,DevicePairingRequestedEventArgs args)
  {if(!ct.IsCancellationRequested && args.PairingKind==DevicePairingKinds.ConfirmOnly)args.Accept();}
  custom.PairingRequested+=Confirm;
  try
  {
   var status=await PairingNegotiation.RunAsync(async(level,token)=>{
    var protection=level switch{PairingProtection.EncryptionAndAuthentication=>DevicePairingProtectionLevel.EncryptionAndAuthentication,PairingProtection.Encryption=>DevicePairingProtectionLevel.Encryption,_=>DevicePairingProtectionLevel.Default};
    var result=level==PairingProtection.Default?await custom.PairAsync(DevicePairingKinds.ConfirmOnly).AsTask(token):await custom.PairAsync(DevicePairingKinds.ConfirmOnly,protection).AsTask(token);
    DeviceLog.Write("pairing-result",new{requested=level.ToString(),status=result.Status.ToString(),protection=result.ProtectionLevelUsed.ToString()});
    return result.Status.ToString();
   },ct);
   if(!PairingOutcome.Accepted(status))throw new DeviceConnectionException(PairingOutcome.FailureMessage(status));
   var refreshed=await DeviceInformation.CreateFromIdAsync(info.Id).AsTask(ct);
   if(!refreshed.Pairing.IsPaired)throw new DeviceConnectionException(AppText.Get("Device.Message.41"));
  }
  finally{custom.PairingRequested-=Confirm;}
 }
 public async Task<byte[]?> ReadAsync(Guid id,CancellationToken ct)
 {
  if(!characteristics.TryGetValue(id,out var characteristic) || !characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Read))return null;
  try
  {
   var result=await ProtectedGatt.RunAsync(()=>characteristic.ReadValueAsync(BluetoothCacheMode.Uncached).AsTask(ct),()=>EnsureAuthenticationAsync(ct),r=>r.Status.ToString(),r=>r.ProtocolError);
   DeviceLog.Write("read",new{characteristic=id,status=result.Status.ToString(),protocolError=result.ProtocolError,length=result.Value?.Length,soundValue=(id==DeviceIds.Volume || id==DeviceIds.Eq) && result.Status==GattCommunicationStatus.Success && result.Value is not null?Convert.ToHexString(Bytes(result.Value)):null});
   return result.Status==GattCommunicationStatus.Success && result.Value is not null ? Bytes(result.Value) : null;
  }
  catch(DeviceConnectionException){throw;}
  catch(OperationCanceledException){throw;}
  catch(UnauthorizedAccessException e){throw new DeviceConnectionException(AppText.Format("Bluetooth.Denied",e.HResult));}
  catch(Exception e){DeviceLog.Write("read-failed",new{characteristic=id,error=e.GetType().Name,hresult=e.HResult});return null;}
 }
 public async Task<bool> WriteAsync(Guid id,byte[] value,CancellationToken ct)
 {
  if(id!=DeviceIds.Volume && id!=DeviceIds.Eq)throw new ArgumentException("Only sound characteristics are writable through this interface.",nameof(id));
  if(id==DeviceIds.Volume?DeviceValues.Volume(value) is null:DeviceValues.Eq(value) is null)throw new ArgumentException("Invalid sound payload.",nameof(value));
  if(!characteristics.TryGetValue(id,out var characteristic) || !characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Write))return false;
  try
  {
   using var writer=new DataWriter();writer.WriteBytes(value);var buffer=writer.DetachBuffer();
   var result=await ProtectedGatt.RunAsync(()=>characteristic.WriteValueWithResultAsync(buffer,GattWriteOption.WriteWithResponse).AsTask(ct),()=>EnsureAuthenticationAsync(ct),r=>r.Status.ToString(),r=>r.ProtocolError);
   DeviceLog.Write("write",new{characteristic=id,status=result.Status.ToString(),protocolError=result.ProtocolError,length=value.Length});
   return result.Status==GattCommunicationStatus.Success;
  }
  catch(DeviceConnectionException){throw;}
  catch(OperationCanceledException){throw;}
  catch(UnauthorizedAccessException e){throw new DeviceConnectionException(AppText.Format("Bluetooth.Denied",e.HResult));}
  catch(Exception e){DeviceLog.Write("write-failed",new{characteristic=id,error=e.GetType().Name,hresult=e.HResult});return false;}
 }
 public async Task<bool> SubscribeAsync(Guid id,CancellationToken ct)
 {
  if(!characteristics.TryGetValue(id,out var characteristic))return false;
  var properties=characteristic.CharacteristicProperties;
  var mode=properties.HasFlag(GattCharacteristicProperties.Notify)?GattClientCharacteristicConfigurationDescriptorValue.Notify:properties.HasFlag(GattCharacteristicProperties.Indicate)?GattClientCharacteristicConfigurationDescriptorValue.Indicate:GattClientCharacteristicConfigurationDescriptorValue.None;
  if(mode==GattClientCharacteristicConfigurationDescriptorValue.None)return false;
  characteristic.ValueChanged+=OnValueChanged;
  // Register before the await so a failed/cancelled CCCD write is still cleaned up.
  subscriptions.Add(characteristic);
  try
  {
   var result=await ProtectedGatt.RunAsync(()=>characteristic.WriteClientCharacteristicConfigurationDescriptorWithResultAsync(mode).AsTask(ct),()=>EnsureAuthenticationAsync(ct),r=>r.Status.ToString(),r=>r.ProtocolError);
   DeviceLog.Write("subscribe",new{characteristic=id,status=result.Status.ToString(),protocolError=result.ProtocolError});
   return result.Status==GattCommunicationStatus.Success;
  }
  catch(DeviceConnectionException){throw;}
  catch(OperationCanceledException){throw;}
  catch(UnauthorizedAccessException e){throw new DeviceConnectionException(AppText.Format("Bluetooth.Denied",e.HResult));}
  catch(Exception e){DeviceLog.Write("subscribe-failed",new{characteristic=id,error=e.GetType().Name,hresult=e.HResult});return false;}
 }
 static byte[] Bytes(IBuffer buffer){using var reader=DataReader.FromBuffer(buffer);var bytes=new byte[buffer.Length];reader.ReadBytes(bytes);return bytes;}
 void OnValueChanged(GattCharacteristic sender,GattValueChangedEventArgs args)
 {
  if(disposed)return;
  try
  {
   var bytes=Bytes(args.CharacteristicValue);
   if(Environment.GetEnvironmentVariable("MIDDLETON_TRACE_SOUND")=="1" && (sender.Uuid==DeviceIds.Volume || sender.Uuid==DeviceIds.Eq))DeviceLog.Write("sound-notification",new{characteristic=sender.Uuid,value=Convert.ToHexString(bytes)});
   Changed?.Invoke(sender.Uuid,bytes);
  }
  catch(Exception e){DeviceLog.Write("notification-failed",new{error=e.GetType().Name});}
 }
 void OnConnectionStatus(BluetoothLEDevice sender,object args)
 {
  if(!disposed && sender.ConnectionStatus==BluetoothConnectionStatus.Disconnected){DeviceLog.Write("disconnected");Disconnected?.Invoke();}
 }
 public async ValueTask DisposeAsync()
 {
  if(disposed)return;disposed=true;Changed=null;Disconnected=null;
  if(device is not null)device.ConnectionStatusChanged-=OnConnectionStatus;
  // Bound cleanup across all CCCDs, including unreachable devices. Release services afterwards.
  using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(3));
  foreach(var characteristic in subscriptions)
  {
   characteristic.ValueChanged-=OnValueChanged;
   try{await characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.None).AsTask(timeout.Token);}catch(Exception){}
  }
  subscriptions.Clear();characteristics.Clear();
  foreach(var service in services)service.Dispose();services.Clear();
  if(session is not null){session.MaintainConnection=false;session.Dispose();session=null;}
  device?.Dispose();device=null;
  DeviceLog.Write("gatt-released");
 }
}



