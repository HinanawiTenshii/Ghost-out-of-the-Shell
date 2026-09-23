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
public class ZeldaFourWayMover:Component {public int CancelCount;public void CancelAttackForStun(){CancelCount++;}}
public enum ZeldaAiState {Idle,Suspicious,Alert,Search,Recovery,Hostile,Stunned}
public static class ZeldaRuntimeRegistry {
    public static List<ZeldaFourWayMover> Movers=new List<ZeldaFourWayMover>();
    public static int GetGameplayScene(GameObject g)=>g.scene;
}
public class ZeldaCharacterAiBase:Component {
    protected ZeldaFourWayMover mover,targetMover;
    protected ZeldaCharacterData characterData;
    protected Body rb=new Body();
    protected Vector2 facingDirection=new Vector2(1,0),moveDirection,lastKnownTargetPosition;
    private float visionRadius=7,visionAngle=90,peripheralVisionRadius=1,peripheralVisionAngle=180;
    protected float regularHostileLostSightTimer,suspicionValue,warningTimer;
    private float royalCommandRemaining;
    protected object activePermissionArea,recognizedPermissionTarget,suspiciousCardboardBox,suspiciousCardboardBoxWearer,
        suspiciousGroundCardboardBox,inspectedCardboardBoxWearer,hostileCardboardBox;
    protected bool hostileUntilTargetDeath,retaliationHostile,regularHostile,hasCardboardBoxAttractionTarget,
        isInspectingCardboardBox,isTravelingToInvestigation;
    private ZeldaCharacterAiBase universalThreatTarget;
    public ZeldaAiState CurrentState{get;private set;}
    public ZeldaFourWayMover CurrentTarget=>targetMover;
    public virtual bool IsUniversalThreat=>false;
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
    protected virtual void Awake(){mover=GetComponent<ZeldaFourWayMover>();characterData=GetComponent<ZeldaCharacterData>();rb.position=transform.position;}
    protected virtual void OnValidate(){}
    protected virtual void OnDestroy(){}
    protected virtual void ChangeState(ZeldaAiState state,bool cue=true){CurrentState=state;}
    protected virtual bool CanSeePlayer(ZeldaFourWayMover m)=>false;
    public virtual void InvestigatePosition(Vector2 p){ChangeState(ZeldaAiState.Search);}
    public virtual void OnCharacterDamagedBy(ZeldaCharacterData a){ChangeState(ZeldaAiState.Hostile);}
    public virtual void OnPlayerAttackedCharacter(ZeldaCharacterData a,ZeldaCharacterData v){ChangeState(ZeldaAiState.Hostile);}
    public virtual void OnPossessionWitnessed(ZeldaFourWayMover a,ZeldaFourWayMover b){ChangeState(ZeldaAiState.Hostile);}
    public virtual bool OnPlayerDestroyedDoor(ZeldaCharacterData a){ChangeState(ZeldaAiState.Hostile);return true;}
    public virtual bool CanReceiveRoyalCommand(ZeldaFourWayMover p)=>true;
    public virtual void ReceiveRoyalCommand(){ChangeState(ZeldaAiState.Recovery);}
    public class SaveState {public ZeldaAiState state;public bool automatonHostilityActivated;}
    public virtual SaveState CaptureSaveState()=>new SaveState{state=CurrentState};
    public virtual void ApplySaveState(SaveState s){ChangeState(s.state,false);}
    public virtual void RecoverAfterPersistentSceneReturn(ZeldaAiState state){ChangeState(ZeldaAiState.Recovery);}
    protected void ResetPermissionResponse(){targetMover=null;moveDirection=Vector2.zero;ChangeState(ZeldaAiState.Idle);}
    protected virtual void UpdateTargetAndState(float dt){TryRespondToUniversalThreat();}
    public void Initialize(){Awake();ChangeState(ZeldaAiState.Idle);}
    public void Step(){UpdateTargetAndState(.02f);}
    public void Request(ZeldaAiState state){ChangeState(state);}
'@
$colorHook=[regex]::Match($ai,'protected virtual Color VisionVisualColor => visionColor;').Value
if(!$colorHook){throw 'Missing production vision color hook'}
$stub+=$colorHook
foreach($signature in @('    private void CreateVisionVisual()', '    private void UpdateVisionMesh()')){
    if(!(Extract $ai $signature).Contains('visionMaterial.color = VisionVisualColor;')){throw 'Vision material bypasses state color'}
}
foreach($sig in @('    protected bool CanSeeCharacter(', '    private bool TryRespondToUniversalThreat()', '    protected bool CanSeeWorldPoint(', '    protected void ConfigureOmnidirectionalVision()', '    protected void BeginTemporaryHostility(')) {
    $stub+=Extract $ai $sig
}
$stub+="}"
$stub+=$automaton.Replace('using System.Collections.Generic;','').Replace('using UnityEngine;','')
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
