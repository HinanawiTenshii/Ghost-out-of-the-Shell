using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Skill-clock audio, with bounded transient pools and one spatially mixed fire bed.</summary>
public sealed class MeteorShowerAudio
{
    public const float DescentVolume = .32f, ImpactVolume = .6f, FireVolume = .27f;
    public const float NearDistance = 2f, FarDistance = 22f;
    private const int PoolSize = 4; // .8/.82-second cues fit between events in a four-slot .28-second ring.
    private struct Landing { public Vector2 point; public float at; }
    private sealed class Voice
    {
        public AudioSource source;
        public int eventId = -1, pendingId;
        public float age, gain, pan;
        public bool started, paused;
    }
    private static AudioClip descentClip, impactClip, fireClip;
    private static readonly HashSet<MeteorShowerAudio> active = new HashSet<MeteorShowerAudio>();
    private readonly GameObject owner;
    private readonly bool hasFire;
    private readonly List<Landing> landings = new List<Landing>(15);
    private readonly Voice[] descents = NewPool(), impacts = NewPool();
    private readonly Voice fire = new Voice();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { descentClip = impactClip = fireClip = null; active.Clear(); }
    private static Voice[] NewPool()
    {
        var pool = new Voice[PoolSize]; for (int i = 0; i < pool.Length; i++) pool[i] = new Voice(); return pool;
    }
    public MeteorShowerAudio(GameObject root, bool lingeringFire)
    {
        owner = root; hasFire = lingeringFire;
        // Load before the first descent, never on a crowded landing frame.
        if (descentClip == null) descentClip = Resources.Load<AudioClip>("Audio/MeteorDescent");
        if (impactClip == null) impactClip = Resources.Load<AudioClip>("Audio/MeteorImpact");
        if (hasFire && fireClip == null) fireClip = Resources.Load<AudioClip>("Audio/MeteorFireLoop");
    }
    public void AddLanding(Vector2 point, float at) => landings.Add(new Landing { point = point, at = at });

