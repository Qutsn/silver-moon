using System;
using System.Buffers.Binary;
using System.Text;

namespace Middleton.Desktop;

// Structural profile only. Passing this check does not select an installation strategy.
public static class FirmwareContainer
{
 public static string? CheckMiddletonV1(ReadOnlySpan<byte> bytes)
 {
  int[] lengths={48,20,276,10924,572116,70128,2059844,552588,256};
  int[] partitions={0,1,4,5,6,7,9};
  int offset=0;
  for(int i=0;i<lengths.Length;i++)
  {
   if(bytes.Length-offset<12)return AppText.Format("Firmware.Header",i+1);
   string expected=i==0?"APPUHDR5":i==8?"APPUPFTR":"PARTDATA";
   if(Encoding.ASCII.GetString(bytes.Slice(offset,8))!=expected)return AppText.Format("Firmware.RecordType",i+1,expected);
   uint length=BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(offset+8,4));
   if(length!=lengths[i])return AppText.Format("Firmware.RecordLength",i+1,lengths[i]);
   if(length>bytes.Length-offset-12)return AppText.Format("Firmware.RecordTruncated",i+1);
   if(i==0 && !bytes.Slice(offset+12,8).SequenceEqual("QCC512Xx"u8))return AppText.Get("Device.Message.49");
   if(i>0 && i<8 && BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(offset+14,2))!=partitions[i-1])return AppText.Format("Firmware.PartitionOrder",i+1);
   offset+=12+(int)length;
  }
  return offset==bytes.Length?null:AppText.Get("Device.Message.50");
 }
}
