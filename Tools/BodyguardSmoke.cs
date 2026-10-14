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
public static class BodyguardSmoke
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    const string PrefabPath = "Assets/Prefabs/Level2NPCS/Idle/Bodyguard-level2.prefab";
    static readonly List<GameObject> owned = new List<GameObject>();
    static IEnumerator test;
    static int checks, frame = -1;
    static BodyguardSmoke() { EditorApplication.update += Tick; }
    public static void Run() { SessionState.SetBool("BodyguardSmoke", true); EditorApplication.EnterPlaymode(); }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return; frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool("BodyguardSmoke", false)) { SessionState.SetBool("BodyguardSmoke", false); test = Verify(); }
            if (test == null || test.MoveNext()) return;
            test = null; Clear(); Debug.Log("BODYGUARD_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { test = null; Debug.LogException(e); Clear(); EditorApplication.Exit(1); }
    }
    static GameObject Own(GameObject go) { owned.Add(go); return go; }
    static void Clear() { for (int i = owned.Count - 1; i >= 0; i--) if (owned[i]) Object.DestroyImmediate(owned[i]); owned.Clear(); }
    static void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
    static Vector2[] Directions = { Vector2.down, Vector2.up, Vector2.left, Vector2.right };
    static Font Font => AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/ZLabsPixel_12px_M_CN.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
    static void SetColor(BodyguardZeldaCharacterData data, string property, Color value)
    {
        var so = new SerializedObject(data); so.FindProperty(property).colorValue = value; so.ApplyModifiedPropertiesWithoutUndo();
        typeof(BodyguardZeldaCharacterData).GetMethod("OnValidate", Flags).Invoke(data, null);
    }
    static IEnumerator Verify()
    {
        Time.timeScale = 0;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Check(prefab != null, "bodyguard prefab imports");
        var root = Own(Object.Instantiate(prefab)); var data = root.GetComponent<BodyguardZeldaCharacterData>();
        Check(data != null && root.GetComponents<ZeldaCharacterData>().Length == 1, "exactly one bodyguard character data component");
        var ai = root.GetComponent<ZeldaCharacterAiBase>(); var mover = root.GetComponent<ZeldaFourWayMover>();
        Check(ai != null && ai.enabled && mover != null && !mover.enabled, "NPC starts with standard stationary guard AI and no player input");
        Check(root.GetComponent<Rigidbody2D>().bodyType == RigidbodyType2D.Kinematic && !root.GetComponent<BoxCollider2D>().isTrigger,
            "normal character collision and movement body");
        Check(data.CurrentHealth == 8 && data.AttackPower == 3 && Mathf.Approximately(data.MoveSpeed, 3.4f), "civilian bodyguard default health, attack and speed");
        Check(data.AttackVisualShape == ZeldaAttackVisualShape.Sword && data.AttackPrefab != null, "shared sword attack is wired");
        Check(PixelCharacterWalkAnimator.Supports(data), "shared walking rig supports bodyguard subclass");
        var config = new SerializedObject(ai);
        Check(config.FindProperty("visionRadius").floatValue == 7f, "bodyguard detection radius configured");
        Check(ai.GetType() == typeof(ZeldaCharacterAiBase), "stationary guard, no military patrol behavior");
        foreach (Component component in root.GetComponents<Component>()) Check(component != null, "no missing script");

        var renderer = root.GetComponent<SpriteRenderer>();
        Sprite[] idle = new Sprite[4], walk = new Sprite[4], attack = new Sprite[4];
        for (int direction = 0; direction < 4; direction++)
        {
            var rows = (string[])typeof(BodyguardZeldaCharacterData).GetField(direction == 0 ? "Front" : direction == 1 ? "Back" : "Left", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            Check(rows.Length == 20, "20 pixel row count");
            foreach (string row in rows) Check(row.Length == 20, "20 pixel column count");
            Check(rows[14] == "......KKK..KKK......" && rows[15] == "......KKK..KKK......", "short boots match walking rig");
            for (int y = 16; y < 20; y++) Check(rows[y] == "....................", "no protruding leg pixels");
            data.ApplyCharacterVisualWithMovement(renderer, Directions[direction], false, false); idle[direction] = renderer.sprite;
            Check(idle[direction] != null && idle[direction].pixelsPerUnit == 16 && idle[direction].texture.filterMode == FilterMode.Point,
                "native point-filtered 16 PPU sprite");
            data.ApplyCharacterVisualWithMovement(renderer, Directions[direction], true, false); walk[direction] = renderer.sprite;
            Check(walk[direction] != idle[direction], "walking uses cached stepping pose");
            data.ApplyCharacterVisualWithMovement(renderer, Directions[direction], false, true); attack[direction] = renderer.sprite;
            Check(attack[direction] != idle[direction], "attack has distinct reaching hand pose");
        }
        Color[] before = idle[0].texture.GetPixels();
        SetColor(data, "tunicColor", new Color(.1f, .65f, .35f));
        data.ApplyCharacterVisual(renderer, Vector2.down, false, false);
        Color[] after = renderer.sprite.texture.GetPixels(); int changed = 0;
        for (int i = 0; i < before.Length; i++)
        {
            Check(before[i].a == after[i].a, "recolor preserves silhouette");
            if (before[i] != after[i]) changed++;
        }
        Check(changed >= 15, "main clothing can be recolored in Inspector");
        for (int y = 14; y < 20; y++) for (int x = 0; x < 20; x++)
            Check(before[y * 20 + x] == after[y * 20 + x], "shirt changes do not recolor headcloth or eyes");
        SetColor(data, "tunicColor", new Color(.65f, .14f, .24f));
        for (int d = 0; d < 4; d++)
        {
            data.ApplyCharacterVisualWithMovement(renderer, Directions[d], false, false); idle[d] = renderer.sprite;
            data.ApplyCharacterVisualWithMovement(renderer, Directions[d], true, false); walk[d] = renderer.sprite;
            data.ApplyCharacterVisualWithMovement(renderer, Directions[d], false, true); attack[d] = renderer.sprite;
        }
        var state = data.CaptureRuntimeState();
        Check(state.characterType.Contains("BodyguardZeldaCharacterData"), "save state keeps distinct bodyguard type");
        data.ApplyRuntimeState(state); Check(data.CurrentHealth == 8, "character state restores normally");
        root.SetActive(false);
        var preview = Preview(idle, walk, attack); while (preview.MoveNext()) yield return preview.Current;

        root.SetActive(true);
        var player = Own(new GameObject("Bodyguard AI test player", typeof(CivilianZeldaCharacterData), typeof(ZeldaFourWayMover)));
        player.transform.position = new Vector2(30, 0); player.GetComponent<Rigidbody2D>().position = player.transform.position;
        ZeldaRuntimeRegistry.ClaimControlledMover(player.GetComponent<ZeldaFourWayMover>());
        typeof(ZeldaCharacterAiBase).GetField("enableAwarenessSound", Flags).SetValue(ai, false);
        ai.OnCharacterDamagedBy(player.GetComponent<ZeldaCharacterData>());
        Check(ai.IsActivelyHostileTo(player.GetComponent<ZeldaFourWayMover>()), "bodyguard uses regular hostile combat, not passive civilian logic");
        Check(SharedPlayerHostilityTimer.ForScene(root.scene).Phase == SharedPlayerHostilityTimer.IncidentPhase.Hostile, "bodyguard participates in shared incident timer");
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
        var camera = Own(new GameObject("Bodyguard preview camera", typeof(Camera))).GetComponent<Camera>();
        camera.enabled = false; camera.orthographic = true; camera.orthographicSize = height / 256f;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color32(20, 29, 44, 255);
        camera.transform.position = new Vector3(0, 0, -30);
        var canvasGo = Own(new GameObject("Bodyguard preview captions", typeof(RectTransform), typeof(Canvas)));
        var canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera;
        var canvasRect = (RectTransform)canvasGo.transform; canvasRect.sizeDelta = new Vector2(width, height); canvasRect.localScale = Vector3.one / 128;
        string[] names = { "正面", "背面", "左侧", "右侧" };
        Sprite[][] poses = { idle, walk, attack };
        string[] rowLabels = { "站立", "行走", "出手" };
        Label(canvasRect, "民用保镖 / BODYGUARD  ·  16 PPU", new Vector2(0, 265), new Vector2(750, 35));
        for (int d = 0; d < 4; d++)
        {
            float x = -225 + d * 175;
            Label(canvasRect, names[d], new Vector2(x, 225), new Vector2(150, 30));
            for (int row = 0; row < 3; row++)
            {
                float y = 90 - row * 150;
                if (d == 0) Label(canvasRect, rowLabels[row], new Vector2(-380, y + 30), new Vector2(85, 30));
                var visual = Own(new GameObject(names[d] + rowLabels[row], typeof(SpriteRenderer)));
                var renderer = visual.GetComponent<SpriteRenderer>(); renderer.sprite = poses[row][d];
                renderer.sortingOrder = 20; visual.transform.position = new Vector3(x / 128f, y / 128f, 0);
            }
        }
        Label(canvasRect, "布质头带 · 酒红短褂 · 褐色皮背心 / 非制式军装，无金属肩甲", new Vector2(0, -260), new Vector2(840, 30));
        var rt = new RenderTexture(width, height, 24); camera.targetTexture = rt;
        yield return null; Canvas.ForceUpdateCanvases(); camera.Render(); var old = RenderTexture.active; RenderTexture.active = rt;
        var image = new Texture2D(width, height, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
        int tan = 0, red = 0; foreach (Color32 p in image.GetPixels32())
        { if (p.r > 145 && p.g > 120 && p.b > 60 && p.r > p.b * 1.2f) tan++; if (p.r > 110 && p.r > p.g * 1.8f && p.b < p.r) red++; }
        Check(tan > 4000 && red > 1000, "rendered bodyguard has visible cloth wraps and burgundy tunic");
        File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), "Bodyguard-preview.png"), image.EncodeToPNG());
        RenderTexture.active = old; camera.targetTexture = null; rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(image);
    }
}
