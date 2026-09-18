$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$source = Get-Content -Raw -Encoding UTF8 "$root/Assets/Scripts/Interaction/SignpostInteraction.cs"
function ExtractMethod([string]$signature) {
    $start=$source.IndexOf($signature,[StringComparison]::Ordinal)
    if($start -lt 0){throw "Method missing: $signature"}
    $cursor=$source.IndexOf('{',$start);$depth=1;$end=$cursor+1
    while($depth -gt 0 -and $end -lt $source.Length){
        if($source[$end] -eq '{'){$depth++};if($source[$end] -eq '}'){$depth--};$end++
    }
    $source.Substring($start,$end-$start)
}
$methods=@(
    'private static void ResetStatics()', 'private void Update()',
    'private ZeldaFourWayMover GetNearbyCharacter()', 'private string GetDirectionText(int index)',
    'private int CountVisibleDirections()', 'private void OpenWindow()', 'private void CloseWindow()',
    'private void RefreshDirections()', 'private void OnDisable()'
)|ForEach-Object {ExtractMethod $_}
$layout=[regex]::Match($source,'(?s)    private static readonly Vector2 DirectionPanelSize.*?(?=    private readonly RectTransform\[\] directionPanels)').Value
if(!$layout){throw 'Independent compass panel layout missing'}
$stubs=@'
using System;
using System.Collections.Generic;
public static class Time {public static int frameCount=10; public static float timeScale=.4f;}
public enum KeyCode {E,Escape}
public static class Input {
    public static HashSet<KeyCode> keys=new HashSet<KeyCode>();
    public static bool GetKeyDown(KeyCode key){return keys.Contains(key);}
}
public struct Vector2 {
    public float x,y;public Vector2(float a,float b){x=a;y=b;}
    public static float Distance(Vector2 a,Vector2 b){return (float)Math.Sqrt((a.x-b.x)*(a.x-b.x)+(a.y-b.y)*(a.y-b.y));}
}
public static class Mathf {public static int Max(int a,int b){return Math.Max(a,b);}}
public class Scene {public int handle=1;}
public class Transform {public Vector2 position;}
public class GameObject {
    public bool activeSelf;public Scene scene=new Scene();public Transform transform=new Transform();
    public void SetActive(bool v){activeSelf=v;}
}
public class RectTransform {public GameObject gameObject=new GameObject();public Vector2 sizeDelta,anchoredPosition;}
public class Text {public GameObject gameObject=new GameObject();public RectTransform rectTransform=new RectTransform();public string text;}
public class ZeldaCharacterData {public bool IsDead;}
public class ZeldaFourWayMover {
    public bool isActiveAndEnabled=true;public GameObject gameObject=new GameObject();
    public Transform transform {get{return gameObject.transform;}}
    public ZeldaCharacterData data=new ZeldaCharacterData();
    public T GetComponent<T>() where T:class {return data as T;}
}
public static class ZeldaRuntimeRegistry {
    public static ZeldaFourWayMover mover=new ZeldaFourWayMover();
    public static ZeldaFourWayMover GetControlledMover(){return mover;}
    public static Scene GetGameplayScene(GameObject actor){return actor.scene;}
}
public static class DocumentReader {
    public static bool otherModal;
    public static bool IsInputBlocked {get{return otherModal||SignpostInteraction.BlocksInput;}}
}
public static class ClockworkPuppetRuntime {public static bool BlocksCharacterInput;}
public static class ZeldaInteractionArbiter {
    public static int offers;public static Action pending;
    public static void OfferInteraction(object o,object m,KeyCode key,Vector2 p,Action<bool> callback){offers++;}
    public static void Submit(object o,object m,KeyCode key,Vector2 p,Action callback){pending=callback;}
}
public class SignpostInteraction {
    private string upText="",downText="",leftText="",rightText="";
    private float interactionDistance=1.5f,previousTimeScale;
    private static SignpostInteraction activeSignpost;
    private static int inputBlockedThroughFrame=-1;
    public static bool BlocksInput {get{return activeSignpost!=null||Time.frameCount<=inputBlockedThroughFrame;}}
    private GameObject windowObject=new GameObject();
    private RectTransform[] directionPanels={new RectTransform(),new RectTransform(),new RectTransform(),new RectTransform()};
    private Text[] directionLabels={new Text(),new Text(),new Text(),new Text()};
    private Text[] directionContents={new Text(),new Text(),new Text(),new Text()};
    private Text emptyLabel=new Text();
    private GameObject gameObject=new GameObject();
    private Transform transform {get{return gameObject.transform;}}
    private void EnsureWindowBuilt(){}private void EnsureWindowOnTop(){}private void SetPromptVisible(bool v){}
'@
$tests=@'
    static int checks;
    static void Check(bool ok,string msg){checks++;if(!ok)throw new Exception(msg);}
    static void Next(){Time.frameCount++;Input.keys.Clear();ZeldaInteractionArbiter.pending=null;}
    public static int Run(){
        ResetStatics();var s=new SignpostInteraction();
        s.RefreshDirections();Check(s.CountVisibleDirections()==0 && s.emptyLabel.gameObject.activeSelf,"Empty sign shows only empty-state message");
        for(int i=0;i<4;i++)Check(!s.directionPanels[i].gameObject.activeSelf,"Blank direction hides entire independent card including border and arrow");
        s.upText="  Market  ";s.downText=" \r\n ";s.leftText="Library\nTemple";s.rightText=null;
        s.RefreshDirections();Check(s.CountVisibleDirections()==2,"Whitespace and null descriptions hidden");
        Check(s.directionContents[0].text=="Market" && s.directionContents[2].text.Contains("\n"),"Trim outside whitespace, retain multiline content");
        Check(s.directionPanels[0].gameObject.activeSelf && s.directionPanels[2].gameObject.activeSelf && !s.directionPanels[1].gameObject.activeSelf && !s.directionPanels[3].gameObject.activeSelf,"Only configured compass cards visible");
        Check(DirectionPanelPositions[0].x==0 && DirectionPanelPositions[0].y>0 && DirectionPanelPositions[1].x==0 && DirectionPanelPositions[1].y<0,"Up/down panels occupy top/bottom");
        Check(DirectionPanelPositions[2].x<0 && DirectionPanelPositions[2].y==0 && DirectionPanelPositions[3].x>0 && DirectionPanelPositions[3].y==0,"Left/right panels occupy left/right");
        for(int i=0;i<4;i++){
            Vector2 p=DirectionPanelPositions[i];
            Check(Math.Abs(p.x)+DirectionPanelSize.x/2+2<640 && Math.Abs(p.y)+DirectionPanelSize.y/2+2<360,"Card stays inside reference viewport with border padding");
            for(int j=i+1;j<4;j++){
                Vector2 q=DirectionPanelPositions[j];
                Check(Math.Abs(p.x-q.x)>DirectionPanelSize.x+4 || Math.Abs(p.y-q.y)>DirectionPanelSize.y+4,"Independent panels never overlap including borders");
            }
        }
        s.downText="Gate";s.rightText="Inn";s.RefreshDirections();Check(s.CountVisibleDirections()==4 && !s.emptyLabel.gameObject.activeSelf,"All four configured directions shown");
        Input.keys.Add(KeyCode.E);s.Update();Check(ZeldaInteractionArbiter.offers==1 && ZeldaInteractionArbiter.pending!=null,"Nearby E registered with shared arbiter without HUD");
        ZeldaInteractionArbiter.pending();Check(BlocksInput && Time.timeScale==0 && s.windowObject.activeSelf,"Opening pauses gameplay and blocks other controls");
        var other=new SignpostInteraction();other.OpenWindow();Check(!other.windowObject.activeSelf,"Second sign cannot replace open window");
        Next();Input.keys.Add(KeyCode.E);s.Update();Check(!s.windowObject.activeSelf && Time.timeScale==.4f,"Second E closes and restores exact prior time scale");
        Check(BlocksInput && DocumentReader.IsInputBlocked,"Closing E remains consumed for rest of frame");
        Next();Check(!BlocksInput,"Input released next frame");s.OpenWindow();
        Next();Input.keys.Add(KeyCode.Escape);s.Update();Check(!s.windowObject.activeSelf && BlocksInput,"Escape closes without also opening pause menu");
        Next();DocumentReader.otherModal=true;s.OpenWindow();Check(!s.windowObject.activeSelf,"Other menus prevent opening");DocumentReader.otherModal=false;
        ClockworkPuppetRuntime.BlocksCharacterInput=true;s.OpenWindow();Check(!s.windowObject.activeSelf,"Remote control cannot open sign");ClockworkPuppetRuntime.BlocksCharacterInput=false;
        ZeldaRuntimeRegistry.mover.transform.position=new Vector2(3,0);s.OpenWindow();Check(!s.windowObject.activeSelf,"Out of range rejected, including stale queued requests");
        ZeldaRuntimeRegistry.mover.transform.position=new Vector2(0,0);ZeldaRuntimeRegistry.mover.data.IsDead=true;
        Check(s.GetNearbyCharacter()==null,"Dead character rejected");ZeldaRuntimeRegistry.mover.data.IsDead=false;
        ZeldaRuntimeRegistry.mover.gameObject.scene.handle=2;Check(s.GetNearbyCharacter()==null,"Actor from different gameplay scene rejected");ZeldaRuntimeRegistry.mover.gameObject.scene.handle=1;
        s.OpenWindow();s.OnDisable();Check(!s.windowObject.activeSelf && Time.timeScale==.4f,"Streaming disable/unload restores gameplay and closes window");
        Next();ZeldaRuntimeRegistry.mover=null;Check(s.GetNearbyCharacter()==null,"Missing controlled actor is safe");
        ResetStatics();Check(!BlocksInput,"Domain-reload-disabled startup clears static state");
        return checks;
    }
}
'@
Add-Type -TypeDefinition ($stubs + $layout + ($methods -join "`n") + $tests)
Write-Output ("Signpost interaction: {0} checks passed." -f [SignpostInteraction]::Run())
$prefab=Get-Content -Raw "$root/Assets/Prefabs/Decorations/GeometricSignpost.prefab"
if(!$prefab.Contains('guid: 6c361f780f3543b098a21d4dc1054511') -or !$prefab.Contains('displayFont: {fileID: 12800000, guid: 292af55397c443f6a5d7774297dac481')){throw 'Missing prefab script or CJK font reference'}
foreach($field in 'upText','downText','leftText','rightText'){if($prefab -notmatch "(?m)^  ${field}:"){throw "Missing editable direction: $field"}}
$gate=Get-Content -Raw "$root/Assets/Scripts/Interaction/DocumentReader.cs"
$arbiter=Get-Content -Raw "$root/Assets/Scripts/Interaction/ZeldaInteractionArbiter.cs"
if(!$gate.Contains('SignpostInteraction.BlocksInput') -or !$arbiter.Contains('ClockworkPuppetRuntime.BlocksCharacterInput || SignpostInteraction.BlocksInput')){throw 'Missing modal/input-consumption integration'}
if(!$source.Contains('CRTScreenEffect.RegisterCanvas(windowCanvas)')){throw 'CRT and F12 registration missing'}
if($source.Contains('row++') -or $source.Contains('title.text =')){throw 'Old list layout or shared window title returned'}
$arrowLabels=[regex]::Match($source,'string\[\] labels = \{ ([^}]+) \}').Groups[1].Value
$expected=@(0x2191,0x2193,0x2190,0x2192)
$tokens=[regex]::Matches($arrowLabels,'"([^"]*)"')
if($tokens.Count -ne 4){throw 'Four arrow labels required'}
for($i=0;$i -lt 4;$i++){if($tokens[$i].Groups[1].Value -cne [string][char]$expected[$i]){throw 'Direction label must be just the matching arrow'}}
if(!$source.Contains('directionPanels[i].gameObject.SetActive(visible)') -or !$source.Contains('CanvasScaler.ScreenMatchMode.Expand')){throw 'Panel visibility or responsive scaling missing'}
Write-Output 'PASS: prefab binding, four editable directions, CJK font, shared input blocking, CRT/F12 registration. Not a Unity Play Mode test.'
