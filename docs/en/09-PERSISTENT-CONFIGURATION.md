# Persistent configuration, channel probes and observed side effects

[简体中文](../zh-CN/09-PERSISTENT-CONFIGURATION.md) · [Documentation index](README.md)

## Scope and evidence

The target is first-generation Middleton / LENNOX, principally official firmware 17.0.37. This document preserves positive observations, failed approaches and unresolved questions to reduce duplicated research. It is not a comprehensive write guide or evidence that unknown writes have no effect. See [Bluetooth channels](03-BLUETOOTH-AND-WINDOWS.md) for UUIDs, framing and sound controls, and [installation recovery](02-UPGRADE-AND-VALIDATION.md) for upgrade state.

Distinguish implemented/protocol facts, physical observations, ADK/schema reference meanings, and unknowns. Successful reads, write acknowledgements, matching readback, persistence after restart, and behavioral changes are separate forms of evidence.

## Channels and identifier namespaces

| Interface | Established use | Evidence boundary |
|---|---|---|
| Classic Bluetooth / A2DP | Audio playback | LE management and media metadata do not prove audio reception, decoding or output |
| GATT `aa08` / `aa16` | Volume and two-band EQ reads, writes and notifications | Supported sound controls, not direct PS writes; one readback does not establish all power-cycle persistence semantics |
| GATT `aa10` / `aa11` | Observed media-control metadata | Metadata can remain available while music is silent; it is not an end-to-end A2DP test |
| GAIA `0x0710` / `0x0712` | PS record read / STORE_PS_KEY | Physical keys differ from schema block IDs; target-specific write-length behavior matters |
| GAIA `0x0189` / `0x0109` | GET / SET_TIMER_CONFIGURATION | Timer IDs are a separate namespace, not equivalent PS keys |
| GAIA upgrade, PS `0x0004` | Staging, recovery and commit context | Not a customization setting; change only required fields under the installation state machine |
| FE8F / TA probes | Historical permission denial, missing replies or absent target characteristics | No evidence establishes successful persistent modification; a write-without-response success alone is insufficient |

Historical command-range probes included setters and toggles, so they must not be described as read-only scans. Incomplete execution records do not establish absence of side effects.

## PS records: findings and limits

| Key | Evidence and conclusion | Unsupported inference |
|---|---|---|
| `0x0004` | Verified upgrade context; parsing, field preservation and recovery checks are implemented | Arbitrary clearing is safe, or this is a shutdown setting |
| `0x009D` | A historical erroneous write shortened an 18-byte record to 10 bytes. Restoring the same device's complete historical value, verifying it and restarting restored Bluetooth music | A specific volume, routing or codec switch, or an identified single causative byte |
| `0x008B` | Audit and historical baseline both contained `0001`; old write-test code exists without sufficient successful execution evidence | The old comment “session enable” is established, or changes have no effect |
| `0x0026` / `0x00C5` | Audit values matched their baseline; exact field consumers remain unmapped | They were never changed indirectly, or arbitrary writes are safe |
| `1/3/5/6/7` | ADK reference enums indicate AHI / permanent pairing / broadcast mode / Fast Pair index / accounts | Reference enums alone establish production Middleton mapping |

An early script incorrectly treated PS9D as upgrade context; the actual context is PS4. Recovery supports an association between the damaged record and the audio failure, but restoration changed both length and multiple bytes. Individual field effects were not isolated, and the record was not deliberately damaged again. Audio was not fully tested immediately before rollback, so this incident does not establish that restoring official firmware caused the failure.

Historical STORE requests exhibited target-specific length handling. One repair used the key, the same device's historical 18-byte value and an additional 18 zero-padding bytes, followed by full readback and post-restart readback. This is a repair observation for one path, not a general STORE encoding specification. Do not transplant another device's record or apply this compensation to unknown keys. Raw PS values, pairing data and private recovery snapshots are excluded from the public index.

A bounded audit compared five records—PS4, PS26, PS8B, PS9D and PSC5—with historical baselines; all matched. Reading timer IDs 0–31 produced 23 successes and nine status `05` responses; successful values matched their historical baseline. This covers those records at those times, not all configuration or all earlier changes.

## Timer reference mapping and counterexamples

Meanings below come from the ADK `gaia_timer_param_t` reference; each production consumer has not been independently traced. Values are research observations, not universal defaults.

