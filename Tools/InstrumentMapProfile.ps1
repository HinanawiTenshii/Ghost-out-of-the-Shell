param([switch]$IncludeConfirmationHooks)
$ErrorActionPreference = 'Stop'
$profileRoot = Join-Path (Split-Path $PSScriptRoot -Parent) '.codex-temp/clockwork-remote-smoke/Assets/Scripts'
# Mechanical profiling instrumentation in the disposable copy only.
function InstrumentMap([string]$relative, [string]$class, [string[]]$methods) {
    $path = Join-Path $profileRoot $relative
    $source = [IO.File]::ReadAllText($path)
    foreach ($method in $methods) {
        $marker = '__mapProfile_' + $method
        if ($source.Contains($marker)) { continue }
        $pattern = '(?m)^    (?:internal|private|protected|public)(?: virtual| override| static)? [\w<>]+ ' + $method + '\([^{}]*?\)\s*\{'
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
        $declaration = "`n    private static readonly Unity.Profiling.ProfilerMarker $marker = new Unity.Profiling.ProfilerMarker(`"MapProfile.$class.$method`");`n"
        $source = $source.Insert($classMatch.Index + $classMatch.Length, $declaration)
    }
    [IO.File]::WriteAllText($path, $source)
}
InstrumentMap 'Map/RuntimeMiniMapGraphic.cs' 'RuntimeMiniMapGraphic' @('RebuildFromScene','RebuildFromSnapshot','PopulateMarkers','Update')
InstrumentMap 'Map/RuntimeMiniMapGraphic.Layers.cs' 'RuntimeMiniMapGraphic' @('SyncRenderLayers','PopulateWalls','SetVerticesDirty')
InstrumentMap 'Map/RuntimeMiniMapGraphic.Surfaces.cs' 'RuntimeMiniMapGraphic' @('CaptureSurfaces','RefreshSurfaceVisibility','PopulateSurfaces')
InstrumentMap 'Map/LockedDoorMapMarker.cs' 'LockedDoorMapMarker' @('UpdateMarker')
InstrumentMap 'UI/ZeldaHealthHeartsUI.cs' 'ZeldaHealthHeartsUI' @('RefreshDisplay','UpdateTaskPointer')
InstrumentMap 'UI/TabJournalMenuController.cs' 'TabJournalMenuController' @('RefreshMiniMap','RebuildMiniMapPoints')
InstrumentMap 'Characters/ActiveZeldaCharacterDataSource.cs' 'ActiveZeldaCharacterDataSource' @('Refresh')
InstrumentMap 'Rendering/CRTScreenEffect.cs' 'CRTScreenEffect' @('ConfigureRegisteredCanvases','OnRenderImage')
if ($IncludeConfirmationHooks) {
    $layerPath = Join-Path $profileRoot 'Map/RuntimeMiniMapLayerGraphic.cs'
    $layerSource = [IO.File]::ReadAllText($layerPath)
    if (!$layerSource.Contains('SkipDimensionDirty')) {
        $hooks = @'
    public static bool TraceDirty, SkipDimensionDirty;
    private static int traced;
    public override void SetVerticesDirty()
    {
        if (TraceDirty && DrawWalls && traced++ < 4)
            UnityEngine.Debug.Log("MAP_DIRTY_TRACE " + rectTransform.rect + "\n" + UnityEngine.StackTraceUtility.ExtractStackTrace());
        base.SetVerticesDirty();
    }
'@
        $classMatch = [regex]::Match($layerSource, 'class RuntimeMiniMapLayerGraphic[^\{]*\{')
        $layerSource = $layerSource.Insert($classMatch.Index + $classMatch.Length, "`n" + $hooks + "`n")
        $dimensionMatch = [regex]::Match($layerSource, 'protected override void OnRectTransformDimensionsChange\(\)\s*\{')
        if ($dimensionMatch.Success) {
            $layerSource = $layerSource.Insert($dimensionMatch.Index + $dimensionMatch.Length, "`n        if (SkipDimensionDirty && (DrawWalls || DrawSurfaces)) return;`n")
        } else {
            $classMatch = [regex]::Match($layerSource, 'class RuntimeMiniMapLayerGraphic[^\{]*\{')
            $layerSource = $layerSource.Insert($classMatch.Index + $classMatch.Length, "`n    protected override void OnRectTransformDimensionsChange() { if (SkipDimensionDirty && (DrawWalls || DrawSurfaces)) return; base.OnRectTransformDimensionsChange(); }`n")
        }
        [IO.File]::WriteAllText($layerPath, $layerSource)
    }
    $crtPath = Join-Path $profileRoot 'Rendering/CRTScreenEffect.cs'
    $crtSource = [IO.File]::ReadAllText($crtPath)
    if (!$crtSource.Contains('SkipRegistryConfigure')) {
        $classMatch = [regex]::Match($crtSource, 'class CRTScreenEffect[^\{]*\{')
        $crtSource = $crtSource.Insert($classMatch.Index + $classMatch.Length, "`n    public static bool SkipRegistryConfigure;`n")
        $methodMatch = [regex]::Match($crtSource, 'private static void ConfigureRegisteredCanvases\([^\{]*\{')
        $crtSource = $crtSource.Insert($methodMatch.Index + $methodMatch.Length, "`n        if (SkipRegistryConfigure) return;`n")
        [IO.File]::WriteAllText($crtPath, $crtSource)
    }
}
Write-Output 'Map profiling instrumentation installed in isolated copy only.'
