param([switch]$UseBundledSdk)
$ErrorActionPreference='Stop'
$repositoryRoot=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$bundledTools=Join-Path $repositoryRoot 'research/desktop-stack-spike/toolchains'
$installed=Get-Command dotnet -ErrorAction SilentlyContinue
$script:DotnetExecutable=$null
if(!$UseBundledSdk -and $installed){
    $sdks=& $installed.Source --list-sdks
    if($LASTEXITCODE -eq 0 -and ($sdks | Where-Object {$_ -match '^(1[0-9]|[2-9][0-9])\.'})){$script:DotnetExecutable=$installed.Source}
}
if(!$script:DotnetExecutable){
    $script:DotnetExecutable=Join-Path $bundledTools 'dotnet10/dotnet.exe'
    if(!(Test-Path -LiteralPath $script:DotnetExecutable)){$script:DotnetExecutable=Join-Path $bundledTools 'dotnet/dotnet.exe'}
    if(!(Test-Path -LiteralPath $script:DotnetExecutable)){throw 'Install .NET SDK 10.0 or newer, or supply the local SDK 10 toolchain at research/desktop-stack-spike/toolchains/dotnet10.'}
    $env:DOTNET_CLI_HOME=Join-Path $bundledTools 'dotnet-home'
    $env:NUGET_PACKAGES=Join-Path $bundledTools 'nuget'
    $env:TEMP=Join-Path $bundledTools 'build-temp'
    $env:TMP=$env:TEMP
    $env:APPDATA=Join-Path $bundledTools 'appdata'
    $env:LOCALAPPDATA=Join-Path $bundledTools 'localappdata'
    @($env:DOTNET_CLI_HOME,$env:NUGET_PACKAGES,$env:TEMP,$env:APPDATA,$env:LOCALAPPDATA) | ForEach-Object {New-Item -ItemType Directory -Path $_ -Force | Out-Null}
}
$availableSdks=& $script:DotnetExecutable --list-sdks
if($LASTEXITCODE -ne 0 -or !($availableSdks | Where-Object {$_ -match '^(1[0-9]|[2-9][0-9])\.'})){throw 'The selected toolchain does not contain .NET SDK 10 or newer.'}
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
Write-Host "Using SDK host: $script:DotnetExecutable"
