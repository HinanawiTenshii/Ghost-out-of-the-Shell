$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$rules=Get-Content -Raw "$root/Assets/Scripts/Interaction/ElectricGateRules.cs"
$network=Get-Content -Raw "$root/Assets/Scripts/Interaction/ElectricGateNetwork.cs"
$visual=Get-Content -Raw "$root/Assets/Scripts/Interaction/ElectricGatePillarVisual.cs"
function Extract($source,$signature){
 $start=$source.IndexOf($signature);if($start -lt 0){throw "Missing $signature"}
 $b=$source.IndexOf('{',$start);$e=$b+1;$depth=1
 while($depth){if($source[$e] -eq '{'){$depth++};if($source[$e] -eq '}'){$depth--};$e++}
 $source.Substring($start,$e-$start)
}
$stub=@'
namespace UnityEngine {
 public struct Vector2 {
  public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}
  public static Vector2 zero=>new Vector2();public float sqrMagnitude=>x*x+y*y;public float magnitude=>(float)System.Math.Sqrt(sqrMagnitude);
  public Vector2 normalized=>magnitude<.000001f?zero:this*(1/magnitude);
  public static Vector2 operator +(Vector2 a,Vector2 b)=>new Vector2(a.x+b.x,a.y+b.y);
  public static Vector2 operator -(Vector2 a,Vector2 b)=>new Vector2(a.x-b.x,a.y-b.y);
  public static Vector2 operator *(Vector2 a,float f)=>new Vector2(a.x*f,a.y*f);
  public static float Dot(Vector2 a,Vector2 b)=>a.x*b.x+a.y*b.y;public static float Distance(Vector2 a,Vector2 b)=>(a-b).magnitude;
  public static Vector2 Lerp(Vector2 a,Vector2 b,float t)=>a+(b-a)*t;
  public static implicit operator Vector2(Vector3 v)=>new Vector2(v.x,v.y);
 }
 public struct Vector3 {public float x,y,z;public Vector3(float x,float y,float z=0){this.x=x;this.y=y;this.z=z;}}
 public struct Color {public float r,g,b,a;public Color(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;}}
 public static class Mathf {
  public const float PI=(float)System.Math.PI;
  public static float Sin(float x)=>(float)System.Math.Sin(x);public static float Abs(float x)=>System.Math.Abs(x);
  public static float Min(float a,float b)=>System.Math.Min(a,b);public static float Max(float a,float b)=>System.Math.Max(a,b);
  public static float Clamp(float v,float a,float b)=>Max(a,Min(v,b));public static int Clamp(int v,int a,int b)=>System.Math.Max(a,System.Math.Min(v,b));
  public static float Clamp01(float v)=>Clamp(v,0,1);public static int CeilToInt(float v)=>(int)System.Math.Ceiling(v);
  public static float SmoothStep(float a,float b,float t){t=Clamp01(t);return a+(b-a)*t*t*(3-2*t);}
 }
 public static class Time {public static float time;}
 public static class Application {public static bool isPlaying=true;}
 public static class Random {public static float value=.5f;public static float Range(float a,float b)=>(a+b)*.5f;}
 public class GameObject {public bool activeInHierarchy=true;public int scene=1;}
 public class Transform {public Vector3 position;}
 public class Bounds {public Vector3 extents=new Vector3(.15f,.15f);}
 public class BoxCollider2D {public Bounds bounds=new Bounds();}
 public class LineRenderer {public bool enabled;public Color startColor,endColor;}
 public class SpriteRenderer {public Color color;}
 public class MeshRenderer {public bool enabled;}
 public class Material {public Color color;public float intensity;public void SetColor(int id,Color c){color=c;}public void SetFloat(int id,float n){intensity=n;}}
 public class ParticleSystem {
  public struct EmitParams {public Vector3 position;public Color startColor;public float startSize,startLifetime;}
  public readonly System.Collections.Generic.List<EmitParams> particles=new System.Collections.Generic.List<EmitParams>();
  public int clears;public void Clear(){particles.Clear();clears++;}public void Emit(EmitParams p,int n){for(int i=0;i<n;i++)particles.Add(p);}
 }
}
public class ZeldaCharacterData {
 public bool IsDead,IsGhostLike;public int PermissionLevel=3;public string IndividualAccessId="one";
 public UnityEngine.GameObject gameObject=new UnityEngine.GameObject();
}
public class ZeldaFourWayMover {
 public ZeldaCharacterData data=new ZeldaCharacterData();public UnityEngine.GameObject gameObject=>data.gameObject;
 public UnityEngine.Transform transform=new UnityEngine.Transform();public UnityEngine.BoxCollider2D collider=new UnityEngine.BoxCollider2D();
 public T GetComponent<T>() where T:class=>typeof(T)==typeof(ZeldaCharacterData)?data as T:collider as T;
}
public static class ZeldaRuntimeRegistry {public static int GetGameplayScene(UnityEngine.GameObject go)=>go.scene;}
public class ElectricGatePillar {
 public UnityEngine.GameObject gameObject=new UnityEngine.GameObject();public UnityEngine.Transform transform=new UnityEngine.Transform();
 public bool Powered=true,HasPanel=true,isActiveAndEnabled=true,individual;public int threshold=3;public string identity="one";
 public ElectricGateRules.SignalState Signal;
 public bool Allows(ZeldaCharacterData d)=>!d.IsGhostLike&&ElectricGateRules.Allows(individual,threshold,identity,d.PermissionLevel,d.IndividualAccessId);
 public void ReportSignal(ElectricGateRules.SignalState s){Signal=ElectricGateRules.MergeSignal(Signal,s);}
}
public static class BatterySocket {
 public static int transactions;public static bool deplete;
 public static bool TryConsumeForMechanisms(ElectricGatePillar a,ElectricGatePillar b){transactions++;if(deplete){if(a!=null)a.Powered=false;if(b!=null)b.Powered=false;}return true;}
}
public class ElectricGateAudio {public bool active,warning;public void SetState(bool a,bool w){active=a;warning=w;}}
public class SignalHarness {
 public ElectricGatePillar A=new ElectricGatePillar(),B=new ElectricGatePillar{HasPanel=false};
 public UnityEngine.Vector2 Start=>A.transform.position;public UnityEngine.Vector2 End=>B.transform.position;
 public bool Active=>A.Powered&&B.Powered&&A.isActiveAndEnabled&&B.isActiveAndEnabled;
 public UnityEngine.ParticleSystem sparks=new UnityEngine.ParticleSystem();
 public UnityEngine.LineRenderer[] strands={new UnityEngine.LineRenderer(),new UnityEngine.LineRenderer(),new UnityEngine.LineRenderer()};
 public UnityEngine.LineRenderer[] arcs={new UnityEngine.LineRenderer(),new UnityEngine.LineRenderer(),new UnityEngine.LineRenderer(),new UnityEngine.LineRenderer()};
 public System.Collections.Generic.Dictionary<ZeldaCharacterData,UnityEngine.Vector2> previous=new System.Collections.Generic.Dictionary<ZeldaCharacterData,UnityEngine.Vector2>();
 public System.Collections.Generic.HashSet<ZeldaCharacterData> touching=new System.Collections.Generic.HashSet<ZeldaCharacterData>();
 public System.Collections.Generic.List<ZeldaCharacterData> stale=new System.Collections.Generic.List<ZeldaCharacterData>();
 public float nextFlicker;public float[] arcUntil=new float[2];public UnityEngine.Vector2[] arcPoints=new UnityEngine.Vector2[2];public bool softGlow=true,previouslyPassable;
 public System.Collections.Generic.HashSet<ZeldaCharacterData> pendingTargets=new System.Collections.Generic.HashSet<ZeldaCharacterData>();
 public void AdvancePendingStrikes(){} public void CancelStrikes(){}
 public ElectricGateRules.SignalState signal;public ElectricGateAudio audio=new ElectricGateAudio();public int strikes;
 public void Strike(ZeldaCharacterData data,UnityEngine.Vector2 old,UnityEngine.Vector2 current){strikes++;}
 public void Zigzag(UnityEngine.LineRenderer line,UnityEngine.Vector2 a,UnityEngine.Vector2 b,float spread,float bow=0){}
'@
$harness=$stub+((@('private bool Allows(','public void Tick(','public void PublishSignal(')|ForEach-Object{Extract $network $_}) -join "`n")+"}`n"
$harness+=@'
public class LampHarness {
 public ElectricGatePillar pillar=new ElectricGatePillar();public UnityEngine.SpriteRenderer conductor=new UnityEngine.SpriteRenderer(),cap=new UnityEngine.SpriteRenderer();
 public UnityEngine.MeshRenderer glowRenderer=new UnityEngine.MeshRenderer();public UnityEngine.Material glowMaterial=new UnityEngine.Material();
 public float indicatorGlowIntensity=1.15f,indicatorBreathPeriod=2.8f;public const int GlowColorId=0,GlowIntensityId=1;
'@
$harness+=((@('public static Color IndicatorColor(','public static float BreathMultiplier(','private void ApplySignal()')|ForEach-Object{(Extract $visual $_).Replace('private void','public void')}) -join "`n")+"}`n"
$tests=@'
public static class SignalTests {
 static int n;static void Check(bool b,string m){n++;if(!b)throw new System.Exception(m);}
 static void Tick(SignalHarness h,params ZeldaFourWayMover[] movers){Time.time+=.05f;h.sparks.Clear();h.Tick(new System.Collections.Generic.List<ZeldaFourWayMover>(movers));h.A.Signal=h.B.Signal=ElectricGateRules.SignalState.Off;h.PublishSignal();}
 static void State(SignalHarness h,ElectricGateRules.SignalState s){Check(h.signal==s&&h.A.Signal==s&&h.B.Signal==s,"Both endpoints share "+s);}
 public static int Run(){
  var h=new SignalHarness();h.B.transform.position=new Vector3(8,0);Tick(h);State(h,ElectricGateRules.SignalState.Ready);
  int baseCount=h.sparks.particles.Count;Check(baseCount==4&&!h.audio.warning,"Normal current density and hum");
  var allowed=new ZeldaFourWayMover();allowed.transform.position=new Vector3(4,1);
  Tick(h,allowed);State(h,ElectricGateRules.SignalState.Passage);
  Check(h.sparks.particles.Count==baseCount&&!h.strands[1].enabled&&!h.strands[2].enabled,"Blue current never multiplies particles or strands");
  Check(!h.audio.warning&&h.strikes==0,"Allowed entrant has no warning audio or damage");
  foreach(var p in h.sparks.particles)Check(p.startColor.b>p.startColor.r*3,"Every emitted passage spark is blue");
  Check(h.strands[0].startColor.b==1&&h.strands[0].endColor.b==1,"Both ends of current are blue");
  allowed.transform.position=new Vector3(4,0);Tick(h,allowed);Check(h.strikes==0&&BatterySocket.transactions==0,"Allowed crossing costs no charge");
  allowed.transform.position=new Vector3(4,3);Tick(h,allowed);State(h,ElectricGateRules.SignalState.Ready);
  allowed.transform.position=new Vector3(4,1);allowed.data.IsGhostLike=true;Tick(h,allowed);State(h,ElectricGateRules.SignalState.Ready);allowed.data.IsGhostLike=false;
  allowed.data.IsDead=true;Tick(h,allowed);State(h,ElectricGateRules.SignalState.Ready);allowed.data.IsDead=false;
  allowed.gameObject.scene=2;Tick(h,allowed);State(h,ElectricGateRules.SignalState.Ready);allowed.gameObject.scene=1;
  var denied=new ZeldaFourWayMover();denied.data.PermissionLevel=2;denied.transform.position=new Vector3(4,1);
  Tick(h,denied);State(h,ElectricGateRules.SignalState.Warning);
  Check(h.sparks.particles.Count==16&&h.strands[1].enabled&&h.audio.warning,"Unauthorized nearby keeps original warning density and sound");
  Tick(h,allowed,denied);State(h,ElectricGateRules.SignalState.Warning);Tick(h,denied,allowed);State(h,ElectricGateRules.SignalState.Warning);
  Tick(h,allowed);State(h,ElectricGateRules.SignalState.Passage);Check(h.sparks.particles.Count==baseCount,"Returns to base density after denied visitor leaves");
  h.A.individual=true;allowed.data.PermissionLevel=-2;Tick(h,allowed);State(h,ElectricGateRules.SignalState.Passage);
  h.A.identity="different";Tick(h,allowed);State(h,ElectricGateRules.SignalState.Warning);
  h.A.individual=false;allowed.data.PermissionLevel=9;h.B.HasPanel=true;h.B.individual=true;h.B.identity="other";
  Tick(h,allowed);State(h,ElectricGateRules.SignalState.Warning);h.B.individual=false;Tick(h,allowed);State(h,ElectricGateRules.SignalState.Passage);
  h.A.Powered=false;Tick(h,allowed);State(h,ElectricGateRules.SignalState.Off);Check(h.sparks.particles.Count==0&&!h.strands[0].enabled&&!h.audio.active,"No power: no current or hum");
  h.A.Powered=true;h.B.Powered=false;Tick(h);State(h,ElectricGateRules.SignalState.Off);h.B.Powered=true;
  Tick(h,denied);denied.transform.position=new Vector3(4,0);BatterySocket.deplete=true;Tick(h,denied);State(h,ElectricGateRules.SignalState.Off);
  Check(h.strikes==1&&BatterySocket.transactions==1,"Last-charge attack preserved; both lamps immediately dark");
  h.A.Powered=h.B.Powered=true;Tick(h,allowed);h.A.Signal=h.B.Signal=ElectricGateRules.SignalState.Off;h.A.Powered=false;h.PublishSignal();
  Check(h.A.Signal==ElectricGateRules.SignalState.Off&&h.B.Signal==ElectricGateRules.SignalState.Off,"Later link depletion checked before publication");
  var merged=ElectricGateRules.MergeSignal(ElectricGateRules.SignalState.Off,ElectricGateRules.SignalState.Passage);
  Check(merged==ElectricGateRules.SignalState.Passage,"Dead link does not suppress another live link");
  Check(ElectricGateRules.MergeSignal(merged,ElectricGateRules.SignalState.Warning)==ElectricGateRules.SignalState.Warning,"Multi-link warning priority");
  var lamp=new LampHarness();lamp.ApplySignal();Check(!lamp.glowRenderer.enabled&&lamp.conductor.color.r<.2f,"Off conductor dark and no halo");
  lamp.pillar.Signal=ElectricGateRules.SignalState.Ready;lamp.ApplySignal();Check(lamp.glowRenderer.enabled&&lamp.conductor.color.r==.85f,"Original powered red restored");
  lamp.pillar.Signal=ElectricGateRules.SignalState.Passage;lamp.ApplySignal();Check(lamp.conductor.color.b==1&&lamp.cap.color.b==1&&lamp.glowMaterial.color.b==1,"Body, cap and halo all blue");
  for(int i=0;i<1000;i++){float b=LampHarness.BreathMultiplier(i*.01f,2.8f);Check(b>=.7599f&&b<=1.2401f,"Gentle breath never switches lamp off");}
  float start=LampHarness.BreathMultiplier(0,2.8f);Check(System.Math.Abs(start-LampHarness.BreathMultiplier(2.8f,2.8f))<.00001f,"Breath cycle seamless");
  lamp.indicatorGlowIntensity=0;lamp.ApplySignal();Check(!lamp.glowRenderer.enabled,"Optional glow disable retains colored lamp");
  return n;
 }
}
'@
Add-Type -TypeDefinition ("using System.Collections.Generic;`n"+$rules+"`n"+$harness+"`n"+$tests)
$checks=[SignalTests]::Run()
foreach($fragment in @('foreach (var pillar in Pillars) if (pillar != null) pillar.ResetSignal();','foreach (var field in fields.Values) field.PublishSignal();','sparks.Clear(); nextFlicker = 0f;')){
 if(!$network.Contains($fragment)){throw "Missing network guard: $fragment"};$checks++
}
foreach($fragment in @('Resources.Load<Shader>("WarmLightGlow")','glowRenderer.sortingLayerID = 0;','Release(glowMesh); Release(glowMaterial);','BreathMultiplier(Time.time, indicatorBreathPeriod)')){
 if(!$visual.Contains($fragment)){throw "Missing visual guard: $fragment"};$checks++
}
$shader=Get-Content -Raw "$root/Assets/Resources/ElectricGateGlow.shader"
if(!$shader.Contains('lerp(input.color.rgb, float3(1.0, 1.0, 1.0), 0.8)')){throw 'Bright core must preserve blue signal hue'}
Write-Output "PASS: $($checks+1) actual field Tick, permissions, warning density, pair power, signal color and breathing checks."
