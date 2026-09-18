$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$source = Get-Content -Raw "$root/Assets/Scripts/Map/MapPointOfInterest.cs"
$source = $source.Substring(0, $source.IndexOf('public sealed class MapPointOfInterestUiInteraction'))
$source = $source.Replace('using UnityEngine.EventSystems;', '').Replace('using UnityEngine.UI;', '')
$stubs = @'
namespace UnityEngine {
    public class SerializeField : Attribute {}
    public class Tooltip : Attribute {public Tooltip(string s){}}
    public class TextArea : Attribute {public TextArea(int a,int b){}}
    public class Min : Attribute {public Min(float f){}}
    public class RangeAttribute : Attribute {public RangeAttribute(float a,float b){}}
    public class DefaultExecutionOrder : Attribute {public DefaultExecutionOrder(int n){}}
    public enum RuntimeInitializeLoadType {BeforeSceneLoad}
    public class RuntimeInitializeOnLoadMethod : Attribute {public RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType t){}}
    public struct Vector2 {
        public float x,y;
        public static Vector2 zero {get{return new Vector2();}}
        public static implicit operator Vector2(Vector3 v){return new Vector2{x=v.x,y=v.y};}
    }
    public struct Vector3 {public float x,y,z; public Vector3(float a,float b,float c){x=a;y=b;z=c;}}
    public struct Color {}
    public struct Color32 {
        public Color32(byte a,byte b,byte c,byte d){}
        public static implicit operator Color(Color32 c){return new Color();}
    }
    public struct Bounds {}
    public class Transform {public Vector3 position; public string StableId;}
    public class Scene {public string name;}
    public class MonoBehaviour {
        public GameObject gameObject; public Transform transform {get{return gameObject.transform;}}
        public T GetComponent<T>() where T:MonoBehaviour {return gameObject.GetComponent<T>();}
        public static void Destroy(object o){} public static void DontDestroyOnLoad(object o){}
    }
    public class GameObject {
        public string name; public Scene scene; public Transform transform=new Transform();
        Dictionary<Type,MonoBehaviour> components=new Dictionary<Type,MonoBehaviour>();
        public GameObject(string s){name=s;}
        public T GetComponent<T>() where T:MonoBehaviour {MonoBehaviour c;return components.TryGetValue(typeof(T),out c)?(T)c:null;}
        public T AddComponent<T>() where T:MonoBehaviour,new(){
            var c=new T{gameObject=this};components[typeof(T)]=c;
            var method=typeof(T).GetMethod("Awake",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            if(method!=null)method.Invoke(c,null);return c;
        }
    }
    public class Camera:MonoBehaviour {
        public static Camera main;
        public Vector3 viewport=new Vector3(0.5f,0.5f,1f);
        public Vector3 WorldToViewportPoint(Vector3 p){return viewport;}
    }
    public class SpriteRenderer:MonoBehaviour {public object sprite=new object();public Bounds bounds;}
    public static class Time {public static float unscaledTime;}
    public static class Mathf {public static float Max(float a,float b){return Math.Max(a,b);}}
}
public class LeverData:UnityEngine.MonoBehaviour {}
public class ZeldaFourWayMover:UnityEngine.MonoBehaviour {}
public class CameraCircularVision:UnityEngine.MonoBehaviour {
    public bool isActiveAndEnabled=true,insideRadius=true,unoccluded=true;
    public bool IsWorldBoundsVisible(UnityEngine.Bounds b,UnityEngine.Transform t){return insideRadius&&unoccluded;}
    public bool IsWorldPositionVisible(UnityEngine.Vector3 p,UnityEngine.Transform t){return insideRadius&&unoccluded;}
}
public static class ZeldaRuntimeRegistry {
    public static ZeldaFourWayMover Controlled;
    public static ZeldaFourWayMover GetControlledMover(){return Controlled;}
    public static UnityEngine.Scene GetGameplayScene(UnityEngine.GameObject g){return g.scene;}
}
public static class GameSaveSystem {public static bool IsLoading;}
public static class SceneTravelStateManager {public static string GetMapObjectId(UnityEngine.Transform t){return t.StableId;}}
public static class DiscoveryRegression {
    static int checks;
    static void Check(bool b,string message){checks++;if(!b)throw new Exception(message);}
    static void Update(MapPointOfInterest p){UnityEngine.Time.unscaledTime+=1f;typeof(MapPointOfInterest).GetMethod("Update",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(p,null);}
    static MapPointOfInterest Lever(UnityEngine.Scene scene,string id){
        var g=new UnityEngine.GameObject("Lever"){scene=scene};g.transform.StableId=id;
        g.AddComponent<LeverData>();g.AddComponent<UnityEngine.SpriteRenderer>();return g.AddComponent<MapPointOfInterest>();
    }
    public static int Run(){
        var scene=new UnityEngine.Scene{name="Floor2"};
        var cameraObject=new UnityEngine.GameObject("Camera"){scene=scene};
        var camera=cameraObject.AddComponent<UnityEngine.Camera>();UnityEngine.Camera.main=camera;
        var vision=cameraObject.AddComponent<CameraCircularVision>();
        var playerObject=new UnityEngine.GameObject("Player"){scene=scene};
        var player=playerObject.AddComponent<ZeldaFourWayMover>();ZeldaRuntimeRegistry.Controlled=player;
        var lever=Lever(scene,"Mechanism@0/Lever@0");var second=Lever(scene,"Mechanism@0/Lever@1");
        var manager=MapPointOfInterestManager.GetOrCreate();
        Check(lever.IconShape==MapPointIconShape.Lever && lever.Title=="拉杆","Simple lever icon configured automatically");
        Check(lever.PointId!=second.PointId,"Prefab instances have separate stable discovery IDs");
        Check(manager.GetDiscoveredPoints(scene.name).Count==0,"All undiscovered levers hidden");
        vision.insideRadius=false;Update(lever);Check(!manager.IsDiscovered(lever.PointId),"Outside vision radius hidden");
        vision.insideRadius=true;vision.unoccluded=false;Update(lever);Check(!manager.IsDiscovered(lever.PointId),"Behind walls hidden despite being on screen");
        vision.unoccluded=true;camera.viewport.x=1.2f;Update(lever);Check(!manager.IsDiscovered(lever.PointId),"Outside viewport hidden");
        camera.viewport.x=0.5f;GameSaveSystem.IsLoading=true;Update(lever);Check(!manager.IsDiscovered(lever.PointId),"Loading cannot discover from temporary camera state");
        GameSaveSystem.IsLoading=false;ZeldaRuntimeRegistry.Controlled=null;Update(lever);Check(!manager.IsDiscovered(lever.PointId),"No controlled character cannot discover");
        ZeldaRuntimeRegistry.Controlled=player;Update(lever);Check(manager.IsDiscovered(lever.PointId),"First actual sight discovers lever");
        Check(manager.GetDiscoveredPoints(scene.name).Count==1,"Only seen lever appears");
        MapPointOfInterestRecord record;manager.TryGetPoint(lever.PointId,out record);
        Check(record.TooltipText=="拉杆","Lever hover shows name only, without description");
        Check(!manager.ToggleMarked(lever.PointId) && !record.Marked,"Lever cannot be selected/marked");
        record.SetMarked(true);
        Check(!record.Marked,"Legacy saved lever marks are ignored");
        Check(manager.GetDiscoveredPoints("OtherFloor").Count==0,"Map scene filtering preserved");
        vision.unoccluded=false;Update(lever);Check(manager.IsDiscovered(lever.PointId),"Leaving sight never hides discovered icon");
        var saved=manager.CaptureSaveState();manager.ResetAllPoints();
        Check(manager.GetDiscoveredPoints(scene.name).Count==0,"New game clears discovery");
        manager.ApplySaveState(saved);Check(manager.IsDiscovered(lever.PointId),"Save reload restores discovery");
        var revisited=Lever(scene,"Mechanism@0/Lever@0");Check(revisited.PointId==lever.PointId && manager.IsDiscovered(revisited.PointId),"Scene recreation preserves discovered lever");
        manager.ApplySaveState(null);Update(revisited);Check(!manager.IsDiscovered(revisited.PointId),"Other save does not inherit discovery");
        var otherScene=new UnityEngine.Scene{name="OtherFloor"};var remote=Lever(otherScene,"Mechanism@0/Lever@0");
        vision.unoccluded=true;Update(remote);Check(!manager.IsDiscovered(remote.PointId),"Uncontrolled additive scene cannot discover");
        var ordinaryObject=new UnityEngine.GameObject("Other point"){scene=scene};
        var ordinary=ordinaryObject.AddComponent<MapPointOfInterest>();ordinary.ChangeInformation("Other","Details");ordinary.ForceDiscover();
        MapPointOfInterestRecord ordinaryRecord;manager.TryGetPoint(ordinary.PointId,out ordinaryRecord);
        Check(manager.ToggleMarked(ordinary.PointId) && ordinaryRecord.Marked,"Other map points retain click-to-mark");
        Check(ordinaryRecord.TooltipText=="Other\nDetails","Other map point descriptions unchanged");
        return checks;
    }
}
'@
Add-Type -TypeDefinition ("#pragma warning disable 0649`n"+$source+"`n"+$stubs)
Write-Output ("Lever discovery: {0} assertions passed." -f [DiscoveryRegression]::Run())
$save=Get-Content -Raw "$root/Assets/Scripts/SceneManagement/GameSaveSystem.cs"
if ($save -notmatch 'mapPoints = MapPointOfInterestManager.GetOrCreate\(\).CaptureSaveState\(\)' -or $save -notmatch 'ApplySaveState\(data.mapPoints\)') {throw 'Missing save/load discovery integration'}
$leverSource=Get-Content -Raw "$root/Assets/Scripts/Interaction/LeverData.cs"
if ($leverSource -notmatch 'gameObject.AddComponent<MapPointOfInterest>\(\)') {throw 'Missing automatic attachment for existing levers'}
Write-Output 'Existing prefab attachment and save-slot integration verified.'
