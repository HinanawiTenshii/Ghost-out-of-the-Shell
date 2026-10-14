using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class TurretShotAudio : MonoBehaviour
{
    private AudioSource source;
    private float remaining;
    private float volume;
    private bool paused;
    public static void Play(Scene scene, Vector3 position, AudioClip clip, float volume)
    {
        if (clip == null || !scene.IsValid() || !scene.isLoaded) return;
        var go = new GameObject("Heavy Turret Shot"); SceneManager.MoveGameObjectToScene(go, scene);
        go.transform.position = position; go.AddComponent<CameraVisionStreamingExempt>();
        var sound = go.AddComponent<TurretShotAudio>(); sound.volume = volume; sound.remaining = clip.length + .05f;
        sound.source = go.AddComponent<AudioSource>(); sound.source.playOnAwake = false;
        sound.source.clip = clip; sound.source.spatialBlend = 0; sound.source.dopplerLevel = 0;
        sound.RefreshVolume(); sound.source.Play();
    }
    private void RefreshVolume()
    {
        var player = ZeldaRuntimeRegistry.GetControlledMover();
        Vector2 listener = player != null ? (Vector2)player.transform.position : Camera.main != null ? (Vector2)Camera.main.transform.position : (Vector2)transform.position;
        source.volume = volume * Mathf.Clamp01(1f - Mathf.Max(0f, Vector2.Distance(listener, transform.position) - 2f) / 20f);
    }
    private void Update()
    {
        bool pause = Time.timeScale <= 0;
        if (pause != paused) { if (pause) source.Pause(); else source.UnPause(); paused = pause; }
        RefreshVolume(); remaining -= Time.deltaTime;
        if (remaining <= 0f) Destroy(gameObject);
    }
}
