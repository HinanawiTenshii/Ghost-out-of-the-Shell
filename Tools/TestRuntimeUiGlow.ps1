$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$scope=Get-Content -Raw "$root/Assets/Scripts/UI/RuntimeUiGlowScope.cs"
$effect=Get-Content -Raw "$root/Assets/Scripts/UI/TitleScreenGlow.cs"
$stubs=@'
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace UnityEngine {
 public class DisallowMultipleComponent:System.Attribute{} public class RequireComponent:System.Attribute{public RequireComponent(System.Type t){}}
 public static class Application {public static bool isPlaying=true;}
 public static class Time {public static int frameCount;}
 public struct Vector2 {public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}public static Vector2 operator *(Vector2 a,float n)=>new Vector2(a.x*n,a.y*n);}
 public struct Vector3 {public float x,y,z;}
 public struct Color {public float r,g,b,a;public Color(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;}}
 public struct Color32 {public byte r,g,b,a;}
 public struct UIVertex {public Vector3 position;public Color32 color;public Vector2 uv0;}
 public struct Rect {public float width,height;public Rect(float w,float h){width=w;height=h;}}
 public class RectTransform {public Rect rect=new Rect(30,30);}
 public class Component {
  public GameObject gameObject=new GameObject();public bool isActiveAndEnabled=true;
  private bool active=true;public bool enabled {get=>active;set{if(active==value)return;active=value;isActiveAndEnabled=value;GetType().GetMethod(value?"OnEnable":"OnDisable",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)?.Invoke(this,null);}}
  public T GetComponent<T>() where T:class{return gameObject.GetComponent<T>();}
  public T[] GetComponentsInChildren<T>(bool inactive) where T:class {
   var result=new List<T>();foreach(var c in gameObject.children)foreach(var x in c.components)if(x is T t)result.Add(t);return result.ToArray();
  }
  protected static void Destroy(Component c){c.enabled=false;c.gameObject.components.Remove(c);}
 }
 public class MonoBehaviour:Component{}
 public class GameObject {
  public List<object> components=new List<object>();public List<GameObject> children=new List<GameObject>();public int adds;
  public T GetComponent<T>() where T:class{foreach(var c in components)if(c is T value)return value;return null;}
  public T AddComponent<T>() where T:Component,new(){var c=new T();c.gameObject=this;components.Add(c);adds++;var m=typeof(T).GetMethod("OnEnable",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);m?.Invoke(c,null);return c;}
 }
 public class Canvas:Component {
  public static event System.Action preWillRenderCanvases;
  public static void Render(){preWillRenderCanvases?.Invoke();}
 }
 public static class Mathf {public static float Abs(float n)=>System.Math.Abs(n);public static float Min(float a,float b)=>System.Math.Min(a,b);public static float Max(float a,float b)=>System.Math.Max(a,b);public static int RoundToInt(float n)=>(int)System.Math.Round(n);}
}
namespace UnityEngine.UI {
 public class Graphic:Component {public Canvas canvas;public RectTransform rectTransform=new RectTransform();public Color color=new Color(1,1,1);}
 public class Text:Graphic{} public class RawImage:Graphic{} public class Image:Graphic{public object sprite;}
 public class Mask:Component{} public class RectMask2D:Component{}
 public class VertexHelper {
  public List<UIVertex> data=new List<UIVertex>();public int currentVertCount=>data.Count;
  public void GetUIVertexStream(List<UIVertex> output)=>output.AddRange(data);
  public void Clear()=>data.Clear();public void AddUIVertexTriangleStream(List<UIVertex> source)=>data.AddRange(source);
 }
 public abstract class BaseMeshEffect:MonoBehaviour {public bool IsActive()=>isActiveAndEnabled;public abstract void ModifyMesh(VertexHelper v);}
 public static class GraphicRegistry {
  private static Dictionary<Canvas,List<Graphic>> entries=new Dictionary<Canvas,List<Graphic>>();
  public static IList<Graphic> GetGraphicsForCanvas(Canvas canvas){if(!entries.ContainsKey(canvas))entries[canvas]=new List<Graphic>();return entries[canvas];}
 }
}
public class MapSceneQuestMarkerGraphic:Graphic{}
public class OtherMapGeometry:Graphic{}
public static class UiGlowTests {
 static int count;
 static void Check(bool ok,string text){count++;if(!ok)throw new System.Exception(text);}
 static bool Has(Graphic g)=>g.GetComponent<TitleScreenGlow>()!=null;
 static Image Image(float w,float h,bool sprite,Color color){var image=new GameObject().AddComponent<Image>();image.rectTransform.rect=new Rect(w,h);image.sprite=sprite?new object():null;image.color=color;return image;}
 static void NextFrame(){Time.frameCount++;Canvas.Render();}
 public static int Run(){
  var canvas=new GameObject().AddComponent<Canvas>();var list=GraphicRegistry.GetGraphicsForCanvas(canvas);
  var text=new GameObject().AddComponent<Text>();list.Add(text);
  var edge=Image(700,3,false,new Color(.37f,.97f,.91f));list.Add(edge);
  var icon=Image(48,48,true,new Color(1,1,1));list.Add(icon);
  var bar=Image(180,16,false,new Color(.37f,.97f,.91f));list.Add(bar);
  var badge=new GameObject().AddComponent<MapSceneQuestMarkerGraphic>();list.Add(badge);
  var dimmer=Image(1280,720,false,new Color(0,0,0,.8f));list.Add(dimmer);
  var whiteBackground=Image(600,400,false,new Color(1,1,1));list.Add(whiteBackground);
  var darkButton=Image(80,24,false,new Color(.03f,.12f,.2f));list.Add(darkButton);
  var viewport=Image(60,24,true,new Color(1,1,1));viewport.gameObject.AddComponent<Mask>();list.Add(viewport);
  var rectMask=Image(60,24,true,new Color(1,1,1));rectMask.gameObject.AddComponent<RectMask2D>();list.Add(rectMask);
  var raw=new GameObject().AddComponent<RawImage>();list.Add(raw);
  var map=new GameObject().AddComponent<OtherMapGeometry>();list.Add(map);
  RuntimeUiGlowScope.Attach(canvas);RuntimeUiGlowScope.Attach(canvas);RuntimeUiGlowScope.Attach(null);
  Check(canvas.gameObject.components.Count==2,"Canvas scope installed once");NextFrame();
  Check(Has(text)&&Has(edge)&&Has(icon)&&Has(bar)&&Has(badge),"Opt-in window text, borders, icons, progress and quest markers enhanced");
  Check(!Has(dimmer)&&!Has(whiteBackground)&&!Has(darkButton),"Full panels and dark fills remain unchanged");
  Check(!Has(viewport)&&!Has(rectMask)&&!Has(raw)&&!Has(map),"Masks, render textures and map mesh excluded");
  int adds=text.gameObject.adds;NextFrame();Canvas.Render();Check(text.gameObject.adds==adds,"No duplicate mesh effect after repeated render callbacks");
  var popup=new GameObject().AddComponent<Text>();list.Add(popup);NextFrame();Check(Has(popup),"Popup added after initial registration is covered");
  var delayed=Image(0,0,false,new Color(.2f,.6f,1));list.Add(delayed);NextFrame();Check(!Has(delayed),"Zero-size pending layout is deferred");
  delayed.rectTransform.rect=new Rect(120,3);NextFrame();Check(Has(delayed),"Finished layout receives glow without rebuilding the whole Canvas");
  darkButton.color=new Color(.2f,.7f,1);NextFrame();Check(Has(darkButton),"Accent becoming bright later is discovered");
  var hidden=new GameObject().AddComponent<Text>();hidden.isActiveAndEnabled=false;list.Add(hidden);NextFrame();Check(!Has(hidden),"Inactive content waits until enabled");
  hidden.isActiveAndEnabled=true;NextFrame();Check(Has(hidden),"Reactivated menu gets glow");
  var scope=canvas.GetComponent<RuntimeUiGlowScope>();
  typeof(RuntimeUiGlowScope).GetMethod("OnDisable",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(scope,null);
  var after=new GameObject().AddComponent<Text>();list.Add(after);NextFrame();Check(!Has(after),"Disabled scope unsubscribes render callback");
  typeof(RuntimeUiGlowScope).GetMethod("OnEnable",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(scope,null);
  NextFrame();Check(Has(after),"Reenabled scope resumes discovery");
  var hud=new GameObject().AddComponent<Canvas>();var hudText=new GameObject().AddComponent<Text>();hudText.canvas=hud;
  hud.gameObject.children.Add(hudText.gameObject);GraphicRegistry.GetGraphicsForCanvas(hud).Add(hudText);
  RuntimeUiGlowScope.Detach(hud);NextFrame();Check(!Has(hudText)&&hud.GetComponent<RuntimeUiGlowScope>()==null,"Default HUD has no glow scope or mesh effect");
  RuntimeUiGlowScope.Attach(hud);NextFrame();Check(Has(hudText),"Simulate HUD enhanced by old global registration");
  var hiddenHud=new GameObject().AddComponent<Text>();hiddenHud.canvas=hud;hiddenHud.isActiveAndEnabled=false;
  hud.gameObject.children.Add(hiddenHud.gameObject);TitleScreenGlow.Attach(hiddenHud);
  var nestedPopup=new GameObject().AddComponent<Text>();nestedPopup.canvas=canvas;hud.gameObject.children.Add(nestedPopup.gameObject);TitleScreenGlow.Attach(nestedPopup);
  RuntimeUiGlowScope.Detach(hud);NextFrame();
  Check(!Has(hudText)&&!Has(hiddenHud)&&hud.GetComponent<RuntimeUiGlowScope>()==null,"Remove old HUD glow including hidden children and unsubscribe scope");
  Check(Has(nestedPopup)&&Has(text),"HUD cleanup preserves nested and independent popup canvas glow");
  var newHudIcon=Image(32,32,true,new Color(1,1,1));GraphicRegistry.GetGraphicsForCanvas(hud).Add(newHudIcon);NextFrame();
  Check(!Has(hudText)&&!Has(newHudIcon),"HUD refresh cannot recreate halos after removal");
  Application.isPlaying=false;var editorCanvas=new GameObject().AddComponent<Canvas>();RuntimeUiGlowScope.Attach(editorCanvas);
  Check(editorCanvas.GetComponent<RuntimeUiGlowScope>()==null,"No edit-mode scene mutations");Application.isPlaying=true;
  var mesh=new VertexHelper();for(int i=0;i<6;i++)mesh.data.Add(new UIVertex{color=new Color32{r=95,g=248,b=233,a=(byte)(i==0?0:255)}});
  new TitleScreenGlow().ModifyMesh(mesh);
  Check(mesh.data.Count==150,"Shared three-ring style used in gameplay");
  Check(mesh.data[145].color.g==253&&mesh.data[145].color.b==242&&mesh.data[145].color.r==129,"Gameplay cyan stays cyan instead of becoming title blue");
  Check(mesh.data[1].color.a==7&&mesh.data[49].color.a==16&&mesh.data[97].color.a==31,"All three cyan halo rings are reduced by 25 percent before alpha quantization");
  Check(mesh.data[145].color.a==255,"Cyan core opacity remains unchanged for readability");
  for(int i=0;i<25;i++)Check(mesh.data[i*6].color.a==0,"Fully transparent elements never leave a glow");
  return count;
 }
}
'@
$combined=$stubs+([regex]::Replace($effect,'(?m)^using .*;\r?\n',''))+([regex]::Replace($scope,'(?m)^using .*;\r?\n',''))
Add-Type -TypeDefinition $combined
$checks=[UiGlowTests]::Run()
$crt=Get-Content -Raw "$root/Assets/Scripts/Rendering/CRTScreenEffect.cs"
if(!$crt.Contains('bool enableGlow = false') -or !$crt.Contains('if (enableGlow) RuntimeUiGlowScope.Attach(canvas);') -or !$crt.Contains('else RuntimeUiGlowScope.Detach(canvas);')){throw 'Canvas glow must be explicit opt-in and removable'}
if($crt.Contains('RuntimeUiGlowScope.Attach(canvases[i]);')){throw 'Scene canvas discovery must not re-enable HUD glow'}
$checks+=2
$refresh=$scope.Substring($scope.IndexOf('    private void Refresh()'))
if($refresh.Contains('FindObjectsOfType') -or $refresh.Contains('GetComponentsInChildren')){throw 'Avoid recurring full-scene/hierarchy UI scans; only one-time detach may inspect hidden children'}
$checks++
foreach($entry in @(
 @('UI/PauseMenuController.cs','menuCanvas'),@('UI/TabJournalMenuController.cs','menuCanvas'),
 @('UI/TitleScreenController.cs','titleCanvas'),@('UI/SaveSlotPanel.cs','canvas'),
 @('Interaction/DocumentReader.cs','readingCanvas'),@('Interaction/DoorPasswordPanel.cs','passwordCanvas'),
 @('Interaction/ElectricGatePanel.cs','canvas'),@('Interaction/SignpostInteraction.cs','windowCanvas'),
 @('Items/ItemDescriptionWindow.cs','windowCanvas'),@('UI/ZeldaRequirementWindow.cs','overlayCanvas'),
 @('Characters/ZeldaCharacterStatsWindow.cs','overlayCanvas'))){
 $source=Get-Content -Raw "$root/Assets/Scripts/$($entry[0])"
 if(!$source.Contains("RegisterCanvas($($entry[1]), enableGlow: true)")){throw "Missing popup glow: $($entry[0])"};$checks++
}
$hudSource=Get-Content -Raw "$root/Assets/Scripts/UI/ZeldaHealthHeartsUI.cs"
if(!$hudSource.Contains('RegisterCanvas(hudCanvas);') -or $hudSource.Contains('enableGlow: true')){throw 'HUD must use non-glowing registration'}
$checks++
Write-Output "PASS: $checks production Canvas-discovery, lifecycle, eligibility and cyan-glow checks (Unity stubs, not Play Mode)."
