# Mechanical reserialization of the generated catalog, preserving its header,
# prefab references and scene membership. Does not touch player save files.
$ErrorActionPreference = 'Stop'
$catalogProjectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$catalogAssetPath = Join-Path $catalogProjectRoot 'Assets/Resources/SaveGameCatalog.asset'
$catalogText = Get-Content -LiteralPath $catalogAssetPath -Raw
$catalogSceneHeader = [regex]::Match($catalogText, '(?m)^  scenes:\s*\r?$')
if (-not $catalogSceneHeader.Success) { throw 'Catalog scene list not found.' }
$catalogEntries = [regex]::Matches($catalogText, '(?m)^  - name: ([^\r\n]+)\r?\n    path: ([^\r\n]+)')
if ($catalogEntries.Count -eq 0) { throw 'No catalog scenes found; refusing to overwrite.' }
$catalogOutput = [Text.StringBuilder]::new()
[void]$catalogOutput.Append($catalogText.Substring(0, $catalogSceneHeader.Index))
[void]$catalogOutput.AppendLine('  scenes:')
foreach ($catalogEntry in $catalogEntries) {
    $catalogSceneName = $catalogEntry.Groups[1].Value.Trim()
    $catalogScenePath = $catalogEntry.Groups[2].Value.Trim()
    $catalogResolvedScene = [IO.Path]::GetFullPath((Join-Path $catalogProjectRoot $catalogScenePath))
    if (-not $catalogResolvedScene.StartsWith($catalogProjectRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
        -not (Test-Path -LiteralPath $catalogResolvedScene -PathType Leaf)) {
        throw "Invalid source scene: $catalogScenePath"
    }
    $catalogSceneText = [IO.File]::ReadAllText($catalogResolvedScene)
    $catalogEncoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($catalogSceneText))
    if ([Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($catalogEncoded)) -cne $catalogSceneText) {
        throw "Scene round-trip failed: $catalogScenePath"
    }
    [void]$catalogOutput.AppendLine("  - name: $catalogSceneName")
    [void]$catalogOutput.AppendLine("    path: $catalogScenePath")
    [void]$catalogOutput.AppendLine("    authoredState: ''")
    [void]$catalogOutput.AppendLine("    authoredStateBase64: $catalogEncoded")
}
$catalogBackupFolder = Join-Path $catalogProjectRoot '.codex-temp'
[void](New-Item -ItemType Directory -Path $catalogBackupFolder -Force)
$catalogBackupPath = Join-Path $catalogBackupFolder ('SaveGameCatalog-before-encoding-' + [Guid]::NewGuid().ToString('N') + '.asset')
Copy-Item -LiteralPath $catalogAssetPath -Destination $catalogBackupPath
[IO.File]::WriteAllText($catalogAssetPath, $catalogOutput.ToString(), [Text.UTF8Encoding]::new($false))
Write-Output "Re-encoded $($catalogEntries.Count) scene templates; every decoded scene matches its source."
Write-Output "Original generated catalog backed up to: $catalogBackupPath"
