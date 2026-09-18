using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Declares that several Unity scenes belong to one gameplay level.
/// Attach this component to a dedicated object in the level's entry scene or
/// bootstrap scene. The runtime registry keeps an immutable copy after that
/// scene unloads, so future save data can use stable level and scene keys.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Scene Management/Level Scene Group")]
public sealed class LevelSceneGroup : MonoBehaviour
{
    [Serializable]
    public sealed class SceneEntry
    {
#if UNITY_EDITOR
        [SerializeField, Tooltip("Scene asset belonging to this level. Assigning it automatically fills the runtime name and path.")]
        private SceneAsset sceneAsset;
#endif
        [SerializeField, Tooltip("Runtime scene name. This may also be entered manually when no Scene Asset is assigned.")]
        private string sceneName;
        [SerializeField, Tooltip("Stable per-scene identifier used inside save data. Leave blank once to initialize it from the scene name, then avoid changing it after saves are released.")]
        private string sceneSaveId;
        [SerializeField, HideInInspector]
        private string scenePath;

        public string SceneName => NormalizeSceneName(sceneName);
        public string ScenePath => scenePath == null
            ? string.Empty
            : scenePath.Trim();
        public string SceneSaveId => string.IsNullOrWhiteSpace(sceneSaveId)
            ? SceneName
            : sceneSaveId.Trim();

#if UNITY_EDITOR
        internal void SynchronizeEditorReference()
        {
            if (sceneAsset == null)
            {
                sceneName = NormalizeSceneName(sceneName);
                if (string.IsNullOrWhiteSpace(sceneSaveId))
                {
                    sceneSaveId = sceneName;
                }
                return;
            }

            scenePath = AssetDatabase.GetAssetPath(sceneAsset);
            sceneName = NormalizeSceneName(scenePath);
            if (string.IsNullOrWhiteSpace(sceneSaveId))
            {
                sceneSaveId = sceneName;
            }
        }
#endif
    }

    /// <summary>
    /// Immutable runtime definition suitable for use by a save-game index.
    /// It intentionally contains no scene object references.
    /// </summary>
    public sealed class Definition
    {
        private readonly string[] sceneNames;
        private readonly string[] scenePaths;
        private readonly string[] sceneSaveIds;

        internal Definition(
            string configuredLevelId,
            string configuredDisplayName,
            int configuredSaveSchemaVersion,
            string[] configuredSceneNames,
            string[] configuredScenePaths,
            string[] configuredSceneSaveIds,
            int configuredEntrySceneIndex)
        {
            LevelId = configuredLevelId;
            DisplayName = configuredDisplayName;
            SaveSchemaVersion = configuredSaveSchemaVersion;
            sceneNames = configuredSceneNames;
            scenePaths = configuredScenePaths;
            sceneSaveIds = configuredSceneSaveIds;
            EntrySceneIndex = Mathf.Clamp(
                configuredEntrySceneIndex,
                0,
                Mathf.Max(0, sceneNames.Length - 1));
        }

        public string LevelId { get; }
        public string DisplayName { get; }
        public int SaveSchemaVersion { get; }
        public int EntrySceneIndex { get; }
        public string LevelSaveKey => "levels/" + LevelId;
        public IReadOnlyList<string> SceneNames => sceneNames;
        public IReadOnlyList<string> ScenePaths => scenePaths;
        public IReadOnlyList<string> SceneSaveIds => sceneSaveIds;
        public string EntrySceneName => sceneNames.Length > 0
            ? sceneNames[EntrySceneIndex]
            : string.Empty;

        public bool ContainsScene(string sceneIdentifier)
        {
            return TryGetSceneIndex(sceneIdentifier, out _);
        }

        public bool TryGetSceneIndex(
            string sceneIdentifier,
            out int sceneIndex)
        {
            string normalized = NormalizeSceneName(sceneIdentifier);
            for (int index = 0; index < sceneNames.Length; index++)
            {
                if (string.Equals(
                        sceneNames[index],
                        normalized,
                        StringComparison.OrdinalIgnoreCase))
                {
                    sceneIndex = index;
                    return true;
                }
            }

            sceneIndex = -1;
            return false;
        }

