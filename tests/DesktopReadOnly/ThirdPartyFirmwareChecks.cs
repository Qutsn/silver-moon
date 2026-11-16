using Middleton.Desktop;

static class ThirdPartyFirmwareChecks
{
 static void Check(bool ok,string reason){if(!ok)throw new Exception(reason);}
 public static async Task Run()
 {
  Check(FirmwareCatalog.Analyze(new byte[64]).Installable is null,"Short input must not reach installer");
  if(TestFirmware.SkipMaterialChecks)return;
  byte[] baseline=await File.ReadAllBytesAsync(TestFirmware.R2);
  Check(ThirdPartyFirmware.Check(baseline) is null,"Verified r2 must satisfy fixed-region hashes");
  foreach(int offset in new[]{ThirdPartyFirmware.PayloadStart,2574543,2574544,ThirdPartyFirmware.PayloadEnd-1})
  {
   var custom=(byte[])baseline.Clone();custom[offset]^=1;
   var analysis=FirmwareCatalog.Analyze(custom);
   Check(analysis.Known is null && analysis.ThirdParty is not null && analysis.CanInstallOn("middleton"),"MCU-only edit should admit distinct third-party identity");
   Check(analysis.Installable!.UpgradeId!=FirmwareCatalog.Analyze(baseline).Known!.UpgradeId,"Never reuse r2 image ID");
   Check(!analysis.CanInstallOn("acton"),"Third-party profile must not widen model support");
  }
  foreach(int offset in new[]{0,8,60,74,ThirdPartyFirmware.PayloadStart-1,ThirdPartyFirmware.PayloadEnd,3266040,baseline.Length-1})
  {
   var rejected=(byte[])baseline.Clone();rejected[offset]^=1;
   var analysis=FirmwareCatalog.Analyze(rejected);
   Check(analysis.Installable is null && analysis.InstallationError is not null,"Protected region mutation must explain rejection");
   try{_ =new ResumableUpgrade(rejected,null!,new(),_=>{},_=>{});throw new Exception("Rejected file entered coordinator");}catch(UpgradeProtocolException){}
  }
  var state=new byte[38];state[0]=0x80;state[1]=3;state[3]=5;state[5]=2;state[7]=1;
  state[15]=5;state[17]=2;state[29]=10;
  state[22]=0x56;state[23]=0x78;state[24]=0x12;state[25]=0x34;
  var context=UpgradeContext.Decode(state);
  Check(context.StagedPartitionsClosed("12345678"),"Use file-selected image identity");
  try{context.PrepareR2Validation("6e0b0188");throw new Exception("Wrong image accepted");}catch(UpgradeProtocolException){}
  Check(context.PrepareR2Validation("12345678")[29]==1,"Matching custom image may prepare resume");
  Console.WriteLine("PASS: third-party MCU profile, protected boundaries, separate identity and fail-closed installer");
 }
}
