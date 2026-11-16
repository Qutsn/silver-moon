using Middleton.Desktop;
static class SoundStepperChecks
{
 public static void Run()
 {
  static void Check(bool ok,string text){if(!ok)throw new Exception(text);}
  var state=new SoundStepperState(32);state.Observe(0,true);
  Check(!state.CanDecrease && state.CanIncrease,"Readback zero must remain visible with only plus available");
  var a=state.Request(1)!.Value;var b=state.Request(1)!.Value;
  Check(state.Confirmed==0 && b.Target==2,"Clicks accumulate without presenting targets as readbacks");
  state.Observe(1,true);Check(state.Target==2,"Intermediate readback must not erase latest target");
  Check(!state.Complete(a.Revision) && state.Target==2,"Old completion must not clear latest target");
  state.Observe(2,true);Check(state.Complete(b.Revision) && state.Target is null,"Final completion clears pending");
  state.Observe(17,true);Check(state.Confirmed==17 && state.Target is null,"External notification updates confirmed value only");
  var old=state.Request(1)!.Value;state.Observe(null,false);
  Check(!state.Complete(old.Revision) && state.Request(1) is null,"Disconnect discards targets and old completions");
  var volume=new SoundStepperState(32,0);volume.Observe(1,true);
  Check(volume.CanDecrease && volume.Request(-1)?.Target==0,"Volume must decrease from one to zero");
  Check(!volume.CanDecrease && volume.Request(-1) is null,"Volume must stop at zero");
  var eq=new SoundStepperState(10);eq.Observe(1,true);
  Check(!eq.CanDecrease && eq.Request(-1) is null,"EQ adjustment minimum remains one");
  Console.WriteLine("PASS: UI target isolation, zero, external updates, stale completions");
 }
}
