using System;
using System.Buffers.Binary;

namespace Middleton.Desktop;
// Middleton-specific PS4 layout established by captured readbacks, not a generic
// CSR/Marshall schema. See docs/02-UPGRADE-AND-VALIDATION.md and UpgradeRecoveryChecks for preservation tests.
// Word() is big-endian; image ID word order is intentionally reversed (12, then 11).
public sealed class UpgradeContext
{
 readonly byte[] raw;
 UpgradeContext(byte[] raw)=>this.raw=raw;
 ushort Word(int n)=>BinaryPrimitives.ReadUInt16BigEndian(raw.AsSpan(n*2,2));
 public string ImageId=>$"{Word(12):x4}{Word(11):x4}";
 public ushort Resume=>Word(13);
 public ushort LastClosed=>Word(14);
 public bool IsIdle=>ImageId=="00000000" && Resume==0 && LastClosed==0 && Word(7)==0 && Word(8)==0 && Word(9)==0;
 public bool R2PartitionsClosed=>StagedPartitionsClosed("6e0b0188");
 public bool StagedPartitionsClosed(string expectedImageId)=>ImageId==expectedImageId && LastClosed==10 && Word(7)==5 && Word(8)==2;
 public byte[] Value=>(byte[])raw.Clone();
 public static UpgradeContext Decode(ReadOnlySpan<byte> value)
 {
  if(value.Length!=38)throw new UpgradeProtocolException(AppText.Get("Upgrade.Message.32"));
  return new(value.ToArray());
 }
 public byte[] PrepareR2Validation(string expectedImageId="6e0b0188")
 {
  if(!StagedPartitionsClosed(expectedImageId) || Resume!=0 || Word(0)!=0x8003 || Word(1)!=5 || Word(2)!=2 || Word(3)!=1)
   throw new UpgradeProtocolException(AppText.Get("Upgrade.Message.33"));
  // STORE_PS_KEY: key (2 bytes), complete value (38 bytes), trailing mask (38).
  // Preserve all original words except resume word 13; no other model is authorized.
  var payload=new byte[78];payload[1]=4;raw.CopyTo(payload,2);payload[28]=0;payload[29]=1;return payload;
 }
}

