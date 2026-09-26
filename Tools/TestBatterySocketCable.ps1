$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$router=Get-Content -Raw "$root/Assets/Scripts/Interaction/OrthogonalCableRouter.cs"
$stubs=@'
namespace UnityEngine {
 public struct Vector2 {public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}public static Vector2 zero=>new Vector2();public static Vector2 operator -(Vector2 v)=>new Vector2(-v.x,-v.y);public static Vector2 Min(Vector2 a,Vector2 b)=>new Vector2(System.Math.Min(a.x,b.x),System.Math.Min(a.y,b.y));public static Vector2 Max(Vector2 a,Vector2 b)=>new Vector2(System.Math.Max(a.x,b.x),System.Math.Max(a.y,b.y));}
 public struct Vector3 {public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}}
 public class GameObject {public Transform transform=new Transform();public bool activeInHierarchy=true;public int layer;}
 public class Component {public GameObject gameObject=new GameObject();public Transform transform=>gameObject.transform;}
 public class MonoBehaviour:Component {public bool enabled=true;}
 public class Transform {
  public float x,y,angle,sx=1,sy=1;
  public Vector2 TransformVector(Vector3 v){double c=System.Math.Cos(angle),s=System.Math.Sin(angle);return new Vector2((float)(v.x*sx*c-v.y*sy*s),(float)(v.x*sx*s+v.y*sy*c));}
  public Vector2 TransformPoint(Vector2 p){var v=TransformVector(new Vector3(p.x,p.y,0));return new Vector2(v.x+x,v.y+y);}
 }
 public struct Rect {
  public float xMin,yMin,xMax,yMax;
  public static Rect MinMaxRect(float x,float y,float X,float Y)=>new Rect{xMin=x,yMin=y,xMax=X,yMax=Y};
  public bool Overlaps(Rect r)=>xMin<r.xMax&&xMax>r.xMin&&yMin<r.yMax&&yMax>r.yMin;
 }
 public static class Mathf {
  public static float Min(float a,float b)=>System.Math.Min(a,b);
  public static float Max(float a,float b)=>System.Math.Max(a,b);
  public static float Abs(float a)=>System.Math.Abs(a);
 }
}
public class WaterwayGateController:UnityEngine.MonoBehaviour{} public class LeverData:UnityEngine.MonoBehaviour{} public class BatterySocket:UnityEngine.MonoBehaviour{}
public class CameraVisionObjectStreaming {public System.Collections.Generic.HashSet<UnityEngine.GameObject> suspended=new System.Collections.Generic.HashSet<UnityEngine.GameObject>();public bool IsSuspendedByStreamingOrAncestor(UnityEngine.GameObject target)=>suspended.Contains(target);}
public static class CableTests {
 public static int count;
 static void Check(bool b,string label){count++;if(!b)throw new System.Exception(label);}
 static UnityEngine.Rect R(float x,float y,float X,float Y)=>UnityEngine.Rect.MinMaxRect(x,y,X,Y);
 static UnityEngine.Vector2 V(float x,float y)=>new UnityEngine.Vector2(x,y);
 public static System.Collections.Generic.List<UnityEngine.Vector2> Route;
 public static System.Collections.Generic.List<UnityEngine.Rect> PreviewWalls;
 public static System.Collections.Generic.List<System.Collections.Generic.List<UnityEngine.Vector2>> PreviewRoutes;
 static bool Solve(UnityEngine.Vector2 a,UnityEngine.Vector2 b,System.Collections.Generic.List<UnityEngine.Rect> walls,float margin=12,float clearance=.16f,int cap=40000,System.Collections.Generic.List<UnityEngine.Rect> bands=null,float stagger=0,int variation=0){
  Route=new System.Collections.Generic.List<UnityEngine.Vector2>();
  bool ok=OrthogonalCableRouter.TryRoute(a,b,walls,clearance,margin,cap,.35f,Route,bands,stagger,variation);
  if(!ok){Check(Route.Count==0,"Failure never returns partial or penetrating route");return false;}
  Check(Route.Count>0&&Route[0].x==a.x&&Route[0].y==a.y,"Exact source port");
  Check(Route[Route.Count-1].x==b.x&&Route[Route.Count-1].y==b.y,"Exact destination port");
  for(int i=1;i<Route.Count;i++){
   var p=Route[i-1];var q=Route[i];Check((p.x==q.x) != (p.y==q.y),"Only nonzero horizontal or vertical segments");
   foreach(var wall in walls){var r=R(wall.xMin-clearance,wall.yMin-clearance,wall.xMax+clearance,wall.yMax+clearance);
    bool crosses=p.y==q.y?p.y>r.yMin&&p.y<r.yMax&&System.Math.Max(p.x,q.x)>r.xMin&&System.Math.Min(p.x,q.x)<r.xMax:
      p.x>r.xMin&&p.x<r.xMax&&System.Math.Max(p.y,q.y)>r.yMin&&System.Math.Min(p.y,q.y)<r.yMax;
    Check(!crosses,"Cable including its clearance cannot intersect any obstacle");
   }
   if(i>1){var before=Route[i-2];Check(!((before.x==p.x&&p.x==q.x)||(before.y==p.y&&p.y==q.y)),"Collinear vertices merged; only actual right-angle corners retained");}
  }
  return true;
 }
 static void Reserve(System.Collections.Generic.List<UnityEngine.Rect> bands,System.Collections.Generic.List<UnityEngine.Vector2> path){
  for(int i=1;i<path.Count;i++){var a=path[i-1];var b=path[i];bands.Add(R(System.Math.Min(a.x,b.x)-.34f,System.Math.Min(a.y,b.y)-.34f,System.Math.Max(a.x,b.x)+.34f,System.Math.Max(a.y,b.y)+.34f));}
 }
 static float Exposure(System.Collections.Generic.List<UnityEngine.Vector2> path,System.Collections.Generic.List<UnityEngine.Rect> bands){
  float sum=0;for(int i=1;i<path.Count;i++){var a=path[i-1];var b=path[i];foreach(var r in bands){
   if(a.y==b.y&&a.y>r.yMin&&a.y<r.yMax)sum+=System.Math.Max(0,System.Math.Min(System.Math.Max(a.x,b.x),r.xMax)-System.Math.Max(System.Math.Min(a.x,b.x),r.xMin));
   if(a.x==b.x&&a.x>r.xMin&&a.x<r.xMax)sum+=System.Math.Max(0,System.Math.Min(System.Math.Max(a.y,b.y),r.yMax)-System.Math.Max(System.Math.Min(a.y,b.y),r.yMin));
  }}return sum;
 }
 public static int RunBottomPorts(){
  var walls=new System.Collections.Generic.List<UnityEngine.Rect>();
  var bodyA=R(-.375f,-.458333f,.375f,.458333f);var a=V(0,bodyA.yMin);
  foreach(var b in new[]{V(6,-.62f),V(-6,-.62f),V(0,5),V(0,-5),V(6,5),V(-6,-5)}){
   var bodyB=R(b.x-.43f,b.y,b.x+.43f,b.y+1.24f);
   var path=new System.Collections.Generic.List<UnityEngine.Vector2>();
   Check(OrthogonalCableRouter.TryRouteBottomPorts(a,b,bodyA,bodyB,.28f,walls,.16f,12,40000,.2f,path,null,.45f,21),"Bottom ports route in all relative directions");
   Check(path[0].x==a.x&&path[0].y==a.y&&path[1].x==a.x&&path[1].y<a.y,"First segment leaves socket strictly downward");
   int last=path.Count-1;
   Check(path[last].x==b.x&&path[last].y==b.y&&path[last-1].x==b.x&&path[last-1].y<b.y,"Last segment arrives at mechanism from below");
   Check(a.y-path[1].y>=.2799f&&b.y-path[last-1].y>=.2799f,"Protected lead length retained despite natural-turn styling");
   for(int i=1;i<path.Count;i++){
    var p=path[i-1];var q=path[i];Check((p.x==q.x)!=(p.y==q.y),"Protected port route never introduces diagonal or zero-length segment");
    if(i==1||i==last)continue;
    foreach(var r in new[]{bodyA,bodyB}){
     bool intersects=p.y==q.y?p.y>r.yMin&&p.y<r.yMax&&System.Math.Max(p.x,q.x)>r.xMin&&System.Math.Min(p.x,q.x)<r.xMax:
      p.x>r.xMin&&p.x<r.xMax&&System.Math.Max(p.y,q.y)>r.yMin&&System.Math.Min(p.y,q.y)<r.yMax;
     Check(!intersects,"Middle route cannot re-enter socket or mechanism silhouette");
    }
   }
  }
  var blocked=new System.Collections.Generic.List<UnityEngine.Rect>{R(-.2f,-.72f,.2f,-.66f)};
  Route=new System.Collections.Generic.List<UnityEngine.Vector2>();
  Check(!OrthogonalCableRouter.TryRouteBottomPorts(a,V(6,-.62f),bodyA,R(5.57f,-.62f,6.43f,.62f),.28f,blocked,.04f,12,40000,.2f,Route,null,.45f,1),"Wall blocking protected exit yields failure, never reconnects at side or cuts through it");
  Check(Route.Count==0,"Blocked leads clear stale geometry");
  var controllerBounds=PortGeometry.GetLocalBodyBounds(new WaterwayGateController());
  Check(controllerBounds.yMin==-.62f,"Controller anchor matches the fixed housing, not moving handle");
  Check(System.Math.Abs(PortGeometry.GetLocalBodyBounds(new LeverData()).yMin+.0855f)<.00001f,"Lever uses base pixel extent and noncentral sprite pivot");
  Check(System.Math.Abs(PortGeometry.GetLocalBodyBounds(new UnityEngine.Component()).yMin+11f/24f)<.00001f,"Socket port matches visible bottom pixels");
  foreach(float degrees in new[]{0f,90f,180f,270f,35f}){
   var owner=new UnityEngine.Transform{x=6,y=3,angle=degrees*(float)(System.Math.PI/180),sx=1.5f,sy=2f};
   UnityEngine.Rect body;var port=PortGeometry.GetBottomPort(owner,controllerBounds,out body);var dir=PortGeometry.GetPortDirection(owner);
   Check(System.Math.Abs(port.x-(6+1.24f*(float)System.Math.Sin(owner.angle)))<.0001f&&System.Math.Abs(port.y-(3-1.24f*(float)System.Math.Cos(owner.angle)))<.0001f,"Physical local-bottom anchor follows rotation and nonuniform scale exactly");
   Check((dir.x==0&&System.Math.Abs(dir.y)==1)||(dir.y==0&&System.Math.Abs(dir.x)==1),"Transformed exit always chooses cardinal direction");
   if(degrees==180)Check(port.y>3&&dir.y==1,"Upside-down controller connects at screen-top circled base");
   Route=new System.Collections.Generic.List<UnityEngine.Vector2>();
   Check(OrthogonalCableRouter.TryRoutePorts(a,port,V(0,-1),dir,bodyA,body,.28f,walls,.16f,12,40000,.2f,Route,null,.45f,2),"Rotated mechanism keeps local-bottom port reachable");
   int last=Route.Count-1;var previous=Route[last-1];
   Check(System.Math.Abs((previous.x-port.x)*dir.y-(previous.y-port.y)*dir.x)<.0001f&&((previous.x-port.x)*dir.x+(previous.y-port.y)*dir.y)>0,"Arrival lead comes from outside the rotated base");
   for(int i=1;i<Route.Count;i++)Check(Route[i-1].x==Route[i].x||Route[i-1].y==Route[i].y,"Even arbitrary-angle prop introduces no diagonal cable segments");
  }
  var mirrored=new UnityEngine.Transform{sy=-1};UnityEngine.Rect mirroredBody;var mirroredPort=PortGeometry.GetBottomPort(mirrored,controllerBounds,out mirroredBody);
  Check(mirroredPort.y==.62f&&PortGeometry.GetPortDirection(mirrored).y==1,"Negative-scale flip also moves physical bottom port to the top");
  PreviewWalls=new System.Collections.Generic.List<UnityEngine.Rect>{R(0,-1.5f,1,1.5f)};
  PreviewRoutes=new System.Collections.Generic.List<System.Collections.Generic.List<UnityEngine.Vector2>>();
  var occupied=new System.Collections.Generic.List<UnityEngine.Rect>();
  for(int i=0;i<3;i++){
   var p=new System.Collections.Generic.List<UnityEngine.Vector2>();
   Check(OrthogonalCableRouter.TryRoutePorts(V(-5,-.458333f),V(7,3.62f),V(0,-1),V(0,1),R(-5.375f,-.458333f,-4.625f,.458333f),R(6.57f,2.38f,7.43f,3.62f),.28f,PreviewWalls,.16f,12,40000,.2f,p,occupied,.45f,i),"Shared transformed bottom ports retain cable separation");
   PreviewRoutes.Add(p);Reserve(occupied,p);
  }
  return count;
 }
 public static int RunDeviceAvoidance(){
  var h=new DeviceAvoidanceHarness();var owner=new BatterySocket();owner.gameObject=h.gameObject;
  var target=new WaterwayGateController();target.transform.x=6;
  var otherSocket=new BatterySocket();otherSocket.transform.x=3;otherSocket.transform.y=-.9f;otherSocket.gameObject.layer=9;
  var otherController=new WaterwayGateController();otherController.transform.x=2;otherController.transform.y=1.1f;otherController.enabled=false;
  var otherLever=new LeverData();otherLever.transform.x=4;otherLever.transform.y=-2;otherLever.transform.angle=1.5707963f;otherLever.transform.sy=-1;
  h.devices.Add(owner);h.devices.Add(target);h.devices.Add(otherSocket);h.devices.Add(otherController);h.devices.Add(otherLever);
  h.AddDeviceObstacles(target);
  Check(h.obstacles.Count==3,"Exclude own socket and bound target; include every other socket/controller/lever irrespective of layer or behaviour.enabled");
  UnityEngine.Rect socketBody;PortGeometry.GetBottomPort(otherSocket.transform,PortGeometry.GetLocalBodyBounds(otherSocket),out socketBody);
  Check(System.Math.Abs(h.obstacles[0].xMin-(socketBody.xMin-.12f))<.00001f,"Additional device edge spacing applied outside actual visual silhouette");
  var start=V(0,-11f/24f);var end=V(6,-.62f);var startBody=R(-.375f,-11f/24f,.375f,11f/24f);var endBody=R(5.57f,-.62f,6.43f,.62f);
  var bands=new System.Collections.Generic.List<UnityEngine.Rect>{R(-8,-3,8,2)};
  var routed=new System.Collections.Generic.List<UnityEngine.Vector2>();
  foreach(float stagger in new[]{0f,.45f}){
   Check(OrthogonalCableRouter.TryRoutePorts(start,end,V(0,-1),V(0,-1),startBody,endBody,.28f,h.obstacles,.16f,12,40000,.2f,routed,bands,stagger,31),"Congested cables still route around unrelated devices with or without natural turns");
   for(int i=1;i<routed.Count;i++)foreach(var raw in h.obstacles){
    var r=R(raw.xMin-.16f,raw.yMin-.16f,raw.xMax+.16f,raw.yMax+.16f);var p=routed[i-1];var q=routed[i];
    bool crosses=p.y==q.y?p.y>r.yMin&&p.y<r.yMax&&System.Math.Max(p.x,q.x)>r.xMin&&System.Math.Min(p.x,q.x)<r.xMax:
     p.x>r.xMin&&p.x<r.xMax&&System.Math.Max(p.y,q.y)>r.yMin&&System.Math.Min(p.y,q.y)<r.yMax;
    Check(!crosses,"Every segment including fixed port leads preserves full clearance from other device");
   }
  }
  otherSocket.gameObject.activeInHierarchy=false;h.obstacles.Clear();h.AddDeviceObstacles(target);
  Check(h.obstacles.Count==2,"Intentionally inactive unrelated device does not block route");
  h.streaming[0].suspended.Add(otherSocket.gameObject);h.obstacles.Clear();h.AddDeviceObstacles(target);
  Check(h.obstacles.Count==3,"Streaming suspension never turns unrelated equipment into a pass-through");
  otherSocket.transform.x=8;h.obstacles.Clear();h.AddDeviceObstacles(target);
  Check(h.obstacles[0].xMin>7,"Moving cached device immediately updates collected geometry without scene rescan");
  otherSocket.transform.x=0;otherSocket.transform.y=-.85f;h.obstacles.Clear();h.AddDeviceObstacles(target);
  Check(!OrthogonalCableRouter.TryRoutePorts(start,end,V(0,-1),V(0,-1),startBody,endBody,.28f,h.obstacles,.16f,12,40000,.2f,routed,bands,.45f,31),"Unrelated device over endpoint lead blocks route rather than allowing a misleading attachment");
  Check(routed.Count==0,"Failure hides stale route through endpoint obstruction");
  return count;
 }
 public static int RunStyled(){
  var empty=new System.Collections.Generic.List<UnityEngine.Rect>();
  Check(Solve(V(0,0),V(8,0),empty,12,.16f,40000,null,.45f,12)&&Route.Count==4,"Aligned ports gain only two clean corners");
  var remembered=new System.Collections.Generic.List<UnityEngine.Vector2>(Route);
  Solve(V(0,0),V(8,0),empty,12,.16f,40000,null,.45f,12);
  Check(Route.Count==remembered.Count,"Regeneration keeps count stable");
  for(int i=0;i<Route.Count;i++)Check(Route[i].x==remembered[i].x&&Route[i].y==remembered[i].y,"Identical inputs have deterministic route; no random jitter");
  Check(Solve(V(0,0),V(8,6),empty,12,.16f,40000,null,.45f,12)&&Route.Count==5,"Long L becomes modest staircase with exactly two additional corners");
  Check(Solve(V(0,0),V(1,0),empty,12,.16f,40000,null,.45f,12)&&Route.Count==2,"Short connections are not forced into fussy zigzags");
  var bands=new System.Collections.Generic.List<UnityEngine.Rect>{R(-5.34f,-.34f,5.34f,.34f)};
  Check(Solve(V(-5,0),V(5,0),empty,12,.16f,40000,bands,.45f,1),"Shared endpoints never become hard blockers");
  Check(Exposure(Route,bands)<1,"Avoid over 90 percent of the former ten-unit cable overlap");
  Check(Solve(V(-5,0),V(5,0),empty,12,.16f,2,bands,.45f,1),"Soft reservation graph over budget falls back to valid wall-safe route, not a hidden cable");
  var narrow=new System.Collections.Generic.List<UnityEngine.Rect>{R(-100,-2,100,-.3f),R(-100,.3f,100,2)};
  Check(Solve(V(-5,0),V(5,0),narrow,12,.16f,40000,bands,.45f,1)&&Route.Count==2,"Narrow passage permits necessary shared run, never hides valid route or penetrates wall");
  var cross=new System.Collections.Generic.List<UnityEngine.Rect>{R(-10,-.2f,10,.2f)};
  Check(Solve(V(0,-4),V(0,4),empty,12,.16f,40000,cross,.45f,2),"Perpendicular crossing allowed without extreme detour");
  Check(Exposure(Route,cross)<.41f,"Crossing remains short instead of following other cable");
  var random=new System.Random(31);
  for(int trial=0;trial<80;trial++){
   var walls=new System.Collections.Generic.List<UnityEngine.Rect>();
   for(int i=0;i<12;i++){float x=(float)random.NextDouble()*10-5,y=(float)random.NextDouble()*8-4;walls.Add(R(x,y,x+.6f,y+1.3f));}
   Solve(V(-8,0),V(8,0),walls);int plainCount=Route.Count;
   Check(Solve(V(-8,0),V(8,0),walls,12,.16f,40000,null,.45f,trial),"Styled obstacle route exists");
   Check(Route.Count<=plainCount+2,"Natural styling always obeys two-corner limit");
   var reservation=new System.Collections.Generic.List<UnityEngine.Rect>();Reserve(reservation,Route);
   Check(Solve(V(-8,0),V(8,0),walls,12,.16f,40000,reservation,0,trial),"Second cable avoids reserved route but can share if necessary");
   float before=Exposure(Route,reservation);int beforeCount=Route.Count;
   Check(Solve(V(-8,0),V(8,0),walls,12,.16f,40000,reservation,.45f,trial),"Styled second cable still avoids all wall geometry");
   Check(Exposure(Route,reservation)<=before+.0002f&&Route.Count<=beforeCount+2,"Natural corners never worsen cable congestion");
  }
  PreviewWalls=new System.Collections.Generic.List<UnityEngine.Rect>{R(0,-.5f,1,1.5f)};
  PreviewRoutes=new System.Collections.Generic.List<System.Collections.Generic.List<UnityEngine.Vector2>>();
  var occupied=new System.Collections.Generic.List<UnityEngine.Rect>();
  for(int i=0;i<3;i++){
   Check(Solve(V(-5,0),V(7,3),PreviewWalls,12,.16f,40000,occupied,.45f,i),"Three priority-ordered cables route successfully");
   PreviewRoutes.Add(new System.Collections.Generic.List<UnityEngine.Vector2>(Route));Reserve(occupied,Route);
  }
  return count;
 }
 public static int Run(){
  var empty=new System.Collections.Generic.List<UnityEngine.Rect>();
  Check(Solve(V(0,0),V(5,0),empty)&&Route.Count==2,"Straight horizontal cable");
  Check(Solve(V(0,0),V(0,5),empty)&&Route.Count==2,"Straight vertical cable");
  Check(Solve(V(.13f,.27f),V(4.72f,3.91f),empty)&&Route.Count==3,"Off-grid ports use exactly one orthogonal corner");
  Check(Solve(V(0,0),V(.000001f,3),empty)&&Route.Count==3,"Tiny endpoint difference never gets simplified into diagonal");
  Check(Solve(V(1,1),V(1,1),empty)&&Route.Count==1,"Coincident ports handled");
  var wall=new System.Collections.Generic.List<UnityEngine.Rect>{R(-.1f,-2,.1f,2)};
  Check(Solve(V(-4,0),V(4,0),wall)&&Route.Count>=4,"Detour around direct obstruction");
  Check(Solve(V(4,0),V(-4,0),wall),"Reverse direction also routes");
  Check(!Solve(V(0,0),V(4,0),wall),"Port in wall refuses unsafe cable");
  wall=new System.Collections.Generic.List<UnityEngine.Rect>{R(-.001f,-10,.001f,10)};
  Check(!Solve(V(-4,0),V(4,0),wall,5),"No diagonal shortcut or fallback through long thin wall");
  Check(Solve(V(-4,0),V(4,0),wall,12),"Larger search margin permits long detour");
  var gap=new System.Collections.Generic.List<UnityEngine.Rect>{R(-.2f,-20,.2f,-.2f),R(-.2f,.2f,.2f,20)};
  Check(Solve(V(-4,0),V(4,0),gap,5,.16f),"Cable fits sufficiently wide gap");
  Check(!Solve(V(-4,0),V(4,0),gap,5,.25f),"Reject gap narrower than cable clearance");
  var enclosure=new System.Collections.Generic.List<UnityEngine.Rect>{R(-2,-2,-1,2),R(1,-2,2,2),R(-2,1,2,2),R(-2,-2,2,-1)};
  Check(!Solve(V(0,0),V(4,0),enclosure),"Closed room yields no route");
  Check(!Solve(V(-4,0),V(4,0),wall,12,.16f,2),"Search memory budget enforced");
  var random=new System.Random(1937);
  for(int sample=0;sample<120;sample++){
   var many=new System.Collections.Generic.List<UnityEngine.Rect>();
   for(int j=0;j<15;j++){float x=(float)random.NextDouble()*12-6,y=(float)random.NextDouble()*10-5;many.Add(R(x,y,x+.2f+(float)random.NextDouble()*1.8f,y+.2f+(float)random.NextDouble()*2));}
   Check(Solve(V(-9,0),V(9,0),many),"Random multi-obstacle route exists around finite obstacles");
  }
  PreviewWalls=new System.Collections.Generic.List<UnityEngine.Rect>{R(-2,-5,-1,1.7f),R(1,-1.7f,2,5),R(4,-5,5,1.7f)};
  Check(Solve(V(-5,0),V(7,0),PreviewWalls,3),"Alternating obstacles produce multiple right-angle turns");
  return count;
 }
}
'@
$component=Get-Content -Raw "$root/Assets/Scripts/Interaction/BatterySocketCable.cs"
$geometry='public static class PortGeometry {'
foreach($signature in @('private static Rect GetLocalBodyBounds(', 'private static Vector2 GetBottomPort(', 'private static Vector2 GetPortDirection(')){
 $start=$component.IndexOf($signature);if($start -lt 0){throw "Missing $signature"}
 $begin=$component.IndexOf('{',$start);$end=$begin+1;$depth=1
 while($depth){if($component[$end] -eq '{'){$depth++};if($component[$end] -eq '}'){$depth--};$end++}
 $geometry+=$component.Substring($start,$end-$start).Replace('private static','public static')
}
$geometry+='}'
$geometry+='public class DeviceAvoidanceHarness {public UnityEngine.GameObject gameObject=new UnityEngine.GameObject();public System.Collections.Generic.List<UnityEngine.Component> devices=new System.Collections.Generic.List<UnityEngine.Component>();public System.Collections.Generic.List<UnityEngine.Rect> obstacles=new System.Collections.Generic.List<UnityEngine.Rect>();public float deviceClearance=.12f;public CameraVisionObjectStreaming[] streaming={new CameraVisionObjectStreaming()};private static UnityEngine.Rect GetLocalBodyBounds(UnityEngine.Component d)=>PortGeometry.GetLocalBodyBounds(d);private static UnityEngine.Vector2 GetBottomPort(UnityEngine.Transform t,UnityEngine.Rect r,out UnityEngine.Rect body)=>PortGeometry.GetBottomPort(t,r,out body);'
foreach($signature in @('private bool IsPresentForRouting(', 'private void AddDeviceObstacles(')){
 $start=$component.IndexOf($signature);if($start -lt 0){throw "Missing $signature"}
 $begin=$component.IndexOf('{',$start);$end=$begin+1;$depth=1
 while($depth){if($component[$end] -eq '{'){$depth++};if($component[$end] -eq '}'){$depth--};$end++}
 $geometry+=$component.Substring($start,$end-$start).Replace('private bool','public bool').Replace('private void','public void')
}
$geometry+='}'
Add-Type -TypeDefinition ($router+"`n"+$stubs+"`n"+$geometry)
$checks=[CableTests]::Run()
$checks=[CableTests]::RunStyled()
$checks=[CableTests]::RunBottomPorts()
$checks=[CableTests]::RunDeviceAvoidance()
$component=Get-Content -Raw "$root/Assets/Scripts/Interaction/BatterySocketCable.cs"
foreach($required in @('LayerMask.NameToLayer("Blocks")','IsSuspendedByStreamingOrAncestor','GetBottomPort(transform, GetLocalBodyBounds(socket)','GetBottomPort(binding.transform, GetLocalBodyBounds(binding)','transform.InverseTransformPoint','previousObstacles','generated.SetActive(false)')){
 if(!$component.Contains($required)){throw "Missing integration guard: $required"};$checks++
}
foreach($required in @('string.CompareOrdinal(other.GetOrderKey(), orderKey) >= 0','previousReservations','StableVariation(orderKey)','ActiveCables.Remove(this)','other.gameObject.scene != gameObject.scene')){
 if(!$component.Contains($required)){throw "Missing cable coordination guard: $required"};$checks++
}
foreach($required in @('GetComponentsInChildren<BatterySocket>(true)','GetComponentsInChildren<WaterwayGateController>(true)','GetComponentsInChildren<LeverData>(true)','AddDeviceObstacles(binding)')){
 if(!$component.Contains($required)){throw "Missing independent device discovery: $required"};$checks++
}
if($component.Contains('typeof(BoxCollider2D)') -or $component.Contains('LineRenderer')){throw 'Cable must not add collision or smoothed line geometry'}
$prefab=Get-Content -Raw "$root/Assets/Prefabs/Decorations/BatterySocket.prefab"
$guid=[regex]::Match((Get-Content -Raw "$root/Assets/Scripts/Interaction/BatterySocketCable.cs.meta"),'guid: ([a-f0-9]{32})').Groups[1].Value
if(!$prefab.Contains($guid)){throw 'Cable component missing from socket prefab'}
foreach($required in @('generated.layer = 0;', 'cableRenderer.sortingLayerID = 0;', 'cableRenderer.sortingOrder = Mathf.Min(0, sortingOrder);')){
 if(!$component.Contains($required)){throw "Missing masked ground render policy: $required"};$checks++
}
$mover=Get-Content -Raw "$root/Assets/Scripts/Characters/ZeldaFourWayMover.cs"
if(!$mover.Contains('spriteRenderer.sortingOrder = 1;')){throw 'Recheck cable order against changed character/ghost order'};$checks++
$vision=Get-Content -Raw "$root/Assets/Scripts/Camera/CameraCircularVision.cs"
if(!$vision.Contains('overlayRenderer.sortingOrder = 30000;') -or !$vision.Contains('return (blockLayers.value & ~hiddenBlocksLayerMask) | visibleNonBlockingLayerMask;')){throw 'Recheck world rendering against changed vision compositing'};$checks++

