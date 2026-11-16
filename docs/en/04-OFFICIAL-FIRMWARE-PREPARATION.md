# Official firmware acquisition, caching and deterministic patching

[简体中文](../zh-CN/04-OFFICIAL-FIRMWARE-PREPARATION.md) · [Documentation index](README.md)

## Purpose and inputs

The public portable build contains no complete firmware image. Selecting official or modified firmware prepares an exact supported image on demand. Builds supplied with firmware materials instead validate those bundled files. File preparation does not itself start an installation or open a BLE link; final installation confirmation is separate.

[FirmwarePreparation](../../glass-playground/Application/FirmwarePreparation.cs) accepts only the following base and result:

| Property | Required value |
|---|---|
| Model codename | Lennox |
| Official version | 17.0.37 |
| Filename | `Lennox_FW_V17.0.37.bin` |
| Length | 3266308 bytes |
| Official SHA-256 | `db74c3468bd08d61f3a4e4e4e9e6eea9b19d95d6e852b9fd5e9cbac3e91c9472` |
| Modified SHA-256 | `80ed2088247976f10968f5d4aefb3cabf367ff0b0a1f774ac6e62a292c459024` |

A version string or official hostname alone is insufficient. Complete bytes must match the pinned hash. Metadata cannot add supported versions or patch instructions.

## Acquisition pipeline and network boundary

1. Read the local official-image cache with a size bound and verify its full SHA-256.
2. If no valid cache exists, POST to `https://fota.marshall.software/msdk/firmware` using the production Lennox metadata contract. The serial field is placeholder `x`; the actual speaker serial is not sent by this request.
3. Bound metadata to 65536 bytes and require exact version, filename and advertised size.
4. Require an absolute HTTPS URL with host `fota.marshall.software`, default port, no user information or fragment, and exact path `/devices/Lennox/prod/Lennox_FW_V17.0.37.bin`. A query may carry server-supplied download authorization; it is not logged.
5. The production HttpClient disables redirects. Check Content-Length when supplied, bound the actual stream, then verify exact length and SHA-256.
6. Write validated bytes to a temporary cache file and replace the target. Return official bytes, or construct and verify r2 locally.

Preparation has a three-minute linked deadline and respects cancellation. Changed versions, paths or bytes are rejected; the client does not follow arbitrary URLs or silently patch the newest release.

## Cache and patch semantics

The cache is `%LOCALAPPDATA%/MiddletonDesktop/firmware-cache/official-17.0.37.bin`; the directory name is retained as an implementation identifier. Every reuse checks the bytes. The original supports rollback; modified output is generated from a copy with fixed offsets and original-byte preconditions. The final r2 hash must match.

A verified cache supports offline preparation. Without it, server unavailability or an unsupported response blocks automatic preparation. Import remains subject to the [third-party format](05-THIRD-PARTY-FIRMWARE-GUIDE.md).

Future releases require independent analysis, explicit hashes/patches and validation. The interface has not been established as an arbitrary historical-version selector; continued availability of 17.0.37 is not guaranteed.

## Verification

[FirmwarePreparationChecks](../../tests/DesktopReadOnly/FirmwarePreparationChecks.cs) exercises fake HTTP responses, deterministic output, valid/corrupt cache behavior, unsupported versions, external URLs, corrupt/oversized bodies and cancellation. Tests requiring exact firmware bytes need those materials; source-only CI explicitly skips such checks.

Opt-in network check, without BLE operations:

```powershell
dotnet run --project tests/DesktopReadOnly -- --firmware-download-check C:/your-test-cache
```

Use the required .NET SDK. Download/hash checks establish file identity, not device installation. Release staging separately checks Windows integrity labels to avoid carrying a Low label into pairing or cache operations.
