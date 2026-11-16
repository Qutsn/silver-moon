using System;
using System.Linq;
using System.Security.Cryptography;

namespace Middleton.Desktop;

public enum FirmwareStrategy { OfficialFull, MiddletonR2Staged }
public sealed record FirmwareDescriptor(string Id,string DisplayName,string DisplayVersion,string ContainerVersion,
 int Length,string Sha256,string UpgradeId,string DeviceProfile,FirmwareStrategy Strategy,string Source);
public sealed record FirmwareAnalysis(int Length,string Sha256,string UpgradeId,FirmwareDescriptor? Known)
{
 public string? StructureError {get;init;}
 public FirmwareDescriptor? ThirdParty {get;init;}
 public FirmwareDescriptor? Installable=>Known??ThirdParty;
 public string? InstallationError {get;init;}
 // Compatibility is not authorization to flash: coordinator readiness and a fresh device check are separate gates.
 public bool CanInstallOn(string profile)=>Installable is not null && Installable.DeviceProfile==profile;
}
public static class FirmwareCatalog
{
 static readonly FirmwareDescriptor[] Entries={
  new("middleton-r2",AppText.Get("FirmwareChooser.Text.2069"),"25.2.52","5.2",3266308,
   "80ed2088247976f10968f5d4aefb3cabf367ff0b0a1f774ac6e62a292c459024","6e0b0188","middleton",FirmwareStrategy.MiddletonR2Staged,AppText.Get("Upgrade.Message.34")),
  new("middleton-official",AppText.Get("FirmwareChooser.Text.2080"),"17.0.37","5.2",3266308,
   "db74c3468bd08d61f3a4e4e4e9e6eea9b19d95d6e852b9fd5e9cbac3e91c9472","91408307","middleton",FirmwareStrategy.OfficialFull,AppText.Get("Upgrade.Message.35"))
 };
 public static FirmwareAnalysis Analyze(ReadOnlySpan<byte> bytes)
 {
  string sha=Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
  // MD5 tail is the device's protocol identifier, not an authenticity check.
  string id=Convert.ToHexString(MD5.HashData(bytes).AsSpan(12,4)).ToLowerInvariant();
  int length=bytes.Length;
  var known=Entries.SingleOrDefault(e=>e.Length==length && e.Sha256==sha && e.UpgradeId==id);
  string? error=known is null?ThirdPartyFirmware.Check(bytes):null;
  // Keep Known reserved for exact catalog hashes. A third-party file earns a
  // strategy only through the separately versioned, byte-enforced profile.
  var thirdParty=known is null && error is null
   ?new FirmwareDescriptor("third-party-"+sha,AppText.Get("Upgrade.Message.36"),"25.2.52","5.2",length,sha,id,"middleton",FirmwareStrategy.MiddletonR2Staged,ThirdPartyFirmware.Specification)
   :null;
  return new(length,sha,id,known){StructureError=FirmwareContainer.CheckMiddletonV1(bytes),ThirdParty=thirdParty,InstallationError=error};
 }
}
