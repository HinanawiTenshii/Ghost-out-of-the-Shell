using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Two separate cues, triggered by the wave's actual release and terminal events.</summary>
public sealed class PurpleEnergyWaveAudio : MonoBehaviour
{
    public const float ReleaseVolume = .5f, BurstVolume = .6f;
    public const float NearDistance = 2f, FarDistance = 18f;
    private static AudioClip releaseClip, burstClip;
    private AudioSource voice;
    private float age, lifetime, gain, playbackRate;
    private bool paused, started;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetClips() { releaseClip = burstClip = null; }

    public static void PlayRelease(Transform attack, Vector3 hand, float travelDuration)
    {
        if (releaseClip == null) releaseClip = Resources.Load<AudioClip>("Audio/PurpleWaveRelease");
        // Load both small PCM clips before travel, not on the terminal impact frame.
        if (burstClip == null) burstClip = Resources.Load<AudioClip>("Audio/PurpleWaveBurst");
        Create(attack.gameObject.scene, attack, hand, releaseClip, travelDuration, ReleaseVolume);
    }

    public static void PlayBurst(Scene scene, Vector3 terminal)
    {
        if (burstClip == null) burstClip = Resources.Load<AudioClip>("Audio/PurpleWaveBurst");
        if (burstClip != null) Create(scene, null, terminal, burstClip, burstClip.length, BurstVolume);
    }

    private static void Create(Scene scene, Transform parent, Vector3 point, AudioClip clip, float seconds, float volume)
    {
        if (clip == null || !scene.IsValid() || !scene.isLoaded) return;
        var root = new GameObject(parent != null ? "Purple Wave Release Audio" : "Purple Wave Burst Audio");
        SceneManager.MoveGameObjectToScene(root, scene);
        if (parent != null) root.transform.SetParent(parent, false);
        root.transform.position = point;
        root.AddComponent<CameraVisionStreamingExempt>();
        var sound = root.AddComponent<PurpleEnergyWaveAudio>();
        sound.lifetime = Mathf.Max(.05f, seconds); sound.gain = volume;
        sound.playbackRate = clip.length / sound.lifetime;
        sound.voice = root.AddComponent<AudioSource>();
        sound.voice.playOnAwake = false; sound.voice.loop = false; sound.voice.clip = clip;
        // The camera listener is offset in Z; attenuation is deliberately planar.
        sound.voice.spatialBlend = 0; sound.voice.dopplerLevel = 0;
        sound.voice.priority = 115; sound.voice.ignoreListenerPause = false;
        sound.RenderAudio();
    }

    private void Update()
    {
        bool freeze = Time.timeScale <= 0 || AudioListener.pause || GameSaveSystem.IsLoading;
        if (!freeze) age += Time.deltaTime;
        if (age >= lifetime) { voice.Stop(); Destroy(gameObject); return; }
        RenderAudio();
    }

    private void RenderAudio()
    {
        if (voice == null) return;
        if (Time.timeScale <= 0 || AudioListener.pause || GameSaveSystem.IsLoading)
        {
            if (started && !paused) voice.Pause();
            paused = true; return;
        }
        bool audible = TryGetEar(out Vector2 ear);
        Vector2 offset = (Vector2)transform.position - ear;
        voice.volume = audible ? gain * ElectricGateAudio.DistanceGain(offset.magnitude, NearDistance, FarDistance) : 0;
        voice.panStereo = Mathf.Clamp(offset.x / FarDistance, -.55f, .55f);
        voice.pitch = Mathf.Clamp(playbackRate * Time.timeScale, .01f, 3f);
        if (!started) { voice.Play(); started = true; }
        else if (paused) voice.UnPause();
        paused = false;
        // Keep phase aligned after slow motion, suspension or a frame hitch.
        float clipAge = Mathf.Min(age * playbackRate, voice.clip.length - 1f / voice.clip.frequency);
        if (Mathf.Abs(voice.time - clipAge) > .08f)
            voice.timeSamples = Mathf.Clamp(Mathf.RoundToInt(clipAge * voice.clip.frequency), 0, voice.clip.samples - 1);
    }

    private bool TryGetEar(out Vector2 ear)
    {
        ear = Vector2.zero;
        var player = ZeldaRuntimeRegistry.GetControlledMover();
        if (player != null)
        {
            if (ZeldaRuntimeRegistry.GetGameplayScene(player.gameObject) != gameObject.scene) return false;
            ear = player.transform.position; return true;
        }
        var camera = Camera.main;
        if (camera == null || SceneManager.GetActiveScene() != gameObject.scene) return false;
        ear = camera.transform.position; return true;
    }

    private void OnDisable() { if (voice != null) voice.Stop(); }
}
