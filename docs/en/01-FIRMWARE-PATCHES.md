# Stock power policy and the r2 firmware patch

[简体中文](../zh-CN/01-FIRMWARE-PATCHES.md) · [Documentation index](README.md)

## Scope and evidence

This analysis applies only to the official Middleton 17.0.37 container whose SHA-256 is `db74c3468bd08d61f3a4e4e4e9e6eea9b19d95d6e852b9fd5e9cbac3e91c9472`. Container offsets are zero-based byte offsets from the start of the complete file. Instruction addresses are runtime addresses unless explicitly described as partition-relative offsets. Applying these offsets to another image is unsupported.

The public patch generator makes the input identity, original bytes, changed offsets and output identity reproducible. Instruction semantics and control-flow interpretations below derive from the project's reverse-engineering work. The original disassemblies and device logs are not distributed in this repository; the generator alone cannot independently establish those semantic conclusions. See [Evidence and verification](08-EVIDENCE-AND-VERIFICATION.md).

## Embedded images and address spaces

Partition 7 contains the File filesystem and two MCU variants:

| File | Container start | Length (bytes) | Runtime mapping base |
|---|---|---:|---|
| mcu.bin | 0x2748D0 | 138872 | 0x08008000 |
| huada.bin | 0x2550FC | 128980 | 0x6000 |

These MCU images contain Thumb instructions. The VTOR write and reset vector support the `0x08008000` mapping for `mcu.bin`. The historical composite `mcu_slice.bin` must not be treated as one MCU image. Both embedded variants are patched; identifying the variant actually executed on a particular device requires independent MCU readback.

## AUX sleep: suppressing a call, not changing thresholds

The `mcu.bin` path at `0x080154A8` identifies AUX as mode 2. Identified conditions include a seconds counter of at least 61, an input report with low byte 0 and high byte below 36, GPIOA.4 equal to 1, an amplifier not already asleep, and additional state gates. When these conditions hold, the call at `0x0801557A` targets `0x08019AFE`, which sets a sleep flag, writes peripheral state and records `AMP ENTER SLEEP`.

The wake logic uses a high byte of at least 49 or changed GPIO conditions to clear the counter/request wake-up.

| Item | Address / interpretation |
|---|---|
| Seconds counter | RAM `0x200040A4` |
| Amplifier sleep flag | RAM `0x2000417E` |
| Mode variable | RAM `0x20003BCC` |
| Counter timing | SysTick/main-loop analysis supports approximately one-second increments |
| Threshold units | Unknown; 36/49 must not be described as dB or millivolts |

Polling and state gates affect the wall-clock trigger time; this is not a guarantee of exactly 61 seconds. The patch replaces the four-byte BL instruction with two Thumb NOP instructions (`00 BF 00 BF`). It preserves the shared sleep function, other callers and the wake path. It does not raise the 61/36/49 thresholds, and no verified ordinary PS setting that disables this AUX behavior has been identified.

## Ten-minute shutdown: suppressing the request store

Under the identified gating conditions, the stock branch writes a shutdown request when a shared counter reaches 600 and also resets/cleans up that counter. A separate approximately 3600-count path and a shared request consumer exist.

The patch replaces only the `10 70` STRB instruction with `00 BF` (NOP). Counter reset remains intact. Skipping the whole branch would alter the shared-counter behavior and could affect the longer timer.

The 600-count request is not restricted to AUX and may affect other input modes satisfying the original gates. The independent >=3600 path and other shutdown sources, including protection conditions, are not collectively removed. The patch must not be described as guaranteeing that the speaker never powers off.

## Complete r2 patch table

The first four spans implement behavior changes; the last three change the QCC-reported display marker.

| Image | Instruction address / partition offset | Container offset | Original | Replacement |
|---|---|---|---|---|
| mcu.bin | 0x0801557a | 0x281e4a | `04f0c0fa` | `00bf00bf` |
| mcu.bin | 0x08017a1a | 0x2842ea | `1070` | `00bf` |
| huada.bin | 0x13a84 | 0x262b80 | `01f0e0ff` | `00bf00bf` |
| huada.bin | 0x1938a | 0x268486 | `1070` | `00bf` |
| apps_p1 | 0x14f34 (partition-relative) | 0x2ab6a4 | `11f02882` | `01f03440` |
| apps_p1 | 0x14f3c (partition-relative) | 0x2ab6ac | `00f030cf` | `00f052e0` |
| apps_p1 | 0x14f40 (partition-relative) | 0x2ab6b0 | `00f051e1` | `00f059e1` |

The seven spans cover 24 bytes, but some bytes remain identical; the actual binary difference is **18 bytes**. Only partitions 7 and 9 change. Layout, total length, the original signature footer and the P0 image in partition 5 are preserved. Preserving the footer does not make its original signature valid for the modified contents.

The output SHA-256 is `80ed2088247976f10968f5d4aefb3cabf367ff0b0a1f774ac6e62a292c459024`. [FirmwarePreparation.BuildModified](../../src/SilverMoon/Application/FirmwarePreparation.cs) checks the complete input hash, checks each original byte, applies the fixed edits and checks the complete output hash. Unknown versions are rejected rather than patched by pattern matching.

## Display version and MCU update evidence

The QCC edits report `25.2.52`, a project marker rather than a Marshall release version. The original final component came from an MCU report; forcing it to 52 removes its ability to independently demonstrate MCU replacement. Actual AUX and shutdown behavior requires separate functional validation.

The analyzed QCC path sends `OTA=0` following an OTA-complete event. The analyzed MCU application enters state `0x15`, writes a `UPOO` marker and resets. QCC `IMGU`/`IMGR`/`IMGE` operations provide filename-based opening, chunk transfer and termination. The MCU bootloader itself is absent from the analyzed application images. Its file selection, integrity checks and same-version handling have not been fully reconstructed, so the application code alone cannot prove every internal programming step.
