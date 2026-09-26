# Production curve and ribbon mesh/history tests. Engine lifecycle is not Play Mode tested.
param([string]$PreviewPath)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$effect=Get-Content -Raw "$taskRoot/Assets/Scripts/Items/GrowthCollectiblePickupEffect.cs"
$ribbon=Get-Content -Raw "$taskRoot/Assets/Scripts/Items/GrowthPickupRibbonTrail.cs"
function Extract($s,$signature) {
 $start=$s.IndexOf($signature);if($start -lt 0){throw "Missing $signature"}
 $begin=$s.IndexOf('{',$start);$end=$begin+1;$depth=1
 while($depth){if($s[$end] -eq '{'){$depth++};if($s[$end] -eq '}'){$depth--};$end++}
 $s.Substring($start,$end-$start)
}
$code=@'
using System;
using System.Collections.Generic;
using UnityEngine;
namespace UnityEngine {
 public class Object {public bool destroyed;public static void Destroy(Object o){o.destroyed=true;}}
 public class Material:Object {}
 public enum HideFlags{HideAndDontSave}
 public class Transform {public void SetParent(Transform p,bool b){}public Vector3 InverseTransformPoint(Vector3 p)=>p;}
 public class GameObject {public Transform transform=new Transform();public GameObject(string n){}public T AddComponent<T>() where T:new()=>new T();}
 public class MeshFilter {public Mesh sharedMesh;}
 public class MeshRenderer {public Material sharedMaterial;public int sortingOrder;public Rendering.ShadowCastingMode shadowCastingMode;public bool receiveShadows;}
 namespace Rendering {public enum ShadowCastingMode{Off}}
 public class Mesh:Object {
  public static Mesh Latest;public string name;public HideFlags hideFlags;
  public List<Vector3> vertices=new List<Vector3>();public List<Color> colors=new List<Color>();public List<int> triangles=new List<int>();
  public Mesh(){Latest=this;}public void MarkDynamic(){}public void Clear(bool b){vertices.Clear();colors.Clear();triangles.Clear();}
  public void SetVertices(List<Vector3> v){vertices.AddRange(v);}public void SetColors(List<Color> c){colors.AddRange(c);}
  public void SetUVs(int i,List<Vector2> v){}public void SetTriangles(List<int> v,int sub){triangles.AddRange(v);}public void RecalculateBounds(){}
 }
 public struct Color {public float r,g,b,a;public Color(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;}public static Color white=>new Color(1,1,1);}
 public struct Vector2 {public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}}
 public struct Vector3 {
  public float x,y,z;public Vector3(float x,float y,float z=0){this.x=x;this.y=y;this.z=z;}
  public static Vector3 zero=>new Vector3();public float sqrMagnitude=>x*x+y*y+z*z;public float magnitude=>(float)Math.Sqrt(sqrMagnitude);
  public Vector3 normalized=>magnitude>.000001f?this/magnitude:zero;
  public static Vector3 operator +(Vector3 a,Vector3 b)=>new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);
  public static Vector3 operator -(Vector3 a,Vector3 b)=>new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);
  public static Vector3 operator -(Vector3 a)=>zero-a;
  public static Vector3 operator *(Vector3 a,float b)=>new Vector3(a.x*b,a.y*b,a.z*b);
  public static Vector3 operator *(float b,Vector3 a)=>a*b;
  public static Vector3 operator /(Vector3 a,float b)=>a*(1/b);
  public static float Distance(Vector3 a,Vector3 b)=>(a-b).magnitude;
  public static float Dot(Vector3 a,Vector3 b)=>a.x*b.x+a.y*b.y+a.z*b.z;
  public static Vector3 Lerp(Vector3 a,Vector3 b,float t)=>a+(b-a)*Mathf.Clamp01(t);
  public static Vector3 ClampMagnitude(Vector3 a,float m)=>a.magnitude>m?a.normalized*m:a;
 }
 public static class Mathf {
  public const float PI=(float)Math.PI;
  public static float Clamp(float a,float min,float max)=>Math.Max(min,Math.Min(max,a));
  public static float Clamp01(float a)=>Clamp(a,0,1);
  public static float Min(float a,float b)=>Math.Min(a,b);public static int Min(int a,int b)=>Math.Min(a,b);
  public static float Max(float a,float b)=>Math.Max(a,b);public static int Max(int a,int b)=>Math.Max(a,b);
  public static float Pow(float a,float b)=>(float)Math.Pow(a,b);public static float Sqrt(float a)=>(float)Math.Sqrt(a);
  public static float Lerp(float a,float b,float t)=>a+(b-a)*Clamp01(t);
  public static float InverseLerp(float a,float b,float v)=>a==b?0:Clamp01((v-a)/(b-a));
  public static float SmoothStep(float a,float b,float t){t=Clamp01(t);return Lerp(a,b,t*t*(3-2*t));}
 }
