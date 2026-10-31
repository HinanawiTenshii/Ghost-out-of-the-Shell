#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ApplyVisualStyleLabTerminal
{
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\','/').Contains("/.codex-temp/")) throw new Exception("Isolated lab only");
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Experiments/VisualStyleLab.unity");
        var components = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Component>(true)).Where(c => c != null).ToArray();
        if (components.Any(c => c is VisualStyleLabTerminalPresentation)) throw new Exception("Already applied");
        var snapshot = typeof(ApplyVisualStyleLabActorsProps).GetMethod("GameplayFields", BindingFlags.NonPublic|BindingFlags.Static);
        var unchanged = components.ToDictionary(c => c, c => (string)snapshot.Invoke(null, new object[]{c}));
        var systems = scene.GetRootGameObjects().Single(r => r.name.StartsWith("00"));
        systems.AddComponent<VisualStyleLabTerminalPresentation>();
        foreach (var p in unchanged)
            if (p.Key == null || p.Value != (string)snapshot.Invoke(null, new object[]{p.Key})) throw new Exception("Gameplay changed: " + p.Key);
        ConfigureNotice();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Debug.Log("LAB_TERMINAL_PASS scene-only-profile=1 existing-gameplay=unchanged");
    }
    public static void CompleteTypography()
    {
        if (!Application.dataPath.Replace('\\','/').Contains("/.codex-temp/")) throw new Exception("Isolated lab only");
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/Experiments/VisualStyleLab.unity");
        ConfigureNotice(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
    }
    private static void ConfigureNotice()
    {
        var hud=UnityEngine.Object.FindObjectOfType<ZeldaHealthHeartsUI>();
        var font=new SerializedObject(hud).FindProperty("permissionLabelFont").objectReferenceValue;
        if (font==null) throw new Exception("Missing lab UI font");
        var notice=UnityEngine.Object.FindObjectOfType<DocumentReader>();
        var data=new SerializedObject(notice);
        data.FindProperty("documentFont").objectReferenceValue=font;
        // Keep this lab-only help page within the existing reader's fixed text area.
        data.FindProperty("documentText").stringValue="视觉实验街区\n\n按[F]附身左侧工程师，使用拉杆开启商铺大门。室内可拾取物品并查看说明。\n\n右侧水道可测试水闸与断电效果。北侧紫色区域需要权限2。\n\n右键查看属性；E交互；C物品说明；Tab地图与技能；Esc暂停。\n\n此处不自动保存，重新运行将恢复初始布局。";
        data.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
