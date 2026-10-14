#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
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

    public static void GenerateSnapshots(bool force)
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
            // Never publish unsaved editing changes as the built scene's geometry.
            Scene loaded = SceneManager.GetSceneByPath(buildScene.path);
            if (loaded.IsValid() && loaded.isLoaded && loaded.isDirty) continue;
            RuntimeMiniMapSceneData existingData =
                AssetDatabase.LoadAssetAtPath<RuntimeMiniMapSceneData>(assetPath);
            if (!force &&
                !IsSnapshotStale(buildScene.path, assetPath) &&
                existingData != null &&
                existingData.SourceRevision == AssetDatabase.GetAssetDependencyHash(buildScene.path).ToString() &&
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
        data.SetSourceRevision(AssetDatabase.GetAssetDependencyHash(scenePath).ToString());
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

// Build snapshots from saved source, rather than relying on a previous editor session.
public sealed class MiniMapBuildPreparation : IPreprocessBuildWithReport
{
    public int callbackOrder => 100;
    public void OnPreprocessBuild(BuildReport report)
    {
        RuntimeMiniMapSceneDataGenerator.GenerateSnapshots(false);
        foreach (var scene in EditorBuildSettings.scenes)
        {
            if (!scene.enabled || string.IsNullOrEmpty(scene.path)) continue;
            string name = Path.GetFileNameWithoutExtension(scene.path);
            if (name == "TitleScreen") continue;
            var data = AssetDatabase.LoadAssetAtPath<RuntimeMiniMapSceneData>("Assets/Resources/MiniMaps/" + name + ".asset");
            if (data == null || !data.UsesCurrentSchema || data.SourceRevision != AssetDatabase.GetAssetDependencyHash(scene.path).ToString())
                throw new BuildFailedException("请先保存场景并重新生成地图快照：" + scene.path);
        }
    }
}
#endif
