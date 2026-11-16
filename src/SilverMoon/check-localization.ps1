$ErrorActionPreference='Stop'
function Read-Catalog([string]$Name) {
    $path=Join-Path $PSScriptRoot "Localization/$Name.json"
    $document=[System.Text.Json.JsonDocument]::Parse([IO.File]::ReadAllText($path))
    try {
        $keys=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        foreach($property in $document.RootElement.EnumerateObject()) {
            if(!$keys.Add($property.Name)){throw "Duplicate key in ${Name}: $($property.Name)"}
            if($property.Value.ValueKind -ne [System.Text.Json.JsonValueKind]::String){throw "Non-string resource: $($property.Name)"}
        }
    } finally {$document.Dispose()}
    return Get-Content -LiteralPath $path -Raw | ConvertFrom-Json -AsHashtable
}
$source=Read-Catalog 'zh'
$missing=0
foreach($file in Get-ChildItem (Join-Path $PSScriptRoot 'Localization') -Filter '*.json' | Where-Object BaseName -ne 'source-map') {
    $catalog=Read-Catalog $file.BaseName
    if(Compare-Object @($source.Keys) @($catalog.Keys)){throw "Resource key mismatch: $($file.Name)"}
    foreach($key in $source.Keys) {
        if([string]::IsNullOrWhiteSpace($source[$key])){throw "Empty source: $key"}
        $text=$catalog[$key]
        if([string]::IsNullOrWhiteSpace($text)){throw "Empty translation: $($file.Name)/$key"}
        # Format entries use composite placeholders, including their numeric format.
        $expected=@([regex]::Matches($source[$key],'(?<!\{)\{\d+(?:[^{}]*)\}(?!\})') | ForEach-Object Value | Sort-Object -Unique)
        $actual=@([regex]::Matches($text,'(?<!\{)\{\d+(?:[^{}]*)\}(?!\})') | ForEach-Object Value | Sort-Object -Unique)
        if(($expected -join '|') -cne ($actual -join '|')){throw "Changed placeholders: $($file.Name)/$key"}
        if($expected.Count -gt 0){[void][System.Text.CompositeFormat]::Parse($text)}
    }
}
@('Views','Domain','Application','Infrastructure') | ForEach-Object { Get-ChildItem (Join-Path $PSScriptRoot $_) -File -Recurse } | Where-Object Extension -in '.cs','.axaml' | ForEach-Object {
    $text=[IO.File]::ReadAllText($_.FullName)
    foreach($match in [regex]::Matches($text,'(?:UiText|AppText)\.(?:Get|Format)\("([^"]+)"|\{local:Tr ([^}]+)\}')) {
        $key=if($match.Groups[1].Success){$match.Groups[1].Value}else{$match.Groups[2].Value}
        if(!$source.ContainsKey($key)){throw "Unknown UI resource: $key in $($_.Name)"}
    }
}
Write-Output "PASS: $($source.Count) source keys; no empty translations; placeholders and references valid."
