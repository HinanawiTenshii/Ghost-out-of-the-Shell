$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$source = Get-Content -Raw "$root/Assets/Scripts/Map/MapPointOfInterest.cs"
$interaction = [regex]::Match($source, '(?s)public sealed class MapPointOfInterestUiInteraction.*?(?=\[RequireComponent\(typeof\(CanvasRenderer\)\)\])').Value
$graphicMethods = [regex]::Match($source, '(?s)    public void Configure\(MapPointIconShape configuredShape, bool isMarked\).*?(?=    protected override void OnPopulateMesh)').Value
if (!$interaction -or !$graphicMethods) { throw 'UI methods not found' }
$stubs = @'
using System;
using UnityEngine;
using UnityEngine.EventSystems;
namespace UnityEngine {
    public class Transform {}
    public class RectTransform:Transform {}
    public class MonoBehaviour {
        public Transform transform = new RectTransform();
        public RuntimeMiniMapPointIconGraphic graphic;
        public T GetComponent<T>() where T:class{return graphic as T;}
    }
}
namespace UnityEngine.EventSystems {
    public interface IPointerEnterHandler {void OnPointerEnter(PointerEventData e);}
    public interface IPointerExitHandler {void OnPointerExit(PointerEventData e);}
    public interface IPointerClickHandler {void OnPointerClick(PointerEventData e);}
    public class PointerEventData {public enum InputButton {Left,Right,Middle} public InputButton button;}
}
public enum MapPointIconShape {Diamond,Circle,Square,Star,Exclamation,Lever}
public class MapPointOfInterestManager {
    public static int Clicks;
    public static MapPointOfInterestManager GetOrCreate(){return new MapPointOfInterestManager();}
    public void ToggleMarked(string id){Clicks++;}
}
'@
$graphic = @'
public class RuntimeMiniMapPointIconGraphic {
    private MapPointIconShape shape;
    private bool marked,hovered;
    public bool IsHovered {get{return hovered;}}
    public bool IsMarked {get{return marked;}}
    public void SetVerticesDirty(){}
'@
$tests = @'
public static class HoverRegression {
    static int checks;
    static void Check(bool b,string msg){checks++;if(!b)throw new Exception(msg);}
    public static int Run(){
        var icon=new RuntimeMiniMapPointIconGraphic();icon.Configure(MapPointIconShape.Lever,true);
        var ui=new MapPointOfInterestUiInteraction {graphic=icon}; int entered=0,exited=0;
        ui.Configure("lever",(id,rect)=>entered++,id=>exited++,true);
        Check(!icon.IsHovered && !icon.IsMarked,"Idle lever is normal-sized and unmarked, including legacy saves");
        var mouse=new PointerEventData();ui.OnPointerEnter(mouse);
        Check(icon.IsHovered && entered==1,"Hover enlarges lever and requests its label");
        ui.OnPointerClick(mouse);Check(MapPointOfInterestManager.Clicks==0 && icon.IsHovered,"Left click neither selects nor changes hovered state");
        mouse.button=PointerEventData.InputButton.Right;ui.OnPointerClick(mouse);
        Check(MapPointOfInterestManager.Clicks==0,"Right click is inert");
        ui.OnPointerExit(mouse);Check(!icon.IsHovered && exited==1,"Exit restores normal size and hides label");
        ui.OnPointerEnter(mouse);
        typeof(MapPointOfInterestUiInteraction).GetMethod("OnDisable",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(ui,null);
        Check(!icon.IsHovered && exited==2,"Closing/rebuilding map clears hover state");
        icon.Configure(MapPointIconShape.Diamond,false);ui.Configure("normal",null,null);
        ui.OnPointerEnter(mouse);Check(!icon.IsHovered,"Other map icons retain their original sizing");
        mouse.button=PointerEventData.InputButton.Left;ui.OnPointerClick(mouse);
        Check(MapPointOfInterestManager.Clicks==1,"Other map icons retain click-to-mark");
        return checks;
    }
}
'@
Add-Type -TypeDefinition ($stubs+"`n"+$interaction+"`n"+$graphic+"`n"+$graphicMethods+"`n}`n"+$tests)
Write-Output ("Lever hover UI: {0} assertions passed." -f [HoverRegression]::Run())
$menu=Get-Content -Raw "$root/Assets/Scripts/UI/TabJournalMenuController.cs"
if ($menu -notmatch 'mapPointTooltipText.text = point.TooltipText;' -or $source -notmatch 'if \(hovered\) radius \*= 1.3f;') {throw 'Missing name-only tooltip or visual enlargement'}
