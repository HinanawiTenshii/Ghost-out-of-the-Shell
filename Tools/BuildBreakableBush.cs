using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Run in an isolated editor project; copy the resulting native assets to the project.
// Flat-colour polygon sprites match the existing geometric plants, without a runtime generator.
public static class BuildBreakableBush
{
    const string Folder = "Assets/Prefabs/Decorations/Plants/";
    public static void Rebuild()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/")) throw new Exception("isolated only");
        AssetDatabase.DeleteAsset(Folder + "BreakableBush.prefab");
        AssetDatabase.DeleteAsset(Folder + "BreakableBush-Foliage.asset");
        Run();
    }
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"))
            throw new InvalidOperationException("Asset generation must run in the isolated test project.");
        string spritePath = Folder + "BreakableBush-Foliage.asset";
        if (AssetDatabase.LoadMainAssetAtPath(spritePath) != null)
            throw new InvalidOperationException("Do not overwrite existing foliage assets.");
        EditorSettings.serializationMode = SerializationMode.ForceText;
        var texture = new Texture2D(96, 64, TextureFormat.RGBA32, false);
        texture.name = "Bush White Polygon Texture";
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        var outline = new[] {
            new Vector2(-.82f, -.8f), new Vector2(.78f, -.8f),
            new Vector2(1.2f, -.4f), new Vector2(1.14f, .37f),
            new Vector2(.66f, .8f), new Vector2(-.57f, .8f),
            new Vector2(-1.12f, .43f), new Vector2(-1.2f, -.32f)
        };
        var pixels = new Color[96*64];
        for (int y = 0; y < 64; y++) for (int x = 0; x < 96; x++)
        {
            var point = new Vector2((x+.5f)/40-1.2f, (y+.5f)/40-.8f);
            bool inside = true;
            for (int i = 0; i < outline.Length; i++)
            {
                var a = outline[i]; var b = outline[(i+1)%outline.Length];
                if ((b.x-a.x)*(point.y-a.y)-(b.y-a.y)*(point.x-a.x) < 0) { inside = false; break; }
            }
            pixels[y*96+x] = inside ? Color.white : Color.clear;
        }
        texture.SetPixels(pixels); texture.Apply();
        // Bake the polygon mask: Sprite.OverrideGeometry is runtime-only and
        // Unity discards it when native sprite assets are reimported.
        var sprite = Sprite.Create(texture, new Rect(0,0,96,64), Vector2.one*.5f, 40, 0, SpriteMeshType.FullRect);
        sprite.name = "Bush Bevelled Foliage";
        AssetDatabase.CreateAsset(sprite, spritePath);
        AssetDatabase.AddObjectToAsset(texture, sprite);
        EditorUtility.SetDirty(sprite);
        AssetDatabase.SaveAssets();

        var root = new GameObject("BreakableBush");
        var renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = new Color(.08f, .43f, .32f, 1);
        renderer.sortingOrder = 5;
        var collider = root.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(2.15f, 1.25f);
        collider.offset = new Vector2(0, -.025f);
        collider.isTrigger = false;
        var leaves = new[] {
            Leaf(root, sprite, "West Foliage", new Vector2(-.63f,.14f), new Vector2(.57f,.7f), -12, new Color(.12f,.7f,.4f,1), 6),
            Leaf(root, sprite, "East Foliage", new Vector2(.62f,.13f), new Vector2(.56f,.69f), 10, new Color(.11f,.62f,.37f,1), 6),
            Leaf(root, sprite, "Upper Foliage", new Vector2(-.03f,.43f), new Vector2(.58f,.64f), 0, new Color(.22f,.82f,.48f,1), 7),
            Leaf(root, sprite, "Lower West Foliage", new Vector2(-.42f,-.33f), new Vector2(.46f,.38f), 8, new Color(.10f,.56f,.34f,1), 7),
            Leaf(root, sprite, "Lower East Foliage", new Vector2(.46f,-.30f), new Vector2(.43f,.4f), -8, new Color(.12f,.7f,.4f,1), 7),
            Leaf(root, sprite, "Central Foliage", new Vector2(.02f,.01f), new Vector2(.45f,.47f), -5, new Color(.16f,.77f,.45f,1), 8)
        };
        var door = root.AddComponent<DoorData>();
        var settings = new SerializedObject(door);
        settings.FindProperty("durability").intValue = 3;
        settings.FindProperty("fragmentCount").intValue = 22;
        settings.FindProperty("fragmentLifetime").floatValue = .75f;
        settings.FindProperty("fragmentSpeed").floatValue = 2.4f;
        settings.FindProperty("fragmentScale").floatValue = .12f;
        settings.FindProperty("shakeDuration").floatValue = .22f;
        settings.FindProperty("shakeAmount").floatValue = .05f;
        settings.FindProperty("breakSoundVolume").floatValue = .48f;
        var attached = settings.FindProperty("attachedVisuals");
        attached.arraySize = leaves.Length;
        for (int i = 0; i < leaves.Length; i++) attached.GetArrayElementAtIndex(i).objectReferenceValue = leaves[i];
        settings.ApplyModifiedPropertiesWithoutUndo();
        var effects = root.AddComponent<BushDestructionEffects>();
        var effectSettings = new SerializedObject(effects);
        effectSettings.FindProperty("breakSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/Audio/BushBreak.wav");
        effectSettings.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root, Folder + "BreakableBush.prefab");
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        Debug.Log("BREAKABLE_BUSH_GENERATED");
        EditorApplication.Exit(0);
    }

    static Transform Leaf(GameObject root, Sprite sprite, string name, Vector2 position, Vector2 scale, float angle, Color color, int order)
    {
        var leaf = new GameObject(name);
        leaf.transform.SetParent(root.transform, false);
        leaf.transform.localPosition = position;
        leaf.transform.localScale = new Vector3(scale.x, scale.y, 1);
        leaf.transform.localRotation = Quaternion.Euler(0, 0, angle);
        var renderer = leaf.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite; renderer.color = color; renderer.sortingOrder = order;
        return leaf.transform;
    }
}
