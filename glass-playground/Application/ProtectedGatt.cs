using System;
using System.Threading.Tasks;
namespace Middleton.Desktop;

// Shared by reads, writes and CCCD subscriptions. Permission/encryption errors are never optional data.
public static class ProtectedGatt
{
 public static bool NeedsAuthentication(string status,byte? error)=>status=="AccessDenied" || status=="ProtocolError" && error is 5 or 15;
 public static async Task<T> RunAsync<T>(Func<Task<T>> operation,Func<Task<bool>> authenticate,Func<T,string> status,Func<T,byte?> error)
 {
  var result=await operation();
  if(!NeedsAuthentication(status(result),error(result)))return result;
  if(await authenticate())result=await operation();
  if(NeedsAuthentication(status(result),error(result)))
   throw new DeviceConnectionException(AppText.Format("Bluetooth.Protected",status(result),error(result)?.ToString()??AppText.Get("Device.None")));
  return result;
 }
}
