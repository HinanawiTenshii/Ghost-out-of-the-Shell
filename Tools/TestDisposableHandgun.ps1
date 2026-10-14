# Production projectile/use methods; simplified swept-AABB physics and presentation substitutes.
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$bullet=Get-Content -Raw "$root/Assets/Scripts/Items/DisposableHandgunBullet.cs"
$item=Get-Content -Raw "$root/Assets/Scripts/Items/DisposableHandgunPickupItem.cs"
function Extract($source,$signature){
    $start=$source.IndexOf($signature);if($start -lt 0){throw "Missing $signature"}
    $end=$source.IndexOf('{',$start)+1;$depth=1
    while($depth){if($source[$end] -eq '{'){$depth++};if($source[$end] -eq '}'){$depth--};$end++}
    $source.Substring($start,$end-$start)
}
$code=@'
using System;
using System.Collections.Generic;
public struct Vector2 {
    public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}
    public float sqrMagnitude=>x*x+y*y;public float magnitude=>(float)Math.Sqrt(sqrMagnitude);public Vector2 normalized=>this*(1/(float)Math.Sqrt(sqrMagnitude));
    public static Vector2 operator *(Vector2 a,float s)=>new Vector2(a.x*s,a.y*s);
    public static Vector2 operator /(Vector2 a,float s)=>new Vector2(a.x/s,a.y/s);
    public static float Distance(Vector3 a,Vector3 b)=>((Vector2)(a-b)).magnitude;
    public static implicit operator Vector2(Vector3 a)=>new Vector2(a.x,a.y);
}
public struct Vector3 {
    public float x,y,z;public Vector3(float x,float y,float z=0){this.x=x;this.y=y;this.z=z;}
    public static implicit operator Vector3(Vector2 a)=>new Vector3(a.x,a.y);
    public static Vector3 operator +(Vector3 a,Vector3 b)=>new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);
    public static Vector3 operator -(Vector3 a,Vector3 b)=>new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);
}
public struct Quaternion { public static Quaternion Euler(float x,float y,float z)=>new Quaternion(); }
public static class Mathf {public const float Rad2Deg=57.29578f;public static float Min(float a,float b)=>Math.Min(a,b);public static float Atan2(float y,float x)=>(float)Math.Atan2(y,x);}
public static class Time {public static float fixedDeltaTime=.02f;}
public struct Scene {
    public int id;public bool isLoaded=>id>0;public bool IsValid()=>id>0;
    public static bool operator ==(Scene a,Scene b)=>a.id==b.id;public static bool operator !=(Scene a,Scene b)=>a.id!=b.id;
    public override bool Equals(object o)=>o is Scene&&((Scene)o).id==id;public override int GetHashCode()=>id;
}
public static class SceneManager {public static void MoveGameObjectToScene(GameObject g,Scene s){g.scene=s;}}
public class Transform {
    public GameObject gameObject;public Vector3 position;public Quaternion rotation;public Transform parent;
    public bool IsChildOf(Transform t){for(var p=this;p!=null;p=p.parent)if(p==t)return true;return false;}
}
public class GameObject {
    public static List<GameObject> all=new List<GameObject>();public List<Component> components=new List<Component>();
    public Scene scene=new Scene{id=1};public Transform transform;public int layer;public bool activeInHierarchy=true,destroyed;public string name;
    public GameObject(string n="object"){name=n;transform=new Transform{gameObject=this};all.Add(this);}
    public T AddComponent<T>() where T:Component,new(){var c=new T{gameObject=this};components.Add(c);if(c is DisposableHandgunBullet)AddComponent<SpriteRenderer>();return c;}
    public T GetComponent<T>() where T:Component {foreach(var c in components)if(c is T)return (T)c;return null;}
}
public class Component {
    public GameObject gameObject;public bool enabled=true;public Transform transform=>gameObject.transform;
    public bool isActiveAndEnabled=>enabled&&gameObject.activeInHierarchy&&!gameObject.destroyed;
    public T GetComponent<T>() where T:Component=>gameObject.GetComponent<T>();
    public T GetComponentInParent<T>() where T:Component {for(var p=transform;p!=null;p=p.parent){var c=p.gameObject.GetComponent<T>();if(c!=null)return c;}return null;}
    public static void Destroy(GameObject g){g.destroyed=true;}
}
public class SpriteRenderer:Component {}
public class Collider2D:Component {public bool isTrigger;public Vector2 half=new Vector2(.1f,.1f);}
public struct RaycastHit2D {public Collider2D collider;public float distance;}
public struct ContactFilter2D {public bool useTriggers;public int mask;public void SetLayerMask(int m){mask=m;}}
public static class LayerMask {public static int NameToLayer(string n)=>n=="Blocks"?7:n=="Hidden Blocks"?8:-1;}
public static class Physics2D {
    public const int AllLayers=-1;public static List<Collider2D> world=new List<Collider2D>();public static int syncs;
    public static void SyncTransforms(){syncs++;}
    static bool Slab(float origin,float dir,float min,float max,ref float entry,ref float exit){
        if(Math.Abs(dir)<.00001)return origin>=min&&origin<=max;
        float a=(min-origin)/dir,b=(max-origin)/dir;if(a>b){float t=a;a=b;b=t;}
        entry=Math.Max(entry,a);exit=Math.Min(exit,b);return entry<=exit;
    }
    public static void CircleCast(Vector2 origin,float radius,Vector2 dir,ContactFilter2D f,List<RaycastHit2D> result,float length){
        foreach(var c in world){if(!c.enabled||!c.gameObject.activeInHierarchy||c.gameObject.destroyed||(!f.useTriggers&&c.isTrigger))continue;
            float entry=0,exit=length;Vector2 p=c.transform.position;
            if(Slab(origin.x,dir.x,p.x-c.half.x-radius,p.x+c.half.x+radius,ref entry,ref exit)&&
               Slab(origin.y,dir.y,p.y-c.half.y-radius,p.y+c.half.y+radius,ref entry,ref exit))result.Add(new RaycastHit2D{collider=c,distance=entry});
        }
        result.Reverse(); // Deliberately unsorted: production must choose the nearest valid contact.
    }
}
public class CameraVisionStreamingRegion:Component {public int refreshes;public float radius;public void Configure(float r){radius=r;refreshes++;}}
public static class ZeldaRuntimeRegistry {public static Scene GetGameplayScene(GameObject g)=>g.scene.id==99?new Scene{id=1}:g.scene;}
public class ZeldaFourWayMover:Component {public Vector2 FacingDirection=new Vector2(1,0);}
public class ZeldaCharacterData:Component {
    public bool IsDead,IsGhostLike;public bool CanTakeAttackDamage=true;public int calls,damage;public ZeldaCharacterData attacker;public Action callback;
    public Vector2 investigatedPosition;
    public void TakeHandgunDamage(int d,Vector2 p){investigatedPosition=p;TakeDamage(d,null);}
    public void TakeDamage(int d,ZeldaCharacterData a){calls++;damage+=d;attacker=a;if(callback!=null)callback();}
}
public class Receiver:Component {public int calls,damage;public ZeldaCharacterData attacker;public void ReceiveAttack(int d,ZeldaCharacterData a){calls++;damage+=d;attacker=a;}}
public class DoorData:Receiver {public bool direct;public void ReceiveAttack(int d,ZeldaCharacterData a,bool direct){this.direct=direct;base.ReceiveAttack(d,a);}}
public class CardboardBoxPickupItem:Receiver {}
public class BatteryPickupItem:Receiver {}
public class BatterySocket:Receiver {}
public class BombPickupItem:Receiver {public void DetonateFromAttack(ZeldaCharacterData a){calls++;attacker=a;}}
public class PlacedBomb:Receiver {public void DetonateImmediately(){calls++;}}
public class StabilityCrystalPickupItem:Receiver {public void BurstFromAttack(){calls++;}}
public class DollAttackShake:Receiver {public void TriggerAttackShake(){calls++;}}
public class PickupItemBase:Component {protected virtual bool ApplyUseEffect(ZeldaCharacterData user)=>false;}
public static class DisposableHandgunAudio { public static int plays;public static void Play(Scene s,Vector3 p){plays++;} }
// Animation has its own production-method test; preserve body-origin projectile cases here.
public static class DisposableHandgunAttackVisual {
    public static bool TryFire(ZeldaCharacterData u,ZeldaFourWayMover m,float speed)=>DisposableHandgunBullet.Fire(u,m.FacingDirection,speed);
}
public class DisposableHandgunPickupItem:PickupItemBase {
    private bool fired;private float bulletSpeed=10;public bool Use(ZeldaCharacterData u)=>ApplyUseEffect(u);
'@
$code+=Extract $item '    protected override bool ApplyUseEffect('
$code+=@'
}
public class DisposableHandgunBullet:Component {
    public const int AttackPower=2;public const float Lifetime=5;private const float Radius=.06f;
    private readonly List<RaycastHit2D> hits=new List<RaycastHit2D>();private ContactFilter2D filter;
    private ZeldaCharacterData owner;private Vector2 firingPosition;private Transform ownerTransform;private CameraVisionStreamingRegion retention;
    private Vector2 direction;private float speed,elapsed,retentionTimer;private bool finished;
    private void ConfigureVisual(){}public void Tick()=>FixedUpdate();public void Contact(Collider2D c)=>Hit(c);
'@
foreach($sig in @('    public static bool Fire(', '    public static bool FireFromMuzzle(', '    private bool MoveSwept(', '    private void RefreshRetention()', '    private void FixedUpdate()',
    '    private int FindFirstContact(', '    private bool CanHit(', '    private static Component FindDamageTarget(',
    '    private void Hit(', '    private void Finish()')){$code+=Extract $bullet $sig}
$code+=@'
}
public static class GunTests {
    static int checks;static void Check(bool b,string m){checks++;if(!b)throw new Exception(m);}
    static Collider2D Collider(GameObject g,bool trigger=false){var c=g.AddComponent<Collider2D>();c.isTrigger=trigger;Physics2D.world.Add(c);return c;}
    static ZeldaCharacterData Shooter(){Physics2D.world.Clear();GameObject.all.Clear();var g=new GameObject("Shooter");g.AddComponent<ZeldaFourWayMover>();Collider(g);return g.AddComponent<ZeldaCharacterData>();}
    static DisposableHandgunBullet Latest(){for(int i=GameObject.all.Count-1;i>=0;i--){var b=GameObject.all[i].GetComponent<DisposableHandgunBullet>();if(b!=null)return b;}throw new Exception("No bullet");}
    static T Target<T>(float x,float y=0,bool trigger=true) where T:Component,new(){var g=new GameObject("Target");g.transform.position=new Vector3(x,y);Collider(g,trigger);return g.AddComponent<T>();}
    public static int Run(){
        foreach(var dir in new[]{new Vector2(1,0),new Vector2(-1,0),new Vector2(0,1),new Vector2(0,-1)}){
            var shooter=Shooter();shooter.GetComponent<ZeldaFourWayMover>().FacingDirection=dir;
            var item=new GameObject().AddComponent<DisposableHandgunPickupItem>();Check(item.Use(shooter),"Valid use consumes item");var bullet=Latest();
            Check(!item.Use(shooter),"Same item cannot fire twice");
            for(int i=0;i<249;i++)bullet.Tick();Check(!bullet.gameObject.destroyed,"Alive before five seconds");
            for(int i=0;i<2;i++)bullet.Tick();Check(bullet.gameObject.destroyed,"Expires at five seconds");
            Check(Math.Abs(bullet.transform.position.x-dir.x*50)<.002&&Math.Abs(bullet.transform.position.y-dir.y*50)<.002,"Facing direction, configured speed and bounded five-second distance");
            var region=bullet.GetComponent<CameraVisionStreamingRegion>();Check(!region.enabled&&region.refreshes>1,"Moving retention refreshed and released on expiry");
        }
        foreach(float step in new[]{.01f,.02f,.1f,.5f}){
            Time.fixedDeltaTime=step;var shooter=Shooter();var wall=new GameObject("Thin wall");wall.transform.position=new Vector3(.3f,0);var wc=Collider(wall);wc.half=new Vector2(.005f,1);
            var victim=Target<ZeldaCharacterData>(1);DisposableHandgunBullet.Fire(shooter,new Vector2(1,0),100);var bullet=Latest();bullet.Tick();
            Check(bullet.gameObject.destroyed&&victim.calls==0,"Swept hit stops at thin wall, no damage behind wall");
            Check(bullet.transform.position.x<.3,"Bullet remains at near-side impact");
        }
        Time.fixedDeltaTime=.02f;
        var user=Shooter();var first=Target<ZeldaCharacterData>(.3f);var second=Target<ZeldaCharacterData>(.5f);
        Collider(first.gameObject);DisposableHandgunBullet.Fire(user,new Vector2(1,0),50);var shot=Latest();
        first.callback=()=>shot.Contact(second.GetComponent<Collider2D>());shot.Tick();shot.Tick();
        Check(first.calls==1&&first.damage==2&&first.attacker==null&&second.calls==0,"One anonymous 2-power hit, multiple colliders and reentrant contact protected");
        Check(user.calls==0,"Shooter ignored");
        user=Shooter();var ghost=Target<ZeldaCharacterData>(.2f);ghost.CanTakeAttackDamage=false;
        DisposableHandgunBullet.Fire(user,new Vector2(1,0),10);shot=Latest();shot.Tick();Check(shot.gameObject.destroyed&&ghost.calls==0,"Immune character stops bullet without overriding immunity");
        foreach(bool trigger in new[]{false,true}){user=Shooter();var g=new GameObject();g.layer=7;g.transform.position=new Vector3(.2f,0);Collider(g,trigger);
            DisposableHandgunBullet.Fire(user,new Vector2(1,0),10);shot=Latest();shot.Tick();Check(shot.gameObject.destroyed,"Solid or trigger Blocks obstacle stops bullet");}
        user=Shooter();var area=new GameObject();Collider(area,true);var otherScene=Target<ZeldaCharacterData>(.1f);otherScene.gameObject.scene=new Scene{id=2};
        DisposableHandgunBullet.Fire(user,new Vector2(1,0),10);shot=Latest();shot.Tick();Check(!shot.gameObject.destroyed&&otherScene.calls==0,"Ignore area triggers and other gameplay scenes");
        user=Shooter();user.gameObject.scene=new Scene{id=99};DisposableHandgunBullet.Fire(user,new Vector2(1,0),10);shot=Latest();
        Check(shot.gameObject.scene.id==1,"Persistent player bullet belongs to current gameplay scene");
        user.IsDead=true;user.gameObject.destroyed=true;var afterDeath=Target<ZeldaCharacterData>(.2f);shot.Tick();Check(afterDeath.damage==2,"Fired bullet survives shooter's death");
        foreach(Type type in new[]{typeof(DoorData),typeof(CardboardBoxPickupItem),typeof(BatteryPickupItem),typeof(BatterySocket),typeof(BombPickupItem),typeof(PlacedBomb),typeof(StabilityCrystalPickupItem),typeof(DollAttackShake)}){
            user=Shooter();var g=new GameObject();g.transform.position=new Vector3(.2f,0);var c=Collider(g,true);var receiver=(Receiver)Activator.CreateInstance(type);receiver.gameObject=g;g.components.Add(receiver);
            DisposableHandgunBullet.Fire(user,new Vector2(1,0),10);shot=Latest();shot.Tick();shot.Contact(c);
            Check(receiver.calls==1&&shot.gameObject.destroyed,"Damageable prop hit once: "+type.Name);
            if(type==typeof(DoorData)||type==typeof(CardboardBoxPickupItem)||type==typeof(BatteryPickupItem)||type==typeof(BatterySocket))Check(receiver.damage==2&&receiver.attacker==user,"Prop power/source: "+type.Name);
        }
        user=Shooter();var invalid=new GameObject().AddComponent<DisposableHandgunPickupItem>();Check(!invalid.Use(null),"Null user retains item");
        user.IsDead=true;Check(!invalid.Use(user),"Dead user retains item");user.IsDead=false;user.IsGhostLike=true;Check(!invalid.Use(user),"Ghost retains item");user.IsGhostLike=false;
        user.GetComponent<ZeldaFourWayMover>().enabled=false;Check(!invalid.Use(user),"Uncontrolled user retains item");
        Check(!DisposableHandgunBullet.Fire(user,new Vector2(),10)&&!DisposableHandgunBullet.Fire(user,new Vector2(1,0),float.NaN),"Invalid direction/speed rejected");
        user=Shooter();Check(DisposableHandgunBullet.FireFromMuzzle(user,new Vector2(1,0),10,new Vector3(.6f,.2f)),"Muzzle fire succeeds");
        shot=Latest();Check(Math.Abs(shot.transform.position.x-.6f)<.0001&&Math.Abs(shot.transform.position.y-.2f)<.0001,"Bullet starts exactly at muzzle, including perpendicular barrel offset");
        shot.Tick();Check(Math.Abs(shot.transform.position.x-.8f)<.0001&&Math.Abs(shot.transform.position.y-.2f)<.0001,"After muzzle placement bullet travels along facing, not body-to-muzzle diagonal");
        user=Shooter();var muzzleWall=new GameObject();muzzleWall.transform.position=new Vector3(.3f,0);Collider(muzzleWall).half=new Vector2(.005f,1);
        Check(DisposableHandgunBullet.FireFromMuzzle(user,new Vector2(1,0),10,new Vector3(.6f,.2f)),"Blocked muzzle still counts as firing");
        shot=Latest();Check(shot.gameObject.destroyed&&shot.transform.position.x<.3f,"Muzzle placement cannot teleport through thin wall");
        user=Shooter();user.transform.position=new Vector3(3,4);var distant=Target<ZeldaCharacterData>(3.8f,4);
        int sounds=DisposableHandgunAudio.plays;DisposableHandgunBullet.Fire(user,new Vector2(1,0),10);shot=Latest();user.transform.position=new Vector3(30,40);
        for(int i=0;i<5&&!shot.gameObject.destroyed;i++)shot.Tick();
        Check(distant.calls==1&&distant.investigatedPosition.x==3&&distant.investigatedPosition.y==4,"Hit investigates captured firing location, not moved shooter or muzzle");
        Check(DisposableHandgunAudio.plays==sounds+1,"Exactly one sound at firing, not on impact");
        return checks;
    }
}
'@
Add-Type -TypeDefinition $code
$count=[GunTests]::Run()
$prefab=Get-Content -Raw "$root/Assets/Resources/PickupItems/DisposableHandgunPickupItem.prefab"
foreach($text in @('itemId: disposable_handgun','maxStackSize: 3','itemName: "一次性手铳"','装填了一发子弹的一次性手铳，在发射一发子弹后就会被废弃。')){
    if(!$prefab.Contains($text)){throw "Prefab missing $text"}
}
foreach($script in @('DisposableHandgunPickupItem','DisposableHandgunPickupItemVisual')){
    $meta=Get-Content -Raw "$root/Assets/Scripts/Items/$script.cs.meta";$guid=[regex]::Match($meta,'guid: (\w+)').Groups[1].Value
    if(!$prefab.Contains($guid)){throw "Prefab missing script $script"}
}
$visual=Get-Content -Raw "$root/Assets/Scripts/Items/DisposableHandgunPickupItemVisual.cs"
$rows=[regex]::Matches($visual,'"([.DSHGWL]{16})"');if($rows.Count -ne 16){throw 'Icon must be 16 x 16'}
if(!$bullet.Contains('public const int AttackPower = 2;') -or !$bullet.Contains('public const float Lifetime = 5f;')){throw 'Unexpected power or lifetime'}
Write-Output "PASS: $count production use/projectile checks; prefab identity, scripts, constants and 16x16 icon validated. Physics is a test substitute, not Unity Play Mode."
