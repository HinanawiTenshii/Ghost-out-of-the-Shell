$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$source=Get-Content -Raw "$root/Assets/Scripts/Interaction/SpeedBoostArea.cs"
$fixture=@'
namespace UnityEngine {
 public class ExecuteAlways:System.Attribute{} public class DisallowMultipleComponent:System.Attribute{}
 public class RequireComponent:System.Attribute{public RequireComponent(System.Type a,System.Type b){}}
 public class AddComponentMenu:System.Attribute{public AddComponentMenu(string s){}}
 public class SerializeField:System.Attribute{}
 public enum RuntimeInitializeLoadType{SubsystemRegistration}
 public class RuntimeInitializeOnLoadMethod:System.Attribute{public RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType t){}}
 public static class Application{public static bool isPlaying=true;}
 public struct Vector2 {
  public float x,y;public Vector2(float a,float b){x=a;y=b;}
  public static Vector2 zero{get{return new Vector2();}}
  public static Vector2 operator +(Vector2 a,Vector2 b){return new Vector2(a.x+b.x,a.y+b.y);}
 }
 public struct Bounds{}
 public struct Matrix4x4{}
 public struct Color{public float r,g,b,a;public Color(float r,float g,float b,float a){this.r=r;this.g=g;this.b=b;this.a=a;}}
 public class Scene{}
 public class Transform{public Vector2 position,scale=new Vector2(1,1);public float angle;public Matrix4x4 localToWorldMatrix;}
 public class GameObject{
  public Scene scene;public bool active=true;public Transform transform=new Transform();public BoxCollider2D box;
  public bool activeInHierarchy{get{return active;}}
 }
 public class Component{
  public GameObject gameObject=new GameObject();
  public Transform transform{get{return gameObject.transform;}}
  public T GetComponent<T>() where T:class{return gameObject.box as T;}
 }
 public class MonoBehaviour:Component{
  public bool enabled=true;
  public bool isActiveAndEnabled{get{return enabled&&gameObject.active;}}
 }
 public class BoxCollider2D:MonoBehaviour{
  public bool isTrigger;public Vector2 size,offset;public Bounds bounds;
  public bool OverlapPoint(Vector2 point){
   double r=transform.angle*System.Math.PI/180;
   float x=point.x-transform.position.x,y=point.y-transform.position.y;
   float localX=(float)(x*System.Math.Cos(r)+y*System.Math.Sin(r))/transform.scale.x-offset.x;
   float localY=(float)(-x*System.Math.Sin(r)+y*System.Math.Cos(r))/transform.scale.y-offset.y;
   return System.Math.Abs(localX)<=size.x*.5f+.00001f && System.Math.Abs(localY)<=size.y*.5f+.00001f;
  }
 }
 public static class Mathf{public static float Max(float a,float b){return System.Math.Max(a,b);}public static float Min(float a,float b){return System.Math.Min(a,b);}}
 public static class Gizmos{public static Matrix4x4 matrix;public static Color color;public static void DrawCube(Vector2 a,Vector2 b){}public static void DrawWireCube(Vector2 a,Vector2 b){}}
}
public class CameraVisionStreamingExempt:UnityEngine.MonoBehaviour{}
public class ZeldaFourWayMover:UnityEngine.MonoBehaviour{}
public static class ZeldaRuntimeRegistry{
 public static ZeldaFourWayMover player;
 public static ZeldaFourWayMover GetControlledMover(){return player;}
 public static UnityEngine.Scene GetGameplayScene(UnityEngine.GameObject go){return go.scene;}
}
public static class SpeedBoostAreaTests{
 static int checks;
 static void Check(bool b,string s){checks++;if(!b)throw new System.Exception(s);}
 static void Call(object o,string name){o.GetType().GetMethod(name,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(o,null);}
 static void Set(object o,string name,object value){o.GetType().GetField(name,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(o,value);}
 static SpeedBoostArea Make(UnityEngine.Scene scene){
  var area=new SpeedBoostArea();area.gameObject.scene=scene;
  area.gameObject.box=new UnityEngine.BoxCollider2D{gameObject=area.gameObject};Call(area,"OnEnable");return area;
 }
 public static int Run(){
  var scene=new UnityEngine.Scene();var other=new UnityEngine.Scene();
  var p=new ZeldaFourWayMover();p.gameObject.scene=scene;ZeldaRuntimeRegistry.player=p;
  Check(SpeedBoostArea.GetSpeedMultiplier(p)==1,"No area");
  var a=Make(scene);Check(a.gameObject.box.isTrigger,"Area is non-solid");
  Check(a.AreaSize.x==4 && a.AreaSize.y==3,"Matches permission area defaults");
  Check(SpeedBoostArea.GetSpeedMultiplier(p)==2,"Inside doubles player speed");
  for(int i=0;i<10;i++)Check(SpeedBoostArea.GetSpeedMultiplier(p)==2,"Repeated queries do not accumulate");
  p.transform.position=new UnityEngine.Vector2(2,1.5f);Check(SpeedBoostArea.GetSpeedMultiplier(p)==2,"Boundary included");
  p.transform.position=new UnityEngine.Vector2(2.01f,1.5f);Check(SpeedBoostArea.GetSpeedMultiplier(p)==1,"Leave immediately restores");
  p.transform.position=UnityEngine.Vector2.zero;var b=Make(scene);Check(SpeedBoostArea.GetSpeedMultiplier(p)==2,"Overlap stays 2x");
  Call(a,"OnDisable");Check(SpeedBoostArea.GetSpeedMultiplier(p)==2,"One overlap removed still boosts");
  b.enabled=false;Check(SpeedBoostArea.GetSpeedMultiplier(p)==1,"Disabled component cannot boost");b.enabled=true;
  b.gameObject.box.enabled=false;Check(SpeedBoostArea.GetSpeedMultiplier(p)==1,"Disabled collider cannot boost");b.gameObject.box.enabled=true;
  b.gameObject.active=false;Check(SpeedBoostArea.GetSpeedMultiplier(p)==1,"Inactive object cannot boost");b.gameObject.active=true;
  Call(b,"OnDestroy");Check(SpeedBoostArea.GetSpeedMultiplier(p)==1,"Destroyed area cleans registry");
  var remote=Make(other);Check(SpeedBoostArea.GetSpeedMultiplier(p)==1,"Additive other-floor area ignored");
  p.gameObject.scene=other;Check(SpeedBoostArea.GetSpeedMultiplier(p)==2,"Scene travel reevaluates immediately");
  var npc=new ZeldaFourWayMover();npc.gameObject.scene=other;
  npc.enabled=false;
  Check(SpeedBoostArea.GetSpeedMultiplier(npc)==2,"AI with disabled input mover is boosted");
  ZeldaRuntimeRegistry.player=npc;Check(SpeedBoostArea.GetSpeedMultiplier(npc)==2 && SpeedBoostArea.GetSpeedMultiplier(p)==2,"Possession does not remove boost from previous body");
  p.gameObject.scene=scene;ZeldaRuntimeRegistry.player=p;Call(remote,"OnDisable");
  a.enabled=true;Call(a,"OnEnable");Set(a,"areaOffset",new UnityEngine.Vector2(3,0));Call(a,"OnValidate");
  Check(SpeedBoostArea.GetSpeedMultiplier(p)==1,"Area offset changes coverage");
  p.transform.position=new UnityEngine.Vector2(3,0);Check(SpeedBoostArea.GetSpeedMultiplier(p)==2,"Offset region applies");
  a.transform.angle=90;a.transform.scale=new UnityEngine.Vector2(2,1);
  p.transform.position=new UnityEngine.Vector2(0,6);Check(SpeedBoostArea.GetSpeedMultiplier(p)==2,"Rotated scaled local region");
  p.transform.position=new UnityEngine.Vector2(2,6);Check(SpeedBoostArea.GetSpeedMultiplier(p)==1,"Rotation does not use oversized AABB");
  Set(a,"areaSize",new UnityEngine.Vector2(-1,0));Call(a,"OnValidate");Check(a.AreaSize.x==.1f && a.AreaSize.y==.1f,"Positive minimum size");
  p.transform.position=new UnityEngine.Vector2(0,6);p.enabled=false;
  Check(SpeedBoostArea.GetSpeedMultiplier(p)==2,"Disabled input component does not disable area effect");
  p.gameObject.active=false;Check(SpeedBoostArea.GetSpeedMultiplier(p)==1,"Unloaded body does not receive boost");
  Check(SpeedBoostArea.GetSpeedMultiplier(null)==1,"Missing actor");
  return checks;
 }
}
'@
Add-Type -TypeDefinition ($source+"`n"+$fixture)
Write-Output "PASS: $([SpeedBoostAreaTests]::Run()) production area assertions using collider/math stubs."
$mover=Get-Content -Raw "$root/Assets/Scripts/Characters/ZeldaFourWayMover.cs"
if($mover -match 'SpeedBoostArea.GetSpeedMultiplier'){throw 'Player must not apply area multiplier twice'}
$data=Get-Content -Raw "$root/Assets/Scripts/Characters/ZeldaCharacterData.cs"
if($data -notmatch 'public float MoveSpeed =>[^;]+SpeedBoostArea.GetSpeedMultiplier\(this\)'){throw 'Missing shared character speed integration'}
$ai=Get-Content -Raw "$root/Assets/Scripts/AI/ZeldaCharacterAiBase.cs"
if($ai -notmatch 'effectiveMoveDirection \* characterData.MoveSpeed'){throw 'AI must consume shared character speed'}
$prefab=Get-Content -Raw "$root/Assets/Prefabs/SpeedBoostArea.prefab"
if(!$prefab.Contains('70d5b8fec8204b669b0895a6e851fcca') -or !$prefab.Contains('076bd33f4dd04d1188f3cf494d301c92') -or !$prefab.Contains('m_IsTrigger: 1')){throw 'Prefab wiring incorrect'}
$editor=Get-Content -Raw "$root/Assets/Editor/SpeedBoostAreaEditor.cs"
foreach($required in @('BoxBoundsHandle','area.transform.localToWorldMatrix','Undo.RecordObject','areaSize','areaOffset')){
 if(!$editor.Contains($required)){throw "Missing permission-style editing: $required"}
}
Write-Output 'PASS: prefab, streaming exemption, shared player/AI movement hook and permission-style editor wiring. Not a Unity Play Mode test.'