        public string GetSceneSaveKey(string sceneIdentifier)
        {
            return TryGetSceneIndex(sceneIdentifier, out int sceneIndex)
                ? LevelSaveKey + "/scenes/" + sceneSaveIds[sceneIndex]
                : string.Empty;
        }
    }

    [Header("Stable Save Identity")]
    [SerializeField, Tooltip("Stable identifier written into save data. Do not change it after saves have been released.")]
    private string levelId = "level.new";
    [SerializeField, Tooltip("Player-facing level name. This may change without invalidating saves.")]
    private string displayName = "新关卡";
    [SerializeField, Min(1), Tooltip("Increase this only when a future save migration changes this level's stored data format.")]
    private int saveSchemaVersion = 1;

    [Header("Scenes In This Level")]
    [SerializeField, Tooltip("All scenes belonging to the level. The list size and order can be changed in the Inspector.")]
    private List<SceneEntry> scenes = new List<SceneEntry>
    {
        new SceneEntry()
    };
    [SerializeField, Min(0), Tooltip("Index of the scene used when starting this level from a menu or a new save.")]
    private int entrySceneIndex;

    private static readonly Dictionary<string, Definition> DefinitionsById =
        new Dictionary<string, Definition>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Definition> DefinitionsByScene =
        new Dictionary<string, Definition>(StringComparer.OrdinalIgnoreCase);

    public static event Action RegistryChanged;

    public string LevelId => NormalizeLevelId(levelId);
    public string DisplayName => string.IsNullOrWhiteSpace(displayName)
        ? LevelId
        : displayName.Trim();
    public int SaveSchemaVersion => Mathf.Max(1, saveSchemaVersion);
    public int EntrySceneIndex => scenes == null || scenes.Count == 0
        ? 0
        : Mathf.Clamp(entrySceneIndex, 0, scenes.Count - 1);
    public IReadOnlyList<SceneEntry> Scenes => scenes;

    private void OnEnable()
    {
        Register();
    }