| ID | Observed value | ADK reference meaning and unit |
|---|---:|---|
| 0 | 0 | Auto-off time, seconds; zero does not disable the MCU's independent shutdown policy |
| 1 | 3 | Limbo timeout, seconds |
| 4 | 180 | Pairing-mode timeout, seconds; not an established AUX sleep timer |
| 5 | 10 | Mute reminder interval, seconds; not ten-minute shutdown |
| 6 | 60 | Connectable-state timeout, seconds; not sixty-minute shutdown |
| 9 | 15 | Encryption refresh interval, minutes |
| 10 | 120 | Inquiry timeout, seconds |
| 14 | 0 | Reconnect window after A2DP loss, seconds |
| 16 | 0 | Delay before saving volume/source, seconds |
| 17 | 16 | Power-off timeout after wired audio connection, seconds |
| 18 | 0 | PS defragmentation check interval, seconds |
| 19 | 3 | Amplifier-off delay in limbo, seconds |
| 24 / 25 | 10 / 10 | Amplifier unmute / mute delay, milliseconds |
| 28 / 29 | 1000 / 3000 | BR/EDR / LE authenticated payload timeout; reference labels seconds, target units not independently verified |

GET uses `[id:u8]`; observed successful replies are `[00,id,value_hi,value_lo]`. SET uses `[id:u8,value:u16 BE]`. Representability does not imply validity. Retention across an LE reconnect does not prove power-cycle persistence.

Historical id6 edits and restoration were recorded; zero-value sweep scripts also exist. Script presence does not prove every branch executed successfully. Evidence did not establish these timers as a way to implement the desired AUX/ten-minute behavior. The released patch instead modifies identified MCU logic; see [firmware patches](01-FIRMWARE-PATCHES.md).

## Searchable schema index

The [machine-readable index](../data/persistent-config-index.json) contains 523 located field definitions and 64 declared block names, covering 44 numeric BlockIds plus Relative array templates. It preserves names, types, groups and locations, not device-instance values. `physical_ps_key: null` and `device_write_verified: false` explicitly indicate missing physical-address mapping and per-field write verification.

The source is a device-exported configuration-definition XML, SHA-256 `23addd2a31b274545382f04fce6fab1a5d122abda38388303a4bbbf5c205cde7`. The index is a structural extraction, not the original firmware or complete XML. Original materials are not distributed with public source; this hash identifies the research input but the public index alone cannot reconstruct or independently validate that input.

| Category | Reference BlockId | Remaining verification |
|---|---|---|
| A2DP volume, codec, reconnection | 33 | Production enablement and hardware/decoder support |
| Source, pre-gain, five-band filters | 133 | Production consumers and DSP routing; not equivalent to official two-band EQ |
| EQ bypass, bank, enhancement, compander | 41 | Tuning and protection interactions |
| Volume behavior and curves | 1232 / 1198 / Relative | Units, ranges and limiting; do not bypass protections |
| Connection, pairing, multipoint | 1053 / 1080 / 33 | Product enablement and connection limits |
| A2DP/TWS/Wired latency | 1590 | Stability and dropouts; not upgrade bandwidth configuration |
| Prompts, LEDs, buttons | 1186 / 1085 / 59 / 981 / 988 / 196 / 164 | Event mapping, essential indications and button paths |
| Analog input and wired shutdown | 102 / 1349 / 1359 | Distinction from MCU AUX amplifier sleep |
| Sleep, PS management, upgrade | 1053 / 1499 / 1504 | Not substitutes for MCU 600/3600 counter logic |

BlockIds, timer IDs, GATT UUIDs, DSP enums and PS keys are distinct. In particular, the XML `0x009D` CELT capability enum does not explain PS9D. Generic HFP, SPDIF and USB definitions do not establish hardware support. QCC PS does not describe all MCU storage.

## Extending the research

For each new control, establish the chain from schema definition to actual storage, production consumer and physical behavior. Record units, valid range, baseline provenance, RAM/nonvolatile location, application timing, restart/power-cycle retention and App/button overrides. Change one understood field at a time, retain complete same-device backups, verify request/reply lengths, and check unrelated fields and audio behavior. Prefer supported business-level interfaces to generic PS writes.

Unresolved work includes PS9D word semantics, complete PS-to-schema mapping, MCU storage and missing historical probe execution evidence. “No side effect observed” must not be promoted to “safe to write.”
