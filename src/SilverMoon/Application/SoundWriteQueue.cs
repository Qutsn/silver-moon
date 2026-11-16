using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Middleton.Desktop;

public enum SoundWriteKind { Volume, Eq }
public sealed record SoundWriteRequest(Guid SessionId,SoundWriteKind Kind,int? Volume,int? Bass,int? Treble);

// One worker; only requests not yet started are merged. Cancelled requests never contribute values.
public sealed class SoundWriteQueue : IAsyncDisposable
{
 readonly object gate=new();
 readonly List<Entry> pending=new();
 readonly Func<SoundWriteRequest,CancellationToken,Task> execute;
 readonly CancellationTokenSource shutdown=new();
 Task worker=Task.CompletedTask;
 bool running,disposed;
 readonly int minimumIntervalMs;
 long lastDispatch;
 public SoundWriteQueue(Func<SoundWriteRequest,CancellationToken,Task> execute,int minimumIntervalMs=0){this.execute=execute;this.minimumIntervalMs=minimumIntervalMs;}
 public async Task EnqueueAsync(SoundWriteRequest request,CancellationToken ct)
 {
  ct.ThrowIfCancellationRequested();
  var entry=new Entry(request,ct);
  using var registration=ct.Register(()=>entry.Completion.TrySetCanceled(ct));
  lock(gate)
  {
   ObjectDisposedException.ThrowIf(disposed,this);
   pending.Add(entry);
   if(!running){running=true;worker=Task.Run(RunAsync);}
  }
  await entry.Completion.Task;
 }
 async Task RunAsync()
 {
  while(true)
  {
   // First dispatch is immediate. Merge the latest pending targets after the window.
   int delay=(int)Math.Max(0,minimumIntervalMs-(Environment.TickCount64-lastDispatch));
   if(lastDispatch!=0 && delay>0)await Task.Delay(delay);
   List<Entry> batch=new();SoundWriteRequest request;
   lock(gate)
   {
    pending.RemoveAll(e=>e.Token.IsCancellationRequested);
    if(pending.Count==0){running=false;return;}
    request=pending[0].Request;
    // Merge the contiguous batch, retaining the latest explicit value of each EQ band.
    while(pending.Count>0 && pending[0].Request.SessionId==request.SessionId && pending[0].Request.Kind==request.Kind)
    {
     var entry=pending[0];pending.RemoveAt(0);batch.Add(entry);
     request=request with {Volume=entry.Request.Volume??request.Volume,Bass=entry.Request.Bass??request.Bass,Treble=entry.Request.Treble??request.Treble};
    }
   }
   using var timeout=CancellationTokenSource.CreateLinkedTokenSource(shutdown.Token);
   timeout.CancelAfter(TimeSpan.FromSeconds(15));
   lastDispatch=Environment.TickCount64;
   try {await execute(request,timeout.Token);foreach(var e in batch)e.Completion.TrySetResult();}
   catch(Exception error){foreach(var e in batch)e.Completion.TrySetException(error);}
  }
 }
 public async ValueTask DisposeAsync()
 {
  Task active;
  lock(gate){disposed=true;foreach(var e in pending)e.Completion.TrySetCanceled();pending.Clear();active=worker;}
  shutdown.Cancel();await active;
 }
 sealed record Entry(SoundWriteRequest Request,CancellationToken Token)
 {public TaskCompletionSource Completion {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);}
}
