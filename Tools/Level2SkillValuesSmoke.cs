using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Run only in the isolated validation project. No main-project scene saves.
[InitializeOnLoad]
public static class Level2SkillValuesSmoke
{
    static Level2SkillValuesSmoke() { EditorApplication.update += Tick; }
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(nameof(Level2SkillValuesSmoke), true);
        EditorApplication.EnterPlaymode();
    }
    static int Expected(string name)
    {
        if (name.StartsWith("Engineer") || name.StartsWith("AdvancedEngineer")) return 6;
        if (name.StartsWith("Blacksmith")) return 5;
        if (name.StartsWith("ArabStrongman")) return 2;
        if (name.StartsWith("HeavyPaladin")) return 3;
        if (name.StartsWith("Strongman") || name.StartsWith("Bodyguard")) return 1;
        if (name.StartsWith("Civilian") || name.StartsWith("Merchant") || name.StartsWith("Chef") || name.StartsWith("Scribe") || name.StartsWith("Dancer")) return 2;
        if (name.Contains("ArabSoldier") || name.StartsWith("DesertScout")) return 3;
        if (name.Contains("Paladin") || name.StartsWith("Alchemist") || name.StartsWith("Wizard") || name.StartsWith("Priest") || name.StartsWith("EasternSwordsman")) return 4;
        if (name.StartsWith("Automaton")) return 1;
        if (name.StartsWith("Skeleton") || name.StartsWith("Sandworm")) return 0;
        throw new Exception("Unclassified character: " + name);
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || !SessionState.GetBool(nameof(Level2SkillValuesSmoke), false)) return;
        SessionState.SetBool(nameof(Level2SkillValuesSmoke), false);
        try
        {
            Time.timeScale = 0;
            int imported = 0, spawned = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Level2NPCS" }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                var template = prefab.GetComponent<ZeldaCharacterData>();
                int expected = Expected(prefab.name);
                if (template == null || template.SkillValue != expected) throw new Exception(prefab.name + " serialized skill");
                imported++;
                // Special AI is unchanged; avoid starting a boss encounter for a data migration.
                if (prefab.name.StartsWith("Sandworm") || prefab.name.StartsWith("Skeleton") || prefab.name.StartsWith("Automaton")) continue;
                var instance = Object.Instantiate(prefab, new Vector3(1000, 1000), Quaternion.identity);
                var live = instance.GetComponent<ZeldaCharacterData>();
                if (live.SkillValue != expected || live.FinalSkillValue != expected) throw new Exception(prefab.name + " runtime skill");
                var state = live.CaptureRuntimeState();
                if (state.skillValue != expected) throw new Exception(prefab.name + " captured skill");
                live.ApplyRuntimeState(state);
                if (live.SkillValue != expected) throw new Exception(prefab.name + " restored skill");
                instance.SetActive(false); instance.SetActive(true);
                if (live.SkillValue != expected) throw new Exception(prefab.name + " streaming reset skill");
                Object.DestroyImmediate(instance);
                spawned++;
            }
            if (imported < 45 || spawned != imported - 3) throw new Exception("Unexpected prefab coverage: " + imported + "/" + spawned);
            Debug.Log("LEVEL2_SKILL_VALUES_PASS imported=" + imported + " spawned=" + spawned);
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
