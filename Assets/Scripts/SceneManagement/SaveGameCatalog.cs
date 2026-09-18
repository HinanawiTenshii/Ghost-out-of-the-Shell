using System;
using System.Collections.Generic;
using UnityEngine;

// Referenced assets remain available in standalone builds, without AssetDatabase.
public sealed class SaveGameCatalog : ScriptableObject
{
    private static PickupItemBase[] pickupTemplates;
    public static PickupItemBase[] LoadPickupTemplates()
    {
        if (pickupTemplates != null) return pickupTemplates;
        var result = new List<PickupItemBase>();
        foreach (var prefab in Resources.LoadAll<GameObject>("PickupItems"))
        {
            var pickup = prefab.GetComponent<PickupItemBase>();
            if (pickup != null) result.Add(pickup);
        }
        return pickupTemplates = result.ToArray();
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache() { pickupTemplates = null; }
    [Serializable] public sealed class SceneTemplate
    {
        public string name, path, authoredState;
        // Scene YAML contains PPtr-like inline mappings. Keep it out of the
        // catalog asset's YAML parser/reference scanner by storing safe text.
        public string authoredStateBase64;

        public static SceneTemplate FromSceneText(string sceneName, string scenePath, string text)
        {
            return new SceneTemplate {
                name = sceneName, path = scenePath,
                authoredStateBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(text))
            };
        }

        public SceneTemplate CreateSaveSnapshot()
        {
            // Preserve the original save-file schema and support old raw-text catalogs.
            return new SceneTemplate {
                name = name, path = path,
                authoredState = string.IsNullOrEmpty(authoredStateBase64) ? authoredState :
                    System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(authoredStateBase64))
            };
        }
    }
    public GameObject[] characterPrefabs;
    public List<SceneTemplate> scenes = new List<SceneTemplate>();

    public GameObject FindCharacter(string type, string objectName)
    {
        GameObject fallback = null;
        foreach (var prefab in characterPrefabs ?? new GameObject[0])
        {
            if (prefab == null) continue;
            var data = prefab.GetComponent<ZeldaCharacterData>();
            if (data == null || data.GetType().AssemblyQualifiedName != type) continue;
            if (fallback == null) fallback = prefab;
            if (objectName.Replace("(Clone)", "").Trim() == prefab.name) return prefab;
        }
        return fallback;
    }
}
