using System;
using System.Threading;
using System.Threading.Tasks;

namespace Middleton.Desktop;

// Daily-control readiness is independent of installation evidence. This policy
// never sends upgrade commands or changes the completed installation journal.
public static class PostInstallConnection
{
 public static bool Ready(DeviceSnapshot? snapshot,DeviceSnapshot expected) =>
  snapshot is {Connected:true,Volume:not null,Bass:not null,Treble:not null}
  && snapshot.Capabilities.Volume && snapshot.Capabilities.Eq
  && !string.IsNullOrWhiteSpace(snapshot.Serial)
  && snapshot.Serial==expected.Serial && snapshot.Model==expected.Model;

 public static async Task<DeviceSnapshot> RestoreAsync(DeviceSnapshot expected,
  Func<CancellationToken,Task<DeviceSnapshot>> connect,
  Func<CancellationToken,Task> delay,CancellationToken cancellationToken)
 {
  Exception? last=null;
  for(int attempt=0;attempt<3;attempt++)
  {
   cancellationToken.ThrowIfCancellationRequested();
   try
   {
    var snapshot=await connect(cancellationToken);
    if(Ready(snapshot,expected))return snapshot;
    last=new DeviceConnectionException("Daily-control data is not ready for the installed device.");
   }
   catch(Exception e) when(!cancellationToken.IsCancellationRequested){last=e;}
   if(attempt<2)await delay(cancellationToken);
  }
  throw last!;
 }
}
