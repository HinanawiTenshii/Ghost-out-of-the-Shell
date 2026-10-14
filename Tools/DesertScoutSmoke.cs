using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class DesertScoutSmoke
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    const string PrefabPath = "Assets/Prefabs/Level2NPCS/Patrol/DesertScout-level2.prefab";
    static readonly List<GameObject> owned = new List<GameObject>();
    static IEnumerator test;
    static int checks, frame = -1;
    static DesertScoutSmoke() { EditorApplication.update += Tick; }
    public static void Run() { SessionState.SetBool("DesertScoutSmoke", true); EditorApplication.EnterPlaymode(); }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return; frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool("DesertScoutSmoke", false)) { SessionState.SetBool("DesertScoutSmoke", false); test = Verify(); }
            if (test == null || test.MoveNext()) return;
            test = null; Clear(); Debug.Log("DESERT_SCOUT_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { test = null; Debug.LogException(e); Clear(); EditorApplication.Exit(1); }
    }
    static GameObject Own(GameObject go) { owned.Add(go); return go; }
    static void Clear() { for (int i = owned.Count - 1; i >= 0; i--) if (owned[i]) Object.DestroyImmediate(owned[i]); owned.Clear(); }
    static void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
    static Vector2[] Directions = { Vector2.down, Vector2.up, Vector2.left, Vector2.right };
    static Font Font => AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/ZLabsPixel_12px_M_CN.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
    static void SetColor(DesertScoutZeldaCharacterData data, string property, Color value)
    {
        var so = new SerializedObject(data); so.FindProperty(property).colorValue = value; so.ApplyModifiedPropertiesWithoutUndo();
        typeof(CivilianZeldaCharacterData).GetMethod("OnValidate", Flags).Invoke(data, null);
    }
    static IEnumerator Verify()
    {
        Time.timeScale = 0;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Check(prefab != null, "scout prefab imports");
        var root = Own(Object.Instantiate(prefab)); var data = root.GetComponent<DesertScoutZeldaCharacterData>();
        Check(data != null && root.GetComponents<ZeldaCharacterData>().Length == 1, "exactly one scout character data component");
        var ai = root.GetComponent<ZeldaPatrolCharacterAi>(); var mover = root.GetComponent<ZeldaFourWayMover>();
        Check(ai != null && ai.enabled && mover != null && !mover.enabled, "NPC starts with standard patrol AI and no player input");
        Check(root.GetComponent<Rigidbody2D>().bodyType == RigidbodyType2D.Kinematic && !root.GetComponent<BoxCollider2D>().isTrigger,
            "normal character collision and movement body");
        Check(data.CurrentHealth == 5 && data.AttackPower == 2 && Mathf.Approximately(data.MoveSpeed, 4.2f), "light scout default health, attack and speed");
        Check(data.AttackVisualShape == ZeldaAttackVisualShape.Sword && data.AttackPrefab != null, "shared sword attack is wired");
        Check(PixelCharacterWalkAnimator.Supports(data), "shared walking rig supports scout subclass");
        var config = new SerializedObject(ai);
        Check(config.FindProperty("visionRadius").floatValue == 7f, "scout detection radius configured");
        Check(config.FindProperty("patrolPoint2Offset").vector2Value != Vector2.zero, "editable patrol offsets configured");
        foreach (Component component in root.GetComponents<Component>()) Check(component != null, "no missing script");

        var renderer = root.GetComponent<SpriteRenderer>();
        Sprite[] idle = new Sprite[4], walk = new Sprite[4], attack = new Sprite[4];
        for (int direction = 0; direction < 4; direction++)
        {
            var rows = (string[])typeof(DesertScoutZeldaCharacterData).GetMethod("GetBodyRows", Flags).Invoke(data, new object[] { Directions[direction] });
            Check(rows.Length == 16, "16 pixel row count");
            foreach (string row in rows) Check(row.Length == 16, "16 pixel column count");
            Check(rows[11] == ".....KK..KK....." && rows[12] == ".....KK..KK.....", "short boots match walking rig");
            for (int y = 13; y < 16; y++) Check(rows[y] == "................", "no protruding leg pixels");
            data.ApplyCharacterVisualWithMovement(renderer, Directions[direction], false, false); idle[direction] = renderer.sprite;
            Check(idle[direction] != null && idle[direction].pixelsPerUnit == 16 && idle[direction].texture.filterMode == FilterMode.Point,
                "native point-filtered 16 PPU sprite");
            data.ApplyCharacterVisualWithMovement(renderer, Directions[direction], true, false); walk[direction] = renderer.sprite;
            Check(walk[direction] != idle[direction], "walking uses cached stepping pose");
            data.ApplyCharacterVisualWithMovement(renderer, Directions[direction], false, true); attack[direction] = renderer.sprite;
            Check(attack[direction] != idle[direction], "attack has distinct reaching hand pose");
        }
        Color[] before = idle[0].texture.GetPixels();
        SetColor(data, "clothingColor", new Color(.65f, .12f, .22f));
        data.ApplyCharacterVisual(renderer, Vector2.down, false, false);
        Color[] after = renderer.sprite.texture.GetPixels(); int changed = 0;
        for (int i = 0; i < before.Length; i++)
        {
            Check(before[i].a == after[i].a, "recolor preserves silhouette");
            if (before[i] != after[i]) changed++;
        }
        Check(changed >= 15, "main clothing can be recolored in Inspector");
        for (int y = 11; y < 16; y++) for (int x = 0; x < 16; x++)
            Check(before[y * 16 + x] == after[y * 16 + x], "shirt changes do not recolor headcloth or eyes");
        SetColor(data, "clothingColor", new Color(.72f, .43f, .18f));
        for (int d = 0; d < 4; d++)
        {
            data.ApplyCharacterVisualWithMovement(renderer, Directions[d], false, false); idle[d] = renderer.sprite;
            data.ApplyCharacterVisualWithMovement(renderer, Directions[d], true, false); walk[d] = renderer.sprite;
            data.ApplyCharacterVisualWithMovement(renderer, Directions[d], false, true); attack[d] = renderer.sprite;
        }
        var state = data.CaptureRuntimeState();
        Check(state.characterType.Contains("DesertScoutZeldaCharacterData"), "save state keeps distinct scout type");
        data.ApplyRuntimeState(state); Check(data.CurrentHealth == 5, "character state restores normally");
        root.SetActive(false);
        var preview = Preview(idle, walk, attack); while (preview.MoveNext()) yield return preview.Current;

        root.SetActive(true);
        var player = Own(new GameObject("Scout AI test player", typeof(CivilianZeldaCharacterData), typeof(ZeldaFourWayMover)));
        player.transform.position = new Vector2(30, 0); player.GetComponent<Rigidbody2D>().position = player.transform.position;
        ZeldaRuntimeRegistry.ClaimControlledMover(player.GetComponent<ZeldaFourWayMover>());
        typeof(ZeldaCharacterAiBase).GetField("enableAwarenessSound", Flags).SetValue(ai, false);
        ai.OnCharacterDamagedBy(player.GetComponent<ZeldaCharacterData>());
        Check(ai.IsActivelyHostileTo(player.GetComponent<ZeldaFourWayMover>()), "scout uses regular hostile combat, not passive civilian logic");
        Check(SharedPlayerHostilityTimer.ForScene(root.scene).Phase == SharedPlayerHostilityTimer.IncidentPhase.Hostile, "scout participates in shared incident timer");
    }

    static Text Label(Transform parent, string value, Vector2 position, Vector2 size)
    {
        var go = Own(new GameObject(value, typeof(RectTransform), typeof(Text))); go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform; rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
        rect.anchoredPosition = position; rect.sizeDelta = size;
        var text = go.GetComponent<Text>(); text.font = Font; text.text = value; text.fontSize = 16;
        text.alignment = TextAnchor.MiddleCenter; text.color = new Color32(174, 217, 223, 255); return text;
    }
    static IEnumerator Preview(Sprite[] idle, Sprite[] walk, Sprite[] attack)
    {
        const int width = 896, height = 580;
        var camera = Own(new GameObject("Scout preview camera", typeof(Camera))).GetComponent<Camera>();
        camera.enabled = false; camera.orthographic = true; camera.orthographicSize = height / 256f;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color32(20, 29, 44, 255);
        camera.transform.position = new Vector3(0, 0, -30);
        var canvasGo = Own(new GameObject("Scout preview captions", typeof(RectTransform), typeof(Canvas)));
        var canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera;
        var canvasRect = (RectTransform)canvasGo.transform; canvasRect.sizeDelta = new Vector2(width, height); canvasRect.localScale = Vector3.one / 128;
        string[] names = { "正面", "背面", "左侧", "右侧" };
        Sprite[][] poses = { idle, walk, attack };
        string[] rowLabels = { "站立", "行走", "出手" };
        Label(canvasRect, "沙漠斥候 / DESERT SCOUT  ·  16 PPU", new Vector2(0, 265), new Vector2(750, 35));
        for (int d = 0; d < 4; d++)
        {
            float x = -225 + d * 175;
            Label(canvasRect, names[d], new Vector2(x, 225), new Vector2(150, 30));
            for (int row = 0; row < 3; row++)
            {
                float y = 110 - row * 155;
                if (d == 0) Label(canvasRect, rowLabels[row], new Vector2(-380, y + 30), new Vector2(85, 30));
                var visual = Own(new GameObject(names[d] + rowLabels[row], typeof(SpriteRenderer)));
                var renderer = visual.GetComponent<SpriteRenderer>(); renderer.sprite = poses[row][d];
                renderer.sortingOrder = 20; visual.transform.position = new Vector3(x / 128f, y / 128f, 0);
            }
        }
        Label(canvasRect, "沙色面巾 · 绿色短披肩 · 褐色皮具 / 衣服与配饰可独立调色", new Vector2(0, -260), new Vector2(840, 30));
        var rt = new RenderTexture(width, height, 24); camera.targetTexture = rt;
        yield return null; Canvas.ForceUpdateCanvases(); camera.Render(); var old = RenderTexture.active; RenderTexture.active = rt;
        var image = new Texture2D(width, height, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
        int tan = 0, green = 0; foreach (Color32 p in image.GetPixels32())
        { if (p.r > 145 && p.g > 120 && p.b > 60 && p.r > p.b * 1.2f) tan++; if (p.g > 100 && p.g > p.r * 1.5f && p.g > p.b * 1.1f) green++; }
        Check(tan > 4000 && green > 1000, "rendered scout has visible sandcloth and green cape");
        File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), "DesertScout-preview.png"), image.EncodeToPNG());
        RenderTexture.active = old; camera.targetTexture = null; rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(image);
    }
}
