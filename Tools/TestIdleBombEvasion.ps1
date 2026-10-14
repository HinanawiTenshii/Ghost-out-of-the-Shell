$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$ai=Get-Content -Raw "$root/Assets/Scripts/AI/ZeldaCharacterAiBase.cs"
function Extract($source,$signature){
 $start=$source.IndexOf($signature);if($start -lt 0){throw "Missing $signature"}
 $brace=$source.IndexOf('{',$start);$end=$brace+1;$depth=1
 while($depth -gt 0){if($source[$end] -eq '{'){$depth++};if($source[$end] -eq '}'){$depth--};$end++}
 $source.Substring($start,$end-$start)
}
$code=@'
using System;
using System.Collections.Generic;
public struct Vector2 {
 public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}
 public static Vector2 zero=>new Vector2();public float sqrMagnitude=>x*x+y*y;
 public static Vector2 operator +(Vector2 a,Vector2 b)=>new Vector2(a.x+b.x,a.y+b.y);
 public static Vector2 operator -(Vector2 a,Vector2 b)=>new Vector2(a.x-b.x,a.y-b.y);
}
public struct Bounds {
 public Vector2 center,extents;
 public Bounds(Vector2 c,Vector2 size){center=c;extents=new Vector2(size.x/2,size.y/2);}
 public Vector2 min=>center-extents;public Vector2 max=>center+extents;
 public void Encapsulate(Bounds b){var lo=new Vector2(Math.Min(min.x,b.min.x),Math.Min(min.y,b.min.y));var hi=new Vector2(Math.Max(max.x,b.max.x),Math.Max(max.y,b.max.y));center=new Vector2((lo.x+hi.x)/2,(lo.y+hi.y)/2);extents=new Vector2((hi.x-lo.x)/2,(hi.y-lo.y)/2);}
}
public static class Mathf {public static float Abs(float a)=>Math.Abs(a);public static float Clamp(float x,float a,float b)=>Math.Min(b,Math.Max(a,x));}
public static class Time {public static float time;}
public class GameObject {public int scene=1;}
public class Transform {public Vector2 position;}
public class Body {public Bounds bounds;}
public class RB {public Vector2 position;}
public static class ZeldaRuntimeRegistry {public static int GetGameplayScene(GameObject o)=>o.scene;}
public enum ZeldaAiState {Idle,Hostile,Search,Alert,Suspicious,Recovery,Stunned}
public class PlacedBomb {
 public static List<PlacedBomb> ActiveBombs=new List<PlacedBomb>();
 public bool IsArmed=true,IsDecoy;public GameObject gameObject=new GameObject();public Transform transform=new Transform();
 public Bounds ExplosionBounds=new Bounds(new Vector2(),new Vector2(2,2));
}
public class Probe {
 private readonly List<PlacedBomb> noticedIdleBombs=new List<PlacedBomb>();
 private Vector2 bombEscapeDestination;private float nextBombEscapePlanTime;private bool hasBombEscapeDestination;
 public ZeldaAiState currentState=ZeldaAiState.Idle;public bool RemainsStationary,visible=true,blockLeft;
 public GameObject gameObject=new GameObject();public RB rb=new RB();public Body bodyCollider=new Body();
 public Vector2 moveDirection,planned;public int plans,invalidations,sightChecks;public bool hasNavigationDebugTarget;
 public Probe(){At(0,0);}
 public void At(float x,float y){rb.position=new Vector2(x,y);bodyCollider.bounds=new Bounds(rb.position,new Vector2(.4f,.4f));}
 bool CanSeeWorldPoint(Vector2 p,Transform t){sightChecks++;return visible;}
 bool IsBodyPathSegmentClear(Vector2 from,Vector2 to,float padding)=>!blockLeft||to.x>=from.x;
 Vector2 PlanDirectionTo(Vector2 destination,float stop){planned=destination;plans++;return destination-rb.position;}
 void InvalidateNavigationPlan(){invalidations++;}
 public bool Tick()=>TryEvadeVisibleBomb();
 public static bool Overlap(Bounds a,Bounds b,float padding=0)=>BombThreatOverlaps(a,b,padding);
 public static Vector2 Candidate(Bounds a,Bounds b,int i)=>GetBombEscapeCandidate(a,b,i);
 METHODS
}
public static class Tests {
 static int count;static void Check(bool b,string s){count++;if(!b)throw new Exception(s);}
 static PlacedBomb Setup(){PlacedBomb.ActiveBombs.Clear();Time.time=0;var b=new PlacedBomb();PlacedBomb.ActiveBombs.Add(b);return b;}
 public static int Run(){
  var bomb=Setup();var p=new Probe();Check(p.Tick()&&p.plans==1,"Idle detects visible armed bomb in blast");
  Check(!Probe.Overlap(bomb.ExplosionBounds,new Bounds(p.planned,new Vector2(.4f,.4f)),.12f),"Target clears whole body plus margin");
  p.visible=false;p.Tick();Check(p.plans==2,"Turning away retains detected threat");
  p.At(p.planned.x,p.planned.y);Check(p.Tick()&&p.moveDirection.sqrMagnitude==0,"Safe escape waits without reentering patrol route");
  bomb.IsArmed=false;Check(!p.Tick()&&p.invalidations==1,"Explosion ends evasion and clears stale navigation");
  bomb=Setup();p=new Probe();p.At(4,0);Check(!p.Tick()&&p.plans==0&&p.sightChecks==0,"Outside damage range has no response, even within attraction range");
  p.At(1.15f,0);Check(p.Tick(),"Body partially inside blast must flee");
  bomb=Setup();p=new Probe{visible=false};Check(!p.Tick(),"Behind wall or outside cone does not acquire bomb");
  p.visible=true;bomb.IsArmed=false;Check(!p.Tick(),"Unarmed/disabled/exploded object ignored");
  bomb.IsArmed=true;bomb.IsDecoy=true;Check(!p.Tick(),"Decoy is approached, never handled as timed bomb evasion");
  bomb.IsDecoy=false;bomb.gameObject.scene=2;Check(!p.Tick(),"Other loaded scene ignored");
  bomb=Setup();foreach(ZeldaAiState s in Enum.GetValues(typeof(ZeldaAiState))){if(s==ZeldaAiState.Idle)continue;p=new Probe{currentState=s};Check(!p.Tick(),"Non-idle state unchanged: "+s);}
  p=new Probe{RemainsStationary=true};Check(!p.Tick(),"Dormant stationary automaton unchanged");
  p=new Probe{blockLeft=true};Check(p.Tick()&&p.planned.x>=0,"Blocked nearest exit selects another clear exit");
  bomb=Setup();p=new Probe();p.Tick();p.currentState=ZeldaAiState.Hostile;Check(!p.Tick()&&p.invalidations==1,"Hostility takes priority and clears evasion memory");
  bomb=Setup();p=new Probe();p.Tick();PlacedBomb.ActiveBombs.Remove(bomb);bomb.IsArmed=false;Check(!p.Tick(),"Destroyed/despawned bomb ends memory");
  PlacedBomb.ActiveBombs.Clear();p=new Probe();Check(!p.Tick(),"Box-contained fuse alone is not a visible PlacedBomb");
  var blast=new Bounds(new Vector2(3,-2),new Vector2(6,4));
  for(int i=0;i<8;i++)foreach(float width in new[]{.4f,1f,2f}){
   var body=new Bounds(new Vector2(3,-2),new Vector2(width,width));var dest=Probe.Candidate(blast,body,i);
   Check(!Probe.Overlap(blast,new Bounds(dest,new Vector2(width,width)),.12f),"All exit candidates clear scaled bodies");
  }
  bomb=Setup();var second=new PlacedBomb{ExplosionBounds=new Bounds(new Vector2(1,0),new Vector2(3,3))};PlacedBomb.ActiveBombs.Add(second);
  p=new Probe();Check(p.Tick(),"Overlapping bombs detected");
  var endBody=new Bounds(p.planned,new Vector2(.4f,.4f));Check(!Probe.Overlap(bomb.ExplosionBounds,endBody)&&!Probe.Overlap(second.ExplosionBounds,endBody),"Exit outside both noticed blasts");
  return count;
 }
}
'@
$methods=''
foreach($signature in @('    private bool TryEvadeVisibleBomb()', '    private void ClearIdleBombEvasion()', '    private static bool BombThreatOverlaps(', '    private static Vector2 GetBombEscapeCandidate(')){$methods+=Extract $ai $signature}
Add-Type -TypeDefinition $code.Replace(' METHODS',$methods)
$count=[Tests]::Run()
$tick=Extract $ai '    private void TickCurrentState('
if($tick.IndexOf('if (TryEvadeVisibleBomb()) return;') -gt $tick.IndexOf('switch (currentState)') -or !$tick.Contains('if (TryEvadeVisibleBomb()) return;')){throw 'Evasion must precede virtual patrol tick'}
if($methods.Contains('CardboardBoxPickupItem')){throw 'Concealed bombs must not be perceived'}
$bombSource=Get-Content -Raw "$root/Assets/Scripts/Items/PlacedBomb.cs"
foreach($guard in @('private void OnEnable() => activeBombs.Add(this);','private void OnDisable() => activeBombs.Remove(this);','isActiveAndEnabled && configured && !exploded','Mathf.Abs(explosionSize.x)')){if(!$bombSource.Contains($guard)){throw "Registry/blast guard missing: $guard"}}
Write-Output "PASS: $count production evasion/geometry checks and registry/idle integration guards (Unity navigation and perception substituted, not Play Mode)."
