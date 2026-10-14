param([switch]$RenderPreview)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$visual=Get-Content -Raw "$root/Assets/Scripts/Items/DisposableHandgunAttackVisual.cs"
$mover=Get-Content -Raw "$root/Assets/Scripts/Characters/ZeldaFourWayMover.cs"
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
 public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}public static Vector2 zero=>new Vector2();
 public float sqrMagnitude=>x*x+y*y;
 public static Vector2 operator +(Vector2 a,Vector2 b)=>new Vector2(a.x+b.x,a.y+b.y);
 public static Vector2 operator -(Vector2 a,Vector2 b)=>new Vector2(a.x-b.x,a.y-b.y);
 public static Vector2 operator *(Vector2 a,float b)=>new Vector2(a.x*b,a.y*b);
 public static Vector2 operator /(Vector2 a,float b)=>new Vector2(a.x/b,a.y/b);
 public static implicit operator Vector2(Vector3 v)=>new Vector2(v.x,v.y);
 public static Vector2 down=>new Vector2(0,-1);public static Vector2 up=>new Vector2(0,1);
 public static Vector2 left=>new Vector2(-1,0);public static Vector2 right=>new Vector2(1,0);
}
public struct Vector3 {
 public float x,y,z;public Vector3(float x,float y,float z=0){this.x=x;this.y=y;this.z=z;}public static Vector3 one=>new Vector3(1,1,1);
 public static implicit operator Vector3(Vector2 v)=>new Vector3(v.x,v.y);
 public static Vector3 operator +(Vector3 a,Vector3 b)=>new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);
 public static Vector3 operator -(Vector3 a,Vector3 b)=>new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);
}
public struct Quaternion {public static Quaternion identity=>new Quaternion();}
public struct Color {public float r,g,b;public Color(float r,float g,float b){this.r=r;this.g=g;this.b=b;}}
public static class Mathf {
 public const float PI=(float)Math.PI;public static float Abs(float v)=>Math.Abs(v);public static float Max(float a,float b)=>Math.Max(a,b);
 public static float Clamp(float v,float a,float b)=>Math.Max(a,Math.Min(b,v));public static float Clamp01(float v)=>Clamp(v,0,1);public static float Sin(float v)=>(float)Math.Sin(v);
}
public static class Time {public static float deltaTime=.02f;}
public struct Scene {public bool isLoaded=>true;public bool IsValid()=>true;}
public static class ZeldaRuntimeRegistry {public static Scene GetGameplayScene(GameObject g)=>new Scene();}
public class Transform {
 public Transform parent;public Vector3 position,localScale=Vector3.one;public Quaternion rotation;
 public void SetParent(Transform p,bool stay){parent=p;}
 public Vector3 lossyScale=>parent==null?localScale:new Vector3(localScale.x*parent.lossyScale.x,localScale.y*parent.lossyScale.y,1);
 public Vector3 TransformPoint(Vector3 local)=>position+new Vector3(local.x*lossyScale.x,local.y*lossyScale.y,local.z);
}
public class GameObject {
 public static List<GameObject> all=new List<GameObject>();public List<Component> components=new List<Component>();public Transform transform=new Transform();
 public bool active=true,destroyed;public string name;
 public GameObject(string name="object"){this.name=name;all.Add(this);}
 public T AddComponent<T>() where T:Component,new(){var c=new T{gameObject=this};components.Add(c);return c;}
 public T GetComponent<T>() where T:Component {foreach(var c in components)if(c is T)return (T)c;return null;}
 public void SetActive(bool v){if(active&&!v)foreach(var c in components)if(c is DisposableHandgunAttackVisual)((DisposableHandgunAttackVisual)c).Disable();active=v;}
}
public class Component {
 public GameObject gameObject;public bool enabled=true;public bool isActiveAndEnabled=>enabled&&gameObject.active&&!gameObject.destroyed;public Transform transform=>gameObject.transform;
 public T GetComponent<T>() where T:Component=>gameObject.GetComponent<T>();public static void Destroy(GameObject g){g.destroyed=true;}
}
public class Sprite {public Vector2 pivot;public float pixelsPerUnit=16;}
public class SpriteRenderer:Component {public Sprite sprite;public int sortingLayerID,sortingOrder;public bool flipX,flipY;public Color color;}
public class ZeldaCharacterData:Component {public bool IsDead,IsGhostLike;public float AttackDuration=.24f;}
public class AutomatonZeldaCharacterData:ZeldaCharacterData{} public class BehemothZeldaCharacterData:ZeldaCharacterData{}
public class StrongmanZeldaCharacterData:ZeldaCharacterData{} public class HeavyPaladinZeldaCharacterData:ZeldaCharacterData{}
public class ChefZeldaCharacterData:ZeldaCharacterData{} public class ArabSoldierZeldaCharacterData:ZeldaCharacterData{}
public class AlchemistZeldaCharacterData:ZeldaCharacterData{} public class PaladinZeldaCharacterData:ZeldaCharacterData{}
public class PriestZeldaCharacterData:ZeldaCharacterData{} public class WizardZeldaCharacterData:ZeldaCharacterData{}
public class ZeldaFourWayMover:Component {
 private ZeldaCharacterData characterData=>GetComponent<ZeldaCharacterData>();public Vector2 facingDirection,moveDirection;
 private float attackTimer;private bool itemAttackPose;public int poses;
 public Vector2 FacingDirection=>facingDirection;public bool IsAttacking=>attackTimer>0;public bool IsUsingItemAttack=>itemAttackPose&&IsAttacking;
 private void ApplyFacingVisual(){poses++;}public void SimulateStun(){attackTimer=0;}
'@
foreach($sig in @('    public bool TryBeginItemAttack(', '    public void EndItemAttack()', '    private Vector2 ToNearestCardinalDirection(')){$code+=Extract $mover $sig}
$code+=@'
}
public static class DisposableHandgunBullet {
 public static bool success=true;public static int shots;public static Vector3 muzzle;public static Vector2 facing;
 public static bool FireFromMuzzle(ZeldaCharacterData u,Vector2 f,float speed,Vector3 m){muzzle=m;facing=f;if(success)shots++;return success;}
}
public class DisposableHandgunAttackVisual:Component {
 private const float PixelsPerUnit=24f;private static Sprite flashSprite;
 private ZeldaFourWayMover mover;private ZeldaCharacterData user;private SpriteRenderer body,gun,flash;
 private Vector2 facing;private int view;private float elapsed,duration;
 private static Sprite CreateSprite(string[] rows,Vector2 pivot,string n)=>new Sprite{pivot=pivot,pixelsPerUnit=PixelsPerUnit};
 public static Sprite GetSprite(int d)=>new Sprite{pivot=GripPixel(d),pixelsPerUnit=PixelsPerUnit};
 public void Tick()=>LateUpdate();public void Disable()=>OnDisable();public Vector3 Muzzle=>MuzzleWorld;
'@
$arrayStart=$visual.IndexOf('    private static readonly string[] Right')
$arrayEnd=$visual.IndexOf('    public static bool TryFire(')
$code+=$visual.Substring($arrayStart,$arrayEnd-$arrayStart)
$code+=[regex]::Match($visual,'private Vector3 MuzzleWorld =>[^;]+;').Value
foreach($sig in @('    public static bool TryFire(', '    private void LateUpdate()', '    private void ApplyPose()', '    public static Vector2 BodyGripPixel(', '    private void OnDisable()')){$code+=Extract $visual $sig}
$code+=@'
}
public static class FiringTests {
 static int checks;static void Check(bool b,string m){checks++;if(!b)throw new Exception(m);}
 static bool Near(Vector3 a,Vector3 b)=>Math.Abs(a.x-b.x)<.0001&&Math.Abs(a.y-b.y)<.0001;
 static ZeldaCharacterData User(Vector2 facing,float sx=1,float sy=1){
  GameObject.all.Clear();var g=new GameObject();g.transform.position=new Vector3(10,20);g.transform.localScale=new Vector3(sx,sy,1);
  var u=g.AddComponent<ZeldaCharacterData>();g.AddComponent<ZeldaFourWayMover>().facingDirection=facing;
  g.AddComponent<SpriteRenderer>().sprite=new Sprite{pivot=new Vector2(8,5)};return u;
 }
 static DisposableHandgunAttackVisual Last(){for(int i=GameObject.all.Count-1;i>=0;i--){var a=GameObject.all[i].GetComponent<DisposableHandgunAttackVisual>();if(a!=null)return a;}throw new Exception("No visual");}
 public static int Run(){
  Vector2[] directions={Vector2.down,Vector2.up,Vector2.left,Vector2.right};
  for(int d=0;d<4;d++){
   string[] rows=DisposableHandgunAttackVisual.GetPixelRows(d);Check(rows.Length==12,"12 rows");foreach(string row in rows)Check(row.Length==12,"12 columns");
   var muzzle=DisposableHandgunAttackVisual.MuzzlePixel(d);var grip=DisposableHandgunAttackVisual.GripPixel(d);
   Check((muzzle.x-grip.x)*directions[d].x+(muzzle.y-grip.y)*directions[d].y>0,"Muzzle ahead of grip for each view");
   foreach(float sx in new[]{.6f,1f,1.4f,-1f})foreach(float sy in new[]{.8f,1.2f,-1f}){
    var u=User(directions[d],sx,sy);var m=u.GetComponent<ZeldaFourWayMover>();int shots=DisposableHandgunBullet.shots;
    Check(DisposableHandgunAttackVisual.TryFire(u,m,10),"Starts valid shot");var visual=Last();
    Check(m.IsUsingItemAttack&&m.poses==1,"Reuses character raised-hand attack pose");
    Check(DisposableHandgunBullet.shots==shots+1&&Near(DisposableHandgunBullet.muzzle,visual.Muzzle),"One bullet emitted exactly at visible muzzle");
    Check(!DisposableHandgunAttackVisual.TryFire(u,m,10),"Cannot fire or consume next gun during active attack");
    Vector3 delta=visual.Muzzle-visual.transform.position;Check(delta.x*directions[d].x+delta.y*directions[d].y>0,"Parent negative scale cannot reverse authored gun direction");
    for(int t=0;t<3;t++)visual.Tick();Check(DisposableHandgunBullet.shots==shots+1,"Animation does not emit more shots");
    for(int t=0;t<25;t++)if(!visual.gameObject.destroyed)visual.Tick();Check(visual.gameObject.destroyed&&!m.IsAttacking,"Visual and pose cleaned after duration");
   }
  }
  var user=User(Vector2.right);var mover=user.GetComponent<ZeldaFourWayMover>();DisposableHandgunBullet.success=false;
  Check(!DisposableHandgunAttackVisual.TryFire(user,mover,10)&&!mover.IsAttacking,"Failed bullet spawn restores pose and reports no consumption");DisposableHandgunBullet.success=true;
  foreach(int cancel in new[]{0,1,2,3}){
   user=User(Vector2.right);mover=user.GetComponent<ZeldaFourWayMover>();DisposableHandgunAttackVisual.TryFire(user,mover,10);var v=Last();int shots=DisposableHandgunBullet.shots;
   if(cancel==0)user.IsDead=true;else if(cancel==1)mover.enabled=false;else if(cancel==2)user.IsGhostLike=true;else mover.SimulateStun();
   v.Tick();Check(v.gameObject.destroyed&&!mover.IsAttacking&&DisposableHandgunBullet.shots==shots,"Death/transfer/form/stun removes weapon without spawning another shot");
  }
  string[] right=DisposableHandgunAttackVisual.GetPixelRows(3),left=DisposableHandgunAttackVisual.GetPixelRows(2);
  for(int y=0;y<12;y++)for(int x=0;x<12;x++)Check(right[y][x]==left[y][11-x],"Side views mirror horizontally, not upside-down");
  return checks;
 }
}
'@
Add-Type -TypeDefinition $code
$count=[FiringTests]::Run()
$begin=Extract $mover '    public bool TryBeginItemAttack('
if($begin -match 'Instantiate|hitbox|PlayAttackSound\('){throw 'Item attack must not spawn the melee weapon/hitbox/audio'}
$smith=Get-Content -Raw "$root/Assets/Scripts/Characters/BlacksmithZeldaCharacterData.cs"
if(!$smith.Contains('CreateTexture(facingDirection, true, false)') -or !$smith.Contains('if (drawHammer) DrawHammer')){throw 'Blacksmith must replace embedded hammer during item attack'}
Write-Output "PASS: $count firing/pose/muzzle/four-view assertions; no melee spawning; blacksmith replacement wired. Unity rendering is stubbed."
if($RenderPreview){
    Add-Type -AssemblyName System.Drawing
    $bitmap=[Drawing.Bitmap]::new(704,236)
    $graphics=[Drawing.Graphics]::FromImage($bitmap)
    $graphics.Clear([Drawing.Color]::FromArgb(24,32,46))
    $paletteSource=Get-Content -Raw "$root/Assets/Scripts/Items/DisposableHandgunPickupItemVisual.cs"
    $brushes=@{}
    foreach($match in [regex]::Matches($paletteSource,"case '([A-Z])': return new Color\(([^)]+)\)")){
        $channels=$match.Groups[2].Value.Split(',') | ForEach-Object { [int]([float]($_.Trim().TrimEnd('f'))*255) }
        $brushes[$match.Groups[1].Value]=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb($channels[0],$channels[1],$channels[2]))
    }
    $font=[Drawing.Font]::new('Consolas',12)
    $labels=@('DOWN','UP','LEFT','RIGHT')
    for($d=0;$d -lt 4;$d++){
        $rows=[DisposableHandgunAttackVisual]::GetPixelRows($d)
        for($row=0;$row -lt 12;$row++){for($x=0;$x -lt 12;$x++){
            $symbol=$rows[$row].Substring($x,1)
            if($brushes.ContainsKey($symbol)){$graphics.FillRectangle($brushes[$symbol],16+$d*176+$x*12,28+$row*12,12,12)}
        }}
        $graphics.DrawString($labels[$d],$font,[Drawing.Brushes]::White,[single](52+$d*176),[single]190)
    }
    $path=Join-Path $root 'Docs/HandgunFiring-preview.png'
    $bitmap.Save($path,[Drawing.Imaging.ImageFormat]::Png)
    foreach($brush in $brushes.Values){$brush.Dispose()}
    $font.Dispose();$graphics.Dispose();$bitmap.Dispose()
    Write-Output "Code-art preview (not Unity screenshot): $path"
}
