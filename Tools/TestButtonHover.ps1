$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskSource = Get-Content -Raw "$taskRoot/Assets/Scripts/UI/ButtonHoverHighlight.cs"
$taskHarness = @'
namespace UnityEngine {
 public class DisallowMultipleComponent : System.Attribute {}
 public class Component {
  public GameObject gameObject; public Transform transform {get{return gameObject.transform;}}
  public T GetComponent<T>() where T:class {return gameObject.GetComponent<T>();}
 }
 public class MonoBehaviour:Component {}
 public class Transform:Component {
  public Transform parent;public bool first;
  public void SetParent(Transform value,bool world){parent=value;value.gameObject.children.Add(gameObject);}
  public void SetAsFirstSibling(){first=true;}
 }
 public class RectTransform:Transform {public Vector2 anchorMin,anchorMax,offsetMin,offsetMax;}
 public class GameObject {
  public string name;public Transform transform;
  public System.Collections.Generic.List<Component> components=new System.Collections.Generic.List<Component>();
  public System.Collections.Generic.List<GameObject> children=new System.Collections.Generic.List<GameObject>();
  public GameObject(string name,params System.Type[] types){this.name=name;transform=AddComponent<RectTransform>();foreach(var t in types)if(t!=typeof(RectTransform))Add(t);}
  public T AddComponent<T>() where T:Component,new(){return (T)Add(typeof(T));}
  Component Add(System.Type type){var c=(Component)System.Activator.CreateInstance(type);c.gameObject=this;components.Add(c);return c;}
  public T GetComponent<T>() where T:class{foreach(var c in components)if(c is T)return c as T;return null;}
 }
 public struct Vector2 {public float x,y; public static Vector2 zero {get{return new Vector2();}} public static Vector2 one {get{return new Vector2{x=1,y=1};}}}
 public struct Color {public float r,g,b,a;public Color(float r,float g,float b,float a){this.r=r;this.g=g;this.b=b;this.a=a;}}
 public static class Time {public static float unscaledDeltaTime,timeScale;}
 public static class Mathf {
  public static bool Approximately(float a,float b){return System.Math.Abs(a-b)<0.000001;}
  public static float MoveTowards(float a,float b,float max){return System.Math.Abs(b-a)<=max?b:a+System.Math.Sign(b-a)*max;}
 }
}
namespace UnityEngine.UI {
 public class Graphic:UnityEngine.Component {public UnityEngine.Color color;public bool raycastTarget=true;}
 public class Image:Graphic {}
 public class Selectable:UnityEngine.Component {public enum Transition{None,ColorTint,SpriteSwap,Animation}}
 public class Button:Selectable {
  public Transition transition;public Graphic targetGraphic;public bool interactable=true,active=true,groupInteractable=true;
  public bool IsInteractable(){return interactable&&groupInteractable;}public bool IsActive(){return active;}
 }
}
namespace UnityEngine.EventSystems {
 public class PointerEventData {}
 public interface IPointerEnterHandler {void OnPointerEnter(PointerEventData e);}
 public interface IPointerExitHandler {void OnPointerExit(PointerEventData e);}
}
public class PauseMenuButtonHover:UnityEngine.MonoBehaviour {}
public static class ZeldaUiPalette {public static UnityEngine.Color Ghost=new UnityEngine.Color(.157f,.51f,.824f,1);}
public static class HoverChecks {
 static int checks;
 static void Check(bool value,string message){checks++;if(!value)throw new System.Exception(message);}
 static UnityEngine.UI.Button Make(){var go=new UnityEngine.GameObject("Button",typeof(UnityEngine.UI.Image),typeof(UnityEngine.UI.Button));var b=go.GetComponent<UnityEngine.UI.Button>();b.targetGraphic=go.GetComponent<UnityEngine.UI.Image>();b.targetGraphic.color=new UnityEngine.Color(.03f,.12f,.2f,1);return b;}
 static void Invoke(ButtonHoverHighlight e,string name){typeof(ButtonHoverHighlight).GetMethod(name,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(e,null);}
 public static int Run(){
  ButtonHoverHighlight.AttachIfMissing(null);
  foreach(var transition in new[]{UnityEngine.UI.Selectable.Transition.ColorTint,UnityEngine.UI.Selectable.Transition.SpriteSwap,UnityEngine.UI.Selectable.Transition.Animation}){
   var existing=Make();existing.transition=transition;ButtonHoverHighlight.AttachIfMissing(existing);
   Check(existing.gameObject.children.Count==0&&existing.GetComponent<ButtonHoverHighlight>()==null,"Preserve existing native transitions");
  }
  var custom=Make();custom.gameObject.AddComponent<PauseMenuButtonHover>();ButtonHoverHighlight.AttachIfMissing(custom);
  Check(custom.GetComponent<ButtonHoverHighlight>()==null,"Preserve existing custom hover");
  var tab=Make();tab.transition=UnityEngine.UI.Selectable.Transition.ColorTint;
  var tabGraphic=tab.targetGraphic;var tabColor=tabGraphic.color;
  ButtonHoverHighlight.AttachOverlay(tab);ButtonHoverHighlight.AttachOverlay(tab);
  Check(tab.gameObject.children.Count==1,"Top tab gets one independent hover layer");
  Check(tab.transition==UnityEngine.UI.Selectable.Transition.ColorTint&&tab.targetGraphic==tabGraphic,"Top tab native pressed/selected feedback unchanged");
  var tabEffect=tab.GetComponent<ButtonHoverHighlight>();var tabLayer=tab.gameObject.children[0].GetComponent<UnityEngine.UI.Image>();
  UnityEngine.Time.unscaledDeltaTime=.12f;tabEffect.OnPointerEnter(null);Invoke(tabEffect,"LateUpdate");
  Check(UnityEngine.Mathf.Approximately(tabLayer.color.a,.18f),"Top tab brightens on pointer enter");
  tabGraphic.color=new UnityEngine.Color(.18f,.4f,.5f,1);Invoke(tabEffect,"LateUpdate");
  Check(tabGraphic.color.r==.18f&&tabLayer.color.a>.17f,"Selected tab retains both base and hover feedback");
  tabEffect.OnPointerExit(null);Invoke(tabEffect,"LateUpdate");
  Check(tabLayer.color.a==0&&tabGraphic.color.r==.18f,"Pointer exit preserves active page colour");
  var button=Make();var originalGraphic=button.targetGraphic;var originalColor=originalGraphic.color;
  ButtonHoverHighlight.AttachIfMissing(button);ButtonHoverHighlight.AttachIfMissing(button);
  Check(button.gameObject.children.Count==1,"No duplicate overlay");
  var effect=button.GetComponent<ButtonHoverHighlight>();var layer=button.gameObject.children[0].GetComponent<UnityEngine.UI.Image>();
  Check(layer.color.a==0,"Initially invisible");Check(!layer.raycastTarget,"Overlay cannot block clicks or drags");
  Check(layer.transform.first,"Below text/icons/borders");
  Check(button.targetGraphic==originalGraphic,"Native button target retained");
  Check(button.transition==UnityEngine.UI.Selectable.Transition.None,"Native transition unchanged");
  var rect=(UnityEngine.RectTransform)layer.transform;
  Check(rect.anchorMin.Equals(UnityEngine.Vector2.zero)&&rect.anchorMax.Equals(UnityEngine.Vector2.one),"Follows button dimensions");
  UnityEngine.Time.timeScale=0;UnityEngine.Time.unscaledDeltaTime=.06f;
  effect.OnPointerEnter(null);Invoke(effect,"LateUpdate");Check(UnityEngine.Mathf.Approximately(layer.color.a,.09f),"Smooth half fade while paused");
  Invoke(effect,"LateUpdate");Check(UnityEngine.Mathf.Approximately(layer.color.a,.18f),"Full hover in 120 ms");
  Check(originalGraphic.color.Equals(originalColor),"Data-driven background preserved");
  originalGraphic.color=new UnityEngine.Color(.4f,.75f,.8f,1);Invoke(effect,"LateUpdate");
  Check(originalGraphic.color.r==.4f,"Refresh/selection colours not overwritten");
  effect.OnPointerExit(null);Invoke(effect,"LateUpdate");Check(UnityEngine.Mathf.Approximately(layer.color.a,.09f),"Smooth exit");
  Invoke(effect,"LateUpdate");Check(layer.color.a==0,"Exit clears hover");
  effect.OnPointerEnter(null);Invoke(effect,"LateUpdate");button.interactable=false;Invoke(effect,"LateUpdate");Check(layer.color.a==0,"Disabled upgrade/locked node not highlighted");
  button.interactable=true;Invoke(effect,"LateUpdate");Check(layer.color.a>0,"Reenabled while pointer remains inside");
  button.groupInteractable=false;Invoke(effect,"LateUpdate");Check(layer.color.a==0,"Parent CanvasGroup respected");
  button.groupInteractable=true;Invoke(effect,"LateUpdate");button.active=false;Invoke(effect,"LateUpdate");Check(layer.color.a==0,"Inactive button does not glow");
  button.active=true;Invoke(effect,"LateUpdate");Invoke(effect,"OnDisable");Check(layer.color.a==0,"Closing menu clears highlight immediately");
  Invoke(effect,"LateUpdate");Check(layer.color.a==0,"Reopening cannot retain stale pointer state");
  effect.OnPointerEnter(null);Invoke(effect,"LateUpdate");effect.OnPointerExit(null);effect.OnPointerEnter(null);Invoke(effect,"LateUpdate");
  Check(layer.color.a<=.180001f,"Rapid pointer changes are bounded");
  return checks;
 }
}
'@
Add-Type -TypeDefinition ($taskSource + $taskHarness)
$checks = [HoverChecks]::Run()
$menu = Get-Content -Raw "$taskRoot/Assets/Scripts/UI/TabJournalMenuController.cs"
if (([regex]::Matches($menu, 'ButtonHoverHighlight.AttachIfMissing')).Count -ne 2) { throw 'Only the equipment-slot and skill-button factories should be modified.' }
if (([regex]::Matches($menu, 'ButtonHoverHighlight.AttachOverlay')).Count -ne 1) { throw 'Explicit additional hover should only be attached by the top-tab factory.' }
Write-Output "PASS: $checks production hover behaviour checks plus scoped integration check (Unity stubs)."
