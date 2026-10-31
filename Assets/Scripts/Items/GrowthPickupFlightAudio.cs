using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>One quiet homing bed and a bounded, rapid stream of particle-arrival ticks.</summary>
public sealed class GrowthPickupFlightAudio : MonoBehaviour
{
    private const int MaximumArrivalVoices = 4;
    private const float ArrivalInterval = .04f;
    private const float PendingArrivalWindow = .1f;
    private static AudioClip defaultHoming, defaultAbsorption;
    private GrowthCollectiblePickupEffect owner;
    private AudioSource flightSource;
    private readonly AudioSource[] arrivalSources = new AudioSource[MaximumArrivalVoices];
    private float flightVolume, arrivalVolume, voiceTail, audibleAge, lastCueAge = -1f, lastArrivalAge;
    private int arrivalCueCount, pendingArrivals, nextArrivalVoice;
    private bool flying, finishing, paused;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache() { defaultHoming = null; defaultAbsorption = null; }

    public static void Preload()
    {
        if (defaultHoming == null) defaultHoming = Resources.Load<AudioClip>("Audio/DataHomingLoop");
        if (defaultAbsorption == null) defaultAbsorption = Resources.Load<AudioClip>("Audio/DataAbsorption");
    }

    public static GrowthPickupFlightAudio Create(GrowthCollectiblePickupEffect effect,
        AudioClip homing, AudioClip absorption, float homingVolume, float absorptionVolume)
    {
        if (!Application.isPlaying || effect == null || GameSaveSystem.IsLoading ||
            (homingVolume <= 0 && absorptionVolume <= 0)) return null;
        var scene = effect.gameObject.scene;
        if (!scene.IsValid() || !scene.isLoaded) return null;
        Preload();
        if (homing == null) homing = defaultHoming;
        if (absorption == null) absorption = defaultAbsorption;
        if (homing == null && absorption == null) return null;
        // Detached from the visual so its last chime is not cut short by cleanup.
        var root = new GameObject("D.A.T.A Flight and Absorption Sound");
        SceneManager.MoveGameObjectToScene(root, scene);
        root.transform.position = effect.transform.position;
        root.AddComponent<CameraVisionStreamingExempt>();
        var sound = root.AddComponent<GrowthPickupFlightAudio>();
        sound.owner = effect;
        sound.flightVolume = Mathf.Clamp01(homingVolume);
        sound.arrivalVolume = Mathf.Clamp01(absorptionVolume);
        sound.flightSource = sound.CreateSource(homing, true);
        for (int i = 0; i < sound.arrivalSources.Length; i++)
        {
            sound.arrivalSources[i] = sound.CreateSource(absorption, false);
            sound.arrivalSources[i].volume = sound.arrivalVolume * .65f;
        }
        sound.flightSource.volume = 0;
        return sound;
    }

    private AudioSource CreateSource(AudioClip clip, bool loop)
    {
        var source = gameObject.AddComponent<AudioSource>();
        source.clip = clip; source.loop = loop; source.playOnAwake = false;
        source.spatialBlend = 0; source.dopplerLevel = 0;
        source.ignoreListenerPause = false; source.priority = 110;
        return source;
    }

    public void ReportFrame(bool isHoming, int arrivals, int remaining, Vector3 destination)
    {
        if (finishing) return;
        transform.position = destination;
        flying = isHoming && remaining > 0;
        if (arrivals > 0 && !GameSaveSystem.IsLoading)
        {
            // Briefly spread same-frame batches into a few audible ticks, without
            // building a seconds-long queue after the last particle has vanished.
            pendingArrivals = Mathf.Min(8, pendingArrivals + arrivals);
            lastArrivalAge = audibleAge;
            TryPlayArrival();
        }
        if (remaining == 0) { finishing = true; flying = false; }
    }

    // Cancellation discards queued ticks; already audible tails may finish.
    public void Finish() { finishing = true; flying = false; pendingArrivals = 0; }

    private void TryPlayArrival()
    {
        if (pendingArrivals <= 0 || Time.timeScale <= 0 || AudioListener.pause || GameSaveSystem.IsLoading) return;
        if (audibleAge - lastArrivalAge > PendingArrivalWindow) { pendingArrivals = 0; return; }
        if (arrivalVolume <= 0 || arrivalSources[0].clip == null) { pendingArrivals = 0; return; }
        if (audibleAge - lastCueAge < ArrivalInterval) return;
        // Explicit voice pool: even custom long clips cannot create unbounded
        // PlayOneShot overlaps, and an active note is never abruptly restarted.
        for (int i = 0; i < arrivalSources.Length; i++)
        {
            int index = (nextArrivalVoice + i) % arrivalSources.Length;
            AudioSource source = arrivalSources[index];
            if (source.isPlaying) continue;
            source.pitch = .96f + (arrivalCueCount * 3 % 7) * .025f;
            source.Play();
            voiceTail = Mathf.Max(voiceTail, source.clip.length / source.pitch + .08f);
            nextArrivalVoice = (index + 1) % arrivalSources.Length;
            pendingArrivals--;
            arrivalCueCount++;
            lastCueAge = audibleAge;
            return;
        }
    }

    private void Update()
    {
        if (flightSource == null || arrivalSources[0] == null) return;
        bool freeze = Time.timeScale <= 0 || AudioListener.pause || GameSaveSystem.IsLoading;
        if (freeze != paused)
        {
            paused = freeze;
            if (paused) flightSource.Pause(); else flightSource.UnPause();
            foreach (var source in arrivalSources)
                if (paused) source.Pause(); else source.UnPause();
        }
        if (paused) return;
        if (owner == null || !owner.isActiveAndEnabled) Finish();
        float dt = Time.unscaledDeltaTime;
        audibleAge += dt;
        voiceTail = Mathf.Max(0, voiceTail - dt);
        TryPlayArrival();
        float targetVolume = flying ? flightVolume : 0;
        // Short attack/release eliminates clicks when a target disappears or returns.
        flightSource.volume = Mathf.MoveTowards(flightSource.volume, targetVolume,
            Mathf.Max(.01f, flightVolume) * dt / (flying ? .08f : .12f));
        flightSource.pitch = Mathf.Lerp(.96f, 1.12f, Mathf.Clamp01(audibleAge / .9f));
        if (flightSource.volume > .001f && flightSource.clip != null)
        {
            if (!flightSource.isPlaying) flightSource.Play();
        }
        else if (flightSource.isPlaying) flightSource.Stop();
        if (finishing && pendingArrivals == 0 && flightSource.volume <= .001f && voiceTail <= 0) Destroy(gameObject);
    }
}
