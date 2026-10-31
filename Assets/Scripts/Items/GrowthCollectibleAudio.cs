using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Local pickup feedback only: no AI attraction or persistent/save state.</summary>
[DisallowMultipleComponent]
public sealed class GrowthCollectibleAudio : MonoBehaviour
{
    private static AudioClip defaultClip;
    private AudioSource source;
    private float remaining;
    private bool paused;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetClip() => defaultClip = null;

    public static void Preload()
    {
        if (defaultClip == null) defaultClip = Resources.Load<AudioClip>("Audio/DataPickupCrystal");
    }

    public static void Play(Scene scene, Vector3 position, AudioClip clip, float volume)
    {
        if (!Application.isPlaying || GameSaveSystem.IsLoading || volume <= 0f ||
            !scene.IsValid() || !scene.isLoaded) return;
        if (clip == null) { Preload(); clip = defaultClip; }
        if (clip == null) return;

        // Outlives the collected object and even very short/custom pickup particles.
        var root = new GameObject("D.A.T.A Crystal Pickup Sound");
        SceneManager.MoveGameObjectToScene(root, scene);
        root.transform.position = position;
        root.AddComponent<CameraVisionStreamingExempt>();
        var sound = root.AddComponent<GrowthCollectibleAudio>();
        sound.source = root.AddComponent<AudioSource>();
        sound.source.playOnAwake = false;
        sound.source.loop = false;
        sound.source.clip = clip;
        sound.source.volume = Mathf.Clamp01(volume);
        // Player feedback, not a world noise: camera Z must not muffle the pickup.
        sound.source.spatialBlend = 0f;
        sound.source.dopplerLevel = 0f;
        sound.source.pitch = 1f;
        sound.source.ignoreListenerPause = false;
        sound.source.priority = 100;
        sound.remaining = clip.length + .1f;
        sound.source.Play();
        sound.RefreshPause();
    }

    private void RefreshPause()
    {
        bool freeze = Time.timeScale <= 0f || AudioListener.pause || GameSaveSystem.IsLoading;
        if (freeze == paused) return;
        paused = freeze;
        if (paused) source.Pause(); else source.UnPause();
    }

    private void Update()
    {
        RefreshPause();
        if (paused) return;
        remaining -= Time.unscaledDeltaTime;
        if (remaining <= 0f) Destroy(gameObject);
    }
}
