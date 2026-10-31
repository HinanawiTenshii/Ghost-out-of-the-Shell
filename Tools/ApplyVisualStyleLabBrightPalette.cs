#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// One-time, scene-local palette bake. No new runtime work, light, or global material changes.
public static class ApplyVisualStyleLabBrightPalette
{
    const string ScenePath = "Assets/Scenes/Experiments/VisualStyleLab.unity";
    const string Baseline = "BB3568154F56BAB130A9A9443758D3D17B845F11CE838A57473D0053C682D3F7";
    static int fields, sprites, meshes;

    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"))
            throw new Exception("Isolated project only; never overwrite an open work scene.");
        using (var sha = SHA256.Create())
            if (BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(ScenePath))).Replace("-", "") != Baseline)
                throw new Exception("Scene differs from reviewed baseline; refusing to reapply or overwrite edits.");
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var components = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Component>(true))
            .Where(c => c != null && (c.hideFlags & HideFlags.DontSave) == 0).ToArray();
        var snapshot = typeof(ApplyVisualStyleLabActorsProps).GetMethod("GameplayFields", BindingFlags.NonPublic | BindingFlags.Static);
        Func<Component, string> data = c => (string)snapshot.Invoke(null, new object[] { c });
        var unchanged = components.Where(c => !(c is Renderer) && !(c is MeshFilter)).ToDictionary(c => c, data);

        foreach (var c in components)
        {
            if (c is EngineerZeldaCharacterData)
                Fields(c, .45f, .07f, "shirtColor apronColor capColor brassColor lensColor toolColor hammerColor scarfColor");
            else if (c is ArabSoldierZeldaCharacterData)
                Fields(c, .45f, .07f, "armorColor armorShadow armorHighlight headclothColor headclothHighlight headclothShadow scarfColor scarfShadow");
            else if (c is CivilianZeldaCharacterData)
            {
                Fields(c, .45f, .07f, "clothingColor");
                if (c is DesertMerchantZeldaCharacterData)
                    Fields(c, .45f, .07f, "headclothColor trimColor leatherColor");
            }
            if (c is CivilianZeldaCharacterData || c is EngineerZeldaCharacterData)
                Fields(c, .32f, 0, "skinColor");
            if (c is GeometricCurvedTable)
                Fields(c, .62f, .10f, "woodColor edgeColor surfaceColor insetEdgeColor");
            if (c is GeometricStorageFurniture)
                Fields(c, .62f, .10f, "woodColor edgeColor lightWoodColor metalColor");
            if (c is GeometricSignpostDecoration)
                Fields(c, .62f, 0, "poleColor boardColor");
            if (c is GeometricTreeDecoration)
                Fields(c, .62f, 0, "trunkColor crownColor upperCrownColor leafAccentColor");
            if (c is GeometricAlchemyDecoration)
                Fields(c, .62f, .10f, "stone wood"); // Liquid/accent is gameplay state, not scenery.
            if (c is KeroseneStreetLamp)
                Fields(c, .62f, .10f, "metalColor");
            if (c is LeverData)
                Fields(c, .62f, .10f, "baseColor leverColor metalColor");
            if (c is DoubleSlidingDoor)
                Fields(c, .20f, .04f, "negativeColor positiveColor");
        }

        foreach (var sr in components.OfType<SpriteRenderer>())
        {
            string group = sr.transform.root.name;
            float strength, cool;
            if (group.StartsWith("01_")) { strength = .35f; cool = .05f; }
            else if (group.StartsWith("02_") && sr.name != "Shop Breakable Glass") { strength = .07f; cool = 0; }
            else if (group.StartsWith("03_") || group.StartsWith("04_")) { strength = .62f; cool = .10f; }
            else if (group.StartsWith("05_")) { strength = .40f; cool = .06f; }
            else continue;
            // White tint is the identity for generated sprites; don't colorize textures or state lights.
            if (sr.color == Color.white || sr.color.a < .75f || sr.name.Contains("Shadow")) continue;
            Color color = Lift(sr.color, strength, cool);
            if (color == sr.color) continue;
            sr.color = color; EditorUtility.SetDirty(sr); sprites++;
        }

        // Baked overlays cover their original sprites; lift the matching opaque vertex colors as well.
        foreach (var mf in components.OfType<MeshFilter>())
        {
            var mesh = mf.sharedMesh;
            if (!mesh) continue;
            string path = AssetDatabase.GetAssetPath(mesh);
            bool surface = path.StartsWith("Assets/Art/VisualStyleLab/SurfaceStudy/");
            bool rim = path.StartsWith("Assets/Art/VisualStyleLab/ActorPropStudy/") && path.EndsWith("Rim.asset");
            if (!surface && !rim) continue;
            bool wall = mesh.name.Contains("Wall");
            var before = mesh.colors;
            var after = before.Select(c => Lift(c, rim ? .62f : wall ? .07f : .35f, rim ? .10f : wall ? 0 : .05f)).ToArray();
            if (after.SequenceEqual(before)) continue;
            mesh.colors = after; EditorUtility.SetDirty(mesh); meshes++;
        }

        foreach (var p in unchanged)
            if (p.Key == null || p.Value != data(p.Key)) throw new Exception("Non-color scene data changed: " + p.Key);
        if (fields < 60 || sprites < 40 || meshes < 7) throw new Exception("Unexpected palette coverage");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("LAB_BRIGHT_PALETTE_PASS fields=" + fields + " sprites=" + sprites + " meshes=" + meshes +
                  " gameplay=unchanged crt=unchanged geometry=unchanged runtime-cost=none");
    }

    static void Fields(Component component, float strength, float cool, string names)
    {
        var so = new SerializedObject(component);
        foreach (string name in names.Split(' '))
        {
            var p = so.FindProperty(name);
            if (p == null || p.propertyType != SerializedPropertyType.Color)
                throw new Exception(component.GetType().Name + " lacks appearance field " + name);
            p.colorValue = Lift(p.colorValue, strength, cool); fields++;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static Color Lift(Color c, float strength, float cool)
    {
        float max = Mathf.Max(c.r, c.g, c.b), min = Mathf.Min(c.r, c.g, c.b);
        if (c.a < .75f || max < .12f || max >= .97f) return c;
        // Lift midtones, not blacks or white highlights: silhouettes and material separation survive.
        float gain = 1 + strength * (1 - max);
        var result = new Color(c.r * gain, c.g * gain, c.b * gain, c.a);
        bool neutralOrBlue = c.b > c.r * 1.08f || max - min < .035f;
        if (neutralOrBlue && cool > 0)
        {
            // Normalized tint keeps the brightness lift, rather than washing everything toward white.
            var cyan = new Color(.42f, .90f, 1, c.a) * (max * gain);
            result = Color.Lerp(result, cyan, cool);
        }
        result.a = c.a;
        return result;
    }
}
#endif
