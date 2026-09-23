$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$source = Get-Content -Raw "$root/Assets/Scripts/Camera/CameraCircularVision.cs"
$tags = Get-Content -Raw "$root/ProjectSettings/TagManager.asset"
$layerSection = [regex]::Match($tags, '(?s)  layers:\r?\n(.*?)  m_SortingLayers:').Groups[1].Value
$layers = @([regex]::Matches($layerSection, '(?m)^  -([^\r\n]*)') | ForEach-Object { $_.Groups[1].Value.Trim() })
if ($layers.Count -ne 32 -or $layers[7] -ne 'Blocks' -or $layers[8] -ne 'Hidden Blocks' -or $layers[9] -ne 'Visible Non Blocking') {
    throw 'Layer registration changed or existing indices shifted'
}
function ExtractMethod([string]$name) {
    $start = [regex]::Match($source, "    private (?:void|int) $name\(\)").Index
    if (!$start) { throw "Missing method: $name" }
    $brace = $source.IndexOf('{', $start); $depth = 1; $end = $brace + 1
    while ($depth -gt 0 -and $end -lt $source.Length) {
        if ($source[$end] -eq '{') { $depth++ }
        if ($source[$end] -eq '}') { $depth-- }
        $end++
    }
    return $source.Substring($start, $end - $start)
}
$configure = ExtractMethod 'EnsureBlockLayerConfiguration'
$overlay = ExtractMethod 'GetOverlayCullingMask'
# Execute the production methods with only Unity's LayerMask conversion/lookup stubbed.
Add-Type -TypeDefinition @"
using System;
public struct LayerMask {
    public int value;
    public static bool MissingVisibleLayer;
    public static implicit operator LayerMask(int n) { return new LayerMask { value = n }; }
    public static int NameToLayer(string n) {
        if (n == "Blocks") return 7;
        if (n == "Hidden Blocks") return 8;
        if (n == "Visible Non Blocking" && !MissingVisibleLayer) return 9;
        return -1;
    }
}
public class VisibleLayerHarness {
    private LayerMask blockLayers;
    private int hiddenBlocksLayerMask;
    private int visibleNonBlockingLayerMask;
$configure
$overlay
    public static void Verify(int input) {
        var h = new VisibleLayerHarness { blockLayers = input };
        h.EnsureBlockLayerConfiguration();
        int visible = LayerMask.MissingVisibleLayer ? 0 : 512;
        int expected = input & ~visible;
        if (expected == 0) expected = 128;
        expected |= 256;
        int display = (expected & ~256) | visible;
        if (h.blockLayers.value != expected || h.GetOverlayCullingMask() != display)
            throw new Exception("Incorrect blocker/display separation for " + input);
        h.EnsureBlockLayerConfiguration();
        if (h.blockLayers.value != expected || h.GetOverlayCullingMask() != display)
            throw new Exception("Configuration is not idempotent");
    }
}
"@
foreach ($mask in 0..1023) { [VisibleLayerHarness]::Verify($mask) }
[VisibleLayerHarness]::Verify(-1)
[VisibleLayerHarness]::Verify([int]::MinValue)
[VisibleLayerHarness]::Verify([int]::MaxValue)
[LayerMask]::MissingVisibleLayer = $true
[VisibleLayerHarness]::Verify(0)
[VisibleLayerHarness]::Verify(-1)
if ($source -notmatch 'blocksCamera.cullingMask = GetOverlayCullingMask\(\)') { throw 'Overlay mask is not wired to camera' }
Write-Output 'PASS: 32 layer slots; Blocks/Hidden Blocks indices preserved; 1029 executable mask cases including Everything, legacy masks, custom bits and missing-layer fallback; repeat initialization stable.'
