using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Middleton.Desktop;
/// <summary>
/// Owns one device upgrade for this user's local processes. Lock by device (not
/// firmware), so two images cannot be installed concurrently on the same speaker.
/// Hashed filenames avoid interpreting a peripheral-provided serial as a path.
/// Local records are recovery evidence, not authenticated storage against the user.
/// </summary>
public sealed class UpgradeJournalStore : IDisposable
{
 readonly string path;
 readonly FileStream ownership;
 const int MaximumJournalBytes=64*1024;
 public UpgradeJournalStore(string serial,string sha):this(serial,sha,
  Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"MiddletonDesktop","upgrades")){}
 internal UpgradeJournalStore(string serial,string sha,string folder)
 {
  string key=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(serial+":"+sha)));
  Directory.CreateDirectory(folder);path=Path.Combine(folder,key+".json");
  string deviceKey=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(serial)));
  ownership=new FileStream(Path.Combine(folder,deviceKey+".lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
 }
 public UpgradeJournal Load(string serial,string sha)
 {
  if(!File.Exists(path))return new(){Serial=serial,Sha256=sha};
  // Open one stable snapshot; reject oversized/corrupt local records before allocation/parsing.
  using var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
  if(file.Length is <=0 or >MaximumJournalBytes)throw new InvalidDataException(AppText.Get("Device.Message.17"));
  UpgradeJournal journal;
  try{journal=JsonSerializer.Deserialize<UpgradeJournal>(file)??throw new InvalidDataException(AppText.Get("Device.Message.18"));}
  catch(JsonException e){throw new InvalidDataException(AppText.Get("Device.Message.19"),e);}
  if(journal.Serial!=serial || journal.Sha256!=sha)throw new InvalidDataException(AppText.Get("Device.Message.20"));
  return journal.Finished?new(){Serial=serial,Sha256=sha}:journal;
 }
 public void Save(UpgradeJournal journal)
 {
  string temporary=path+".tmp";
  using(var file=new FileStream(temporary,FileMode.Create,FileAccess.Write,FileShare.None))
  {JsonSerializer.Serialize(file,journal);file.Flush(true);}
  File.Move(temporary,path,true);
 }
 public void Dispose()=>ownership.Dispose();
}
