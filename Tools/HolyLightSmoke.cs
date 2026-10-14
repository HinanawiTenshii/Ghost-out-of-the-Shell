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
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class HolyLightSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static IEnumerator suite; static int checks, frame = -1; static double deadline;
    static readonly List<GameObject> targets = new List<GameObject>();
    static HolyLightSmoke() { EditorApplication.update += Tick; }
    public static void Run()
    {
        Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "isolated project");
        PlayerSettings.productName = "holy-light-smoke";
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(nameof(HolyLightSmoke), true); EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return; frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(HolyLightSmoke), false))
            { SessionState.SetBool(nameof(HolyLightSmoke), false); suite = Verify(); deadline = EditorApplication.timeSinceStartup + 90; }
            if (suite == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timeout");
            if (suite.MoveNext()) return;
            suite = null; Debug.Log("HOLY_LIGHT_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    static FieldInfo Field(object o, string name)
    {
        for (Type t = o.GetType(); t != null; t = t.BaseType) { var f = t.GetField(name, F); if (f != null) return f; }
        throw new Exception("Missing field " + name);
    }
    static T Get<T>(object o, string name) => (T)Field(o, name).GetValue(o);
    static void Set(object o, string name, object value) => Field(o, name).SetValue(o, value);
    static object Call(object o, string name, params object[] args)
    {
        for (Type t = o.GetType(); t != null; t = t.BaseType)
        { var m = t.GetMethod(name, F); if (m != null) return m.Invoke(o, args); }
        throw new Exception("Missing method " + name);
    }
    static void To(HolyLightSkillRuntime cast, float time) => Call(cast, "Advance", time - Get<float>(cast, "elapsed"));
    static HolyLightSkillRuntime Cast(ZeldaCharacterData owner)
    {
        owner.GetComponent<ZeldaFourWayMover>().EndItemAttack();
        Check(HolyLightSkillRuntime.TryUse(owner), "cast succeeds");
        var cast = Object.FindObjectsOfType<HolyLightSkillRuntime>().Single(); cast.enabled = false; return cast;
    }
    static ZeldaCharacterData Target(Vector2 point)
    {
        var go = new GameObject("Holy light test target"); go.transform.position = point; targets.Add(go);
        var data = go.AddComponent<SwordZeldaCharacterData>(); go.AddComponent<ZeldaFourWayMover>().enabled = false;
        foreach (var collider in go.GetComponentsInChildren<Collider2D>()) collider.enabled = false;
        var hitbox = go.AddComponent<BoxCollider2D>(); hitbox.size = Vector2.one * .1f;
        var state = data.CaptureRuntimeState(); state.health = state.currentHealth = 30; data.ApplyRuntimeState(state);
        return data;
    }
    static void ResetTargets() { foreach (var go in targets) if (go) Object.DestroyImmediate(go); targets.Clear(); }
    static void Energy(ZeldaCharacterData owner, int value)
    {
        var state = owner.CaptureRuntimeState(); state.possessionEnergy = value;
        state.spentBasePossessionEnergy = state.spentGrowthPossessionEnergy = state.crystalEnergyThirds = 0;
        owner.ApplyRuntimeState(state); owner.GetComponent<ZeldaFourWayMover>().EndItemAttack();
    }
    static IEnumerator Verify()
    {
        Time.timeScale = 1; while (DocumentReader.IsInputBlocked) yield return null;
        var scene = SceneManager.CreateScene("Holy Light Test"); SceneManager.SetActiveScene(scene);
        new GameObject("Holy Light Test Audio Listener").AddComponent<AudioListener>();
        var growth = PlayerGrowthAttributes.GetOrCreate(); growth.ApplyRuntimeState(default);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Level2NPCS/Idle/AdvancedPriest-level2-Idle.prefab");
        Check(prefab.GetComponent<HolyLightCharacterSkillData>() != null, "native attached to advanced priest");
        var ordinary = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Level2NPCS/Idle/Priest-level2-Idle.prefab");
        Check(ordinary.GetComponent<NativeCharacterSkillData>() == null, "ordinary priest unchanged");
        var root = Object.Instantiate(prefab); var owner = root.GetComponent<ZeldaCharacterData>();
        var mover = root.GetComponent<ZeldaFourWayMover>();
        Check(!owner.TryUseCharacterSkill(), "unpossessed priest cannot cast");
        mover.ReceiveControl(Vector2.down); ZeldaRuntimeRegistry.ClaimControlledMover(mover); Energy(owner, 4);
        Check(owner.NativeCharacterSkillId == "holy_light" && owner.CharacterSkillName == "圣光打击", "native discovery identity");
        Check(owner.CharacterSkillIcon == HolyLightCharacterSkillData.GetIcon(0), "property/native HUD glyph");
        int selfHealth = owner.CurrentHealth;
        var center = Target(new Vector2(2.5f, 0));
        var extra = new GameObject("Duplicate child collider"); extra.transform.SetParent(center.transform, false);
        extra.AddComponent<BoxCollider2D>().size = Vector2.one * .1f;
        var diagonal = Target(new Vector2(2.3f, 2.3f));
        var beyondSmall = Target(new Vector2(4, 1.2f));
        var centralEdge = Target(new Vector2(0, 2.98f));
        var centralOutside = Target(new Vector2(0, 3.08f));
        var smallEdge = Target(new Vector2(4, .98f));
        var lateEntry = Target(new Vector2(20, 20));
        var cardinal = new List<ZeldaCharacterData>();
        foreach (Vector2 d in new[] { Vector2.up, Vector2.right, Vector2.down, Vector2.left }) cardinal.Add(Target(d * 4));
        var second = Target(new Vector2(6, 0)); var third = Target(new Vector2(8, 0)); var fourth = Target(new Vector2(10, 0));
        var foreign = Target(new Vector2(2.5f, 0)); var otherScene = SceneManager.CreateScene("Other Holy Light Scene");
        SceneManager.MoveGameObjectToScene(foreign.gameObject, otherScene);
        Check(owner.TryUseCharacterSkill(), "native cast before learning");
        var cast = Object.FindObjectsOfType<HolyLightSkillRuntime>().Single(); cast.enabled = false;
        Check(owner.FinalPossessionEnergy == 0, "base cost four");
        To(cast, .08f); Check(center.CurrentHealth == 30, "no damage before descending light lands");
        To(cast, .17f); Check(center.CurrentHealth == 30, "outside expanding wave is not damaged early");
        Check(centralEdge.CurrentHealth == 30, "outer edge untouched at landing");
        var centralStrike = Get<IList>(cast, "strikes")[0];
        var centralResolver = Get<ZeldaAttackHitbox>(centralStrike, "resolver");
        var initialBuffer = Get<object>(centralResolver, "circularOverlaps");
        To(cast, .8f); Check(center.CurrentHealth == 26, "wave reaches target, AP4 once despite duplicate collider");
        Check(centralEdge.CurrentHealth == 30, "wave front not yet at radius three");
        To(cast, .9f); Check(centralEdge.CurrentHealth == 26 && centralOutside.CurrentHealth == 30, "maximum radius boundary matches visible ring outer edge");
        lateEntry.transform.position = new Vector2(2.4f, 0);
        To(cast, 1.2f); Check(lateEntry.CurrentHealth == 26, "entering ongoing shockwave receives one hit");
        lateEntry.transform.position = new Vector2(20, 20); To(cast, 1.4f);
        lateEntry.transform.position = new Vector2(2.4f, 0); To(cast, 1.6f);
        Check(lateEntry.CurrentHealth == 26 && center.CurrentHealth == 26, "remaining inside or reentering never causes repeated damage");
        Check(ReferenceEquals(initialBuffer, Get<object>(centralResolver, "circularOverlaps")), "overlap buffer reused throughout shockwave");
        Check(diagonal.CurrentHealth == 30, "true radius three, not square");
        Check(foreign.CurrentHealth == 30, "no cross-scene damage");
        Check(owner.CurrentHealth == selfHealth, "central caster immunity");
        To(cast, 2.15f); Check(Get<int>(cast, "roundsIssued") == 0 && center.CurrentHealth == 26, "central visual lasts two seconds without damage ticks");
        root.transform.position = new Vector2(4, 0);
        To(cast, 2.18f);
        var fadeEntry = Target(new Vector2(0, 1.8f));
        To(cast, 2.34f);
        Check(Get<int>(cast, "roundsIssued") == 1 && cardinal.All(t => t.CurrentHealth == 28), "first four cardinal hits at AP2");
        Check(second.CurrentHealth == 30 && third.CurrentHealth == 30, "later rounds not simultaneous");
        Check(smallEdge.CurrentHealth == 30, "small wave also expands before reaching outer targets");
        Check(fadeEntry.CurrentHealth == 30, "visual fade after two seconds does not extend damage window");
        Check(owner.CurrentHealth == selfHealth && Get<Vector2>(cast, "origin") == Vector2.zero, "moving caster excluded; origin stays at cast point");
        Check(beyondSmall.CurrentHealth == 30, "small radius exactly one");
        To(cast, 2.7f); Check(second.CurrentHealth == 28 && third.CurrentHealth == 30, "second outward round");
        To(cast, 2.77f); Check(smallEdge.CurrentHealth == 28 && beyondSmall.CurrentHealth == 30, "small wave radius one exact, no damage outside visible edge");
        To(cast, 3.06f); Check(third.CurrentHealth == 28 && fourth.CurrentHealth == 30, "third outward round, no fourth at base level");
        Check(Get<IList>(cast, "strikes").Count == 13 && cardinal.All(t => t.CurrentHealth == 28), "13 total single-hit columns");
        To(cast, 3.7f); Check(center.CurrentHealth == 26 && third.CurrentHealth == 28, "lingering shockwaves never repeat damage");
        Object.DestroyImmediate(cast.gameObject); ResetTargets(); root.transform.position = Vector3.zero;
        var instantTarget = Target(new Vector2(20, 20));
        ZeldaAttackHitbox.ApplyCircularPulse(new Vector2(20, 20), 1, 2, owner);
        Check(instantTarget.CurrentHealth == 28, "legacy instantaneous pulse still hits immediately");
        ZeldaAttackHitbox.ApplyCircularPulse(new Vector2(20, 20), 1, 2, owner);
        Check(instantTarget.CurrentHealth == 26, "separate instantaneous pulses retain independent hit histories");
        ResetTargets();
        VerifyWaveGeometry();
        VerifyColumnReach();
        mover.EndItemAttack(); Check(!owner.TryUseCharacterSkill(), "insufficient energy no cast");
        Energy(owner, 8); Time.timeScale = 0; Check(!owner.TryUseCharacterSkill(), "pause blocked"); Time.timeScale = 1;
        GhostFormRuntime.Restore(owner, 5, 5); Check(!owner.TryUseCharacterSkill(), "ghost form blocked"); GhostFormRuntime.Restore(owner, 0);
        Check(!HolyLightSkillRuntime.TryUse(null), "null owner safe");
        var basic = Array.Find(PlayerGrowthAttributes.Skills, s => s.id == "holy_light");
        Check(!growth.IsSkillDiscovered(basic), "tree locked before possessing priest");
        growth.RecordPossessedCharacter(owner); growth.AddCollectibles(20);
        Check(growth.IsSkillDiscovered(basic) && !growth.TryUpgradeSkill("holy_light_2"), "discovery and prerequisite");
        Check(growth.TryUpgradeSkill("holy_light") && growth.CollectibleCount == 16, "learning costs four skill points");
        Check(growth.ToggleEquippedSkill(0, "holy_light"), "learned skill equip");
        Check(growth.TryUpgradeSkill("holy_light_2") && growth.GetEquippedSkill(0) == "holy_light_2", "upgrade replaces equipped base");
        fourth = Target(new Vector2(10, 0)); center = Target(new Vector2(2.5f, 0));
        cast = Cast(owner); To(cast, 3.42f);
        Check(Get<IList>(cast, "strikes").Count == 17 && fourth.CurrentHealth == 28 && center.CurrentHealth == 26, "first upgrade adds fourth round without raising damage");
        Check(owner.FinalPossessionEnergy == 4, "first upgrade costs four energy");
        Object.DestroyImmediate(cast.gameObject); ResetTargets();
        Check(growth.TryUpgradeSkill("holy_light_3") && growth.GetEquippedSkill(0) == "holy_light_3", "second upgrade replaces equipped first upgrade");
        fourth = Target(new Vector2(10, 0)); center = Target(new Vector2(2.5f, 0));
        cast = Cast(owner); To(cast, 3.42f);
        Check(fourth.CurrentHealth == 27 && center.CurrentHealth == 25, "second upgrade AP3 small / AP5 central");
        Check(owner.FinalPossessionEnergy == 0, "second upgrade still costs four energy");
        Object.DestroyImmediate(cast.gameObject); ResetTargets();
        var saved = JsonUtility.FromJson<PlayerGrowthAttributes.RuntimeState>(JsonUtility.ToJson(growth.CaptureRuntimeState()));
        growth.ApplyRuntimeState(default); growth.ApplyRuntimeState(saved);
        Check(growth.HasSkill("holy_light_3") && growth.GetEquippedSkill(0) == "holy_light_3" && growth.IsSkillDiscovered(basic), "save roundtrip preserves learning/equipment/upgrades");
        var learned = Target(new Vector2(20, 20)); mover.enabled = false;
        var learnedMover = learned.GetComponent<ZeldaFourWayMover>(); learnedMover.ReceiveControl(Vector2.up); ZeldaRuntimeRegistry.ClaimControlledMover(learnedMover);
        Energy(learned, 8); cast = Cast(learned); Check(learned.NativeCharacterSkillId != "holy_light" && Get<int>(cast, "bonus") == 1, "learned skill works on other bodies");
        float elapsed = Get<float>(cast, "elapsed"); Call(cast, "Advance", 0f); Check(Get<float>(cast, "elapsed") == elapsed, "scaled zero delta freezes sequence");
        // A scene transition cancels delayed damage, rather than applying it in a destination scene.
        SceneManager.MoveGameObjectToScene(learned.gameObject, otherScene); To(cast, 3.42f);
        Check(Get<bool>(cast, "cancelled") && Get<int>(cast, "roundsIssued") == 0, "travel cancels pending waves");
        Object.DestroyImmediate(cast.gameObject); SceneManager.MoveGameObjectToScene(learned.gameObject, scene);
        Energy(learned, 4); cast = Cast(learned); Set(learned, "isDead", true); To(cast, 3.42f);
        Check(Get<bool>(cast, "cancelled") && Get<int>(cast, "roundsIssued") == 0, "death cancels pending waves");
        Object.DestroyImmediate(cast.gameObject); ResetTargets();
        Check(SkillPageArt.GetAbilitySprite("holy_light") != SkillPageArt.GetAbilitySprite("holy_light_2") &&
            SkillPageArt.GetAbilitySprite("holy_light_2") != SkillPageArt.GetAbilitySprite("holy_light_3"), "distinct upgrade glyphs");
        foreach (string id in new[] { "holy_light", "holy_light_2", "holy_light_3" })
        {
            var node = Array.Find(PlayerGrowthAttributes.Skills, s => s.id == id);
            Check(node.type == PlayerGrowthAttributes.SkillType.Active && node.position.y == -330 && node.position.x >= -90, "requested lower-right chain " + id);
        }
        SkillPreview(growth); EffectsPreview(root);
        // Exercise production Update/time and destruction, not just the deterministic stepping above.
        root.transform.position = new Vector3(50, 50); mover.ReceiveControl(Vector2.down); ZeldaRuntimeRegistry.ClaimControlledMover(mover);
        Energy(owner, 4); Check(owner.TryUseCharacterSkill(), "live runtime cast");
        var live = Object.FindObjectsOfType<HolyLightSkillRuntime>().Single();
        float started = Time.time; while (live != null && Time.time - started < 7f) yield return null;
        Check(live == null && !CameraVisionStreamingRegion.HasActiveRegions, "effect and retention region cleaned up after realtime sequence");
    }
    static void VerifyWaveGeometry()
    {
        foreach (float maximum in new[] { 1f, 3f })
        {
            float duration = maximum == 3 ? 2f : .65f;
            var root = new GameObject("Shockwave boundary test"); var visual = root.AddComponent<HolyLightColumnVisual>();
            visual.Configure(maximum, duration);
            Check(HolyLightSkillRuntime.GetShockwaveRadius(maximum, -.01f, duration) == 0, "no radius before impact");
            foreach (float impactAge in new[] { 0f, .04f, .16f, .3f, .6f, duration })
            {
                visual.RenderAt(impactAge + HolyLightSkillRuntime.DescentDuration);
                float physicsRadius = HolyLightSkillRuntime.GetShockwaveRadius(maximum, impactAge, duration);
                var mesh = Get<Mesh>(visual, "ground"); float furthest = mesh.vertices.Max(v => ((Vector2)v).magnitude);
                Check(Mathf.Abs(furthest - physicsRadius) < .0001f && furthest <= maximum + .0001f,
                    "rendered outer edge equals physics radius at " + impactAge + " / radius " + maximum);
            }
            Object.DestroyImmediate(root);
        }
    }
    static void VerifyColumnReach()
    {
        var cam = new GameObject("Column reach test camera").AddComponent<Camera>();
        cam.tag = "MainCamera"; cam.orthographic = true; cam.aspect = 16f / 9f;
        foreach (float maximum in new[] { 1f, 3f })
        {
            cam.transform.position = new Vector3(1000, 1000, -10);
            cam.transform.rotation = Quaternion.identity; cam.orthographicSize = 7;
            var root = new GameObject("Offscreen column reach test"); root.transform.position = new Vector3(1000, 992, 0);
            var visual = root.AddComponent<HolyLightColumnVisual>(); visual.Configure(maximum, maximum == 3 ? 2 : .65f);
            visual.RenderAt(.08f);
            var mesh = Get<Mesh>(visual, "column"); var initial = mesh.vertices;
            Check(initial.Length == 16, "four batched shafts before landing");
            float fallingBottom = initial[0].y, oldTop = initial[1].y;
            foreach (var setup in new[] { new Vector2(1008, 7), new Vector2(1015, 10), new Vector2(1025, 13) })
            {
                cam.transform.position = new Vector3(1000, setup.x, -10); cam.orthographicSize = setup.y;
                visual.RenderAt(.08f); var points = mesh.vertices;
                float screenTop = setup.x + setup.y;
                for (int shaft = 0; shaft < 4; shaft++)
                    Check(root.transform.TransformPoint(points[shaft * 4 + 1]).y >= screenTop + setup.y * 2 - .001f,
                        "all shaft tips remain a full viewport above view after movement and zoom, radius " + maximum);
                Check(points[0].y == fallingBottom && points[1].y >= oldTop,
                    "extension never reverses descending front or retracts column");
                oldTop = points[1].y;
            }
            cam.transform.position = new Vector3(1000, 995, -10); cam.orthographicSize = 5;
            visual.RenderAt(HolyLightSkillRuntime.DescentDuration);
            Check(mesh.vertices[0].y == 0 && mesh.vertices[1].y == oldTop,
                "landing stays at original time and ground point; downward camera movement does not shorten shaft");
            Check(root.GetComponentsInChildren<MeshRenderer>().Length == 2 && root.GetComponentsInChildren<Collider2D>().Length == 0,
                "longer light uses same two batched meshes, no added damage colliders");
            visual.RenderAt(.5f);
            float beforeZoomRadius = Get<Mesh>(visual, "ground").vertices.Max(v => ((Vector2)v).magnitude);
            cam.orthographicSize = 20; visual.RenderAt(.5f);
            Check(Mathf.Abs(beforeZoomRadius - Get<Mesh>(visual, "ground").vertices.Max(v => ((Vector2)v).magnitude)) < .0001f,
                "camera-dependent height never affects shockwave radius");
            Object.DestroyImmediate(root);
        }
        Object.DestroyImmediate(cam.gameObject);
    }
    static void SkillPreview(PlayerGrowthAttributes growth)
    {
        var root = new GameObject("Holy Light Skill Preview", typeof(RectTransform), typeof(Canvas));
        root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace; root.transform.position = new Vector3(1000, 1000);
        var page = root.AddComponent<CharacterSkillPage>();
        Font font = AssetDatabase.FindAssets("ZLabsPixel_12px_M_CN t:Font").Select(g => AssetDatabase.LoadAssetAtPath<Font>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault();
        page.Build(font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf"));
        Set(page, "selectedId", "holy_light"); Call(page, "Refresh"); Get<SkillTreeScroll>(page, "tree").Zoom(-.4f);
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 30;
        var art = root.GetComponentsInChildren<SkillPageArt>().Where(a => a.holyLight).ToArray();
        Check(art.Length == 3 && art.All(a => a.mainTexture != Texture2D.whiteTexture), "all three tree nodes use light glyphs");
        var cam = new GameObject("Skill Preview Camera").AddComponent<Camera>();
        cam.transform.position = root.transform.position + Vector3.back * 10; cam.orthographic = true; cam.orthographicSize = 285;
        cam.cullingMask = 1 << 30; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;
        Canvas.ForceUpdateCanvases(); Capture(cam, "Holy-light-skill-preview.png", 1600, 850);
        Object.DestroyImmediate(root); Object.DestroyImmediate(cam.gameObject);
    }
    static void EffectsPreview(GameObject priest)
    {
        var root = new GameObject("Effect Preview"); root.transform.position = new Vector3(2000, 2000);
        var body = Object.Instantiate(priest, root.transform); body.transform.localPosition = Vector3.zero;
        foreach (var ai in body.GetComponents<ZeldaCharacterAiBase>()) ai.enabled = false;
        body.GetComponent<ZeldaFourWayMover>().enabled = false;
        var center = new GameObject("Central"); center.transform.SetParent(root.transform, false);
        var visual = center.AddComponent<HolyLightColumnVisual>(); visual.Configure(3, 2); visual.RenderAt(.8f);
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 30;
        var cam = new GameObject("Light Preview Camera").AddComponent<Camera>();
        cam.transform.position = root.transform.position + new Vector3(0, 1.5f, -10); cam.orthographic = true; cam.orthographicSize = 5.3f;
        cam.cullingMask = 1 << 30; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.045f, .067f, .16f);
        Capture(cam, "Holy-light-central-preview.png", 1100, 950);
        cam.transform.position += Vector3.up * 4;
        Capture(cam, "Holy-light-central-upward-preview.png", 1100, 950);
        cam.transform.position -= Vector3.up * 4;
        Object.DestroyImmediate(center);
        for (int round = 0; round < 3; round++) foreach (var d in new[] { Vector2.up, Vector2.right, Vector2.down, Vector2.left })
        {
            var column = new GameObject("Small column"); column.transform.SetParent(root.transform, false); column.transform.localPosition = d * (4 + round * 2);
            var small = column.AddComponent<HolyLightColumnVisual>(); small.Configure(1, .65f); small.RenderAt(.25f + (2 - round) * .3f);
        }
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 30;
        cam.orthographicSize = 11; Capture(cam, "Holy-light-waves-preview.png", 1100, 1100);
        Object.DestroyImmediate(root); Object.DestroyImmediate(cam.gameObject);
    }
    static void Capture(Camera cam, string file, int width, int height)
    {
        var rt = new RenderTexture(width, height, 24); cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
        File.WriteAllBytes(Application.dataPath + "/../" + file, texture.EncodeToPNG());
        RenderTexture.active = null; cam.targetTexture = null;
        Object.DestroyImmediate(texture); Object.DestroyImmediate(rt);
    }
}
