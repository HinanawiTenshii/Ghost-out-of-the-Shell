# Runs extracted production methods with data stubs, not Unity Play Mode.
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
function Method($source,$signature) {
    $start=$source.IndexOf($signature)
    if($start -lt 0){throw "Missing $signature"}
    $brace=$source.IndexOf('{',$start);$end=$brace+1;$depth=1
    while($depth -gt 0){if($source[$end] -eq '{'){$depth++};if($source[$end] -eq '}'){$depth--};$end++}
    $source.Substring($start,$end-$start)
}
$registry=Get-Content -Raw "$root/Assets/Scripts/Characters/ZeldaRuntimeRegistry.cs"
$effect=Get-Content -Raw "$root/Assets/Scripts/Items/GrowthCollectiblePickupEffect.cs"
$streaming=Get-Content -Raw "$root/Assets/Scripts/Camera/CameraVisionObjectStreaming.cs"
$region=Get-Content -Raw "$root/Assets/Scripts/Camera/CameraVisionStreamingRegion.cs"
$bomb=Get-Content -Raw "$root/Assets/Scripts/Items/PlacedBomb.cs"
$stubs=@'
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace UnityEngine.SceneManagement {
    public struct Scene {
        public string name; public Scene(string n){name=n;}
        public bool IsValid(){return name!=null;}
        public static bool operator ==(Scene a,Scene b){return a.name==b.name;}
        public static bool operator !=(Scene a,Scene b){return !(a==b);}
        public override bool Equals(object o){return o is Scene && this==(Scene)o;}
        public override int GetHashCode(){return name==null?0:name.GetHashCode();}
    }
    public static class SceneManager {
        public static Scene active=new Scene("Level1-Floor-1");
        public static Scene GetActiveScene(){return active;}
    }
}
namespace UnityEngine {
    public class RequireComponent:Attribute {public RequireComponent(Type t){}}
    public enum RuntimeInitializeLoadType {SubsystemRegistration}
    public class RuntimeInitializeOnLoadMethod:Attribute {public RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType t){}}
    public struct Vector3 {
        public float x,y,z; public Vector3(float x,float y,float z=0){this.x=x;this.y=y;this.z=z;}
        public static Vector3 zero {get{return new Vector3();}}
    }
    public struct Bounds {
        public Vector3 center,extents;
        public Bounds(Vector3 c,Vector3 size){center=c;extents=new Vector3(size.x/2,size.y/2,size.z/2);}
    }
    public static class Mathf {
        public static float Max(float a,float b){return Math.Max(a,b);}
        public static float Abs(float a){return Math.Abs(a);}
    }
    public static class Time {public static float unscaledTime=20;}
    public class Transform {
        public GameObject gameObject; public Vector3 position; public Transform visual;
        public Transform Find(string n){return visual;}
        public T GetComponent<T>() where T:class {return gameObject.GetComponent<T>();}
    }
    public class GameObject {
        public Scene scene=SceneManager.active; public bool activeSelf=true;public Transform transform;
        public List<Component> components=new List<Component>();
        public GameObject(){transform=new Transform {gameObject=this};}
        public void SetActive(bool value){activeSelf=value;}
        public T GetComponent<T>() where T:class {foreach(var c in components)if(c is T)return c as T;return null;}
        public T Add<T>() where T:Component,new(){var c=new T {gameObject=this};components.Add(c);return c;}
    }
    public class Component {
        public GameObject gameObject; public Transform transform {get{return gameObject.transform;}}
        public bool enabled=true;public bool isActiveAndEnabled {get{return enabled&&gameObject.activeSelf;}}
        public T GetComponent<T>() where T:class {return gameObject.GetComponent<T>();}
    }
    public class MonoBehaviour:Component {
        protected static T[] FindObjectsOfType<T>(){return CameraVisionObjectStreaming.instances.ToArray() as T[];}
    }
    public class SpriteRenderer:Component {public object sprite;public Bounds bounds;}
}
public class CameraVisionStreamingExempt:MonoBehaviour {}
public class ZeldaFourWayMover:MonoBehaviour {}
public class ZeldaCharacterData:MonoBehaviour {public bool IsDead;}
public static class ZeldaRuntimeRegistry {
    public static ZeldaFourWayMover target;
    public static ZeldaFourWayMover GetControlledMover(){return target;}
'@
$stubs+=(Method $registry '    public static Scene GetGameplayScene')+"`n}`n"
$stubs+=($region -replace '(?m)^using [^;]+;\r?\n','')
$stubs+=@'
public class PickupHarness:MonoBehaviour {
    private ZeldaFourWayMover cachedTarget;private ZeldaCharacterData targetData;
    private SpriteRenderer targetVisual;private Vector3 origin=new Vector3(0,0,3);
    public bool Query(out Vector3 p,out bool changed){return TryGetTarget(out p,out changed);}
'@
$stubs+=(Method $effect '    private bool TryGetTarget')+"`n}`n"
$stubs+=@'
public class CameraVisionObjectStreaming:MonoBehaviour {
    public static List<CameraVisionObjectStreaming> instances=new List<CameraVisionObjectStreaming>();
    private bool enableVisionStreaming=true;
    public class ManagedObject {
        public GameObject gameObject;public Bounds localBounds;public float outsideTimer,lastVisibilityCheckTime;
        public bool suspendedByManager;
    }
    public List<ManagedObject> managedObjects=new List<ManagedObject>();
    private static Bounds TransformLocalBoundsToWorld(Transform t,Bounds b){
        b.center=new Vector3(b.center.x+t.position.x,b.center.y+t.position.y,b.center.z+t.position.z);return b;
    }
'@
$stubs+=(Method $streaming '    public static void RefreshRetentionRegionsNow')
$stubs+=(Method $streaming '    private void RestoreObjectsInRetentionRegions')+"`n}`n"
$tests=@'
public static class ItemStreamingTests {
    static int checks;
    static Scene level=new Scene("Level1-Floor-1"),other=new Scene("Level2-Floor1");
    static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
    static void Invoke(object o,string method){o.GetType().GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(o,null);}
    static CameraVisionStreamingRegion Region(float x,float y,float radius,Scene scene){
        var o=new GameObject {scene=scene};o.transform.position=new Vector3(x,y);
        var r=o.Add<CameraVisionStreamingRegion>();Invoke(r,"OnEnable");r.Configure(radius);return r;
    }
    static void Remove(CameraVisionStreamingRegion r){Invoke(r,"OnDisable");r.enabled=false;}
    static Bounds At(float x,float y,float width=0,float height=0,float z=0){return new Bounds(new Vector3(x,y,z),new Vector3(width,height));}
    static CameraVisionObjectStreaming.ManagedObject Managed(float x,bool suspended,bool active,Scene scene){
        return new CameraVisionObjectStreaming.ManagedObject {gameObject=new GameObject {scene=scene,activeSelf=active},
            localBounds=At(x,0,1,1),suspendedByManager=suspended,outsideTimer=8};
    }
    public static int Run(){
        var fx=new GameObject {scene=level}.Add<PickupHarness>();
        var player=new GameObject {scene=new Scene("DontDestroyOnLoad")};
        var mover=player.Add<ZeldaFourWayMover>();var data=player.Add<ZeldaCharacterData>();
        player.transform.position=new Vector3(2,4,99);ZeldaRuntimeRegistry.target=mover;
        Vector3 p;bool changed;
        Check(fx.Query(out p,out changed)&&!changed,"Persistent traveling player remains a valid pickup target");
        Check(p.x==2&&p.y==4&&p.z==3,"Target position uses collectible depth");
        SceneManager.active=other;
        Check(!fx.Query(out p,out changed)&&changed,"Actual travel cancels old-level particles");
        SceneManager.active=level;player.scene=level;
        Check(fx.Query(out p,out changed)&&!changed,"Scene-local player still works");
        player.scene=other;
        Check(!fx.Query(out p,out changed)&&changed,"Other additive level cannot become the pickup target");
        var possessed=new GameObject {scene=level};var second=possessed.Add<ZeldaFourWayMover>();
        possessed.Add<ZeldaCharacterData>();ZeldaRuntimeRegistry.target=second;
        Check(fx.Query(out p,out changed)&&!changed,"Possession switches homing target in the same level");
        ZeldaRuntimeRegistry.target=null;
        Check(!fx.Query(out p,out changed)&&!changed,"Possession gap does not falsely report scene travel");
        player.scene=level;ZeldaRuntimeRegistry.target=mover;data.IsDead=true;
        Check(!fx.Query(out p,out changed)&&!changed,"Dead player rejected without scene mismatch");
        data.IsDead=false;mover.enabled=false;
        Check(!fx.Query(out p,out changed),"Disabled mover rejected");mover.enabled=true;
        var manager=new GameObject().Add<CameraVisionObjectStreaming>();CameraVisionObjectStreaming.instances.Add(manager);
        var near=Managed(4,true,false,level);var puzzle=Managed(3,false,false,level);
        var far=Managed(20,true,false,level);var foreign=Managed(4,true,false,other);
        var active=Managed(2,false,true,level);
        manager.managedObjects.AddRange(new[]{near,puzzle,far,foreign,active});
        var bomb=Region(0,0,5,level);
        Check(near.gameObject.activeSelf&&!near.suspendedByManager,"Placement wakes already-streamed targets synchronously");
        Check(near.outsideTimer==0&&near.lastVisibilityCheckTime==Time.unscaledTime,"Wake resets unloading timer");
        Check(!puzzle.gameObject.activeSelf,"Puzzle-disabled objects are not forced open");
        Check(far.suspendedByManager&&!far.gameObject.activeSelf,"Distant objects stay suspended");
        Check(foreign.suspendedByManager&&!foreign.gameObject.activeSelf,"Other loaded scene stays untouched");
        Check(active.outsideTimer==0&&active.gameObject.activeSelf,"Active targets keep their simulation alive");
        Check(CameraVisionStreamingRegion.Intersects(level,At(3,4)),"Circle includes exact boundary");
        Check(!CameraVisionStreamingRegion.Intersects(level,At(4,4)),"Does not use a square attraction radius");
        Check(CameraVisionStreamingRegion.Intersects(level,At(6,0,2,2)),"Bounds crossing region edge retained even if pivot is outside");
        Check(CameraVisionStreamingRegion.Intersects(level,At(0,0,0,0,100)),"2D retention ignores sprite depth");
        Check(!CameraVisionStreamingRegion.Intersects(other,At(0,0)),"Region is scoped to gameplay scene");
        var secondBomb=Region(4,0,5,level);Remove(bomb);
        Check(CameraVisionStreamingRegion.Intersects(level,At(1,0)),"One bomb ending does not release overlap of another");
        Check(!CameraVisionStreamingRegion.Intersects(level,At(-3,0)),"Region released outside surviving bomb");
        var damageLease=Region(4,0,5,level);Remove(secondBomb);
        Check(CameraVisionStreamingRegion.Intersects(level,At(4,0)),"Explosion damage lease survives bomb destruction until physics resolves");
        Remove(damageLease);
        Check(!CameraVisionStreamingRegion.HasActiveRegions&&!CameraVisionStreamingRegion.Intersects(level,At(4,0)),"All leases released on disable/destruction");
        var chained=Region(30,0,12,level);
        Check(far.gameObject.activeSelf&&!far.suspendedByManager,"New chain-explosion region immediately wakes its expanded neighborhood");
        Remove(chained);
        return checks;
    }
}
'@
Add-Type -TypeDefinition ($stubs+$tests)
$count=[ItemStreamingTests]::Run()
# Integration assertions: ordinary and super bombs share this path; no prefab tuning required.
if(!$bomb.Contains('Mathf.Max(investigationRadius, explosionSize.magnitude * 0.5f)')){throw 'Missing damage/attraction radius union'}
if(!$bomb.Contains('hitboxObject.AddComponent<CameraVisionStreamingRegion>().Configure(StreamingRetentionRadius);')){throw 'Missing explosion handoff'}
$explode=Method $bomb '    private void Explode()'
$refreshIndex=$explode.IndexOf('RefreshStreamingRetention();')
if($refreshIndex -lt 0 -or $refreshIndex -gt $explode.IndexOf('NotifyNearbyAi')){throw 'AI notification precedes wake-up'}
if(!(Method $bomb '    public void RestoreSavedOwner').Contains('RefreshStreamingRetention();')){throw 'Restored bomb config does not refresh retention'}
if(!$streaming.Contains('CameraVisionStreamingRegion.Intersects(managedObject.gameObject.scene, worldBounds)')){throw 'Streaming unload path ignores retention regions'}
Write-Output "PASS: $count pickup-target/region-lifecycle regression checks plus bomb/streaming integration guards."
