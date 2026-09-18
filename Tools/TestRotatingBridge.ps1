$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$controller = Get-Content -Raw "$root/Assets/Scripts/Interaction/RotatingBridgeMechanism.cs"
$lever = Get-Content -Raw "$root/Assets/Scripts/Interaction/LeverData.cs"
$dispatch = [regex]::Match($lever, '(?s)    private static void ApplyLinkedObjectChange\(GameObject target\).*?(?=    private void ApplyVisual)').Value
if (!$dispatch) { throw 'Lever dispatch method not found' }
$stubs = @'
namespace UnityEngine {
    public class MonoBehaviour { public bool isActiveAndEnabled = true; }
    public class SerializeField : System.Attribute {}
    public class Min : System.Attribute { public Min(float value) {} }
    public class Header : System.Attribute { public Header(string text) {} }
    public class AddComponentMenu : System.Attribute { public AddComponentMenu(string text) {} }
    public class DisallowMultipleComponent : System.Attribute {}
    public class GameObject {
        public bool activeSelf = true;
        public System.Collections.Generic.Dictionary<System.Type, object> components = new System.Collections.Generic.Dictionary<System.Type, object>();
        public T GetComponent<T>() where T : class { object o; return components.TryGetValue(typeof(T), out o) ? o as T : null; }
        public void SetActive(bool value) { activeSelf = value; }
    }
}
public class DoorHingeInteraction {
    public bool IsOpen, IsChangingOpenState;
    public int toggles;
    public void ToggleFromExternal() { toggles++; IsOpen = !IsOpen; IsChangingOpenState = true; }
}
public class DoubleSlidingDoor {
    public bool IsOpenRequested, IsFullyOpen, IsFullyClosed = true;
    public bool SafetyBlocked;
    public int toggles;
    public void SetOpen(bool value) { IsOpenRequested = value; }
    public void SetOpenImmediately(bool value) { IsOpenRequested = IsFullyOpen = value; IsFullyClosed = !value; }
    public void ToggleFromExternal() { toggles++; SetOpen(!IsOpenRequested); }
    public void CloseWithSafetyBarrier(float duration) { SafetyBlocked = true; SetOpen(false); }
}
public static class CameraCircularVision { public static void NotifyBlockersChanged() {} }
public static class BridgeRegression {
    static int checks;
    static void Check(bool value, string message) { checks++; if (!value) throw new System.Exception(message); }
    static void Field(object o, string name, object value) { o.GetType().GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(o, value); }
    static void Tick(object o, string name) { o.GetType().GetMethod(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(o, null); }
    public static int Run() {
        var c = new RotatingBridgeMechanism(); var hinge = new DoorHingeInteraction();
        var doors = new DoubleSlidingDoor[4];
        Field(c, "bridge", hinge);
        for (int i=0;i<4;i++) { doors[i] = new DoubleSlidingDoor(); Field(c, "mechanism"+(i+1), doors[i]); }
        Tick(c,"Start");
        Check(!c.MapUsesRotatedLayout, "Initial map uses original bridge layout");
        Check(doors[0].IsFullyOpen && doors[2].IsFullyOpen, "Initial 1/3 open");
        Check(doors[1].IsFullyClosed && doors[3].IsFullyClosed, "Initial 2/4 closed");
        var target = new UnityEngine.GameObject(); target.components[typeof(RotatingBridgeMechanism)] = c; target.components[typeof(DoorHingeInteraction)] = hinge;
        for(int cycle=0;cycle<6;cycle++) {
            ApplyLinkedObjectChange(target);
            Check(hinge.toggles == cycle+1, "Dispatch must rotate once, not twice");
            Check(c.MapUsesRotatedLayout == (cycle % 2 != 0), "Map retains previous endpoint throughout rotation");
            foreach(var door in doors) Check(door.SafetyBlocked && !door.IsOpenRequested, "Immediately blocked before rotation");
            if(cycle == 0) Check(doors[0].IsFullyOpen, "Visible leaf must not snap closed");
            // Variable-duration motion / pause: no timer may open passages early.
            for(int frame=0;frame<10;frame++) { Tick(c,"LateUpdate"); ApplyLinkedObjectChange(target); }
            Check(hinge.toggles == cycle+1, "Repeated pulls must not reverse moving bridge");
            foreach(var door in doors) {
                Check(door.SafetyBlocked && !door.IsOpenRequested, "Blocked throughout motion");
                door.IsFullyClosed = true; door.IsFullyOpen = false;
            }
            hinge.IsChangingOpenState = false; Tick(c,"LateUpdate");
            Check(c.MapUsesRotatedLayout == hinge.IsOpen, "Map switches only at completed endpoint");
            Check(doors[0].IsOpenRequested == !hinge.IsOpen && doors[2].IsOpenRequested == !hinge.IsOpen, "1/3 follow hinge end");
            Check(doors[1].IsOpenRequested == hinge.IsOpen && doors[3].IsOpenRequested == hinge.IsOpen, "2/4 follow hinge end");
        }
        hinge.IsOpen = true; hinge.IsChangingOpenState = false; Tick(c,"LateUpdate");
        Check(!doors[0].IsOpenRequested && doors[1].IsOpenRequested, "Saved open hinge restores group B");
        hinge.IsOpen = false; Tick(c,"LateUpdate");
        Check(doors[0].IsOpenRequested && !doors[1].IsOpenRequested, "Saved closed hinge restores group A");
        doors[0].IsFullyClosed = false;
        ApplyLinkedObjectChange(target); hinge.IsChangingOpenState = false;
        Tick(c,"LateUpdate");
        Check(!doors[1].IsOpenRequested, "Fast bridge waits for closing animation before reopening");
        ApplyLinkedObjectChange(target); Check(hinge.toggles == 7, "Closing phase is also interlocked");
        doors[0].IsFullyClosed = true; Tick(c,"LateUpdate");
        Check(doors[1].IsOpenRequested, "Reopen after both rotation and closing finish");
        c.isActiveAndEnabled = false; c.ToggleFromExternal(); Check(hinge.toggles == 7, "Disabled controller does not rotate");
        var normalDoor = new UnityEngine.GameObject(); var normalHinge = new DoorHingeInteraction(); normalDoor.components[typeof(DoorHingeInteraction)] = normalHinge;
        ApplyLinkedObjectChange(normalDoor); Check(normalHinge.toggles == 1, "Ordinary hinged door behavior unchanged");
        var slidingObject = new UnityEngine.GameObject(); var sliding = new DoubleSlidingDoor(); slidingObject.components[typeof(DoubleSlidingDoor)] = sliding;
        ApplyLinkedObjectChange(slidingObject); Check(sliding.toggles == 1, "Ordinary sliding door behavior unchanged");
        var generic = new UnityEngine.GameObject(); ApplyLinkedObjectChange(generic); Check(!generic.activeSelf, "Generic linked object behavior unchanged");
        ApplyLinkedObjectChange(null);
        return checks;
    }
'@
Add-Type -TypeDefinition ("#pragma warning disable 0649`n" + $controller + "`n" + $stubs + "`n" + $dispatch + "`n}")
Write-Output ("Bridge logic: {0} assertions passed." -f [BridgeRegression]::Run())

$scene = Get-Content -Raw "$root/Assets/Scenes/Level1/Level1-Floor2.unity"
$ids = [regex]::Matches($scene, '(?m)^--- !u!\d+ &(\d+)') | ForEach-Object { $_.Groups[1].Value }
if (@($ids | Group-Object | Where-Object Count -gt 1).Count) { throw 'Duplicate scene object IDs' }
$blocks = [regex]::Split($scene, '(?m)(?=^--- !u!)')
$levers = @($blocks | Where-Object { $_ -match '^--- !u!1001' -and $_ -match 'guid: d37f0b1ff829dac44bde786e615a70f8' })
if ($levers.Count -ne 3) { throw 'Keep exactly the three existing levers; no southern lever' }
foreach ($b in $levers) {
    if ($b -notmatch 'propertyPath: linkedObject\s+value:\s+objectReference: \{fileID: 607445785\}') { throw 'Lever not linked to pivot' }
}
$instances = @(1453853502,1347815659,1034879667,1432766864)
for ($i=0;$i -lt 4;$i++) {
    $id = $instances[$i]
    $b = $blocks | Where-Object { $_ -match "^--- !u!1001 &$id\r?\n" }
    $expected = if ($i % 2 -eq 0) { 1 } else { 0 }
    if ($b -notmatch "propertyPath: open\s+value: $expected\s") { throw "Wrong initial state for mechanism $($i+1)" }
    $ref = 2107000001 + $i
    $binding = $blocks | Where-Object { $_ -match "^--- !u!114 &$ref stripped" }
    if ($binding -notmatch "m_PrefabInstance: \{fileID: $id\}") { throw 'Incorrect mechanism component binding' }
}
Write-Output 'Scene validation passed: original three levers, four mechanism bindings, initial states, unique IDs.'