    public void RenderAt(float elapsed)
    {
        if (owner == null) { Stop(); return; }
        active.Add(this);
        if (Time.timeScale <= 0f || AudioListener.pause || GameSaveSystem.IsLoading)
        {
            foreach (var voice in descents) Pause(voice);
            foreach (var voice in impacts) Pause(voice);
            Pause(fire); return;
        }
        bool audible = TryGetEar(out Vector2 ear);
        RenderPhase(descents, descentClip, 0, MeteorShowerSkillRuntime.FallDuration, DescentVolume, elapsed, ear, audible, "Meteor Descent Audio");
        RenderPhase(impacts, impactClip, MeteorShowerSkillRuntime.FallDuration, impactClip != null ? impactClip.length : .82f,
            ImpactVolume, elapsed, ear, audible, "Meteor Impact Audio");
        RenderFire(elapsed, ear, audible);
    }
    private void RenderPhase(Voice[] pool, AudioClip clip, float delay, float duration, float volume,
        float elapsed, Vector2 ear, bool audible, string label)
    {
        float totalGain = 0;
        for (int slot = 0; slot < pool.Length; slot++)
        {
            var voice = pool[slot]; voice.pendingId = -1; voice.gain = 0;
            for (int i = slot; i < landings.Count; i += pool.Length)
            {
                float age = elapsed - landings[i].at - delay;
                if (age < 0 || age >= duration) continue;
                Vector2 offset = landings[i].point - ear;
                voice.pendingId = i; voice.age = age;
                voice.gain = audible ? ElectricGateAudio.DistanceGain(offset.magnitude, NearDistance, FarDistance) : 0;
                voice.pan = Mathf.Clamp(offset.x / FarDistance, -.55f, .55f);
                totalGain += voice.gain; break;
            }
        }
        // Gain budgets cap coherent peaks even when every impact is nearby.
        // Multiple simultaneous casts also share that budget instead of doubling loudness.
        float normalization = Mathf.Max(1f, totalGain) * Mathf.Max(1, active.Count);
        foreach (var voice in pool)
        {
            if (voice.pendingId < 0 || clip == null) { StopVoice(voice); continue; }
            Play(voice, label, clip, voice.pendingId, landings[voice.pendingId].point,
                voice.age * clip.length / duration, clip.length / duration,
                volume * voice.gain / normalization, voice.pan, false);
        }
    }
    private void RenderFire(float elapsed, Vector2 ear, bool audible)
    {
        if (!hasFire || fireClip == null) { StopVoice(fire); return; }
        float level = 0, panSum = 0, weight = 0;
        Vector2 weightedPoint = Vector2.zero;
        bool burning = false;
        foreach (var landing in landings)
        {
            float age = elapsed - landing.at - MeteorShowerSkillRuntime.FallDuration;
            if (age < 0 || age >= MeteorShowerSkillRuntime.FireDuration) continue;
            burning = true;
            Vector2 offset = landing.point - ear;
            float envelope = Mathf.Clamp01(Mathf.Min(age / .18f, (MeteorShowerSkillRuntime.FireDuration - age) / .3f));
            // Standing anywhere inside a fire disk sounds close, not four units away from its center.
            float gain = audible ? ElectricGateAudio.DistanceGain(Mathf.Max(0, offset.magnitude - MeteorShowerSkillRuntime.Radius),
                NearDistance, FarDistance) * envelope : 0;
            level = Mathf.Max(level, gain);
            panSum += Mathf.Clamp(offset.x / FarDistance, -.55f, .55f) * gain;
            weightedPoint += landing.point * gain; weight += gain;
        }
        if (!burning) { StopVoice(fire); return; }
        Play(fire, "Meteor Fire Audio", fireClip, 0, weight > 0 ? weightedPoint / weight : ear,
            Mathf.Repeat(elapsed - MeteorShowerSkillRuntime.FallDuration, fireClip.length), 1f,
            level * FireVolume / Mathf.Max(1, active.Count), weight > 0 ? panSum / weight : 0f, true);
    }
    private void Play(Voice voice, string label, AudioClip clip, int eventId, Vector2 point,
        float clipAge, float rate, float gain, float pan, bool loop)
    {
        if (gain <= 0f) { StopVoice(voice); return; }
        if (voice.source == null)
        {
            var go = new GameObject(label); go.transform.SetParent(owner.transform, false);
            var source = go.AddComponent<AudioSource>(); voice.source = source;
            source.playOnAwake = false; source.spatialBlend = 0f; source.dopplerLevel = 0f;
            source.ignoreListenerPause = false; source.priority = loop ? 160 : 112;
        }
        var sound = voice.source;
        sound.transform.position = point; sound.volume = gain; sound.panStereo = pan;
        sound.pitch = Mathf.Clamp(rate * Time.timeScale, .01f, 3f);
        int sample = Mathf.Clamp(Mathf.RoundToInt(clipAge * clip.frequency), 0, clip.samples - 1);
        if (!voice.started || voice.eventId != eventId)
        {
            sound.Stop(); sound.clip = clip; sound.loop = loop; sound.timeSamples = sample; sound.Play();
            voice.started = true; voice.eventId = eventId; voice.paused = false;
        }
        else if (voice.paused)
        {
            sound.timeSamples = sample; sound.UnPause(); voice.paused = false;
        }
        else if (Mathf.Abs(sound.time - clipAge) > .08f) sound.timeSamples = sample;
    }
    private bool TryGetEar(out Vector2 ear)
    {
        ear = Vector2.zero;
        var mover = ZeldaRuntimeRegistry.GetControlledMover();
        if (mover != null)
        {
            if (ZeldaRuntimeRegistry.GetGameplayScene(mover.gameObject) != owner.scene) return false;
            ear = mover.transform.position; return true;
        }
        var camera = Camera.main;
        if (camera == null || SceneManager.GetActiveScene() != owner.scene) return false;
        ear = camera.transform.position; return true;
    }
    private static void Pause(Voice voice)
    {
        if (voice.source != null && voice.started && !voice.paused) { voice.source.Pause(); voice.paused = true; }
    }
    private static void StopVoice(Voice voice)
    {
        if (voice.source != null)
        {
            if (voice.started) voice.source.Stop();
            voice.source.volume = 0;
        }
        voice.eventId = -1; voice.started = voice.paused = false;
    }
    public void Stop()
    {
        foreach (var voice in descents) StopVoice(voice);
        foreach (var voice in impacts) StopVoice(voice);
        StopVoice(fire); active.Remove(this);
    }
}
