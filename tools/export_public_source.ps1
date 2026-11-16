param([Parameter(Mandatory=$true)][string]$Destination)
$ErrorActionPreference='Stop'
$repo=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$target=[IO.Path]::GetFullPath($Destination)
if(Test-Path -LiteralPath $target){throw 'Destination must not exist. Export never overwrites or deletes.'}
# Explicit source inventory: no Git history, research archives, firmware or logs.
$files=[Collections.Generic.List[string]]::new()
foreach($name in @('LICENSE','CONTRIBUTING.md','SECURITY.md','.github/workflows/windows.yml','tools/export_public_source.ps1','README.md','README.zh-CN.md')){$files.Add($name)}
foreach($dir in @('Application','Domain','Infrastructure','Views','Localization','Assets')){
 foreach($f in Get-ChildItem -LiteralPath (Join-Path $repo "glass-playground/$dir") -File -Recurse){
  if($f.Attributes -band [IO.FileAttributes]::ReparsePoint){throw "Source link requires review: $($f.FullName)"}
  if($f.Extension -in @('.cs','.axaml','.json','.png','.ico','.md')){$files.Add([IO.Path]::GetRelativePath($repo,$f.FullName))}
 }
}
foreach($f in Get-ChildItem -LiteralPath (Join-Path $repo 'glass-playground') -File){
 if($f.Extension -in @('.cs','.axaml','.csproj','.targets','.Config','.manifest','.ps1')){$files.Add("glass-playground/$($f.Name)")}
}
foreach($f in Get-ChildItem -LiteralPath (Join-Path $repo 'tests/DesktopReadOnly') -File){if($f.Extension -in @('.cs','.csproj','.ps1')){$files.Add("tests/DesktopReadOnly/$($f.Name)")}}
foreach($name in @('05-THIRD-PARTY-FIRMWARE-GUIDE.md','07-UI-LOCALIZATION.md','06-CODE-REVIEWER-GUIDE.md','04-OFFICIAL-FIRMWARE-PREPARATION.md','01-FIRMWARE-PATCHES.md','02-UPGRADE-AND-VALIDATION.md','03-BLUETOOTH-AND-WINDOWS.md')){$files.Add("docs/$name")}
$files.Add('glass-playground/RELEASE.md')
New-Item -ItemType Directory -Path $target | Out-Null
$manifest=@()
foreach($relative in $files){
 $source=Join-Path $repo $relative;$out=Join-Path $target $relative
 if(!(Test-Path -LiteralPath $source -PathType Leaf)){throw "Missing source: $relative"}
 if(([IO.File]::GetAttributes($source)) -band [IO.FileAttributes]::ReparsePoint){throw "Source link requires review: $relative"}
 New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($out)) -Force | Out-Null
 Copy-Item -LiteralPath $source -Destination $out
 $hash=(Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
 if((Get-FileHash -LiteralPath $out -Algorithm SHA256).Hash -ne $hash){throw "Copy mismatch: $relative"}
 $manifest+=@{path=$relative;sha256=$hash}
}
[IO.File]::WriteAllText((Join-Path $target '.gitignore'),"**/bin/`n**/obj/`n**/publish*/`n**/*.log`nresearch/`ndesktop-release/`n",[Text.UTF8Encoding]::new($false))
foreach($relative in @('.gitignore')){$manifest+=@{path=$relative;sha256=(Get-FileHash -LiteralPath (Join-Path $target $relative) -Algorithm SHA256).Hash}}
$manifest | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $target 'source-manifest.json') -Encoding utf8
Write-Output "Exported $($files.Count) verified source files to $target"





