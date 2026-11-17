# Desktop portable release


The release is a Windows x64, self-contained single EXE (`SilverMoon.exe`). A compressed single-file candidate can be built with `-CompressSingleFile`; compression reduces the bundle size but can increase first-launch extraction work. Exact size depends on dependencies and whether firmware is bundled. It requires Windows 10 2004 / build 19041 or newer. The receiving computer does not need a .NET installation. .NET extracts the bundled runtime, native libraries, assets and any bundled firmware into its per-user cache on first launch; single-file delivery does not mean execution without extraction. Do not launch from a directory carrying a Low integrity label.

The application and upgrade worker launch the same EXE. Packaging checks Windows integrity labels because Low integrity can prevent access to the per-user extraction cache and recovery journal; see [Windows process and pairing behavior](../../docs/en/03-BLUETOOTH-AND-WINDOWS.md). The packaging flow does not require elevation or changes to Windows Bluetooth settings.

## Build prerequisites

Use Windows x64, PowerShell 7 and the .NET 10 SDK or newer. `NuGet.Config` restores from public nuget.org. The script selects an installed SDK; if none exists, it falls back to the repository's ignored local toolchain. `-UseBundledSdk` selects the local fallback explicitly. The application targets .NET 10 LTS.

Run scripts in a separate PowerShell process because build/test scripts return their result with `exit`. Always publish to a new or empty directory:

```powershell
pwsh -NoProfile -File ./src/SilverMoon/build.ps1 -OutputDirectory publish-portable-new -CompressSingleFile `
  -OfficialFirmwarePath C:/private-materials/official.bin `
  -R2FirmwarePath C:/private-materials/r2.bin
pwsh -NoProfile -File ./tests/DesktopReadOnly/test.ps1 `
  -OfficialFirmwarePath C:/private-materials/official.bin `
  -R2FirmwarePath C:/private-materials/r2.bin
pwsh -NoProfile -File ./src/SilverMoon/verify-portable.ps1 `
  -PublishedDirectory ./src/SilverMoon/publish-portable-new
```

For a build with bundled firmware, provide both exact hash-matching materials through the explicit path arguments. The public on-demand-download build uses -WithoutBundledFirmware and requires neither material at build time. The build verifies SHA-256 before bundling:

| Material | SHA-256 |
|---|---|
| Official 17.0.37 | `db74c3468bd08d61f3a4e4e4e9e6eea9b19d95d6e852b9fd5e9cbac3e91c9472` |
| Project r2 | `80ed2088247976f10968f5d4aefb3cabf367ff0b0a1f774ac6e62a292c459024` |

Materials are not downloaded automatically or committed by this workflow. Supply the exact hash-matching input files; matching names alone are insufficient. Rebuilding r2 independently from source is outside this packaging procedure. Permission to redistribute firmware must be considered separately from the source-code license.

## Source-only public CI

A clean source checkout can restore and compile using public NuGet without private research files:

```powershell
pwsh -NoProfile -File ./src/SilverMoon/build.ps1 -OutputDirectory publish-source-check -WithoutBundledFirmware
pwsh -NoProfile -File ./tests/DesktopReadOnly/test.ps1 -WithoutFirmwareMaterials
```

This mode prints explicit skips for tests that require genuine package bytes. Skipping material tests must not be described as full material-backed validation. The first two firmware choices now prepare exact supported images on demand from the official server and verified local cache. Portable smoke reports the bundled firmware count; zero bundled images is supported. See [acquisition details](../../docs/en/04-OFFICIAL-FIRMWARE-PREPARATION.md).

## Local package validation and delivery

`verify-portable.ps1` requires exactly one output EXE, copies it to a new Documents directory, compares SHA-256, and checks mandatory integrity labels on the EXE and extracted files. Its `--portable-smoke` check validates the identities of any firmware images present in the bundle (zero is supported), embedded English/Chinese catalogs, bundled project/dependency notices, and a worker self-launch plus a `close` reply through redirected stdio. It performs zero BLE operations. The validation copy and report remain available for inspection.

Run UI checks from that externally staged EXE using `--preview --connection --discovery-smoke`, `--preview --connection --connection-smoke` and `--preview --connection --sound-ui-smoke`. These are simulated UI checks, not a real device upgrade or proof of clean-machine compatibility. A clean-machine verification procedure should cover startup, Chinese/English display, discovery, and a protected uncached read after first pairing. Actual flashing is a separate acceptance step.

For optional local deployment, close the app and run `stage-release.ps1 -PublishedDirectory <publish directory>`. The script stages outside the checkout, checks integrity labels and hashes, preserves the previous version, and recycles an older backup. Building or validating a package does not run this deployment step.
