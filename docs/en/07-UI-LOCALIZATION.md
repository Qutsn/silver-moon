# UI localization architecture and translation procedure

[简体中文](../zh-CN/07-UI-LOCALIZATION.md) · [Documentation index](README.md)

## Selection and fallback

The UI currently supports only Chinese and English. Product pages, device/BLE errors, upgrade stages, recovery messages and third-party format explanations use embedded resources. Catalogs must have matching key sets; validation reports the actual count. Development material/visual experiments are outside product-page translation scope.

- Language selection uses `.NET CultureInfo.CurrentUICulture`, not timezone, keyboard, firmware or device name.
- `zh-CN`, `zh-TW`, `zh-HK` and other Chinese cultures resolve through their parent chain to `zh`, currently a shared Simplified Chinese catalog.
- `en-*` resolves to `en`; unsupported/invariant cultures default to English.
- A missing or blank selected value falls back to `zh`. If neither catalog supplies the key, the key is displayed. Validation rejects missing referenced keys before release.
- Selection occurs at startup. Reopen the app after changing Windows display language; there is no live language switch.
- Resources are embedded; catalogs are not loaded from device/firmware content and do not carry executable translation code.

Unsupported-language selection and missing-key fallback are different mechanisms: the former selects English, while the latter currently uses Chinese.

## Files and APIs

| File under `glass-playground/` | Responsibility |
|---|---|
| `Domain/UiLanguage.cs` | Parent-culture matching and missing-value fallback |
| `Domain/AppText.cs` | Framework-independent embedded catalog loading and formatting |
| `UiText.cs` | UI entry points, XAML extension and preview culture override |
| `Localization/zh.json`, `en.json` | Catalogs with stable, identical keys |
| `Localization/source-map.json` | Original extraction map, not an authoritative current completeness index |
| `check-localization.ps1` | Duplicate keys, key parity, empty values, placeholders and source references |

XAML uses `Text="{local:Tr Actual.Resource.Key}"` with `xmlns:local="using:GlassPlayground"`; C# uses `UiText.Get("Actual.Resource.Key")` or `UiText.Format("Upgrade.TransferredEvidence", acknowledgedBytes, skippedBytes)`. Application/domain code uses `AppText` without an Avalonia dependency.

Numbers in extracted `File.Text.Number` keys are stable identifiers, not current line numbers. Do not renumber them when moving code. New text should prefer semantic keys such as `Connection.ScanFailed`. Rename keys only with coordinated updates to every catalog and reference.

## Translation procedure

1. Translate values, not keys, models, versions, hashes, GAIA identifiers or log event names.
2. Preserve placeholders and format specifiers such as `{0}`, `{1:N0}` and `{0:X8}`; their order may change. Use JSON `\n` for line breaks. Do not construct English word order by concatenating localized fragments.
3. Run `./glass-playground/check-localization.ps1`; missing/empty keys, changed placeholders and unknown references must fail.
4. Run the offline tests and build described in the [review guide](06-CODE-REVIEWER-GUIDE.md).
5. Run `SilverMoon.exe --preview --ui-culture=en-US` and compare `zh-CN`, `zh-TW` and `de-DE` for Chinese normalization and English default selection. The override only applies in preview mode and does not change Windows settings or perform firmware installation.
6. Inspect clipping, wrapping, button width and accessibility names in connection, discovery, details, confirmation, transfer, interruption and completion pages. Compilation alone cannot establish layout quality.

Protocol constants, enum states, SHA-256, versions, profile IDs and raw device names retain their values. System exceptions may use the OS language. MainWindow/NativeWindow/GlassPreviewWindow/ConceptWindow and material-development views are not product localization coverage.

## Verification and adding a language

[LocalizationChecks](../../tests/DesktopReadOnly/LocalizationChecks.cs) covers selection, fallback, catalog completeness and actual resource parsing. Product UI smoke mode `--preview --ui-culture=en-US --connection-smoke --localization-snapshots` captures relevant screens. `--discovery-smoke` separately checks rows, scrolling and borders. Screenshots use the diagnostic directory, overrideable through `MIDDLETON_LOG_DIRECTORY`. Preview captures do not establish real-device behavior.

To add a language, create a catalog with the same keys, register its neutral culture in `UiLanguage.Supported`, add it to the csproj EmbeddedResource list, **and extend AppText loading/selection**, which currently explicitly loads only zh/en. Registering a code alone is insufficient. Update tests and visual checks. Missing values currently fall back to Chinese; changing that policy requires explicit code and regression changes. Distinct Simplified/Traditional Chinese translations require culture-specific registration/selection, not mixed values in one catalog.

[UiLanguage](../../glass-playground/Domain/UiLanguage.cs) · [AppText](../../glass-playground/Domain/AppText.cs) · [Catalog validation](../../glass-playground/check-localization.ps1)
