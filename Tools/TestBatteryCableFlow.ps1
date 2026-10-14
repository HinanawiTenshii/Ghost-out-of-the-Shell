$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$source=Get-Content -Raw "$root/Assets/Scripts/Interaction/BatterySocketCable.cs"
function Extract([string]$signature){
 $start=$source.IndexOf($signature);if($start -lt 0){throw "Missing $signature"}
 $b=$source.IndexOf('{',$start);$e=$b+1;$depth=1
 while($depth){if($source[$e] -eq '{'){$depth++};if($source[$e] -eq '}'){$depth--};$e++}
 $source.Substring($start,$e-$start)
}
$stub=@'
using System;
using System.Collections.Generic;
public struct Vector2 {
 public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}
 public float sqrMagnitude=>x*x+y*y;
 public Vector2 normalized=>this*(1/(float)Math.Sqrt(sqrMagnitude));
 public static Vector2 operator+(Vector2 a,Vector2 b)=>new Vector2(a.x+b.x,a.y+b.y);
 public static Vector2 operator-(Vector2 a,Vector2 b)=>new Vector2(a.x-b.x,a.y-b.y);
 public static Vector2 operator*(Vector2 a,float s)=>new Vector2(a.x*s,a.y*s);
 public static float Dot(Vector2 a,Vector2 b)=>a.x*b.x+a.y*b.y;
 public static float Distance(Vector2 a,Vector2 b)=>(float)Math.Sqrt((a-b).sqrMagnitude);
}
public static class Mathf {
 public static float Max(float a,float b)=>Math.Max(a,b);public static float Min(float a,float b)=>Math.Min(a,b);
 public static float Repeat(float x,float s)=>x-(float)Math.Floor(x/s)*s;
}
public static class Application { public static bool isPlaying=true; }
public static class Time { public static float deltaTime=.1f; }
public class Material { public float travel; public void SetFloat(int id,float value){travel=value;} }
public class MeshRenderer { public bool enabled; }
public class GameObject { public bool activeInHierarchy=true; }
public class BatterySocket { public bool HasBattery=true; public int RemainingCharge=5; public bool HasPower=>HasBattery&&RemainingCharge>0; }
public class FlowHarness {
 public bool showCurrentFlow=true, HasRoute=true,currentFlowRunning;
 public float currentFlowIntensity=1.2f,currentRouteLength=8,currentTravel,currentFlowSpeed=2.4f,currentFlowInterval=2;
 public BatterySocket socket=new BatterySocket(); public GameObject generated=new GameObject();
 public MeshRenderer currentFlowRenderer=new MeshRenderer(); public Material currentFlowMaterial=new Material();
 public const int FlowTravelId=1;
'@
$methods=@('private static float BuildFlowStrip(', 'private static float AdvanceFlowTravel(', 'private void UpdateCurrentFlow()', 'private void StopCurrentFlow()')
$compiled=$stub+(($methods|ForEach-Object{(Extract $_).Replace('private ','public ')}) -join "`n")+'}'
$tests=@'
public static class FlowTests {
 static int checks;
 static void Check(bool b,string message){checks++;if(!b)throw new Exception(message);}
 static void Near(float a,float b,string message){Check(Math.Abs(a-b)<.0001f,message+": "+a+" vs "+b);}
 static Vector2 V(float x,float y)=>new Vector2(x,y);
 static List<Vector2> pts=new List<Vector2>(),uv=new List<Vector2>();static List<int> indices=new List<int>();
 static void Geometry(Vector2[] path,float expected){
  float length=FlowHarness.BuildFlowStrip(path,.256f,pts,uv,indices);
  Near(length,expected,"World arc length");Check(pts.Count==uv.Count,"UV per vertex");
  Check(indices.Count==(pts.Count/2-1)*6,"Two triangles per route segment");
  Near(uv[0].x,0,"Start at battery");Near(uv[uv.Count-1].x,length,"End at connected mechanism");
  for(int i=0;i<pts.Count;i+=2){
   Near(uv[i].x,uv[i+1].x,"Cross section has one arc length");Near(uv[i].y,0,"Ribbon right edge");Near(uv[i+1].y,1,"Ribbon left edge");
   if(i>0)Check(uv[i].x>uv[i-2].x,"Always positive progress, including reverse-facing routes");
   Check(!float.IsNaN(pts[i].x)&&!float.IsInfinity(pts[i].y),"Finite geometry");
  }
  for(int i=0;i<indices.Count;i+=3){
   var a=pts[indices[i]];var b=pts[indices[i+1]];var c=pts[indices[i+2]];
   Check((b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x)<0,"No inverted/degenerate triangles at short elbows");
  }
 }
 public static int Run(){
  Geometry(new[]{V(0,0),V(4,0)},4);
  Geometry(new[]{V(0,0),V(0,-2),V(3,-2),V(3,1)},8);
  Geometry(new[]{V(3,1),V(3,-2),V(0,-2),V(0,0)},8);
  Geometry(new[]{V(5,8),V(5,8),V(5,5),V(9,5),V(9,5)},7);
  Geometry(new[]{V(0,0),V(1,0),V(1,.001f),V(2,.001f)},2.001f);
  for(int i=0;i<100;i++){
   float scale=.003f+i*.037f;
   Geometry(new[]{V(0,0),V(0,-scale),V(scale,-scale),V(scale,scale),V(3*scale,scale)},6*scale);
  }
  Check(FlowHarness.BuildFlowStrip(null,.2f,pts,uv,indices)==0&&pts.Count==0&&uv.Count==0&&indices.Count==0,"Null path clears old geometry");
  Check(FlowHarness.BuildFlowStrip(new[]{V(1,1),V(1,1)},.2f,pts,uv,indices)==0,"Coincident ports");
  Check(FlowHarness.BuildFlowStrip(new[]{V(0,0),V(1,0)},0,pts,uv,indices)==0,"Zero width");
  float s=4.8f,l=13.25f,travel=0;
  for(int i=0;i<50000;i++){
   float next=FlowHarness.AdvanceFlowTravel(travel,.017f,2.4f,l,s);
   Check(next<=l+s+.0001f,"Long sessions stay numerically bounded");
   float expected=travel+.017f*2.4f;
   Near(Mathf.Repeat(expected-next,s)<.0001f?0:Mathf.Repeat(expected-next,s),0,"Wrapping preserves pulse phase");
   if(expected>l+s)Check(next>=l,"Never hide downstream pulses after phase wrap");
   travel=next;
  }
  Near(FlowHarness.AdvanceFlowTravel(2,0,2.4f,10,4.8f),2,"Pause stops travel");
  Near(FlowHarness.AdvanceFlowTravel(2,-.2f,2.4f,10,4.8f),2,"Negative timestep safe");
  var h=new FlowHarness();h.UpdateCurrentFlow();Check(h.currentFlowRenderer.enabled,"Powered existing socket starts effect");Near(h.currentTravel,0,"First wave starts at source");
  h.UpdateCurrentFlow();Near(h.currentTravel,.24f,"Moves toward target");Near(h.currentFlowMaterial.travel,.24f,"Upload travel only");
  Time.deltaTime=0;h.UpdateCurrentFlow();Near(h.currentTravel,.24f,"Pause integration");Time.deltaTime=.1f;
  h.socket.RemainingCharge=0;h.UpdateCurrentFlow();Check(!h.currentFlowRenderer.enabled&&!h.currentFlowRunning,"Depletion stops immediately");
  h.socket.RemainingCharge=5;h.UpdateCurrentFlow();Near(h.currentTravel,0,"Charged replacement restarts at battery");
  h.socket.HasBattery=false;h.UpdateCurrentFlow();Check(!h.currentFlowRenderer.enabled,"Removal/explosion clears effect");h.socket.HasBattery=true;
  h.HasRoute=false;h.UpdateCurrentFlow();Check(!h.currentFlowRenderer.enabled,"Unbinding/failed route hides effect");h.HasRoute=true;
  h.generated.activeInHierarchy=false;h.UpdateCurrentFlow();Check(!h.currentFlowRenderer.enabled,"Inactive cable hides effect");h.generated.activeInHierarchy=true;
  h.showCurrentFlow=false;h.UpdateCurrentFlow();Check(!h.currentFlowRenderer.enabled,"Optional disable");h.showCurrentFlow=true;
  h.currentFlowIntensity=0;h.UpdateCurrentFlow();Check(!h.currentFlowRenderer.enabled,"Zero intensity disables renderer");h.currentFlowIntensity=1.2f;
  Application.isPlaying=false;h.UpdateCurrentFlow();Check(!h.currentFlowRenderer.enabled,"No editor-time animation");Application.isPlaying=true;
  h.UpdateCurrentFlow();h.StopCurrentFlow();Check(!h.currentFlowRenderer.enabled&&!h.currentFlowRunning,"Disable resets phase and rendering");
  h.socket=null;h.UpdateCurrentFlow();Check(!h.currentFlowRenderer.enabled,"Destroyed socket safe");
  h.currentFlowRenderer=null;h.UpdateCurrentFlow();h.currentFlowMaterial=null;h.UpdateCurrentFlow();
  return checks;
 }
}
'@
Add-Type -TypeDefinition ($compiled+$tests)
$checks=[FlowTests]::Run()
$build=Extract 'private void BuildCurrentFlow()'
$update=Extract 'private void UpdateCurrentFlow()'
foreach($guard in @('Resources.Load<Shader>("BatteryCableFlow")','currentFlowRenderer.gameObject.layer = 0;','currentFlowRenderer.sortingLayerID = 0;','currentFlowRenderer.sortingOrder = Mathf.Min(-1, sortingOrder);','transform.InverseTransformPoint','spacing * .6f')){
 if(!$build.Contains($guard)){throw "Missing integration guard: $guard"};$checks++
}
if(!(Extract 'private void DrawRoute()').Contains('BuildCurrentFlow();')){throw 'Route rebuild must refresh UV distances'}
if(!(Extract 'private void LateUpdate()').Contains('UpdateCurrentFlow();')){throw 'Missing frame update'}
if(!(Extract 'private void OnDisable()').Contains('StopCurrentFlow();')){throw 'Disable must stop'}
if(!(Extract 'private void OnDestroy()').Contains('Release(currentFlowMesh); Release(currentFlowMaterial);')){throw 'Owned render resources must be released'}
if($update -match 'TryRoute|DrawRoute|SetVertices|SetTriangles|new List|ConsumeCharge'){throw 'Animation must not reroute, allocate mesh data or consume power'}
$shader=Get-Content -Raw "$root/Assets/Resources/BatteryCableFlow.shader"
foreach($guard in @('Blend One One','ZWrite Off','ZTest LEqual','_FlowTravel - i.uv.x','step(0, behind)','float3(1, .14, .065)')){
 if(!$shader.Contains($guard)){throw "Missing flow shader guard: $guard"};$checks++
}
if($shader.Contains('_Time')){throw 'Use supplied scaled time so pause freezes flow'}
$prefab=Get-Content -Raw "$root/Assets/Prefabs/Decorations/BatterySocket.prefab"
if(!$prefab.Contains('showCurrentFlow: 1')){throw 'Enable on existing socket prefab'}
Write-Output "PASS: $($checks+7) current-flow geometry, phase, lifecycle, power, pause and render integration checks."
