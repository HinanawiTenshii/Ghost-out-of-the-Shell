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
public static class StrangeMerchantSmoke
{
    const string PrefabPath = "Assets/Prefabs/Level2NPCS/Idle/StrangeMerchant-level2-Idle.prefab";
    const string NormalPath = "Assets/Prefabs/Level2NPCS/Idle/Merchant-level2-Idle.prefab";
    const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static readonly Vector2[] Directions = { Vector2.down, Vector2.up, Vector2.left, Vector2.right };
    static IEnumerator suite;
    static int checks, frame = -1;
    static double deadline;
    static StrangeMerchantSmoke() { EditorApplication.update += Tick; }
    static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    public static void Run()
    {
        Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "isolated project required");
        PlayerSettings.productName = "strange-merchant-smoke";
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(nameof(StrangeMerchantSmoke), true);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(StrangeMerchantSmoke), false))
            {
                SessionState.SetBool(nameof(StrangeMerchantSmoke), false);
                deadline = EditorApplication.timeSinceStartup + 60;
                suite = Verify();
            }
            if (suite == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("test timeout");
            if (suite.MoveNext()) return;
            suite = null; Debug.Log("STRANGE_MERCHANT_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static Sprite SpriteFor(CivilianZeldaCharacterData data, int direction, bool attack)
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
        Check(AssetDatabase.FindAssets("StrangeMerchant t:Prefab", new[] { "Assets/Prefabs/Level2NPCS" }).Length == 1, "only idle variant created");
        var root = Object.Instantiate(prefab); var normalRoot = Object.Instantiate(basic);
        normalRoot.transform.position = new Vector3(10, 0, 0);
        var data = root.GetComponent<StrangeMerchantZeldaCharacterData>();
        var normal = normalRoot.GetComponent<DesertMerchantZeldaCharacterData>();
        Check(data && root.GetComponents<ZeldaCharacterData>().Length == 1, "one dedicated data/visual component");
        foreach (var component in root.GetComponentsInChildren<Component>(true)) Check(component, "no missing scripts");
        Check(data.SkillValue == 10, "skill value exactly ten");
        Check(data.Health == normal.Health && data.AttackPower == normal.AttackPower && data.PermissionLevel == normal.PermissionLevel &&
            data.PossessionEnergy == normal.PossessionEnergy && data.PossessionCost == normal.PossessionCost && data.MoveSpeed == normal.MoveSpeed,
            "other core stats equal ordinary merchant");
        Check(data.AttackPrefab == normal.AttackPrefab && data.AttackSize == normal.AttackSize &&
            data.AttackSpawnOffset == normal.AttackSpawnOffset && data.AttackDuration == normal.AttackDuration &&
            data.AttackVisualShape == normal.AttackVisualShape, "ordinary merchant attack retained");
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
            "ordinary merchant scale and collision retained");
        var start = root.transform.position;
        for (int i = 0; i < 8; i++) yield return null;
        Check(ai.CurrentState == ZeldaAiState.Idle && root.transform.position == start, "unprovoked idle remains stationary");
        var frontRows = (string[])typeof(StrangeMerchantZeldaCharacterData).GetMethod("GetBodyRows", F).Invoke(data, new object[] { Vector2.down });
        var leftRows = (string[])typeof(StrangeMerchantZeldaCharacterData).GetMethod("GetBodyRows", F).Invoke(data, new object[] { Vector2.left });
        var rightRows = (string[])typeof(StrangeMerchantZeldaCharacterData).GetMethod("GetBodyRows", F).Invoke(data, new object[] { Vector2.right });
        foreach (int row in new[] { 3, 4 })
        {
            Check(frontRows[row] == new string(frontRows[row].Reverse().ToArray()), "front face is symmetrical " + row);
            Check(leftRows[row] == new string(rightRows[row].Reverse().ToArray()), "profile faces mirror each other " + row);
            Check(new[] { frontRows[row], leftRows[row], rightRows[row] }.All(r => !r.Any(c => c == 'M' || c == 'V' || c == 'F' || c == 'S')),
                "no mask or exposed skin in shadowed face " + row);
        }
        Check(frontRows[3].Count(c => c == 'E') == 2 && leftRows[3].Count(c => c == 'E') == 1 && rightRows[3].Count(c => c == 'E') == 1,
            "two matching eyes, one visible per profile");
        var facePixels = SpriteFor(data, 0, false).texture.GetPixels32();
        Check(facePixels[12 * 16 + 6].Equals(facePixels[12 * 16 + 9]) &&
            facePixels[12 * 16 + 6].Equals((Color32)new Color(1f, .65f, .20f, 1f)), "two matching orange eyes");
        Check(Enumerable.Range(5, 6).All(x => facePixels[11 * 16 + x].Equals((Color32)new Color(.07f, .10f, .14f, 1f))),
            "entire face is original shadow-black instead of skin or mask");
        foreach (int direction in new[] { 2, 3 })
        {
            var profile = SpriteFor(data, direction, false).texture.GetPixels32();
            int eyeX = direction == 2 ? 5 : 10;
            Check(profile[12 * 16 + eyeX].Equals(facePixels[12 * 16 + 6]), "profile eye matches orange front eyes " + direction);
        }
        for (int direction = 0; direction < 4; direction++)
        {
            var rows = (string[])typeof(StrangeMerchantZeldaCharacterData).GetMethod("GetBodyRows", F).Invoke(data, new object[] { Directions[direction] });
            Check(rows.Length == 16 && rows.All(row => row.Length == 16), "four-way 16x16 artwork " + direction);
            Check(rows[11] == ".....KK..KK....." && rows[12] == rows[11] && rows.Skip(13).All(row => row.Trim('.').Length == 0), "short-foot rig retained " + direction);
            var idle = SpriteFor(data, direction, false); var ordinary = SpriteFor(normal, direction, false);
            Check(idle.pixelsPerUnit == 16 && idle.pivot == ordinary.pivot && idle.texture.filterMode == FilterMode.Point, "pixel scale and pivot match " + direction);
            Check(idle.texture.GetPixels32().Where((c, i) => !c.Equals(ordinary.texture.GetPixels32()[i])).Count() > 35, "distinct merchant visual " + direction);
            Check(SpriteFor(data, direction, false) == idle, "idle sprites cached " + direction);
            var attack = SpriteFor(data, direction, true);
            Check(attack && attack != idle && SpriteFor(data, direction, true) == attack, "four-way cached reach/attack pose " + direction);
            data.ApplyCharacterVisualWithMovement(root.GetComponent<SpriteRenderer>(), Directions[direction], true, false);
            Check(root.GetComponent<SpriteRenderer>().sprite && root.GetComponent<PixelCharacterWalkAnimator>(), "shared walking compatibility " + direction);
        }
        var state = JsonUtility.FromJson<ZeldaCharacterData.RuntimeState>(JsonUtility.ToJson(data.CaptureRuntimeState()));
        Check(Resources.Load<SaveGameCatalog>("SaveGameCatalog").FindCharacter(state.characterType, root.name) == prefab, "save catalog restores distinct character type");
        data.TakeDamage(1); data.ApplyRuntimeState(state);
        Check(data.CurrentHealth == state.currentHealth && data.SkillValue == 10, "damage/save restore preserves stats");
        root.SetActive(false); root.SetActive(true);
        Check(SpriteFor(data, 0, false), "streaming reactivation preserves artwork");
        root.SetActive(false); normalRoot.SetActive(false);
        Preview(data, normal, false); Preview(data, normal, true);
        Object.Destroy(root); Object.Destroy(normalRoot); yield return null;
    }
    static void Preview(CivilianZeldaCharacterData data, CivilianZeldaCharacterData normal, bool attack)
    {
        var stage = new GameObject("Preview Stage");
        for (int i = 0; i < 5; i++)
        {
            var go = new GameObject("Preview Actor"); go.transform.SetParent(stage.transform); go.layer = 30;
            go.transform.position = new Vector3((i - 2) * 1.2f, 0, 0);
            go.AddComponent<SpriteRenderer>().sprite = SpriteFor(i == 0 ? normal : data, i == 0 ? 0 : i - 1, attack);
            var caption = new GameObject("Caption"); caption.transform.SetParent(stage.transform); caption.layer = 30;
            caption.transform.position = go.transform.position + new Vector3(0, -.40f, 0);
            var text = caption.AddComponent<TextMesh>(); text.text = new[] { "Merchant", "Front", "Back", "Left", "Right" }[i];
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
        File.WriteAllBytes(Application.dataPath + "/../StrangeMerchant-" + (attack ? "Attack" : "Idle") + "-preview.png", image.EncodeToPNG());
        RenderTexture.active = previous; cam.targetTexture = null;
        Object.DestroyImmediate(image); Object.DestroyImmediate(rt); Object.DestroyImmediate(cam.gameObject); Object.DestroyImmediate(stage);
    }
}
