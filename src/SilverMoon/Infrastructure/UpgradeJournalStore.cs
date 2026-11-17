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
 // Called only after a fresh, identity-checked idle read. A later completed
 // official rollback can supersede an old r2 final-readback record. Version
 // mismatch alone is never enough to discard recovery evidence.
 public bool ArchiveSuperseded(UpgradeJournal journal,UpgradeIdentity identity)
 {
  const string officialSha="db74c3468bd08d61f3a4e4e4e9e6eea9b19d95d6e852b9fd5e9cbac3e91c9472";
  if(identity.Model!="MIDDLETON" || identity.Serial!=journal.Serial || identity.Firmware!="17.0.37"
     || journal.Sha256==officialSha || !journal.CompleteInd || !journal.FinalRestartObserved || !File.Exists(path))return false;
  string key=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(journal.Serial+":"+officialSha)));
  string other=Path.Combine(Path.GetDirectoryName(path)!,key+".json");
  // File timestamps are not chronology: earlier UI versions rewrote the old
  // journal on every retry. Require current official identity plus its complete
  // installation evidence instead, while the caller requires fresh idle state.
  if(!File.Exists(other))return false;
  using var file=new FileStream(other,FileMode.Open,FileAccess.Read,FileShare.Read);
  if(file.Length is <=0 or >MaximumJournalBytes)return false;
  UpgradeJournal? completed;
  try{completed=JsonSerializer.Deserialize<UpgradeJournal>(file);}catch(JsonException){return false;}
  if(completed is not {Finished:true,CompleteInd:true,FinalRestartObserved:true}
     || completed.Serial!=journal.Serial || completed.Sha256!=officialSha)return false;
  // Preserve the exact old evidence before the coordinator starts a new operation.
  File.Copy(path,path+".superseded-"+Guid.NewGuid().ToString("N"),false);
  return true;
 }
 public void Dispose()=>ownership.Dispose();
}
