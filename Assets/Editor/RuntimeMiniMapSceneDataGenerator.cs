#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class RuntimeMiniMapSceneDataGenerator
{
    private const string OutputFolder = "Assets/Resources/MiniMaps";

    static RuntimeMiniMapSceneDataGenerator()
    {
        EditorApplication.delayCall += GenerateStaleSnapshots;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.delayCall += GenerateStaleSnapshots;
        }
    }

    [MenuItem("Tools/Cogitans & Extensa/Rebuild Mini Map Snapshots")]
    public static void RebuildAllSnapshots()
    {
        GenerateSnapshots(true);
    }

    private static void GenerateStaleSnapshots()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        GenerateSnapshots(false);
    }

    private static void GenerateSnapshots(bool force)
    {
        EnsureOutputFolder();
        EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;
        bool changed = false;
        for (int index = 0; index < buildScenes.Length; index++)
        {
            EditorBuildSettingsScene buildScene = buildScenes[index];
            if (!buildScene.enabled || string.IsNullOrEmpty(buildScene.path))
            {
                continue;
            }

            string sceneName = Path.GetFileNameWithoutExtension(buildScene.path);
            if (sceneName == "TitleScreen")
            {
                continue;
            }

            string assetPath = OutputFolder + "/" + sceneName + ".asset";
            RuntimeMiniMapSceneData existingData =
                AssetDatabase.LoadAssetAtPath<RuntimeMiniMapSceneData>(assetPath);
            if (!force &&
                !IsSnapshotStale(buildScene.path, assetPath) &&
                existingData != null &&
                existingData.HasExplicitDisplayName &&
                existingData.UsesCurrentSchema)
            {
                continue;
            }

            GenerateSceneSnapshot(buildScene.path, assetPath, sceneName);
            changed = true;
        }

        if (changed)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }

    private static void GenerateSceneSnapshot(
        string scenePath,
        string assetPath,
        string sceneName)
    {
        Scene scene = SceneManager.GetSceneByPath(scenePath);
        bool openedForCapture = !scene.IsValid() || !scene.isLoaded;
        if (openedForCapture)
        {
            scene = EditorSceneManager.OpenScene(
                scenePath,
                OpenSceneMode.Additive);
        }

        GameObject captureObject = new GameObject(
            "Mini Map Snapshot Capture",
            typeof(RectTransform),
            typeof(CanvasRenderer));
        captureObject.hideFlags = HideFlags.HideAndDontSave;
        RuntimeMiniMapGraphic capture =
            captureObject.AddComponent<RuntimeMiniMapGraphic>();
        capture.RebuildFromScene(scene);

        RuntimeMiniMapSceneData data =
            AssetDatabase.LoadAssetAtPath<RuntimeMiniMapSceneData>(assetPath);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<RuntimeMiniMapSceneData>();
            AssetDatabase.CreateAsset(data, assetPath);
        }

        string displayName = sceneName;
        TabJournalMenuController[] menus =
            Object.FindObjectsOfType<TabJournalMenuController>(true);
        for (int index = 0; index < menus.Length; index++)
        {
            TabJournalMenuController menu = menus[index];
            if (menu != null && menu.gameObject.scene == scene)
            {
                displayName = menu.AreaDisplayName;
                break;
            }
        }

        capture.CopyCurrentGeometryTo(data, sceneName, displayName);
        EditorUtility.SetDirty(data);
        Object.DestroyImmediate(captureObject);

        if (openedForCapture)
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static bool IsSnapshotStale(string scenePath, string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string absoluteScenePath = Path.Combine(projectRoot, scenePath);
        string absoluteAssetPath = Path.Combine(projectRoot, assetPath);
        return !File.Exists(absoluteAssetPath) ||
               File.GetLastWriteTimeUtc(absoluteScenePath) >
               File.GetLastWriteTimeUtc(absoluteAssetPath);
    }

    private static void EnsureOutputFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }
        if (!AssetDatabase.IsValidFolder(OutputFolder))
        {
            AssetDatabase.CreateFolder("Assets/Resources", "MiniMaps");
        }
    }
}
#endif
