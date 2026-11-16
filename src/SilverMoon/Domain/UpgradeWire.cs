using System;
using System.Buffers.Binary;

namespace Middleton.Desktop;

public sealed class UpgradeProtocolException(string message) : Exception(message);
public abstract record GaiaIncoming;
public sealed record GaiaAcknowledgement(ushort Command,byte Status,ReadOnlyMemory<byte> Data) : GaiaIncoming;
public sealed record UpgradeMessage(byte Code,ReadOnlyMemory<byte> Body);
public sealed record UpgradeNotification(UpgradeMessage Message) : GaiaIncoming;
public sealed record OtherGaiaNotification(byte Event,ReadOnlyMemory<byte> Data) : GaiaIncoming;

// BLE GAIA framing only; a GATT write response is never an instance of GaiaAcknowledgement.
public static class UpgradeWire
{
 public static byte[] Command(ushort command,ReadOnlySpan<byte> payload)
 {
  var frame=new byte[checked(4+payload.Length)];
  BinaryPrimitives.WriteUInt16BigEndian(frame,10);
  BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(2),command);
  payload.CopyTo(frame.AsSpan(4));return frame;
 }
 public static byte[] Control(byte code,ReadOnlySpan<byte> body)
 {
  if(body.Length>ushort.MaxValue)throw new UpgradeProtocolException("Upgrade body exceeds 16-bit length.");
  var message=new byte[3+body.Length];message[0]=code;
  BinaryPrimitives.WriteUInt16BigEndian(message.AsSpan(1),(ushort)body.Length);
  body.CopyTo(message.AsSpan(3));return Command(0x0642,message);
 }
 public static GaiaIncoming Parse(ReadOnlySpan<byte> raw)
 {
  if(raw.Length<4 || BinaryPrimitives.ReadUInt16BigEndian(raw)!=10)throw new UpgradeProtocolException("Invalid GAIA header/vendor.");
  ushort command=BinaryPrimitives.ReadUInt16BigEndian(raw.Slice(2));
  if((command&0x8000)!=0)
  {
   if(raw.Length<5)throw new UpgradeProtocolException("Missing GAIA acknowledgement status.");
   return new GaiaAcknowledgement((ushort)(command&0x7fff),raw[4],raw.Slice(5).ToArray());
  }
  if(command!=0x4003 || raw.Length<5)throw new UpgradeProtocolException("Unexpected GAIA command.");
  if(raw[4]!=18)return new OtherGaiaNotification(raw[4],raw.Slice(5).ToArray());
  if(raw.Length<8 || BinaryPrimitives.ReadUInt16BigEndian(raw.Slice(6))!=raw.Length-8)
   throw new UpgradeProtocolException("Upgrade notification length mismatch.");
  return new UpgradeNotification(new UpgradeMessage(raw[5],raw.Slice(8).ToArray()));
 }
 public static (uint Count,uint Skip) DataRequest(UpgradeMessage message)
 {
  Require(message,3,8);
  return (BinaryPrimitives.ReadUInt32BigEndian(message.Body.Span),BinaryPrimitives.ReadUInt32BigEndian(message.Body.Span.Slice(4)));
 }
 public static TimeSpan ValidationDelay(UpgradeMessage message)
 {
  Require(message,23,2);return TimeSpan.FromMilliseconds(BinaryPrimitives.ReadUInt16BigEndian(message.Body.Span));
 }
 public static void Require(UpgradeMessage message,byte code,int length)
 {
  if(message.Code!=code || message.Body.Length!=length)throw new UpgradeProtocolException($"Expected upgrade {code} with {length} bytes.");
 }
}
