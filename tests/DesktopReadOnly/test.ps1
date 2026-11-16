param([string]$OfficialFirmwarePath,[string]$R2FirmwarePath,[switch]$WithoutFirmwareMaterials,[switch]$UseBundledSdk)
$ErrorActionPreference='Stop'
$env:MIDDLETON_TEST_REPOSITORY=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$env:MIDDLETON_SKIP_FIRMWARE_TESTS=if($WithoutFirmwareMaterials){'1'}else{'0'}
$env:MIDDLETON_OFFICIAL_FIRMWARE=if($OfficialFirmwarePath){(Resolve-Path -LiteralPath $OfficialFirmwarePath).Path}else{$null}
$env:MIDDLETON_R2_FIRMWARE=if($R2FirmwarePath){(Resolve-Path -LiteralPath $R2FirmwarePath).Path}else{$null}
. "$PSScriptRoot/../../src/SilverMoon/sdk-environment.ps1" -UseBundledSdk:$UseBundledSdk
& $DotnetExecutable restore "$PSScriptRoot/DesktopReadOnly.csproj" --configfile "$PSScriptRoot/../../src/SilverMoon/NuGet.Config"
if($LASTEXITCODE -ne 0){exit $LASTEXITCODE}
& $DotnetExecutable run --no-restore --project "$PSScriptRoot/DesktopReadOnly.csproj"
exit $LASTEXITCODE
