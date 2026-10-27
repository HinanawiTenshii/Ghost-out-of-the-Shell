using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Read-only validation of resolved scene/prefab settings. No scenes are saved.
public static class LevelCrtSettingsSmoke
{
    static readonly string[] FloatFields = {
        "curvature", "scanlineIntensity", "scanlineCount", "chromaticAberration",
        "vignette", "maskIntensity", "noiseIntensity", "brightness", "contrast", "phosphorGlow"
    };
    static int checks;
    static void Check(bool condition, string label)
    {
        checks++;
        if (!condition) throw new Exception(label);
    }
    static CRTScreenEffect Effect(Scene scene)
    {
        var effects = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CRTScreenEffect>(true)).ToArray();
        Check(effects.Length == 1, scene.name + ": exactly one authored CRT effect");
        Check(effects[0].GetComponent<Camera>() != null, scene.name + ": attached to camera");
        return effects[0];
    }
    public static void Run()
    {
        try
        {
            Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "Use isolated validation project");
            var baseline = Effect(EditorSceneManager.OpenScene("Assets/Scenes/Level0.unity"));
            var serialized = new SerializedObject(baseline);
            var expected = new Dictionary<string, float>();
            foreach (var field in FloatFields) expected[field] = serialized.FindProperty(field).floatValue;
            var material = serialized.FindProperty("crtMaterial").objectReferenceValue;
            bool enabled = baseline.enabled;
            Check(material != null && enabled, "Level0 has enabled CRT with material");
            foreach (var pair in expected) Debug.Log("CRT_BASELINE " + pair.Key + "=" + pair.Value);
            var paths = Directory.GetFiles("Assets/Scenes/Level1", "*.unity")
                .Concat(Directory.GetFiles("Assets/Scenes/Level2", "*.unity")).OrderBy(p => p).ToArray();
            Check(paths.Length == 9, "All five Level1 and four Level2 scenes included");
            foreach (var path in paths)
            {
                var effect = Effect(EditorSceneManager.OpenScene(path));
                var target = new SerializedObject(effect);
                foreach (var field in FloatFields)
                    Check(Mathf.Approximately(target.FindProperty(field).floatValue, expected[field]), path + ": " + field);
                Check(target.FindProperty("crtMaterial").objectReferenceValue == material, path + ": shared material matches Level0");
                Check(effect.enabled == enabled, path + ": CRT enabled state matches Level0");
                Debug.Log("CRT_SCENE_OK " + path.Replace('\\', '/'));
            }
            Debug.Log("LEVEL_CRT_SETTINGS_PASS scenes=" + paths.Length + " checks=" + checks);
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