# Algorithm preview, not a Unity screenshot: render the exact tested route and inflated clearance.
Add-Type -AssemblyName System.Drawing
$bitmap=[System.Drawing.Bitmap]::new(900,460)
$g=[System.Drawing.Graphics]::FromImage($bitmap)
$g.Clear([System.Drawing.Color]::FromArgb(23,31,43))
$font=[System.Drawing.Font]::new('Consolas',13)
$brush=[System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(185,196,203))
$g.DrawString('LOCAL BOTTOM PORTS  /  FOLLOW ROTATION  /  CABLE SPACING',$font,$brush,22,18)
$scale=40;$offsetX=330;$offsetY=238
$wallBrush=[System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(91,104,116))
foreach($r in [CableTests]::PreviewWalls){$g.FillRectangle($wallBrush,($offsetX+$r.xMin*$scale),($offsetY-$r.yMax*$scale),(($r.xMax-$r.xMin)*$scale),(($r.yMax-$r.yMin)*$scale))}
foreach($points in [CableTests]::PreviewRoutes){
foreach($band in @(@(8,[System.Drawing.Color]::FromArgb(43,54,59)),@(6,[System.Drawing.Color]::FromArgb(153,166,168)),@(3,[System.Drawing.Color]::FromArgb(214,23,14)))){
 $pen=[System.Drawing.Pen]::new($band[1],$band[0]);$pen.StartCap=$pen.EndCap=[System.Drawing.Drawing2D.LineCap]::Square
 for($i=1;$i -lt $points.Count;$i++){$a=$points[$i-1];$b=$points[$i];$g.DrawLine($pen,($offsetX+$a.x*$scale),($offsetY-$a.y*$scale),($offsetX+$b.x*$scale),($offsetY-$b.y*$scale))}
 $pen.Dispose()
}
}
$red=[System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(204,26,18))
for($i=0;$i -lt 2;$i++){$p=if($i -eq 0){$points[0]}else{$points[$points.Count-1]};$top=$offsetY-$p.y*$scale;if($i -eq 0){$top-=44};$g.FillRectangle($brush,($offsetX+$p.x*$scale-15),$top,30,44);$g.FillRectangle($red,($offsetX+$p.x*$scale-7),($top+8),14,28)}
$bitmap.Save("$root/Docs/BatterySocketCable-preview.png",[System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose();$bitmap.Dispose();$font.Dispose();$brush.Dispose();$wallBrush.Dispose();$red.Dispose()
Write-Output "PASS: $checks routing and integration checks; algorithm preview generated (not Unity Play Mode)."
