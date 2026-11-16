using System;
using System.Collections.Generic;
namespace Middleton.Desktop;
public sealed class TransferRate
{
 readonly Queue<(double Time,long Bytes)> samples=new();
 public void Reset()=>samples.Clear();
 public double? Observe(double seconds,long acknowledged,bool transferring)
 {
  if(!transferring){Reset();return null;}
  if(samples.Count>0 && acknowledged<samples.Peek().Bytes)Reset();
  samples.Enqueue((seconds,acknowledged));
  while(samples.Count>2 && seconds-samples.Peek().Time>5)samples.Dequeue();
  var first=samples.Peek();double elapsed=seconds-first.Time;
  return elapsed<1?null:(acknowledged-first.Bytes)/elapsed;
 }
}
