$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$source=Get-Content -Raw "$root/Assets/Scripts/Interaction/ZeldaInteractionArbiter.cs"
$fixture=@'
namespace UnityEngine {
 public class RuntimeInitializeOnLoadMethod:System.Attribute {public RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType t){}}
 public enum RuntimeInitializeLoadType {SubsystemRegistration,BeforeSceneLoad}
 public class DefaultExecutionOrder:System.Attribute {public DefaultExecutionOrder(int i){}}
 public class Object {public static void DontDestroyOnLoad(object o){}}
 public class GameObject {
  public bool activeInHierarchy=true;public Transform transform=new Transform();
  public GameObject(string name=""){}public T AddComponent<T>() where T:Component,new(){return new T{gameObject=this};}
 }
 public class Component:Object {
  public GameObject gameObject=new GameObject();public int Id;
  public Transform transform {get{return gameObject.transform;}}
  public int GetInstanceID(){return Id;}
 }
 public class MonoBehaviour:Component {}
 public class Transform {public Vector2 position;}
 public struct Vector2 {
  public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}
  public float sqrMagnitude {get{return x*x+y*y;}}
  public Vector2 normalized {get{float m=(float)Math.Sqrt(sqrMagnitude);return m==0?new Vector2():new Vector2(x/m,y/m);}}
  public static Vector2 operator -(Vector2 a,Vector2 b){return new Vector2(a.x-b.x,a.y-b.y);}
  public static float Dot(Vector2 a,Vector2 b){return a.x*b.x+a.y*b.y;}
 }
 public static class Mathf {public static float Abs(float n){return Math.Abs(n);}}
 public static class Time {public static int frameCount;}
 public enum KeyCode {None,E,F}
 public static class Input {public static HashSet<KeyCode> keys=new HashSet<KeyCode>();public static bool GetKeyDown(KeyCode k){return keys.Contains(k);}}
}
public class ZeldaCharacterData {public bool IsGhostForm;}
public class ZeldaFourWayMover:UnityEngine.MonoBehaviour {
 public bool isActiveAndEnabled=true;public UnityEngine.Vector2 FacingDirection=new UnityEngine.Vector2(1,0);
 public ZeldaCharacterData data=new ZeldaCharacterData();public T GetComponent<T>() where T:class{return data as T;}
}
public class CharacterDialogue:UnityEngine.MonoBehaviour {}
// Physics is covered by InteractionObstructionSmoke; keep this suite focused on E/F arbitration.
public static class ZeldaInteractionObstruction {
 public static bool HasClearPath(UnityEngine.Component owner,ZeldaFourWayMover actor,UnityEngine.Vector2 point){return true;}
}
public static class ClockworkPuppetRuntime {public static bool BlocksCharacterInput;}
public static class SignpostInteraction {public static bool BlocksInput;}
public static class CharacterDialogueWindow {public static bool BlocksInput;}
public static class DialoguePossessionTests {
 static int checks;static void Check(bool b,string m){checks++;if(!b)throw new Exception(m);}
 static ZeldaFourWayMover player,npc;static CharacterDialogue dialogue;static bool e,f;static int actions;
 static void Setup(int dialogueId,int npcId){
  typeof(ZeldaInteractionArbiter).GetMethod("ResetStatics",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,null);
  UnityEngine.Time.frameCount++;UnityEngine.Input.keys.Clear();CharacterDialogueWindow.BlocksInput=false;
  player=new ZeldaFourWayMover{Id=100};npc=new ZeldaFourWayMover{Id=npcId};
  npc.transform.position=new UnityEngine.Vector2(1,0);dialogue=new CharacterDialogue{Id=dialogueId,gameObject=npc.gameObject};
  e=f=false;actions=0;
 }
 static void Offer(){
  ZeldaInteractionArbiter.OfferInteraction(dialogue,player,UnityEngine.KeyCode.E,npc.transform.position,v=>e=v);
  ZeldaInteractionArbiter.OfferInteraction(npc,player,UnityEngine.KeyCode.F,npc.transform.position,v=>f=v);
 }
 static void Submit(){ZeldaInteractionArbiter.Submit(dialogue,player,UnityEngine.KeyCode.E,npc.transform.position,()=>actions++);}
 public static int Run(){
  foreach(bool fFirst in new[]{true,false}){
   Setup(fFirst?20:1,fFirst?1:20);Offer();Submit();UnityEngine.Input.keys.Add(UnityEngine.KeyCode.E);
   ZeldaInteractionArbiter.ResolveCurrentFrame();
   Check(e&&f,"E dialogue and F possession both selected regardless of component ID");
   Check(actions==1,"E performs dialogue even if F won original tie");
   Check(ZeldaInteractionArbiter.IsSelected(npc,player,UnityEngine.KeyCode.F),"F remains usable");
   Check(ZeldaInteractionArbiter.IsSelected(dialogue,player,UnityEngine.KeyCode.E),"E is selectable");
   UnityEngine.Time.frameCount++;UnityEngine.Input.keys.Clear();UnityEngine.Input.keys.Add(UnityEngine.KeyCode.F);Offer();Submit();
   ZeldaInteractionArbiter.ResolveCurrentFrame();Check(e&&f&&actions==1,"F does not accidentally invoke E");
   CharacterDialogueWindow.BlocksInput=true;UnityEngine.Time.frameCount++;Offer();ZeldaInteractionArbiter.ResolveCurrentFrame();
   Check(!e&&!f&&!ZeldaInteractionArbiter.IsSelected(npc,player,UnityEngine.KeyCode.F),"Open dialogue suppresses possession");
   Check(!ZeldaInteractionArbiter.IsSelected(dialogue,player,UnityEngine.KeyCode.E),"Modal clears companion selection");
  }
  Setup(20,1);player.data.IsGhostForm=true;Offer();Submit();UnityEngine.Input.keys.Add(UnityEngine.KeyCode.E);ZeldaInteractionArbiter.ResolveCurrentFrame();
  Check(!e&&f&&actions==0,"Ghost keeps possession but cannot converse");
  Setup(20,1);Offer();var item=new UnityEngine.Component{Id=30};item.transform.position=new UnityEngine.Vector2(.5f,0);bool itemSelected=false;
  ZeldaInteractionArbiter.OfferInteraction(item,player,UnityEngine.KeyCode.E,item.transform.position,v=>itemSelected=v);
  Submit();UnityEngine.Input.keys.Add(UnityEngine.KeyCode.E);ZeldaInteractionArbiter.ResolveCurrentFrame();
  Check(itemSelected&&!e&&!f&&actions==0,"Closer ordinary object retains priority");
  Setup(20,1);dialogue.gameObject=new UnityEngine.GameObject();dialogue.transform.position=new UnityEngine.Vector2(1,0);
  Offer();Submit();UnityEngine.Input.keys.Add(UnityEngine.KeyCode.E);ZeldaInteractionArbiter.ResolveCurrentFrame();
  Check(!e&&f&&actions==0,"Different NPC at same coordinates does not share interaction");
  Setup(20,1);npc.transform.position=new UnityEngine.Vector2(-1,0);Offer();Submit();UnityEngine.Input.keys.Add(UnityEngine.KeyCode.E);ZeldaInteractionArbiter.ResolveCurrentFrame();
  Check(!e&&!f&&actions==0,"Targets behind player remain unavailable");
  Setup(20,1);Offer();ZeldaInteractionArbiter.ResolveCurrentFrame();UnityEngine.Time.frameCount++;
  ZeldaInteractionArbiter.OfferInteraction(npc,player,UnityEngine.KeyCode.F,npc.transform.position,v=>f=v);ZeldaInteractionArbiter.ResolveCurrentFrame();
  Check(!ZeldaInteractionArbiter.IsSelected(dialogue,player,UnityEngine.KeyCode.E),"Missing/stale dialogue offer loses companion selection");
  Setup(20,1);Offer();player.data.IsGhostForm=true;ZeldaInteractionArbiter.ResolveCurrentFrame();
  Check(!e&&f,"Changing to ghost before resolution rejects stale dialogue offer");
  return checks;
 }
}
'@
Add-Type -TypeDefinition ($source+"`n"+$fixture)
Write-Output "PASS: $([DialoguePossessionTests]::Run()) production E/F arbitration assertions."
