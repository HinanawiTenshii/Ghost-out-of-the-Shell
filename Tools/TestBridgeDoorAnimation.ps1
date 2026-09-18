$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$source = Get-Content -Raw "$root/Assets/Scripts/Interaction/DoubleSlidingDoor.cs"
$mapSource = Get-Content -Raw "$root/Assets/Scripts/Map/RuntimeMiniMapSceneData.cs"
$boxPathMethod = [regex]::Match($mapSource, '(?s)    public static Vector2\[\] CreateBoxPath.*(?=\r?\n\})').Value
# Run the production door class; stubs replace Unity rendering/physics plumbing only.
$harness = @'
namespace UnityEngine {
    public class SerializeField : System.Attribute {}
    public class Header : System.Attribute { public Header(string s) {} }
    public class Tooltip : System.Attribute { public Tooltip(string s) {} }
    public class Min : System.Attribute { public Min(float v) {} }
    public class ContextMenu : System.Attribute { public ContextMenu(string s) {} }
    public class AddComponentMenu : System.Attribute { public AddComponentMenu(string s) {} }
    public class ExecuteAlways : System.Attribute {}
    public class DisallowMultipleComponent : System.Attribute {}
    public enum HideFlags { NotEditable }
    public class Component {
        public GameObject gameObject;
        public Transform transform { get { return gameObject.transform; } }
        public T GetComponent<T>() where T:class { return null; }
    }
    public class MonoBehaviour : Component {
        public static void Destroy(object o) {} public static void DestroyImmediate(object o) {}
    }
    public class GameObject {
        public HideFlags hideFlags; public int layer; public Transform transform = new Transform();
        public GameObject(string name) {}
        public T AddComponent<T>() where T:Component,new() { return new T { gameObject=this }; }
    }
    public class Transform {
        public Vector3 localPosition, localScale, position; public Quaternion localRotation;
        public Matrix4x4 localToWorldMatrix; public Transform parent;
        public void SetParent(Transform p,bool keep) { parent=p; }
    }
    public struct Vector2 {
        public float x,y; public Vector2(float a,float b) { x=a;y=b; }
        public static Vector2 zero { get { return new Vector2(); } }
        public static implicit operator Vector2(Vector3 v) { return new Vector2(v.x,v.y); }
        public static Vector2 operator +(Vector2 a,Vector2 b) {return new Vector2(a.x+b.x,a.y+b.y);}
        public static Vector2 operator *(Vector2 a,float b) {return new Vector2(a.x*b,a.y*b);}
    }
    public struct Vector3 {
        public float x,y,z; public Vector3(float a,float b,float c) { x=a;y=b;z=c; }
        public static Vector3 zero { get { return new Vector3(); } }
        public static implicit operator Vector3(Vector2 v) {return new Vector3(v.x,v.y,0);}
    }
    public struct Color {
        public Color(float r,float g,float b,float a) {}
        public static Color cyan { get { return new Color(); } }
    }
    public struct Quaternion { public static Quaternion identity { get { return new Quaternion(); } } }
    public struct Matrix4x4 {
        public Vector3 MultiplyPoint3x4(Vector3 v) {return v;}
    }
    public struct Bounds {
        public Vector3 size; public Bounds(Vector3 p,Vector3 s) {size=s;} public void Encapsulate(Bounds b) {}
    }
    public class Sprite { public Bounds bounds; }
    public class SpriteRenderer : Component { public Sprite sprite; public Color color; public Bounds bounds; }
    public class BoxCollider2D : Component { public bool enabled,isTrigger; public Vector2 offset,size; public Bounds bounds; }
    public static class Application { public static bool isPlaying=true; }
    public static class Time { public static float deltaTime; }
    public static class Physics2D { public static int syncs; public static void SyncTransforms() {syncs++;} }
    public static class Gizmos { public static Matrix4x4 matrix; public static Color color; public static void DrawLine(Vector3 a,Vector3 b) {} }
    public static class Mathf {
        public static float Max(float a,float b) {return System.Math.Max(a,b);}
        public static float Clamp(float v,float a,float b) {return System.Math.Min(b,Max(v,a));}
        public static bool Approximately(float a,float b) {return System.Math.Abs(a-b)<0.00001f;}
        public static float MoveTowards(float a,float b,float delta) { return System.Math.Abs(b-a)<=delta ? b : a+System.Math.Sign(b-a)*delta; }
        public static float SmoothStep(float a,float b,float t) {return a+(b-a)*t*t*(3-2*t);}
    }
}
namespace UnityEngine.Events { public class UnityEvent { public int count; public void Invoke() {count++;} } }
public static class CameraCircularVision { public static void NotifyBlockersChanged(UnityEngine.Bounds b) {} }
public static class AnimationRegression {
    static int checks;
    static readonly System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
    static object Read(object o,string name) {return o.GetType().GetField(name,flags).GetValue(o);}
    static void Write(object o,string name,object value) {o.GetType().GetField(name,flags).SetValue(o,value);}
    static void Call(object o,string name) {o.GetType().GetMethod(name,flags).Invoke(o,null);}
    static void Check(bool b,string label) {checks++;if(!b)throw new System.Exception(label);}
    static void Advance(DoubleSlidingDoor d,float dt) {UnityEngine.Time.deltaTime=dt;Call(d,"Update");}
    public static int Run() {
        var d=new DoubleSlidingDoor(); d.gameObject=new UnityEngine.GameObject("Door");
        Write(d,"negativeSize",new UnityEngine.Vector2(2f,0.3f));
        Write(d,"positiveSize",new UnityEngine.Vector2(1f,0.2f));
        Call(d,"OnEnable"); d.SetOpenImmediately(true);
        d.CloseWithSafetyBarrier(0.25f);
        var barrier=(UnityEngine.BoxCollider2D)Read(d,"safetyBarrier");
        Check(barrier.enabled && !barrier.isTrigger,"Solid barrier immediately enabled");
        Check(UnityEngine.Physics2D.syncs>0,"Physics queries synchronized before animation");
        Check(d.IsFullyOpen && !d.IsOpenRequested,"Initial visual pose preserved when close begins");
        var mapClosed=new System.Collections.Generic.List<UnityEngine.Vector2[]>();
        var mapOpen=new System.Collections.Generic.List<UnityEngine.Vector2[]>();
        d.AppendMapEndpointPaths(false,mapClosed); d.AppendMapEndpointPaths(true,mapOpen);
        Check(mapClosed.Count==2 && mapOpen.Count==2,"Two endpoint rectangles per door");
        Check(mapClosed[0][0].x==-2f && mapClosed[1][2].x==1f,"Closed map geometry respects asymmetric leaves");
        Check(mapOpen[0][0].x<mapClosed[0][0].x && mapOpen[1][2].x>mapClosed[1][2].x,"Open map leaves retract in opposite directions");
        Check(d.IsFullyOpen,"Map generation never moves actual door");
        Check(barrier.size.x==3f && barrier.size.y==0.3f && barrier.offset.x==-0.5f,"Barrier covers asymmetric closed doorway");
        Check(barrier.transform.parent==d.transform,"Barrier inherits door orientation and scale");
        Advance(d,0.125f); Check(!d.IsFullyOpen && !d.IsFullyClosed,"Intermediate animated pose");
        float halfway=(float)Read(d,"progress"); d.CloseWithSafetyBarrier(0.25f);
        Check((float)Read(d,"progress")==halfway,"Repeated close cannot reset progress");
        Advance(d,0f); Check((float)Read(d,"progress")==halfway && barrier.enabled,"Paused animation remains blocked");
        Advance(d,0.125f); Check(d.IsFullyClosed && barrier.enabled,"Closed in 0.25 seconds; barrier retained");
        Check(((UnityEngine.Events.UnityEvent)Read(d,"onClosed")).count==1,"Single close completion event");
        d.SetOpen(true); Advance(d,0.4f); Check(!d.IsFullyOpen && barrier.enabled,"Opening retains normal 0.8-second duration and safety");
        Advance(d,0.4f); Check(d.IsFullyOpen && !barrier.enabled,"Fully open releases barrier");
        d.CloseWithSafetyBarrier(0.25f); Check(barrier.enabled && d.IsFullyOpen,"Next close reuses and reactivates barrier");
        d.SetOpenImmediately(true); Check(!barrier.enabled,"Immediate restore to open releases barrier too");
        var ordinary=new DoubleSlidingDoor(); ordinary.gameObject=new UnityEngine.GameObject("Ordinary door"); Call(ordinary,"OnEnable");
        ordinary.SetOpenImmediately(true); ordinary.Close(); Advance(ordinary,0.25f);
        Check(!ordinary.IsFullyClosed && Read(ordinary,"safetyBarrier")==null,"Ordinary doors retain original timing and colliders");
        return checks;
    }
}
'@
Add-Type -TypeDefinition ("#pragma warning disable 0649`n" + $source + "`n" + $harness + "`npublic static class RuntimeMiniMapSceneData {`n" + $boxPathMethod + "`n}")
Write-Output ("Production door animation: {0} assertions passed." -f [AnimationRegression]::Run())
