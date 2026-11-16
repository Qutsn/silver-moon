# Evidence classification and reproducible verification

[简体中文](../zh-CN/08-EVIDENCE-AND-VERIFICATION.md) · [Documentation index](README.md)

## Purpose

These documents are engineering reports, not a claim of peer review or a complete vendor specification. A technical statement must identify its applicable image/device, evidence type and limits. A precise table does not make an interpretation experimentally proven.

| Evidence class | What it establishes | What it does not establish |
|---|---|---|
| Source invariant | Behavior explicitly implemented in a named function and its guards | That all physical devices exhibit the assumed protocol behavior |
| Offline test | Behavior for supplied synthetic or exact-file fixtures | Physical flash contents or successful hardware operation |
| Reverse-engineering interpretation | A control/data-flow interpretation for identified binaries and address mappings | A general specification for unknown versions or unpublished bootloader code |
| Protocol observation | Messages/state observed under recorded conditions | Every internal flash write, or future behavior on another machine |
| Functional device test | Observable behavior for a defined device/image/scenario | Universal compatibility or absence of unrelated regressions |

## Reproducible file-level checks

Record source commit, build options, OS/SDK version, input size and full SHA-256. Known official/r2 identities are defined in [FirmwareCatalog](../../glass-playground/Domain/FirmwareCatalog.cs). Patch offsets and original bytes are enforced in [FirmwarePreparation](../../glass-playground/Application/FirmwarePreparation.cs).

For exact supported inputs, a reviewer can independently check the container length and records, compare changed byte offsets with the patch table, verify the r2 result hash, and confirm protected-prefix/suffix identities for third-party admission. Firmware materials are not redistributed here. Missing material tests must be reported as skipped, not passed.

Run source-only checks and build from repository root:

```powershell
./glass-playground/check-localization.ps1
./tests/DesktopReadOnly/test.ps1 -WithoutFirmwareMaterials
./glass-playground/build.ps1 -WithoutBundledFirmware -CompressSingleFile -OutputDirectory publish-review
```

Use separate PowerShell processes for test/build scripts that exit their process. A portable smoke run (`SilverMoon.exe --portable-smoke <report-path>`) checks packaging, resources and worker startup without BLE. It is not a pairing or installation test.

## Protocol test design

Exercise loss before and after an ACK, repeated reconnects with unchanged PS4, invalid identity/image IDs, out-of-bounds data requests, stale notifications, cancellation, and completion without an observed restart. Inspect whether the implementation preserves its journal and stops rather than inferring success.

Keep these quantities separate: requested cursor, skipped bytes, cumulative ACK bytes, high-water frontier and device recovery state. Include the expected failure mode in each test. A simulated transport that always emits the expected result does not validate physical-device recovery.

## Physical validation records

A useful report specifies model/hardware, initial and target firmware identities, Windows/Bluetooth adapter configuration, audio source/activity, operation sequence, interruption point, observed messages, completion evidence and functional result. Share redacted identifiers rather than real serials or local usernames. Distinguish Bluetooth being disabled, application exit and speaker power loss; they are not equivalent interruption experiments.

Installation completion is the implemented protocol criterion: commit completion, observed disconnect, then expected version and idle state on fresh connection. Functional validation separately tests AUX behavior, shutdown behavior, audio playback, controls and official rollback as applicable. The QCC display marker cannot by itself prove the MCU application was replaced. No currently consumed signal establishes startup-sound completion.

## Scope of publication and translations

Public code/tests allow implementation review; original disassembly artifacts and private logs are not bundled. Claims based on those materials remain identified as research conclusions rather than independently reproduced from source alone. No statement of complete security, universal compatibility or external peer review is implied.

Maintain paired numbered English/Chinese documents in the same change. Keep hashes, byte order, offsets, inequalities, guards and unsupported cases equivalent. The current source implementation is authoritative for executable behavior; discrepancies should be corrected in both translations, not resolved by silently choosing the less restrictive wording. Old root-level document paths remain language selectors for existing links.
