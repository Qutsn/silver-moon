# Desktop portable release


The release is a Windows x64, self-contained single EXE (`SilveredMoon.exe`). A compressed single-file candidate can be built with `-CompressSingleFile`; this reduces the tested 133.73 MiB package to about 61.95 MiB while increasing first-launch extraction CPU/time. It requires Windows 10 2004 / build 19041 or newer. The receiving computer does not need a .NET installation. .NET extracts the bundled runtime, native libraries, assets and firmware into its per-user cache on first launch; single-file delivery does not mean execution without extraction. Do not launch from a directory carrying a Low integrity label.

The historical system-dotnet forwarding workaround has been removed. The application and private upgrade worker launch the same EXE. Pairing failures were traced to Low file/process integrity; see [pairing evidence](../docs/03-BLUETOOTH-AND-WINDOWS.md). No privilege elevation or changes to Windows Bluetooth settings are required by the packaging flow.

## Build prerequisites

Use Windows x64, PowerShell 7 and the .NET 10 SDK or newer. `NuGet.Config` restores from public nuget.org. The script selects an installed SDK; if none exists, it falls back to the repository's ignored local toolchain. `-UseBundledSdk` selects the local fallback explicitly. The application targets .NET 10 LTS; .NET 8 reached end of support on 2026-11-10 and is no longer the release target.

Run scripts in a separate PowerShell process because build/test scripts return their result with `exit`. Always publish to a new or empty directory:

```powershell
pwsh -NoProfile -File ./glass-playground/build.ps1 -OutputDirectory publish-portable-new -CompressSingleFile `
  -OfficialFirmwarePath C:/private-materials/official.bin `
  -R2FirmwarePath C:/private-materials/r2.bin
pwsh -NoProfile -File ./tests/DesktopReadOnly/test.ps1 `
  -OfficialFirmwarePath C:/private-materials/official.bin `
  -R2FirmwarePath C:/private-materials/r2.bin
pwsh -NoProfile -File ./glass-playground/verify-portable.ps1 `
  -PublishedDirectory ./glass-playground/publish-portable-new
```

The firmware path arguments are optional in the existing research checkout, where historical paths remain defaults. A complete release requires both exact approved materials. The build verifies SHA-256 before bundling:

| Material | SHA-256 |
|---|---|
| Official 17.0.37 | `db74c3468bd08d61f3a4e4e4e9e6eea9b19d95d6e852b9fd5e9cbac3e91c9472` |
| Project r2 | `80ed2088247976f10968f5d4aefb3cabf367ff0b0a1f774ac6e62a292c459024` |

Materials are not downloaded automatically or committed by this workflow. Supply the exact hash-matching input files; matching names alone are insufficient. Rebuilding r2 independently from source is outside this packaging procedure. Permission to redistribute firmware must be considered separately from the source-code license.

## Source-only public CI

A clean source checkout can restore and compile using public NuGet without private research files:

```powershell
pwsh -NoProfile -File ./glass-playground/build.ps1 -OutputDirectory publish-source-check -WithoutBundledFirmware
pwsh -NoProfile -File ./tests/DesktopReadOnly/test.ps1 -WithoutFirmwareMaterials
```

This mode prints explicit skips for tests that require genuine package bytes. Skipping material tests must not be described as full material-backed validation. The first two firmware choices now prepare exact supported images on demand from the official server and verified local cache. Portable smoke reports the bundled firmware count; zero bundled images is supported. See [acquisition details](../docs/04-OFFICIAL-FIRMWARE-PREPARATION.md).

## Local package validation and delivery

`verify-portable.ps1` requires exactly one output EXE, copies it to a new Documents directory, compares SHA-256, and checks mandatory integrity labels on the EXE and extracted files. Its `--portable-smoke` check validates both bundled firmware identities, embedded English/Chinese catalogs, bundled project/dependency notices, and a worker self-launch plus a `close` reply through redirected stdio. It performs zero BLE operations. The validation copy and report remain available for inspection.

Run UI checks from that externally staged EXE using `--preview --connection --discovery-smoke`, `--preview --connection --connection-smoke` and `--preview --connection --sound-ui-smoke`. These are simulated UI checks, not a real device upgrade or proof of clean-machine compatibility. Final acceptance on another Windows computer without .NET must still cover startup, Chinese/English display, discovery, and a protected uncached read after first pairing. Actual flashing is a separate acceptance step.

The fixed working release remains `desktop-release/current/SilveredMoon.exe`. Building and verifying a new portable EXE do not replace it. To replace it later, close the app and explicitly run `stage-release.ps1 -PublishedDirectory <new publish directory>`. That existing deployment flow stages outside the checkout, checks labels/hashes, preserves `previous`, and recycles an older previous copy. Do not run it merely to validate a candidate.
