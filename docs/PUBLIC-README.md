# Silvered Moon

Unofficial Windows desktop application for Marshall Middleton speaker controls and supported firmware installation. Not affiliated with or endorsed by Marshall. Project-owned source is **GPL-3.0-only**, see LICENSE. Third-party components and firmware retain separate rights.

## Build and test

Install PowerShell 7 and .NET SDK 10 on Windows. Packages restore from nuget.org.

```powershell
./tests/DesktopReadOnly/test.ps1 -WithoutFirmwareMaterials
./glass-playground/check-localization.ps1
./glass-playground/build.ps1 -WithoutBundledFirmware -CompressSingleFile -OutputDirectory publish-ci
```

Material-free tests explicitly skip checks requiring the reviewed official/r2 files. They are not full firmware validation. The application has no bundled firmware: it downloads the supported official 17.0.37 image on demand, validates its SHA-256, and generates the accepted r2 patch locally. Verified cache permits offline reuse; unknown server versions are rejected. Supported local files can also be imported. See [firmware preparation](docs/27-OFFICIAL-FIRMWARE-PREPARATION.md). To build a complete bundle, pass `-OfficialFirmwarePath` and `-R2FirmwarePath` with legally obtained, hash-matching files. Full tests accept the same parameters.

The published application is a self-contained portable EXE. It may extract native libraries and content to the .NET bundle cache. Logs and recovery state are stored in the user's application-data directory. No installer or separate .NET runtime is intended to be required. The earlier portable build passed user acceptance on another clean Windows computer. The latest download path has passed a live download/hash check, but has not repeated that clean-machine acceptance.

## Review and support

`glass-playground/Application` contains protocol/session coordination, `Domain` contains byte parsing and policy, `Infrastructure` owns Windows GATT, processes and files, and `Views` owns presentation. `tests/DesktopReadOnly` contains deterministic protocol, cancellation and recovery checks. Live device tests are separate.

See CONTRIBUTING.md, SECURITY.md, [third-party package rules](docs/19-THIRD-PARTY-FIRMWARE-GUIDE.md), and [localization guide](docs/20-UI-LOCALIZATION.md). Chinese research notes describe device-specific observations, not a generic Marshall specification.

The MCU-only third-party profile preserves all bytes outside the two specified MCU payloads. Admission does not guarantee arbitrary MCU code works. Device-side validation and commit are still required. Third-party installation has not completed live acceptance testing.

The built-in modified firmware was accepted by the owner; official rollback and restored sleep were also accepted. A Codex Security Standard static scan completed on 2026-11-17 with no confirmed vulnerabilities in 112 reviewed text/source files. Five binary visual assets and the license legal text were excluded from full security review; runtime dependencies, fuzzing and live-device attack testing were not covered. This result is not a security certification. The scoped code review and NuGet advisory check are not a no-vulnerability certification.

This source snapshot intentionally omits private diagnostics, firmware, research archives and existing Git history. `source-manifest.json` lists hashes of exported source files for local review. GitHub publishing and repository settings are not performed by the exporter.


