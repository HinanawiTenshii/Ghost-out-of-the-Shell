$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$ai=Get-Content -Raw -Encoding UTF8 "$root/Assets/Scripts/AI/ZeldaCharacterAiBase.cs"
$automaton=Get-Content -Raw -Encoding UTF8 "$root/Assets/Scripts/AI/AutomatonCharacterAi.cs"
function Extract($source,$signature) {
    $start=$source.IndexOf($signature); if($start -lt 0){throw "Missing $signature"}
    $brace=$source.IndexOf('{',$start);$end=$brace+1;$depth=1
    while($depth -gt 0){if($source[$end] -eq '{'){$depth++};if($source[$end] -eq '}'){$depth--};$end++}
    return $source.Substring($start,$end-$start)
}
# Execute the complete production Automaton AI and the shared character/threat
# perception methods. Physics occlusion, navigation and Unity lifecycle are stubs.
$stub=@'
#pragma warning disable 0414
using System;
using System.Collections.Generic;
using UnityEngine;
namespace UnityEngine {
    public class SerializeField:Attribute {}
    public class HeaderAttribute:Attribute {public HeaderAttribute(string s){}}
    public class TooltipAttribute:Attribute {public TooltipAttribute(string s){}}
    public class ContextMenu:Attribute {public ContextMenu(string s){}}
    public enum RuntimeInitializeLoadType {SubsystemRegistration}
    public class RuntimeInitializeOnLoadMethodAttribute:Attribute {public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t){}}
    public struct Color {
        public float r,g,b,a;
        public Color(float r,float g,float b,float a){this.r=r;this.g=g;this.b=b;this.a=a;}
    }
    public struct Vector2 {
        public float x,y; public Vector2(float x,float y){this.x=x;this.y=y;}
        public static Vector2 zero=>new Vector2(0,0);
        public float sqrMagnitude=>x*x+y*y;public float magnitude=>(float)Math.Sqrt(sqrMagnitude);
        public Vector2 normalized=>magnitude>0?new Vector2(x/magnitude,y/magnitude):zero;
        public static Vector2 operator -(Vector2 a,Vector2 b)=>new Vector2(a.x-b.x,a.y-b.y);
        public static float Angle(Vector2 a,Vector2 b) {
            if(a.magnitude*b.magnitude==0)return 0;
            return (float)(Math.Acos(Math.Max(-1,Math.Min(1,(a.x*b.x+a.y*b.y)/(a.magnitude*b.magnitude))))*180/Math.PI);
        }
    }
    public class Transform {public Vector2 position; public bool occluded;}
    public class GameObject {
        public bool activeInHierarchy=true;public int scene=1;public Transform transform=new Transform();
        public Dictionary<Type,Component> components=new Dictionary<Type,Component>();
    }
    public class Component {
        public GameObject gameObject;public bool enabled=true;
        public Transform transform=>gameObject.transform;
        public bool isActiveAndEnabled=>enabled&&gameObject.activeInHierarchy;
        public T GetComponent<T>() where T:class {
            foreach(var c in gameObject.components.Values)if(c is T)return c as T;return null;
        }
    }
    public class Body {public Vector2 position;}
}
public class ZeldaCharacterData:Component {
    public bool IsDead,IsGhostLike;public bool CanBeDetectedByAi=true;
}
public class AutomatonZeldaCharacterData:ZeldaCharacterData {}
public class SkeletonZeldaCharacterData:ZeldaCharacterData {}
public class CardboardBoxPickupItem {}
public class ZeldaFourWayMover:Component {public int CancelCount;public void CancelAttackForStun(){CancelCount++;}}
public enum ZeldaAiState {Idle,Suspicious,Alert,Search,Recovery,Hostile,Stunned}
public static class ZeldaRuntimeRegistry {
    public static List<ZeldaFourWayMover> Movers=new List<ZeldaFourWayMover>();
    public static IEnumerable<ZeldaCharacterAiBase> AiCharacters {get{foreach(var m in Movers)yield return m.GetComponent<ZeldaCharacterAiBase>();}}
    public static int GetGameplayScene(GameObject g)=>g.scene;
}
public class ZeldaCharacterAiBase:Component {
    protected ZeldaFourWayMover mover,targetMover;
    protected ZeldaCharacterData characterData;
    protected Body rb=new Body();
    private Vector2 initialScenePosition,initialSceneFacingDirection=new Vector2(1,0);
    private float recoveryPositionTolerance=.15f;
    public Vector2 PlannedDestination;
    private Vector2 PlanDirectionTo(Vector2 target){PlannedDestination=target;return (target-rb.position).normalized;}
    private void UpdateCharacterVisual(){}
    public void MoveTo(Vector2 position){rb.position=position;transform.position=position;}
    public void RecoveryStep(){TickRecovery(.02f);}
    protected Vector2 facingDirection=new Vector2(1,0),moveDirection,lastKnownTargetPosition;
    private float visionRadius=7,visionAngle=90,peripheralVisionRadius=1,peripheralVisionAngle=180;
    protected float regularHostileLostSightTimer,suspicionValue,warningTimer;
    private float royalCommandRemaining;
    public bool IsStunned,possessionLocked;
    public bool IsIgnoringPlayer=>royalCommandRemaining>0;
    private object FindHighestPermissionArea(Vector2 position)=>null;
    protected object activePermissionArea,recognizedPermissionTarget,suspiciousCardboardBox,suspiciousCardboardBoxWearer,
        suspiciousGroundCardboardBox,inspectedCardboardBoxWearer,hostileCardboardBox;
    protected bool hostileUntilTargetDeath,retaliationHostile,regularHostile,hasCardboardBoxAttractionTarget,
        isInspectingCardboardBox,isTravelingToInvestigation;
    private ZeldaCharacterAiBase universalThreatTarget;
    private ZeldaAiState currentState;
    private float regularHostileLoseSightDelay=7f,searchDuration=4f,investigationDuration=4f,searchTimer,searchWaypointTimer,investigationPauseTimer;
    private bool isDoorIncidentInvestigation,isPausingDuringInvestigation;
    private Vector2 searchCenter;
    private bool alarmResponseActive,pendingAlarmResponse,pendingHandgunInvestigation,anonymousHandgunInvestigation;
    private Vector2 alarmPosition,investigationIncidentPosition,investigationApproachPosition,searchWaypoint,
        previousFixedPosition,cornerEscapeDirection,cornerOscillationStartPosition;
    private float previousSearchWaypointDistance,cornerStuckTimer,cornerEscapeTimer,cornerOscillationElapsed,cornerOscillationTravelDistance;
    private Vector2 ResolveInvestigationApproachPosition(Vector2 p)=>p;
    public bool IsRespondingToAlarm=>alarmResponseActive;
    public bool PendingAlarm=>pendingAlarmResponse;
    public Vector2 AlarmDestination=>investigationIncidentPosition;
    public void ReachIncident(){isTravelingToInvestigation=false;}
    public void SetIgnore(float value){royalCommandRemaining=value;}
    public ZeldaAiState CurrentState=>currentState;
    public float LostSightTime=>regularHostileLostSightTimer;
    public ZeldaFourWayMover CurrentTarget=>targetMover;
    public virtual bool IsUniversalThreat=>false;
    public virtual bool SharesHostilityWithOtherNpcs=>true;
    protected virtual bool RemainsStationary=>false;
    protected virtual bool EnforcesPermissionAreas=>true;
    protected Vector2 AiPosition=>rb.position;
    protected Vector2 AiMoveDirection{get=>moveDirection;set=>moveDirection=value;}
    public bool Stationary=>RemainsStationary;
    private Color visionColor=new Color(.45f,.8f,1f,.22f);
    public Color RenderedVisionColor=>VisionVisualColor;
    public void SetConfiguredVisionColor(Color value){visionColor=value;}
    protected void SetFacingDirection(Vector2 direction){facingDirection=direction.normalized;}
    protected bool IsVisionBlocked(Vector2 o,Vector2 d,float len,Transform target)=>target.occluded;
    protected virtual void Awake(){mover=GetComponent<ZeldaFourWayMover>();characterData=GetComponent<ZeldaCharacterData>();rb.position=transform.position;initialScenePosition=rb.position;}
    protected virtual void OnValidate(){}
    protected virtual void OnDestroy(){}
    protected virtual void ChangeState(ZeldaAiState state,bool cue=true){currentState=state;}
    protected virtual bool CanSeePlayer(ZeldaFourWayMover m)=>m!=null&&m.isActiveAndEnabled&&CanSeeCharacter(m);
    public virtual void OnPlayerAttackedCharacter(ZeldaCharacterData a,ZeldaCharacterData v){ChangeState(ZeldaAiState.Hostile);}
    public virtual void OnPlayerPlacedBomb(ZeldaCharacterData user){ChangeState(ZeldaAiState.Hostile);}
    public virtual void OnPossessionWitnessed(ZeldaFourWayMover a,ZeldaFourWayMover b){ChangeState(ZeldaAiState.Hostile);}
    public virtual bool OnPlayerDestroyedDoor(ZeldaCharacterData a){ChangeState(ZeldaAiState.Hostile);return true;}
    public virtual bool CanReceiveRoyalCommand(ZeldaFourWayMover p)=>true;
    public virtual void ReceiveRoyalCommand(){ChangeState(ZeldaAiState.Recovery);}
    public class SaveState {public ZeldaAiState state;public bool automatonHostilityActivated,alarmResponse,pendingAlarmResponse;public Vector2 alarmPosition,home,homeFacing;}
    public virtual SaveState CaptureSaveState()=>new SaveState{state=CurrentState};
    public virtual void ApplySaveState(SaveState s){ChangeState(s.state,false);}
    public virtual void RecoverAfterPersistentSceneReturn(ZeldaAiState state){ChangeState(ZeldaAiState.Recovery);}
    protected void ResetPermissionResponse(){targetMover=null;regularHostile=false;moveDirection=Vector2.zero;ChangeState(ZeldaAiState.Idle);}
    protected virtual void UpdateTargetAndState(float dt){if(TryRespondToUniversalThreat())return;UpdateRememberedHostility(dt);}
    private void ChooseNextSearchWaypoint(){}
    private CardboardBoxPickupItem FindNearestVisibleGroundBox()=>null;
    private bool UpdateCardboardBoxSuspicion(float dt)=>false;
    private ZeldaFourWayMover FindVisiblePlayer(){foreach(var candidate in ZeldaRuntimeRegistry.Movers)if(CanSeePlayer(candidate))return candidate;return null;}
    public void Redirect(ZeldaFourWayMover candidate){BeginTemporaryHostility(candidate,false);}
    public void Initialize(){Awake();ChangeState(ZeldaAiState.Idle);}
    public void Step(float dt=.02f){UpdateTargetAndState(dt);}
    public void Request(ZeldaAiState state){ChangeState(state);}
'@
$colorHook=[regex]::Match($ai,'protected virtual Color VisionVisualColor => visionColor;').Value
if(!$colorHook){throw 'Missing production vision color hook'}
$stub+=$colorHook
$stub+=Extract $ai '    public void RespondToAlarm('
$stub+=Extract $ai '    public void RestoreAlarmResponseState('
$stub+=Extract $ai '    public virtual void InvestigatePosition('
foreach($signature in @('    private void CreateVisionVisual()', '    private void UpdateVisionMesh()')){
    if(!(Extract $ai $signature).Contains('visionMaterial.color = VisionVisualColor;')){throw 'Vision material bypasses state color'}
}
foreach($sig in @('    public virtual void OnCharacterDamagedBy(', '    protected virtual bool CanSeeCharacter(', '    private bool TryRespondToUniversalThreat()', '    protected bool CanSeeWorldPoint(', '    protected void ConfigureOmnidirectionalVision()', '    protected void BeginTemporaryHostility(', '    private void BeginSearch()', '    protected void BeginRecovery()', '    protected virtual void TickRecovery(', '    private void CompleteRecovery()', '    private bool IsTargetAlive()')) {
    $stub+=Extract $ai $sig
}
# Run the exact shared regular-hostility/search branches after threat acquisition.
# Other player permission/possession branches are covered by their separate tests.
$update=Extract $ai '    protected virtual void UpdateTargetAndState('
$begin=$update.IndexOf('        if (regularHostile)')
$end=$update.IndexOf('        if (currentState == ZeldaAiState.Recovery')
if($begin -lt 0 -or $end -le $begin){throw 'Missing shared pursuit/search branches'}
$stub+='private void UpdateRememberedHostility(float deltaTime){'+$update.Substring($begin,$end-$begin)+'}'
$stub+="}"
$stub+=$automaton.Replace('using System.Collections.Generic;','').Replace('using UnityEngine;','')
$skeleton=Get-Content -Raw "$root/Assets/Scripts/AI/SkeletonCharacterAi.cs"
$stub+=$skeleton.Replace('using UnityEngine;','')
$stub+=@'
public static class AutomatonTests {
    private static int checks;
    static void Check(bool value,string text){checks++;if(!value)throw new Exception(text);}
    static T Make<T>(float x,float y,bool player=false) where T:ZeldaCharacterAiBase,new(){
        var go=new GameObject();go.transform.position=new Vector2(x,y);
        var data=new ZeldaCharacterData{gameObject=go};var mover=new ZeldaFourWayMover{gameObject=go,enabled=player};
        var ai=new T{gameObject=go};go.components[typeof(ZeldaCharacterData)]=data;
        go.components[typeof(ZeldaFourWayMover)]=mover;go.components[typeof(ZeldaCharacterAiBase)]=ai;
        ZeldaRuntimeRegistry.Movers.Add(mover);ai.Initialize();return ai;
    }
    public static int Run(){
        var a=Make<AutomatonCharacterAi>(0,0);
        var blue=new Color(.45f,.8f,1f,.22f);var red=new Color(.88f,.18f,.12f,.16f);
        a.SetConfiguredVisionColor(red);
        Check(a.RenderedVisionColor.Equals(blue),"Dormant vision matches ordinary NPC blue");
        var npc=Make<ZeldaCharacterAiBase>(-3,0);var player=Make<ZeldaCharacterAiBase>(4,0,true);
        Check(npc.RenderedVisionColor.Equals(blue),"Ordinary NPC vision unchanged");
        npc.Request(ZeldaAiState.Hostile);
        Check(npc.RenderedVisionColor.Equals(blue),"Ordinary hostile NPC color unchanged");
        a.Step();Check(a.CurrentState==ZeldaAiState.Idle&&a.CurrentTarget==null&&a.Stationary,"Dormant must be stationary");
        a.OnCharacterDamagedBy(npc.GetComponent<ZeldaCharacterData>());a.InvestigatePosition(new Vector2(2,0));
        a.OnPlayerAttackedCharacter(player.GetComponent<ZeldaCharacterData>(),npc.GetComponent<ZeldaCharacterData>());
        a.OnPlayerPlacedBomb(player.GetComponent<ZeldaCharacterData>());
        a.OnPossessionWitnessed(npc.GetComponent<ZeldaFourWayMover>(),player.GetComponent<ZeldaFourWayMover>());
        a.OnPlayerDestroyedDoor(player.GetComponent<ZeldaCharacterData>());
        a.Request(ZeldaAiState.Alert);a.ReceiveRoyalCommand();a.Step();
        Check(!a.HostilityActivated&&a.CurrentState==ZeldaAiState.Idle,"Only explicit activation may wake it");
        npc.Step();Check(npc.CurrentTarget==null,"Others ignore dormant automaton");
        a.ActivateHostility();a.Step();
        Check(a.RenderedVisionColor.Equals(red),"Activation uses existing red including alpha");
        a.Request(ZeldaAiState.Stunned);
        Check(a.RenderedVisionColor.Equals(red),"Temporary stun does not look deactivated");
        a.Request(ZeldaAiState.Hostile);
        Check(a.IsUniversalThreat&&a.CurrentState==ZeldaAiState.Hostile,"Activation enters Hostile");
        Check(a.CurrentTarget==npc.GetComponent<ZeldaFourWayMover>(),"360 degree vision acquires disabled NPC mover behind it");
        npc.Step();Check(npc.CurrentTarget==a.GetComponent<ZeldaFourWayMover>(),"NPC attacks active automaton");
        npc.GetComponent<ZeldaCharacterData>().IsDead=true;a.Step();
        Check(a.CurrentTarget==player.GetComponent<ZeldaFourWayMover>(),"Retarget after death");
        player.transform.occluded=true;a.Step();
        Check(a.CurrentTarget==null&&a.CurrentState==ZeldaAiState.Hostile&&a.Stationary,"Walls block vision without ending activation");
        player.transform.occluded=false;player.gameObject.scene=2;a.Step();
        Check(a.CurrentTarget==null,"No targeting another scene");
        player.gameObject.scene=1;player.transform.position=new Vector2(20,0);a.Step();
        Check(a.CurrentTarget==null,"Respect radius");
        player.transform.position=new Vector2(4,0);player.GetComponent<ZeldaCharacterData>().CanBeDetectedByAi=false;a.Step();
        Check(a.CurrentTarget==null,"Retain ghost/invisibility rules");
        player.GetComponent<ZeldaCharacterData>().CanBeDetectedByAi=true;a.Step();
        Check(a.CurrentTarget==player.GetComponent<ZeldaFourWayMover>(),"Reacquire visible character");
        a.GetComponent<ZeldaFourWayMover>().enabled=true;
        Check(!a.IsUniversalThreat,"Player-controlled automaton not a universal threat");
        npc.Step();Check(npc.CurrentTarget==null,"NPC drops universal hostility on control switch");
        a.GetComponent<ZeldaFourWayMover>().enabled=false;
        var saved=a.CaptureSaveState();a.DeactivateHostility();a.Step();
        Check(a.RenderedVisionColor.Equals(blue),"Deactivation restores blue");
        Check(!a.IsUniversalThreat&&a.CurrentState==ZeldaAiState.Idle&&a.CurrentTarget==null,"Deactivation stops attacks and pursuit");
        a.ApplySaveState(saved);a.RecoverAfterPersistentSceneReturn(saved.state);
        Check(a.RenderedVisionColor.Equals(red),"Save/scene restore restores red");
        Check(a.HostilityActivated&&a.CurrentState==ZeldaAiState.Hostile,"Save/scene return retains condition");
        a.Request(ZeldaAiState.Search);Check(a.CurrentState==ZeldaAiState.Hostile,"No Search state");
        a.DeactivateHostility();a.Request(ZeldaAiState.Hostile);
        var dormant=a.CaptureSaveState();a.ActivateHostility();a.ApplySaveState(dormant);
        Check(a.RenderedVisionColor.Equals(blue),"Dormant save restores blue after activation");
        Check(a.CurrentState==ZeldaAiState.Idle,"Incidental state request cannot activate");
        var other=Make<AutomatonCharacterAi>(2,0);other.ActivateHostility();a.Step();
        Check(a.CurrentTarget==null,"Dormant automaton ignores other threats");
        a.ActivateHostility();a.Step();
        Check(a.CurrentTarget==player.GetComponent<ZeldaFourWayMover>(),"Skip nearer hostile automaton and acquire ordinary player");
        other.DeactivateHostility();a.Step();
        Check(a.CurrentTarget==player.GetComponent<ZeldaFourWayMover>(),"Dormant fellow is also excluded");
        other.GetComponent<ZeldaFourWayMover>().enabled=true;other.enabled=false;
        player.gameObject.activeInHierarchy=false;a.Step();
        Check(a.CurrentTarget==null&&a.Stationary&&a.CurrentState==ZeldaAiState.Hostile,"Only player-controlled fellow visible: wait without attacking");
        var orphan=Make<ZeldaCharacterAiBase>(1,0);
        orphan.gameObject.components[typeof(ZeldaCharacterData)]=new AutomatonZeldaCharacterData{gameObject=orphan.gameObject};
        a.Step();Check(a.CurrentTarget==null,"Automaton character data identifies fellow even without its AI");
        var targetField=typeof(ZeldaCharacterAiBase).GetField("targetMover",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
        targetField.SetValue(a,other.GetComponent<ZeldaFourWayMover>());
        int cancelled=a.GetComponent<ZeldaFourWayMover>().CancelCount;
        a.Step();Check(a.CurrentTarget==null&&a.GetComponent<ZeldaFourWayMover>().CancelCount>cancelled,"Drop previously retained fellow and cancel its attack");
        targetField.SetValue(a,other.GetComponent<ZeldaFourWayMover>());
        a.ApplySaveState(a.CaptureSaveState());
        Check(a.CurrentTarget==null,"Restored friendly target cleared immediately");
        player.gameObject.activeInHierarchy=true;a.Step();
        Check(a.CurrentTarget==player.GetComponent<ZeldaFourWayMover>(),"Resume ordinary target after only-fellows interval");
        npc.GetComponent<ZeldaCharacterData>().IsDead=false;npc.Step();
        Check(npc.CurrentTarget==a.GetComponent<ZeldaFourWayMover>(),"Ordinary NPC still attacks activated automaton");
        foreach(var old in AutomatonCharacterAi.Instances)old.gameObject.activeInHierarchy=false;
        ZeldaRuntimeRegistry.Movers.Clear();
        var guard=Make<ZeldaCharacterAiBase>(0,0);var threat=Make<AutomatonCharacterAi>(3,0);threat.ActivateHostility();
        var threatMover=threat.GetComponent<ZeldaFourWayMover>();guard.Step();
        Check(guard.CurrentTarget==threatMover&&guard.CurrentState==ZeldaAiState.Hostile,"Visible threat acquired");
        int cancels=guard.GetComponent<ZeldaFourWayMover>().CancelCount;
        threat.transform.occluded=true;guard.Step(.1f);
        Check(guard.CurrentTarget==threatMover&&guard.CurrentState==ZeldaAiState.Hostile,"Losing sight for one frame retains hostility and target");
        Check(guard.GetComponent<ZeldaFourWayMover>().CancelCount==cancels,"Occlusion does not cancel attack");
        guard.Step(3f);Check(guard.LostSightTime>3f&&guard.CurrentState==ZeldaAiState.Hostile,"Shared lose-sight timer advances");
        threat.transform.occluded=false;guard.Step();Check(guard.LostSightTime==0,"Reacquiring NPC mover resets timer despite disabled player movement");
        threat.transform.occluded=true;guard.Step(6.9f);Check(guard.CurrentState==ZeldaAiState.Hostile,"Pursuit uses ordinary player grace duration");
        guard.Step(.11f);Check(guard.CurrentState==ZeldaAiState.Search,"Timeout enters shared search, not idle");
        guard.Step(2f);Check(guard.CurrentState==ZeldaAiState.Search,"Search persists after target is out of sight");
        threat.transform.occluded=false;guard.Step();Check(guard.CurrentState==ZeldaAiState.Hostile,"Reappearance during search resumes hostility");
        threat.transform.occluded=true;guard.Step(7.1f);guard.Step(4.1f);
        Check(guard.CurrentState==ZeldaAiState.Recovery&&guard.CurrentTarget==null,"Failed search uses ordinary recovery");
        threat.transform.occluded=false;guard.Step();threat.DeactivateHostility();guard.Step();
        Check(guard.CurrentState==ZeldaAiState.Recovery&&guard.CurrentTarget==null,"Actual deactivation ends pursuit");
        threat.ActivateHostility();guard.Step();threat.gameObject.scene=2;guard.Step();
        Check(guard.CurrentState==ZeldaAiState.Recovery,"Cross-scene threat invalidated");
        threat.gameObject.scene=1;guard.Step();threat.GetComponent<ZeldaCharacterData>().IsDead=true;guard.Step();
        Check(guard.CurrentState==ZeldaAiState.Recovery,"Dead threat invalidated");
        threat.GetComponent<ZeldaCharacterData>().IsDead=false;guard.Step();
        var replacement=Make<ZeldaCharacterAiBase>(4,0,true);guard.Redirect(replacement.GetComponent<ZeldaFourWayMover>());
        threat.DeactivateHostility();guard.Step();
        Check(guard.CurrentTarget==replacement.GetComponent<ZeldaFourWayMover>()&&guard.CurrentState==ZeldaAiState.Hostile,"Stale automaton reference cannot clear newer player hostility");
        ZeldaRuntimeRegistry.Movers.Clear();
        var skeleton=Make<SkeletonCharacterAi>(0,0);
        skeleton.gameObject.components[typeof(ZeldaCharacterData)]=new SkeletonZeldaCharacterData{gameObject=skeleton.gameObject};
        var fellow=Make<SkeletonCharacterAi>(1,0);
        fellow.gameObject.components[typeof(ZeldaCharacterData)]=new SkeletonZeldaCharacterData{gameObject=fellow.gameObject};
        skeleton.Step();Check(skeleton.CurrentTarget==null,"Skeleton does not hunt or respond to universal threat of fellow skeleton");
        var citizen=Make<ZeldaCharacterAiBase>(3,0);skeleton.Step();
        Check(skeleton.CurrentTarget==citizen.GetComponent<ZeldaFourWayMover>()&&skeleton.CurrentState==ZeldaAiState.Hostile,"Skeleton hunts non-player living NPC");
        citizen.GetComponent<ZeldaCharacterData>().IsDead=true;
        var playerTarget=Make<ZeldaCharacterAiBase>(2,0,true);skeleton.Step();
        Check(skeleton.CurrentTarget==playerTarget.GetComponent<ZeldaFourWayMover>(),"Dead NPC excluded and player becomes target");
        playerTarget.gameObject.activeInHierarchy=false;
        skeleton.GetComponent<ZeldaCharacterData>().IsDead=true;
        Check(!skeleton.IsUniversalThreat,"Downed skeleton is not an active threat");
        skeleton.GetComponent<ZeldaCharacterData>().IsDead=false;
        var watcher=Make<ZeldaCharacterAiBase>(-2,0);watcher.Step();
        Check(watcher.CurrentTarget==skeleton.GetComponent<ZeldaFourWayMover>(),"Ordinary NPC responds to living skeleton as enemy");
        foreach(bool machine in new[]{false,true}){
            ZeldaRuntimeRegistry.Movers.Clear();
            var defender=Make<ZeldaCharacterAiBase>(0,0);
            ZeldaCharacterAiBase assailant;
            if(machine){var robot=Make<AutomatonCharacterAi>(-3,0);robot.ActivateHostility();assailant=robot;}
            else{
                var bones=Make<SkeletonCharacterAi>(-3,0);
                bones.gameObject.components[typeof(ZeldaCharacterData)]=new SkeletonZeldaCharacterData{gameObject=bones.gameObject};assailant=bones;
            }
            assailant.transform.occluded=true;
            defender.Step();Check(defender.CurrentTarget==null,"Unseen independent enemy is not acquired without damage");
            defender.OnCharacterDamagedBy(assailant.GetComponent<ZeldaCharacterData>());
            Check(defender.CurrentState==ZeldaAiState.Hostile&&defender.CurrentTarget==assailant.GetComponent<ZeldaFourWayMover>(),"Damage acquires unseen NPC with disabled player mover");
            defender.Step(.1f);Check(defender.CurrentState==ZeldaAiState.Hostile,"Unseen retaliation is retained next tick");
            defender.Step(7.1f);Check(defender.CurrentState==ZeldaAiState.Search,"Retaliation uses ordinary lost-sight search rather than immediate idle");
            assailant.transform.occluded=false;defender.Step();
            Check(defender.CurrentState==ZeldaAiState.Hostile,"Reacquiring assailant resumes attack");
            assailant.GetComponent<ZeldaCharacterData>().IsDead=true;defender.Step();
            Check(defender.CurrentTarget==null&&defender.CurrentState==ZeldaAiState.Recovery,"Dead attacking creature releases retaliation target");
        }
        ZeldaRuntimeRegistry.Movers.Clear();
        var bystander=Make<ZeldaCharacterAiBase>(0,0);var inactive=Make<AutomatonCharacterAi>(-2,0);
        bystander.OnCharacterDamagedBy(inactive.GetComponent<ZeldaCharacterData>());
        Check(bystander.CurrentTarget==null,"Dormant automaton not treated as active attacker");
        inactive.ActivateHostility();inactive.gameObject.scene=2;bystander.OnCharacterDamagedBy(inactive.GetComponent<ZeldaCharacterData>());
        Check(bystander.CurrentTarget==null,"No cross-scene retaliation");inactive.gameObject.scene=1;
        bystander.IsStunned=true;bystander.OnCharacterDamagedBy(inactive.GetComponent<ZeldaCharacterData>());
        Check(bystander.CurrentTarget==null,"Stun perception guard retained");bystander.IsStunned=false;
        bystander.possessionLocked=true;bystander.OnCharacterDamagedBy(inactive.GetComponent<ZeldaCharacterData>());
        Check(bystander.CurrentTarget==null,"Possession lock guard retained");bystander.possessionLocked=false;
        bystander.OnCharacterDamagedBy(inactive.GetComponent<ZeldaCharacterData>());inactive.DeactivateHostility();bystander.Step();
        Check(bystander.CurrentTarget==null&&bystander.CurrentState==ZeldaAiState.Recovery,"Deactivating attacking automaton ends pursuit");
        ZeldaRuntimeRegistry.Movers.Clear();
        var revived=Make<SkeletonCharacterAi>(1,2);
        revived.gameObject.components[typeof(ZeldaCharacterData)]=new SkeletonZeldaCharacterData{gameObject=revived.gameObject};
        revived.MoveTo(new Vector2(10,2));revived.ResumeAfterReassembly();
        Check(revived.CurrentState==ZeldaAiState.Recovery&&revived.CurrentTarget==null,"Revived skeleton without visible enemy enters recovery");
        revived.Step();Check(revived.CurrentState==ZeldaAiState.Recovery,"Recovery survives the next AI update");
        revived.RecoveryStep();Check(revived.PlannedDestination.x==1&&revived.PlannedDestination.y==2,"Routes to original spawn, not death position");
        revived.MoveTo(new Vector2(1,2));revived.RecoveryStep();Check(revived.CurrentState==ZeldaAiState.Idle,"Reaching home completes recovery");
        revived.MoveTo(new Vector2(20,2));revived.ResumeAfterReassembly();revived.RecoveryStep();
        Check(revived.PlannedDestination.x==1&&revived.PlannedDestination.y==2,"Repeated revival preserves the same home");
        var nearbyEnemy=Make<ZeldaCharacterAiBase>(21,2);revived.Step();
        Check(revived.CurrentState==ZeldaAiState.Hostile&&revived.CurrentTarget==nearbyEnemy.GetComponent<ZeldaFourWayMover>(),"Enemy spotted during recovery interrupts return");
        revived.ResumeAfterReassembly();Check(revived.CurrentState==ZeldaAiState.Hostile,"Visible enemy at resurrection immediately takes priority");
        nearbyEnemy.transform.occluded=true;revived.ResumeAfterReassembly();Check(revived.CurrentState==ZeldaAiState.Recovery,"Occluded enemy does not prevent return after revival");
        var nearbyBones=Make<SkeletonCharacterAi>(22,2);nearbyBones.gameObject.components[typeof(ZeldaCharacterData)]=new SkeletonZeldaCharacterData{gameObject=nearbyBones.gameObject};
        revived.ResumeAfterReassembly();Check(revived.CurrentState==ZeldaAiState.Recovery,"Visible fellow skeleton is not a reason to stop returning");
        ZeldaRuntimeRegistry.Movers.Clear();
        var responder=Make<ZeldaCharacterAiBase>(0,0);
        responder.SetIgnore(5);responder.RespondToAlarm(new Vector2(8,2));
        Check(responder.IsRespondingToAlarm&&responder.CurrentState==ZeldaAiState.Search&&!responder.IsIgnoringPlayer,"Alarm overrides idle/ignore and starts investigation");
        Check(responder.AlarmDestination.x==8&&responder.AlarmDestination.y==2,"Alarm is the incident destination");
        responder.MoveTo(new Vector2(5,0));responder.Step(10);
        Check(responder.CurrentState==ZeldaAiState.Search,"Search time does not expire while traveling");
        responder.ReachIncident();responder.Step(4.1f);
        Check(responder.CurrentState==ZeldaAiState.Recovery&&responder.IsRespondingToAlarm,"Alarm remains retained throughout return");
        responder.RecoveryStep();Check(responder.PlannedDestination.x==0&&responder.PlannedDestination.y==0,"Alarm return targets initial home");
        responder.MoveTo(Vector2.zero);responder.RecoveryStep();
        Check(responder.CurrentState==ZeldaAiState.Idle&&!responder.IsRespondingToAlarm&&responder.gameObject.activeInHierarchy,"Idle releases alarm retention without force-disabling object");
        responder.RespondToAlarm(new Vector2(2,0));
        var seenPlayer=Make<ZeldaCharacterAiBase>(3,0,true);responder.Step();
        Check(responder.CurrentState==ZeldaAiState.Hostile&&responder.CurrentTarget==seenPlayer.GetComponent<ZeldaFourWayMover>(),"Seeing player in alarm search immediately causes hostility");
        responder.RespondToAlarm(new Vector2(9,0));
        Check(responder.CurrentState==ZeldaAiState.Search&&responder.CurrentTarget==null&&responder.AlarmDestination.x==9,"Repeat alarm restarts investigation from hostility");
        seenPlayer.RespondToAlarm(Vector2.zero);Check(!seenPlayer.IsRespondingToAlarm,"Never commandeer player");
        responder.IsStunned=true;responder.RespondToAlarm(new Vector2(6,0));
        Check(responder.PendingAlarm&&responder.AlarmDestination.x==9,"Stun defers dispatch without losing existing investigation");
        responder.IsStunned=false;responder.RespondToAlarm(new Vector2(6,0));
        Check(!responder.PendingAlarm&&responder.AlarmDestination.x==6,"Wakeup dispatches queued destination");
        responder.GetComponent<ZeldaCharacterData>().IsDead=true;responder.RespondToAlarm(new Vector2(7,0));
        Check(responder.AlarmDestination.x==6,"Dead responders do not receive new work");
        ZeldaRuntimeRegistry.Movers.Clear();
        var alarmMachine=Make<AutomatonCharacterAi>(1,2);alarmMachine.RespondToAlarm(new Vector2(5,2));
        Check(alarmMachine.CurrentState==ZeldaAiState.Search&&!alarmMachine.Stationary&&!alarmMachine.HostilityActivated,"Explicit alarm temporarily enables machine search without activation latch");
        alarmMachine.ReachIncident();alarmMachine.Step(4.1f);alarmMachine.RecoveryStep();
        Check(alarmMachine.CurrentState==ZeldaAiState.Idle&&!alarmMachine.IsRespondingToAlarm&&alarmMachine.Stationary,"Machine completes alarm and returns to normal dormant rules");
        var savedAlarm=new ZeldaCharacterAiBase.SaveState{alarmResponse=true,home=new Vector2(4,5),homeFacing=new Vector2(1,0),alarmPosition=new Vector2(10,0)};
        responder.GetComponent<ZeldaCharacterData>().IsDead=false;responder.RestoreAlarmResponseState(savedAlarm);
        responder.RecoverAfterPersistentSceneReturn(ZeldaAiState.Search);responder.RecoveryStep();
        Check(responder.PlannedDestination.x==4&&responder.PlannedDestination.y==5,"Scene return keeps authored home from saved dispatch");
        responder.RestoreAlarmResponseState(null);Check(!responder.IsRespondingToAlarm,"Old save clears alarm latch safely");
        return checks;
    }
}
'@
Add-Type -TypeDefinition $stub -WarningAction SilentlyContinue
Write-Output ("PASS: "+[AutomatonTests]::Run()+" production AI activation/perception/target/save/control-switch checks (physics/navigation stubbed).")
$prefab=Get-Content -Raw "$root/Assets/Prefabs/Level2NPCS/Automaton-level2.prefab"
if($prefab -notmatch 'hostilityActivated: 0' -or $prefab -notmatch 'visionAngle: 360' -or $prefab -notmatch 'peripheralVisionAngle: 360'){throw 'Prefab defaults incorrect'}
if($prefab -notmatch 'attackPrefab: \{fileID: 100000, guid: c7a3afec32db4e4d83d29bb18d2f601c'){throw 'Attack prefab missing'}
Write-Output 'PASS: prefab dormant, 360-degree vision and valid attack reference.'
