using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>One distance-attenuated voice follows the saved ritual clock, never a free-running coroutine.</summary>
public sealed class SummoningCircleAudio
{
    private static AudioClip gathering, eruption, dispersal;
    private readonly GameObject owner;
    private AudioSource source;
    private SummoningCircle.RitualStage playingStage;
    private bool started, paused;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { gathering = eruption = dispersal = null; }
    public SummoningCircleAudio(GameObject owner)
    {
        this.owner = owner;
        if (gathering == null) gathering = Resources.Load<AudioClip>("Audio/SummoningGather");
        if (eruption == null) eruption = Resources.Load<AudioClip>("Audio/SummoningEruption");
        if (dispersal == null) dispersal = Resources.Load<AudioClip>("Audio/SummoningBurst");
    }
    public void Render(SummoningCircle circle, float volume)
    {
        if (owner == null || !circle.IsRitualActive || !circle.isActiveAndEnabled) { Stop(); return; }
        if (circle.IsRitualFrozen || AudioListener.pause)
        {
            if (source != null && started && !paused) { source.Pause(); paused = true; }
            return;
        }
        var clip = circle.Stage == SummoningCircle.RitualStage.Gathering ? gathering :
            circle.Stage == SummoningCircle.RitualStage.Erupting ? eruption : dispersal;
        if (clip == null || !TryGetEar(out Vector2 ear)) { Stop(); return; }
        Vector2 offset = (Vector2)owner.transform.position - ear;
        float gain = ElectricGateAudio.DistanceGain(offset.magnitude, 2f, 26f);
        if (gain <= 0f || volume <= 0f) { Stop(); return; }
        if (source == null)
        {
            var child = new GameObject("Summoning Ritual Audio") { hideFlags = HideFlags.HideAndDontSave };
            child.transform.SetParent(owner.transform, false);
            source = child.AddComponent<AudioSource>();
            source.playOnAwake = false; source.loop = false; source.spatialBlend = 0f;
            source.dopplerLevel = 0f; source.priority = 115; source.ignoreListenerPause = false;
        }
        float rate = clip.length / circle.StageDuration;
        float age = Mathf.Min(clip.length - 1f / clip.frequency, circle.StageElapsed * rate);
        int sample = Mathf.Clamp(Mathf.RoundToInt(age * clip.frequency), 0, clip.samples - 1);
        source.volume = Mathf.Clamp01(volume) * gain;
        source.panStereo = Mathf.Clamp(offset.x / 26f, -.55f, .55f);
        source.pitch = Mathf.Clamp(rate * Time.timeScale, .01f, 3f);
        if (!started || playingStage != circle.Stage)
        {
            source.Stop(); source.clip = clip; source.timeSamples = sample; source.Play();
            started = true; paused = false; playingStage = circle.Stage;
        }
        else if (paused)
        {
            source.timeSamples = sample; source.UnPause(); paused = false;
        }
        else if (Mathf.Abs(source.time - age) > .1f) source.timeSamples = sample;
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
    public void Stop()
    {
        if (source != null && (started || paused)) { source.Stop(); source.volume = 0; }
        started = paused = false;
    }
}