    public void Register()
    {
        Definition definition = CreateDefinition();
        if (definition == null)
        {
            return;
        }

        if (DefinitionsById.TryGetValue(
                definition.LevelId,
                out Definition previousDefinition))
        {
            RemoveSceneMappings(previousDefinition);
        }

        DefinitionsById[definition.LevelId] = definition;
        for (int index = 0;
             index < definition.SceneNames.Count;
             index++)
        {
            string sceneName = definition.SceneNames[index];
            if (DefinitionsByScene.TryGetValue(
                    sceneName,
                    out Definition otherDefinition) &&
                !string.Equals(
                    otherDefinition.LevelId,
                    definition.LevelId,
                    StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning(
                    "Scene '" + sceneName + "' is assigned to both level '" +
                    otherDefinition.LevelId + "' and '" + definition.LevelId +
                    "'. The latest LevelSceneGroup registration is used.",
                    this);
            }
            DefinitionsByScene[sceneName] = definition;
        }

        RegistryChanged?.Invoke();
    }

    public Definition CreateDefinition()
    {
        string stableLevelId = LevelId;
        if (string.IsNullOrWhiteSpace(stableLevelId))
        {
            Debug.LogWarning(
                "LevelSceneGroup requires a non-empty stable Level Id.",
                this);
            return null;
        }

        List<string> names = new List<string>();
        List<string> paths = new List<string>();
        List<string> saveIds = new List<string>();
        HashSet<string> uniqueNames =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (scenes != null)
        {
            for (int index = 0; index < scenes.Count; index++)
            {
                SceneEntry entry = scenes[index];
                if (entry == null || string.IsNullOrWhiteSpace(entry.SceneName) ||
                    !uniqueNames.Add(entry.SceneName))
                {
                    continue;
                }

                names.Add(entry.SceneName);
                paths.Add(entry.ScenePath);
                saveIds.Add(entry.SceneSaveId);
            }
        }

        if (names.Count == 0 && gameObject.scene.IsValid())
        {
            names.Add(gameObject.scene.name);
            paths.Add(gameObject.scene.path);
            saveIds.Add(gameObject.scene.name);
        }
        if (names.Count == 0)
        {
            Debug.LogWarning(
                "LevelSceneGroup requires at least one valid scene.",
                this);
            return null;
        }

        int configuredEntryIndex = scenes == null || scenes.Count == 0
            ? 0
            : Mathf.Clamp(entrySceneIndex, 0, scenes.Count - 1);
        string configuredEntryScene = scenes != null && scenes.Count > 0 &&
            scenes[configuredEntryIndex] != null
                ? scenes[configuredEntryIndex].SceneName
                : string.Empty;
        int resolvedEntryIndex = names.FindIndex(sceneName =>
            string.Equals(
                sceneName,
                configuredEntryScene,
                StringComparison.OrdinalIgnoreCase));
        if (resolvedEntryIndex < 0)
        {
            resolvedEntryIndex = 0;
        }
        return new Definition(
            stableLevelId,
            DisplayName,
            SaveSchemaVersion,
            names.ToArray(),
            paths.ToArray(),
            saveIds.ToArray(),
            resolvedEntryIndex);
    }

    public static bool TryGetLevel(
        string configuredLevelId,
        out Definition definition)
    {
        return DefinitionsById.TryGetValue(
            NormalizeLevelId(configuredLevelId),
            out definition);
    }

    public static bool TryGetLevelForScene(
        string sceneIdentifier,
        out Definition definition)
    {
        return DefinitionsByScene.TryGetValue(
            NormalizeSceneName(sceneIdentifier),
            out definition);
    }

    public static bool TryGetActiveLevel(out Definition definition)
    {
        return TryGetLevelForScene(
            SceneManager.GetActiveScene().name,
            out definition);
    }

    public static bool AreScenesInSameLevel(
        string firstScene,
        string secondScene)
    {
        return TryGetLevelForScene(firstScene, out Definition firstLevel) &&
            TryGetLevelForScene(secondScene, out Definition secondLevel) &&
            string.Equals(
                firstLevel.LevelId,
                secondLevel.LevelId,
                StringComparison.OrdinalIgnoreCase);
    }

    private static void RemoveSceneMappings(Definition definition)
    {
        for (int index = 0; index < definition.SceneNames.Count; index++)
        {
            string sceneName = definition.SceneNames[index];
            if (DefinitionsByScene.TryGetValue(
                    sceneName,
                    out Definition mappedDefinition) &&
                ReferenceEquals(mappedDefinition, definition))
            {
                DefinitionsByScene.Remove(sceneName);
            }
        }
    }

    private static string NormalizeLevelId(string configuredLevelId)
    {
        return configuredLevelId == null
            ? string.Empty
            : configuredLevelId.Trim();
    }

    private static string NormalizeSceneName(string sceneIdentifier)
    {
        if (string.IsNullOrWhiteSpace(sceneIdentifier))
        {
            return string.Empty;
        }

        string normalized = sceneIdentifier.Trim().Replace('\\', '/');
        int finalSlash = normalized.LastIndexOf('/');
        if (finalSlash >= 0 && finalSlash + 1 < normalized.Length)
        {
            normalized = normalized.Substring(finalSlash + 1);
        }
        if (normalized.EndsWith(
                ".unity",
                StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized.Substring(
                0,
                normalized.Length - ".unity".Length);
        }
        return normalized;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        DefinitionsById.Clear();
        DefinitionsByScene.Clear();
        RegistryChanged = null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        levelId = NormalizeLevelId(levelId);
        displayName = displayName == null ? string.Empty : displayName.Trim();
        saveSchemaVersion = Mathf.Max(1, saveSchemaVersion);
        if (scenes == null)
        {
            scenes = new List<SceneEntry>();
        }
        for (int index = 0; index < scenes.Count; index++)
        {
            scenes[index]?.SynchronizeEditorReference();
        }
        entrySceneIndex = scenes.Count == 0
            ? 0
            : Mathf.Clamp(entrySceneIndex, 0, scenes.Count - 1);

        if (Application.isPlaying)
        {
            Register();
        }
    }
#endif
}
