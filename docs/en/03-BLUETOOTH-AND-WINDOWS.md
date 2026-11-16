# Middleton Bluetooth channels and Windows implementation

[简体中文](../zh-CN/03-BLUETOOTH-AND-WINDOWS.md) · [Documentation index](README.md)

## Scope and channel separation

The scope is the studied Middleton/LENNOX, principally official firmware 17.0.37. UUIDs, formats and observations must not be assumed to apply to other Marshall products.

One physical speaker can appear as an audio device and as a separate LE device in Windows. Classic Bluetooth carries audio playback; BLE GATT exposes information, sound controls and upgrade communication. Connecting the management channel does not establish the audio playback channel.

Names are user-editable and BLE addresses may change. Discovery presents connectable LE candidates; support is determined after connection, not by requiring “Middleton” in the advertised name. The current profile checks model `MIDDLETON` and the required GAIA write/response characteristics. Manufacturer text can identify an otherwise unsupported Marshall/Zound device. Public strings and UUIDs are compatibility hints, not cryptographic manufacturer authentication.

## GATT map

| Channel | Characteristic / service | Purpose |
|---|---|---|
| Device information | `2a24`, `2a25`, `2a26`, `2a27` | Model, serial, firmware, hardware |
| Battery | `2a19` | Battery Level |
| Sound | Service `aa00`; characteristics `aa08`, `aa16` | Volume and two-band EQ; reads, writes and notifications |
| GAIA upgrade | `00001100-d102-11e1-9b23-00025b00a5a5` | Upgrade service |
| GAIA write | `00001101-d102-11e1-9b23-00025b00a5a5` | Commands |
| GAIA response/data | `00001102-d102-11e1-9b23-00025b00a5a5`, `00001103-d102-11e1-9b23-00025b00a5a5` | Responses and notifications |

Short UUIDs above expand using `0000xxxx-0000-1000-8000-00805f9b34fb`. Sound and upgrade interfaces have separate semantics. An upgrade START response field must not be reinterpreted as an established battery percentage.

## Implemented BLE GAIA framing

[UpgradeWire](../../glass-playground/Domain/UpgradeWire.cs) implements the following BLE representation. Multi-byte integers in this table are big-endian.

| Frame | Representation |
|---|---|
| Command | Vendor `0x000A` (2 bytes), command (2 bytes), payload |
| Upgrade control | Command `0x0642`; payload = message code (1 byte), body length (2 bytes), body |
| Command ACK | Original command OR `0x8000`; first payload byte is status (0 = success), followed by optional data |
| Upgrade notification | Command `0x4003`; event `0x12`, message code, two-byte body length, body |
| Data request | Message 3; body = count (4 bytes), skip (4 bytes) |
| Validation delay | Message 23; body = delay in milliseconds (2 bytes) |

The parser checks vendor, minimum frame size and notification body length. [UpgradeMailbox](../../glass-playground/Application/UpgradeMailbox.cs) allows one outstanding command, matches the ACK command identifier, and separately buffers up to 256 upgrade messages. Overflow or an unexpected ACK breaks the session rather than silently discarding upgrade events. A mailbox belongs to one connection and is not reused after loss or ambiguous timeout.

The transfer implementation uses at most 45 firmware bytes per chunk and waits for each command ACK. Prior experiments stalled with larger chunks; this conservative value is not a claimed Bluetooth maximum and cannot be increased solely from ATT MTU arithmetic.

## Windows pairing and resource lifetime

Windows BLE/GATT APIs manage discovery, access requests, GattSession, services and characteristics. Bond state, reachability, protection negotiation and notification subscription are different conditions. “Paired” does not imply that every service can currently be accessed.

[PairingNegotiation](../../glass-playground/Application/PairingNegotiation.cs) tries EncryptionAndAuthentication, Encryption, then Default **only** when the preceding result is `ProtectionLevelCouldNotBeMet`. AccessDenied, rejection, timeout and cancellation do not continue that sequence. [ProtectedGatt](../../glass-playground/Application/ProtectedGatt.cs) separately gates protected reads, writes and subscriptions on authentication.

A prior directory-dependent pairing failure was traced to Low file/process integrity on a failing copy versus Medium on a working copy. Ordinary ACL checks or administrator status alone do not establish this distinction. Staging scripts create fresh copies and check integrity labels instead of requiring elevation or changes to Windows Bluetooth settings. This observation does not imply that every AccessDenied result has that cause.

Stale sessions after reboot have failed service discovery while a fresh process could discover characteristics. [UpgradeWorker](../../glass-playground/Infrastructure/UpgradeWorker.cs) owns the upgrade transport in a separate child process, with fresh transport/session resources on reconnect. This is lifetime management, not a security sandbox or a universal Windows Bluetooth repair.

## Volume and EQ

Volume is **0–32 steps**, written as one byte to `aa08`; it is not 0–255, percent or dB. The two EQ bands use protocol values 0–10. The `aa16` payload is:

```text
[Bass, FF, FF, FF, Treble, 00, 00, 00]
```

EQ adjustments use 1–10 while displaying genuine readback 0; volume can be set to 0. Single-band adjustments preserve the other band. Notifications update the UI after physical-button changes. A requested target, write completion and a device report are different states.

A newly connected, non-playing device may report volume 0 even when that is not the previously audible setting. Playback or physical volume-button activity can synchronize the reported value. This does not establish a UI decoding error or justify inventing another “actual” value. Independent EQ readability does not establish the same readiness for volume.

## Source references

[DeviceConnection](../../glass-playground/Application/DeviceConnection.cs) · [WindowsBleTransport](../../glass-playground/Infrastructure/WindowsBleTransport.cs) · [DeviceProfiles](../../glass-playground/Domain/DeviceProfiles.cs) · [SoundControl](../../glass-playground/Application/SoundControl.cs) · [SoundWriteQueue](../../glass-playground/Application/SoundWriteQueue.cs)
