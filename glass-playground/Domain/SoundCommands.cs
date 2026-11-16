using System;

namespace Middleton.Desktop;

public static class SoundCommands
{
 public static byte[] Volume(int value) => TryVolume(value) ?? throw new ArgumentOutOfRangeException(nameof(value));
 public static byte[]? TryVolume(int value) => value is >= 0 and <= 32 ? new[]{(byte)value} : null;
 public static byte[] Eq(int bass,int treble) => TryEq(bass,treble) ?? throw new ArgumentOutOfRangeException();
 public static byte[]? TryEq(int bass,int treble) => bass is >= 0 and <= 10 && treble is >= 0 and <= 10
  ? new[]{(byte)bass,byte.MaxValue,byte.MaxValue,byte.MaxValue,(byte)treble,(byte)0,(byte)0,(byte)0} : null;
 public static byte[]? UpdateEq(byte[]? current,int? bass,int? treble)
 {
  var parsed=DeviceValues.Eq(current);
  if(parsed is null || (bass is null && treble is null))return null;
  return TryEq(bass ?? parsed.Value.Bass,treble ?? parsed.Value.Treble);
 }
}
