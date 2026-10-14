using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>One sound emitter per undirected electric field, with XY/segment distance falloff.</summary>
[DisallowMultipleComponent]
public sealed class ElectricGateAudio : MonoBehaviour
{
    private static AudioClip humClip, warningClip, strikeClip;
    private Transform start, end;
    private AudioSource hum, warning, strike;
    private bool powered, strengthened, paused;
    private float powerBlend, warningBlend, nextStrikeTime;
    private Vector2 lastStrikePoint;

    [Range(0f, 1f)] public float humVolume = 0.24f;
    [Range(0f, 1f)] public float warningVolume = 0.42f;
    [Range(0f, 1f)] public float strikeVolume = 0.75f;
    [Min(0f)] public float fullVolumeDistance = 1.5f;
    [Min(0.1f)] public float audibleDistance = 16f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetClips() { humClip = warningClip = strikeClip = null; }

    public void Initialize(Transform first, Transform second)
    {
        start = first; end = second;
        // PCM/preload clips are shared by every pair; no I/O on the damage frame.
        if (humClip == null) humClip = Resources.Load<AudioClip>("Audio/ElectricGate/CurrentLoop");
        if (warningClip == null) warningClip = Resources.Load<AudioClip>("Audio/ElectricGate/WarningLoop");
        if (strikeClip == null) strikeClip = Resources.Load<AudioClip>("Audio/ElectricGate/Discharge");
        hum = CreateSource(humClip, true, 180);
        warning = CreateSource(warningClip, true, 150);
        strike = CreateSource(strikeClip, false, 100);
    }

    private AudioSource CreateSource(AudioClip clip, bool loop, int priority)
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false; source.loop = loop; source.clip = clip;
        // The camera listener is at Z=-10. Native 3D distance would make every gate sound remote.
        // Use manual planar attenuation and modest pan instead, with zero residual 2D volume at range.
        source.spatialBlend = 0f; source.dopplerLevel = 0f; source.volume = 0f;
        source.priority = priority; source.ignoreListenerPause = false;
        return source;
    }

    public void SetState(bool active, bool warningActive)
    {
        powered = active;
        strengthened = active && warningActive;
    }

    private bool TryGetEar(out Vector2 position)
    {
        position = Vector2.zero;
        var mover = ZeldaRuntimeRegistry.GetControlledMover();
        if (mover != null)
        {
            if (ZeldaRuntimeRegistry.GetGameplayScene(mover.gameObject) != gameObject.scene) return false;
            position = mover.transform.position;
            return true;
        }
        Camera camera = Camera.main;
        if (camera == null || SceneManager.GetActiveScene() != gameObject.scene) return false;
        position = camera.transform.position;
        return true;
    }

    private void Update()
    {
        if (hum == null) return;
        if (start == null || end == null || !start.gameObject.activeInHierarchy || !end.gameObject.activeInHierarchy)
        {
            StopAll(); return;
        }
        // Physics/visual warning freezes during menus/loading; freeze audio at the same time.
        bool shouldPause = Time.timeScale <= 0f || GameSaveSystem.IsLoading;
        SetPaused(shouldPause);
        if (shouldPause) return;
        if (!TryGetEar(out Vector2 ear)) { StopAll(); return; }

        float dt = Time.unscaledDeltaTime;
        powerBlend = Mathf.MoveTowards(powerBlend, powered ? 1f : 0f, dt / 0.08f);
        warningBlend = Mathf.MoveTowards(warningBlend, strengthened ? 1f : 0f, dt / 0.12f);
        Vector2 nearest = NearestPoint(ear, start.position, end.position);
        float gain = DistanceGain(Vector2.Distance(ear, nearest), fullVolumeDistance, audibleDistance);
        float pan = Mathf.Clamp((nearest.x - ear.x) / 10f, -0.65f, 0.65f);
        UpdateLoop(hum, humVolume * powerBlend * (1f - warningBlend * 0.3f) * gain, pan);
        UpdateLoop(warning, warningVolume * warningBlend * powerBlend * gain, pan);
        strike.volume = strikeVolume * DistanceGain(Vector2.Distance(ear, lastStrikePoint), fullVolumeDistance, audibleDistance);
        strike.panStereo = Mathf.Clamp((lastStrikePoint.x - ear.x) / 10f, -0.65f, 0.65f);
    }

    private static void UpdateLoop(AudioSource source, float volume, float pan)
    {
        source.volume = Mathf.Clamp01(volume); source.panStereo = pan;
        if (volume > 0.0001f && source.clip != null)
        {
            if (!source.isPlaying) source.Play();
        }
        else if (source.isPlaying) source.Stop();
    }

    public void PlayStrike(Vector2 point)
    {
        if (strike == null || strike.clip == null || Time.timeScale <= 0f || GameSaveSystem.IsLoading ||
            Time.unscaledTime < nextStrikeTime || !TryGetEar(out Vector2 ear)) return;
        lastStrikePoint = point;
        float gain = DistanceGain(Vector2.Distance(ear, point), fullVolumeDistance, audibleDistance);
        if (gain <= 0f) return;
        // One discharge per two-arc pair; simultaneous victims are coalesced
        // instead of multiplying peak volume. The delayed pair gets its own cue.
        nextStrikeTime = Time.unscaledTime + 0.08f;
        strike.volume = strikeVolume * gain;
        strike.panStereo = Mathf.Clamp((point.x - ear.x) / 10f, -0.65f, 0.65f);
        strike.Play();
        // Independent of powered: the final battery unit must still produce its complete discharge sound.
    }

    private void SetPaused(bool value)
    {
        if (paused == value) return;
        paused = value;
        if (value) { hum.Pause(); warning.Pause(); strike.Pause(); }
        else { hum.UnPause(); warning.UnPause(); strike.UnPause(); }
    }

    private void StopAll()
    {
        if (hum != null) hum.Stop();
        if (warning != null) warning.Stop();
        if (strike != null) strike.Stop();
        powerBlend = warningBlend = 0f;
    }

    private void OnDisable() { StopAll(); paused = false; powered = strengthened = false; }

    public static Vector2 NearestPoint(Vector2 point, Vector2 a, Vector2 b)
    {
        Vector2 axis = b - a;
        return axis.sqrMagnitude <= 0.000001f ? a : a + axis * Mathf.Clamp01(Vector2.Dot(point - a, axis) / axis.sqrMagnitude);
    }

    public static float DistanceGain(float distance, float near, float far)
    {
        near = Mathf.Max(0f, near); far = Mathf.Max(near + 0.01f, far);
        float t = Mathf.Clamp01((distance - near) / (far - near));
        return 1f - t * t * (3f - 2f * t);
    }
}
