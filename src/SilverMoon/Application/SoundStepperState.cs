using System;
namespace Middleton.Desktop;

// UI-thread state. Device readbacks are separate from unconfirmed click targets.
public sealed class SoundStepperState
{
 public SoundStepperState(int maximum,int minimum=1){Maximum=maximum;Minimum=minimum;}
 public int Minimum {get;}
 public int Maximum {get;}
 public int? Confirmed {get;private set;}
 public int? Target {get;private set;}
 public bool Enabled {get;private set;}
 public long Revision {get;private set;}
 public bool CanDecrease=>Enabled && (Target??Confirmed)>Minimum;
 public bool CanIncrease=>Enabled && (Target??Confirmed)<Maximum;
 public void Observe(int? raw,bool enabled)
 {
  Confirmed=raw is >=0 && raw<=Maximum?raw:null;
  Enabled=enabled && Confirmed.HasValue;
  if(!Enabled)ResetPending();
 }
 public (long Revision,int Target)? Request(int delta)
 {
  if(!Enabled || Confirmed is null)return null;
  var start=Target??Confirmed.Value;
  var target=Math.Clamp(start+delta,Minimum,Maximum);
  if(target==start || (start==0 && delta<0))return null;
  Target=target;return (++Revision,target);
 }
 // An older async write must not clear the target from a newer click or session.
 // Confirmed is updated only by Observe; completing a write is not a device readback.
 public bool Complete(long revision)
 {
  if(revision!=Revision)return false;
  Target=null;return true;
 }
 public void ResetPending(){Revision++;Target=null;}
}
public enum SoundSetting { Volume, Bass, Treble }
