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
 }
 static void Reject(Action read)
 {
  try{read();throw new Exception("Invalid journal accepted");}catch(InvalidDataException){}
 }
}
