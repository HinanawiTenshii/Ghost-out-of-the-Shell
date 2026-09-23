# Production configuration, population and particle update methods; Unity rendering is stubbed.
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$src=Get-Content -Raw -Encoding UTF8 "$root/Assets/Scripts/Rendering/SpriteWaterFlowParticles.cs"
$wrapper=Get-Content -Raw -Encoding UTF8 "$root/Assets/Scripts/Rendering/SpriteStillWaterSurface.cs"
function Extract($s,$signature){
    $start=$s.IndexOf($signature);if($start -lt 0){throw "Missing $signature"}
    $b=$s.IndexOf('{',$start);$e=$b+1;$depth=1
    while($depth){if($s[$e] -eq '{'){$depth++};if($s[$e] -eq '}'){$depth--};$e++}
    $s.Substring($start,$e-$start)
}
$code=@'
#pragma warning disable 0414
using System;
using UnityEngine;
using Random = UnityEngine.Random;
namespace UnityEngine {
 public class Header:Attribute{public Header(string s){}} public class Min:Attribute{public Min(float x){}}
 public class Tooltip:Attribute{public Tooltip(string s){}} public class RangeAttribute:Attribute{public RangeAttribute(float a,float b){}}
 public struct Color{public float r,g,b,a;public Color(float r,float g,float b,float a){this.r=r;this.g=g;this.b=b;this.a=a;}}
 public struct Vector2{public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}public static Vector2 right=>new Vector2(1,0);public Vector2 normalized{get{float n=(float)Math.Sqrt(x*x+y*y);return n>0?new Vector2(x/n,y/n):new Vector2();}}public static implicit operator Vector3(Vector2 v)=>new Vector3(v.x,v.y,0);}
 public struct Vector3 {
  public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
  public static Vector3 right=>new Vector3(1,0,0);public static Vector3 up=>new Vector3(0,1,0);
  public float magnitude=>(float)Math.Sqrt(x*x+y*y+z*z);public Vector3 normalized=>magnitude>0?this*(1/magnitude):new Vector3();
  public static Vector3 operator +(Vector3 a,Vector3 b)=>new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);
  public static Vector3 operator -(Vector3 a,Vector3 b)=>new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);
  public static Vector3 operator *(Vector3 a,float b)=>new Vector3(a.x*b,a.y*b,a.z*b);
  public static float Dot(Vector3 a,Vector3 b)=>a.x*b.x+a.y*b.y+a.z*b.z;
  public static Vector3 Cross(Vector3 a,Vector3 b)=>new Vector3(a.y*b.z-a.z*b.y,a.z*b.x-a.x*b.z,a.x*b.y-a.y*b.x);
 }
 public struct Matrix4x4 {
  public float a,b,c,d,tx,ty;
  public static Matrix4x4 identity=>new Matrix4x4{a=1,d=1};
  public Matrix4x4 inverse{get{float det=a*d-b*c;return new Matrix4x4{a=d/det,b=-b/det,c=-c/det,d=a/det,tx=(b*ty-d*tx)/det,ty=(c*tx-a*ty)/det};}}
  public Vector3 MultiplyPoint3x4(Vector3 p)=>new Vector3(a*p.x+b*p.y+tx,c*p.x+d*p.y+ty,p.z);
  public static Matrix4x4 operator *(Matrix4x4 x,Matrix4x4 y)=>new Matrix4x4{a=x.a*y.a+x.b*y.c,b=x.a*y.b+x.b*y.d,c=x.c*y.a+x.d*y.c,d=x.c*y.b+x.d*y.d,tx=x.a*y.tx+x.b*y.ty+x.tx,ty=x.c*y.tx+x.d*y.ty+x.ty};
 }
 public class Transform {
  public Matrix4x4 localToWorldMatrix=Matrix4x4.identity;
  public Vector3 TransformVector(Vector3 p){var m=localToWorldMatrix;return new Vector3(m.a*p.x+m.b*p.y,m.c*p.x+m.d*p.y,p.z);}
  public Vector3 TransformPoint(Vector3 p)=>localToWorldMatrix.MultiplyPoint3x4(p);
  public Vector3 InverseTransformPoint(Vector3 p)=>localToWorldMatrix.inverse.MultiplyPoint3x4(p);
 }
 public class Bounds{public Vector3 min=new Vector3(-.5f,-.5f,0),max=new Vector3(.5f,.5f,0),size=new Vector3(1,1,0),center;}
 public class SpriteRenderer{public Transform transform=new Transform();public Bounds bounds=new Bounds();}
 public static class Mathf{
  public const float Rad2Deg=57.29578f;public static float Max(float a,float b)=>Math.Max(a,b);public static int Max(int a,int b)=>Math.Max(a,b);public static int Min(int a,int b)=>Math.Min(a,b);
  public static float Clamp(float a,float b,float c)=>Math.Max(b,Math.Min(a,c));public static int Clamp(int a,int b,int c)=>Math.Max(b,Math.Min(a,c));public static float Clamp01(float a)=>Clamp(a,0,1);
  public static int RoundToInt(float a)=>(int)Math.Round(a);public static int NextPowerOfTwo(int a){int n=1;while(n<a)n*=2;return n;}
  public static float Sin(float a)=>(float)Math.Sin(a);public static float Atan2(float a,float b)=>(float)Math.Atan2(a,b);
  public static float Lerp(float a,float b,float t)=>a+(b-a)*t;public static float Repeat(float a,float b)=>a-(float)Math.Floor(a/b)*b;
 }
 public static class Random{public static float Range(float a,float b)=>(a+b)/2;}
 public class ParticleSystem{
  public struct Particle{public Vector3 position,velocity;public float remainingLifetime,rotation;public uint randomSeed;}
  public class Main{public int maxParticles;}public Main main=new Main();public Particle[] live=new Particle[0];
  public int GetParticles(Particle[] b){Array.Copy(live,b,live.Length);return live.Length;}
  public void SetParticles(Particle[] b,int n){live=new Particle[n];Array.Copy(b,live,n);}
 }
}
public class WaterHarness{
'@
$code+=(Extract $src '    public sealed class StillWaterSettings').Replace('[Range(', '[UnityEngine.RangeAttribute(')
$code+=@'
 private SpriteRenderer sourceRenderer;private Vector2 flowDirection=Vector2.right;
 private bool stationarySurface,localDirection,followSpriteShape,checkReadableAlpha,keepInsideSprite,refreshRequired;
 private float speed=.5f,speedRandomness=.25f,directionSpread,currentSway,lifetime,particlesPerSquareUnit,ribbonDensityMultiplier,particleSize,particleLength,ribbonLengthScale,ribbonWidthScale,shapeAnimationSpeed,horizontalVisibilityBoost,alphaThreshold,localSurfaceArea=1,flowTime;
 private int sortingOrderOffset,distributionCells,particleSafetyLimit,maximumParticles;private Color particleColor;
 private ParticleSystem particles=new ParticleSystem();private ParticleSystem.Particle[] buffer;
 private Matrix4x4 previousSurfaceToWorld;private Bounds surfaceBounds=new Bounds();
 public float SurfaceWorldArea{get;private set;}public int TargetParticleCount{get;private set;}
 private bool IsInsideSurface(Vector3 p)=>p.x>=-.5f&&p.x<=.5f&&p.y>=-.5f&&p.y<=.5f;
 public void Init(SpriteRenderer r,StillWaterSettings s){ConfigureStillWater(r,s);previousSurfaceToWorld=r.transform.localToWorldMatrix;UpdateAreaAndCapacity();}
 public void Seed(Vector3 p){particles.live=new[]{new ParticleSystem.Particle{position=p,remainingLifetime=2,randomSeed=41}};}
 public void Step(){flowTime+=.016f;UpdateAreaAndCapacity();UpdateCurrentParticles();}
 public ParticleSystem.Particle Particle=>particles.live[0];
 public int Count=>particles.live.Length;public int Capacity=>buffer.Length;
 public float AnimationSpeed=>shapeAnimationSpeed;public bool CurrentDisabled=>speed==0&&speedRandomness==0&&directionSpread==0&&currentSway==0;
 public void OrdinaryFlow(){stationarySurface=false;keepInsideSprite=false;speed=.5f;speedRandomness=0;}
'@
foreach($sig in @('    public void ConfigureStillWater(','    private void UpdateAreaAndCapacity()', '    private Vector3 WorldFlowDirection()', '    private int UpdateCurrentParticles()')){$code+=Extract $src $sig}
$code+="`n}`n"
$code+=@'
public static class StillWaterTests{
 static int count;static void Check(bool v,string why){count++;if(!v)throw new Exception(why);}
 static bool Near(float a,float b)=>Math.Abs(a-b)<.0001;
 public static int Run(){
  var source=new SpriteRenderer();source.transform.localToWorldMatrix=new Matrix4x4{a=4,d=4};
  var water=new WaterHarness();var settings=new WaterHarness.StillWaterSettings();water.Init(source,settings);
  Check(water.CurrentDisabled,"Standing water disables all directional current");
  Check(Near(water.SurfaceWorldArea,16)&&water.TargetParticleCount==10,"Area-scaled density");
  settings.lifetime=12;water.Init(source,settings);Check(water.TargetParticleCount==10,"Density independent of lifetime");
  settings.lifetime=.2f;water.Init(source,settings);Check(water.TargetParticleCount==10,"Short lifetime preserves density");
  water.Seed(new Vector3(.4f,.4f,0));for(int i=0;i<600;i++)water.Step();
  Check(Near(water.Particle.position.x,.4f)&&Near(water.Particle.position.y,.4f)&&water.Particle.velocity.magnitude==0,"No accumulated drift over 600 updates");
  source.transform.localToWorldMatrix=new Matrix4x4{a=4,d=4,tx=3,ty=-2};water.Step();
  Check(Near(water.Particle.position.x,3.4f)&&Near(water.Particle.position.y,-1.6f),"Translation follows pool");
  source.transform.localToWorldMatrix=new Matrix4x4{b=-4,c=4,tx=3,ty=-2};water.Step();
  Check(Near(water.Particle.position.x,2.6f)&&Near(water.Particle.position.y,-1.6f),"Rotation follows pool");
  source.transform.localToWorldMatrix=new Matrix4x4{a=-2,d=3,tx=3,ty=-2};water.Step();
  Check(Near(water.Particle.position.x,2.8f)&&Near(water.Particle.position.y,-1.7f),"Nonuniform negative scale preserves local anchor");
  Check(Near(water.SurfaceWorldArea,6)&&water.TargetParticleCount==4,"Area handles negative scale");
  settings.animationSpeed=0;water.Init(source,settings);Check(water.AnimationSpeed==0&&water.CurrentDisabled,"Shape animation can stop without enabling current");
  settings.density=0;water.Init(source,settings);water.Seed(new Vector3(3,-2,0));water.Step();Check(water.TargetParticleCount==0&&water.Count==0,"Zero density clears population");
  settings.density=10000;settings.particleLimit=512;water.Init(source,settings);Check(water.TargetParticleCount==512&&water.Capacity<=512,"Large surface count capped");
  settings.density=-1;settings.lifetime=-1;settings.particleLimit=1;settings.animationSpeed=-1;water.Init(source,settings);Check(water.TargetParticleCount==0&&water.AnimationSpeed==0,"Invalid runtime settings safely clamped");
  water.Init(source,null);Check(water.CurrentDisabled&&water.TargetParticleCount==4,"Null settings use calm defaults");
  source.transform.localToWorldMatrix=Matrix4x4.identity;water.Init(source,new WaterHarness.StillWaterSettings{density=4});water.Seed(new Vector3(.1f,.1f,0));water.OrdinaryFlow();
  source.transform.localToWorldMatrix=new Matrix4x4{a=1,d=1,tx=3};water.Step();
  Check(Near(water.Particle.position.x,.1f)&&Near(water.Particle.velocity.x,.5f),"Ordinary flow keeps world simulation and velocity");
  return count;
 }
}
'@
Add-Type -TypeDefinition $code -WarningAction SilentlyContinue
Write-Output ("PASS: "+[StillWaterTests]::Run()+" production configuration/population/no-drift/transform/regression checks; engine particles stubbed.")
foreach($s in @('runtimeEffect.ConfigureStillWater(sourceRenderer, settings);','runtimeEffect.gameObject.SetActive(false);','Destroy(runtimeEffect.gameObject);','HideFlags.HideAndDontSave','configuredRenderer == sourceRenderer')){if(!$wrapper.Contains($s)){throw "Missing wrapper lifecycle safeguard $s"}}
if($wrapper.Contains('sourceRenderer.material =') -or $wrapper.Contains('sourceRenderer.sprite =')){throw 'Original water surface must not be overwritten'}
Write-Output 'PASS: owned runtime helper, lifecycle cleanup and source renderer preservation wiring.'
