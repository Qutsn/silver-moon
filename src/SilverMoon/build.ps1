param(
    [string]$OutputDirectory='publish-portable',
    [string]$OfficialFirmwarePath,
    [string]$R2FirmwarePath,
    [switch]$WithoutBundledFirmware,
    [switch]$CompressSingleFile,
    [switch]$UseBundledSdk
)
$ErrorActionPreference='Stop'
. "$PSScriptRoot/sdk-environment.ps1" -UseBundledSdk:$UseBundledSdk
$output=if([IO.Path]::IsPathRooted($OutputDirectory)){$OutputDirectory}else{Join-Path $PSScriptRoot $OutputDirectory}
if((Test-Path -LiteralPath $output) -and (Get-ChildItem -LiteralPath $output -Force | Select-Object -First 1)){throw 'Use a new or empty output directory; old release files must not be mixed into a single-file package.'}
$arguments=@('publish',"$PSScriptRoot/GlassPlayground.csproj",'-c','Release','-o',$output,'--configfile',"$PSScriptRoot/NuGet.Config",'--nologo')
if($OfficialFirmwarePath){$arguments+="-p:OfficialFirmwarePath=$((Resolve-Path -LiteralPath $OfficialFirmwarePath).Path)"}
if($R2FirmwarePath){$arguments+="-p:R2FirmwarePath=$((Resolve-Path -LiteralPath $R2FirmwarePath).Path)"}
if($WithoutBundledFirmware){$arguments+='-p:BundleFirmware=false'}
if($CompressSingleFile){$arguments+='-p:EnableCompressionInSingleFile=true'}
$restoreArguments=@('restore',"$PSScriptRoot/GlassPlayground.csproj",'--configfile',"$PSScriptRoot/NuGet.Config",'--nologo')
$restoreArguments+=@($arguments | Where-Object {$_ -like '-p:*'})
& $DotnetExecutable @restoreArguments *> "$PSScriptRoot/build.log"
if($LASTEXITCODE -ne 0){Get-Content "$PSScriptRoot/build.log" -Tail 30;exit $LASTEXITCODE}
# Use a fresh notices directory to avoid carrying licenses for removed dependencies.
$notices=Join-Path $PSScriptRoot ('obj/n/'+[Guid]::NewGuid().ToString('N').Substring(0,8))
& "$PSScriptRoot/collect-notices.ps1" -AssetsFile "$PSScriptRoot/obj/project.assets.json" -Destination $notices
$arguments+='--no-restore'
$arguments+="-p:PortableNoticesPath=$notices"
& $DotnetExecutable @arguments *>> "$PSScriptRoot/build.log"
$result=$LASTEXITCODE
if($result -eq 0){
    $publishedExe=Join-Path $output 'SilverMoon.exe'
    if(Test-Path -LiteralPath $publishedExe){
        # Research checkouts can carry a low-integrity label into generated
        # files. The desktop app needs the user's normal integrity level to
        # write its local recovery journal; this does not grant elevation.
        & icacls.exe $publishedExe /setintegritylevel M *> "$PSScriptRoot/integrity.log"
        if($LASTEXITCODE -ne 0){Write-Error 'Could not set the published executable integrity level.';$result=1}
    }
}
Get-Content "$PSScriptRoot/build.log" -Tail 30
exit $result
