$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$prefab = Get-Content -Raw (Join-Path $root 'Assets/Prefabs/Decorations/DoubleDoorHinge.prefab')
$source = Get-Content -Raw (Join-Path $root 'Assets/Scripts/Interaction/DoorHingeInteraction.cs')
$anchors = @([regex]::Matches($prefab, '(?m)^--- !u!\d+ &(\d+)') | ForEach-Object { $_.Groups[1].Value })
if ($anchors.Count -ne 33 -or ($anchors | Select-Object -Unique).Count -ne 33) { throw 'Unexpected or duplicate prefab objects (two handles per leaf).' }
foreach ($ref in [regex]::Matches($prefab, '\{fileID: (\d+)\}')) {
    if ($ref.Groups[1].Value -ne '0' -and $ref.Groups[1].Value -notin $anchors) { throw "Dangling fileID: $($ref.Value)" }
}
if ([regex]::Matches($prefab, 'guid: 8cc4b96d10d34d76a36e53c0d64a9871').Count -ne 1) { throw 'Expected a single interaction owner.' }
if ([regex]::Matches($prefab, 'guid: 45a54a2446b64fa98583ad82d23a6ac4').Count -ne 2) { throw 'Expected two damageable door leaves.' }
foreach ($expected in @('primaryHinge: {fileID: 211}', 'secondaryHinge: {fileID: 311}', 'm_Name: LeftHinge', 'm_Name: RightHinge')) {
    if (-not $prefab.Contains($expected)) { throw "Missing: $expected" }
}
# Every gameplay transition must update both targets, including AI and load.
foreach ($method in @('ToggleDoor', 'ApplyPersistentState', 'BeginAiPassage', 'EndAiPassage')) {
    $body = [regex]::Match($source, '(?s)(?:private|public) void '+$method+'\([^)]*\).*?(?=\r?\n    (?:private|public))').Value
    if (-not $body.Contains('UpdateHingeTargets();')) { throw "Unsynchronized transition: $method" }
}
if (-not $source.Contains('PrimaryHinge => primaryHinge != null ? primaryHinge : transform')) { throw 'Single-door fallback missing.' }
if (-not $source.Contains('secondaryClosedRotation * Quaternion.Euler(0f, 0f, -signedAngle)')) { throw 'Second leaf must counter-rotate.' }
# Each unit-square sprite is centered halfway between its outer hinge and seam.
$culture = [Globalization.CultureInfo]::InvariantCulture
function Number([string]$text) { [double]::Parse($text, $culture) }
function TransformBlock([int]$id) { [regex]::Match($prefab, '(?ms)^--- !u!4 &'+$id+'\r?\n.*?(?=^---|\z)').Value }
function X([string]$block, [string]$field) { Number ([regex]::Match($block, $field+': \{x: ([\d.-]+)').Groups[1].Value) }
$rootScale = X (TransformBlock 101) 'm_LocalScale'
foreach ($pair in @(@(211,201,1),@(311,301,-1))) {
    $pivotX = X (TransformBlock $pair[0]) 'm_LocalPosition'
    $leafX = X (TransformBlock $pair[1]) 'm_LocalPosition'
    $width = X (TransformBlock $pair[1]) 'm_LocalScale'
    if ([Math]::Abs($pivotX+$leafX+$pair[2]*$width/2) -gt 0.00001) { throw 'Closed seam does not meet at zero.' }
    if ([Math]::Abs($leafX-$pair[2]*$width/2) -gt 0.00001) { throw 'Hinge is not on the outer edge.' }
    # Opposing 90 degree rotations take both centers to the same opening side.
    $openedY = $pair[2]*$leafX*$rootScale
    if ($openedY -le 0) { throw 'Leaves open to different sides.' }
}
Write-Output 'PASS: prefab references, single controller, two leaves, centered seam, opposite rotation, single-door fallback, and all state transition paths.'
