# Mechanical refresh of generated templates only; preserve other levels and all prefab references.
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$catalogPath = Join-Path $projectRoot 'Assets/Resources/SaveGameCatalog.asset'
$catalog = [IO.File]::ReadAllText($catalogPath)
if ($catalog -notmatch '(?m)^  scenes:') { throw 'Missing scene catalog list.' }
foreach ($name in 'Level2-Floor1','Level2-Floor-1','Level2-Floor2') {
    $scenePath = "Assets/Scenes/Level2/$name.unity"
    $source = [IO.File]::ReadAllText((Join-Path $projectRoot $scenePath))
    $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($source))
    $entry = "  - name: $name`n    path: $scenePath`n    authoredState: ''`n    authoredStateBase64: $encoded`n"
    $pattern = '(?ms)^  - name: ' + [regex]::Escape($name) + '\r?\n.*?(?=^  - name: |\z)'
    if ([regex]::IsMatch($catalog, $pattern)) { $catalog = [regex]::Replace($catalog, $pattern, $entry) }
    else { $catalog = $catalog.TrimEnd() + "`n" + $entry }
}
$backup = Join-Path $projectRoot '.codex-temp/SaveGameCatalog-before-level2-infrastructure.asset'
if (!(Test-Path -LiteralPath $backup)) { Copy-Item -LiteralPath $catalogPath -Destination $backup }
[IO.File]::WriteAllText($catalogPath, $catalog, [Text.UTF8Encoding]::new($false))
Write-Output 'Updated three Level2 scene templates; preserved other templates and character prefab references.'
