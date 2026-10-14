$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
function Extract($source,$signature){
 $start=$source.IndexOf($signature);if($start -lt 0){throw "Missing $signature"}
 $begin=$source.IndexOf('{',$start);$end=$begin+1;$depth=1
 while($depth){if($source[$end] -eq '{'){$depth++};if($source[$end] -eq '}'){$depth--};$end++}
 $source.Substring($start,$end-$start)
}
$rules=Get-Content -Raw "$root/Assets/Scripts/Interaction/ElectricGateRules.cs"
$network=Get-Content -Raw "$root/Assets/Scripts/Interaction/ElectricGateNetwork.cs"
$stubs=@'
namespace UnityEngine {
 public struct Vector2 {
  public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}
  public static Vector2 zero=>new Vector2();public float sqrMagnitude=>x*x+y*y;public float magnitude=>(float)System.Math.Sqrt(sqrMagnitude);
  public Vector2 normalized=>magnitude<.000001f?zero:this*(1/magnitude);
  public static Vector2 operator +(Vector2 a,Vector2 b)=>new Vector2(a.x+b.x,a.y+b.y);
  public static Vector2 operator -(Vector2 a,Vector2 b)=>new Vector2(a.x-b.x,a.y-b.y);
  public static Vector2 operator *(Vector2 a,float n)=>new Vector2(a.x*n,a.y*n);
  public static float Dot(Vector2 a,Vector2 b)=>a.x*b.x+a.y*b.y;
  public static float Distance(Vector2 a,Vector2 b)=>(a-b).magnitude;
 }
 public static class Mathf {
  public static float Abs(float x)=>System.Math.Abs(x);
  public static float Max(float a,float b)=>System.Math.Max(a,b);
  public static float Min(float a,float b)=>System.Math.Min(a,b);
  public static float Clamp(float v,float a,float b)=>Max(a,Min(v,b));
 }
 public static class Time {public static float time;}
}
public class ZeldaCharacterData {
 public bool IsDead;public int health=40;public System.Collections.Generic.List<int> damage=new System.Collections.Generic.List<int>();
 public System.Action onDeath;
 public void TakeDamage(int value,object owner){damage.Add(value);health-=value;if(health<=0){IsDead=true;onDeath?.Invoke();}}
}
public class ElectricGateAudio { public void PlayStrike(UnityEngine.Vector2 p) {} }
public class StrikeHarness {
 public ElectricGateAudio audio;
 public UnityEngine.Vector2 Start=new UnityEngine.Vector2(0,0),End=new UnityEngine.Vector2(8,0),strikePoint;
 public float strikeUntil;
 public static ZeldaCharacterData struckCharacter;
 public static UnityEngine.Vector2 reboundPosition;
'@
$strike=(Extract $network 'private void DamagePair(').Replace('private void DamagePair','public void DamagePair')
$tests=@'
}
public static class ElectricGateTests {
 static int count;
 static void Check(bool value,string label){count++;if(!value)throw new System.Exception(label);}
 static UnityEngine.Vector2 V(float x,float y)=>new UnityEngine.Vector2(x,y);
 public static int Run(){
  Check(ElectricGateRules.Range==10&&ElectricGateRules.Width==.5f&&ElectricGateRules.FirstPairDamage==2&&ElectricGateRules.SecondPairDamage==6&&ElectricGateRules.ArcCount==4,"Exact design constants");
  for(int t=-2;t<=9;t++)for(int p=-2;p<=9;p++)Check(ElectricGateRules.Allows(false,t,"other",p,"self")== (p>=t),"Every permission threshold, including equality");
  Check(!ElectricGateRules.Allows(true,-2,"",9,"self"),"Unenrolled individual mode denies everyone");
  Check(!ElectricGateRules.Allows(true,-2,"first",9,"second"),"High permission does not bypass individual enrollment");
  Check(ElectricGateRules.Allows(true,9,"first",-2,"first"),"Individual identity survives control switches regardless of permission");
  Check(!ElectricGateRules.NearerCandidate(100.01f,200,1,2),"Out of range rejected");
  Check(ElectricGateRules.NearerCandidate(100,101,1,2),"Range boundary included");
  Check(!ElectricGateRules.NearerCandidate(0,101,1,2),"Coincident pillars rejected");
  Check(ElectricGateRules.NearerCandidate(9,16,2,1),"Nearest candidate replaces farther one");
  Check(!ElectricGateRules.NearerCandidate(16,9,1,2),"Farther candidate cannot replace nearest");
  Check(ElectricGateRules.NearerCandidate(9,9,1,2)&&!ElectricGateRules.NearerCandidate(9,9,2,1),"Deterministic equal-distance ties");
  for(int degrees=0;degrees<360;degrees+=5){
   float angle=degrees*(float)System.Math.PI/180;
   var axis=V((float)System.Math.Cos(angle),(float)System.Math.Sin(angle));var n=V(-axis.y,axis.x);
   var a=V(2,-3);var b=a+axis*8;var mid=a+axis*4;float entry;
   Check(ElectricGateRules.Sweep(mid, V(0,0),a,b,0,out entry),"Strip center inside under rotation");
   Check(!ElectricGateRules.Sweep(mid+n*.26f,V(0,0),a,b,0,out entry),"Uninflated strip really is 0.5 wide");
   Check(ElectricGateRules.Sweep(mid-n*3,n*6,a,b,.2f,out entry)&&entry>0&&entry<1,"Fast crossing detected both ends outside");
   Check(!ElectricGateRules.Sweep(a-axis*2-n*3,n*6,a,b,.2f,out entry),"Path beyond endpoints does not trigger field");
   var start=mid-n*3;var delta=ElectricGateRules.BlockGhost(start,n*6,a,b,.2f);
   Check(UnityEngine.Vector2.Dot(start+delta-mid,n)<-.449f,"Swept ghost cannot tunnel across");
   var reverse=ElectricGateRules.BlockGhost(mid+n*3,n*-6,a,b,.2f);
   Check(UnityEngine.Vector2.Dot(mid+n*3+reverse-mid,n)>.449f,"Reverse ghost approach blocked");
   var escape=ElectricGateRules.BlockGhost(mid+n*.1f,n*.6f,a,b,.2f);
   Check(escape.magnitude>.599f,"Ghost spawned inside can escape toward same side");
   Check(ElectricGateRules.BlockGhost(mid+n*.1f,n*-.6f,a,b,.2f).magnitude<.001f,"Overlapping ghost cannot escape through opposite side");
   Check(ElectricGateRules.BlockGhost(mid,n*.6f,a,b,.2f).magnitude>.599f,"Ghost created exactly on centerline is not trapped");
   Check(ElectricGateRules.BlockGhost(mid+n*1,axis*.5f,a,b,.2f).magnitude>.499f,"Parallel walk outside strip remains free");
  }
  Check(ElectricGateRules.IntersectsBox(V(-2,0),V(4,0),V(-.5f,-.5f),V(.5f,.5f)),"Streamed wall intersection remains obstructing");
  Check(!ElectricGateRules.IntersectsBox(V(-2,1),V(4,0),V(-.5f,-.5f),V(.5f,.5f)),"Nearby wall not intersected does not obstruct");
  var strike=new StrikeHarness();var healthy=new ZeldaCharacterData();
  strike.DamagePair(healthy,V(4,-1),V(4,0),ElectricGateRules.FirstPairDamage);
  Check(healthy.damage.Count==2&&healthy.damage[0]==2&&healthy.damage[1]==2&&healthy.health==36,"First pair executes two separate two-damage attacks");
  strike.DamagePair(healthy,V(4,-1),V(4,0),ElectricGateRules.SecondPairDamage);
  Check(healthy.damage.Count==4&&healthy.damage[2]==6&&healthy.damage[3]==6&&healthy.health==24,"Second pair executes two separate six-damage attacks");
  Check(StrikeHarness.struckCharacter==null,"Rebound context always cleared after strike");
  Check(ElectricGateRules.PairDelay>ElectricGateRules.ArcLifetime,"Groups have distinct non-overlapping visual windows");
  foreach(int hp in new[]{1,3,6,12}){
   var dying=new ZeldaCharacterData{health=hp};bool reboundContext=false;
   dying.onDeath=()=>{reboundContext=StrikeHarness.struckCharacter==dying&&StrikeHarness.reboundPosition.y<-.9f;};
   strike.DamagePair(dying,V(4,-1),V(4,.1f),ElectricGateRules.FirstPairDamage);
   strike.DamagePair(dying,V(4,-1),V(4,.1f),ElectricGateRules.SecondPairDamage);
   Check(reboundContext,"Death on any arc carries perpendicular entry-side rebound context");
   Check(dying.damage.Count==(hp<=4?(hp+1)/2:2+(hp-4+5)/6),"No remaining damage calls on an already destroyed/dead target");
  }
  var above=new ZeldaCharacterData{health=1};bool positive=false;
  above.onDeath=()=>positive=StrikeHarness.reboundPosition.y>.9f;
  strike.DamagePair(above,V(4,1),V(4,-.1f),ElectricGateRules.FirstPairDamage);Check(positive,"Reverse entrant bounces to reverse entry side");
  return count;
 }
}
'@
Add-Type -TypeDefinition ($rules+"`n"+$stubs+$strike+$tests)
$checks=[ElectricGateTests]::Run()
function Check($ok,$label){if(!$ok){throw $label};$script:checks++}
$save=Get-Content -Raw "$root/Assets/Scripts/SceneManagement/SceneTravelStateManager.cs"
$data=Get-Content -Raw "$root/Assets/Scripts/Characters/ZeldaCharacterData.cs"
$mover=Get-Content -Raw "$root/Assets/Scripts/Characters/ZeldaFourWayMover.cs"
$panel=Get-Content -Raw "$root/Assets/Scripts/Interaction/ElectricGatePanel.cs"
$prefab=Get-Content -Raw "$root/Assets/Prefabs/Decorations/ElectricGatePillar.prefab"
Check ($save.Contains('electricGate.CaptureRuntimeState()') -and $save.Contains('electricGate.ApplyRuntimeState(state.electricGateState)')) 'Panel mode, threshold and enrollment persisted in scene saves'
Check ($data.Contains('individualAccessId = IndividualAccessId,') -and $data.Contains('individualAccessId = state.individualAccessId')) 'Body identity persisted in regular and traveling character snapshots'
Check ($mover.Contains('ElectricGateNetwork.ReboundDeathGhost(deadCharacterData, ghostMover)') -and $mover.Contains('ElectricGateNetwork.ConstrainGhost(characterData')) 'Production ghost spawn and movement hooks installed'
Check (([regex]::Matches($network,'BatterySocket.TryConsumeForMechanisms')).Count -eq 1 -and $network.Contains('touching.Contains(data)') -and $network.Contains('touching.Remove(data)')) 'Entry latch wraps one battery transaction around all four damage arcs'
Check ($network.Contains('arcs = new LineRenderer[ElectricGateRules.ArcCount]') -and $network.Contains('i % 2 == 0 ? Start : End')) 'Four visual arcs alternate between the two pillars'
Check ($network.Contains('characters.AddRange(ZeldaRuntimeRegistry.Movers)')) 'Registry copied before death creates a ghost during iteration'
Check ($network.Contains('!active || data.IsGhostLike') -and $network.Contains('if (Allows(data))') -and $network.Contains('allowedNearby |= nearby;')) 'Ghosts excluded; allowed characters receive passage signal instead of damage or warning'
Check ($network.Contains('IsSuspendedByStreamingOrAncestor') -and $network.Contains('LinecastNonAlloc')) 'Line of sight handles streaming and avoids routine raycast allocations'
Check ($panel.Contains('Close(false)') -and $panel.Contains('Close(true)') -and $panel.Contains('owner.ApplyRuntimeState(draft)')) 'UI supports commit and cancel without mutating live settings while editing'
Check ($panel.Contains('draft.individualId = actor.IndividualAccessId') -and $panel.Contains('CRTScreenEffect.RegisterCanvas(canvas, enableGlow: true)')) 'Enrollment uses body identity and UI uses existing CRT compositor with popup glow'
Check ($prefab.Contains('hasPanel: 1') -and $prefab.Contains('m_Size: {x: 1, y: 1}') -and $prefab.Contains('m_Layer: 0')) 'Prefab has optional panel, solid pillar and fog-masked world layer'
foreach($name in @('ElectricGatePillar','ElectricGatePillarVisual')){
 $guid=[regex]::Match((Get-Content -Raw "$root/Assets/Scripts/Interaction/$name.cs.meta"),'guid: ([a-f0-9]{32})').Groups[1].Value
 Check ($guid.Length -eq 32 -and $prefab.Contains($guid)) 'Prefab script reference resolves to matching meta'
}
$glowPath="$root/Assets/Resources/ElectricGateGlow.shader"
$glow=Get-Content -Raw $glowPath
Check ($network.Contains('Resources.Load<Shader>("ElectricGateGlow")') -and (Test-Path "$glowPath.meta")) 'Glow shader is a referenced Resources asset, not a stripped Shader.Find-only dependency'
Check ($glow.Contains('Blend SrcAlpha One') -and $glow.Contains('ZWrite Off') -and $glow.Contains('ZTest LEqual') -and $glow.Contains('"Queue"="Transparent"')) 'Local additive glow uses normal transparent world rendering'
Check (!$glow.Contains('ZTest Always') -and !$glow.Contains('"Queue"="Overlay"') -and !$glow.Contains('GrabPass')) 'Glow does not bypass masking or introduce a full-screen copy'
Check ($glow.Contains('lerp(abs(p.y), length(p), saturate(_Radial))') -and $glow.Contains('halo = halo * halo * halo') -and $glow.Contains('input.color.a')) 'Lines use transverse falloff, sparks radial falloff, and both respect fading alpha'
Check ($network.Contains('particleMaterial.SetFloat("_Radial", 1f)') -and $network.Contains('renderer.sharedMaterial = particleMaterial')) 'Particle system uses shared radial glow material'
Check ($network.Contains('Destroy(particleMaterial)') -and $network.Contains('Destroy(material)')) 'Both network-owned materials released on teardown'
Check ($network.Contains('new GradientAlphaKey(0, 1)') -and $network.Contains('Mathf.Clamp01((until - Time.time) / ElectricGateRules.ArcLifetime)')) 'Each pair has its own short discharge fade'
Check ($network.Contains('nextFlicker = 0f;') -and $network.Contains('softGlow ? .18f : .028f') -and $network.Contains('softGlow ? .6f : .09f')) 'Discharge updates immediately; shader fallback retains original non-glowing widths'
Write-Output "PASS: $checks electric-gate production geometry, permission, damage and integration checks (Unity stubs; not Play Mode or GPU shader rendering)."
