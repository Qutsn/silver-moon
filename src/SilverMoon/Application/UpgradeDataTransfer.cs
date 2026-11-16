using System;
using System.Threading;
using System.Threading.Tasks;

namespace Middleton.Desktop;

public sealed record UpgradeTransferProgress(long Cursor,long SentBytes,long AcknowledgedBytes,long SkippedBytes);

// One instance per transfer operation. After uncertain delivery, create a new instance only after device resynchronization.
public sealed class UpgradeDataTransfer
{
 readonly byte[] image;
 int busy;
 bool faulted;
 UpgradeTransferProgress progress=new(0,0,0,0);
 public UpgradeTransferProgress Progress=>Volatile.Read(ref progress);
 public UpgradeDataTransfer(ReadOnlySpan<byte> firmware)
 {
  if(firmware.Length==0)throw new ArgumentException("Empty firmware.",nameof(firmware));
  image=firmware.ToArray();
 }
 // The delegate completes only after the matching GAIA 0x8642 ACK, not the ATT write response.
 public async Task SendRequestedAsync(uint count,uint skip,
  Func<ReadOnlyMemory<byte>,CancellationToken,Task<byte>> sendAndAcknowledge,CancellationToken cancellationToken)
 {
  if(Interlocked.CompareExchange(ref busy,1,0)!=0)throw new UpgradeProtocolException("Another DATA request is active.");
  try
  {
   if(faulted)throw new UpgradeProtocolException("Transfer requires device resynchronization.");
   cancellationToken.ThrowIfCancellationRequested();
   long start=progress.Cursor+skip;
   if(count==0 || start>image.Length || count>image.Length-start)throw new UpgradeProtocolException("DATA request exceeds firmware bounds or is empty.");
   progress=progress with{Cursor=start,SkippedBytes=progress.SkippedBytes+skip};
   long end=start+count;
   while(progress.Cursor<end)
   {
    cancellationToken.ThrowIfCancellationRequested();
    int size=(int)Math.Min(45,end-progress.Cursor);
    var payload=new byte[size+1];payload[0]=(byte)(progress.Cursor+size==image.Length?1:0);
    Array.Copy(image,(int)progress.Cursor,payload,1,size);
    progress=progress with{SentBytes=progress.SentBytes+size};
    byte status=await sendAndAcknowledge(UpgradeWire.Control(4,payload),cancellationToken).ConfigureAwait(false);
    if(status!=0)throw new UpgradeProtocolException($"DATA rejected with GAIA status 0x{status:X2}.");
    progress=progress with{Cursor=progress.Cursor+size,AcknowledgedBytes=progress.AcknowledgedBytes+size};
    if(progress.Cursor<end)await Task.Delay(2,cancellationToken).ConfigureAwait(false);
   }
  }
  catch {faulted=true;throw;}
  finally{Volatile.Write(ref busy,0);}
 }
}
