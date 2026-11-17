# Research findings and rejected approaches

[简体中文](../zh-CN/10-RESEARCH-FINDINGS.md) · [Documentation index](README.md)

## Reading the evidence

This record concerns first-generation Middleton / LENNOX and the exact 17.0.37 input identified in [the patch specification](01-FIRMWARE-PATCHES.md). It consolidates research conclusions rather than providing a general firmware-writing recipe. Production-code analysis, protocol observations, reference SDK definitions and proposed extensions have different evidentiary weight. Private device logs and proprietary binaries are not included in this repository; historical observations below are not independently reproducible from the public source alone.

## Results that prevent repeated dead ends

| Investigated approach or assumption | Result and reusable conclusion |
|---|---|
| Reuse another model's TA OTA / FA302D24 path | The established Middleton route is BLE GAIA / VM Upgrade. Similar branding does not establish protocol compatibility. |
| Treat PS `0x009D` as upgrade state | Incorrect: the established upgrade context is PS4. PS9D corruption was associated with loss of Bluetooth audio; individual field meanings remain unresolved. See [persistent configuration](09-PERSISTENT-CONFIGURATION.md). |
| Infer stored length from STORE request length | Target-specific handling required padding and complete readback. Do not generalize the PS4 encoding to arbitrary keys. |
| Wait for normal authentication failure, then resume | A modified full-image attempt returned `003d` and lost its context before host cleanup. The supported staged path stops before the footer; it does not recover an ordinarily rejected signature. |
| Jump directly to resume point 2 | This skips the required copy path. The supported route uses the constrained PRE_VALIDATE/resume-1 transition; see [validation and recovery](02-UPGRADE-AND-VALIDATION.md). |
| Recompute a checksum or use another product's signing method | CRC/XOR reconstruction does not replace RSA-PSS authentication. No signing private key was recovered and the modified image is not re-signed. |
| Use the displayed modified version as proof of MCU behavior | The QCC display marker and MCU power-policy changes are separate. Version readback establishes identity evidence, not successful execution of every MCU change. |
| Assume every failed trial boot is recoverable | A trial-timeout rollback was observed; that does not prove universal recovery after arbitrary firmware changes. |
| Treat “all bytes sent” as radio traffic | Some historical cursor accounting included skipped ranges. Track acknowledged transmitted bytes, device-requested skips and logical image cursor separately. |
| Use mini-containers to shorten installation | These experiments probed parsing/validation. They did not establish a safe short-flash or official-rollback method. Removing partitions is not a validated optimization. |
| Raise chunk size based only on MTU | A 45-byte probe continued while a 55-byte probe stalled; the cause remains unknown. This is not a physical BLE maximum. See [throughput](03-BLUETOOTH-AND-WINDOWS.md#transfer-duration-and-throughput). |

## Official Android App sound-control analysis

Static analysis used an APK with SHA-256 `09ebe8831b92f17f881bfb5b6060b3e122cfc893b1137a7fe5ca5d2603010ef7`, recorded as obtained from APKPure. Its publisher signature was not independently verified in this analysis. These findings are scoped to that artifact and the traced Middleton branches, not the current store release or iOS application.

- `PlayerViewModel` and `BLEDeviceStateController` request an initial volume read and combine read results with notifications. The Middleton legacy characteristic is `aa08`; valid zero is retained. No alternate paused-playback “actual volume” interface was found in the traced path.
- The official write path first updates local state, then queues the GATT write. A UI target therefore is not proof of device adoption. The one-shot feedback-suppression flag is not a general filter that discards all notifications during interaction.
- `TwoBandEQViewModel` follows the two-band path through `aa16`, preserving the other band from local state. Extended encoding is `bass FF FF FF treble 00 00 00`. Using stale local state can overwrite a more recent change to the other band.
- The traced wrapper defaults select a 100 ms `throttleLatest` window: the first value is emitted immediately and later values are coalesced. This is not a debounce that waits until interaction stops. Queued writes and notifications still do not supply a separate MCU-applied acknowledgement.
- The official generic EQ decoder's invalid-data fallback is a zero-valued object. Silver Moon treats malformed data as unknown instead of presenting it as a valid device report.

The analysis explains UI responsiveness but does not establish a remedy for a legitimate `aa08=00` report before playback. Current behavior and supported ranges are described in [Bluetooth and Windows](03-BLUETOOTH-AND-WINDOWS.md#volume-and-eq).

## Configurable sleep: located extension points, not an implemented feature

The released r2 patch suppresses two identified actions. It does not implement adjustable sleep durations or a new BLE settings command. Existing GAIA timers have not been established as a substitute for the MCU's AUX and ten-minute paths.

For the STM32 application mapping in document 01, the following locations are research anchors:

| Location | Static observation | Unresolved requirement |
|---|---|---|
| `0x080134b8` → `0x080129d4` | CR/LF-delimited text collection and dispatch | Safe extension, message bounds and production routing |
| `0x08014384` | Separate `0xCC` binary branch | Frame semantics; not interchangeable with text commands |
| `0x08011f44` → `0x08014d66` | Character-by-character outgoing path | End-to-end acknowledgement handling in QCC |
| `0x08016054` | Sends `AT+WIRE=48000` or `AT+WIRE=0` | Does not establish arbitrary host-to-MCU control |
| `0x0801c42c` → `0x080210e6` | Existing upgrade-marker storage use | Storage ownership, medium and free-space mapping; device index 8 is not an established flash page 8 |

Strings such as `READY`, `ABSV`, `AUDIO`, `AMP=`, `OTA=` and `MUSIC=` support locating internal handlers; their presence does not expose those handlers as BLE commands. QCC disassembly offsets `0x12a3e` / `0x12a70` lead toward a sender at `0x1f77c`, but its ABI, buffer ownership and custom reply route remain unresolved. These offsets belong to the studied disassembly, not host file offsets or universal addresses. Huada receiver, acknowledgement and storage mappings require separate analysis.

A possible future design would keep validated configuration in QCC and synchronize a versioned RAM copy to the MCU, with explicit requested/saved/applied states and capability negotiation. No free PS key, safe MCU storage allocation or complete custom-message path has been established. This proposal is not an available installation profile. Changing the shared shutdown counter also affects reset semantics; simply replacing 600 with a new number is not a validated configurable-time implementation.

## Windows and completion-state interpretation

Discovery, bonding, encrypted GATT access, upgrade completion and daily-control readiness are distinct. Two names in a scan may be stale/rotating addresses or separate devices; names alone are not identity. A successful model read does not prove authorization for protected sound characteristics.

After restart, repeated identical PS4 reads are not progress. Installation completion requires the protocol and independent readback evidence described in document 02. Daily-control services may become ready later and must recover without replaying an already-completed installation. No startup-chime-completed signal has been established; Bluetooth reachability alone does not prove that the audible startup sequence has ended.

For further work, preserve exact input hashes, address spaces, request/reply framing and negative results. Label an untested hypothesis explicitly instead of turning it into a public operational instruction.
