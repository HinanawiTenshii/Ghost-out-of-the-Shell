$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$graphic = Get-Content -Raw "$root/Assets/Scripts/Map/RuntimeMiniMapGraphic.cs"
$refresh = [regex]::Match($graphic, '(?s)    private void RefreshBridgeLayouts\(bool force\).*?(?=    protected override void OnPopulateMesh)').Value
if (!$refresh) { throw 'Map switching method not found' }
$harness = @'
using System;
using System.Collections.Generic;
struct RuntimeMiniMapPathData { public int[] points; }
struct RuntimeMiniMapBridgeData { public string persistentId; public RuntimeMiniMapPathData[] originalPaths,rotatedPaths; }
class RotatingBridgeMechanism { public bool MapUsesRotatedLayout; }
class SceneTravelStateManager {
    public static SceneTravelStateManager Instance;
    public bool Saved;
    public bool TryGetMapDoorState(string scene,string id,out bool open) {open=Saved; return true;}
}
public class MapRegression {
    readonly List<int[]> blockPaths=new List<int[]>();
    readonly List<RuntimeMiniMapBridgeData> bridgeLayouts=new List<RuntimeMiniMapBridgeData>();
    readonly List<RotatingBridgeMechanism> liveBridges=new List<RotatingBridgeMechanism>();
    readonly List<bool> displayedBridgeStates=new List<bool>();
    int staticPathCount, rebuilds, dirty;
    string geometrySceneName="Level1-Floor2";
    void RebuildFilledWallTriangles(){rebuilds++;} void SetVerticesDirty(){dirty++;}
    static int checks;
    static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    public static int Run(){
        var m=new MapRegression(); var bridge=new RotatingBridgeMechanism();
        int[] wall={1,2,3}; int[] original={4,5,6}; int[] rotated={7,8,9};
        m.blockPaths.Add(wall); m.staticPathCount=1;
        m.bridgeLayouts.Add(new RuntimeMiniMapBridgeData {persistentId="bridge",originalPaths=new[]{new RuntimeMiniMapPathData {points=original}},rotatedPaths=new[]{new RuntimeMiniMapPathData {points=rotated}}});
        m.liveBridges.Add(bridge); m.displayedBridgeStates.Add(false);
        m.RefreshBridgeLayouts(true);
        Check(m.blockPaths.Count==2 && m.blockPaths[1]==original,"Original endpoint appended once");
        for(int frame=0;frame<200;frame++)m.RefreshBridgeLayouts(false);
        Check(m.rebuilds==1 && m.dirty==1,"Unchanged layout never rebuilds each frame");
        bridge.MapUsesRotatedLayout=true; m.RefreshBridgeLayouts(false);
        Check(m.blockPaths.Count==2 && m.blockPaths[1]==rotated,"Completed rotation replaces rather than overlays old layout");
        Check(m.blockPaths[0]==wall,"Static wall paths remain intact");
        Check(m.rebuilds==2,"Single rebuild per completed change");
        bridge.MapUsesRotatedLayout=false; m.RefreshBridgeLayouts(false);
        Check(m.blockPaths[1]==original && m.rebuilds==3,"Reverse rotation restores original endpoint");
        m.liveBridges[0]=null; SceneTravelStateManager.Instance=new SceneTravelStateManager {Saved=true};
        m.RefreshBridgeLayouts(false);
        Check(m.blockPaths[1]==rotated,"Unloaded scene uses saved hinge state");
        SceneTravelStateManager.Instance=null; m.RefreshBridgeLayouts(false);
        Check(m.blockPaths[1]==original,"Unvisited scene uses original state");
        return checks;
    }
'@
Add-Type -TypeDefinition ($harness + "`n" + $refresh + "`n}")
Write-Output ("Map switching: {0} assertions passed." -f [MapRegression]::Run())
$scene = Get-Content -Raw "$root/Assets/Scenes/Level1/Level1-Floor2.unity"
$blocks = [regex]::Split($scene, '(?m)(?=^--- !u!)')
$group = $blocks | Where-Object { $_ -match '^--- !u!114 &2107000030\r?\n' }
$ids = [regex]::Matches($group, '(?m)^  - \{fileID: (\d+)\}')
if ($ids.Count -ne 17) { throw 'Expected all 17 yellow-wall colliders' }
foreach ($id in $ids) {
    $value = $id.Groups[1].Value
    if (!($blocks | Where-Object {$_ -match "^--- !u!61 &$value\r?\n"})) {throw "Missing wall collider $value"}
}
$snapshot = Get-Content -Raw "$root/Assets/Resources/MiniMaps/Level1-Floor2.asset"
if ($snapshot -notmatch 'schemaVersion: 4') {throw 'Missing regenerated schema-4 snapshot'}
$original = [regex]::Match($snapshot, '(?s)    originalPaths:(.*?)    rotatedPaths:').Groups[1].Value
$rotated = [regex]::Match($snapshot, '(?s)    rotatedPaths:(.*)').Groups[1].Value
if ([regex]::Matches($original,'- points:').Count -ne 10 -or [regex]::Matches($rotated,'- points:').Count -ne 10) {throw 'Each bridge layout needs two wall and eight door-leaf rectangles'}
if ($original.Trim() -eq $rotated.Trim()) {throw 'Bridge layouts must differ'}
Write-Output 'Scene/snapshot geometry passed: 17 wall references and 10 polygons per bridge endpoint.'
