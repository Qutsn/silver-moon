# Third-party installation profile: middleton-mcu-v1

[简体中文](../zh-CN/05-THIRD-PARTY-FIRMWARE-GUIDE.md) · [Documentation index](README.md)

## Admission contract

This is an executable, versioned admission rule for fixed-layout MCU modifications, not a general binary flashing interface. Known official/r2 hashes retain their catalog strategies. Unknown files must satisfy this profile before installation confirmation is enabled; otherwise the app reports a rejection reason. The coordinator independently reanalyzes the bytes. Offline tests exercise this behavior, not arbitrary firmware functionality on a physical speaker.

The normative implementation is [ThirdPartyFirmware](../../glass-playground/Domain/ThirdPartyFirmware.cs), together with [FirmwareContainer](../../glass-playground/Domain/FirmwareContainer.cs) and [FirmwareCatalog](../../glass-playground/Domain/FirmwareCatalog.cs). “Must” below denotes an implemented requirement unless identified as an author's responsibility.

## Allowed changes and protected bytes

Use the complete r2 image with SHA-256 `80ed2088247976f10968f5d4aefb3cabf367ff0b0a1f774ac6e62a292c459024` as the base. Only equal-length edits within these embedded files are admitted. All ranges are zero-based container byte offsets with an exclusive end.

| File | Start | Length | End |
|---|---:|---:|---:|
| huada.bin | 2445564 | 128980 | 2574544 |
| mcu.bin | 2574544 | 138872 | 2713416 |

The adjacent ranges form `[2445564,2713416)`. MCU internal checks, bootability and behavior remain the author's responsibility. The desktop validator cannot establish arbitrary machine-code correctness.

The complete file must be 3266308 bytes. Protected ranges must match the base, enforced by SHA-256 rather than filenames or declarations:

| Protected range | SHA-256 |
|---|---|
| `[0,2445564)` | `c14433030a3b862dc04e8a6b5079e95a8ca0e7a90445717854cc7f57f7843614` |
| `[2713416,3266308)` | `c09c70c2cb0c6536428a44b0363f227f56ca83345e8d98bf05a57866e22636d4` |

This preserves headers, QSPI/partition metadata, filesystem directory, non-MCU resources, QCC upgrade code, the r2 display marker and footer. Changing the Bluetooth application, partition sizes or file directory requires a different profile and strategy.

## Container constraints

Records contain an eight-byte ASCII identifier, a four-byte big-endian body length and the body. Truncation and trailing bytes are rejected.

| Order | Identifier | Body length | Partition |
|---|---|---:|---:|
| 1 | APPUHDR5 | 48 | — |
| 2 | PARTDATA | 20 | 0 |
| 3 | PARTDATA | 276 | 1 |
| 4 | PARTDATA | 10924 | 4 |
| 5 | PARTDATA | 572116 | 5 |
| 6 | PARTDATA | 70128 | 6 |
| 7 | PARTDATA | 2059844 | 7 |
| 8 | PARTDATA | 552588 | 9 |
| 9 | APPUPFTR | 256 | — |

The header chip identifier is `QCC512Xx`; the footer starts at 3266040. Import additionally imposes a 16 MiB read limit. Structure alone never admits a file: protected-range checks are also required.

## Authoring and installation procedure

1. Obtain the specified base from a legitimate source and verify its complete SHA-256.
2. Modify MCU bytes in place, preserving lengths and every byte outside the permitted ranges. Do not repack the filesystem.
3. Select the local `.bin`. The app calculates its full SHA-256 and protocol image ID, then reports admission or rejection.
4. After confirmation, the app rereads the file and checks its hash to detect replacement after selection. The staged strategy requires Middleton, hardware `3.0.2`, and current firmware `17.0.37` or `25.2.52`.
5. Transfer stops at the fixed partition boundary, verifies staged state against this file's image ID, and changes only the guarded recovery field. After loss, the device requests the resume location.
6. Trial boot, commit, observed disconnection and fresh version/idle checks must complete before protocol installation success is reported.

No extra manifest is required: protected content fixes the layout and displayed version. Full SHA-256 identifies each package and isolates its journal. The image ID is the last four bytes of the complete file's MD5 digest, as used by the existing protocol; it is not an authenticity check or the fixed r2 image ID reused for every package.

## Result interpretation

Protected QCC code retains display version **25.2.52**, not an author-defined version. That string cannot independently establish internal MCU programming or functional correctness. Full SHA-256 distinguishes packages with the same display version.

Admission permits an attempt, not guaranteed acceptance or bootability. Transfer 100% is not installation success. Identity mismatches, rejection or commit failure stop the process and retain its journal. The profile does not repair the vendor signature and cannot write arbitrary partitions.

[ThirdPartyFirmwareChecks](../../tests/DesktopReadOnly/ThirdPartyFirmwareChecks.cs) and [ResumableUpgradeChecks](../../tests/DesktopReadOnly/ResumableUpgradeChecks.cs) cover protected-boundary rejection, MCU-range admission, independent IDs, simulated transfer, lost-ACK recovery and final readback. Material-dependent tests require exact base bytes; simulation does not replace physical-device testing.
