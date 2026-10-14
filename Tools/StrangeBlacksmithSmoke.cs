using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class StrangeBlacksmithSmoke
{
    const string PrefabPath = "Assets/Prefabs/Level2NPCS/Idle/StrangeBlacksmith-level2-Idle.prefab";
    const string NormalPath = "Assets/Prefabs/Level2NPCS/Idle/Blacksmith-level2-Idle.prefab";
    const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static readonly Vector2[] Directions = { Vector2.down, Vector2.up, Vector2.left, Vector2.right };
    static IEnumerator suite;
    static int checks, frame = -1;
    static double deadline;
    static StrangeBlacksmithSmoke() { EditorApplication.update += Tick; }
    static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    public static void Run()
    {
        Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "isolated project required");
        PlayerSettings.productName = "strange-blacksmith-smoke";
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(nameof(StrangeBlacksmithSmoke), true);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(StrangeBlacksmithSmoke), false))
            {
                SessionState.SetBool(nameof(StrangeBlacksmithSmoke), false);
                deadline = EditorApplication.timeSinceStartup + 60;
                suite = Verify();
            }
            if (suite == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("test timeout");
            if (suite.MoveNext()) return;
            suite = null; Debug.Log("STRANGE_BLACKSMITH_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static Sprite SpriteFor(BlacksmithZeldaCharacterData data, int direction, bool attack)
    {
        var renderer = data.GetComponent<SpriteRenderer>();
        data.ApplyCharacterVisual(renderer, Directions[direction], false, attack);
        return renderer.sprite;
    }
    static IEnumerator Verify()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var basic = AssetDatabase.LoadAssetAtPath<GameObject>(NormalPath);
        Check(prefab && basic, "both prefabs import");
        Check(AssetDatabase.FindAssets("StrangeBlacksmith t:Prefab", new[] { "Assets/Prefabs/Level2NPCS" }).Length == 1, "only idle variant created");
        var root = Object.Instantiate(prefab); var normalRoot = Object.Instantiate(basic);
        normalRoot.transform.position = new Vector3(10, 0, 0);
        var data = root.GetComponent<StrangeBlacksmithZeldaCharacterData>();
        var normal = normalRoot.GetComponent<DesertBlacksmithZeldaCharacterData>();
        Check(data && root.GetComponents<ZeldaCharacterData>().Length == 1, "one dedicated data/visual component");
        foreach (var component in root.GetComponentsInChildren<Component>(true)) Check(component, "no missing scripts");
        Check(data.SkillValue == normal.SkillValue && data.SkillValue == 5, "skill value equals existing level2 smith");
        Check(data.Health == normal.Health && data.AttackPower == normal.AttackPower && data.PermissionLevel == normal.PermissionLevel &&
            data.PossessionEnergy == normal.PossessionEnergy && data.PossessionCost == normal.PossessionCost && data.MoveSpeed == normal.MoveSpeed,
            "other core stats equal ordinary smith");
        Check(data.AttackPrefab == normal.AttackPrefab && data.AttackSize == normal.AttackSize &&
            data.AttackSpawnOffset == normal.AttackSpawnOffset && data.AttackDuration == normal.AttackDuration &&
            data.AttackVisualShape == normal.AttackVisualShape, "ordinary smith attack retained");
        Check(string.IsNullOrEmpty(data.NativeCharacterSkillId), "no unrequested skill added");
        var ai = root.GetComponent<ZeldaCharacterAiBase>();
        Check(ai.GetType() == typeof(CivilianIdleCharacterAi) && !root.GetComponent<ZeldaFourWayMover>().enabled &&
            root.GetComponent<ZeldaPatrolCharacterAi>() == null, "idle AI only, possession mover initially disabled");
        Check(new SerializedObject(ai).FindProperty("visionRadius").floatValue == 7 &&
            new SerializedObject(ai).FindProperty("peripheralVisionRadius").floatValue == 1.7f &&
            new SerializedObject(ai).FindProperty("peripheralVisionAngle").floatValue == 240 &&
            new SerializedObject(ai).FindProperty("immediateResponseDistance").floatValue == 5.5f, "level2 vision settings retained");
        var collider = root.GetComponent<BoxCollider2D>(); var baseCollider = normalRoot.GetComponent<BoxCollider2D>();
        Check(collider.size == baseCollider.size && collider.offset == baseCollider.offset && root.transform.localScale == Vector3.one,
            "ordinary smith scale and collision retained");
        var start = root.transform.position;
        for (int i = 0; i < 8; i++) yield return null;
        Check(ai.CurrentState == ZeldaAiState.Idle && root.transform.position == start, "unprovoked idle remains stationary");
        var rowsMethod = typeof(StrangeBlacksmithZeldaCharacterData).GetMethod("GetBodyRows", F);
        var front = (string[])rowsMethod.Invoke(data, new object[] { Vector2.down });
        var left = (string[])rowsMethod.Invoke(data, new object[] { Vector2.left });
        var right = (string[])rowsMethod.Invoke(data, new object[] { Vector2.right });
        Check(front[3].Count(c => c == 'E') == 2 && left[3].Count(c => c == 'E') == 1,
            "work goggles: two frontal lenses and one profile lens");
        Check(front[4].Contains("VVVV") && front.All(row => !row.Contains("F")), "face wrap conceals lower face");
        Check(left.Select((row, i) => row == new string(right[i].Reverse().ToArray())).All(equal => equal),
            "profile body rows are mirrored");
        Check(front.Any(row => row.Contains("X")) && front.Any(row => row.Contains("T")), "patched apron and rivet details");
        for (int direction = 0; direction < 4; direction++)
        {
            var rows = (string[])typeof(StrangeBlacksmithZeldaCharacterData).GetMethod("GetBodyRows", F).Invoke(data, new object[] { Directions[direction] });
            Check(rows.Length == 16 && rows.All(row => row.Length == 16), "four-way 16x16 artwork " + direction);
            Check(rows[11] == ".....PP..PP....." && rows[12] == rows[11] && rows.Skip(13).All(row => row.Trim('.').Length == 0), "short-foot rig retained " + direction);
            var idle = SpriteFor(data, direction, false); var ordinary = SpriteFor(normal, direction, false);
            Check(idle.pixelsPerUnit == 16 && idle.pivot == ordinary.pivot && idle.texture.filterMode == FilterMode.Point, "pixel scale and pivot match " + direction);
            Check(idle.texture.GetPixels32().Where((c, i) => !c.Equals(ordinary.texture.GetPixels32()[i])).Count() > 35, "distinct smith visual " + direction);
            Check(SpriteFor(data, direction, false) == idle, "idle sprites cached " + direction);
            Check(Connected(idle), "idle body and hammer stay connected " + direction);
            var attack = SpriteFor(data, direction, true);
            Check(attack && attack != idle && SpriteFor(data, direction, true) == attack, "four-way cached reach/attack pose " + direction);
            Check(Connected(attack), "extended hammer and arm stay attached " + direction);
            data.ApplyCharacterVisualWithMovement(root.GetComponent<SpriteRenderer>(), Directions[direction], true, false);
            Check(root.GetComponent<SpriteRenderer>().sprite && root.GetComponent<PixelCharacterWalkAnimator>(), "shared walking compatibility " + direction);
        }
        ai.enabled = false;
        var mover = root.GetComponent<ZeldaFourWayMover>(); mover.enabled = true;
        ZeldaRuntimeRegistry.ClaimControlledMover(mover); mover.ReceiveControl(Vector2.down);
        for (int i = 0; i < 4; i++)
        {
            var attack = SpriteFor(data, i, true);
            Check(mover.TryBeginItemAttack(.24f), "possessed worker can reach for interactions " + i);
            var reach = SpriteFor(data, i, true);
            Check(reach && reach != attack && Connected(reach), "separate connected item-use pose " + i);
            mover.EndItemAttack();
        }
        var state = JsonUtility.FromJson<ZeldaCharacterData.RuntimeState>(JsonUtility.ToJson(data.CaptureRuntimeState()));
        Check(Resources.Load<SaveGameCatalog>("SaveGameCatalog").FindCharacter(state.characterType, root.name) == prefab, "save catalog restores distinct character type");
        data.TakeDamage(1); data.ApplyRuntimeState(state);
        Check(data.CurrentHealth == state.currentHealth && data.SkillValue == normal.SkillValue, "damage/save restore preserves stats");
        root.SetActive(false); root.SetActive(true);
        Check(SpriteFor(data, 0, false), "streaming reactivation preserves artwork");
        root.SetActive(false); normalRoot.SetActive(false);
        Preview(data, normal, false); Preview(data, normal, true);
        Object.Destroy(root); Object.Destroy(normalRoot); yield return null;
    }
    static bool Connected(Sprite sprite)
    {
        var rt = RenderTexture.GetTemporary(16, 16, 0, RenderTextureFormat.ARGB32);
        var previous = RenderTexture.active; Graphics.Blit(sprite.texture, rt); RenderTexture.active = rt;
        var image = new Texture2D(16, 16, TextureFormat.RGBA32, false);
        image.ReadPixels(new Rect(0, 0, 16, 16), 0, 0); image.Apply();
        var pixels = image.GetPixels32(); Object.Destroy(image); RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt);
        int seed = Array.FindIndex(pixels, c => c.a > 0);
        if (seed < 0) return false;
        var visited = new bool[256]; var queue = new System.Collections.Generic.Queue<int>();
        visited[seed] = true; queue.Enqueue(seed);
        while (queue.Count > 0)
        {
            int p = queue.Dequeue();
            foreach (int delta in new[] { -16, 16, -1, 1 })
            {
                int n = p + delta;
                if (n < 0 || n >= 256 || Math.Abs(n % 16 - p % 16) > 1 || visited[n] || pixels[n].a == 0) continue;
                visited[n] = true; queue.Enqueue(n);
            }
        }
        return pixels.Where((c, i) => c.a > 0 && !visited[i]).Count() == 0;
    }
    static void Preview(BlacksmithZeldaCharacterData data, BlacksmithZeldaCharacterData normal, bool attack)
    {
        var stage = new GameObject("Preview Stage");
        for (int i = 0; i < 5; i++)
        {
            var go = new GameObject("Preview Actor"); go.transform.SetParent(stage.transform); go.layer = 30;
            go.transform.position = new Vector3((i - 2) * 1.2f, 0, 0);
            go.AddComponent<SpriteRenderer>().sprite = SpriteFor(i == 0 ? normal : data, i == 0 ? 0 : i - 1, attack);
            var caption = new GameObject("Caption"); caption.transform.SetParent(stage.transform); caption.layer = 30;
            caption.transform.position = go.transform.position + new Vector3(0, -.40f, 0);
            var text = caption.AddComponent<TextMesh>(); text.text = new[] { "Smith", "Front", "Back", "Left", "Right" }[i];
            text.fontSize = 36; text.characterSize = .07f; text.anchor = TextAnchor.MiddleCenter;
            text.color = new Color(.65f, .77f, .82f);
        }
        var cam = new GameObject("Preview Camera").AddComponent<Camera>();
        cam.transform.position = new Vector3(0, .1f, -10); cam.orthographic = true; cam.orthographicSize = .83f;
        cam.cullingMask = 1 << 30; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color32(24,31,43,255);
        var rt = new RenderTexture(1280, 320, 24); cam.targetTexture = rt; cam.Render();
        var previous = RenderTexture.active; RenderTexture.active = rt;
        var image = new Texture2D(1280, 320, TextureFormat.RGBA32, false);
        image.ReadPixels(new Rect(0,0,1280,320),0,0); image.Apply();
        File.WriteAllBytes(Application.dataPath + "/../StrangeBlacksmith-" + (attack ? "Attack" : "Idle") + "-preview.png", image.EncodeToPNG());
        RenderTexture.active = previous; cam.targetTexture = null;
        Object.DestroyImmediate(image); Object.DestroyImmediate(rt); Object.DestroyImmediate(cam.gameObject); Object.DestroyImmediate(stage);
    }
}
