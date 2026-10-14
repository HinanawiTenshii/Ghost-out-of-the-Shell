$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$source = Get-Content -Raw -Encoding UTF8 "$root/Assets/Scripts/Interaction/CharacterDialogue.cs"
$window = Get-Content -Raw -Encoding UTF8 "$root/Assets/Scripts/Interaction/CharacterDialogueWindow.cs"
function Method([string]$code, [string]$signature) {
    $start=$code.IndexOf($signature); if($start -lt 0){throw "Missing $signature"}
    $cursor=$code.IndexOf('{',$start);$end=$cursor+1;$depth=1
    while($depth -gt 0){if($code[$end] -eq '{'){$depth++};if($code[$end] -eq '}'){$depth--};$end++}
    $code.Substring($start,$end-$start)
}
$fixture = @'
using System;
public class Time { public static int frameCount=5; public static float timeScale; }
public enum CursorLockMode { None, Locked }
public class Cursor { public static bool visible; public static CursorLockMode lockState; }
public struct Vector2 {
 public float x,y; public Vector2(float x,float y){this.x=x;this.y=y;}
 public static Vector2 operator -(Vector2 a,Vector2 b){return new Vector2(a.x-b.x,a.y-b.y);}
 public float magnitude {get{return (float)Math.Sqrt(x*x+y*y);}} public Vector2 normalized {get{return this;}}
 public static float Distance(Vector2 a,Vector2 b){return (a-b).magnitude;}
}
public class Scene {public int handle=1;}
public class Transform {public Vector2 position;public bool IsChildOf(Transform t){return this==t;}}
public class GameObject {public Scene scene=new Scene();public int layer;public bool activeSelf=true;public void SetActive(bool v){activeSelf=v;}}
public class ZeldaCharacterData {public bool IsDead,IsGhostLike;public int PermissionLevel;}
public class PermissionArea {
 public bool isActiveAndEnabled=true;public int PermissionLevel;public GameObject gameObject=new GameObject();
 public Vector2 center;public float radius=.4f;
 public bool Contains(Vector2 position){return Vector2.Distance(center,position)<=radius;}
}
public static class DocumentReader {public static bool IsInputBlocked;}
public static class ClockworkPuppetRuntime {public static bool BlocksCharacterInput;}
public class ZeldaHealthHeartsUI {
 public static ZeldaHealthHeartsUI Instance=new ZeldaHealthHeartsUI();public int messages;public string message;
 public void ShowNotificationPopup(string value){messages++;message=value;}
}
public class ZeldaFourWayMover {
 public Transform transform=new Transform();public GameObject gameObject=new GameObject();public bool isActiveAndEnabled=true,IsAttacking;
 public ZeldaCharacterData data=new ZeldaCharacterData(); public T GetComponent<T>() where T:class{return data as T;}
 public int faces;public void FaceForDialogue(Vector2 direction){faces++;}
}
public enum ZeldaAiState {Idle,Suspicious,Alert,Hostile,Search}
public class ZeldaCharacterAiBase {public bool isActiveAndEnabled=true,IsStunned,IsPossessionLocked;public ZeldaFourWayMover CurrentTarget;public ZeldaAiState CurrentState;public int faces;public void FaceForDialogue(Vector2 direction){faces++;}}
public class Collider2D {public Transform transform=new Transform();public GameObject gameObject=new GameObject();public bool isTrigger;public ZeldaCharacterData data;public T GetComponentInParent<T>() where T:class{return data as T;}}
public struct RaycastHit2D {public Collider2D collider;}
public static class Physics2D {public static Collider2D hit; public static int RaycastNonAlloc(Vector2 a,Vector2 b,RaycastHit2D[] c,float d,int mask){if(hit==null||(mask&(1<<hit.gameObject.layer))==0)return 0;c[0]=new RaycastHit2D{collider=hit};return 1;}}
public static class LayerMask {public static int NameToLayer(string s){return s=="Blocks"?8:9;}}
public static class ZeldaRuntimeRegistry {
 public static System.Collections.Generic.List<PermissionArea> PermissionAreas=new System.Collections.Generic.List<PermissionArea>();
 public static ZeldaFourWayMover controlled;
 public static ZeldaFourWayMover GetControlledMover(){return controlled;}
 public static Scene GetGameplayScene(GameObject go){return go.scene;}
}
public static class CharacterDialogueWindow {public static bool IsOpen;public static int opens;public static void Open(CharacterDialogue s,ZeldaFourWayMover p){opens++;}}
public class DialogueOptionEffect {
 public bool IsVisible(CharacterDialogue a,ZeldaFourWayMover b){return true;}
 public string GetChoiceText(CharacterDialogue a,ZeldaFourWayMover b,string text){return text;}
 public bool allowed=true,success=true;public int calls;public CharacterDialogue seenSpeaker;public ZeldaFourWayMover seenPlayer;
 public bool CanSelect(CharacterDialogue a,ZeldaFourWayMover b,out string reason){reason=allowed?null:"不足";return allowed;}
 public bool TryExecute(CharacterDialogue a,ZeldaFourWayMover b){calls++;seenSpeaker=a;seenPlayer=b;return success;}
}
public class UnityEvent {public int calls;public Action callback;public void Invoke(){calls++;if(callback!=null)callback();}}
public class CharacterDialogue {
 public enum ChoiceAction {Continue,Exit}
 public class Choice {public string text="choice",nextNodeId="next"; public ChoiceAction action;public DialogueOptionEffect effect;public UnityEvent onSelected=new UnityEvent();}
 public class Node {public string id="start",text; public Choice[] choices;}
 public Node[] nodes;public Node StartNode {get{return FindNode("start");}}
 public ZeldaCharacterData data=new ZeldaCharacterData(); public ZeldaFourWayMover mover=new ZeldaFourWayMover();public ZeldaCharacterAiBase ai=new ZeldaCharacterAiBase();
 public bool isActiveAndEnabled=true;public float interactionDistance=1.8f;public Transform transform=new Transform();public GameObject gameObject=new GameObject();
 public RaycastHit2D[] sightHits=new RaycastHit2D[32];
 // FIND_NODE
 // SPEAKER_AVAILABLE
 // SHOW_ICON
 public bool IconVisible {get{return ShouldShowIcon();}}
 // CAN_CONVERSE
 // PERMISSION
 // BEGIN
}
public class Button {public GameObject gameObject=new GameObject();}
public class Text {public string text;}
public static class Debug {public static void LogException(Exception e,object o){}}
public class WindowFixture {
 public static WindowFixture active;public static int closedFrame=-1;
 public CharacterDialogue speaker;public ZeldaFourWayMover player;public CharacterDialogue.Node current;
 public CharacterDialogue.Choice[] visibleChoices {get {var result=new CharacterDialogue.Choice[4];if(current.choices!=null)Array.Copy(current.choices,result,Math.Min(4,current.choices.Length));return result;}}
 public Button[] buttons={new Button(),new Button(),new Button(),new Button()};public Text status=new Text();
 public bool restored,selecting;public int inputFrame=-1,refreshes;public string history="";
 public float previousTimeScale=.4f;public bool previousCursor;public CursorLockMode previousLock=CursorLockMode.Locked;
 public GameObject ownedEventSystem;public int destroyed;
 public void Destroy(GameObject go){destroyed++;}
 public void RefreshChoices(){refreshes++;}public void Enter(CharacterDialogue.Node node){current=node;}
 public void Close(){Restore();} public void Choose(int index){Select(index);}
 // AVAILABLE
 // CHOICE_TEXT
 // SELECT
 // RESTORE
}
public static class DialogueTests {
 static int checks;static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
 static WindowFixture Setup(){
  Time.frameCount++; var choice=new CharacterDialogue.Choice();
  var a=new CharacterDialogue.Node{choices=new[]{choice}};var b=new CharacterDialogue.Node{id="next"};
  var w=new WindowFixture{speaker=new CharacterDialogue{nodes=new[]{a,b}},player=new ZeldaFourWayMover(),current=a};
  WindowFixture.active=w;return w;
 }
 public static int Run(){
  var w=Setup();var s=w.speaker;var p=w.player;
  Check(s.CanConverse(p),"Normal nearby actor allowed");
  for(int mask=1;mask<64;mask++){
   s.data.IsDead=(mask&1)!=0;s.data.IsGhostLike=(mask&2)!=0;p.data.IsDead=(mask&4)!=0;
   p.data.IsGhostLike=(mask&8)!=0;s.mover.IsAttacking=(mask&16)!=0;p.IsAttacking=(mask&32)!=0;
   Check(!s.CanConverse(p),"Dead/ghost/attack guard "+mask);
  }
  w=Setup();s=w.speaker;p=w.player;
  Check(!s.CanConverse(s.mover),"No self dialogue");s.isActiveAndEnabled=false;Check(!s.CanConverse(p),"Disabled");s.isActiveAndEnabled=true;
  s.ai.CurrentTarget=p;s.ai.CurrentState=ZeldaAiState.Alert;Check(!s.CanConverse(p),"Warning blocks");
  s.ai.CurrentState=ZeldaAiState.Hostile;Check(!s.CanConverse(p),"Hostile blocks");
  s.ai.CurrentTarget=new ZeldaFourWayMover();Check(!s.CanConverse(p),"Fighting other NPCs also blocks dialogue");
  foreach(var state in new[]{ZeldaAiState.Suspicious,ZeldaAiState.Alert,ZeldaAiState.Hostile}){
   s.ai.CurrentState=state;Check(!s.IconVisible && !s.CanConverse(p),"Reactive state hides icon and blocks E: "+state);
   s.ai.CurrentState=ZeldaAiState.Idle;Check(s.IconVisible && s.CanConverse(p),"Recovery restores icon and E");
  }
  s.ai.IsStunned=true;Check(!s.CanConverse(p),"Stun blocks");s.ai.IsStunned=false;
  s.ai.IsPossessionLocked=true;Check(!s.CanConverse(p),"Possession lock blocks");s.ai.IsPossessionLocked=false;
  s.ai.IsStunned=true;Check(!s.IconVisible,"Stun hides icon");s.ai.IsStunned=false;
  s.ai.IsPossessionLocked=true;Check(!s.IconVisible,"Actual possession hides icon");s.ai.IsPossessionLocked=false;
  Check(s.IconVisible,"Eligible possession target without active transfer retains icon");
  s.mover.IsAttacking=true;Check(!s.IconVisible,"Attack hides icon");s.mover.IsAttacking=false;
  s.data.IsDead=true;Check(!s.IconVisible,"Death hides icon");s.data.IsDead=false;
  s.data.IsGhostLike=true;Check(!s.IconVisible,"Ghost NPC has no icon");s.data.IsGhostLike=false;
  s.isActiveAndEnabled=false;Check(!s.IconVisible,"Disabled dialogue hides icon");s.isActiveAndEnabled=true;
  ZeldaRuntimeRegistry.controlled=s.mover;Check(!s.IconVisible,"Controlled actor has no own dialogue icon");ZeldaRuntimeRegistry.controlled=p;
  CharacterDialogueWindow.IsOpen=true;Check(!s.IconVisible,"Open conversation hides icon");CharacterDialogueWindow.IsOpen=false;
  p.transform.position=new Vector2(10,0);Check(s.IconVisible && !s.CanConverse(p),"Distant eligible NPC keeps discovery icon");p.transform.position=new Vector2();
  p.transform.position=new Vector2(2,0);Check(!s.CanConverse(p),"Range");p.transform.position=new Vector2();
  p.gameObject.scene.handle=2;Check(!s.CanConverse(p),"Different scene");p.gameObject.scene.handle=1;
  Physics2D.hit=new Collider2D();Check(s.CanConverse(p),"Solid non-Blocks counter permits dialogue");
  Physics2D.hit.gameObject.layer=9;Check(s.CanConverse(p),"Hidden Blocks does not block dialogue");
  Physics2D.hit.gameObject.layer=8;Check(!s.CanConverse(p),"Solid Blocks wall obstructs");
  Physics2D.hit.gameObject.layer=0;
  Physics2D.hit.isTrigger=true;Check(s.CanConverse(p),"Ordinary trigger ignored");
  Physics2D.hit.gameObject.layer=8;Check(!s.CanConverse(p),"Blocks trigger obstructs");Physics2D.hit=null;
  Check(s.FindNode("next")==s.nodes[1] && s.FindNode("missing")==null && s.FindNode(null)==null,"Stable ID resolution");
  ZeldaRuntimeRegistry.controlled=p;
  var area=new PermissionArea{PermissionLevel=3};ZeldaRuntimeRegistry.PermissionAreas.Add(area);
  p.data.PermissionLevel=2;
  Check(s.IconVisible&&s.CanConverse(p),"Insufficient permission preserves icon and interaction offer");
  int opened=CharacterDialogueWindow.opens;int faced=s.ai.faces;
  s.BeginDialogue();
  Check(CharacterDialogueWindow.opens==opened&&s.ai.faces==faced,"Denied attempt neither opens nor turns speaker");
  Check(ZeldaHealthHeartsUI.Instance.messages==1&&ZeldaHealthHeartsUI.Instance.message.Contains("无法进行对话"),"Denied attempt shows notification");
  p.data.PermissionLevel=3;s.BeginDialogue();Check(CharacterDialogueWindow.opens==opened+1,"Equal permission permits dialogue");
  p.data.PermissionLevel=4;Check(s.HasDialoguePermission(p),"Higher permission allowed");
  area.center=new Vector2(1,0);p.transform.position=area.center;p.data.PermissionLevel=0;
  Check(!s.HasDialoguePermission(p),"Illegal player position blocks dialogue even when speaker outside");
  s.transform.position=area.center;p.transform.position=new Vector2();
  Check(s.HasDialoguePermission(p),"Public player position allows restricted shopkeeper dialogue");
  p.transform.position=area.center;
  area.isActiveAndEnabled=false;Check(s.HasDialoguePermission(p),"Disabled area ignored");area.isActiveAndEnabled=true;
  area.gameObject.scene.handle=2;Check(s.HasDialoguePermission(p),"Other scene area ignored");area.gameObject.scene.handle=1;
  p.data.PermissionLevel=3;var stricter=new PermissionArea{center=area.center,PermissionLevel=5};
  ZeldaRuntimeRegistry.PermissionAreas.Add(stricter);Check(!s.HasDialoguePermission(p),"Overlapping areas require highest permission");
  p.data.PermissionLevel=5;Check(s.HasDialoguePermission(p),"Highest overlapping requirement met");
  Check(!s.HasDialoguePermission(null),"Missing controlled actor rejected");
  ZeldaRuntimeRegistry.PermissionAreas.Clear();s.data.PermissionLevel=9;p.data.PermissionLevel=0;
  Check(s.HasDialoguePermission(p),"NPC personal rank is not the area requirement");
  var temp=s.nodes[0];s.nodes[0]=s.nodes[1];s.nodes[1]=temp;Check(s.StartNode==temp,"Reorder does not alter links");
  w=Setup();var c=w.current.choices[0];w.Choose(0);Check(w.current.id=="next" && c.onSelected.calls==1,"Branch and event");
  Check(w.history=="你\nchoice\n\n","Player name above speech, like NPC name");
  w=Setup();c=w.current.choices[0];c.nextNodeId="missing";w.Choose(0);Check(c.onSelected.calls==0 && w.current.id=="start","Broken link cannot execute");
  w=Setup();c=w.current.choices[0];c.nextNodeId="start";w.Choose(0);w.Choose(0);Check(c.onSelected.calls==1,"One execution per frame");
  w=Setup();c=w.current.choices[0];c.effect=new DialogueOptionEffect{allowed=false};w.Choose(0);Check(c.effect.calls==0 && c.onSelected.calls==0,"Condition prevents side effects");
  c.effect.allowed=true;c.effect.success=false;w.Choose(0);Check(c.effect.calls==1 && c.onSelected.calls==0 && w.current.id=="start","Failed transaction stays on node");
  Time.frameCount++;c.effect.success=true;w.Choose(0);Check(c.effect.seenPlayer==w.player && c.effect.seenSpeaker==w.speaker && c.onSelected.calls==1,"Context hook and successful event");
  w=Setup();c=w.current.choices[0];c.onSelected.callback=()=>w.Close();w.Choose(0);Check(w.restored && w.current.id=="start","Reentrant close does not advance");
  w=Setup();c=w.current.choices[0];c.action=CharacterDialogue.ChoiceAction.Exit;Time.timeScale=0;w.Choose(0);
  Check(w.restored && WindowFixture.active==null && WindowFixture.closedFrame==Time.frameCount,"Exit lifecycle");
  Check(Time.timeScale==.4f && !Cursor.visible && Cursor.lockState==CursorLockMode.Locked,"Exact time/cursor restore");
  Time.timeScale=.8f;w.Close();Check(Time.timeScale==.8f,"Restore idempotence");
  w=Setup();w.current.choices=new CharacterDialogue.Choice[0];w.Choose(0);Check(w.restored,"Terminal node fallback exit");
  w=Setup();w.inputFrame=Time.frameCount;w.Choose(0);Check(w.current.id=="start","Opening input cannot choose");
  w=Setup();w.Choose(4);w.Choose(-1);Check(w.current.id=="start","At most four choices");
  return checks;
 }
}
'@
$fixture=$fixture.Replace('// FIND_NODE',(Method $source 'public Node FindNode(string id)'))
$fixture=$fixture.Replace('// SPEAKER_AVAILABLE',(Method $source 'private bool IsSpeakerAvailable()'))
$fixture=$fixture.Replace('// SHOW_ICON',(Method $source 'private bool ShouldShowIcon()'))
$fixture=$fixture.Replace('// CAN_CONVERSE',(Method $source 'public bool CanConverse(ZeldaFourWayMover player)'))
$fixture=$fixture.Replace('// PERMISSION',(Method $source 'public bool HasDialoguePermission(ZeldaFourWayMover player)'))
$fixture=$fixture.Replace('// BEGIN',(Method $source 'public void BeginDialogue()'))
$fixture=$fixture.Replace('// AVAILABLE',(Method $window 'private bool IsChoiceAvailable(CharacterDialogue.Choice choice, out string reason)'))
$fixture=$fixture.Replace('// CHOICE_TEXT',(Method $window 'private string GetChoiceText(CharacterDialogue.Choice choice)'))
$fixture=$fixture.Replace('// SELECT',(Method $window 'private void Select(int index)'))
$fixture=$fixture.Replace('// RESTORE',(Method $window 'private void Restore()'))
Add-Type -TypeDefinition $fixture
$checks=[DialogueTests]::Run()
Write-Output "PASS: $checks production dialogue assertions (eligibility, branches, conditions, effects, exit and cleanup)."
