param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$inventory = Get-Content -LiteralPath (Join-Path $projectRoot 'Docs/AudioAssetInventory.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$register = Get-Content -LiteralPath (Join-Path $projectRoot 'Docs/AudioSources.md') -Raw -Encoding UTF8
$extensions = @('.wav', '.mp3', '.ogg', '.aiff', '.aif', '.flac', '.m4a', '.aac', '.mod', '.it', '.s3m', '.xm')
$files = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'Assets') -Recurse -File | Where-Object { $_.Extension.ToLowerInvariant() -in $extensions })
$paths = @($files | ForEach-Object { $_.FullName.Substring($projectRoot.Length + 1).Replace('\', '/') })
$recordedPaths = @($inventory.assets | ForEach-Object { $_.path })
$difference = @(Compare-Object $paths $recordedPaths)
if ($difference.Count -ne 0) { throw "Audio inventory coverage mismatch: $($difference | Out-String)" }
if (@($recordedPaths | Select-Object -Unique).Count -ne $recordedPaths.Count) { throw 'Duplicate inventory paths.' }

foreach ($entry in $inventory.assets) {
    if (-not $register.Contains($entry.path)) { throw "Missing source-register entry: $($entry.path)" }
    if ($entry.sourceId -notin @('S1', 'S2', 'S3', 'S4', 'S5', 'S6', 'S7', 'M-ART', 'UNKNOWN')) { throw "Unknown source ID: $($entry.sourceId)" }
    $assetPath = Join-Path $projectRoot $entry.path
    $hash = (Get-FileHash -LiteralPath $assetPath -Algorithm SHA256).Hash
    if ($hash -ne $entry.sha256) { throw "Audio changed since provenance snapshot: $($entry.path)" }
    if ((Get-Item -LiteralPath $assetPath).Length -ne $entry.bytes) { throw "Size mismatch: $($entry.path)" }
    $meta = Get-Content -LiteralPath ($assetPath + '.meta') -Raw
    $guid = [regex]::Match($meta, '(?m)^guid: ([a-f0-9]+)').Groups[1].Value
    if ([string]::IsNullOrEmpty($guid) -or $guid -ne $entry.guid) { throw "GUID mismatch: $($entry.path)" }
}

Write-Output "PASS: all $($files.Count) audio assets have source-register entries and matching GUID/SHA-256/size snapshots. Licensing conclusions require human review."
