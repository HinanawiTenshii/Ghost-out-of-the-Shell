using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class MortarRemoteSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static IEnumerator suite;
    static int frame = -1, checks;
    static double deadline;
    static MortarRemoteSmoke() { EditorApplication.update += Tick; }
    static void Check(bool ok, string label) { checks++; if (!ok) throw new Exception(label); }
    static void Set(object o, string n, object v) => o.GetType().GetField(n, F).SetValue(o, v);
    static object Call(object o, string n, params object[] a) => o.GetType().GetMethod(n, F).Invoke(o, a);
    static object Get(object o, string n) => o.GetType().GetField(n, F).GetValue(o);
    public static void Run()
    {
        Check(Application.dataPath.Replace('\\','/').Contains("/.codex-temp/"), "isolated project required");
        PlayerSettings.productName = "mortar-remote-smoke";
        var authored = EditorSceneManager.OpenScene("Assets/Scenes/Level2/Level2-Floor2.unity");
        int installed = 0;
        foreach (var root in authored.GetRootGameObjects())
            foreach (var station in root.GetComponentsInChildren<FortressMortarRemoteInstallation>(true))
                if (station.name == "臼炮") installed++;
        Check(installed == 1, "authored Floor2 mortar inherits installer without scene reserialization");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(nameof(MortarRemoteSmoke),true); EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return; frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(MortarRemoteSmoke),false))
            { SessionState.SetBool(nameof(MortarRemoteSmoke),false); suite = Verify(); deadline = EditorApplication.timeSinceStartup + 150; }
            if (suite == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("timeout");
            if (suite.MoveNext()) return;
            suite = null; Debug.Log("MORTAR_REMOTE_PASS checks="+checks); EditorApplication.Exit(0);
        }
        catch(Exception e) { suite=null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static CivilianZeldaCharacterData Actor(string name, Vector2 point)
    {
        var go = new GameObject(name); go.transform.position=point;
        var data=go.AddComponent<CivilianZeldaCharacterData>();
        var state=data.CaptureRuntimeState(); state.health=20; state.currentHealth=20; data.ApplyRuntimeState(state);
        var collider=go.AddComponent<BoxCollider2D>(); collider.size=Vector2.one*.2f;
        return data;
    }
    static IEnumerator Verify()
    {
        while(DocumentReader.IsInputBlocked) yield return null;
        var floor=SceneManager.CreateScene("Level2-Floor2"); SceneManager.SetActiveScene(floor);
        new GameObject("Test listener", typeof(AudioListener));
        var mortar=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Decorations/Fortress/Fortress-Mortar.prefab"))
            .GetComponent<FortressMortarRemoteInstallation>();
        var player=Actor("Operator",new Vector2(0,-2)); var mover=player.gameObject.AddComponent<ZeldaFourWayMover>();
        mover.ReceiveControl(Vector2.up); ZeldaRuntimeRegistry.ClaimControlledMover(mover);
        var bag=new GameObject("Bag"); bag.SetActive(false); var inv=bag.AddComponent<PersistentInventory>(); Set(inv,"saveToPlayerPrefs",false);
        bag.SetActive(true); inv.enabled=false; inv.ClearAll();
        var journal = QuestJournalManager.GetOrCreate(); journal.ResetAllEntries();
        journal.AddOrUpdateEntry(QuestJournalManager.Level2SandwormQuestId, QuestJournalManager.Level2SandwormQuestTitle, "hunt");
        journal.AddOrUpdateEntry(QuestJournalManager.SandwormMortarQuestId, QuestJournalManager.SandwormMortarTitle, QuestJournalManager.SandwormMortarDetails);
        var kit=inv.ResolveItemPrefab(MortarControlDevicePickupItem.DefaultItemId);
        var remote=inv.ResolveItemPrefab(MortarRemotePickupItem.DefaultItemId) as MortarRemotePickupItem;
        Check(kit!=null && remote!=null && kit.MaxStackSize==1 && remote.MaxStackSize==1,"both prefabs cached, unstackable");
        Check(!kit.CanUseFromInventory && kit.RequiredUseSkill==0 && remote.RequiredUseSkill==0,"passive kit, default zero skill requirement");
        Check(kit.ItemVisual.InventoryIcon!=null && remote.ItemVisual.InventoryIcon!=null,"both pixel icons render");
        Check(!mortar.TryInstall(),"missing kit rejected");
        for(int i=0;i<5;i++) inv.SetSlot(i,"gold_coin",1);
        inv.SetSlot(2,kit.ItemId,1); inv.SelectSlot(2);
        Check(!inv.TryUseSelectedItem(),"R does not consume installation kit");
        bool committed=false,reentry=false;
        Action changed=()=>{committed=mortar.IsInstalled && inv.GetSlot(2).ItemId==remote.ItemId; reentry|=mortar.TryInstall();};
        inv.InventoryChanged+=changed;
        Check(mortar.TryInstall(),"E installation succeeds with full bag");
        Check(journal.IsEntryCompleted(QuestJournalManager.SandwormMortarQuestId) && !journal.IsEntryCompleted(QuestJournalManager.Level2SandwormQuestId),
            "actual E installation completes only the engineer child quest");
        inv.InventoryChanged-=changed;
        Check(committed && !reentry && inv.SelectedSlotIndex==2,"install state and same-slot return committed atomically");
        Check(mortar.GetComponentInChildren<SpriteRenderer>()!=null,"mounted receiver displayed");
        mover.EndItemAttack(); Check(!mortar.TryInstall(),"cannot farm remotes by installing twice");
        Check(!inv.TryUseSelectedItem() && inv.GetSlot(2).StoredCharge==0,"Floor2 cannot call shell");

        var manager=SceneTravelStateManager.Instance ?? new GameObject("State manager").AddComponent<SceneTravelStateManager>();
        var snapshot=Call(manager,"CreateSceneSnapshot",floor);
        var states=(IDictionary)Get(snapshot,"objects"); var keys=new List<object>(); foreach(var key in states.Keys) keys.Add(key);
        foreach(var key in keys) { var s=states[key]; states[key]=JsonUtility.FromJson(JsonUtility.ToJson(s),s.GetType()); }
        string disk=manager.CaptureDiskWorld();
        Check(disk.Contains("\"hasMortarInstallation\":true") && disk.Contains("\"mortarInstallationState\":{\"installed\":true}"),"compact disk save includes installer state");
        mortar.ApplyRuntimeState(default); Check(!mortar.IsInstalled,"checkpoint reset supported");
        Check((bool)Call(manager,"RestoreSceneSnapshot",floor,snapshot,false) && mortar.IsInstalled,"JSON scene-return restore keeps installation");
        mortar.gameObject.SetActive(false); mortar.gameObject.SetActive(true); Check(mortar.IsInstalled,"distance streaming preserves installation");

        var desert=SceneManager.CreateScene("Level2-Desert"); SceneManager.SetActiveScene(desert); SceneManager.MoveGameObjectToScene(player.gameObject,desert);
        player.transform.position=new Vector3(30,30); mover.ReceiveControl(Vector2.right); ZeldaRuntimeRegistry.ClaimControlledMover(mover);
        Check(inv.TryUseSelectedItem(),"desert accepts remote");
        var strike=Object.FindObjectOfType<MortarAirstrike>(); Check(strike!=null && strike.LandingPoint==new Vector2(30,30),"use snapshots exact location");
        strike.enabled=false;
        Check(inv.GetSlot(2).ItemId==remote.ItemId && inv.GetSlot(2).StoredCharge==5,"reusable remote starts 5 second cooldown");
        mover.EndItemAttack(); Check(!inv.TryUseSelectedItem(),"repeat R cannot bypass cooldown");
        player.transform.position=new Vector3(60,60);
        Check(strike.LandingPoint==new Vector2(30,30),"moving after call does not retarget");
        Call(strike,"Advance",.79f); Check(!strike.HasImpacted,"call delay precedes fall");
        var victim=Actor("Inside blast",new Vector2(34.7f,30));
        var duplicate=new GameObject("Second collider"); duplicate.transform.SetParent(victim.transform,false); duplicate.AddComponent<CircleCollider2D>().radius=.1f;
        var outside=Actor("Outside blast",new Vector2(35.5f,30));
        int hp=victim.CurrentHealth, outsideHp=outside.CurrentHealth;
        Call(strike,"Advance",.57f);
        Check(strike.HasImpacted && victim.CurrentHealth==hp-7,"radius5 hit is exactly AP7 despite multiple colliders");
        Check(outside.CurrentHealth==outsideHp,"outside radius unaffected");
        Call(strike,"Advance",.1f); Check(victim.CurrentHealth==hp-7,"single impact only");
        Check(Object.FindObjectOfType<BombExplosionVisual>()!=null,"blast visual created");
        Call(inv,"TickMortarRemoteCooldowns",2f); Check(Mathf.Approximately(inv.GetSlot(2).StoredCharge,3f),"cooldown counts down without inventory redraw");
        var saved=JsonUtility.FromJson<PersistentInventory.RuntimeState>(JsonUtility.ToJson(inv.CaptureRuntimeState()));
        inv.ClearAll(); inv.ApplyRuntimeState(saved); Check(inv.GetSlot(2).StoredCharge==3,"inventory JSON retains cooldown");
        inv.SwapSlots(2,4); Check(inv.GetSlot(4).StoredCharge==3,"cooldown follows slot swap"); inv.SelectSlot(4);
        Check(inv.TryDropSelectedItem(),"remote can be dropped");
        MortarRemotePickupItem dropped=null;
        foreach(var item in Object.FindObjectsOfType<MortarRemotePickupItem>()) if(item.gameObject.scene.IsValid()) dropped=item;
        Check(dropped!=null && Mathf.Abs(dropped.InventoryCharge-3)<.1f,"drop retains remaining cooldown");
        dropped.ApplyInventoryCharge(true,2f);
        Check(inv.TryAddUniqueItem(dropped.ItemId,dropped.UniqueInstanceId,1,dropped.MaxStackSize,dropped.ItemName,dropped.ItemDescription,
            false,Color.white,dropped.HasInventoryCharge,dropped.InventoryCharge,dropped.InventoryState),"pickup metadata restores remote");
        Object.Destroy(dropped.gameObject);
        Check(Mathf.Approximately(inv.GetSlot(inv.SelectedSlotIndex).StoredCharge,2),"picked-up remote cannot reset cooldown");
        var overlayRoot=new GameObject("Cooldown slot",typeof(RectTransform));
        overlayRoot.GetComponent<RectTransform>().sizeDelta=Vector2.one*32;
        var overlay=overlayRoot.AddComponent<InventoryItemCooldownOverlay>(); overlay.Configure(inv.SelectedSlotIndex,Resources.GetBuiltinResource<Font>("Arial.ttf"));
        var text=(Text)Get(overlay,"seconds"); var shade=(Image)Get(overlay,"shade");
        Check(text.text=="2" && shade.gameObject.activeSelf && Mathf.Abs(shade.rectTransform.anchorMax.y-.4f)<.001f,"HUD shows number and proportional dark wipe");
        Time.timeScale=0; Call(inv,"TickMortarRemoteCooldowns",0f); Check(inv.GetSlot(inv.SelectedSlotIndex).StoredCharge==2,"pause retains timer"); Time.timeScale=1;
        Call(inv,"TickMortarRemoteCooldowns",3f); Call(overlay,"LateUpdate"); Check(!shade.gameObject.activeSelf,"ready item clears overlay");

        // Buried worm: impact intersects tail but not root/head. No reliance on disabled colliders.
        var worm=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Level2NPCS/Sandworm-Boss.prefab"));
        var motion=worm.GetComponent<SandwormBossPrototype>(); motion.ResetPose();
        var attack=worm.GetComponent<SandwormEruptionAttack>(); var data=worm.GetComponent<SandwormZeldaCharacterData>();
        Vector2 point=motion.Tail.GetComponent<SpriteRenderer>().bounds.center; int health=data.CurrentHealth;
        player.transform.position=point; mover.EndItemAttack(); Check(inv.TryUseSelectedItem(),"second call after cooldown");
        MortarAirstrike wormStrike=null; foreach(var s in Object.FindObjectsOfType<MortarAirstrike>()) if(!s.HasImpacted) wormStrike=s;
        wormStrike.enabled=false; player.transform.position=new Vector3(60,60); Call(wormStrike,"Advance",1.36f);
        Check(data.CurrentHealth==health-7 && attack.IsRoarRecovery,"buried tail hit ejects worm once for AP7");
        attack.AdvanceAttack(.6f); Check(attack.Phase==SandwormEruptionAttack.AttackPhase.RoarStunned && attack.StunRemaining>4.9f,"existing 5 second stun and ejection animation");
        Call(wormStrike,"Advance",.1f); Check(data.CurrentHealth==health-7,"emerged body not damaged again");
        // Independent shell retains damage if the caller dies before arrival.
        var another=Actor("Doomed caller",new Vector2(100,100));
        Check(MortarAirstrike.TryCall(another,.1f),"independent delayed shot created");
        MortarAirstrike last=null; foreach(var s in Object.FindObjectsOfType<MortarAirstrike>()) if(!s.HasImpacted) last=s;
        last.enabled=false; Object.DestroyImmediate(another.gameObject);
        var witness=Actor("Posthumous victim",new Vector2(102,100)); hp=witness.CurrentHealth;
        Call(last,"Advance",.66f); Check(last.HasImpacted && witness.CurrentHealth==hp-7,"called shell survives caller destruction");
        var self=Actor("Self damage", new Vector2(140,140)); hp=self.CurrentHealth;
        ZeldaAttackHitbox.ApplyExplosionPulse(self.transform.position,5,7,self,desert);
        Check(self.CurrentHealth==hp-7,"artillery blast does not give its user immunity");
        var oldGhost=new GameObject("Ghost test").AddComponent<GhostZeldaCharacterData>();
        Check(!MortarAirstrike.TryCall(oldGhost,.1f),"ghost cannot call artillery");
        Check(Resources.Load<AudioClip>("Audio/MeteorDescent")!=null && Resources.Load<AudioClip>("Audio/BombExplosion")!=null,"existing synthesized fall/blast sounds available");
        yield return null;
        Preview(kit,remote);
        Time.timeScale=1;
    }
    static void Preview(PickupItemBase kit,PickupItemBase remote)
    {
        var canvas=new GameObject("Item preview",typeof(RectTransform),typeof(Canvas)).GetComponent<Canvas>();
        canvas.renderMode=RenderMode.WorldSpace; canvas.GetComponent<RectTransform>().sizeDelta=new Vector2(600,240);
        for(int i=0;i<2;i++)
        {
            var image=new GameObject("Icon",typeof(RectTransform),typeof(Image)).GetComponent<Image>(); image.transform.SetParent(canvas.transform,false);
            image.rectTransform.anchoredPosition=new Vector2(i==0?-150:150,20); image.rectTransform.sizeDelta=Vector2.one*150;
            image.sprite=(i==0?kit:remote).ItemVisual.InventoryIcon; image.preserveAspect=true;
            var label=new GameObject("Label",typeof(RectTransform),typeof(Text)).GetComponent<Text>(); label.transform.SetParent(canvas.transform,false);
            label.rectTransform.anchoredPosition=new Vector2(i==0?-150:150,-82); label.rectTransform.sizeDelta=new Vector2(270,32);
            label.font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/ZLabsPixel_12px_M_CN.ttf"); label.fontSize=16; label.alignment=TextAnchor.MiddleCenter;
            label.color=ZeldaUiPalette.Ghost; label.text=(i==0?kit:remote).ItemName;
        }
        foreach(var t in canvas.GetComponentsInChildren<Transform>(true)) t.gameObject.layer=30;
        var camera=new GameObject("Preview camera").AddComponent<Camera>(); camera.enabled=false; camera.orthographic=true; camera.orthographicSize=120;
        camera.transform.position=new Vector3(0,0,-100); camera.backgroundColor=new Color32(15,25,40,255); camera.clearFlags=CameraClearFlags.SolidColor;
        camera.cullingMask=1<<30;
        canvas.worldCamera=camera; var rt=new RenderTexture(600,240,24); camera.targetTexture=rt; Canvas.ForceUpdateCanvases(); camera.Render();
        var prior=RenderTexture.active; RenderTexture.active=rt; var tex=new Texture2D(600,240,TextureFormat.RGB24,false);
        tex.ReadPixels(new Rect(0,0,600,240),0,0); tex.Apply(); File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(),"MortarRemote-preview.png"),tex.EncodeToPNG());
        RenderTexture.active=prior; camera.targetTexture=null; Object.Destroy(rt); Object.Destroy(tex); Object.Destroy(canvas.gameObject); Object.Destroy(camera.gameObject);
    }
}
