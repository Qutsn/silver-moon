# Technical documentation

[简体中文](../zh-CN/README.md) · [Project](../../README.md)

These documents describe the supported Middleton images, implemented constraints and evidence limits. They are engineering documentation, not a general Marshall specification. Offsets and state values must not be generalized to other firmware versions.

| No. | Document | Scope |
|---|---|---|
| 01 | [Power policy and patches](01-FIRMWARE-PATCHES.md) | MCU address spaces, AUX/shutdown logic, complete r2 edits |
| 02 | [Installation validation and recovery](02-UPGRADE-AND-VALIDATION.md) | RSA-PSS footer path, staged recovery state, PS4, resume and completion |
| 03 | [Bluetooth and Windows](03-BLUETOOTH-AND-WINDOWS.md) | GATT/GAIA protocol, pairing, sound controls and measured transfer performance |
| 04 | [Firmware preparation](04-OFFICIAL-FIRMWARE-PREPARATION.md) | Exact-version download, network guards, cache and deterministic patching |
| 05 | [Third-party profile](05-THIRD-PARTY-FIRMWARE-GUIDE.md) | middleton-mcu-v1 byte constraints and installation contract |
| 06 | [Architecture and review](06-CODE-REVIEWER-GUIDE.md) | Source/test map and invariants |
| 07 | [UI localization](07-UI-LOCALIZATION.md) | Culture selection, catalogs, translation and extension |
| 08 | [Evidence and verification](08-EVIDENCE-AND-VERIFICATION.md) | Reproduction, physical-test records and interpretation limits |
| 09 | [Persistent configuration and side effects](09-PERSISTENT-CONFIGURATION.md) | PS keys, timer references, schema index and unsuccessful probes |

Every numbered document links to its counterpart. Source/test links use repository-relative paths so they follow the revision being read. Review a specific commit for a stable reference. Protocol installation success and functional firmware correctness are different claims.

[10 · Research findings and rejected approaches](10-RESEARCH-FINDINGS.md): corrected assumptions, official App analysis and unimplemented sleep-configuration extension points.
