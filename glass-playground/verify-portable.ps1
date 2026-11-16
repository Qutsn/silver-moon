param(
    [Parameter(Mandatory=$true)][string]$PublishedDirectory,
    [string]$Destination=(Join-Path ([Environment]::GetFolderPath('MyDocuments')) ('Middleton-portable-check-'+(Get-Date -Format 'yyyyMMdd-HHmmss')))
)
$ErrorActionPreference='Stop'
. "$PSScriptRoot/release-integrity.ps1"
$source=(Resolve-Path -LiteralPath $PublishedDirectory).Path
$files=@(Get-ChildItem -LiteralPath $source -Force)
if($files.Count -ne 1 -or $files[0].Name -ne 'GlassPlayground.exe' -or $files[0].PSIsContainer){throw 'Expected a publish folder containing exactly GlassPlayground.exe.'}
$target=[IO.Path]::GetFullPath($Destination)
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..')).TrimEnd('\')
if($target.Equals($repo,[StringComparison]::OrdinalIgnoreCase) -or $target.StartsWith($repo+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Use a new validation directory outside the repository to avoid inherited Low labels.'}
if(Test-Path -LiteralPath $target){throw 'Validation destination already exists; choose a new directory.'}
New-Item -ItemType Directory -Path $target | Out-Null
Assert-ReleaseIntegrity $target
$exe=Join-Path $target 'GlassPlayground.exe'
Copy-Item -LiteralPath $files[0].FullName -Destination $exe
Assert-ReleaseIntegrity $exe
if((Get-FileHash $exe).Hash -ne (Get-FileHash $files[0].FullName).Hash){throw 'Copied EXE hash mismatch.'}
$report=Join-Path $target 'portable-check.json'
$oldExtraction=$env:DOTNET_BUNDLE_EXTRACT_BASE_DIR
try {
    $env:DOTNET_BUNDLE_EXTRACT_BASE_DIR=Join-Path $target 'extracted-runtime'
    # EXE startup and worker close only: no BLE discovery, pairing, writes, or upgrade.
    $process=Start-Process -FilePath $exe -ArgumentList @('--portable-smoke',('"'+$report+'"')) -PassThru -WindowStyle Hidden
    if(!$process.WaitForExit(60000)){$process.Kill();throw 'Portable check timed out.'}
    if($process.ExitCode -ne 0){if(Test-Path $report){Get-Content $report};throw "Portable check failed: $($process.ExitCode)"}
    Get-ChildItem -LiteralPath $target -Recurse -Force | ForEach-Object {Assert-ReleaseIntegrity $_.FullName}
    $result=Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
    if(!$result.passed){throw 'Portable validation report did not pass.'}
    Get-Content -LiteralPath $report
    Write-Output "Verified EXE SHA256: $((Get-FileHash $exe).Hash)"
    Write-Output 'This is a local packaging check, not a clean-Windows or live BLE acceptance test.'
} finally {$env:DOTNET_BUNDLE_EXTRACT_BASE_DIR=$oldExtraction}
