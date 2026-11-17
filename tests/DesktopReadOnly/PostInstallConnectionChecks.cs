using Middleton.Desktop;
static class PostInstallConnectionChecks
{
 public static async Task Run()
 {
  var ready=new DeviceSnapshot(Guid.NewGuid(),true,"test","Middleton","serial","17.0.37","3.0.2",90,0,0,8,new(true,true,true,true));
  int attempts=0,delays=0;
  var result=await PostInstallConnection.RestoreAsync(ready,_=>{
   attempts++;
   if(attempts==1)throw new IOException("Transient GATT failure");
   return Task.FromResult(attempts==2?ready with{Bass=null,Treble=null}:ready);
  },_=>{delays++;return Task.CompletedTask;},default);
  if(result!=ready || attempts!=3 || delays!=2)throw new Exception("Transient recovery or valid zero handling failed");
  foreach(var invalid in new[]{ready with{Connected=false},ready with{Serial="other"},ready with{Model="other"},ready with{Volume=null}})
  {
   attempts=0;
   try {await PostInstallConnection.RestoreAsync(ready,_=>{attempts++;return Task.FromResult(invalid);},_=>Task.CompletedTask,default);throw new Exception("Accepted invalid connection");}
   catch(DeviceConnectionException){if(attempts!=3)throw new Exception("Retry budget incorrect");}
  }
  using var cancelled=new CancellationTokenSource();cancelled.Cancel();
  try {await PostInstallConnection.RestoreAsync(ready,_=>throw new Exception("Connected after cancellation"),_=>Task.CompletedTask,cancelled.Token);throw new Exception("Ignored cancellation");}
  catch(OperationCanceledException){}
  Console.WriteLine("Post-install control connection checks passed.");
 }
}
