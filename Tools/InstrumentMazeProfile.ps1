$ErrorActionPreference = 'Stop'
$root = Join-Path (Split-Path $PSScriptRoot -Parent) '.codex-temp/clockwork-remote-smoke/Assets/Scripts'
# Mechanical instrumentation in the disposable profile copy ONLY. The real gameplay source is untouched.
function Instrument([string]$relative, [string]$class, [string[]]$methods) {
    $path = Join-Path $root $relative
    $source = [IO.File]::ReadAllText($path)
    foreach ($method in $methods) {
        $marker = '__maze_' + $method
        if ($source.Contains($marker)) { continue }
        $pattern = '(?m)^    (?:private|protected|public)(?: virtual| override| static)? [\w<>]+ ' + $method + '\([^{}]*?\)\s*\{'
        $match = [regex]::Match($source, $pattern)
        if (!$match.Success) { throw "Method not found: $class.$method" }
        $open = $match.Index + $match.Length - 1
        $depth = 1; $end = $open + 1
        while ($depth -gt 0 -and $end -lt $source.Length) {
            if ($source[$end] -eq '{') { $depth++ }
            if ($source[$end] -eq '}') { $depth-- }
            $end++
        }
        if ($depth -ne 0) { throw 'Unbalanced method' }
        $source = $source.Insert($end - 1, "`n        }`n    ").Insert($open + 1, "`n        using ($marker.Auto()) {`n")
        $classMatch = [regex]::Match($source, 'class ' + $class + '[^{]*\{')
        $declaration = "`n    private static readonly Unity.Profiling.ProfilerMarker $marker = new Unity.Profiling.ProfilerMarker(`"Maze.$class.$method`");`n"
        $source = $source.Insert($classMatch.Index + $classMatch.Length, $declaration)
    }
    [IO.File]::WriteAllText($path, $source)
}
Instrument 'AI/ZeldaCharacterAiBase.cs' 'ZeldaCharacterAiBase' @('BuildGridPath','TryBuildGridPath','UpdateVisionMesh','UpdateTargetAndState','TickCurrentState')
Instrument 'AI/SkeletonCharacterAi.cs' 'SkeletonCharacterAi' @('UpdateTargetAndState')
Instrument 'Decorations/EnergyCrystalVisual.cs' 'EnergyCrystalVisual' @('WriteLinks')
Instrument 'Camera/CameraVisionObjectStreaming.cs' 'CameraVisionObjectStreaming' @('UpdateSpatialIndex','UpdateStreaming','ProcessDiscoveryScan')
Write-Output 'Instrumented isolated runtime methods; production scripts unchanged.'
