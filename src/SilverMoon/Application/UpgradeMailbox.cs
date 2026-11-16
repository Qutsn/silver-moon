using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Middleton.Desktop;
public sealed class UpgradeLinkLostException(string message) : Exception(message);

// An instance belongs to exactly one GATT connection. Never reuse it after loss/timeout.
public sealed class UpgradeMailbox
{
 readonly object gate=new();
 // Keep ACK matching separate from asynchronous upgrade events. Never drop an
 // event to make room: losing one can incorrectly advance the install state machine.
 readonly Channel<UpgradeMessage> notifications=Channel.CreateBounded<UpgradeMessage>(256);
 TaskCompletionSource<GaiaAcknowledgement>? pending;
 ushort expected;
 Exception? broken;
 public Task<GaiaAcknowledgement> Expect(ushort command)
 {
  lock(gate)
  {
   if(broken is not null)throw new UpgradeLinkLostException(broken.Message);
   if(pending is not null)throw new InvalidOperationException("Only one GAIA command may be pending.");
   expected=command;pending=new(TaskCreationOptions.RunContinuationsAsynchronously);return pending.Task;
  }
 }
 public void Accept(byte[] bytes)
 {
  lock(gate)
  {
   if(broken is not null)return;
   try
   {
    switch(UpgradeWire.Parse(bytes))
    {
     case GaiaAcknowledgement ack:
      if(pending is null || ack.Command!=expected)throw new UpgradeProtocolException(AppText.Get("Device.Message.29"));
      var completion=pending;pending=null;completion.TrySetResult(ack);break;
     case UpgradeNotification notification:
      if(!notifications.Writer.TryWrite(notification.Message))throw new UpgradeProtocolException(AppText.Get("Device.Message.30"));break;
    }
   }
   catch(Exception e){Break(e);}
  }
 }
 public async Task<UpgradeMessage> ReceiveAsync(CancellationToken ct)
 {
  try{return await notifications.Reader.ReadAsync(ct).ConfigureAwait(false);}
  catch(ChannelClosedException e){throw e.InnerException??new UpgradeLinkLostException(AppText.Get("Device.Message.31"));}
 }
 public void Break(Exception reason)
 {
  lock(gate){broken??=reason;pending?.TrySetException(reason);pending=null;notifications.Writer.TryComplete(reason);}
 }
}
