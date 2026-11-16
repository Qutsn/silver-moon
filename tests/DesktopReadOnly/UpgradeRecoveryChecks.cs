using Middleton.Desktop;

static class UpgradeRecoveryChecks
{
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 public static async Task Run()
 {
  var rate=new TransferRate();
  Check(rate.Observe(0,0,true) is null,"Insufficient speed sample");
  Check(rate.Observe(2,2048,true)==1024,"Speed uses acknowledged bytes");
  Check(rate.Observe(20,2048,false) is null,"Disconnect resets rate");
  Check(rate.Observe(30,2048,true) is null && rate.Observe(32,4096,true)==1024,"Reconnect excludes wait time");
  byte[] value=new byte[38];value[0]=0x80;value[1]=3;value[3]=5;value[5]=2;value[7]=1;
  var idle=UpgradeContext.Decode(value);
  Check(idle.IsIdle,"PS4 idle decoding");
  value[15]=5;value[17]=2;value[22]=1;value[23]=0x88;value[24]=0x6e;value[25]=0x0b;value[29]=10;
  var staged=UpgradeContext.Decode(value);
  Check(staged.ImageId=="6e0b0188" && !staged.IsIdle,"PS4 word ordering");
  var prepared=staged.PrepareR2Validation();
  Check(prepared.Length==78 && prepared[28]==0 && prepared[29]==1,"PS4 padded write offset");
  Check(prepared.AsSpan(2,26).SequenceEqual(value.AsSpan(0,26)) && prepared.AsSpan(30,10).SequenceEqual(value.AsSpan(28,10)),"Preserve every unrelated PS4 byte");
  var mailbox=new UpgradeMailbox();
  var ack=mailbox.Expect(0x0642);
  mailbox.Accept(Convert.FromHexString("000A400312120000"));
  Check(!ack.IsCompleted,"Upgrade message is not GAIA ACK");
  mailbox.Accept(Convert.FromHexString("000A864200"));
  Check((await ack).Status==0 && (await mailbox.ReceiveAsync(default)).Code==18,"Notification retained during ACK wait");
  var pending=mailbox.Expect(0x0642);mailbox.Break(new UpgradeLinkLostException("test loss"));
  try{await pending;throw new Exception("Disconnect did not fail ACK");}catch(UpgradeLinkLostException){}
  try{_ = mailbox.Expect(0x0642);throw new Exception("Failed mailbox reused");}catch(UpgradeLinkLostException){}
  Console.WriteLine("PASS: PS4 preservation, endian decoding and ACK/notification isolation");
 }
}
