param(
    [string]$PublishedDirectory=(Join-Path $PSScriptRoot 'publish'),
    [string]$Destination=(Join-Path $PSScriptRoot '../desktop-release/current')
)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'release-integrity.ps1')
$source=(Resolve-Path -LiteralPath $PublishedDirectory).Path
$repo=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$releaseRoot=Join-Path $repo 'desktop-release'
$target=[IO.Path]::GetFullPath($Destination)
if($target -ne (Join-Path $releaseRoot 'current')){throw 'Use the fixed desktop-release/current destination.'}
if(!(Test-Path -LiteralPath (Join-Path $source 'SilverMoon.exe'))){throw 'PublishedDirectory does not contain SilverMoon.exe.'}
if(Get-Process | Where-Object {$_.Path -and $_.Path.StartsWith($target+'\',[StringComparison]::OrdinalIgnoreCase)}){throw 'Close the running release before updating.'}
# A system-hosted application has an external executable but loads this release DLL.
if(Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" | Where-Object {$_.CommandLine -and $_.CommandLine.IndexOf((Join-Path $target 'SilverMoon.dll'),[StringComparison]::OrdinalIgnoreCase) -ge 0}){throw 'Close the running hosted release before updating.'}
# Create fresh files outside the Low-labelled development checkout. Same-volume
# moves retain labels; copying security descriptors would reintroduce the failure.
$staging=Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'Middleton-release-staging'
if([IO.Path]::GetPathRoot($staging) -ne [IO.Path]::GetPathRoot($target)){throw 'Staging and release must be on the same volume to preserve integrity labels.'}
if(Test-Path -LiteralPath $staging){throw 'Previous staging directory exists; inspect it before retrying.'}
New-Item -ItemType Directory -Path $staging -Force | Out-Null
Assert-ReleaseIntegrity $staging
if(Get-ChildItem -LiteralPath $source -Recurse -Force | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }){throw 'Release source must contain ordinary files and directories, not reparse points.'}
Get-ChildItem -LiteralPath $source -Force | Copy-Item -Destination $staging -Recurse -Force
Get-ChildItem -LiteralPath $staging -Recurse -Force | ForEach-Object {Assert-ReleaseIntegrity $_.FullName}
$files=Get-ChildItem -LiteralPath $source -File -Recurse -Force
foreach($file in $files){
    $relative=[IO.Path]::GetRelativePath($source,$file.FullName)
    if((Get-FileHash -LiteralPath $file.FullName).Hash -ne (Get-FileHash -LiteralPath (Join-Path $staging $relative)).Hash){throw "Copy verification failed: $relative"}
}
# Keep one rollback copy; older copies go to the Windows Recycle Bin.
$previous=Join-Path $releaseRoot 'previous'
if(Test-Path -LiteralPath $previous){
    Add-Type -AssemblyName Microsoft.VisualBasic
    try {[Microsoft.VisualBasic.FileIO.FileSystem]::DeleteDirectory($previous,'OnlyErrorDialogs','SendToRecycleBin')}
    catch {
        if($_.Exception.InnerException -isnot [UnauthorizedAccessException]){throw}
        # Recycling can be unavailable in the calling desktop context. Preserve
        # the rollback instead of deleting it or blocking a verified new release.
        $retained=Join-Path $releaseRoot ('retained-previous-'+[Guid]::NewGuid().ToString('N'))
        if([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($retained)) -ne $releaseRoot){throw 'Invalid rollback destination.'}
        Move-Item -LiteralPath $previous -Destination $retained
        Write-Warning "Recycle Bin access denied; retained rollback at $retained"
    }
}
if(Test-Path -LiteralPath $target){Move-Item -LiteralPath $target -Destination $previous}
try {Move-Item -LiteralPath $staging -Destination $target}
catch {
    if(!(Test-Path -LiteralPath $target) -and (Test-Path -LiteralPath $previous)){Move-Item -LiteralPath $previous -Destination $target}
    throw
}
Write-Output "Verified $($files.Count) files. Launch: $(Join-Path $target 'SilverMoon.exe')"

