using Middleton.Desktop;

static class JournalStoreChecks
{
 public static void Run()
 {
  var folder=Directory.CreateTempSubdirectory("middleton-journal-check-").FullName;
  try
  {
   using var store=new UpgradeJournalStore("../speaker","hash",folder);
   store.Save(new UpgradeJournal{Serial="../speaker",Sha256="hash",ConfirmedFrontier=45});
   if(store.Load("../speaker","hash").ConfirmedFrontier!=45)throw new Exception("Journal round trip");
   try{using var duplicate=new UpgradeJournalStore("../speaker","different-firmware",folder);throw new Exception("Concurrent device owner allowed");}catch(IOException){}
   var path=Directory.GetFiles(folder,"*.json").Single();
   if(Path.GetDirectoryName(path)!=folder)throw new Exception("Serial escaped journal directory");
   File.WriteAllText(path,"{");
   Reject(()=>store.Load("../speaker","hash"));
   File.WriteAllText(path,new string(' ',65537));
   Reject(()=>store.Load("../speaker","hash"));
   if(new FileInfo(path).Length!=65537)throw new Exception("Rejected evidence was changed");
   store.Save(new UpgradeJournal{Serial="other",Sha256="hash"});
   Reject(()=>store.Load("../speaker","hash"));
  }
  finally
  {
   // Delete only the concrete files created by this test, without recursive deletion.
   foreach(var file in Directory.GetFiles(folder))File.Delete(file);
   Directory.Delete(folder);
  }
  Console.WriteLine("PASS: journal ownership, hashed paths, corruption/size/identity rejection");
  CheckSuperseded();
 }
 static void CheckSuperseded()
 {
  const string official="db74c3468bd08d61f3a4e4e4e9e6eea9b19d95d6e852b9fd5e9cbac3e91c9472";
  var folder=Directory.CreateTempSubdirectory("middleton-superseded-").FullName;
  try
  {
   using(var completed=new UpgradeJournalStore("test",official,folder))
    completed.Save(new(){Serial="test",Sha256=official,Finished=true,CompleteInd=true,FinalRestartObserved=true});
   using var store=new UpgradeJournalStore("test","r2",folder);
   var journal=new UpgradeJournal{Serial="test",Sha256="r2",CompleteInd=true,FinalRestartObserved=true};store.Save(journal);
   var identity=new UpgradeIdentity("MIDDLETON","test","17.0.37","3.0.2");
   if(store.ArchiveSuperseded(journal,identity with{Serial="other"}) || store.ArchiveSuperseded(journal,identity with{Firmware="25.2.52"}))throw new Exception("Wrong identity superseded journal");
   journal.FinalRestartObserved=false;
   if(store.ArchiveSuperseded(journal,identity))throw new Exception("Active upgrade was superseded");
   journal.FinalRestartObserved=true;
   if(!store.ArchiveSuperseded(journal,identity) || Directory.GetFiles(folder,"*.superseded-*").Length!=1)throw new Exception("Completed rollback evidence did not archive old record");
   if(store.Load("test","r2").OperationId!=journal.OperationId)throw new Exception("Archival changed original evidence");
  }
  finally{foreach(var file in Directory.GetFiles(folder))File.Delete(file);Directory.Delete(folder);}
  Console.WriteLine("PASS: superseded journal requires matching rollback evidence and preserves archive");
 }
 static void Reject(Action read)
 {
  try{read();throw new Exception("Invalid journal accepted");}catch(InvalidDataException){}
 }
}
