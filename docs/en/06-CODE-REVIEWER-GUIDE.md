# Desktop architecture and code review guide

[简体中文](../zh-CN/06-CODE-REVIEWER-GUIDE.md) · [Documentation index](README.md)

## Scope and reading map

This guide addresses reviewers of the desktop implementation. Comments explain constraints and rationale in concise English; both documentation languages preserve the same evidence boundaries. Build scripts support public NuGet and an installed SDK. Firmware materials are separate inputs.

Paths in this table are relative to `glass-playground/` and `tests/DesktopReadOnly/` respectively.

| Review question | Source | Verification |
|---|---|---|
| Identification after renaming | `Domain/DeviceProfiles.cs`, `DiscoveryCandidates.cs` | `Program.cs` |
| Pairing versus access | `Application/PairingNegotiation.cs`, `ProtectedGatt.cs`, `Infrastructure/WindowsBleTransport.cs` | Authentication/subscription checks in `Program.cs`; [Bluetooth](03-BLUETOOTH-AND-WINDOWS.md) |
| File admission | `Domain/FirmwareContainer.cs`, `FirmwareCatalog.cs`, `Application/FirmwareImport.cs` | `FirmwareChecks.cs`, `ThirdPartyFirmwareChecks.cs` |
| Resume location | `Application/ResumableUpgrade.cs`, `Domain/UpgradeContext.cs` | `ResumableUpgradeChecks.cs`, `UpgradeRecoveryChecks.cs` |
| ACK/event separation | `Domain/UpgradeWire.cs`, `Application/UpgradeMailbox.cs` | `UpgradeChecks.cs`, `UpgradeRecoveryChecks.cs` |
| Local device ownership | `Infrastructure/UpgradeJournalStore.cs`, `UpgradeWorker.cs` | `JournalStoreChecks.cs`; physical GATT/process tests remain distinct |
| EQ preservation and queued input | `Application/SoundWriteQueue.cs`, `SoundControl.cs`, `SoundStepperState.cs` | `SoundQueueChecks.cs`, `SoundStepperChecks.cs` |
| Stale animation callbacks | `Views/ConnectionMotion.cs`, `TextMotion.cs`, `DeviceDiscovery.cs` | connection/discovery/sound UI smoke modes |

## Invariants to preserve

1. Names, models, manufacturer strings and UUIDs are spoofable. Compatibility checks are not manufacturer authentication; BLE bonding does not prove firmware compatibility.
2. Full SHA-256 catalog identities and middleton-mcu-v1 protected ranges select installation strategies. The MD5 tail is only a protocol image ID. Container parsing alone cannot admit arbitrary files or prove their code works.
3. Journals are local evidence. Recheck SYNC, PS4 and requests after reconnect. `ConfirmedFrontier` is not a blind seek offset; cumulative ACK bytes may include retransmissions.
4. ATT completion, GAIA ACK and installation completion are different. Completion requires commit notification, observed disconnection and fresh version/idle readback; it does not prove startup-audio completion.
5. Each mailbox belongs to one session. Discard ambiguous/timed-out sessions. Worker stdio is a private serial protocol, not a sandbox or public RPC endpoint.
6. Live device values, pending targets and UI display policy are separate. A stale completion must not clear a newer target; cancellation must not silently write defaults.
7. Upgrade changes require offline regressions. Simulated devices do not replace installation, interruption and rollback tests on hardware.

## Comment and documentation standards

- Describe trust boundaries, session/thread ownership, cancellation and recovery semantics at their implementation boundaries.
- Explain the origin and scope of firmware constants. Changes to byte order, offsets or completion criteria require matching tests and documentation updates.
- Distinguish source facts, observations and hypotheses. Do not assert universal device support or absolute safety.
- Link shared rationale instead of duplicating entire implementations in comments.
- Keep numbered English/Chinese counterparts synchronized, including tables, hashes, supported versions and limitations.

## Reproduction

Run the repository's test and build scripts in separate PowerShell processes because they can exit their calling process:

```powershell
./tests/DesktopReadOnly/test.ps1 -WithoutFirmwareMaterials
./glass-playground/build.ps1 -WithoutBundledFirmware -CompressSingleFile -OutputDirectory publish-local
```

The source-only mode explicitly skips real-firmware-dependent checks; it is not full material-backed verification. With matching firmware inputs, use the documented full-test options in the [build guide](../../glass-playground/RELEASE.md).

Portable EXE offline UI modes include `--preview --connection --discovery-smoke`, `--connection-smoke`, and `--sound-ui-smoke` (keep `--preview --connection` for each). These do not connect to a real speaker. Packaging validation uses `--portable-smoke <report-path>` and checks bundled resources/licenses and worker launch without BLE operations.

Scripts prefer an installed SDK; a local research toolchain is a development fallback. NuGet.Config uses nuget.org. The export script selects public source explicitly, excluding Git history, firmware and diagnostic logs. Neither unpublished binaries nor personal logs are build dependencies.

## Publication and review limits

Project-owned code uses GPL-3.0-only. Firmware/dependency licenses remain separate; release notices preserve dependency information.

DeviceLog writes locally and does not automatically upload logs. Logs can contain serials, Bluetooth addresses, local paths and exception stacks. Redact before posting issues; review ETL files, journals, screenshots, firmware and research archives separately. Pattern-based credential checks cover selected formats and files; they do not prove an entire repository or history contains no secrets.

Static review does not replace dynamic tests, dependency analysis or device validation and is not a vulnerability-free guarantee. See [Evidence and verification](08-EVIDENCE-AND-VERIFICATION.md).
