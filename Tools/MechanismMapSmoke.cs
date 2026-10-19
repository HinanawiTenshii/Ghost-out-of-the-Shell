using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class MechanismMapSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static IEnumerator steps; static int checks, frame=-1; static double deadline;
    static MechanismMapSmoke() { EditorApplication.update += Tick; }
    static void Check(bool ok,string message) { checks++; if(!ok)throw new Exception(message); }
    static object Get(object o,string n) => o.GetType().GetField(n,F).GetValue(o);
    static void Set(object o,string n,object v) => o.GetType().GetField(n,F).SetValue(o,v);
    static object Call(object o,string n,params object[] v) => o.GetType().GetMethod(n,F,null,Array.ConvertAll(v,x=>x.GetType()),null).Invoke(o,v);
    public static void Run()
    {
        Check(Application.dataPath.Replace('\\','/').Contains("/.codex-temp/"),"isolated project only");
        PlayerSettings.productName="mechanism-map-smoke";
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),"Assets/MechanismMapFixture.unity");
        SessionState.SetBool(nameof(MechanismMapSmoke),true); EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if(!EditorApplication.isPlaying || frame==Time.frameCount)return;frame=Time.frameCount;
        try {
            if(SessionState.GetBool(nameof(MechanismMapSmoke),false)) {
                SessionState.SetBool(nameof(MechanismMapSmoke),false);steps=Verify();deadline=EditorApplication.timeSinceStartup+90;
            }
            if(steps==null)return;
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("timeout");
            if(steps.MoveNext())return;
            steps=null;Debug.Log("MECHANISM_MAP_PASS checks="+checks);EditorApplication.Exit(0);
        } catch(Exception e) { steps=null;Debug.LogException(e);EditorApplication.Exit(1); }
    }
    static IEnumerator Verify()
    {
        var manager=MapPointOfInterestManager.GetOrCreate();manager.ResetAllPoints();
        var paths=new[]{"GroundLever","Decorations/ElectricGatePillar","Decorations/AlchemyWorkshop/Alchemy-Cauldron",
            "Decorations/BatterySocket","Decorations/WaterwayGate","Decorations/WaterwayGateController",
            "Decorations/Fortress/Fortress-Ballista","Decorations/Glass"};
        var shapes=new[]{MapPointIconShape.Lever,MapPointIconShape.ElectricGate,MapPointIconShape.Cauldron,
            MapPointIconShape.BatterySocket,MapPointIconShape.WaterwayGate,MapPointIconShape.WaterwayController,
            MapPointIconShape.Ballista,MapPointIconShape.BreakableGlass};
        var points=new List<MapPointOfInterest>();
        for(int i=0;i<paths.Length;i++) {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/"+paths[i]+".prefab");
            Check(asset!=null,"prefab "+paths[i]);
            var go=Object.Instantiate(asset,new Vector3(i*8,15,0),Quaternion.identity);go.name="Mechanism "+i;
            var p=go.GetComponent<MapPointOfInterest>();
            Check(p!=null && p.IconShape==shapes[i],"automatic marker on old prefab "+paths[i]);
            Check(!manager.IsDiscovered(p.PointId),"unseen mechanism hidden");
            p.ForceDiscover();Check(manager.TryGetPoint(p.PointId,out var record),"record discovered");
            Check(!record.CanBeMarked && !manager.ToggleMarked(p.PointId) && record.TooltipText==p.Title,"hover-only localized name");
            go.SetActive(false);Check(manager.TryGetPoint(p.PointId,out record)&&record.VisibleOnMap,"streaming preserves discovered marker");
            go.SetActive(true);Check(go.GetComponents<MapPointOfInterest>().Length==1,"no duplicate attachment");points.Add(p);
        }
        var duplicate=Object.Instantiate(points[3].gameObject);duplicate.transform.position+=Vector3.right;
        Check(duplicate.GetComponent<MapPointOfInterest>().PointId!=points[3].PointId,"prefab instances have distinct IDs");
        Object.Destroy(duplicate);
        var glass=points[7].GetComponent<DoorData>(); string glassId=points[7].PointId;
        glass.ReceiveAttack(100); Check(manager.TryGetPoint(glassId,out var broken)&&!broken.VisibleOnMap,"broken glass disappears immediately");
        var save=JsonUtility.ToJson(new Save{records=manager.CaptureSaveState()});manager.ResetAllPoints();
        manager.ApplySaveState(JsonUtility.FromJson<Save>(save).records);
        Check(manager.TryGetPoint(glassId,out broken)&&!broken.VisibleOnMap,"broken state survives serialized save");
        var reloadGlass=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Decorations/Glass.prefab")).GetComponent<MapPointOfInterest>();
        Set(reloadGlass,"resolvedPointId",glassId);manager.Register(reloadGlass);
        Check(manager.TryGetPoint(glassId,out broken)&&!broken.VisibleOnMap,"scene re-registration cannot resurrect broken glass icon");Object.Destroy(reloadGlass.gameObject);
        foreach(var p in points) if(p!=null && p.IconShape!=MapPointIconShape.BreakableGlass)
            Check(manager.TryGetPoint(p.PointId,out var record)&&record.VisibleOnMap,"discovery survives reload");
        manager.UpdateElectricField("test-link",SceneManager.GetActiveScene().name,new Vector2(0,5),new Vector2(5,5),true,false);
        Check(!manager.TryGetPoint("test-link",out _),"unseen current not revealed");
        manager.UpdateElectricField("test-link",SceneManager.GetActiveScene().name,new Vector2(0,5),new Vector2(5,5),true,true);
        Check(manager.TryGetPoint("test-link",out var link)&&link.IsLineMarker&&link.TooltipText=="电流区","current geometry and hover label saved");
        int events=0;manager.PointsChanged+=()=>events++;
        for(int i=0;i<30;i++)manager.UpdateElectricField("test-link",SceneManager.GetActiveScene().name,new Vector2(0,5),new Vector2(5,5),true,false);
        Check(events==0,"unchanged current produces no UI rebuild events");
        manager.UpdateElectricField("test-link",SceneManager.GetActiveScene().name,link.LineStart,link.LineEnd,false,false);
        Check(!link.VisibleOnMap&&events==1,"power loss hides current once");
        manager.UpdateElectricField("test-link",SceneManager.GetActiveScene().name,link.LineStart,link.LineEnd,true,false);
        Check(link.VisibleOnMap&&events==2,"restored power shows known link");
        // Actual existing door prefab: names come from the persistent key binding, not its live object.
        var doorAsset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Decorations/DoorHinge.prefab");
        var keyIdentity=ScriptableObject.CreateInstance<PickupItemPersistentIdentity>();Set(keyIdentity,"displayName","骑士长的钥匙");
        var keyDoor=Object.Instantiate(doorAsset,new Vector3(0,8,0),Quaternion.identity).GetComponent<DoorHingeInteraction>();
        var passwordDoor=Object.Instantiate(doorAsset,new Vector3(16,8,0),Quaternion.identity).GetComponent<DoorHingeInteraction>();
        Set(keyDoor,"requiredUnlockItemIdentity",keyIdentity);Set(keyDoor,"isLocked",true);Set(keyDoor,"passwordLockEnabled",false);
        Set(passwordDoor,"isLocked",true);Set(passwordDoor,"passwordLockEnabled",true);Set(passwordDoor,"doorPassword","7028");
        yield return null;
        Check(keyDoor.RequiredUnlockItemDisplayName=="骑士长的钥匙","cached real key binding name");
        var keyMarker=keyDoor.GetComponent<LockedDoorMapMarker>();var passwordMarker=passwordDoor.GetComponent<LockedDoorMapMarker>();
        string keyId=(string)Get(keyMarker,"pointId"),passwordId=(string)Get(passwordMarker,"pointId");
        Check(!manager.IsDiscovered(keyId)&&!manager.IsDiscovered(passwordId),"unseen doors remain hidden");
        manager.UpdateLockedDoor(keyId,SceneManager.GetActiveScene().name,new Vector2(0,8),new Vector2(6,8),Color.red,true,true);
        manager.UpdateLockedDoor(passwordId,SceneManager.GetActiveScene().name,new Vector2(16,8),new Vector2(22,8),Color.red,true,true);
        keyMarker.RefreshLockState();passwordMarker.RefreshLockState();
        manager.TryGetPoint(keyId,out var keyRecord);manager.TryGetPoint(passwordId,out var passwordRecord);
        Check(keyRecord.IconShape==MapPointIconShape.KeyLockedDoor&&!keyRecord.IsLineMarker,"key door uses badge not old line");
        Check(passwordRecord.IconShape==MapPointIconShape.PasswordLockedDoor&&!passwordRecord.IsLineMarker,"password door uses distinct badge");
        Check(keyRecord.TooltipText=="锁住的门（钥匙）\n需要：骑士长的钥匙","key name on hover");
        Check(passwordRecord.TooltipText=="锁住的门（密码）"&&!passwordRecord.TooltipText.Contains("7028"),"password is never disclosed");
        Check(!keyRecord.CanBeMarked&&!passwordRecord.CanBeMarked,"locks remain hover-only");
        int beforeRefresh=events;for(int i=0;i<30;i++){keyMarker.RefreshLockState();passwordMarker.RefreshLockState();}
        Check(events==beforeRefresh,"unchanged door names do not trigger menu rebuilds");
        keyDoor.gameObject.SetActive(false);passwordDoor.gameObject.SetActive(false);
        save=JsonUtility.ToJson(new Save{records=manager.CaptureSaveState()});manager.ResetAllPoints();manager.ApplySaveState(JsonUtility.FromJson<Save>(save).records);
        manager.TryGetPoint(keyId,out keyRecord);manager.TryGetPoint(passwordId,out passwordRecord);
        Check(keyRecord.TooltipText.EndsWith("骑士长的钥匙")&&keyRecord.VisibleOnMap,"serialized key name survives unload and load");
        Check(passwordRecord.IconShape==MapPointIconShape.PasswordLockedDoor,"serialized password type survives unload and load");
        var data=ScriptableObject.CreateInstance<RuntimeMiniMapSceneData>();
        data.Configure(SceneManager.GetActiveScene().name,"地图测试",new[]{new[]{new Vector2(-10,-10),new Vector2(70,30)}},new RuntimeMiniMapTransitionData[0],new RuntimeMiniMapQuestTargetData[0]);
        var hud=new GameObject("HUD",typeof(RectTransform),typeof(CanvasRenderer),typeof(RuntimeMiniMapGraphic)).GetComponent<RuntimeMiniMapGraphic>();
        hud.rectTransform.sizeDelta=new Vector2(320,180);hud.RebuildFromSnapshot(data);hud.SetCameraMarkerVisible(false);
        using(var vh=new VertexHelper()) {
            Call(hud,"PopulateMarkers",vh);int full=vh.currentVertCount;Check(full>100,"HUD draws all discovered mechanism glyphs");
            hud.SetDrawPointIcons(false);Call(hud,"PopulateMarkers",vh);Check(vh.currentVertCount<full,"journal suppresses duplicate shared icons but keeps current line");
            Check(vh.currentVertCount>0,"current line remains shared by both maps");
        }
        var group=new GameObject("Group").AddComponent<LevelSceneGroup>();
        var entry=new LevelSceneGroup.SceneEntry();Set(entry,"sceneName",SceneManager.GetActiveScene().name);
        Set(group,"scenes",new List<LevelSceneGroup.SceneEntry>{entry});group.Register();
        var root=new GameObject("Menu",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster));root.SetActive(false);
        root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
        var menu=root.AddComponent<TabJournalMenuController>();Set(menu,"menuFont",AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/ZLabsPixel_12px_M_CN.ttf"));root.SetActive(true);
        yield return null;while(DocumentReader.IsInputBlocked)yield return null;
        menu.OpenMenu();var map=(RuntimeMiniMapGraphic)Get(menu,"miniMapGraphic");map.RebuildFromSnapshot(data);
        Call(menu,"RebuildMiniMapPoints");Call(menu,"UpdateMiniMapPointPositions");Canvas.ForceUpdateCanvases();
        var layer=(RectTransform)Get(menu,"mapPointLayer");var popup=(GameObject)Get(menu,"mapPointTooltipRoot");
        Check(!popup.activeSelf,"no automatic names");
        var pointer=new PointerEventData(EventSystem.current);
        foreach(var ui in layer.GetComponentsInChildren<MapPointOfInterestUiInteraction>()) {
            string id=(string)Get(ui,"pointId");if(!manager.TryGetPoint(id,out var record))continue;
            ui.OnPointerEnter(pointer);Check(popup.activeSelf&&((Text)Get(menu,"mapPointTooltipText")).text==record.TooltipText,"hover shows exact name "+record.Title);
            ui.OnPointerClick(pointer);Check(!record.Marked,"mechanism clicks do not mark");
            if(ui.GetComponent<RuntimeMiniMapPointIconGraphic>() is RuntimeMiniMapPointIconGraphic icon)Check((bool)Get(icon,"hovered"),"glyph hover feedback");
            if(record.IconShape==MapPointIconShape.KeyLockedDoor) {
                var text=(Text)Get(menu,"mapPointTooltipText");var rect=(RectTransform)Get(menu,"mapPointTooltipRect");Canvas.ForceUpdateCanvases();
                Check(rect.sizeDelta.y>=56&&text.rectTransform.rect.height>=text.preferredHeight,"key name fits hover window");
                Check(ui.GetComponent<RuntimeMiniMapPointIconGraphic>()!=null,"door uses shared map glyph and icon hit target");
            }
            ui.OnPointerExit(pointer);Check(!popup.activeSelf,"hover exit hides popup");
        }
        menu.CloseMenu();Object.Destroy(root);Object.Destroy(hud.gameObject);Object.Destroy(data);
        keyDoor.SetLocked(false);Check(!keyRecord.VisibleOnMap,"unlock hides badge immediately");
        keyDoor.SetLocked(true);Check(keyRecord.VisibleOnMap,"relock restores known badge");
        passwordMarker.NotifyLeafDestroyed();Check(!passwordRecord.VisibleOnMap,"break hides password badge immediately");
        Object.Destroy(keyDoor.gameObject);Object.Destroy(passwordDoor.gameObject);Object.Destroy(keyIdentity);
        yield return null;
        // Real visibility: the camera rectangle alone must not reveal through Blocks.
        var camera=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>();camera.tag="MainCamera";
        camera.orthographic=true;camera.orthographicSize=7;camera.transform.position=new Vector3(0,0,-10);
        var player=new GameObject("Player");player.AddComponent<SwordZeldaCharacterData>();var mover=player.AddComponent<ZeldaFourWayMover>();
        ZeldaRuntimeRegistry.ClaimControlledMover(mover);
        var vision=camera.gameObject.AddComponent<CameraCircularVision>();Set(vision,"visionRadius",7f);Set(vision,"blockLayers",(LayerMask)LayerMask.GetMask("Blocks"));
        var hidden=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Decorations/BatterySocket.prefab"),new Vector3(4,0,0),Quaternion.identity);
        var wall=new GameObject("Blocker",typeof(BoxCollider2D));wall.layer=LayerMask.NameToLayer("Blocks");wall.transform.position=new Vector3(2,0,0);wall.GetComponent<BoxCollider2D>().size=new Vector2(.5f,8);
        Physics2D.SyncTransforms();Call(vision,"UpdateVision",true);
        var hiddenPoint=hidden.GetComponent<MapPointOfInterest>();
        Check(!(bool)Call(hiddenPoint,"IsVisibleToPlayer",camera),"Blocks occlusion prevents discovery");
        wall.SetActive(false);Physics2D.SyncTransforms();Call(vision,"UpdateVision",true);
        Check((bool)Call(hiddenPoint,"IsVisibleToPlayer",camera),"removing blocker reveals mechanism");
        Set(hiddenPoint,"nextVisibilityCheckTime",0f);Call(hiddenPoint,"Update");Check(manager.IsDiscovered(hiddenPoint.PointId),"normal update records discovery");
        var pillarAsset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Decorations/ElectricGatePillar.prefab");
        var a=Object.Instantiate(pillarAsset,new Vector3(-3,-2,0),Quaternion.identity).GetComponent<ElectricGatePillar>();
        var b=Object.Instantiate(pillarAsset,new Vector3(3,-2,0),Quaternion.identity).GetComponent<ElectricGatePillar>();
        Set(a,"hasPanel",false);Set(b,"hasPanel",false);Physics2D.SyncTransforms();
        var network=(ElectricGateNetwork)typeof(ElectricGateNetwork).GetField("instance",F).GetValue(null);Set(network,"nextPairing",0f);Call(network,"FixedUpdate");
        MapPointOfInterestRecord liveLink=null;
        foreach(var record in manager.GetDiscoveredPoints(SceneManager.GetActiveScene().name))
            if(record.IconShape==MapPointIconShape.ElectricField && record.Id!="test-link")liveLink=record;
        Check(liveLink!=null && liveLink.VisibleOnMap && liveLink.LineStart.y==-2 && liveLink.LineEnd.y==-2,"real paired network publishes visible current");
        Set(a,"hasPanel",true);
        var supply=points[3].GetComponent<BatterySocket>();supply.ApplyRuntimeState(new BatterySocket.RuntimeState());supply.BindMechanism(a);
        Call(network,"FixedUpdate");Check(!liveLink.VisibleOnMap,"real empty battery slot hides current");
        supply.ApplyRuntimeState(new BatterySocket.RuntimeState {installed=true});
        Call(network,"FixedUpdate");Check(liveLink.VisibleOnMap,"real battery insertion restores discovered current");
        var empty=supply.CaptureRuntimeState();empty.battery.itemState=BatteryPickupItem.EncodeCharge(0,BatteryPickupItem.DefaultCapacity);
        supply.ApplyRuntimeState(empty);Call(network,"FixedUpdate");Check(!liveLink.VisibleOnMap,"exhausted battery also hides current");
        Object.Destroy(a.gameObject);Object.Destroy(b.gameObject);
        Object.Destroy(camera.gameObject);Object.Destroy(player);Object.Destroy(hidden);Object.Destroy(wall);
        foreach(var p in points)if(p!=null)Object.Destroy(p.gameObject);
        yield return null;RenderIcons();
    }
    [Serializable] public sealed class Save { public List<MapPointOfInterestRecord> records; }
    static void RenderIcons()
    {
        var canvas=new GameObject("Icon preview",typeof(RectTransform),typeof(Canvas)).GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
        var shapes=new[]{MapPointIconShape.SceneTransition,MapPointIconShape.Lever,MapPointIconShape.ElectricGate,MapPointIconShape.Cauldron,
            MapPointIconShape.BatterySocket,MapPointIconShape.WaterwayGate,MapPointIconShape.WaterwayController,MapPointIconShape.Ballista,MapPointIconShape.BreakableGlass,
            MapPointIconShape.KeyLockedDoor,MapPointIconShape.PasswordLockedDoor,MapPointIconShape.LockedDoor};
        var names=new[]{"场景切换","拉杆","电门 / 电流区","炼药锅","电池槽","水闸","水闸控制器","弩炮","可打破玻璃","钥匙门","密码门","旧记录兼容图标"};
        for(int i=0;i<shapes.Length;i++) {
            Vector2 c=new Vector2((i%3-1)*300,315-i/3*210);
            foreach(float size in new[]{110f,25f}) {
                var icon=new GameObject(names[i],typeof(RectTransform),typeof(CanvasRenderer),typeof(RuntimeMiniMapPointIconGraphic)).GetComponent<RuntimeMiniMapPointIconGraphic>();
                icon.transform.SetParent(canvas.transform,false);icon.rectTransform.anchoredPosition=c+new Vector2(size>30?-22:77,12);
                icon.rectTransform.sizeDelta=Vector2.one*size;icon.color=i>=9?(Color)new Color32(174,100,46,255):new Color32(91,211,241,255);icon.Configure(shapes[i],false);
                using(var vh=new VertexHelper()){Call(icon,"OnPopulateMesh",vh);Check(vh.currentVertCount>10&&vh.currentVertCount<200,"compact glyph mesh "+shapes[i]);}
            }
            var label=new GameObject("Label",typeof(RectTransform),typeof(Text)).GetComponent<Text>();label.transform.SetParent(canvas.transform,false);
            label.rectTransform.anchoredPosition=c+Vector2.down*70;label.rectTransform.sizeDelta=new Vector2(290,40);
            label.font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/ZLabsPixel_12px_M_CN.ttf");label.fontSize=22;label.text=names[i];label.alignment=TextAnchor.MiddleCenter;label.color=ZeldaUiPalette.Primary;
        }
        var camera=new GameObject("Render Camera",typeof(Camera)).GetComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.orthographicSize=455;
        camera.transform.position=new Vector3(0,0,-100);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color32(8,19,34,255);canvas.worldCamera=camera;
        var rt=new RenderTexture(1000,910,24);camera.targetTexture=rt;Canvas.ForceUpdateCanvases();camera.Render();
        var previous=RenderTexture.active;RenderTexture.active=rt;var pixels=new Texture2D(1000,910,TextureFormat.RGB24,false);
        pixels.ReadPixels(new Rect(0,0,1000,910),0,0);pixels.Apply();
        File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(),"MechanismMap-icons.png"),pixels.EncodeToPNG());
        RenderTexture.active=previous;camera.targetTexture=null;rt.Release();Object.Destroy(rt);Object.Destroy(pixels);Object.Destroy(canvas.gameObject);Object.Destroy(camera.gameObject);
    }
}
