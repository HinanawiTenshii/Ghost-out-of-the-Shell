using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class SilenceSealRelicSmoke
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    static IEnumerator suite;
    static int frame = -1, checks;
    static double deadline;
    static SilenceSealRelicSmoke() { EditorApplication.update += Tick; }
    public static void Run()
    {
        Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "isolated project only");
        PlayerSettings.productName = "silence-seal-relic-smoke";
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(nameof(SilenceSealRelicSmoke), true); EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return; frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(SilenceSealRelicSmoke), false))
            { SessionState.SetBool(nameof(SilenceSealRelicSmoke), false); suite = Verify(); deadline = EditorApplication.timeSinceStartup + 90; }
            if (suite == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timeout");
            if (suite.MoveNext()) return;
            suite = null; Debug.Log("SILENCE_SEAL_RELIC_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void Check(bool v, string m) { checks++; if (!v) throw new Exception(m); }
    static FieldInfo Field(object o, string n)
    {
        for (Type t = o.GetType(); t != null; t = t.BaseType) { var f = t.GetField(n, F); if (f != null) return f; }
        throw new Exception("Missing field " + n);
    }
    static T Get<T>(object o, string n) => (T)Field(o, n).GetValue(o);
    static void Set(object o, string n, object v) => Field(o, n).SetValue(o, v);
    static object Call(object o, string n, params object[] args) => o.GetType().GetMethod(n, F).Invoke(o, args);
    static ZeldaCharacterData Actor<T>(string name) where T : ZeldaCharacterData
    {
        var go = new GameObject(name); var data = go.AddComponent<T>(); go.AddComponent<ZeldaFourWayMover>();
        var state = data.CaptureRuntimeState(); state.health = state.currentHealth = 20; state.possessionEnergy = 30;
        data.ApplyRuntimeState(state); return data;
    }
    static void Control(ZeldaCharacterData actor)
    {
        var old = ZeldaRuntimeRegistry.GetControlledMover(); if (old != null) old.enabled = false;
        var mover = actor.GetComponent<ZeldaFourWayMover>(); mover.enabled = true;
        mover.ReceiveControl(Vector2.right); ZeldaRuntimeRegistry.ClaimControlledMover(mover);
        Physics2D.SyncTransforms(); ZeldaRuntimeRegistry.InvalidateSpatialSnapshot();
    }
    static void Close() { ItemDescriptionWindow.GetOrCreate().CloseWindow(); Time.timeScale = 1; }
    static IEnumerator Verify()
    {
        while (DocumentReader.IsInputBlocked) yield return null;
        SceneManager.SetActiveScene(SceneManager.CreateScene("Silence Seal Fixture"));
        var growth = PlayerGrowthAttributes.GetOrCreate(); growth.ApplyRuntimeState(default); growth.AddCollectibles(30);
        var basic = Array.Find(PlayerGrowthAttributes.Skills, s => s.id == SilenceOathRuntime.SkillId);
        Check(!growth.IsSkillDiscovered(basic) && !growth.TryUpgradeSkill(basic.id), "new game cannot learn before finding relic");
        var prefab = Resources.Load<GameObject>("PickupItems/SilenceSealRelicPickupItem");
        Check(prefab != null, "relic prefab imports");
        var template = prefab.GetComponent<SilenceSealRelicPickupItem>();
        Check(template.ItemName == "封言圣印" && template.ItemId == SilenceSealRelicPickupItem.DefaultItemId, "relic identity and name");
        Check(!template.EntersInventoryOnPickup && !template.CanUseFromInventory && !template.CanBeStoredInCardboardBox(null), "no inventory/drop/box use");
        Check(!(bool)typeof(PickupItemBase).GetProperty("IsAvailableForShop", F).GetValue(template) && template.RequiredUseSkill == 0, "cannot be sold into inventory, zero skill requirement");
        Check(SaveGameCatalog.LoadPickupTemplates().Count(p => p.ItemId == template.ItemId) == 1, "catalog discovers one template");
        var relic = Object.Instantiate(prefab).GetComponent<SilenceSealRelicPickupItem>();
        var visual = relic.GetComponent<SilenceSealRelicPickupItemVisual>();
        var rows = (string[])Call(visual, "GetPixelRows");
        Check(rows.Length == 26 && rows.All(r => r.Length == 20), "pixel grid dimensions");
        Check(rows.All(r => Enumerable.Range(0, 10).All(x => (r[x] == '.') == (r[19-x] == '.'))), "symmetric silhouette");
        Check(relic.GetComponent<Collider2D>().isTrigger && visual.InventoryIcon.texture.filterMode == FilterMode.Point, "nonblocking pixel pickup");
        Preview(visual.InventoryIcon);
        var shieldOwner = Actor<CivilianZeldaCharacterData>("Shield Owner"); shieldOwner.gameObject.AddComponent<HolyShieldCharacterSkillData>(); Control(shieldOwner);
        Check(!relic.TryPickUp() && relic.gameObject.activeSelf && shieldOwner.NativeCharacterSkillId == "holy_shield", "authored native skill refuses pickup without destroying relic");
        Check(!growth.IsSkillDiscovered(basic), "rejected pickup does not discover skill");
        var behemoth = Actor<BehemothZeldaCharacterData>("Behemoth"); Control(behemoth);
        Check(!relic.TryPickUp() && behemoth.NativeCharacterSkillId == "fear_roar", "override-based native skill also protected");
        var ghost = Actor<GhostZeldaCharacterData>("Ghost"); Control(ghost);
        Check(!relic.TryPickUp() && !ghost.TryGrantRelicCharacterSkill(basic.id), "ghost cannot inherit");
        var player = Actor<CivilianZeldaCharacterData>("Relic Bearer"); Control(player);
        var oldBodyState = player.CaptureRuntimeState();
        var mover = player.GetComponent<ZeldaFourWayMover>(); mover.transform.position = Vector3.right * 5;
        Check(!relic.TryPickUp(), "distance validated"); mover.transform.position = Vector3.zero;
        Time.timeScale = 0; Check(!relic.TryPickUp(), "paused pickup denied"); Time.timeScale = 1;
        var deadState = oldBodyState; deadState.currentHealth = 0; player.ApplyRuntimeState(deadState);
        Check(!relic.TryPickUp(), "dead body cannot inherit"); player.ApplyRuntimeState(oldBodyState);
        Check(!player.TryGrantRelicCharacterSkill("unsupported") && player.CanReceiveRelicCharacterSkill, "unknown skill cannot corrupt body slot");
        var canvas = new GameObject("Relic Test HUD", typeof(RectTransform), typeof(Canvas));
        var hud = new GameObject("Skill Slots", typeof(RectTransform)).AddComponent<RuntimeSkillSlots>();
        hud.transform.SetParent(canvas.transform, false); hud.Build(Resources.GetBuiltinResource<Font>("Arial.ttf"), 20);
        Call(hud, "Update"); Check(string.IsNullOrEmpty(Get<string[]>(hud, "skillNames")[5]), "body slot initially empty");
        var inventoryObject = new GameObject("Full Inventory"); inventoryObject.SetActive(false);
        var inv = inventoryObject.AddComponent<PersistentInventory>(); Set(inv, "saveToPlayerPrefs", false);
        inventoryObject.SetActive(true); inv.ClearAll(); inv.enabled = false;
        for (int i = 0; i < inv.SlotCount; i++) inv.SetSlot(i, GoldCoinPickupItem.DefaultItemId, 20);
        Check(inv.Slots.All(s => !s.IsEmpty), "test inventory full");
        string inventoryBefore = JsonUtility.ToJson(inv.CaptureRuntimeState());
        bool reentered = false; Action callback = () => { reentered = true; Check(!relic.TryPickUp(), "reentrant value-change pickup blocked"); };
        player.ValuesChanged += callback;
        Check(relic.TryPickUp(), "full inventory accepts direct inheritance"); player.ValuesChanged -= callback;
        Check(reentered && !relic.gameObject.activeSelf && !relic.TryPickUp(), "consumed immediately and cannot be picked up twice");
        Check(JsonUtility.ToJson(inv.CaptureRuntimeState()) == inventoryBefore, "inventory unchanged byte-for-byte");
        Check(player.NativeCharacterSkillId == basic.id && player.CharacterSkillName == basic.title && player.CharacterSkillIcon != null, "body receives native name/id/icon");
        Check(growth.IsSkillDiscovered(basic) && !growth.HasSkill(basic.id) && growth.CollectibleCount == 30, "discovered without free global purchase or skill point spend");
        Call(hud, "Update");
        Check(Get<string[]>(hud, "skillNames")[5] == basic.title && Get<Image[]>(hud, "icons")[5].enabled, "same-body HUD refreshes without possession change");
        Check(ItemDescriptionWindow.IsOpen && Get<Text>(ItemDescriptionWindow.GetOrCreate(), "nameText").text == template.ItemName &&
            Get<Image>(ItemDescriptionWindow.GetOrCreate(), "iconImage").sprite != null, "consumed relic still shows first-pickup description and icon");
        Close(); yield return null; yield return null;
        Check(relic == null, "relic destroyed after frame");
        Check(ItemDescriptionWindow.CaptureDiscoveryHistory().Contains(template.DescriptionDiscoveryId), "description discovery persists");
        mover.EndItemAttack();
        var guard = Actor<CivilianZeldaCharacterData>("Oath Target"); guard.transform.position = new Vector3(2, 0); guard.GetComponent<Rigidbody2D>().position = guard.transform.position;
        guard.GetComponent<ZeldaFourWayMover>().enabled = false; var ai = guard.gameObject.AddComponent<ZeldaCharacterAiBase>();
        Set(ai, "enableAwarenessSound", false); Set(ai, "visionObstacleLayers", (LayerMask)0); Set(ai, "facingDirection", Vector2.right);
        Control(player); mover.EndItemAttack();
        Physics2D.SyncTransforms(); ZeldaRuntimeRegistry.InvalidateSpatialSnapshot(); int energy = player.FinalPossessionEnergy;
        Check(SilenceOathRuntime.Rank == 0 && SilenceOathRuntime.RankFor(player) == 1, "native base rank before global learning");
        Check(SilenceOathRuntime.IsValidTarget(mover, ai), "native target valid");
        Check(SilenceOathRuntime.FindTarget(mover) == ai, "native target selected");
        Check(player.TryUseCharacterSkill(), "key-zero body ability casts before tree purchase");
        Check(ai.HasSilenceOath && player.FinalPossessionEnergy == energy - 2, "native cast marks target and costs two energy");
        hud.SelectSlot(5); Call(hud, "LateUpdate"); Check(Get<object>(hud, "silenceOathPreview") != null, "native slot has target preview");
        Object.Destroy(ai.gameObject);
        var bodyJson = JsonUtility.ToJson(player.CaptureRuntimeState());
        var growthJson = JsonUtility.ToJson(growth.CaptureRuntimeState());
        var manager = SceneTravelStateManager.GetOrCreate(); var disk = manager.CaptureDiskPlayer();
        Check(disk.character.grantedCharacterSkillId == basic.id && disk.growth.discoveredCharacterSkills.Contains(basic.id), "disk player captures body ability and global discovery");
        var restored = Actor<CivilianZeldaCharacterData>("Restored Body"); restored.ApplyRuntimeState(JsonUtility.FromJson<ZeldaCharacterData.RuntimeState>(bodyJson));
        Check(restored.NativeCharacterSkillId == basic.id, "body JSON restores ability");
        restored.gameObject.SetActive(false); restored.gameObject.SetActive(true);
        Check(restored.NativeCharacterSkillId == basic.id, "distance unload/reload retains body ability");
        var other = Actor<CivilianZeldaCharacterData>("Other Body"); Control(other);
        Check(string.IsNullOrEmpty(other.NativeCharacterSkillId) && !other.TryUseCharacterSkill() && player.NativeCharacterSkillId == basic.id, "possession does not copy ability to another body");
        var snapshot = Call(manager, "CreateSceneSnapshot", SceneManager.GetActiveScene());
        player.ApplyRuntimeState(oldBodyState); Check(string.IsNullOrEmpty(player.NativeCharacterSkillId), "old save clears later-acquired native ability");
        Call(manager, "RestoreSceneSnapshot", SceneManager.GetActiveScene(), snapshot, false);
        Check(player.NativeCharacterSkillId == basic.id, "scene snapshot restores abandoned-body ability");
        shieldOwner.ApplyRuntimeState(JsonUtility.FromJson<ZeldaCharacterData.RuntimeState>(bodyJson));
        Check(shieldOwner.NativeCharacterSkillId == "holy_shield", "restoration cannot replace authored native ability");
        growth.ApplyRuntimeState(default); growth.ApplyRuntimeState(JsonUtility.FromJson<PlayerGrowthAttributes.RuntimeState>(growthJson));
        Check(growth.IsSkillDiscovered(basic) && !growth.HasSkill(basic.id), "discovery survives save reload");
        Check(growth.TryUpgradeSkill(basic.id) && growth.CollectibleCount == 26, "global learning still costs four");
        Check(growth.TryUpgradeSkill("silence_oath_2") && growth.TryUpgradeSkill("silence_oath_3") && growth.CollectibleCount == 22 && SilenceOathRuntime.RankFor(player) == 3, "upgrades cost two each and enhance native skill");
        var legacy = growth.CaptureRuntimeState(); legacy.discoveredCharacterSkills = new string[0]; legacy.unlockedSkills = new[] { basic.id };
        growth.ApplyRuntimeState(legacy);
        Check(growth.IsSkillDiscovered(basic) && growth.TryUpgradeSkill("silence_oath_2"), "old learned-oath saves keep upgrade access without relic");
        // A second eligible body can consume another placed copy even without an inventory singleton.
        Object.DestroyImmediate(inv.gameObject); growth.ApplyRuntimeState(default); Control(other);
        var noInventory = Object.Instantiate(prefab).GetComponent<SilenceSealRelicPickupItem>();
        noInventory.transform.position = other.transform.position;
        Check(noInventory.TryPickUp() && other.NativeCharacterSkillId == basic.id, "inheritance does not require inventory singleton");
        Close(); yield return null;
    }
    static void Preview(Sprite icon)
    {
        var rt = RenderTexture.GetTemporary(320, 416, 0, RenderTextureFormat.ARGB32);
        var old = RenderTexture.active; Graphics.Blit(icon.texture, rt); RenderTexture.active = rt;
        var png = new Texture2D(320, 416, TextureFormat.RGBA32, false); png.ReadPixels(new Rect(0, 0, 320, 416), 0, 0); png.Apply();
        File.WriteAllBytes(Path.Combine(Application.dataPath, "../SilenceSealRelic-preview.png"), png.EncodeToPNG());
        Object.Destroy(png); RenderTexture.active = old; RenderTexture.ReleaseTemporary(rt);
    }
}
