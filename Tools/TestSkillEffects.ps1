# Execute production targeting/status methods with minimal Unity data stubs.
# This does not replace a Unity play-mode/rendering test.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
function Read-Source($relative) { Get-Content -Raw (Join-Path $root $relative) }
function Get-Method([string]$source, [string]$signature) {
    $start = $source.IndexOf($signature)
    if ($start -lt 0) { throw "Missing method $signature" }
    $brace = $source.IndexOf('{', $start); $depth = 1; $end = $brace + 1
    while ($depth -gt 0) {
        if ($source[$end] -eq '{') { $depth++ }
        if ($source[$end] -eq '}') { $depth-- }
        $end++
    }
    $source.Substring($start, $end-$start)
}
$registry = Read-Source 'Assets/Scripts/Characters/ZeldaRuntimeRegistry.cs'
$behemoth = Read-Source 'Assets/Scripts/Characters/BehemothZeldaCharacterData.cs'
$ai = Read-Source 'Assets/Scripts/AI/ZeldaCharacterAiBase.cs'
$data = Read-Source 'Assets/Scripts/Characters/ZeldaCharacterData.cs'
$domino = Read-Source 'Assets/Scripts/Characters/DominoSkillRuntime.cs'
$stubs = @'
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace UnityEngine.SceneManagement {
    public struct Scene {
        public string name; public Scene(string n) {name=n;}
        public bool IsValid() {return name!=null;}
        public static bool operator ==(Scene a,Scene b) {return a.name==b.name;}
        public static bool operator !=(Scene a,Scene b) {return !(a==b);}
        public override bool Equals(object o) {return o is Scene && this==(Scene)o;}
        public override int GetHashCode() {return name==null?0:name.GetHashCode();}
    }
    public static class SceneManager {
        public static Scene active=new Scene("Level2");
        public static Scene GetActiveScene() {return active;}
        public static void MoveGameObjectToScene(GameObject o,Scene s) {o.scene=s;}
    }
}
namespace UnityEngine {
    public struct Vector2 {
        public float x,y; public Vector2(float a,float b) {x=a;y=b;}
        public float sqrMagnitude {get{return x*x+y*y;}}
        public static Vector2 zero {get{return new Vector2();}}
        public static Vector2 operator -(Vector2 a,Vector2 b) {return new Vector2(a.x-b.x,a.y-b.y);}
    }
    public class Transform {public Vector2 position;}
    public class GameObject {
        public Scene scene; public bool active=true; public Transform transform=new Transform();
        public List<Component> components=new List<Component>();
        public T GetComponent<T>() where T:class {
            foreach(var c in components) if(c is T) return c as T; return null;
        }
        public T Add<T>() where T:Component,new() {var c=new T();c.gameObject=this;components.Add(c);return c;}
    }
    public class Component {
        static int nextId; int id=++nextId;
        public GameObject gameObject; public Transform transform {get{return gameObject.transform;}}
        public bool enabled=true; public bool isActiveAndEnabled {get{return enabled&&gameObject.active;}}
        public T GetComponent<T>() where T:class {return gameObject.GetComponent<T>();}
        public int GetInstanceID(){return id;}
    }
    public class Rigidbody2D {public Vector2 velocity; public float angularVelocity;}
    public static class Time {public static float deltaTime;}
    public static class Mathf {
        public static float Max(float a,float b){return Math.Max(a,b);}
        public static int Max(int a,int b){return Math.Max(a,b);}
        public static float Min(float a,float b){return Math.Min(a,b);}
        public static int Min(int a,int b){return Math.Min(a,b);}
        public static int FloorToInt(float v){return (int)Math.Floor(v);}
    }
}
public class ZeldaFourWayMover:Component {public int cancellations; public void CancelAttackForStun(){cancellations++;}}
public enum ZeldaAiState {Idle,Alert,Hostile,Recovering,Stunned}
public static class DocumentReader {public static bool IsInputBlocked;}
public static class ClockworkPuppetRuntime {public static bool BlocksCharacterInput;}
public class PlayerGrowthAttributes {
    public static PlayerGrowthAttributes Instance=new PlayerGrowthAttributes();
    public int RoyalCommandLevel,DominoLevel,FearRoarLevel,GhostFormLevel;
    public HashSet<string> skills=new HashSet<string>();
    public bool HasSkill(string id){return skills.Contains(id);}
}
'@
$getScene = Get-Method $registry 'public static Scene GetGameplayScene('
$roarScan = (Get-Method $behemoth 'private void AffectCharacters()').Replace('private void','public void')
$init = Get-Method $behemoth 'public void Initialize('
# Use the actual scene/radius/duration initialization; omit only visual renderers.
$init = $init.Substring(0,$init.IndexOf('        var visual')) + "    }"
$canCommand = Get-Method $ai 'public bool CanReceiveRoyalCommand('
$command = Get-Method $ai 'public void ReceiveRoyalCommand()'
$stun = Get-Method $ai 'public void StunForDuration('
$castCommand = Get-Method $data 'public bool TryUseRoyalCommand()'
$castCombat = Get-Method $data 'public bool TryUseCombatExpertise()'
$tick = (Get-Method $data 'protected virtual void Update()').Replace('protected virtual void Update()', 'public void Tick()')
$findTargets = Get-Method $domino 'public static void FindTargets('
$code = $stubs + @"
public static class ZeldaRuntimeRegistry {
    public static List<ZeldaCharacterAiBase> AiCharacters=new List<ZeldaCharacterAiBase>();
    $getScene
}
public class ZeldaCharacterAiBase:Component {
    public ZeldaCharacterData characterData; public ZeldaFourWayMover mover,targetMover;
    public ZeldaAiState currentState,stateBeforeStun; public float stunRemaining,royalCommandRemaining;
    public Vector2 moveDirection; public Rigidbody2D rb=new Rigidbody2D();
    public bool IsStunned {get{return currentState==ZeldaAiState.Stunned;}}
    void UpdateCharacterVisual(){} void UpdateStateIndicator(){} void UpdateRoyalCommandBar(){}
    void BeginRecovery(){currentState=ZeldaAiState.Recovering;}
    $canCommand
    $command
    $stun
}
public class ZeldaCharacterData:Component {
    public bool isDead,IsGhostLike; public bool IsDead {get{return isDead;}}
    public string NativeCharacterSkillId; public int energy=10,health=10,currentHealth=5;
    public float combatExpertiseRemaining; public bool combatExpertiseRegeneration;
    public const float CombatExpertiseDuration=3f;
    public bool IsCombatExpertiseActive {get{return !isDead&&combatExpertiseRemaining>0;}}
    public Action ValuesChanged; void UpdateDamageFeedback(float dt){}
    bool IsPlayerControlled(){return GetComponent<ZeldaFourWayMover>().isActiveAndEnabled;}
    public bool TrySpendPossessionEnergy(int cost){if(energy<cost)return false;energy-=cost;return true;}
    $castCommand
    $castCombat
    $tick
}
public class GhostZeldaCharacterData:ZeldaCharacterData {}
public class FearRoarArea:Component {
    public float radius,stunDuration;
    readonly HashSet<ZeldaCharacterAiBase> affected=new HashSet<ZeldaCharacterAiBase>();
    $init
    $roarScan
}
public static class DominoSkillRuntime {
    public static int TargetLimit {get{return PlayerGrowthAttributes.Instance.DominoLevel>=2?3:2;}}
    public static float Range {get{return PlayerGrowthAttributes.Instance.DominoLevel>=3?4:3;}}
    $findTargets
}
"@
$code += @'
public static class SkillChecks {
    static int checks;
    static void Check(bool ok,string msg){checks++;if(!ok)throw new Exception(msg);}
    static ZeldaCharacterAiBase Actor(Scene scene,float x,bool player=false){
        var go=new GameObject{scene=scene}; go.transform.position=new Vector2(x,0);
        var d=go.Add<ZeldaCharacterData>(); var m=go.Add<ZeldaFourWayMover>();m.enabled=player;
        var ai=go.Add<ZeldaCharacterAiBase>();ai.characterData=d;ai.mover=m;ai.enabled=!player;
        ZeldaRuntimeRegistry.AiCharacters.Add(ai);return ai;
    }
    public static int Run(){
        var current=SceneManager.active; var other=new Scene("OtherLevel");var persistent=new Scene("DontDestroyOnLoad");
        Check(!ZeldaRuntimeRegistry.GetGameplayScene(null).IsValid(),"Null scene");
        foreach(var sourceScene in new[]{current,persistent}) {
            ZeldaRuntimeRegistry.AiCharacters.Clear();
            var caster=Actor(sourceScene,0,true);
            Check(ZeldaRuntimeRegistry.GetGameplayScene(caster.gameObject)==current,"Persistent caster resolves to level");
            var near=Actor(current,2); var edge=Actor(current,3); var far=Actor(current,3.2f);
            var outside=Actor(current,5); var elsewhere=Actor(other,1);var dead=Actor(current,1);dead.characterData.isDead=true;
            var controlled=Actor(current,1,true);var already=Actor(current,1);already.StunForDuration(1);
            var sleeping=Actor(current,1);sleeping.gameObject.active=false;
            var area=new GameObject().Add<FearRoarArea>();area.Initialize(caster.characterData,1);area.AffectCharacters();
            Check(area.gameObject.scene==current,"Roar must belong to level, not persistence scene");
            Check(near.IsStunned&&edge.IsStunned&&near.stunRemaining==3,"Level1 roar hits radius edge");
            Check(!far.IsStunned&&!outside.IsStunned&&!elsewhere.IsStunned,"Roar distance and scene filters");
            Check(!dead.IsStunned&&!controlled.IsStunned&&!caster.IsStunned&&!sleeping.IsStunned,"Roar excludes dead/player/inactive");
            Check(already.stunRemaining==1,"Roar must not refresh an existing stun");
            near.stunRemaining=.4f;area.AffectCharacters();Check(near.stunRemaining==.4f,"No repeated stun in one field");
            far.transform.position=new Vector2(2.9f,0);area.AffectCharacters();Check(far.IsStunned,"Entrant caught during field lifetime");
            var upgraded=Actor(current,3.5f);var area3=new GameObject().Add<FearRoarArea>();area3.Initialize(caster.characterData,3);area3.AffectCharacters();
            Check(upgraded.IsStunned&&upgraded.stunRemaining==5,"Level3 radius and five-second stun");
            Check(near.mover.cancellations==1,"Stun cancels active attack only once");
            var area2=new GameObject().Add<FearRoarArea>();area2.Initialize(caster.characterData,2);
            Check(area2.radius==3.5f&&area2.stunDuration==3,"Level2 radius, normal stun");

            ZeldaRuntimeRegistry.AiCharacters.Clear();ZeldaRuntimeRegistry.AiCharacters.Add(caster);
            var targets=new List<ZeldaCharacterAiBase>();
            for(int i=0;i<6;i++){var a=Actor(current,i+1);a.currentState=ZeldaAiState.Hostile;a.targetMover=caster.mover;targets.Add(a);}
            var wrongScene=Actor(other,.1f);wrongScene.currentState=ZeldaAiState.Hostile;wrongScene.targetMover=caster.mover;
            caster.characterData.NativeCharacterSkillId="royal_command";PlayerGrowthAttributes.Instance.RoyalCommandLevel=1;
            Check(caster.characterData.TryUseRoyalCommand()&&caster.characterData.energy==9,"Command casts and spends one energy");
            for(int i=0;i<6;i++)Check(targets[i].royalCommandRemaining==(i<3?5:0),"Nearest three command targets");
            Check(wrongScene.royalCommandRemaining==0,"Command excludes other loaded scene");
            foreach(var a in targets){a.currentState=ZeldaAiState.Alert;a.royalCommandRemaining=0;}
            PlayerGrowthAttributes.Instance.RoyalCommandLevel=2;
            Check(caster.characterData.TryUseRoyalCommand(),"Command upgrade casts");
            for(int i=0;i<6;i++)Check(targets[i].royalCommandRemaining==(i<5?5:0),"Upgraded command limit");
            foreach(var a in targets)a.currentState=ZeldaAiState.Idle;
            int energy=caster.characterData.energy;
            Check(!caster.characterData.TryUseRoyalCommand()&&caster.characterData.energy==energy,"Empty command does not spend energy");

            var pool=new List<ZeldaFourWayMover>{caster.mover,targets[3].mover,wrongScene.mover,targets[1].mover,targets[0].mover,targets[2].mover};
            var selected=new List<ZeldaFourWayMover>();PlayerGrowthAttributes.Instance.DominoLevel=1;
            DominoSkillRuntime.FindTargets(caster.mover,pool,selected);
            Check(selected.Count==2&&selected[0]==targets[0].mover&&selected[1]==targets[1].mover,"Domino selects nearest two after travel");
            PlayerGrowthAttributes.Instance.DominoLevel=3;targets[1].characterData.isDead=true;
            DominoSkillRuntime.FindTargets(caster.mover,pool,selected);
            Check(selected.Count==3&&selected[2]==targets[3].mover&&!selected.Contains(targets[1].mover),"Domino rank3 range four, excludes dead");
            caster.characterData.NativeCharacterSkillId="combat_expertise";caster.characterData.energy=2;
            PlayerGrowthAttributes.Instance.skills.Add("combat_expertise_2");
            Check(caster.characterData.TryUseCombatExpertise()&&caster.characterData.energy==0,"Combat expertise native cast");
            Check(!caster.characterData.TryUseCombatExpertise(),"Combat expertise cannot stack");
            Time.deltaTime=1;for(int i=0;i<3;i++)caster.characterData.Tick();
            Check(caster.characterData.currentHealth==8&&!caster.characterData.IsCombatExpertiseActive,"Combat rank2 heals three and expires");
            Check(!caster.characterData.TryUseCombatExpertise(),"Not enough energy fails");
        }
        return checks;
    }
}
'@
Add-Type -TypeDefinition $code
$count = [SkillChecks]::Run()
# Verify consumers use the common resolver, including the preview and visual effects.
foreach ($file in @('BehemothZeldaCharacterData.cs','ZeldaCharacterData.cs','DominoSkillRuntime.cs','PossessionTransferParticles.cs')) {
    $s = Read-Source "Assets/Scripts/Characters/$file"
    if ($s -match '(?:mover|ai|target|source|user)\.gameObject\.scene\s*[!=]=') {throw "Raw actor scene comparison: $file"}
}
$streaming = Read-Source 'Assets/Scripts/Camera/CameraVisionObjectStreaming.cs'
if ((Get-Method $streaming 'private static bool HasRuntimeStreamingExemption(').Contains('IsRemoteControlActive')) {throw 'Marks must remain streaming-exempt without puppet control'}
if (-not $data.Contains('(AttackPower + GetControlledGrowthAttackPower()) * (IsCombatExpertiseActive ? 2 : 1)')) {throw 'Missing combat power multiplier'}
$mover = Read-Source 'Assets/Scripts/Characters/ZeldaFourWayMover.cs'
if (-not $mover.Contains('characterData.FinalAttackPower,')) {throw 'Attacks must consume buffed power'}
$ghost = Read-Source 'Assets/Scripts/Characters/GhostFormRuntime.cs'
foreach($rule in @('SkillLevel >= 2 ? 7f : 5f','SkillLevel >= 3 ? 1 : 2','character.TrySpendPossessionEnergy(EnergyCost)','character.GhostForm = effect;')) {
    if (-not $ghost.Contains($rule)) {throw "Missing ghost rule: $rule"}
}
Write-Output "PASS: $count behavioral assertions (normal + persistent caster), skill scene consumers, mark retention, combat/ghost wiring."
