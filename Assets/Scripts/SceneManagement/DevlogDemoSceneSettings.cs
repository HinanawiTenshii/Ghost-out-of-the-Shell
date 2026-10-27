using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Scene-local recording options; ordinary levels keep all safety systems.</summary>
[DisallowMultipleComponent, DefaultExecutionOrder(-1000)]
public sealed class DevlogDemoSceneSettings : MonoBehaviour
{
    [SerializeField, Tooltip("演示时不自动建立正式进度或写入自动存档；手动保存仍由玩家选择。")]
    private bool suppressAutomaticSaves = true;

    private static readonly Dictionary<int, DevlogDemoSceneSettings> scenes =
        new Dictionary<int, DevlogDemoSceneSettings>();
    private int registeredScene;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => scenes.Clear();

    private void OnEnable()
    {
        registeredScene = gameObject.scene.handle;
        scenes[registeredScene] = this;
    }

    private void OnDisable()
    {
        if (scenes.TryGetValue(registeredScene, out var value) && value == this)
            scenes.Remove(registeredScene);
    }

    public static bool SuppressesAutomaticSaves(Scene scene) =>
        scenes.TryGetValue(scene.handle, out var settings) && settings != null &&
        settings.isActiveAndEnabled && settings.suppressAutomaticSaves;
}
