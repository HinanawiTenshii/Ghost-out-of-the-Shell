param([string]$ScenePath = 'Assets/Scenes/Level2/Level2-Floor1.unity')
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$destination = Join-Path $project '.codex-temp/clockwork-remote-smoke'
if (!(Test-Path "$destination/ProjectSettings/ProjectVersion.txt")) { throw 'Missing isolated validation project' }
$guidPaths = @{}
Get-ChildItem "$project/Assets" -Recurse -Filter '*.meta' -File | ForEach-Object {
    $match = [regex]::Match([IO.File]::ReadAllText($_.FullName), '(?m)^guid: ([0-9a-f]{32})')
    if ($match.Success) { $guidPaths[$match.Groups[1].Value] = $_.FullName.Substring(0, $_.FullName.Length - 5) }
}
$pending = [Collections.Generic.Queue[string]]::new()
$seen = [Collections.Generic.HashSet[string]]::new()
$pending.Enqueue((Join-Path $project $ScenePath))
foreach ($folder in 'Assets/Scripts','Assets/Resources','Assets/Shaders','Assets/Materials','Assets/Fonts') {
    Get-ChildItem (Join-Path $project $folder) -Recurse -File | Where-Object Extension -ne '.meta' | ForEach-Object { $pending.Enqueue($_.FullName) }
}
$bytes = 0L
while ($pending.Count -gt 0) {
    $path = $pending.Dequeue()
    if (!$seen.Add($path) -or !(Test-Path -LiteralPath $path -PathType Leaf)) { continue }
    $relative = $path.Substring($project.Length + 1)
    $target = Join-Path $destination $relative
    New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
    Copy-Item -LiteralPath $path -Destination $target
    if (Test-Path -LiteralPath "$path.meta") { Copy-Item -LiteralPath "$path.meta" -Destination "$target.meta" }
    $bytes += (Get-Item -LiteralPath $path).Length
    if ([IO.Path]::GetExtension($path) -in '.unity','.prefab','.mat','.asset','.controller','.overrideController','.anim') {
        foreach ($match in [regex]::Matches([IO.File]::ReadAllText($path), 'guid: ([0-9a-f]{32})')) {
            $guid = $match.Groups[1].Value
            if ($guidPaths.ContainsKey($guid)) { $pending.Enqueue($guidPaths[$guid]) }
        }
    }
}
# Rendering and layer settings match the main project; the main project is never opened by this run.
foreach ($file in 'TagManager.asset','Physics2DSettings.asset','TimeManager.asset','QualitySettings.asset','GraphicsSettings.asset') {
    Copy-Item -LiteralPath "$project/ProjectSettings/$file" -Destination "$destination/ProjectSettings/$file"
}
"Copied $($seen.Count) dependency files, $([Math]::Round($bytes / 1MB, 1)) MiB into isolated profile project."
# A quick source-prefab count for the report, independent of runtime-generated children.
$scene = [IO.File]::ReadAllText((Join-Path $project $ScenePath))
[regex]::Matches($scene, 'm_SourcePrefab: \{fileID: \d+, guid: ([0-9a-f]{32})') |
    Group-Object { $guidPaths[$_.Groups[1].Value] } | Sort-Object Count -Descending |
    Select-Object -First 25 Count, Name | Format-Table -AutoSize
