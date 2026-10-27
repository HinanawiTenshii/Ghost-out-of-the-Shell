# Run actual registry/ritual-query code without Unity, scenes, or player saves.
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$registry = Get-Content -Raw -LiteralPath "$taskRoot/Assets/Scripts/SceneManagement/AutoSaveTransientActivity.cs"
$circle = Get-Content -Raw -LiteralPath "$taskRoot/Assets/Scripts/Interaction/SummoningCircle.cs"
$ritualQuery = [regex]::Match($circle, '(?ms)^    public static bool HasActiveRitual\(.*?^    \}').Value
if (!$ritualQuery) { throw 'Production ritual query not found' }
$fixture = @'
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace UnityEngine.SceneManagement {
    public struct Scene {
        public int id;
        public bool isLoaded;
        public bool IsValid() => id != 0;
        public static bool operator ==(Scene a, Scene b) => a.id == b.id;
        public static bool operator !=(Scene a, Scene b) => a.id != b.id;
        public override bool Equals(object other) => other is Scene scene && scene == this;
        public override int GetHashCode() => id;
    }
}
namespace UnityEngine {
    public enum RuntimeInitializeLoadType { SubsystemRegistration }
    public class RuntimeInitializeOnLoadMethodAttribute : Attribute { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType type) { } }
    public class GameObject { public Scene scene; public bool activeInHierarchy = true; }
    public class Component {
        public GameObject gameObject;
        public bool destroyed;
        public static bool operator ==(Component a, Component b) {
            bool an = ReferenceEquals(a, null) || a.destroyed, bn = ReferenceEquals(b, null) || b.destroyed;
            return an || bn ? an == bn : ReferenceEquals(a, b);
        }
        public static bool operator !=(Component a, Component b) => !(a == b);
        public override bool Equals(object other) => ReferenceEquals(this, other);
        public override int GetHashCode() => base.GetHashCode();
    }
    public class Behaviour : Component { public bool enabled = true; public bool isActiveAndEnabled => enabled && gameObject.activeInHierarchy; }
}
public static class ZeldaRuntimeRegistry {
    public static Scene activeScene;
    public static Scene GetGameplayScene(GameObject go) => go.scene.id == 999 ? activeScene : go.scene;
}
public static class GameSaveSystem {
    public static void NotifyAutoSaveTransientAction(Component activity) { }
}
public class SummoningCircle : Behaviour {
    public static readonly HashSet<SummoningCircle> Instances = new HashSet<SummoningCircle>();
    public bool IsRitualActive;
    // PRODUCTION_RITUAL_QUERY
}
public static class TransientActivityFixture {
    private static int count;
    private static void Check(bool result, string label) { count++; if (!result) throw new Exception(label); }
    public static int Run() {
        var a = new Scene { id = 1, isLoaded = true };
        var b = new Scene { id = 2, isLoaded = true };
        ZeldaRuntimeRegistry.activeScene = a;
        Check(!AutoSaveTransientActivity.HasActivity(a), "empty registry");
        var projectile = new Behaviour { gameObject = new GameObject { scene = a } };
        AutoSaveTransientActivity.Register(projectile);
        AutoSaveTransientActivity.Register(projectile);
        Check(AutoSaveTransientActivity.HasActivity(a), "active projectile blocks");
        Check(!AutoSaveTransientActivity.HasActivity(b), "other scene is independent");
        projectile.enabled = false;
        Check(!AutoSaveTransientActivity.HasActivity(a), "disabled script does not block");
        projectile.enabled = true; projectile.gameObject.activeInHierarchy = false;
        Check(!AutoSaveTransientActivity.HasActivity(a), "inactive object does not block");
        projectile.gameObject.activeInHierarchy = true;
        AutoSaveTransientActivity.Unregister(projectile);
        Check(!AutoSaveTransientActivity.HasActivity(a), "single unregister removes duplicate registration");
        AutoSaveTransientActivity.Register(projectile); projectile.destroyed = true;
        Check(!AutoSaveTransientActivity.HasActivity(a), "destroyed key pruned");
        var set = (HashSet<Component>)typeof(AutoSaveTransientActivity).GetField("Activities", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        Check(set.Count == 0, "destroyed references released");
        var prefab = new Behaviour { gameObject = new GameObject() };
        AutoSaveTransientActivity.Register(prefab);
        Check(set.Count == 0, "prefab without valid scene ignored");
        var ritual = new SummoningCircle { gameObject = new GameObject { scene = a } };
        SummoningCircle.Instances.Add(ritual);
        Check(!AutoSaveTransientActivity.HasActivity(a), "dormant ritual does not block");
        ritual.IsRitualActive = true;
        Check(AutoSaveTransientActivity.HasActivity(a), "active ritual blocks");
        Check(!AutoSaveTransientActivity.HasActivity(b), "ritual is scene scoped");
        ritual.enabled = false;
        Check(!AutoSaveTransientActivity.HasActivity(a), "disabled ritual does not block");
        ritual.enabled = true; ritual.IsRitualActive = false;
        Check(!AutoSaveTransientActivity.HasActivity(a), "completed ritual stops blocking");
        var travelling = new Behaviour { gameObject = new GameObject { scene = new Scene { id = 999, isLoaded = true } } };
        AutoSaveTransientActivity.Register(travelling);
        Check(AutoSaveTransientActivity.HasActivity(a), "travelling attack belongs to active gameplay scene");
        AutoSaveTransientActivity.Unregister(travelling);
        AutoSaveTransientActivity.Register(null); AutoSaveTransientActivity.Unregister(null);
        Check(!AutoSaveTransientActivity.HasActivity(default(Scene)), "invalid scene and null lifecycle are safe");
        return count;
    }
}
'@
$fixture = $fixture.Replace('// PRODUCTION_RITUAL_QUERY', $ritualQuery)
$fixture += "`n" + [regex]::Replace($registry, '(?m)^using [^;]+;\r?\n', '')
Add-Type -TypeDefinition $fixture
$checks = [TransientActivityFixture]::Run()
Write-Output "AutoSaveTransientActivity ALL PASS $checks"

# Every enabled lifetime is registered once, without replacing existing cleanup.
$sources = @('Items/FortressBallistaBolt', 'Items/FortressCannonball', 'Items/DisposableHandgunBullet',
    'Items/ThrownGlassBottle', 'Items/MortarAirstrike', 'Characters/MeteorShowerSkillRuntime',
    'Characters/HolyLightSkillRuntime', 'Characters/ZeldaAttackHitbox')
foreach ($relative in $sources) {
    $source = Get-Content -Raw -LiteralPath "$taskRoot/Assets/Scripts/$relative.cs"
    if ([regex]::Matches($source, 'void OnEnable\(').Count -ne 1 -or [regex]::Matches($source, 'void OnDisable\(').Count -ne 1) {
        throw "Missing or duplicated lifecycle method: $relative"
    }
    if (!$source.Contains('AutoSaveTransientActivity.Register(this)') -or !$source.Contains('AutoSaveTransientActivity.Unregister(this)')) {
        throw "Missing registration: $relative"
    }
}
Write-Output 'AutoSaveTransientActivity lifecycle PASS 8 components'
