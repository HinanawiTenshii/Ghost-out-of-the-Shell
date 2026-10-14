$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$source=Get-Content -Raw "$root/Assets/Scripts/Interaction/SignpostInteraction.cs"
function Extract($sig){
 $start=$source.IndexOf($sig);if($start -lt 0){throw "Missing $sig"};$end=$source.IndexOf('{',$start)+1;$depth=1
 while($depth){if($source[$end] -eq '{'){$depth++};if($source[$end] -eq '}'){$depth--};$end++};$source.Substring($start,$end-$start)
}
$code=@'
using System;using System.Collections.Generic;
public struct Vector2 {public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}public static Vector2 zero=>new Vector2();public static Vector2 one=>new Vector2(1,1);}
public struct Vector3 {public float x,y,z;public Vector3(float x,float y,float z=0){this.x=x;this.y=y;this.z=z;}public static Vector3 one=>new Vector3(1,1,1);}
public struct Quaternion {public float angle;public static Quaternion identity=>new Quaternion();}
public class Transform {
 public GameObject gameObject;public Transform parent;public Vector3 localScale=Vector3.one,position;public Quaternion rotation;
 public List<Transform> children=new List<Transform>();public Vector3 lossyScale=>parent==null?localScale:new Vector3(localScale.x*parent.lossyScale.x,localScale.y*parent.lossyScale.y,localScale.z*parent.lossyScale.z);
 public void SetParent(Transform p,bool stays){parent?.children.Remove(this);parent=p;p?.children.Add(this);}
 public void SetAsFirstSibling(){if(parent!=null){parent.children.Remove(this);parent.children.Insert(0,this);}}
}
public class RectTransform:Transform {public Vector2 anchorMin,anchorMax,offsetMin,offsetMax;}
public class GameObject {
 public string name;public int scene=1;public bool activeSelf=true,destroyed;public RectTransform transform;public List<object> components=new List<object>();
 public GameObject(string n=""){name=n;transform=new RectTransform{gameObject=this};}
 public void SetActive(bool b){activeSelf=b;}
 public T AddComponent<T>() where T:Component,new(){var c=new T{gameObject=this};components.Add(c);if(c is TextMesh)AddComponent<MeshRenderer>();return c;}
 public T GetComponent<T>() where T:class {foreach(var c in components)if(c is T t)return t;return null;}
}
public class Component {public GameObject gameObject;}
public class TextMesh:Component {public string text;public int fontSize;public float characterSize;public Font font;public TextAnchor anchor;public TextAlignment alignment;public Color color;}
public class MeshRenderer:Component {public Material sharedMaterial;public int sortingOrder;}
public enum TextAnchor {MiddleCenter}public enum TextAlignment {Center}public enum FontStyle {Normal}
public class Material {public object mainTexture;public bool destroyed;public Material(){}public Material(Material m){}}
public class Font {public Material material=new Material();public void RequestCharactersInTexture(string s,int n,FontStyle f){}}
public struct Color {public float r,g,b,a;public Color(float r,float g,float b,float a){this.r=r;this.g=g;this.b=b;this.a=a;}}
public class Image:Component {public Color color;public bool raycastTarget=true;}
public static class Mathf {public static float Clamp01(float x)=>Math.Max(0,Math.Min(1,x));}
public static class DocumentReader {public static bool IsInputBlocked;}
public static class ZeldaUiPalette {public static Color Primary=>new Color(0,1,1,1);}
public static class ZeldaPossessionProgressBar {public static void ConfigureOverlayRenderer(MeshRenderer r){}}
public class ZeldaFourWayMover {public Vector3 head=new Vector3(5,6);public Vector3 GetOverheadWorldPosition(Vector2 p)=>new Vector3(head.x,head.y+p.y);}
namespace UnityEngine.SceneManagement {public static class SceneManager {public static void MoveGameObjectToScene(GameObject g,int scene){g.scene=scene;}}}
public class SignPresentation {
 public GameObject gameObject=new GameObject(),promptObject,windowObject=new GameObject();public Transform transform=>gameObject.transform;
 public bool singleNotice;public float backgroundDimOpacity=.6f;public TextMesh promptText;public Material promptMaterial;public ZeldaFourWayMover mover=new ZeldaFourWayMover();
 private Font font=new Font();private Font ResolveFont()=>font;private ZeldaFourWayMover GetNearbyCharacter()=>mover;
 private void CloseWindow(){windowObject?.SetActive(false);}
 private static void Destroy(GameObject g){g.destroyed=true;}private static void Destroy(Material m){m.destroyed=true;}
 private static RectTransform MakeRect(string name,Transform parent,Vector2 position,Vector2 size){var o=new GameObject(name);o.transform.SetParent(parent,false);return o.transform;}
 public void Prompt(bool visible)=>SetPromptVisible(visible);public void Backdrop()=>BuildReadingBackdrop();public void Disable()=>OnDisable();public void Dispose()=>OnDestroy();
'@
foreach($sig in @('    private void SetPromptVisible(', '    private void BuildReadingBackdrop()', '    private void OnDisable()', '    private void OnDestroy()')){$code+=Extract $sig}
$code+=@'
}
public static class PresentationTests {
 static int checks;static void Check(bool b,string m){checks++;if(!b)throw new Exception(m);}
 public static int Run(){
  foreach(bool notice in new[]{false,true}){
   var s=new SignPresentation{singleNotice=notice};s.gameObject.scene=7;
   var ancestor=new GameObject();s.transform.SetParent(ancestor.transform,false);
   foreach(float scale in new[]{.02f,.3f,1f,5f,-2f,0f}){
    ancestor.transform.localScale=new Vector3(scale,scale*3,scale);s.transform.localScale=new Vector3(scale,scale/2,1);s.transform.rotation=new Quaternion{angle=45};
    s.Prompt(true);var t=s.promptObject.transform;
    Check(t.parent==null&&t.lossyScale.x==1&&t.lossyScale.y==1&&t.rotation.angle==0,"Prompt ignores nested scale, zero scale, mirroring and sign rotation");
    Check(t.position.x==5&&Math.Abs(t.position.y-6.9f)<.001&&s.promptObject.scene==7,"Prompt stays over reader in owning scene");
    Check(s.promptText.text==(notice?"按[E]阅读告示牌":"按[E]查看路牌"),"Both sign modes retain correct prompt");
   }
   s.mover.head=new Vector3(9,10);s.Prompt(true);Check(s.promptObject.transform.position.x==9,"Prompt follows moving reader, not sign");
   DocumentReader.IsInputBlocked=true;s.Prompt(true);Check(!s.promptObject.activeSelf,"Opening a reading modal hides prompt");DocumentReader.IsInputBlocked=false;
   s.Backdrop();var backdrop=s.windowObject.transform.children[0];var image=backdrop.gameObject.GetComponent<Image>();
   Check(image.color.r==0&&image.color.g==0&&image.color.b==0&&Math.Abs(image.color.a-.6f)<.001,"Reading dims background by configured opacity");
   var rect=(RectTransform)backdrop;Check(rect.anchorMin.x==0&&rect.anchorMin.y==0&&rect.anchorMax.x==1&&rect.anchorMax.y==1&&rect.offsetMin.x==0&&rect.offsetMax.y==0,"Backdrop stretches to any canvas aspect ratio");
   Check(!image.raycastTarget&&backdrop.parent==s.windowObject.transform,"Backdrop lives behind content and shares modal lifetime");
   s.Prompt(true);s.Disable();Check(!s.windowObject.activeSelf&&!s.promptObject.activeSelf,"Disabling sign hides background and independent prompt");
   s.Dispose();Check(s.windowObject.destroyed&&s.promptObject.destroyed&&s.promptMaterial.destroyed,"Destroying sign cleans root prompt, font material and window");
  }
  foreach(float opacity in new[]{-1f,0f,1f,2f}){var s=new SignPresentation{backgroundDimOpacity=opacity};s.Backdrop();Check(s.windowObject.transform.children[0].gameObject.GetComponent<Image>().color.a==Mathf.Clamp01(opacity),"Opacity remains in valid range");}
  return checks;
 }
}
'@
Add-Type -TypeDefinition $code
$count=[PresentationTests]::Run()
$build=Extract '    private void EnsureWindowBuilt()'
if($build.IndexOf('BuildReadingBackdrop();') -lt 0 -or $build.IndexOf('BuildReadingBackdrop();') -gt $build.IndexOf('if (singleNotice)')){throw 'Both sign modes must create dimmer before foreground content'}
Write-Output "PASS: $count production prompt-transform, scene, dimmer and cleanup checks. Engine transforms/UI use substitutes; not a Unity screenshot test."
