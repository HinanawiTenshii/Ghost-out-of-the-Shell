using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Does not open scenes or execute scene components during a build.
public sealed class SaveGameCatalogBuilder : IPreprocessBuildWithReport
{
    public int callbackOrder => -100;
    public void OnPreprocessBuild(BuildReport report) { Rebuild(); }

    [InitializeOnLoadMethod]
    private static void Initialize()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.ExitingEditMode) Rebuild();
        };
    }

    [MenuItem("Tools/Save System/Rebuild Catalog")]
    public static void Rebuild()
    {
        const string path = "Assets/Resources/SaveGameCatalog.asset";
        var catalog = AssetDatabase.LoadAssetAtPath<SaveGameCatalog>(path);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<SaveGameCatalog>();
            AssetDatabase.CreateAsset(catalog, path);
        }
        var prefabs = new List<GameObject>();
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (prefab != null && prefab.GetComponent<ZeldaCharacterData>() != null) prefabs.Add(prefab);
        }
        catalog.characterPrefabs = prefabs.ToArray();
        catalog.scenes.Clear();
        foreach (var scene in EditorBuildSettings.scenes)
        {
            if (!scene.enabled || !File.Exists(scene.path)) continue;
            catalog.scenes.Add(SaveGameCatalog.SceneTemplate.FromSceneText(
                Path.GetFileNameWithoutExtension(scene.path), scene.path, File.ReadAllText(scene.path)));
        }
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssetIfDirty(catalog);
    }
}
