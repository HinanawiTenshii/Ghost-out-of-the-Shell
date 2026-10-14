using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Run in the isolated validation project, not the open working scene.
[InitializeOnLoad]
public static class AdvancedPriestSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    const string Path = "Assets/Prefabs/Level2NPCS/Idle/AdvancedPriest-level2-Idle.prefab";
    static IEnumerator suite; static int frame = -1, checks;
    static readonly List<GameObject> owned = new List<GameObject>();
    static AdvancedPriestSmoke() { EditorApplication.update += Tick; }
    public static void Run()
    {
        Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "isolated test project");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(nameof(AdvancedPriestSmoke), true); EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return; frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(AdvancedPriestSmoke), false))
            { SessionState.SetBool(nameof(AdvancedPriestSmoke), false); suite = Verify(); }
            if (suite == null || suite.MoveNext()) return;
            suite = null; Cleanup(); Debug.Log("ADVANCED_PRIEST_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Debug.LogException(e); Cleanup(); EditorApplication.Exit(1); }
    }
    static void Cleanup() { foreach (var go in owned) if (go) Object.DestroyImmediate(go); owned.Clear(); }
    static void Check(bool result, string message) { checks++; if (!result) throw new Exception(message); }
    static Sprite Render(PriestZeldaCharacterData data, int direction, int pose) =>
        (Sprite)typeof(PriestZeldaCharacterData).GetMethod("CreateSprite", F).Invoke(data, new object[] { direction, pose });
    static void Release(Sprite sprite) { Object.DestroyImmediate(sprite.texture); Object.DestroyImmediate(sprite); }
    static IEnumerator Verify()
    {
        Time.timeScale = 0f;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Path);
        var basic = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Level2NPCS/Idle/Priest-level2-Idle.prefab");
        Check(prefab != null && basic != null, "prefabs import");
        Check(AssetDatabase.FindAssets("AdvancedPriest t:Prefab", new[] { "Assets/Prefabs/Level2NPCS" }).Length == 1,
            "only requested idle variant exists");
        var root = Object.Instantiate(prefab); owned.Add(root);
        var normalRoot = Object.Instantiate(basic); owned.Add(normalRoot);
        var data = root.GetComponent<AdvancedPriestZeldaCharacterData>();
        var normal = normalRoot.GetComponent<PriestZeldaCharacterData>();
        Check(data && root.GetComponents<ZeldaCharacterData>().Length == 1, "one distinct advanced priest data component");
        foreach (var c in root.GetComponentsInChildren<Component>(true)) Check(c, "no missing script");
        Check(root.GetComponent<ZeldaCharacterAiBase>().GetType() == typeof(CivilianIdleCharacterAi) &&
            !root.GetComponent<ZeldaFourWayMover>().enabled, "stationary idle AI, not patrol");
        // The advanced prefab is now independently tuned in the editor; preserve those values, not the basic priest's.
        var authored = prefab.GetComponent<AdvancedPriestZeldaCharacterData>();
        Check(data.Health == authored.Health && data.AttackPower == authored.AttackPower && data.SkillValue == authored.SkillValue &&
            data.PossessionEnergy == authored.PossessionEnergy && data.PossessionCost == authored.PossessionCost &&
            data.PermissionLevel == authored.PermissionLevel && data.MoveSpeed == authored.MoveSpeed, "authored advanced priest stats preserved");
        Check(data.NativeCharacterSkillId == "holy_light" && root.GetComponent<HolyLightCharacterSkillData>() != null,
            "advanced priest native holy light skill attached");
        Check(data.AttackVisualShape == ZeldaAttackVisualShape.Book && data.AttackPrefab &&
            data.AttackSize == normal.AttackSize && data.AttackSpawnOffset == normal.AttackSpawnOffset,
            "original book attack rig retained");
        var a = root.GetComponent<BoxCollider2D>(); var b = normalRoot.GetComponent<BoxCollider2D>();
        Check(a.size == b.size && a.offset == b.offset && root.transform.localScale == Vector3.one, "collision and world scale unchanged");
        var ai = new SerializedObject(root.GetComponent<ZeldaCharacterAiBase>());
        Check(ai.FindProperty("visionRadius").floatValue == 7f && ai.FindProperty("peripheralVisionAngle").floatValue == 240f &&
            ai.FindProperty("peripheralVisionRadius").floatValue == 1.7f && ai.FindProperty("immediateResponseDistance").floatValue == 5.5f,
            "level 2 unified vision configuration");
        foreach (int direction in Enumerable.Range(0, 4)) foreach (int pose in Enumerable.Range(0, 4))
        {
            var sprite = Render(data, direction, pose); var pixels = sprite.texture.GetPixels();
            Check(sprite.texture.width == 16 && sprite.texture.height == 21 && sprite.pixelsPerUnit == 16 &&
                Vector2.Distance(sprite.pivot, new Vector2(8, 4)) < .01f && sprite.texture.filterMode == FilterMode.Point,
                "16 PPU extended crown keeps fixed foot pivot");
            Check(pixels.Any(c => c.a > .5f && c.r > c.g * 2f) &&
                pixels.Any(c => c.a > .5f && c.r > .8f && c.g > .65f && c.b < .5f), "red and gold in every direction/pose");
            // Idle/walk keeps one connected costume, with empty padding below its boots.
            Check(pixels.Take(16).All(c => c.a == 0), "no foot protrusions at texture bottom");
            Release(sprite);
        }
        // Base maps and their legacy 16x16 standing/walking pixels remain unchanged.
        var maps = new[] { "Front", "Back", "Left", "Right" };
        for (int d = 0; d < 4; d++) for (int p = 0; p < 3; p++)
        {
            var rows = (string[])typeof(PriestZeldaCharacterData).GetField(maps[d], BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            var expected = new Color[256];
            for (int row = 0; row < 16; row++) for (int x = 0; x < 16; x++)
            {
                char c = rows[row][x]; if (c == '.') continue;
                int y = 15 - row; if (row >= 12 && (p == 1 || p == 2)) y += (x < 8) == (p == 1) ? 1 : 0;
                expected[y * 16 + x] = (Color)typeof(PriestZeldaCharacterData).GetMethod("PixelColor", F).Invoke(normal, new object[] { c });
            }
            var sprite = Render(normal, d, p); var actual = sprite.texture.GetPixels32();
            Check(actual.Length == 256 && actual.Select((c, i) => c.Equals((Color32)expected[i])).All(v => v),
                "original priest appearance regression " + d + "/" + p);
            Release(sprite);
        }
        var sr = root.GetComponent<SpriteRenderer>(); data.ApplyCharacterVisual(sr, Vector2.down, false, false);
        var idle = sr.sprite; data.ApplyCharacterVisual(sr, Vector2.down, false, false);
        Check(sr.sprite == idle, "runtime sprite cache reused");
        var crown = idle.texture.GetPixels32();
        Check(Enumerable.Range(13, 8).All(y => Enumerable.Range(0, 8).All(x => crown[y * 16 + x].Equals(crown[y * 16 + 15 - x]))),
            "three-tier frontal crown is horizontally symmetrical");
        Check(Enumerable.Range(0, 16).Count(x => crown[20 * 16 + x].a > 0) == 2 &&
            Enumerable.Range(0, 16).Count(x => crown[19 * 16 + x].a > 0) == 4 &&
            Enumerable.Range(0, 16).Count(x => crown[18 * 16 + x].a > 0) == 2, "small readable crown cross");
        var state = JsonUtility.FromJson<ZeldaCharacterData.RuntimeState>(JsonUtility.ToJson(data.CaptureRuntimeState()));
        Check(Resources.Load<SaveGameCatalog>("SaveGameCatalog").FindCharacter(state.characterType, root.name) == prefab,
            "save catalog restores correct red-gold type even after possession");
        data.TakeDamage(1); data.ApplyRuntimeState(state);
        Check(data.CurrentHealth == state.currentHealth, "damage and save restoration function");
        root.SetActive(false); root.SetActive(true); data.ApplyCharacterVisual(sr, Vector2.right, false, false);
        Check(sr.sprite != null, "distance streaming reactivation keeps appearance");
        root.SetActive(false); normalRoot.SetActive(false);
        Preview(data, normal, 0, "Idle"); Preview(data, normal, 3, "Attack");
        yield return null;
    }
    static void Preview(PriestZeldaCharacterData data, PriestZeldaCharacterData normal, int pose, string label)
    {
        var displays = new List<GameObject>(); var sprites = new List<Sprite>();
        for (int i = 0; i < 5; i++)
        {
            var go = new GameObject("Preview actor"); displays.Add(go); go.layer = 30;
            go.transform.position = new Vector3((i - 2) * 1.4f, 0, 0);
            var sprite = Render(i == 0 ? normal : data, i == 0 ? 0 : i - 1, pose); sprites.Add(sprite);
            go.AddComponent<SpriteRenderer>().sprite = sprite;
            var caption = new GameObject("Caption"); displays.Add(caption); caption.layer = 30;
            caption.transform.position = go.transform.position + new Vector3(0, -.38f, 0);
            var text = caption.AddComponent<TextMesh>(); text.text = new[] { "Priest", "Front", "Back", "Left", "Right" }[i];
            text.fontSize = 36; text.characterSize = .07f; text.anchor = TextAnchor.MiddleCenter;
            text.color = new Color(.65f, .77f, .82f);
        }
        var cam = new GameObject("Preview camera").AddComponent<Camera>();
        cam.transform.position = new Vector3(0, .38f, -10); cam.orthographic = true; cam.orthographicSize = .96f;
        cam.cullingMask = 1 << 30; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color32(24,31,43,255);
        var rt = new RenderTexture(1280, 360, 24); cam.targetTexture = rt; cam.Render();
        RenderTexture.active = rt; var image = new Texture2D(1280, 360, TextureFormat.RGBA32, false);
        image.ReadPixels(new Rect(0, 0, 1280, 360), 0, 0); image.Apply();
        File.WriteAllBytes(Application.dataPath + "/../AdvancedPriest-" + label + "-preview.png", image.EncodeToPNG());
        RenderTexture.active = null; cam.targetTexture = null;
        foreach (var go in displays) Object.DestroyImmediate(go);
        foreach (var sprite in sprites) Release(sprite);
        Object.DestroyImmediate(image); Object.DestroyImmediate(rt); Object.DestroyImmediate(cam.gameObject);
    }
}
