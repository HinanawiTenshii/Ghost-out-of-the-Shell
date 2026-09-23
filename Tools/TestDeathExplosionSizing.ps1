$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$source = Get-Content -Raw "$root/Assets/Scripts/Characters/ZeldaCharacterData.cs"
$start = $source.IndexOf('private static float CalculateDeathExplosionScale(')
if ($start -lt 0) { throw 'Missing production sizing method' }
$brace = $source.IndexOf('{', $start); $end = $brace + 1; $depth = 1
while ($depth -gt 0) {
    if ($source[$end] -eq '{') { $depth++ }
    if ($source[$end] -eq '}') { $depth-- }
    $end++
}
$method = $source.Substring($start, $end-$start).Replace('private static', 'public static')
$stub = @'
using System;
public struct Vector2 { public float x,y; public Vector2(float a,float b) { x=a;y=b; } }
public struct Vector3 { public float x,y,z; public Vector3(float a,float b,float c) { x=a;y=b;z=c; } }
public static class Mathf {
    public static float Abs(float x) { return Math.Abs(x); }
    public static float Max(float a,float b) { return Math.Max(a,b); }
}
public static class DeathSizing {
'@
Add-Type -TypeDefinition ($stub+$method+'}')
$checks = 0
function CheckSize($w,$h,$sx,$sy,$factor,$expected,$label) {
    $actual=[DeathSizing]::CalculateDeathExplosionScale([Vector2]::new($w,$h),[Vector3]::new($sx,$sy,1),$factor)
    if ([Math]::Abs($actual-$expected) -gt 0.0001) { throw "$label expected $expected, got $actual" }
    $script:checks++
}
CheckSize 1 1 1 1 5 5 'Normal body preserves authored size'
CheckSize 1 1 1.1 1.1 5 5.5 'Paladin instance scaling'
CheckSize 2 1.75 1.25 1.25 4 10 'Behemoth sprite and instance scaling'
CheckSize 1 1 0.5 0.5 5 2.5 'Small dead body'
CheckSize 1 1 -2 -2 1.9 3.8 'Mirrored body'
CheckSize 0.5 1 3 2 2 4 'Nonuniform parent scale'
CheckSize 2 0.7 1 1 1.9 3.8 'Wide body'
CheckSize 1 2 1 1 1.9 3.8 'Tall body'
CheckSize 1 1 0 0 5 0 'Zero visual scale'
CheckSize 1 1 1 1 0 0 'Disabled effect'
CheckSize 1 1 1 1 -5 0 'Negative effect multiplier'
$spawn=$source.Substring($source.IndexOf('private void SpawnDeathExplosion()'))
foreach ($pattern in @('transform.Find\(VisualObjectName\)', 'bodyRenderer.transform.lossyScale', 'bodyRenderer.bounds.center', 'bodyCollider.bounds.size', 'Configure\(deathExplosionDuration, effectScale')) {
    if ($spawn -notmatch $pattern) { throw "Missing integration: $pattern" }; $checks++
}
if ($spawn -match 'CurrentlyControlled|Camera\.main|orthographicSize|GetPixels') { throw 'Effect must use only dead body size, without camera or readable texture dependency' }
$checks++
Write-Output "PASS: $checks production death sizing and integration checks."
