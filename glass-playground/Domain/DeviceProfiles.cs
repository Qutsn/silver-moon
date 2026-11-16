using System;
using System.Linq;
namespace Middleton.Desktop;
public sealed record DeviceIdentification(bool IsMarshall,bool Supported,string? ProfileId,string Message);
public sealed record SpeakerProfile(string Id,string Model,Guid[] RequiredCharacteristics);
// Model and characteristic checks establish protocol compatibility, not cryptographic
// manufacturer authenticity. A malicious peripheral can spoof these public values.
// Do not use the user-editable Bluetooth name as an installation authorization gate.
public static class DeviceProfiles
{
 static readonly SpeakerProfile[] Profiles={new("middleton","MIDDLETON",new[]{DeviceIds.GaiaWrite,DeviceIds.GaiaResponse})};
 public static DeviceIdentification Identify(string? model,string? manufacturer,Func<Guid,bool> has)
 {
  var profile=Profiles.FirstOrDefault(p=>string.Equals(p.Model,model?.Trim(),StringComparison.OrdinalIgnoreCase));
  if(profile is not null && profile.RequiredCharacteristics.All(has))return new(true,true,profile.Id,"");
  bool brand=string.Equals(manufacturer?.Trim(),"Marshall",StringComparison.OrdinalIgnoreCase) || string.Equals(manufacturer?.Trim(),"Zound Industries",StringComparison.OrdinalIgnoreCase);
  if(brand)return new(true,false,null,AppText.Format("Device.Unsupported",model??AppText.Get("Device.Message.47")));
  return new(false,false,null,AppText.Get("Device.Message.48"));
 }
}
