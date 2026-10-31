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

// Run only in the disposable Unity project; never reads/writes the player's saves.
[InitializeOnLoad]
public static class KeyVisualSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static IEnumerator suite;
    static int checks, frame = -1;
    static double deadline;
    static KeyVisualSmoke() { EditorApplication.update += Tick; }
    static void Check(bool ok, string label)
    { if (!ok) throw new Exception(label); checks++; Debug.Log("KEY_VISUAL_CHECK " + label); }
    static object Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, F).Invoke(o, args);
    static void Set(object o, string name, object value)
    {
        for (Type t = o.GetType(); t != null; t = t.BaseType)
        { var field = t.GetField(name, F); if (field != null) { field.SetValue(o, value); return; } }
        throw new Exception("Missing field " + name);
    }
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/")) throw new Exception("isolated only");
        PlayerSettings.productName = "key-visual-smoke";
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(nameof(KeyVisualSmoke), true);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(KeyVisualSmoke), false))
            { SessionState.SetBool(nameof(KeyVisualSmoke), false); suite = Verify(); deadline = EditorApplication.timeSinceStartup + 120; }
            if (suite == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Key visual timeout");
            if (suite.MoveNext()) return;
            suite = null; Debug.Log("KEY_VISUAL_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static IEnumerator Verify()
    {
        while (Time.frameCount < 3 || RetroSceneLoadReveal.IsPreparingGameplay) yield return null;
        var prefab = Resources.Load<KeyPickupItem>("PickupItems/KeyPickupItem");
        Check(prefab != null && prefab.MaxStackSize == 1 && prefab.ItemId == "key", "existing key prefab and data unchanged");
        var template = prefab.GetComponent<KeyPickupItemVisual>();
        Color defaultColor = template.DisplayColor;
        var item = Object.Instantiate(prefab);
        var visual = item.GetComponent<KeyPickupItemVisual>();
        var renderer = item.GetComponent<SpriteRenderer>();
        var rows = (string[])Call(visual, "GetPixelRows");
        Check(rows.Length == 14 && rows.All(r => r.Length == 24), "rectangular hand-authored pixel map");
        Check(item.transform.localScale == Vector3.one * .55f && item.GetComponent<BoxCollider2D>().size == new Vector2(1.1f, .65f), "world scale and pickup hitbox preserved");
        Check(renderer.sprite == visual.InventoryIcon && renderer.color == Color.white, "world uses embedded metal and enamel");
        Check(visual.InventoryIcon.texture.filterMode == FilterMode.Point, "pixel filtering");
        var breathing = item.transform.Find("Pickup Breathing Visual").GetComponent<SpriteRenderer>();
        Check(breathing.sprite == renderer.sprite && breathing.color == Color.white, "breathing copy correct on first frame");

        var invGo = new GameObject("Key Test Inventory"); invGo.SetActive(false);
        var inv = invGo.AddComponent<PersistentInventory>(); Set(inv, "saveToPlayerPrefs", false);
        invGo.SetActive(true); inv.ClearAll(); inv.enabled = false;
        var actor = new GameObject("Key Collector").AddComponent<ZeldaCharacterData>();
        var mover = actor.gameObject.AddComponent<ZeldaFourWayMover>();
        ZeldaRuntimeRegistry.ClaimControlledMover(mover);

        Color red = new Color32(194, 69, 59, 255), blue = new Color32(48, 125, 205, 255);
        visual.SetDisplayColor(red);
        string identity = item.UniqueInstanceId;
        Check(visual.DisplayColor == red && renderer.color == Color.white && breathing.sprite == renderer.sprite, "changing colour affects enamel without whole-sprite tint");
        Color32[] redPixels = ReadPixels(visual.InventoryIcon);
        Check(item.TryPickUp(), "red key pickup"); CloseDescription();
        Check(inv.GetSlot(0).VisualColor == red && inv.GetSlot(0).ItemInstanceId == identity, "colour and stable identity captured");
        Check(inv.TryAddUniqueItem("key", "key-visual-blue", hasVisualColor: true, visualColor: blue), "second independent coloured slot");
        Sprite redIcon = template.GetInventoryIcon(inv.GetSlot(0)), blueIcon = template.GetInventoryIcon(inv.GetSlot(1));
        Check(redIcon != blueIcon && template.DisplayColor == defaultColor, "slot icons never mutate shared prefab tint");
        Check(template.GetInventoryTint(red) == Color.white && template.InventoryTint == Color.white, "UI avoids double tinting the metal");
        Check(template.GetInventoryIcon(new PersistentInventory.Slot()) == template.InventoryIcon && template.GetInventoryIcon(null) == template.InventoryIcon, "legacy slots without saved colour use authored default");
        Check(ReadPixels(redIcon).SequenceEqual(redPixels), "world and inventory colours agree");
        Color32[] bluePixels = ReadPixels(blueIcon);
        int changed = 0, opaque = 0;
        for (int row = 0; row < rows.Length; row++)
        for (int x = 0; x < rows[0].Length; x++)
        {
            int i = (rows.Length - 1 - row) * 24 + x;
            char pixel = rows[row][x];
            if (pixel != '.') opaque++;
            if (redPixels[i].Equals(bluePixels[i])) continue;
            changed++;
            Check(pixel == 'C' || pixel == 'H' || pixel == 'S', "only enamel pixels recolour " + x + "/" + row);
        }
        Check(changed > 20 && changed < opaque / 2, "accent is clear but less than half the key");
        for (int i = 0; i < 100; i++)
            Check(template.GetInventoryIcon(inv.GetSlot(0)) == redIcon && template.GetInventoryIcon(inv.GetSlot(1)) == blueIcon, "cached icons stable " + i);

        var window = ItemDescriptionWindow.GetOrCreate();
        yield return null;
        Check(window.OpenForSlot(inv.GetSlot(1)), "description opens coloured key");
        var iconImage = (Image)typeof(ItemDescriptionWindow).GetField("iconImage", F).GetValue(window);
        Check(iconImage.sprite == blueIcon && iconImage.color == Color.white, "description uses per-slot embedded palette"); CloseDescription();
        Preview(template, new Color[] { defaultColor, red, blue, new Color32(59, 149, 99, 255), new Color32(144, 90, 173, 255), new Color32(80, 85, 92, 255) });
        string json = JsonUtility.ToJson(inv.CaptureRuntimeState());
        inv.ClearAll(); inv.ApplyRuntimeState(JsonUtility.FromJson<PersistentInventory.RuntimeState>(json));
        Check(inv.GetSlot(0).VisualColor == red && inv.ContainsItemInstance(identity) && inv.GetSlot(1).VisualColor == blue, "existing inventory JSON round trip");
        inv.PreserveForNextSceneLoad();
        Call(inv, "OnSceneLoaded", SceneManager.GetActiveScene(), LoadSceneMode.Single);
        Check(inv.ContainsItemInstance(identity) && template.GetInventoryIcon(inv.GetSlot(0)) == redIcon, "scene travel preserves per-key colour");

        inv.SelectSlot(0);
        Check(inv.TryDropSelectedItem(), "drop reconstructed key");
        var dropped = Object.FindObjectsOfType<KeyPickupItem>().Single();
        var droppedVisual = dropped.GetComponent<KeyPickupItemVisual>();
        Check(dropped.UniqueInstanceId == identity && droppedVisual.DisplayColor == red &&
            ReadPixels(droppedVisual.InventoryIcon).SequenceEqual(redPixels), "drop restores only enamel tint and retains matching-door ID");
        dropped.gameObject.SetActive(false); dropped.gameObject.SetActive(true);
        Check(dropped.GetComponent<SpriteRenderer>().color == Color.white && droppedVisual.DisplayColor == red, "stream unload/reload retains partial colour");

        var manager = SceneTravelStateManager.GetOrCreate();
        manager.CaptureDiskWorld();
        var states = (IDictionary)typeof(SceneTravelStateManager).GetField("sceneStates", F).GetValue(manager);
        object snapshot = states[SceneManager.GetActiveScene().name];
        droppedVisual.SetDisplayColor(blue);
        Check((bool)Call(manager, "RestoreSceneSnapshot", SceneManager.GetActiveScene(), snapshot, false), "scene snapshot restored");
        Check(droppedVisual.DisplayColor == red && ReadPixels(droppedVisual.InventoryIcon).SequenceEqual(redPixels), "saved world key restores its original enamel");
        Check(dropped.TryPickUp() && inv.ContainsItemInstance(identity), "re-pickup keeps identity"); CloseDescription();
        var door = new GameObject("Matching key door").AddComponent<DoorHingeInteraction>(); door.enabled = false;
        Set(door, "requiredUnlockItemInstanceId", identity);
        Check((bool)Call(door, "HasRequiredUnlockItem"), "bound door still recognizes key after recolour/save/drop");
        Set(door, "requiredUnlockItemInstanceId", "another-door-key");
        Check(!(bool)Call(door, "HasRequiredUnlockItem"), "other door does not accept wrong key");

        var transient = Object.Instantiate(prefab).GetComponent<KeyPickupItemVisual>();
        Sprite ownedSprite = transient.InventoryIcon; Texture ownedTexture = ownedSprite.texture;
        Object.Destroy(transient.gameObject); yield return null; yield return null;
        Check(ownedSprite == null && ownedTexture == null, "instance sprite and texture released on destruction");
        Check(template.GetInventoryIcon(inv.GetSlot(0)) != null, "destroying world keys leaves inventory icons intact");
    }
    static void CloseDescription() { ItemDescriptionWindow.GetOrCreate().CloseWindow(); Time.timeScale = 1; }
    static Color32[] ReadPixels(Sprite sprite)
    {
        var texture = sprite.texture;
        var rt = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
        var previous = RenderTexture.active;
        Graphics.Blit(texture, rt); RenderTexture.active = rt;
        var image = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
        image.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); image.Apply();
        Color32[] pixels = image.GetPixels32(); Object.Destroy(image);
        RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt); return pixels;
    }
    static void Preview(KeyPickupItemVisual template, Color[] colors)
    {
        const int scale = 10, w = 312, h = 200;
        var image = new Texture2D(w * 3, h * 2, TextureFormat.RGBA32, false);
        var pixels = Enumerable.Repeat(new Color32(24, 31, 43, 255), image.width * image.height).ToArray();
        Color saved = template.DisplayColor;
        for (int n = 0; n < colors.Length; n++)
        {
            var slot = new PersistentInventory.Slot();
            Call(slot, "Set", "key", 1, "preview", "", "", true, colors[n], false, 0f, "");
            Color32[] sprite = ReadPixels(template.GetInventoryIcon(slot));
            for (int y = 0; y < 14; y++) for (int x = 0; x < 24; x++)
            {
                Color32 c = sprite[y * 24 + x]; if (c.a == 0) continue;
                for (int dy = 0; dy < scale; dy++) for (int dx = 0; dx < scale; dx++)
                    pixels[(h * (1 - n / 3) + 30 + y * scale + dy) * image.width + w * (n % 3) + 36 + x * scale + dx] = c;
            }
        }
        image.SetPixels32(pixels); image.Apply();
        File.WriteAllBytes(Path.Combine(Application.dataPath, "../Key-Enamel-preview.png"), image.EncodeToPNG());
        Object.Destroy(image);
        Check(template.DisplayColor == saved, "preview variants also leave prefab tint intact");
    }
}
