# Third-party firmware installation profile: middleton-mcu-v1

[简体中文](../zh-CN/05-THIRD-PARTY-FIRMWARE-GUIDE.md) · [Documentation index](README.md)

## Contract and implementation

This versioned profile admits fixed-layout MCU modifications; it is not a general-purpose firmware writer. Exact official/r2 SHA-256 matches retain their catalog strategies. An unknown image is eligible for the staged strategy only if both its container and protected ranges pass validation. The chooser reports rejection reasons, and the coordinator reanalyzes the bytes before transfer. Admission means that the image meets this structural profile, not that its code will boot or work.

The executable definition is [ThirdPartyFirmware](../../src/SilverMoon/Domain/ThirdPartyFirmware.cs), with [FirmwareContainer](../../src/SilverMoon/Domain/FirmwareContainer.cs) and [FirmwareCatalog](../../src/SilverMoon/Domain/FirmwareCatalog.cs). Requirements below refer to enforced checks unless stated as author responsibility.

## Base, permitted ranges and integrity

Use the complete r2 image, SHA-256 `80ed2088247976f10968f5d4aefb3cabf367ff0b0a1f774ac6e62a292c459024`, as the base. Only equal-length edits to these embedded images are admitted. Offsets are zero-based bytes in the full container; end offsets are exclusive.

| Image | Start | Length | End |
|---|---:|---:|---:|
| huada.bin | 2445564 | 128980 | 2574544 |
| mcu.bin | 2574544 | 138872 | 2713416 |

The adjacent ranges form `[2445564,2713416)`. MCU-internal integrity checks, bootability and behavior are the author's responsibility. The desktop validator does not analyze whether arbitrary machine code is correct.

The complete file must be 3266308 bytes. The protected prefix and suffix must match these hashes, regardless of filename or author declaration:

| Range | SHA-256 |
|---|---|
| `[0,2445564)` | `c14433030a3b862dc04e8a6b5079e95a8ca0e7a90445717854cc7f57f7843614` |
| `[2713416,3266308)` | `c09c70c2cb0c6536428a44b0363f227f56ca83345e8d98bf05a57866e22636d4` |

These checks preserve the header, QSPI/partition metadata, filesystem directory, non-MCU resources, QCC upgrade code, r2 display marker and footer. The profile does not admit changes to the Bluetooth application, partition sizes or filesystem directory. Those changes require a separately reviewed profile and installation strategy.

## Container grammar

Each record is an eight-byte ASCII identifier, a four-byte big-endian body length and the body. The parser rejects truncation, unexpected record order/length and trailing bytes.

| Order | Identifier | Body bytes | Partition |
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

The header chip identifier is `QCC512Xx`; `APPUPFTR` starts at byte 3266040. Import reads are bounded to 16 MiB. Passing the container grammar alone does not admit an image; the protected-range hashes are also mandatory.

## Authoring and installation procedure

1. Obtain the specified r2 base from a source you are entitled to use and verify the complete SHA-256.
2. Modify MCU image bytes in place. Preserve all lengths and every byte outside the permitted range; do not repack the filesystem.
3. Select the resulting local `.bin`. The app calculates its SHA-256 and protocol image ID and reports admission or a specific rejection.
4. On confirmation, the app rereads and rechecks the file to detect changes after selection. The staged strategy requires model `MIDDLETON`, hardware `3.0.2`, and current firmware `17.0.37` or `25.2.52`.
5. Transfer stops at the fixed partition boundary. The app checks staged state against this file's image ID and changes only the guarded recovery field. After connection loss, the device requests the resume location.
6. The app requires trial boot, commit, observed disconnection and fresh version/idle-state readback before reporting protocol installation completion.

No extra manifest is required. Protected bytes fix the layout and QCC display version. The complete SHA-256 identifies the package and isolates its local journal. The protocol image ID is the last four bytes of the full image's MD5 digest; it is a protocol field, not an authenticity check. Third-party images do not reuse the catalog's fixed r2 image ID.

## Interpretation and verification limits

Protected QCC code retains display version `25.2.52`, not an author-defined version. This value cannot independently demonstrate MCU programming or functional correctness. Full SHA-256 distinguishes images that share the display version.

Admission allows an installation attempt but does not guarantee acceptance, bootability or function. Reaching 100% transfer is not installation completion. Identity mismatch, device rejection or failed commit stops the process and retains the journal. This profile neither creates a vendor signature nor supports arbitrary partition writes.

[ThirdPartyFirmwareChecks](../../tests/DesktopReadOnly/ThirdPartyFirmwareChecks.cs) and [ResumableUpgradeChecks](../../tests/DesktopReadOnly/ResumableUpgradeChecks.cs) cover boundary rejection, permitted MCU ranges, independent image IDs, simulated transfer, lost-ACK recovery and readback logic. Material-dependent tests require the exact base image. Simulation does not replace physical-device validation.
