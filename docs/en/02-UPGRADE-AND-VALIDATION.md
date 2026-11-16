# Upgrade validation, staged installation and recovery

[简体中文](../zh-CN/02-UPGRADE-AND-VALIDATION.md) · [Documentation index](README.md)

## Scope

This document describes the studied Middleton firmware and current desktop implementation, not a universal firmware writer. It separates project reverse-engineering conclusions from checks visible in the published implementation. Original firmware binaries and private device logs are not included.

## Which validation step is bypassed?

The modified-image strategy stages partitions and uses the upgrade recovery state to avoid the normal full-container RSA-PSS verification entry at the end of transfer. Subsequent partition validation, copying, trial boot, commit and readback remain part of the process.

In prior protocol experiments, a complete modified r2 container sent through the normal path returned `003d`, associated with OEM-footer verification failure. The upgrade context was then cleared. The supported approach is therefore not “fail signature verification and force continuation.”

Instead, transmission stops after all image partitions have closed but before `APPUPFTR` is sent. The implementation verifies staged progress and image identity, then changes only the studied PRE_VALIDATE recovery field in PS key `0x0004`. A normal reset leads into partition validation and the ImageUpgradeCopy path without revisiting the earlier whole-package RSA verification entry.

This neither recovers a vendor private key nor creates a valid new signature. It does not patch the running upgrade library's verification function. The result concerns trust in recovery state; it does not establish the absence of other boot-chain checks or the ability to boot arbitrary images.

## File and state gates

1. Known official/r2 images must match complete SHA-256 identities. Unknown images need the fixed-layout and protected-range checks in [middleton-mcu-v1](05-THIRD-PARTY-FIRMWARE-GUIDE.md).
2. The complete container is 3266308 bytes; `APPUPFTR` begins at 3266040. Staged transmission stops there and requires the next device request to be a 12-byte record header with zero skip. A percentage is not sufficient evidence.
3. Model, serial, image ID, firmware and recovery context are checked. The original PS4 contents are recorded and preserved except for the required field, and the entire result is read back.
4. Trial/commit notifications, observed final disconnection, a fresh connection and expected version/idle state jointly determine completion.

Official rollback uses the normal full-image strategy. Its footer is sent; the staged strategy must not be substituted for it.

## PS4 representation and mutation

[UpgradeContext](../../glass-playground/Domain/UpgradeContext.cs) requires exactly 38 bytes, interpreted as 19 big-endian 16-bit words. These are project-specific semantics, not a general CSR schema.

| Field | Current interpretation |
|---|---|
| Image ID | Word 12 followed by word 11, each rendered as four hexadecimal digits |
| Resume | Word 13 |
| Last closed | Word 14 |
| Staged partitions closed | Expected image ID; word 14 = 10; word 7 = 5; word 8 = 2 |
| Idle | Zero image ID; words 13, 14, 7, 8 and 9 all zero |

Preparing validation additionally requires resume 0 and words 0–3 equal to `0x8003, 5, 2, 1`. The `STORE_PS_KEY` payload is 78 bytes: two-byte key, complete 38-byte value, then a 38-byte mask area. Only word 13 in the value changes to 1. `CheckPrepared` compares the entire subsequent readback against the saved original with that one change. These details describe the guard conditions; manual blind PS writes are not an installation interface.

## Coordinator state progression

The source of truth is [ResumableUpgrade](../../glass-playground/Application/ResumableUpgrade.cs).

| Resume / local evidence | Action and required evidence |
|---|---|
| 0 | SYNC/START and device-requested data transfer; staged images stop before the footer |
| 1 | Prepared-state verification/reset when required, followed by validation polling |
| 2 | Check image ID/resume; send the trial-boot response and reconnect |
| 3 | Check displayed version; send the proceed response and require message 15 |
| 3 or 4 | Send commit response; require message 18 (`COMPLETE_IND`) |
| CompleteInd recorded | Observe disconnection; reconnect; require expected displayed version and idle PS4 |

Values above 4 are rejected. Staged recovery after data transfer also requires recorded partition-boundary evidence and matching staged-partition state. A retained context is never accepted solely because it is non-idle.

An orphaned/incompatible upload may be replaced only in resume 0. The coordinator sends ABORT (message 7), requires confirmation (8), verifies idle PS4, sends GAIA upgrade-disconnect (`0x0641`), resets its journal and opens a fresh transport. Validation, trial and commit states are not automatically aborted.

## Device-directed resume and integrity

ATT write completion, a GAIA ACK and completed installation are distinct events. Each command requires its matching ACK. Following reconnection, SYNC and context checks establish the image/session; device data requests determine positions.

For each data request, `start = cursor + skip` and `end = start + count`. Count must be nonzero and both bounds must remain within the strategy's transfer limit. The cursor starts at zero for each transfer session. Payload chunks contain at most 45 firmware bytes. The cursor and acknowledged byte total advance only after the command succeeds; skipped bytes are counted separately.

`ConfirmedFrontier` is a high-water mark, not an offset blindly used on reconnect. A flash write can survive a lost ACK, and the device can request a different position than the host last acknowledged. Cumulative acknowledged bytes may include retransmissions and do not measure unique coverage. A matching SHA-256 identifies the host's file; it is not a byte-for-byte readback of device flash.

[UpgradeJournalStore](../../glass-playground/Infrastructure/UpgradeJournalStore.cs) isolates records by device and file hash, verifies loaded identities, bounds journal size and obtains an exclusive device lock within the local user's journal directory. The journal is local supporting evidence, not a trusted device checkpoint or a lock across multiple computers.

## Deadlines and completion limits

The current coordinator uses a 75-minute overall limit, a 30-second reconnect/no-progress budget with two-second backoff, 180-second validation and final-disconnect limits, and a 300-second wait for commit completion. Re-reading unchanged PS4 is not forward progress. Relevant state/frontier changes reset recovery accounting; failure preserves the journal rather than reporting success.

Final completion requires `COMPLETE_IND`, observed disconnection, then a fresh identity/version/idle readback. The implementation waits three seconds after the disconnection observation, but no protocol signal proves the startup sound has finished. These checks establish protocol completion, not arbitrary third-party MCU functionality.

## Verification entry points

- [UpgradeWire](../../glass-playground/Domain/UpgradeWire.cs) and [UpgradeMailbox](../../glass-playground/Application/UpgradeMailbox.cs): frame length, ACK and notification separation.
- [ResumableUpgradeChecks](../../tests/DesktopReadOnly/ResumableUpgradeChecks.cs) and [UpgradeRecoveryChecks](../../tests/DesktopReadOnly/UpgradeRecoveryChecks.cs): simulated disconnects, requested offsets and recovery invariants.
- [Evidence and verification](08-EVIDENCE-AND-VERIFICATION.md): reproducibility requirements and limits of simulated tests.
