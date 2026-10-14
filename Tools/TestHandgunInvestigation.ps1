$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$data=Get-Content -Raw "$root/Assets/Scripts/Characters/ZeldaCharacterData.cs"
$ai=Get-Content -Raw "$root/Assets/Scripts/AI/ZeldaCharacterAiBase.cs"
function Extract($s,$sig){
 $start=$s.IndexOf($sig);if($start -lt 0){throw "Missing $sig"};$end=$s.IndexOf('{',$start)+1;$depth=1
 while($depth){if($s[$end] -eq '{'){$depth++};if($s[$end] -eq '}'){$depth--};$end++};$s.Substring($start,$end-$start)
}
$update=Extract $ai '    protected virtual void UpdateTargetAndState('
$searchBranch=Extract $update '        if (currentState == ZeldaAiState.Search)'
$code=@'
using System;
using System.Collections.Generic;
public struct Vector2 {public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}public static Vector2 zero=>new Vector2();public static Vector2 operator -(Vector2 a,Vector2 b)=>new Vector2(a.x-b.x,a.y-b.y);}
public static class Mathf {public static int Max(int a,int b)=>Math.Max(a,b);}
public enum SendMessageOptions {DontRequireReceiver}
public static class Debug {public static void Log(string m,object o){}}
public class GameObject {public List<object> components=new List<object>();public T GetComponent<T>() where T:class {foreach(var c in components)if(c is T)return (T)c;return null;}}
public class Component {public GameObject gameObject=new GameObject();public Body transform=new Body();public bool isActiveAndEnabled=true;public T GetComponent<T>() where T:class=>gameObject.GetComponent<T>();}
public class Body {public Vector2 position;}
public class CardboardBoxWearState {public bool blocks;public bool TryBlockAttack(ZeldaCharacterData a)=>blocks;}
public class ZeldaFourWayMover:Component {public CardboardBoxWearState ActiveCardboardBox;public void InterruptPossessionByDamage(){}}
public enum ZeldaAiState {Idle,Search,Recovery,Alert,Suspicious,Hostile,Stunned}
public class ZeldaCharacterData:Component {
 public int currentHealth=10,health=10;private bool isDead;public bool IsDead=>isDead;
 public Action ValuesChanged;private float damageFeedbackTimer,damageFeedbackDuration=.18f;public string name="victim";
 public int retalCalls,deaths,hurtSounds;private void CaptureDamageFeedbackBaseVisual(){}private void PlayDamageSound(){hurtSounds++;}
 private void BroadcastMessage(string n,ZeldaCharacterData a,SendMessageOptions o){retalCalls++;GetComponent<ZeldaCharacterAiBase>().ExternalMelee();}
 private void Die(){isDead=true;deaths++;}
 public void Hit(int d,ZeldaCharacterData a){ApplyDamage(d,a,false);}
'@
foreach($sig in @('    public void TakeHandgunDamage(', '    private void ApplyDamage(', '    internal void TakeDominoDamage(')){$code+=Extract $data $sig}
$code+=@'
}
public static class DominoSkillRuntime {
 public static ZeldaCharacterData first,second;public static ZeldaCharacterData lastAttacker;
 public static void ShareDamage(ZeldaCharacterData o,int d,ZeldaCharacterData a){lastAttacker=a;var t=o==first?second:o==second?first:null;if(t!=null&&!t.IsDead)t.TakeDominoDamage(d,a);}
}
public class ZeldaCharacterAiBase:Component {
 public ZeldaCharacterData characterData;public ZeldaFourWayMover mover,targetMover;
 public bool possessionLocked,IsIgnoringPlayer;public bool IsStunned=>currentState==ZeldaAiState.Stunned;
 public ZeldaAiState currentState;private bool anonymousHandgunInvestigation,pendingHandgunInvestigation;private Vector2 pendingHandgunPosition;
 public bool isTravelingToInvestigation,isDoorIncidentInvestigation,isPausingDuringInvestigation;
 public float investigationPauseTimer,investigationDuration=3,searchDuration=5,searchTimer,searchWaypointTimer,previousSearchWaypointDistance;
 public Vector2 investigationIncidentPosition,investigationApproachPosition,searchCenter,searchWaypoint;
 public object activePermissionArea,recognizedPermissionTarget,suspiciousCardboardBox,suspiciousCardboardBoxWearer,suspiciousGroundCardboardBox,inspectedCardboardBoxWearer,hostileCardboardBox;
 public bool hostileUntilTargetDeath,retaliationHostile,regularHostile,hasCardboardBoxAttractionTarget,isInspectingCardboardBox,retainLoadedUntilIdle,hasConsumedUnawareFirstHit;
 public float regularHostileLostSightTimer,warningTimer,suspicionValue,cornerStuckTimer,cornerEscapeTimer,cornerOscillationElapsed,cornerOscillationTravelDistance,hostileIndicatorTimer,hostileIndicatorDuration;
 public Vector2 previousFixedPosition,cornerEscapeDirection,cornerOscillationStartPosition,moveDirection;public Body rb=new Body();public int multiplier=1;
 public static int witnessCalls;public int normalPerceptionCalls;
 public ZeldaFourWayMover visiblePlayer;public Vector2 lastKnownTargetPosition;
 private ZeldaFourWayMover FindVisiblePlayer()=>visiblePlayer;
 private bool UpdateCardboardBoxSuspicion(float dt)=>false;
 public static void NotifyPlayerAttackWitnesses(ZeldaCharacterData a,ZeldaCharacterData v){witnessCalls++;}
 public int ResolveIncomingAttackDamage(int d)=>d*multiplier;
 private Vector2 ResolveInvestigationApproachPosition(Vector2 p)=>p;
 private void SetFacingDirection(Vector2 p){}private void ResetHostileAttackCycle(){}private void InvalidateNavigationPlan(){}
 private void OnStateExited(ZeldaAiState a,ZeldaAiState b){}private void OnStateEntered(ZeldaAiState a,ZeldaAiState b){}
 private bool ShouldPlayAwarenessCue(ZeldaAiState a,ZeldaAiState b)=>false;private void PlayAwarenessCue(){}
 private void BeginRecovery(){ChangeState(ZeldaAiState.Recovery);}
 public void ExternalMelee(){ChangeState(ZeldaAiState.Hostile);}
 public void Tick(float deltaTime){if(IsStunned)return;ResumePendingHandgunInvestigation();if(IsIgnoringPlayer)return;normalPerceptionCalls++;
'@
$code+=$searchBranch+"`n}`n"
foreach($sig in @('    public virtual void InvestigateHandgunShot(', '    private void ResumePendingHandgunInvestigation(',
 '    public virtual void InvestigatePosition(', '    protected virtual void ChangeState(')){$code+=Extract $ai $sig}
$code+=@'
}
public class TwoStateAi:ZeldaCharacterAiBase {public override void InvestigatePosition(Vector2 p){} }
public static class InvestigationTests {
 static int checks;static void Check(bool b,string m){checks++;if(!b)throw new Exception(m);}
 static ZeldaCharacterData Actor(int hp=10){var d=new ZeldaCharacterData{currentHealth=hp};var mover=new ZeldaFourWayMover{gameObject=d.gameObject,isActiveAndEnabled=false};var ai=new ZeldaCharacterAiBase{gameObject=d.gameObject,characterData=d,mover=mover};d.gameObject.components.AddRange(new object[]{d,mover,ai});return d;}
 public static int Run(){
  var origin=new Vector2(7,11);
  for(int health=1;health<=12;health++)for(int power=1;power<=5;power++){
   var d=Actor(health);var a=d.GetComponent<ZeldaCharacterAiBase>();int witnesses=ZeldaCharacterAiBase.witnessCalls;
   d.TakeHandgunDamage(power,origin);Check(d.currentHealth==Math.Max(0,health-power),"Normal damage preserved");
   Check(d.retalCalls==0&&ZeldaCharacterAiBase.witnessCalls==witnesses,"No retaliation or witness identity on either fatal or nonfatal bullet");
   Check(d.IsDead?a.currentState==ZeldaAiState.Idle:a.currentState==ZeldaAiState.Search,"Only surviving target investigates");
   if(!d.IsDead){Check(a.investigationIncidentPosition.x==7&&a.investigationIncidentPosition.y==11,"Investigates fixed firing origin");
    a.Tick(30);Check(a.currentState==ZeldaAiState.Search&&a.normalPerceptionCalls==1,"Investigates unseen shooter without expiring during travel, while keeping perception active");
    a.isTravelingToInvestigation=false;a.Tick(2);Check(a.currentState==ZeldaAiState.Search,"Searches after arrival");a.Tick(1.1f);Check(a.currentState==ZeldaAiState.Recovery,"Normal recovery after search");
    a.Tick(.1f);Check(a.normalPerceptionCalls==4,"Normal perception remains active throughout and after investigation");}
  }
  var victim=Actor();var ai=victim.GetComponent<ZeldaCharacterAiBase>();ai.currentState=ZeldaAiState.Hostile;ai.regularHostile=true;ai.retaliationHostile=true;ai.hostileUntilTargetDeath=true;
  victim.TakeHandgunDamage(2,origin);Check(ai.currentState==ZeldaAiState.Search&&!ai.regularHostile&&!ai.retaliationHostile&&!ai.hostileUntilTargetDeath,"Investigation replaces immediate retaliation/pursuit latches");
  var attacker=Actor();victim.Hit(1,attacker);Check(ai.currentState==ZeldaAiState.Hostile&&victim.retalCalls==1,"Later ordinary melee still causes hostility");ai.Tick(.1f);Check(ai.normalPerceptionCalls==1,"Direct hostility interrupts anonymous investigation");
  victim=Actor();ai=victim.GetComponent<ZeldaCharacterAiBase>();ai.currentState=ZeldaAiState.Stunned;victim.TakeHandgunDamage(2,origin);
  Check(ai.currentState==ZeldaAiState.Stunned,"Hit does not clear stun");ai.currentState=ZeldaAiState.Idle;ai.Tick(.1f);Check(ai.currentState==ZeldaAiState.Search,"Queued investigation starts after stun");
  victim=Actor();var linked=Actor();DominoSkillRuntime.first=victim;DominoSkillRuntime.second=linked;victim.TakeHandgunDamage(2,origin);
  Check(linked.currentHealth==8&&linked.retalCalls==0&&DominoSkillRuntime.lastAttacker==null,"Domino shares damage without restoring player identity");DominoSkillRuntime.first=null;DominoSkillRuntime.second=null;
  victim=Actor();ai=victim.GetComponent<ZeldaCharacterAiBase>();victim.GetComponent<ZeldaFourWayMover>().ActiveCardboardBox=new CardboardBoxWearState{blocks=true};victim.TakeHandgunDamage(2,origin);
  Check(victim.currentHealth==10&&ai.currentState==ZeldaAiState.Idle,"Blocked shot preserves mitigation and does not count as character injury");
  victim=Actor();ai=victim.GetComponent<ZeldaCharacterAiBase>();ai.multiplier=3;victim.TakeHandgunDamage(2,origin);Check(victim.currentHealth==4,"Existing damage modifiers unchanged");
  victim=Actor();ai=victim.GetComponent<ZeldaCharacterAiBase>();victim.TakeHandgunDamage(0,origin);victim.TakeHandgunDamage(-1,origin);Check(ai.currentState==ZeldaAiState.Idle,"Invalid damage does not trigger investigation");
  victim=Actor();ai=victim.GetComponent<ZeldaCharacterAiBase>();ai.mover.isActiveAndEnabled=true;victim.TakeHandgunDamage(2,origin);Check(ai.currentState==ZeldaAiState.Idle,"Player-controlled body never commandeered by investigation");
  var sentinel=new TwoStateAi{characterData=Actor()};sentinel.InvestigateHandgunShot(origin);Check(sentinel.currentState==ZeldaAiState.Idle,"Two-state automaton retains its explicit non-investigating contract");
  foreach(bool traveling in new[]{true,false}){
   victim=Actor();ai=victim.GetComponent<ZeldaCharacterAiBase>();victim.TakeHandgunDamage(2,origin);
   ai.isTravelingToInvestigation=traveling;ai.isPausingDuringInvestigation=!traveling;
   ai.Tick(.1f);Check(ai.currentState==ZeldaAiState.Search,"No visible player: keep investigating");
   ai.visiblePlayer=new ZeldaFourWayMover();ai.visiblePlayer.transform.position=new Vector2(13,17);
   ai.Tick(.01f);Check(ai.currentState==ZeldaAiState.Hostile&&ai.regularHostile&&ai.targetMover==ai.visiblePlayer,"Visible player becomes hostile immediately during travel or pause");
   Check(ai.lastKnownTargetPosition.x==13&&ai.lastKnownTargetPosition.y==17&&ai.regularHostileLostSightTimer==0,"Hostility tracks newly visible player using common search logic");
  }
  victim=Actor();ai=victim.GetComponent<ZeldaCharacterAiBase>();ai.currentState=ZeldaAiState.Stunned;victim.TakeHandgunDamage(2,origin);
  ai.visiblePlayer=new ZeldaFourWayMover();ai.currentState=ZeldaAiState.Idle;ai.Tick(.01f);
  Check(ai.currentState==ZeldaAiState.Hostile,"Queued investigation checks visibility in the same frame stun ends");
  return checks;
 }
}
'@
Add-Type -TypeDefinition $code -CompilerOptions '/nowarn:0414,0649'
$count=[InvestigationTests]::Run()
$update=Extract $ai '    protected virtual void UpdateTargetAndState('
if($update.IndexOf('ResumePendingHandgunInvestigation();') -lt 0 -or $update.IndexOf('ResumePendingHandgunInvestigation();') -gt $update.IndexOf('FindVisiblePlayer')){throw 'Pending investigation must resume before normal perception'}
if($ai.Contains('if (UpdateHandgunInvestigation(deltaTime)) return;')){throw 'Handgun investigation must not bypass normal perception'}
if((Extract $ai '    private void ResumePendingHandgunInvestigation()').Contains('searchTimer')){throw 'Search timing must run only in common search logic'}
foreach($key in @('handgunInvestigation','pendingHandgunInvestigation','handgunIncident','handgunApproach','handgunTraveling','handgunPausing','handgunPause')){
 if(!(Extract $ai '    public virtual SaveState CaptureSaveState()').Contains($key) -or !(Extract $ai '    public virtual void ApplySaveState(').Contains($key)){throw "Missing save/restore: $key"}
}
Write-Output "PASS: $count production damage/investigation checks; perception ordering and save-state wiring verified. Navigation/presentation stubbed."
