using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class MeteorShowerSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static IEnumerator suite; static int checks, frame = -1; static double deadline;
    static readonly List<GameObject> targets = new List<GameObject>();
    static MeteorShowerSmoke() { EditorApplication.update += Tick; }
    public static void Run()
    {
        Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "isolated project only");
        PlayerSettings.productName = "meteor-shower-smoke";
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(nameof(MeteorShowerSmoke), true); EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return; frame = Time.frameCount;
        try {
            if (SessionState.GetBool(nameof(MeteorShowerSmoke), false)) {
                SessionState.SetBool(nameof(MeteorShowerSmoke), false); suite = Verify(); deadline = EditorApplication.timeSinceStartup + 150;
            }
            if (suite == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timeout");
            if (suite.MoveNext()) return;
            suite = null; Debug.Log("METEOR_SHOWER_PASS checks=" + checks); EditorApplication.Exit(0);
        } catch (Exception e) { suite = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
    static FieldInfo Field(object o, string name) {
        for (Type t = o.GetType(); t != null; t = t.BaseType) { var field = t.GetField(name, F); if (field != null) return field; }
        throw new Exception("Missing field " + name);
    }
    static T Get<T>(object o, string name) => (T)Field(o, name).GetValue(o);
    static void Set(object o, string name, object value) => Field(o, name).SetValue(o, value);
    static void To(MeteorShowerSkillRuntime cast, float time) => typeof(MeteorShowerSkillRuntime).GetMethod("Advance", F).Invoke(cast, new object[] { time - Get<float>(cast, "elapsed") });
    static IList Strikes(MeteorShowerSkillRuntime cast) => Get<IList>(cast, "strikes");
    static IEnumerable<object> Entries(MeteorShowerSkillRuntime cast) => Strikes(cast).Cast<object>();
    static MeteorShowerSkillRuntime Cast(ZeldaCharacterData owner) {
        owner.GetComponent<ZeldaFourWayMover>().EndItemAttack();
        Check(owner.TryUseCharacterSkill() || MeteorShowerSkillRuntime.TryUse(owner), "cast succeeds");
        var cast = Object.FindObjectsOfType<MeteorShowerSkillRuntime>().Single(); cast.enabled = false; return cast;
    }
    static void Energy(ZeldaCharacterData owner, int amount) {
        var state = owner.CaptureRuntimeState(); state.possessionEnergy = amount;
        state.spentBasePossessionEnergy = state.spentGrowthPossessionEnergy = state.crystalEnergyThirds = 0;
        owner.ApplyRuntimeState(state); owner.GetComponent<ZeldaFourWayMover>().EndItemAttack();
    }
    static ZeldaCharacterData Target(Vector2 point) {
        var go = new GameObject("Meteor target"); go.transform.position = point; targets.Add(go);
        var data = go.AddComponent<SwordZeldaCharacterData>(); go.AddComponent<ZeldaFourWayMover>().enabled = false;
        foreach (var collider in go.GetComponentsInChildren<Collider2D>()) collider.enabled = false;
        go.AddComponent<BoxCollider2D>().size = Vector2.one * .04f;
        var state = data.CaptureRuntimeState(); state.health = state.currentHealth = 500; data.ApplyRuntimeState(state); return data;
    }
    static void ClearTargets() { foreach (var go in targets) if (go) Object.DestroyImmediate(go); targets.Clear(); }
    static IEnumerator Verify()
    {
        Time.timeScale = 1; while (DocumentReader.IsInputBlocked) yield return null;
        var scene = SceneManager.CreateScene("MeteorTest"); SceneManager.SetActiveScene(scene);
        var growth = PlayerGrowthAttributes.GetOrCreate(); growth.ApplyRuntimeState(default);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Level2NPCS/Idle/OuterRealmVisitor-level2-Idle.prefab");
        Check(prefab.GetComponent<MeteorShowerCharacterSkillData>() != null, "native component attached to visitor");
        var root = Object.Instantiate(prefab); var owner = root.GetComponent<ZeldaCharacterData>();
        var mover = root.GetComponent<ZeldaFourWayMover>(); root.GetComponent<ZeldaCharacterAiBase>().enabled = false;
        Check(!MeteorShowerSkillRuntime.TryUse(owner), "uncontrolled body cannot cast");
        mover.ReceiveControl(Vector2.down); ZeldaRuntimeRegistry.ClaimControlledMover(mover);
        var camera = new GameObject("Meteor Test Camera").AddComponent<Camera>(); camera.tag = "MainCamera";
        camera.orthographic = true; camera.orthographicSize = 13; camera.transform.position = new Vector3(0, 0, -10);
        var follow = camera.gameObject.AddComponent<CameraFollowActiveZeldaMover>();
        var vision = camera.gameObject.AddComponent<CameraCircularVision>(); vision.SetVisionMaskRadius(13);
        new GameObject("Test Listener").AddComponent<AudioListener>();
        Energy(owner, 10);
        Check(owner.NativeCharacterSkillId == "meteor_shower" && owner.CharacterSkillName == "流星群", "native skill identity");
        Check(owner.CharacterSkillIcon == MeteorShowerCharacterSkillData.GetIcon(0), "native skill glyph");
        var visible = new List<ZeldaCharacterData>();
        for (int i = 0; i < 9; i++) visible.Add(Target(new Vector2(-2 - i % 3 * 2, -4 + i / 3 * 4)));
        var wall = new GameObject("Vision Blocks Wall"); wall.layer = LayerMask.NameToLayer("Blocks");
        wall.transform.position = new Vector2(2, 0); wall.AddComponent<BoxCollider2D>().size = new Vector2(.5f, 30);
        var hidden = Target(new Vector2(5, 0)); var distant = Target(new Vector2(-20, 0));
        var foreign = Target(new Vector2(-3, 0)); var otherScene = SceneManager.CreateScene("MeteorOtherScene"); SceneManager.MoveGameObjectToScene(foreign.gameObject, otherScene);
        var ghost = Target(new Vector2(-3, 1)); GhostFormRuntime.Restore(ghost, 5, 5);
        var dead = Target(new Vector2(-3, 2)); Set(dead, "isDead", true);
        var disabled = Target(new Vector2(-3, 3)); disabled.gameObject.SetActive(false);
        Physics2D.SyncTransforms(); ZeldaRuntimeRegistry.InvalidateSpatialSnapshot();
        var cast = Cast(owner);
        Check(owner.FinalPossessionEnergy == 5 && Strikes(cast).Count == 12, "five energy, twelve meteors");
        var locked = Entries(cast).Where(s => Get<bool>(s, "targeted")).Select(s => Get<Vector2>(s, "point")).ToArray();
        Check(locked.Length == 6 && locked.Distinct().Count() == 6, "six different targets, no duplicates");
        Check(locked.All(p => visible.Any(t => (Vector2)t.transform.position == p)), "no ghost/dead/disabled/player/hidden/foreign target");
        Check(Entries(cast).All(s => vision.IsWorldPositionVisible(Get<Vector2>(s, "point"))), "all landings respect actual vision mask");
        Check(Entries(cast).All(s => Get<Vector2>(s, "point").x < 2), "random landings do not penetrate wall");
        for (int i = 0; i < visible.Count; i++) visible[i].transform.position = new Vector2(-50, i * 2);
        Check(Entries(cast).Where(s => Get<bool>(s, "targeted")).Select(s => Get<Vector2>(s, "point")).SequenceEqual(locked), "positions frozen, never home onto moving targets");
        To(cast, .79f); Check(Get<int>(cast, "impacts") == 0, "no early damage before landing");
        To(cast, .81f); Check(Get<int>(cast, "impacts") == 1 && Get<float>(follow, "shakeRemaining") > 0, "first landing shakes camera once");
        To(cast, 1.09f); Check(Get<int>(cast, "impacts") == 2, "sequential arrivals");
        To(cast, 3.89f); Check(Get<int>(cast, "impacts") == 12 && !Get<bool>(cast, "fire"), "all twelve land, base has no lingering fire");
        Object.DestroyImmediate(cast.gameObject); Object.DestroyImmediate(wall); ClearTargets();
        Energy(owner, 10);
        var one = Target(new Vector2(3, 0)); Physics2D.SyncTransforms(); ZeldaRuntimeRegistry.InvalidateSpatialSnapshot();
        cast = Cast(owner);
        Check(Strikes(cast).Count == 12 && Entries(cast).Count(s => Get<bool>(s, "targeted")) == 1, "one visible actor yields one lock and eleven random positions");
        Object.DestroyImmediate(cast.gameObject); ClearTargets();
        Energy(owner, 10); cast = Cast(owner);
        Check(Entries(cast).All(s => !Get<bool>(s, "targeted")), "zero candidates: all twelve random");
        Object.DestroyImmediate(cast.gameObject);
        var baseNode = Array.Find(PlayerGrowthAttributes.Skills, s => s.id == "meteor_shower");
        Check(!growth.IsSkillDiscovered(baseNode), "not discovered before possession record");
        growth.RecordPossessedCharacter(owner); growth.AddCollectibles(12);
        Check(!growth.TryUpgradeSkill("meteor_shower_2"), "upgrade requires prerequisite");
        Check(growth.TryUpgradeSkill("meteor_shower") && growth.CollectibleCount == 6, "unlock costs six");
        Check(growth.ToggleEquippedSkill(0, "meteor_shower"), "equip learned skill");
        Check(growth.TryUpgradeSkill("meteor_shower_2") && growth.CollectibleCount == 3 && growth.GetEquippedSkill(0) == "meteor_shower_2", "first upgrade costs three, replaces base");
        for (int i = 0; i < 10; i++) Target(new Vector2(-6 + i, 2));
        Energy(owner, 10); ZeldaRuntimeRegistry.InvalidateSpatialSnapshot(); cast = Cast(owner);
        Check(Strikes(cast).Count == 15 && Entries(cast).Count(s => Get<bool>(s, "targeted")) == 8 && !Get<bool>(cast, "fire"), "first upgrade: eight targeted/seven random, no fire");
        Object.DestroyImmediate(cast.gameObject); ClearTargets();
        Check(growth.TryUpgradeSkill("meteor_shower_3") && growth.CollectibleCount == 0 && growth.GetEquippedSkill(0) == "meteor_shower_3", "second upgrade costs three");
        Energy(owner, 10); cast = Cast(owner);
        Check(Get<bool>(cast, "fire") && Strikes(cast).Count == 15, "second upgrade enables fire without adding more meteors");
        // Isolate one production strike, leaving the real radius/resolver/tick logic intact.
        while (Strikes(cast).Count > 1) Strikes(cast).RemoveAt(Strikes(cast).Count - 1);
        Set(Strikes(cast)[0], "point", new Vector2(40, 0));
        var center = Target(new Vector2(40, 0)); var edge = Target(new Vector2(43.97f, 0)); var outside = Target(new Vector2(44.08f, 0));
        var extra = new GameObject("Duplicate collider"); extra.transform.SetParent(center.transform, false); extra.AddComponent<BoxCollider2D>().size = Vector2.one * .02f;
        var late = Target(new Vector2(50, 0)); int hp = owner.CurrentHealth;
        root.transform.position = new Vector2(40, 0);
        To(cast, .81f); Check(center.CurrentHealth == 490 && edge.CurrentHealth == 490 && outside.CurrentHealth == 500, "AP10 once per character, exact circular radius four");
        To(cast, 1.79f); Check(center.CurrentHealth == 490, "no immediate fire tick");
        To(cast, 1.81f); Check(center.CurrentHealth == 488, "first AP2 tick at one second");
        late.transform.position = new Vector2(39, 0); To(cast, 2.81f);
        Check(center.CurrentHealth == 486 && late.CurrentHealth == 498, "late entry burns, no retroactive explosion");
        late.transform.position = new Vector2(50, 0); To(cast, 5.81f);
        Check(center.CurrentHealth == 480 && edge.CurrentHealth == 480 && late.CurrentHealth == 498 && outside.CurrentHealth == 500,
            "exactly five AP2 ticks, outside/leaving targets safe, large frame delta does not skip ticks");
        To(cast, 5.9f); Check(center.CurrentHealth == 480 && owner.CurrentHealth == hp, "fire ends after five ticks, caster immunity");
        Object.DestroyImmediate(cast.gameObject); ClearTargets(); root.transform.position = Vector3.zero;
        Energy(owner, 4); Check(!owner.TryUseCharacterSkill() && owner.FinalPossessionEnergy == 4, "insufficient energy is not spent");
        Energy(owner, 5); Time.timeScale = 0; Check(!owner.TryUseCharacterSkill(), "paused input blocked"); Time.timeScale = 1;
        GhostFormRuntime.Restore(owner, 5, 5); Check(!owner.TryUseCharacterSkill(), "ghost form cannot cast"); GhostFormRuntime.Restore(owner, 0);
        var saved = JsonUtility.FromJson<PlayerGrowthAttributes.RuntimeState>(JsonUtility.ToJson(growth.CaptureRuntimeState()));
        growth.ApplyRuntimeState(default); growth.ApplyRuntimeState(saved);
        Check(growth.HasSkill("meteor_shower_3") && growth.GetEquippedSkill(0) == "meteor_shower_3" && growth.IsSkillDiscovered(baseNode), "skill save roundtrip");
        for (int i = 0; i < 3; i++) {
            string id = i == 0 ? "meteor_shower" : "meteor_shower_" + (i + 1);
            var node = Array.Find(PlayerGrowthAttributes.Skills, s => s.id == id);
            Check(node.position.y == -495 && node.position.x >= -90, "horizontal row under holy light");
            Check(SkillPageArt.GetAbilitySprite(id) == MeteorShowerCharacterSkillData.GetIcon(i), "tree and quickslot sprite " + i);
        }
        SkillPreview();
        mover.enabled = false; var learned = Target(Vector2.zero); var learnedMover = learned.GetComponent<ZeldaFourWayMover>();
        learnedMover.ReceiveControl(Vector2.down); ZeldaRuntimeRegistry.ClaimControlledMover(learnedMover); Energy(learned, 10);
        cast = Cast(learned); Check(learned.NativeCharacterSkillId != "meteor_shower" && Get<bool>(cast, "fire"), "learned skill usable on other bodies");
        To(cast, 0); Check(Get<int>(cast, "impacts") == 0 && Get<float>(cast, "elapsed") == 0, "zero scaled delta freezes sequence");
        SceneManager.MoveGameObjectToScene(learned.gameObject, otherScene); To(cast, 1);
        Check(Get<bool>(cast, "cancelled") && Get<int>(cast, "impacts") == 0, "travel cancels delayed damage");
        Object.DestroyImmediate(cast.gameObject); SceneManager.MoveGameObjectToScene(learned.gameObject, scene);
        Energy(learned, 5); cast = Cast(learned); Set(learned, "isDead", true); To(cast, 1);
        Check(Get<bool>(cast, "cancelled") && Get<int>(cast, "impacts") == 0, "death cancels pending meteors");
        Object.DestroyImmediate(cast.gameObject); ClearTargets();
        root.transform.position = Vector3.zero; mover.ReceiveControl(Vector2.down); ZeldaRuntimeRegistry.ClaimControlledMover(mover);
        Energy(owner, 5); cast = Cast(owner); To(cast, 1.47f);
        Render(camera, "MeteorShower-sequence-preview.png"); Object.DestroyImmediate(cast.gameObject);
        var visualRoot = new GameObject("Meteor Visual Preview"); var visual = visualRoot.AddComponent<MeteorShowerVisual>(); visual.Configure(true, 0);
        visual.RenderAt(.74f); camera.orthographicSize = 6; Render(camera, "MeteorShower-fall-preview.png");
        visual.RenderAt(2.35f); Render(camera, "MeteorShower-fire-preview.png");
        Check(visualRoot.GetComponentsInChildren<MeshRenderer>().Length == 2 && visualRoot.GetComponentsInChildren<Collider2D>().Length == 0, "two batched visual meshes, no flame colliders");
        var before = Get<Mesh>(visual, "ground").vertices; visual.RenderAt(2.7f);
        Check(!before.SequenceEqual(Get<Mesh>(visual, "ground").vertices), "fire shape animates over time");
        Object.DestroyImmediate(visualRoot);
        Energy(owner, 5); Check(owner.TryUseCharacterSkill(), "live runtime cast"); var live = Object.FindObjectsOfType<MeteorShowerSkillRuntime>().Single();
        float start = Time.time; while (live != null && Time.time - start < 15) yield return null;
        Check(live == null && !CameraVisionStreamingRegion.HasActiveRegions, "live sequence cleans up visuals and loading region");
        Check(Object.FindObjectsOfType<MeteorShowerVisual>().Length == 0, "no leftover meteor effects");
    }
    static void SkillPreview()
    {
        var root = new GameObject("Meteor Skill Preview", typeof(RectTransform), typeof(Canvas));
        root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace; root.transform.position = new Vector3(1000, 1000);
        var page = root.AddComponent<CharacterSkillPage>();
        Font font = AssetDatabase.FindAssets("ZLabsPixel_12px_M_CN t:Font").Select(g => AssetDatabase.LoadAssetAtPath<Font>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault();
        page.Build(font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf"));
        Set(page, "selectedId", "meteor_shower"); typeof(CharacterSkillPage).GetMethod("Refresh", F).Invoke(page, null);
        Get<SkillTreeScroll>(page, "tree").Zoom(-.4f);
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 30;
        var art = root.GetComponentsInChildren<SkillPageArt>().Where(a => a.meteorShower).ToArray();
        Check(art.Length == 3 && art.All(a => a.mainTexture != Texture2D.whiteTexture), "all three tree nodes use meteor glyphs");
        var cam = new GameObject("Skill Preview Camera").AddComponent<Camera>();
        cam.transform.position = root.transform.position + Vector3.back * 10; cam.orthographic = true; cam.orthographicSize = 450;
        cam.cullingMask = 1 << 30; Canvas.ForceUpdateCanvases(); Render(cam, "MeteorShower-skill-preview.png", false);
        Object.DestroyImmediate(root); Object.DestroyImmediate(cam.gameObject);
    }
    static void Render(Camera camera, string file, bool center = true)
    {
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color32(24,31,43,255);
        if (center) camera.transform.position = new Vector3(0, 1, -10);
        var rt = new RenderTexture(1280, 900, 24); camera.targetTexture = rt; camera.Render();
        var previous = RenderTexture.active; RenderTexture.active = rt;
        var image = new Texture2D(1280, 900, TextureFormat.RGBA32, false); image.ReadPixels(new Rect(0,0,1280,900), 0, 0); image.Apply();
        File.WriteAllBytes(Application.dataPath + "/../" + file, image.EncodeToPNG());
        RenderTexture.active = previous; camera.targetTexture = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(image);
    }
}
