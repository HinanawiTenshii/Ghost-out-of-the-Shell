$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$data=Get-Content -Raw "$root/Assets/Scripts/Characters/ZeldaCharacterData.cs"
$ai=Get-Content -Raw "$root/Assets/Scripts/AI/ZeldaCharacterAiBase.cs"
function Extract($s,$sig){
    $start=$s.IndexOf($sig);if($start -lt 0){throw "Missing $sig"}
    $b=$s.IndexOf('{',$start);$e=$b+1;$depth=1
    while($depth){if($s[$e] -eq '{'){$depth++};if($s[$e] -eq '}'){$depth--};$e++}
    $s.Substring($start,$e-$start)
}
# Production damage, witness dispatch, response, visibility and hostility methods.
# Unity physics, presentation, navigation and registry lifetime are replaced by test doubles.
$code=@'
#pragma warning disable 0414
using System;
using System.Collections.Generic;
public struct Vector2 {
 public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}
 public float sqrMagnitude=>x*x+y*y;public float magnitude=>(float)Math.Sqrt(sqrMagnitude);
 public Vector2 normalized=>magnitude>0?new Vector2(x/magnitude,y/magnitude):new Vector2();
 public static Vector2 operator -(Vector2 a,Vector2 b)=>new Vector2(a.x-b.x,a.y-b.y);
 public static float Angle(Vector2 a,Vector2 b)=>a.magnitude*b.magnitude==0?0:(float)(Math.Acos(Math.Max(-1,Math.Min(1,(a.x*b.x+a.y*b.y)/(a.magnitude*b.magnitude))))*180/Math.PI);
}
public class Transform {public Vector2 position;}
public class Body {public Vector2 position;}
public class GameObject {
 public string scene="level";public bool active=true,destroyed;public Transform transform=new Transform();public List<object> components=new List<object>();
 public T GetComponent<T>() where T:class {foreach(var c in components)if(c is T)return c as T;return null;}
}
public class Component {
 public GameObject gameObject;public bool enabled=true;public bool isActiveAndEnabled=>enabled&&gameObject.active&&!gameObject.destroyed;
 public Transform transform=>gameObject.transform;public T GetComponent<T>() where T:class=>gameObject.GetComponent<T>();
}
public class CardboardBoxWearState {public bool blocks;public bool TryBlockAttack(ZeldaCharacterData d)=>blocks;}
public class ZeldaFourWayMover:Component {public CardboardBoxWearState ActiveCardboardBox;public void InterruptPossessionByDamage(){}}
public static class Mathf {public static int Max(int a,int b)=>Math.Max(a,b);}
public enum SendMessageOptions {DontRequireReceiver}
public static class Debug {public static void Log(string s,object o){}}
public static class DominoSkillRuntime {public static int shares;public static void ShareDamage(ZeldaCharacterData v,int d,ZeldaCharacterData a){shares++;}}
public static class ZeldaRuntimeRegistry {
 public static List<ZeldaCharacterAiBase> AiCharacters=new List<ZeldaCharacterAiBase>();public static ZeldaFourWayMover controlled;
 public static ZeldaFourWayMover GetControlledMover()=>controlled;
 public static string GetGameplayScene(GameObject g)=>g.scene=="DontDestroyOnLoad"?"level":g.scene;
}
public enum ZeldaAiState {Idle,Hostile}
public class ZeldaCharacterData:Component {
 public bool isDead,IsGhostLike;public bool IsDead=>isDead;public bool CanBeDetectedByAi=true;
 public int currentHealth=6,health=6;public string name="victim";public Action ValuesChanged;
 private float damageFeedbackTimer,damageFeedbackDuration=.18f;public int damageSounds,deathCount,retaliations;
 private void CaptureDamageFeedbackBaseVisual(){}private void PlayDamageSound(){damageSounds++;}
 private void BroadcastMessage(string m,ZeldaCharacterData a,SendMessageOptions o){retaliations++;}
 private void Die(){isDead=true;deathCount++;gameObject.destroyed=true;var ai=GetComponent<ZeldaCharacterAiBase>();if(ai!=null)ZeldaRuntimeRegistry.AiCharacters.Remove(ai);}
 public void Damage(int d,ZeldaCharacterData a,bool shared=false)=>ApplyDamage(d,a,shared);
'@
$code+=Extract $data '    private void ApplyDamage('
$code+=@'
}
public class ZeldaCharacterAiBase:Component {
 public ZeldaCharacterData characterData;public ZeldaFourWayMover mover,targetMover;
 public bool IsIgnoringPlayer,IsStunned,possessionLocked,blocked,regularHostile,hostileUntilTargetDeath;
 public int responses,damageMultiplier=1;public ZeldaAiState currentState;
 public Body rb=new Body();public Vector2 facingDirection=new Vector2(1,0);
 public float visionRadius=7,visionAngle=90,peripheralVisionRadius=1,peripheralVisionAngle=180;
 public int ResolveIncomingAttackDamage(int d)=>d*damageMultiplier;
 private bool IsRecognizedPermissionTarget(ZeldaFourWayMover m)=>false;
 private bool IsVisionBlocked(Vector2 a,Vector2 d,float distance,Transform t)=>blocked;
 private void BeginTemporaryHostility(ZeldaFourWayMover p,bool ghost){responses++;targetMover=p;currentState=ZeldaAiState.Hostile;regularHostile=true;}
'@
$signatures = @('    public static void NotifyPlayerAttackWitnesses(', '    public virtual void OnPlayerAttackedCharacter(', '    protected virtual bool CanSeePlayer(', '    public bool IsActivelyHostileTo(')
foreach($sig in $signatures){$code+=Extract $ai $sig}
$code+='}'
$code+=@'
public static class WitnessedAttackTests {
 static int checks;static void Check(bool v,string s){if(!v)throw new Exception(s);checks++;}
 static ZeldaCharacterData Make(bool player=false){var g=new GameObject();var d=new ZeldaCharacterData{gameObject=g};var m=new ZeldaFourWayMover{gameObject=g,enabled=player};var a=new ZeldaCharacterAiBase{gameObject=g,characterData=d,mover=m};g.components.Add(d);g.components.Add(m);g.components.Add(a);ZeldaRuntimeRegistry.AiCharacters.Add(a);if(player)ZeldaRuntimeRegistry.controlled=m;return d;}
 static ZeldaCharacterData attacker,victim;static ZeldaCharacterAiBase witness;
 static void Setup(){ZeldaRuntimeRegistry.AiCharacters.Clear();attacker=Make(true);attacker.transform.position=new Vector2(2,0);victim=Make();victim.transform.position=new Vector2(2.5f,0);witness=Make().GetComponent<ZeldaCharacterAiBase>();}
 public static int Run(){
  Setup();victim.Damage(6,attacker);Check(victim.IsDead&&witness.responses==1&&witness.targetMover==attacker.GetComponent<ZeldaFourWayMover>(),"Lethal hit immediately alerts visible bystander");
  Check(!ZeldaRuntimeRegistry.AiCharacters.Contains(victim.GetComponent<ZeldaCharacterAiBase>())&&witness.IsActivelyHostileTo(attacker.GetComponent<ZeldaFourWayMover>()),"Witness survives removal of victim from registry");
  Check(victim.damageSounds==0&&victim.deathCount==1,"Fatal hit still has one death and no hurt sound");
  Setup();victim.GetComponent<ZeldaCharacterAiBase>().damageMultiplier=3;victim.Damage(2,attacker);Check(victim.IsDead&&witness.responses==1,"Surprise-hit lethal multiplier");
  Setup();victim.Damage(1,attacker);Check(!victim.IsDead&&witness.responses==1&&victim.damageSounds==1&&victim.retaliations==1,"Nonlethal hit preserves damage and response");
  victim.Damage(1,attacker);Check(witness.responses==1,"Repeated hits do not reset witness attack cycle");
  Setup();victim.Damage(0,attacker);victim.Damage(-1,attacker);Check(witness.responses==0,"Invalid damage has no hostility");
  Setup();victim.GetComponent<ZeldaFourWayMover>().ActiveCardboardBox=new CardboardBoxWearState{blocks=true};victim.Damage(10,attacker);Check(witness.responses==0&&!victim.IsDead,"Blocked attack has no damage witness event");
  Setup();victim.Damage(10,null);Check(witness.responses==0,"No attributed attacker");
  Setup();attacker.GetComponent<ZeldaFourWayMover>().enabled=false;victim.Damage(10,attacker);Check(witness.responses==0,"NPC attack not blamed on player");
  Setup();ZeldaRuntimeRegistry.controlled=null;victim.Damage(10,attacker);Check(witness.responses==0,"Enabled mover is not enough without actual player ownership");
  Setup();attacker.IsGhostLike=true;victim.Damage(10,attacker);Check(witness.responses==0,"Ghost hostility exemption preserved");
  Setup();attacker.isDead=true;victim.Damage(10,attacker);Check(witness.responses==0,"Dead source not targeted");
  Setup();attacker.CanBeDetectedByAi=false;victim.Damage(10,attacker);Check(witness.responses==0,"Invisible player remains undetected");
  Setup();attacker.GetComponent<ZeldaFourWayMover>().ActiveCardboardBox=new CardboardBoxWearState();victim.Damage(10,attacker);Check(witness.responses==0,"Existing box concealment rule preserved");
  Setup();witness.blocked=true;victim.Damage(10,attacker);Check(witness.responses==0,"Walls block witnessing");
  Setup();attacker.transform.position=new Vector2(20,0);victim.Damage(10,attacker);Check(witness.responses==0,"Outside radius");
  Setup();attacker.transform.position=new Vector2(-3,0);victim.Damage(10,attacker);Check(witness.responses==0,"Outside vision cone");
  Setup();witness.gameObject.scene="other";victim.Damage(10,attacker);Check(witness.responses==0,"Other scene witness excluded");
  Setup();victim.gameObject.scene="other";victim.Damage(10,attacker);Check(witness.responses==0,"Cross-scene victim excluded");
  Setup();attacker.gameObject.scene="DontDestroyOnLoad";victim.Damage(10,attacker);Check(witness.responses==1,"Persistent player body maps to gameplay scene");
  foreach(var guard in new[]{"stun","ignore","lock","disabled","dead","player"}){
   Setup();if(guard=="stun")witness.IsStunned=true;if(guard=="ignore")witness.IsIgnoringPlayer=true;if(guard=="lock")witness.possessionLocked=true;if(guard=="disabled")witness.enabled=false;if(guard=="dead")witness.characterData.isDead=true;if(guard=="player")witness.mover.enabled=true;
   victim.Damage(10,attacker);Check(witness.responses==0,"Excluded witness: "+guard);
  }
  Setup();victim.Damage(10,attacker,true);Check(witness.responses==0,"Domino damage does not dispatch duplicate direct-attack evidence");
  Setup();attacker.Damage(10,attacker);Check(witness.responses==0,"Self damage not treated as attacking another character");
  Setup();victim.isDead=true;victim.Damage(10,attacker);Check(witness.responses==0,"Already dead victim cannot dispatch");
  return checks;
 }
}
'@
Add-Type -TypeDefinition $code
Write-Output "PASS: $([WitnessedAttackTests]::Run()) production damage/witness/perception regressions (Unity physics/presentation stubbed)."
$apply=Extract $data '    private void ApplyDamage('
if($apply.IndexOf('NotifyPlayerAttackWitnesses') -gt $apply.IndexOf('DominoSkillRuntime.ShareDamage') -or $apply.IndexOf('NotifyPlayerAttackWitnesses') -gt $apply.IndexOf('Die();')){throw 'Witness dispatch must precede direct and linked deaths'}
Write-Output 'PASS: witness notification occurs before victim death and linked damage.'
