using System;
using System.Text;

namespace Middleton.Desktop;

public static class DeviceIds
{
 public static Guid Standard(string id) => Guid.Parse($"0000{id}-0000-1000-8000-00805f9b34fb");
 public static readonly Guid GaiaService = Guid.Parse("00001100-d102-11e1-9b23-00025b00a5a5");
 public static readonly Guid GaiaWrite = Guid.Parse("00001101-d102-11e1-9b23-00025b00a5a5");
 public static readonly Guid GaiaResponse = Guid.Parse("00001102-d102-11e1-9b23-00025b00a5a5");
 public static readonly Guid GaiaData = Guid.Parse("00001103-d102-11e1-9b23-00025b00a5a5");
 public static readonly Guid Manufacturer=Standard("2a29"), Model=Standard("2a24"), Serial=Standard("2a25"), Firmware=Standard("2a26"), Hardware=Standard("2a27"), Battery=Standard("2a19"), Volume=Standard("aa08"), Eq=Standard("aa16");
}
public static class DeviceValues
{
 public static string? Text(byte[]? bytes) => bytes is {Length: >0} ? Encoding.UTF8.GetString(bytes).TrimEnd('\0').Trim() : null;
 public static int? Volume(byte[]? bytes) => Scalar(bytes,32);
 public static int? Battery(byte[]? bytes) => Scalar(bytes,100);
 static int? Scalar(byte[]? bytes,int max) => bytes is {Length:1} && bytes[0]<=max ? bytes[0] : null;
 public static (int Bass,int Treble)? Eq(byte[]? bytes) => bytes is {Length:8} && bytes[0]<=10 && bytes[4]<=10 && bytes[1]==255 && bytes[2]==255 && bytes[3]==255 && bytes[5]==0 && bytes[6]==0 && bytes[7]==0 ? (bytes[0],bytes[4]) : null;
}
public sealed record DeviceCandidate(string Id,string Name,string DisplayLabel,string AddressType="Unspecified");
public sealed record DeviceCapabilities(bool Battery,bool Volume,bool Eq,bool UpgradeEndpoint);
public sealed record DeviceSnapshot(Guid SessionId,bool Connected,string? Name,string Model,string? Serial,string? Firmware,string? Hardware,int? Battery,int? Volume,int? Bass,int? Treble,DeviceCapabilities Capabilities);
public enum InstallationState { Unavailable, Transferring, Validating, Rebooting, Committing, ReadingBack, ResultUnknown, Complete, Failed }
public sealed record InstallationEvidence(bool TransferComplete,bool TrialVersionMatches,bool CompleteIndication,bool IndependentIdleReadback)
{
 public bool IsSuccessful => TransferComplete && TrialVersionMatches && CompleteIndication && IndependentIdleReadback;
}
public sealed class DeviceConnectionException : Exception
{
 public DeviceConnectionException(string message) : base(message) {}
}
