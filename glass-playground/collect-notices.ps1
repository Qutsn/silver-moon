param([Parameter(Mandatory=$true)][string]$AssetsFile,[Parameter(Mandatory=$true)][string]$Destination)
$ErrorActionPreference='Stop'
$assets=Get-Content -LiteralPath $AssetsFile -Raw | ConvertFrom-Json -AsHashtable
New-Item -ItemType Directory -Path $Destination -Force | Out-Null
# Runtime/apphost packs are downloadDependencies rather than ordinary libraries.
foreach($framework in $assets.project.frameworks.Values){
    foreach($dependency in $framework.downloadDependencies){
        $version=$dependency.version.Trim('[',']').Split(',')[0].Trim()
        $key=$dependency.name+'/'+$version
        if(!$assets.libraries.ContainsKey($key)){$assets.libraries[$key]=@{type='package';path=($dependency.name+'/'+$version).ToLowerInvariant()}}
    }
}
$inventory=@()
foreach($entry in $assets.libraries.GetEnumerator() | Sort-Object Key){
    if($entry.Value.type -ne 'package'){continue}
    $package=$null
    foreach($folder in $assets.packageFolders.Keys){$candidate=Join-Path $folder $entry.Value.path;if(Test-Path -LiteralPath $candidate){$package=$candidate;break}}
    if(!$package){throw "Cannot locate restored dependency: $($entry.Key)"}
    $packageDirectory=($inventory.Count+1).ToString('D3')
    $packageTarget=Join-Path $Destination $packageDirectory
    foreach($item in Get-ChildItem -LiteralPath $package -Recurse -File | Where-Object {$_.Extension -eq '.nuspec' -or $_.Name -match '(?i)license|notice|copying'}){
        $target=Join-Path $packageTarget ([IO.Path]::GetRelativePath($package,$item.FullName))
        New-Item -ItemType Directory -Path (Split-Path $target) -Force | Out-Null
        Copy-Item -LiteralPath $item.FullName -Destination $target -Force
    }
    $inventory+=@{package=$entry.Key;directory=$packageDirectory}
}
$inventory | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $Destination 'packages.json') -Encoding utf8
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../LICENSE') -Destination (Join-Path $Destination 'PROJECT-LICENSE.txt') -Force
