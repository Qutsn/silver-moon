using System;
using System.Threading;
using System.Threading.Tasks;
namespace Middleton.Desktop;
public enum PairingProtection { EncryptionAndAuthentication, Encryption, Default }
public static class PairingNegotiation
{
 public static async Task<string> RunAsync(Func<PairingProtection,CancellationToken,Task<string>> pair,CancellationToken ct)
 {
  string status="Failed";
  foreach(var level in new[]{PairingProtection.EncryptionAndAuthentication,PairingProtection.Encryption,PairingProtection.Default})
  {
   ct.ThrowIfCancellationRequested();
   status=await pair(level,ct);
   // Only a protection negotiation failure permits another protection level.
   // Permission denials, rejection, timeout and cancellation must not be retried this way.
   if(status!="ProtectionLevelCouldNotBeMet")return status;
  }
  return status;
 }
}
