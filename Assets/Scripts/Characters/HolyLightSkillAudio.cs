using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Audio follows the existing skill clock; one voice per four-column wave, not per column.</summary>
public sealed class HolyLightSkillAudio
{
    public const float CentralVolume = .55f, WaveVolume = .45f;
    public const float NearDistance = 2f, FarDistance = 22f;
    private static AudioClip centralClip, waveClip;
    private static readonly Vector2[] Directions = { Vector2.up, Vector2.right, Vector2.down, Vector2.left };
    private readonly GameObject owner;
    private readonly Vector2 origin;
    private readonly AudioSource[] voices;
    private readonly bool[] started, paused;
    private readonly int rounds;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetClips() { centralClip = waveClip = null; }

    public HolyLightSkillAudio(GameObject owner, Vector2 origin, int rounds)
    {
        this.owner = owner; this.origin = origin; this.rounds = Mathf.Clamp(rounds, 0, 4);
        voices = new AudioSource[this.rounds + 1];
        started = new bool[voices.Length]; paused = new bool[voices.Length];
        if (centralClip == null) centralClip = Resources.Load<AudioClip>("Audio/HolyLightCentral");
        if (waveClip == null) waveClip = Resources.Load<AudioClip>("Audio/HolyLightWave");
    }

    public void RenderAt(float elapsed, bool cancelled)
    {
        if (cancelled || owner == null) { Stop(); return; }
        bool freeze = Time.timeScale <= 0f || GameSaveSystem.IsLoading || AudioListener.pause;
        bool canHear = TryGetEar(out Vector2 ear);
        for (int i = 0; i <= rounds; i++)
        {
            var clip = i == 0 ? centralClip : waveClip;
            float at = i == 0 ? 0f : HolyLightSkillRuntime.DescentDuration + HolyLightSkillRuntime.CentralDuration +
                (i - 1) * HolyLightSkillRuntime.RoundInterval;
            float age = elapsed - at;
            if (clip == null || age < 0f || age >= clip.length)
            {
                StopVoice(i);
                continue;
            }
            var source = voices[i];
            if (freeze)
            {
                if (source != null && started[i] && !paused[i]) { source.Pause(); paused[i] = true; }
                continue;
            }
            if (source == null)
            {
                source = owner.AddComponent<AudioSource>(); voices[i] = source;
                source.playOnAwake = false; source.loop = false; source.clip = clip;
                source.spatialBlend = 0f; source.dopplerLevel = 0f; source.priority = 110;
                source.ignoreListenerPause = false;
            }
            float distance = i == 0 ? 0f : HolyLightSkillRuntime.FirstRoundDistance + (i - 1) * HolyLightSkillRuntime.RoundSpacing;
            GetMix(ear, distance, out float gain, out float pan);
            source.volume = canHear ? gain * (i == 0 ? CentralVolume : WaveVolume) : 0f;
            source.panStereo = pan;
            source.pitch = Mathf.Clamp(Time.timeScale, .01f, 3f);
            int sample = Mathf.Clamp(Mathf.RoundToInt(age * clip.frequency), 0, clip.samples - 1);
            if (!started[i])
            {
                source.timeSamples = sample; source.Play(); started[i] = true;
            }
            else if (paused[i])
            {
                source.timeSamples = sample; source.UnPause(); paused[i] = false;
            }
            // Recover from hitches or application suspension without replaying impacts.
            else if (Mathf.Abs(source.time - age) > .1f) source.timeSamples = sample;
        }
    }

    private bool TryGetEar(out Vector2 ear)
    {
        ear = Vector2.zero;
        if (SceneManager.GetActiveScene() != owner.scene) return false;
        var mover = ZeldaRuntimeRegistry.GetControlledMover();
        if (mover != null && ZeldaRuntimeRegistry.GetGameplayScene(mover.gameObject) == owner.scene)
        { ear = mover.transform.position; return true; }
        var camera = Camera.main;
        if (camera == null) return false;
        ear = camera.transform.position; return true;
    }

    private void GetMix(Vector2 ear, float distance, out float gain, out float pan)
    {
        if (distance <= 0f)
        {
            Vector2 offset = origin - ear;
            gain = ElectricGateAudio.DistanceGain(offset.magnitude, NearDistance, FarDistance);
            pan = Mathf.Clamp(offset.x / FarDistance, -.55f, .55f); return;
        }
        // All four strike positions inform attenuation/pan, but never multiply loudness by four.
        gain = 0f; float weightedPan = 0f, weight = 0f;
        foreach (var direction in Directions)
        {
            Vector2 offset = origin + direction * distance - ear;
            float level = ElectricGateAudio.DistanceGain(offset.magnitude, NearDistance, FarDistance);
            gain = Mathf.Max(gain, level);
            weightedPan += Mathf.Clamp(offset.x / FarDistance, -.55f, .55f) * level; weight += level;
        }
        pan = weight > 0f ? weightedPan / weight : 0f;
    }

    private void StopVoice(int index)
    {
        if (voices[index] != null && started[index]) voices[index].Stop();
        started[index] = paused[index] = false;
    }
    public void Stop()
    {
        for (int i = 0; i < voices.Length; i++) StopVoice(i);
    }
}
