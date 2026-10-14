using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class HolyShieldSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static IEnumerator suite; static int checks, frame = -1; static double deadline;
    static HolyShieldSmoke() { EditorApplication.update += Tick; }
    public static void Run()
    {
        Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "isolated project only");
        PlayerSettings.productName = "holy-shield-smoke";
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(nameof(HolyShieldSmoke), true); EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return; frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(HolyShieldSmoke), false))
            { SessionState.SetBool(nameof(HolyShieldSmoke), false); suite = Verify(); deadline = EditorApplication.timeSinceStartup + 90; }
            if (suite == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timeout");
            if (suite.MoveNext()) return;
            suite = null; Debug.Log("HOLY_SHIELD_PASS checks=" + checks); EditorApplication.Exit(0);
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
    static object Call(object o, string n, params object[] args)
    {
        for (Type t = o.GetType(); t != null; t = t.BaseType)
        { var m = t.GetMethod(n, F); if (m != null) return m.Invoke(o, args); }
        throw new Exception("Missing method " + n);
    }
    static ZeldaCharacterData Actor(string name, bool native = false)
    {
        var go = new GameObject(name);
        var data = go.AddComponent<SwordZeldaCharacterData>();
        go.AddComponent<ZeldaFourWayMover>().enabled = false;
        if (native) go.AddComponent<HolyShieldCharacterSkillData>();
        var state = data.CaptureRuntimeState(); state.health = state.currentHealth = 30;
        state.possessionEnergy = 10; data.ApplyRuntimeState(state);
        return data;
    }
    static void Control(ZeldaCharacterData data)
    {
        var old = ZeldaRuntimeRegistry.GetControlledMover();
        if (old != null && old.gameObject != data.gameObject) old.enabled = false;
        var mover = data.GetComponent<ZeldaFourWayMover>();
        mover.ReceiveControl(Vector2.down); ZeldaRuntimeRegistry.ClaimControlledMover(mover); mover.EndItemAttack();
    }
    static void Energy(ZeldaCharacterData data, int value)
    {
        var state = data.CaptureRuntimeState(); state.possessionEnergy = value;
        state.spentBasePossessionEnergy = state.spentGrowthPossessionEnergy = state.crystalEnergyThirds = 0;
        data.ApplyRuntimeState(state); data.GetComponent<ZeldaFourWayMover>().EndItemAttack();
    }
    static void Cast(ZeldaCharacterData data)
    {
        data.GetComponent<ZeldaFourWayMover>().EndItemAttack();
        int energy = data.FinalPossessionEnergy;
        Check(HolyShieldRuntime.TryUse(data), "cast succeeds");
        Check(data.FinalPossessionEnergy == energy - 2, "cast costs two energy");
    }
    static void Buff(RuntimeBuffDurationBar bar, ZeldaCharacterData actor, int rank)
    {
        bar.RefreshNow(); var icon = bar.GetIcon(BuffDurationIcon.HolyShield);
        Check(icon.gameObject.activeSelf && !icon.IsTimed && icon.SkillSprite == HolyShieldCharacterSkillData.GetIcon(rank),
            "untimed buff HUD reuses ranked skill sprite");
        Check(CharacterSkillBuffs.TryGet(actor, BuffDurationIcon.HolyShield, out var state) &&
            !state.IsTimed && state.Duration == 0, "buff adapter has no invented duration");
    }
    static IEnumerator Verify()
    {
        Time.timeScale = 1; while (DocumentReader.IsInputBlocked) yield return null;
        var scene = SceneManager.CreateScene("Holy Shield Tests"); SceneManager.SetActiveScene(scene);
        var growth = PlayerGrowthAttributes.GetOrCreate(); growth.ApplyRuntimeState(default);
        foreach (string folder in new[] { "Idle", "Patrol", "Troops" })
        {
            string suffix = folder == "Idle" ? "" : folder == "Patrol" ? "-Patrol" : "-Squad";
            foreach (string type in new[] { "Paladin", "AdvancedPaladin" })
            {
                var p = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Level2NPCS/" + folder + "/" + type + suffix + ".prefab");
                Check(p != null && p.GetComponents<NativeCharacterSkillData>().Length == 1 &&
                    p.GetComponent<HolyShieldCharacterSkillData>() != null, "native skill on " + folder + "/" + type);
            }
            suffix = folder == "Idle" ? "" : folder == "Patrol" ? "-Patrol" : "-Suqad";
            var heavy = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Level2NPCS/" + folder + "/HeavyPaladin-level2" + suffix + ".prefab");
            Check(heavy != null && heavy.GetComponent<HolyShieldCharacterSkillData>() == null, "heavy excluded " + folder);
        }
        var paladinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Level2NPCS/Idle/Paladin.prefab");
        var actual = Object.Instantiate(paladinPrefab).GetComponent<ZeldaCharacterData>();
        Check(actual.NativeCharacterSkillId == "holy_shield" && actual.CharacterSkillName == "圣盾", "real paladin exposes native skill");
        Check(!actual.TryUseCharacterSkill(), "NPC cannot cast");
        Control(actual); Energy(actual, 2);
        Check(actual.TryUseCharacterSkill() && actual.HolyShieldCharges == 3 && actual.FinalPossessionEnergy == 0, "real prefab native cast");
        actual.GetComponent<ZeldaFourWayMover>().enabled = false; Object.DestroyImmediate(actual.gameObject);
        var owner = Actor("Shield owner", true); Control(owner);
        var attacker = Actor("Hit source"); attacker.transform.position = Vector3.right * 100;
        int evidence = 0; owner.AttackedBy += source => { if (source == attacker) evidence++; };
        var sourceUi = ActiveZeldaCharacterDataSource.Instance;
        if (sourceUi == null) sourceUi = new GameObject("Active data").AddComponent<ActiveZeldaCharacterDataSource>();
        sourceUi.Refresh();
        var hud = ZeldaHealthHeartsUI.Instance;
        if (hud == null) hud = new GameObject("Shield HUD", typeof(RectTransform), typeof(Canvas)).AddComponent<ZeldaHealthHeartsUI>();
        yield return null;
        var shields = Get<Image[]>(hud, "holyShields"); var hearts = Get<Image[]>(hud, "hearts");
        var bar = hud.GetComponentInChildren<RuntimeBuffDurationBar>();
        var basePosition = Get<Vector2>(hud, "heartContainerBasePosition");
        Check(shields.All(i => !i.gameObject.activeSelf), "no shield row before casting");
        Cast(owner); Buff(bar, owner, 0);
        Check(owner.HolyShieldCharges == 3 && owner.HolyShieldCapacity == 3, "base three charges");
        Check(shields.Count(i => i.gameObject.activeSelf) == 3 &&
            Get<Vector2>(hud, "heartContainerBasePosition").x > basePosition.x, "three shields reserve space to left of hearts");
        owner.TakeDamage(0); owner.TakeDamage(-10); Check(owner.HolyShieldCharges == 3, "invalid hits consume nothing");
        owner.TakeDamage(999, attacker);
        Check(owner.CurrentHealth == 30 && !owner.IsDead && owner.HolyShieldCharges == 2, "lethal hit completely blocked");
        Check(sourceUi.HolyShieldCharges == 2 && evidence == 1 && Get<float>(hud, "healthFlashTimer") > 0,
            "immune hit refreshes event-driven HUD and still supplies AI attack evidence");
        Call(hud, "SetHeartColors", true);
        Check(shields[2].gameObject.activeSelf && shields[2].color == hearts[0].color && shields[2].color == Get<Color>(hud, "healthFlashColor"),
            "lost shield and hearts flash together");
        Call(hud, "UpdateHealthFlash", 10f);
        Check(shields.Count(i => i.gameObject.activeSelf) == 2 && shields[0].color == Get<Color>(hud, "ghostHeartColor"),
            "flash completion removes lost shield, remaining shields blue");
        owner.TakeHandgunDamage(2, Vector2.right * 100, attacker);
        Check(owner.HolyShieldCharges == 1 && owner.CurrentHealth == 30 && evidence == 2, "handgun hit blocked with evidence preserved");
        Call(owner, "TakeDominoDamage", 8, attacker);
        Check(owner.HolyShieldCharges == 0 && owner.CurrentHealth == 30, "shared damage is also one blocked hit");
        Call(hud, "UpdateHealthFlash", 10f); bar.RefreshNow();
        Check(!bar.GetIcon(BuffDurationIcon.HolyShield).gameObject.activeSelf && shields.All(i => !i.gameObject.activeSelf) &&
            Get<Vector2>(hud, "heartContainerBasePosition") == basePosition, "empty shields remove buff and restore original HUD layout");
        owner.TakeDamage(2); Check(owner.CurrentHealth == 28, "fourth damage is no longer blocked");
        Cast(owner); owner.TakeDamage(1); Cast(owner);
        Check(owner.HolyShieldCharges == 3, "recast refreshes not stacks");
        Energy(owner, 1); int charges = owner.HolyShieldCharges;
        Check(!HolyShieldRuntime.TryUse(owner) && owner.HolyShieldCharges == charges, "insufficient energy cannot refresh");
        Energy(owner, 10); Time.timeScale = 0; Check(!HolyShieldRuntime.TryUse(owner), "paused cast blocked"); Time.timeScale = 1;
        GhostFormRuntime.Restore(owner, 5, 5); Check(!HolyShieldRuntime.TryUse(owner), "ghost-form cast blocked"); GhostFormRuntime.Restore(owner, 0);
        Check(!HolyShieldRuntime.TryUse(null), "null cast safe");
        yield return null; Cast(owner);
        var stored = JsonUtility.FromJson<ZeldaCharacterData.RuntimeState>(JsonUtility.ToJson(owner.CaptureRuntimeState()));
        owner.TakeDamage(1);
        stored.holyShield.charges = 2; owner.ApplyRuntimeState(stored);
        Buff(bar, owner, 0); Check(owner.HolyShieldCharges == 2, "body state JSON roundtrip preserves charges");
        Call(hud, "UpdateHealthFlash", 10f);
        var other = Actor("Alternate body"); Control(other); sourceUi.Refresh(); bar.RefreshNow();
        Check(!bar.GetIcon(BuffDurationIcon.HolyShield).gameObject.activeSelf && shields.All(i => !i.gameObject.activeSelf),
            "buff and shields do not transfer to other body");
        Control(owner); sourceUi.Refresh(); Buff(bar, owner, 0);
        Check(shields.Count(i => i.gameObject.activeSelf) == 2, "switching back shows stored charges");
        owner.gameObject.SetActive(false); owner.gameObject.SetActive(true); Control(owner);
        Check(owner.HolyShieldCharges == 2, "distance-unload/reactivation keeps charges");
        var otherScene = SceneManager.CreateScene("Shield destination");
        SceneManager.MoveGameObjectToScene(owner.gameObject, otherScene); Buff(bar, owner, 0);
        Check(owner.HolyShieldCharges == 2, "scene transition preserves body buff");
        SceneManager.MoveGameObjectToScene(owner.gameObject, scene);
        var oldSave = other.CaptureRuntimeState(); oldSave.holyShield = default; other.ApplyRuntimeState(oldSave);
        Check(other.HolyShieldCharges == 0 && !CharacterSkillBuffs.TryGet(other, BuffDurationIcon.HolyShield, out _), "legacy save defaults no buff");
        var basic = Array.Find(PlayerGrowthAttributes.Skills, s => s.id == "holy_shield");
        Check(!growth.IsSkillDiscovered(basic), "skill requires native possession discovery");
        growth.RecordPossessedCharacter(owner); growth.AddCollectibles(20);
        Check(growth.IsSkillDiscovered(basic) && !growth.TryUpgradeSkill("holy_shield_2"), "discovery and prerequisite enforced");
        Check(growth.TryUpgradeSkill("holy_shield") && growth.CollectibleCount == 16, "learning costs four");
        Check(growth.ToggleEquippedSkill(0, "holy_shield"), "equip learned skill");
        Check(growth.TryUpgradeSkill("holy_shield_2") && growth.GetEquippedSkill(0) == "holy_shield_2", "first upgrade replaces equipment");
        Energy(owner, 10); Cast(owner); Buff(bar, owner, 1);
        Check(owner.HolyShieldCharges == 4 && !owner.HolyShieldPreservesCharges, "upgrade one has four ordinary charges");
        for (int i = 0; i < 4; i++) owner.TakeDamage(500);
        Check(owner.HolyShieldCharges == 0 && owner.CurrentHealth == stored.currentHealth, "all four hits negated");
        Check(growth.TryUpgradeSkill("holy_shield_3") && growth.GetEquippedSkill(0) == "holy_shield_3", "second upgrade replaces equipment");
        Energy(owner, 10); Cast(owner); Buff(bar, owner, 2);
        Check(owner.HolyShieldCharges == 4 && owner.HolyShieldPreservesCharges, "upgrade two retains four charges with preservation");
        VerifyChance(owner);
        var savedGrowth = JsonUtility.FromJson<PlayerGrowthAttributes.RuntimeState>(JsonUtility.ToJson(growth.CaptureRuntimeState()));
        growth.ApplyRuntimeState(default); growth.ApplyRuntimeState(savedGrowth);
        Check(growth.HasSkill("holy_shield_3") && growth.GetEquippedSkill(0) == "holy_shield_3" &&
            growth.IsSkillDiscovered(basic), "learning, discovery, equipment and upgrade persist");
        var savedShield = JsonUtility.FromJson<ZeldaCharacterData.RuntimeState>(JsonUtility.ToJson(owner.CaptureRuntimeState()));
        other.ApplyRuntimeState(savedShield); Control(other);
        Check(other.HolyShieldPreservesCharges && other.HolyShieldCharges == savedShield.holyShield.charges, "restored upgraded buff retains proc policy");
        Energy(other, 8); Cast(other);
        Check(other.NativeCharacterSkillId != "holy_shield" && other.HolyShieldCharges == 4, "learned skill works on other bodies");
        // Neither poison nor the old body's deliberate death is an incoming damage hit.
        var poison = Actor("Poison fixture"); poison.ApplyRuntimeState(savedShield); poison.KillFromPotion();
        Check(poison.IsDead && poison.HolyShieldCharges == 0, "direct death clears buff without immunity");
        var transfer = Actor("Transfer fixture"); transfer.ApplyRuntimeState(savedShield); transfer.DestroyAfterSuccessfulPossession();
        Check(transfer.IsDead && transfer.HolyShieldCharges == 0, "body transfer death still occurs and clears buff");
        VerifyDominoAndEcho();
        Control(owner); Energy(owner, 10); Cast(owner); owner.TakeDamage(1); Call(hud, "UpdateHealthFlash", 10f);
        Buff(bar, owner, 2);
        SkillPreview(growth); HudPreview(hud, owner);
        Time.timeScale = 0; int before = owner.HolyShieldCharges;
        yield return null; yield return null;
        Check(owner.HolyShieldCharges == before, "untimed shield never ticks down");
    }
    static void VerifyChance(ZeldaCharacterData owner)
    {
        var oldRandom = UnityEngine.Random.state; UnityEngine.Random.InitState(1394);
        int kept = 0, used = 0;
        for (int i = 0; i < 10000; i++)
        {
            var s = new HolyShieldRuntime.State { charges = 4, capacity = 4, preserveCharges = true };
            if (!HolyShieldRuntime.TryAbsorb(ref s)) throw new Exception("nonempty shield must absorb");
            if (s.charges == 4) kept++; else if (s.charges == 3) used++; else throw new Exception("consumed more than one");
        }
        Check(kept > 3100 && kept < 3500 && kept + used == 10000, "33 percent preservation distribution");
        bool actualKept = false, actualConsumed = false; int hp = owner.CurrentHealth;
        for (int i = 0; i < 50; i++)
        {
            var s = owner.CaptureRuntimeState(); s.holyShield.charges = 4; owner.ApplyRuntimeState(s);
            owner.TakeDamage(999);
            actualKept |= owner.HolyShieldCharges == 4; actualConsumed |= owner.HolyShieldCharges == 3;
        }
        Check(actualKept && actualConsumed && owner.CurrentHealth == hp, "both proc outcomes negate entire incoming hit");
        var empty = new HolyShieldRuntime.State { capacity = 4, preserveCharges = true };
        var rng = UnityEngine.Random.state;
        Check(!HolyShieldRuntime.TryAbsorb(ref empty) && JsonUtility.ToJson(rng) == JsonUtility.ToJson(UnityEngine.Random.state),
            "empty shield neither blocks nor rolls RNG");
        UnityEngine.Random.state = oldRandom;
    }
    static void VerifyDominoAndEcho()
    {
        var a = Actor("Linked shield"); var b = Actor("Linked partner");
        var s = a.CaptureRuntimeState(); s.holyShield = new HolyShieldRuntime.State { charges = 3, capacity = 3 }; a.ApplyRuntimeState(s);
        var group = new GameObject("Test links").AddComponent<DominoSkillRuntime>();
        Call(group, "AddMember", a); Call(group, "AddMember", b);
        a.TakeDamage(999); Check(a.CurrentHealth == 30 && b.CurrentHealth == 30, "fully blocked hit does not propagate nonexistent damage");
        b.TakeDamage(2); Check(a.HolyShieldCharges == 1 && a.CurrentHealth == 30 && b.CurrentHealth == 28, "shared hit can be shielded at recipient");
        Object.DestroyImmediate(group.gameObject);
        Check(CharacterPotionBuffs.TryApply(a, AlchemyPotionKind.Echo), "echo fixture applied");
        a.TakeDamage(999);
        Check(a.HolyShieldCharges == 0 && a.GetComponent<CharacterPotionBuffs>().DisplayRemaining(AlchemyPotionKind.Echo, out _) > 0,
            "shield resolves before lethal Echo, preserving Echo");
        a.TakeDamage(999); Check(!a.IsDead && a.GetComponent<CharacterPotionBuffs>().DisplayRemaining(AlchemyPotionKind.Echo, out _) <= 0,
            "after shield exhausted lethal hit still triggers Echo");
        Object.DestroyImmediate(a.gameObject); Object.DestroyImmediate(b.gameObject);
    }
    static void SkillPreview(PlayerGrowthAttributes growth)
    {
        var root = new GameObject("Holy Shield Skill Preview", typeof(RectTransform), typeof(Canvas));
        root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace; root.transform.position = new Vector3(1000, 1000);
        var page = root.AddComponent<CharacterSkillPage>();
        Font font = AssetDatabase.FindAssets("ZLabsPixel_12px_M_CN t:Font").Select(g => AssetDatabase.LoadAssetAtPath<Font>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault();
        page.Build(font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf"));
        Set(page, "selectedId", "holy_shield"); Call(page, "Refresh"); Get<SkillTreeScroll>(page, "tree").Zoom(-.4f);
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 30;
        var art = root.GetComponentsInChildren<SkillPageArt>().Where(a => a.holyShield).ToArray();
        Check(art.Length == 3 && art.All(a => a.mainTexture != Texture2D.whiteTexture), "three skill tree shield sprites");
        var builder = Array.Find(PlayerGrowthAttributes.Skills, s => s.id == "builder");
        foreach (string id in new[] { "holy_shield", "holy_shield_2", "holy_shield_3" })
        {
            var node = Array.Find(PlayerGrowthAttributes.Skills, s => s.id == id);
            Check(node.position.y < builder.position.y && node.position.y == -495 && node.type == PlayerGrowthAttributes.SkillType.Active,
                "horizontal chain below builder " + id);
        }
        var cam = new GameObject("Shield Tree Preview Camera").AddComponent<Camera>();
        cam.transform.position = root.transform.position + Vector3.back * 10; cam.orthographic = true; cam.orthographicSize = 285;
        cam.cullingMask = 1 << 30; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;
        Canvas.ForceUpdateCanvases(); Capture(cam, "Holy-shield-skill-preview.png", 1600, 850);
        Object.DestroyImmediate(root); Object.DestroyImmediate(cam.gameObject);
    }
    static void HudPreview(ZeldaHealthHeartsUI hud, ZeldaCharacterData owner)
    {
        var state = owner.CaptureRuntimeState(); state.health = state.currentHealth = 8;
        state.holyShield = new HolyShieldRuntime.State { charges = 3, capacity = 3 }; owner.ApplyRuntimeState(state);
        Call(hud, "UpdateHealthFlash", 10f);
        hud.GetComponentInChildren<RuntimeBuffDurationBar>().RefreshNow();
        hud.enabled = false; hud.GetComponent<CanvasScaler>().enabled = false;
        var canvas = hud.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
        var rect = (RectTransform)hud.transform; rect.sizeDelta = new Vector2(1280, 720);
        rect.position = new Vector3(2000, 2000); rect.localScale = Vector3.one;
        foreach (var t in hud.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 30;
        var cam = new GameObject("Shield HUD Preview Camera").AddComponent<Camera>();
        cam.transform.position = hud.transform.position + new Vector3(-365, 260, -10);
        cam.orthographic = true; cam.orthographicSize = 100; cam.cullingMask = 1 << 30;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.035f, .055f, .12f);
        Canvas.ForceUpdateCanvases(); Capture(cam, "Holy-shield-hud-preview.png", 1100, 400);
        Object.DestroyImmediate(cam.gameObject);
    }
    static void Capture(Camera cam, string path, int width, int height)
    {
        var rt = new RenderTexture(width, height, 24); cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
        File.WriteAllBytes(Application.dataPath + "/../" + path, texture.EncodeToPNG());
        RenderTexture.active = null; cam.targetTexture = null;
        Object.DestroyImmediate(texture); Object.DestroyImmediate(rt);
    }
}
