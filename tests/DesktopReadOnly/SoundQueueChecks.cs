using Middleton.Desktop;
static class SoundQueueChecks
{
 public static async Task Run()
 {
  static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
  var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
  var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
  var seen=new List<SoundWriteRequest>();var session=Guid.NewGuid();
  await using var queue=new SoundWriteQueue(async(r,ct)=>{seen.Add(r);if(seen.Count==1){entered.SetResult();await release.Task.WaitAsync(ct);}});
  var first=queue.EnqueueAsync(new(session,SoundWriteKind.Volume,10,null,null),default);await entered.Task;
  var a=queue.EnqueueAsync(new(session,SoundWriteKind.Volume,11,null,null),default);
  var b=queue.EnqueueAsync(new(session,SoundWriteKind.Volume,12,null,null),default);
  using var cancelled=new CancellationTokenSource();
  var c=queue.EnqueueAsync(new(session,SoundWriteKind.Volume,32,null,null),cancelled.Token);cancelled.Cancel();
  var bass=queue.EnqueueAsync(new(session,SoundWriteKind.Eq,null,3,null),default);
  var treble=queue.EnqueueAsync(new(session,SoundWriteKind.Eq,null,null,7),default);
  release.SetResult();await Task.WhenAll(first,a,b,bass,treble);
  try{await c;throw new Exception("Cancelled request completed");}catch(OperationCanceledException){}
  Check(seen.Count==3 && seen[1].Volume==12,"Queued volume must coalesce and exclude cancelled target");
  Check(seen[2].Bass==3 && seen[2].Treble==7,"EQ coalescing must preserve both requested bands");
  var active=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);int executions=0;
  var closing=new SoundWriteQueue(async(r,ct)=>{executions++;active.SetResult();await Task.Delay(Timeout.Infinite,ct);});
  var inFlight=closing.EnqueueAsync(new(session,SoundWriteKind.Volume,8,null,null),default);await active.Task;
  var waiting=closing.EnqueueAsync(new(session,SoundWriteKind.Volume,9,null,null),default);
  await closing.DisposeAsync();
  try{await inFlight;throw new Exception("In-flight shutdown ignored");}catch(OperationCanceledException){}
  try{await waiting;throw new Exception("Queued shutdown ignored");}catch(OperationCanceledException){}
  Check(executions==1,"Shutdown must not dispatch queued writes");
  var dispatches=new List<(int? Value,long Time)>();
  await using var throttled=new SoundWriteQueue((r,ct)=>{dispatches.Add((r.Volume,Environment.TickCount64));return Task.CompletedTask;},100);
  await throttled.EnqueueAsync(new(session,SoundWriteKind.Volume,1,null,null),default);
  var second=throttled.EnqueueAsync(new(session,SoundWriteKind.Volume,2,null,null),default);
  var latest=throttled.EnqueueAsync(new(session,SoundWriteKind.Volume,3,null,null),default);
  await Task.WhenAll(second,latest);
  Check(dispatches.Count==2 && dispatches[1].Value==3,"Throttle must send first and latest targets");
  Check(dispatches[1].Time-dispatches[0].Time>=90,"Throttle must bound consecutive dispatch frequency");
  Console.WriteLine("PASS: serial queue, coalescing, cancelled target, independent EQ bands");
 }
}
