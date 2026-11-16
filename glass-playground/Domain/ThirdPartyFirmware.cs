using System;
using System.Security.Cryptography;

namespace Middleton.Desktop;

/// <summary>
/// Versioned admission rule for fixed-layout Middleton MCU modifications.
/// The two adjacent MCU payloads may change; all other bytes must match the
/// reviewed r2 base. Hashes enforce metadata, filesystem and QCC preservation.
/// Admission proves this rule only, not that arbitrary MCU instructions can boot.
/// See docs/19 for offsets, provenance and the device-side completion boundary.
/// </summary>
public static class ThirdPartyFirmware
{
 public const string Specification="middleton-mcu-v1";
 public const int ImageLength=3266308;
 public const int PayloadStart=2445564;
 public const int PayloadEnd=2713416;
 public const int TransferEnd=3266040;
 const string PrefixSha256="c14433030a3b862dc04e8a6b5079e95a8ca0e7a90445717854cc7f57f7843614";
 const string SuffixSha256="c09c70c2cb0c6536428a44b0363f227f56ca83345e8d98bf05a57866e22636d4";
 public static string? Check(ReadOnlySpan<byte> image)
 {
  if(image.Length!=ImageLength)return AppText.Get("Upgrade.Message.37");
  if(FirmwareContainer.CheckMiddletonV1(image) is { } error)return AppText.Get("Upgrade.Message.38")+error;
  if(!Matches(image[..PayloadStart],PrefixSha256) || !Matches(image[PayloadEnd..],SuffixSha256))
   return AppText.Get("Upgrade.Message.39");
  return null;
 }
 static bool Matches(ReadOnlySpan<byte> bytes,string expected)=>Convert.ToHexString(SHA256.HashData(bytes)).Equals(expected,StringComparison.OrdinalIgnoreCase);
}
