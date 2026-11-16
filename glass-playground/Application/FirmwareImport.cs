using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Middleton.Desktop;

/// <summary>
/// Reads an untrusted file into a bounded snapshot before classification. Apply the
/// limit while reading: a file picker stream need not be seekable or report its size.
/// Structure alone never authorizes installation; FirmwareCatalog selects strategies.
/// </summary>
public static class FirmwareImport
{
 public const int MaximumLength=16*1024*1024;
 public static async Task<FirmwareAnalysis> AnalyzeAsync(Stream input,CancellationToken cancellationToken)
 {
  cancellationToken.ThrowIfCancellationRequested();
  using var data=new MemoryStream();
  byte[] buffer=new byte[64*1024];
  while(true)
  {
   int read=await input.ReadAsync(buffer.AsMemory(),cancellationToken).ConfigureAwait(false);
   if(read==0)break;
   if(data.Length+read>MaximumLength)throw new InvalidDataException(AppText.Get("Device.Message.1"));
   data.Write(buffer,0,read);
  }
  if(data.Length==0)throw new InvalidDataException(AppText.Get("Device.Message.2"));
  cancellationToken.ThrowIfCancellationRequested();
  return FirmwareCatalog.Analyze(data.GetBuffer().AsSpan(0,(int)data.Length));
 }
}
