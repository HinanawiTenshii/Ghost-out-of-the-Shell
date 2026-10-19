using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Run only in the isolated test project; does not save scenes or change assets.
public static class Level1PlayerViewSmoke
{
    static readonly string[] Scenes = {
        "Level1-Garden", "Level1-Floor-1", "Level1-Floor1", "Level1-Floor2", "Level1-Floor3"
    };
    static int checks;
    static void Check(bool value, string context)
    {
        checks++;
        if (!value) throw new Exception(context);
    }
    static void VerifyView(ZeldaCharacterData data, string scenePath, string context)
    {
        float expectedSize = data is GhostZeldaCharacterData ? 5f : 9f;
        Check(Mathf.Approximately(data.GetCameraOrthographicSize(scenePath), expectedSize), context + " camera size");
        Check(Mathf.Approximately(data.GetPlayerVisionRadius(scenePath), 13f), context + " player vision");
    }
    public static void Run()
    {
        try
        {
            Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "isolated project only");
            int characters = 0, prefabs = 0;
            foreach (string name in Scenes)
            {
                string path = "Assets/Scenes/Level1/" + name + ".unity";
                var scene = EditorSceneManager.OpenScene(path);
                var actors = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<ZeldaCharacterData>(true)).ToArray();
                Check(actors.Length > 0, "No characters in " + name);
                foreach (var actor in actors) VerifyView(actor, path, name + "/" + actor.name);
                characters += actors.Length;
                Debug.Log("LEVEL1_PLAYER_VIEW_SCENE " + name + " characters=" + actors.Length);
            }
            var paths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Level1NPCS" })
                .Select(AssetDatabase.GUIDToAssetPath).Concat(new[] {
                    "Assets/Prefabs/Ghost.prefab", "Assets/Prefabs/SwordGuy.prefab",
                    "Assets/Prefabs/Level0NPCs/SwordGuy-level0.prefab"
                });
            foreach (string path in paths)
            {
                var data = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<ZeldaCharacterData>();
                Check(data != null, "Character data missing on " + path);
                foreach (string scene in Scenes)
                    VerifyView(data, "Assets/Scenes/Level1/" + scene + ".unity", path + " in " + scene);
                foreach (string other in new[] { "Assets/Scenes/TutorialLevel.unity", "Assets/Scenes/Level0.unity",
                    "Assets/Scenes/Level2/Level2-Floor1.unity", "Assets/Scenes/Level2/Level2-Desert.unity" })
                {
                    float originalSize = path == "Assets/Prefabs/Ghost.prefab" ? 5f : 7f;
                    Check(Mathf.Approximately(data.GetCameraOrthographicSize(other), originalSize), path + " changed another level's camera");
                    Check(Mathf.Approximately(data.GetPlayerVisionRadius(other), 13f), path + " changed another level's vision");
                }
                prefabs++;
            }
            Debug.Log("LEVEL1_PLAYER_VIEW_PASS characters=" + characters + " prefabs=" + prefabs + " checks=" + checks);
            EditorApplication.Exit(0);
        }
        catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
    }
}
