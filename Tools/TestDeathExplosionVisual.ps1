# Exercise the production animation curves without launching Unity Play Mode.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$source = Get-Content -Raw "$root/Assets/Scripts/Characters/ZeldaDeathExplosionVisual.cs"
function Extract-Method($name) {
    $start = $source.IndexOf("private static float $name(")
    if ($start -lt 0) { throw "Missing production curve: $name" }
    $brace = $source.IndexOf('{', $start)
    $depth = 1; $end = $brace + 1
    while ($depth -gt 0) {
        if ($source[$end] -eq '{') { $depth++ }
        if ($source[$end] -eq '}') { $depth-- }
        $end++
    }
    $source.Substring($start, $end - $start).Replace('private static', 'public static')
}
$stub = @'
using System;
public static class Mathf {
    public static float Clamp01(float x) { return Math.Max(0f, Math.Min(1f, x)); }
    public static float Exp(float x) { return (float)Math.Exp(x); }
    public static float InverseLerp(float a, float b, float t) { return Clamp01((t-a)/(b-a)); }
    public static float SmoothStep(float a, float b, float t) { t=Clamp01(t);return a+(b-a)*t*t*(3-2*t); }
}
public static class DeathCurves {
'@
Add-Type -TypeDefinition ($stub + (Extract-Method 'EvaluateTravel') + (Extract-Method 'EvaluateFlashAlpha') + (Extract-Method 'EvaluateFlashSize') + (Extract-Method 'EvaluateFragmentAlpha') + '}')
$checks = 0
function Check($ok, $message) {
    if (!$ok) { throw $message }
    $script:checks++
}
Check ([Math]::Abs([DeathCurves]::EvaluateTravel(0)) -lt 0.0001) 'Travel starts at zero'
Check ([Math]::Abs([DeathCurves]::EvaluateTravel(1) - 1) -lt 0.0001) 'Travel reaches the configured extent'
Check ([DeathCurves]::EvaluateTravel(0.2) -gt 0.58) 'Initial fragment burst must be fast'
Check ([DeathCurves]::EvaluateFlashAlpha(0) -eq 1) 'Immediate bright core'
Check ([DeathCurves]::EvaluateFlashAlpha(0.16) -eq 1) 'Core retains initial brightness for readability'
Check ([DeathCurves]::EvaluateFlashAlpha(0.26) -gt 0.7) 'Core remains visible at the former cutoff'
Check ([DeathCurves]::EvaluateFlashAlpha(0.48) -eq 0) 'Core fades out at 48% of the authored duration'
Check ([Math]::Abs([DeathCurves]::EvaluateFlashSize(0) - 0.06) -lt 0.0001) 'Cross begins at a small seed'
Check ([DeathCurves]::EvaluateFlashSize(0.05) -lt 0.1) 'First frames do not jump to a large cross'
Check ([Math]::Abs([DeathCurves]::EvaluateFlashSize(0.18) - 0.38) -lt 0.0001) 'Growth remains readable halfway through expansion'
Check ([Math]::Abs([DeathCurves]::EvaluateFlashSize(0.36) - 0.7) -lt 0.0001) 'Final cross footprint unchanged'
Check ([DeathCurves]::EvaluateFlashAlpha(0.36) -gt 0.3) 'Cross is still visible when expansion finishes'
Check ([Math]::Abs([DeathCurves]::EvaluateFlashSize(0.48) - 0.7) -lt 0.0001) 'No late scale jump before disappearing'
Check ([DeathCurves]::EvaluateFragmentAlpha(0.36) -eq 1) 'Fragments keep contrast during initial burst'
Check ([DeathCurves]::EvaluateFragmentAlpha(1) -eq 0) 'No visible fragments at cleanup'
$previousTravel = 0; $previousAlpha = 1; $previousStep = 1
foreach ($i in 1..100) {
    $p = $i / 100.0
    $travel = [DeathCurves]::EvaluateTravel($p)
    $alpha = [DeathCurves]::EvaluateFragmentAlpha($p)
    $step = $travel - $previousTravel
    Check ($step -ge 0 -and $step -le $previousStep + 0.00001) "Continuous deceleration at $p"
    Check ($alpha -ge 0 -and $alpha -le $previousAlpha) "Monotonic fade at $p"
    $previousTravel = $travel; $previousAlpha = $alpha; $previousStep = $step
}
Check ($source -match 'FragmentCount = 8') 'Bounded fragment count'
Check ($source -match 'DurationMultiplier = 1.35f' -and $source -match 'duration = Mathf.Max\(0f, effectDuration\) \* DurationMultiplier') 'All existing prefab durations extended by 35%, zero still disables'
Check ($source -match 'flashProgress = Mathf.Clamp01\(progress \* DurationMultiplier\)' -and $source -match 'EvaluateFlashAlpha\(flashProgress\)') 'Opening flash timing is independent of the longer fragment lifetime'
Check ($source -match 'SpriteMeshType.FullRect') 'Runtime sprites use explicit geometry'
Check ($source -notmatch 'BombExplosionVisual|Collider2D|Rigidbody2D|TrailRenderer|Camera\.main') 'No bomb, physics, trail or camera side effects'
$data = Get-Content -Raw "$root/Assets/Scripts/Characters/ZeldaCharacterData.cs"
$spawn = $data.Substring($data.IndexOf('private void SpawnDeathExplosion()'))
Check ($spawn -match 'CameraVisionStreamingExempt' -and $spawn -match 'GetGameplayScene') 'Death visuals retain correct scene and streaming lifetime'
Check ($spawn -match 'sortingLayerID' -and $spawn -match 'sortingOrder') 'Sorting follows character'
Check ($data -match 'if \(!silentGhostPossessionDeath\) PlayDeathSound\(\)') 'Ghost possession audio suppression retained'
Check ($data -match 'if \(!soulDetonationDeath\) SpawnDeathExplosion\(\)') 'Soul detonation does not duplicate the ordinary death burst'
Write-Output "PASS: $checks death visual curve and integration checks (not Play Mode rendering)."