public class Flight {
 private float scatterDuration=.65f,homingSpeed=12;
 private Vector3 origin;
 private Vector3[] scatterStarts=new Vector3[24],scatterControls=new Vector3[24],scatterOffsets=new Vector3[24],flightVelocities=new Vector3[24];
 private float[] scatterEasePowers=new float[24],speedMultipliers=new float[24];
 private Vector3[] returnStarts=new Vector3[24],returnControls=new Vector3[24],returnEndOffsets=new Vector3[24],returnTargets=new Vector3[24];
 private float[] returnElapsed=new float[24],returnDurations=new float[24];private bool[] returnReady=new bool[24];
 public Flight(){
  var random=new Random(12);
  for(int i=0;i<24;i++){
   float angle=(float)random.NextDouble()*Mathf.PI*2, radius=.65f*1.25f*Mathf.Lerp(.65f,1.9f,(float)random.NextDouble());
   scatterOffsets[i]=new Vector3((float)Math.Cos(angle),(float)Math.Sin(angle))*radius;
   float bend=angle+Mathf.Lerp(-1.2f,1.2f,(float)random.NextDouble());
   scatterControls[i]=new Vector3((float)Math.Cos(bend),(float)Math.Sin(bend))*radius*.45f;
   scatterEasePowers[i]=Mathf.Lerp(1.6f,3.4f,(float)random.NextDouble());speedMultipliers[i]=Mathf.Lerp(.8f,1.2f,(float)random.NextDouble());
   Vector3 p,v;EvaluateScatter(i,.65f,out p,out v);flightVelocities[i]=v;BeginReturn(i,p,new Vector3(1.1f,-.25f));
  }
 }
 public Vector3 Position(int id,float time,Vector3 target){
  if(time<=scatterDuration){Vector3 p,v;EvaluateScatter(id,time,out p,out v);return p;}
  return EvaluateReturn(returnStarts[id],returnControls[id],target+returnEndOffsets[id],target,(time-scatterDuration)/returnDurations[id]);
 }
 public float Arrival(int id)=>scatterDuration+returnDurations[id];
 public static Vector3 Curve(Vector3 a,Vector3 b,Vector3 c,Vector3 d,float t)=>EvaluateReturn(a,b,c,d,t);
 public static bool Hits(Vector3 a,Vector3 b,Vector3 c)=>PassesTarget(a,b,c);
'@
foreach($sig in @('private void EvaluateScatter(', 'private void BeginReturn(', 'private static Vector3 EvaluateReturn(', 'private static bool PassesTarget(')){$code+=Extract $effect $sig}
$code+="`n}`n}`n"
$code += [regex]::Replace($ribbon,'(?m)^using .*;\r?\n','')
$code+=@'
public static class RibbonChecks {
 static int checks;static void Check(bool c,string message){checks++;if(!c)throw new Exception(message);}
 static void MeshValid(Mesh m){
  Check(m.vertices.Count<=64*65*5,"Bounded mesh");Check(m.colors.Count==m.vertices.Count,"Color alignment");
  foreach(var p in m.vertices)Check(!float.IsNaN(p.x)&&!float.IsInfinity(p.y),"Finite vertex");
  foreach(int index in m.triangles)Check(index>=0&&index<m.vertices.Count,"Valid triangle index");
 }
 public static int Run(){
  var a=new Vector3(0,0);var b=new Vector3(1,0);var c=new Vector3(2,0);var d=new Vector3(3,0);
  Check(Vector3.Distance(Flight.Curve(a,b,c,d,0),a)<.0001,"Exact curve start");
  Check(Vector3.Distance(Flight.Curve(a,b,c,d,1),d)<.0001,"Exact curve end");
  Check(Flight.Curve(a,b,c,d,.9f).x-Flight.Curve(a,b,c,d,.8f).x>Flight.Curve(a,b,c,d,.2f).x-Flight.Curve(a,b,c,d,.1f).x,"Accelerating parameter, not constant-speed line");
  Check(Flight.Hits(new Vector3(-1,0),new Vector3(1,0),Vector3.zero),"Fast segment crossing catches target");
  var flight=new Flight();var target=new Vector3(1.1f,-.25f);
  foreach(int fps in new[]{15,30,60,144}){
   var trail=new GrowthPickupRibbonTrail(new Transform(),24,.24f,new Material(),4);var arrived=new bool[24];
   for(int id=0;id<24;id++){trail.SetWidth(id,.12f);trail.Record(id,flight.Position(id,0,target),0);}
   for(int frame=1;frame<=fps*3;frame++){
    float time=(float)frame/fps;var moving=target+new Vector3(time*.15f,0);
    for(int id=0;id<24;id++){
     if(arrived[id])continue;
     trail.Record(id,flight.Position(id,time,moving),time);
     if(time>=flight.Arrival(id)){trail.Arrive(id,moving,time);arrived[id]=true;}
    }
    trail.Render(time,true,moving);
    if(frame==fps)MeshValid(Mesh.Latest);
   }
   Check(!trail.HasVisibleRibbons&&Mesh.Latest.vertices.Count==0,"No remnant after absorption at "+fps+"fps");
   var owned=Mesh.Latest;trail.Dispose();Check(owned.destroyed,"Owned mesh disposed");
  }
  var single=new GrowthPickupRibbonTrail(new Transform(),1,.4f,new Material(),4);single.SetWidth(0,.12f);
  for(int i=0;i<=120;i++)single.Record(0,new Vector3(i/120f,0),i/120f);
  single.Arrive(0,new Vector3(1,0),1);single.Render(1,true,new Vector3(1,0));
  Check(single.HasVisibleRibbons,"Tail survives head arrival");float start=Mesh.Latest.vertices[2].x;
  single.Render(1.2f,true,new Vector3(1.1f,0));
  Check(Mesh.Latest.vertices[2].x>start,"Tail retracts along path instead of whole-strip fading");
  Check(Math.Abs(Mesh.Latest.vertices[Mesh.Latest.vertices.Count-3].x-1.1f)<.0001,"Absorbed tip follows moving body");
  Check(Mesh.Latest.colors[0].a==0&&Mesh.Latest.colors[4].a==0,"Soft transparent ribbon edges");
  single.Render(1.41f,true,target);Check(!single.HasVisibleRibbons,"Absorption completes within history duration");
  for(int i=0;i<10000;i++)single.Record(0,new Vector3(i*.001f,0),i*.001f);
  single.Render(9.999f,false,Vector3.zero);MeshValid(Mesh.Latest);
  single.ClearAll();single.Render(10,false,Vector3.zero);Check(!single.HasVisibleRibbons&&Mesh.Latest.vertices.Count==0,"Scene expiry clears geometry");single.Dispose();
  return checks;
 }
 public static Mesh Preview(float time){
  var f=new Flight();var t=new Vector3(1.1f,-.25f);var r=new GrowthPickupRibbonTrail(new Transform(),24,.24f,new Material(),4);var arrived=new bool[24];
  for(int id=0;id<24;id++){r.SetWidth(id,.12f);r.Record(id,f.Position(id,0,t),0);}
  for(float now=1f/120f;now<=time;now+=1f/120f){
   for(int id=0;id<24;id++){if(arrived[id])continue;r.Record(id,f.Position(id,now,t),now);if(now>=f.Arrival(id)){r.Arrive(id,t,now);arrived[id]=true;}}
   r.Render(now,true,t);
  }
  return Mesh.Latest;
 }
}
'@
# The test harness intentionally leaves a zero-valued origin field.
Add-Type -TypeDefinition ("#pragma warning disable 0649`n"+$code)
Write-Output "PASS: $([RibbonChecks]::Run()) production curve/history/mesh assertions."
foreach($required in @('main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;', 'int id = (int)buffer[i].randomSeed - 1;', 'Time.deltaTime <= 0f', 'ribbons.Arrive(', 'ribbons?.Dispose();')) {
 if(!$effect.Contains($required)){throw "Missing safety integration: $required"}
}
$collectible=Get-Content -Raw "$taskRoot/Assets/Scripts/Items/GrowthCollectible.cs"
if(([regex]::Matches($collectible,'growth.AddCollectibles\(')).Count -ne 1 -or $effect.Contains('AddCollectibles(')){throw 'Currency must remain independent and awarded once'}
if(!$collectible.Contains('AddComponent<CameraVisionStreamingExempt>()')){throw 'Preserve streaming exemption'}
Write-Output 'PASS: pause, stable IDs, lifetime ownership, streaming and one-time currency integration.'

if($PreviewPath) {
 Add-Type -AssemblyName System.Drawing
 $bitmap=[System.Drawing.Bitmap]::new(1200,360)
 $g=[System.Drawing.Graphics]::FromImage($bitmap)
 $g.Clear([System.Drawing.Color]::FromArgb(9,18,42))
 $g.SmoothingMode=[System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
 $font=[System.Drawing.Font]::new('Arial',14)
 $caption=[System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(105,200,235))
 $targetPen=[System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(50,125,205),2)
 $flight=[UnityEngine.Flight]::new();$target=[UnityEngine.Vector3]::new(1.1,-.25,0)
 $times=@(.42,.88,1.2);$labels=@('SCATTER / 0.42s','CURVED RETURN / 0.88s','TAIL ABSORPTION / 1.20s')
 for($panel=0;$panel -lt 3;$panel++) {
  $cx=$panel*400+184;$cy=166;$scale=78
  $mesh=[RibbonChecks]::Preview($times[$panel])
  $g.DrawString($labels[$panel],$font,$caption,[single]($panel*400+26),[single]320)
  $g.DrawEllipse($targetPen,[single]($cx+1.1*$scale-11),[single]($cy+.25*$scale-14),[single]22,[single]28)
  for($i=0;$i -lt $mesh.triangles.Count;$i+=3) {
   $points=[System.Drawing.PointF[]]::new(3);$red=0.;$green=0.;$blue=0.;$alpha=0.
   for($j=0;$j -lt 3;$j++) {
    $index=$mesh.triangles[$i+$j];$v=$mesh.vertices[$index];$c=$mesh.colors[$index]
    $points[$j]=[System.Drawing.PointF]::new($cx+$v.x*$scale,$cy-$v.y*$scale)
    $red+=$c.r;$green+=$c.g;$blue+=$c.b;$alpha+=$c.a
   }
   $brush=[System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb([int]($alpha*85),[int]($red*85),[int]($green*85),[int]($blue*85)))
   $g.FillPolygon($brush,$points);$brush.Dispose()
  }
  for($id=0;$id -lt 24;$id++) {
   if($times[$panel] -ge $flight.Arrival($id)){continue}
   $p=$flight.Position($id,$times[$panel],$target)
   $g.FillEllipse([System.Drawing.Brushes]::White,[single]($cx+$p.x*$scale-3.8),[single]($cy-$p.y*$scale-3.8),[single]7.6,[single]7.6)
  }
 }
 $bitmap.Save($PreviewPath,[System.Drawing.Imaging.ImageFormat]::Png)
 $targetPen.Dispose();$caption.Dispose();$font.Dispose();$g.Dispose();$bitmap.Dispose()
 Write-Output "Production-math design preview (not a Unity screenshot): $PreviewPath"
}
