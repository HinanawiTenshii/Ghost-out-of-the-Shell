#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static partial class VisualStyleLabSmoke
{
    static void VerifyBrightPalette(Scene scene)
    {
        var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Component>(true)).ToArray();
        var shop = scene.GetRootGameObjects().Single(r => r.name.StartsWith("03_Shop"));
        var tops = shop.GetComponentsInChildren<SpriteRenderer>(true).Where(s => s.name == "Table-TopPanel").ToArray();
        Check(tops.Length > 0 && tops.All(s => s.color.g > .46f && s.color.b > .49f), "Furniture midtones are brighter cyan, not just brighter outlines");
        var tree = all.OfType<GeometricTreeDecoration>().Single();
        var leaf = new SerializedObject(tree).FindProperty("crownColor").colorValue;
        Check(leaf.g > .56f && leaf.g > leaf.b * 1.1f, "Tree palette is brighter and retains green-leaf motion classification");
        var engineer = all.OfType<EngineerZeldaCharacterData>().Single();
        var shirt = new SerializedObject(engineer).FindProperty("shirtColor").colorValue;
        Check(shirt.g > .49f && shirt.b > .54f, "Character clothing is lifted while keeping the cool palette");
        var cauldron = all.OfType<GeometricAlchemyDecoration>().Single();
        var vessel = new SerializedObject(cauldron);
        Check(vessel.FindProperty("stone").colorValue.g > .64f, "Cauldron stone housing is lifted");
        var liquid = vessel.FindProperty("accent").colorValue;
        Check(liquid == AlchemyRecipeCatalog.UnbrewedColor, "Cauldron liquid retains its unbrewed gameplay color");
    }
}
#endif
