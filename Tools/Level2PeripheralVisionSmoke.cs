using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Run in the isolated Unity smoke project with the current Level2NPCS prefabs.
[InitializeOnLoad]
public static class Level2PeripheralVisionSmoke
{
    static Level2PeripheralVisionSmoke() { EditorApplication.update += Tick; }
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(nameof(Level2PeripheralVisionSmoke), true);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || !SessionState.GetBool(nameof(Level2PeripheralVisionSmoke), false)) return;
        SessionState.SetBool(nameof(Level2PeripheralVisionSmoke), false);
        try
        {
            int checkedCount = 0, skipped = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Level2NPCS" }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                var ai = prefab.GetComponent<ZeldaCharacterAiBase>();
                if (ai == null) { skipped++; continue; }
                var before = new SerializedObject(ai);
                if (before.FindProperty("peripheralVisionRadius").floatValue != 1.7f)
                    throw new Exception(prefab.name + " serialized radius");
                var instance = Object.Instantiate(prefab, new Vector3(1000, 1000), Quaternion.identity);
                var live = new SerializedObject(instance.GetComponent<ZeldaCharacterAiBase>());
                if (live.FindProperty("peripheralVisionRadius").floatValue != 1.7f)
                    throw new Exception(prefab.name + " radius overwritten during Awake");
                foreach (string field in new[] { "visionRadius", "visionAngle", "peripheralVisionAngle", "immediateResponseDistance" })
                    if (live.FindProperty(field).floatValue != before.FindProperty(field).floatValue)
                        throw new Exception(prefab.name + " changed " + field);
                Object.DestroyImmediate(instance);
                checkedCount++;
            }
            if (checkedCount != 35 || skipped != 1) throw new Exception("Unexpected prefab coverage: " + checkedCount + "/" + skipped);
            Debug.Log("LEVEL2_PERIPHERAL_VISION_PASS prefabs=" + checkedCount + " skippedSandworm=" + skipped);
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
