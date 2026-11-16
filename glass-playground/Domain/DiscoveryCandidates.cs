using System;
using System.Collections.Generic;
using System.Linq;

namespace Middleton.Desktop;

// An address is a temporary route, never a physical-device identity.
public sealed class DiscoveryCandidates
{
 readonly Dictionary<(string,string),Entry> entries=new();
 public void Observe(string id,string addressType,string name,bool connectable,DateTimeOffset time)
 {
  var key=(id,addressType);
  if(!entries.TryGetValue(key,out var entry))entry=new(id,addressType,"",DateTimeOffset.MinValue,DateTimeOffset.MinValue);
  entries[key]=entry with {Name=string.IsNullOrEmpty(name)?entry.Name:name,LastSeen=time,LastConnectable=connectable?time:entry.LastConnectable};
 }
 public IReadOnlyList<DeviceCandidate> Snapshot(DateTimeOffset now) => entries.Values
  .Where(e=>now-e.LastSeen<=TimeSpan.FromSeconds(3) && now-e.LastConnectable<=TimeSpan.FromSeconds(3))
  .OrderByDescending(e=>e.Name.Contains("Middleton",StringComparison.OrdinalIgnoreCase))
  .ThenBy(e=>e.Id,StringComparer.Ordinal)
  .Select(e=>new DeviceCandidate(e.Id,e.Name,$"{(string.IsNullOrWhiteSpace(e.Name)?AppText.Get("Device.Message.40"):e.Name)} · {e.Id[^Math.Min(4,e.Id.Length)..]}",e.AddressType)).ToArray();
 sealed record Entry(string Id,string AddressType,string Name,DateTimeOffset LastSeen,DateTimeOffset LastConnectable);
}

public static class PairingOutcome
{
 public static bool Accepted(string status)=>status is "Paired" or "AlreadyPaired";
 public static string FailureMessage(string status)=>status=="AccessDenied"
  ? AppText.Get("Device.Message.51")
  : AppText.Format("Pairing.Incomplete",status);
}
