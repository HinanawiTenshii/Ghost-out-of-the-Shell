using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Names only: validates live HUD, current map and unloaded-map display paths.
public static class Level2MapNamesSmoke
{
    static readonly string[] Scenes = { "Level2-Floor1", "Level2-Floor2", "Level2-Floor-1", "Level2-Desert" };
    static readonly string[] Labels = { "城镇一层", "城镇二层", "城镇地下", "沙漠" };
    static int checks;
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    public static void Run()
    {
        try
        {
            Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "isolated project required");
            var hudName = typeof(ZeldaHealthHeartsUI).GetMethod("ResolveSceneAreaDisplayName", BindingFlags.NonPublic | BindingFlags.Static);
            var menuName = typeof(TabJournalMenuController).GetMethod("GetMapSceneDisplayName", BindingFlags.NonPublic | BindingFlags.Instance);
            for (int i = 0; i < Scenes.Length; i++)
            {
                string name = Scenes[i];
                Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/Level2/" + name + ".unity");
                var roots = scene.GetRootGameObjects();
                var menu = roots.SelectMany(r => r.GetComponentsInChildren<TabJournalMenuController>(true)).Single();
                Check(scene.name == name, "runtime scene identity unchanged " + name);
                Check(menu.AreaDisplayName == Labels[i], "authored map name " + name);
                Check((string)hudName.Invoke(null, new object[] { scene }) == Labels[i], "HUD minimap label " + name);
                var snapshot = Resources.Load<RuntimeMiniMapSceneData>("MiniMaps/" + name);
                Check(snapshot != null && snapshot.SceneName == name && snapshot.DisplayName == Labels[i], "snapshot identity and label " + name);
                for (int j = 0; j < Scenes.Length; j++)
                    Check((string)menuName.Invoke(menu, new object[] { Scenes[j], scene }) == Labels[j], "menu current/unloaded region label " + name + "/" + Scenes[j]);
                var group = roots.SelectMany(r => r.GetComponentsInChildren<LevelSceneGroup>(true)).Single().CreateDefinition();
                Check(group.LevelId == "level2" && group.GetSceneSaveKey(name) == "levels/level2/scenes/" + name, "save identifiers unchanged " + name);
            }
            Debug.Log("LEVEL2_MAP_NAMES_PASS checks=" + checks);
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
