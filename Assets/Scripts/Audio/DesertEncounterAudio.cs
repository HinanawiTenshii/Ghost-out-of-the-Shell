using UnityEngine;

/// <summary>
/// Scene-local desert ambience and latched sandworm encounter music. Discovery is
/// shared with the boss HUD: no second visibility query or scene-wide Update scan.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Audio/Desert Encounter Audio")]
public sealed class DesertEncounterAudio : MonoBehaviour
{
    [Header("Looping clips / 循环音源")]
    [SerializeField] private AudioClip sandstormClip;
    [SerializeField] private AudioClip bossMusicClip;
    [Header("Mix / 低音量背景混音")]
    [SerializeField, Range(0f, 1f)] private float explorationWindVolume = .12f;
    [SerializeField, Range(0f, 1f)] private float battleWindVolume = .035f;
    [SerializeField, Range(0f, 1f)] private float bossMusicVolume = .095f;
    [SerializeField, Min(.05f)] private float crossfadeDuration = 1.5f;
    [SerializeField, Min(.05f)] private float sceneFadeInDuration = .8f;

    private AudioSource windSource, bossSource;
    private SandwormZeldaCharacterData encounterTarget;
    private float battleBlend, entranceBlend;
    private bool suspended;

    public AudioSource WindSource => windSource;
    public AudioSource BossSource => bossSource;
    public bool IsInEncounter => encounterTarget != null && !encounterTarget.IsDead && encounterTarget.CurrentHealth > 0;

    private void Awake()
    {
        windSource = CreateSource(sandstormClip, 190);
        bossSource = CreateSource(bossMusicClip, 160);
    }

    private AudioSource CreateSource(AudioClip clip, int priority)
    {
        var source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.clip = clip;
        source.loop = true;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
        source.volume = 0f;
        source.priority = priority;
        return source;
    }

    private void OnEnable()
    {
        suspended = false;
        RuntimeSandwormBossBar.EncounterStarted += BeginEncounter;
    }

    private void Start()
    {
        // Supports enabling this service after the HUD already discovered a boss.
        // This is a one-time lookup, never performed during regular playback.
        foreach (var bar in FindObjectsOfType<RuntimeSandwormBossBar>())
            if (bar.IsEncounterVisible) BeginEncounter(bar.Target);
    }

    private void BeginEncounter(SandwormZeldaCharacterData target)
    {
        if (suspended || target == null || target.IsDead || target.CurrentHealth <= 0 ||
            ZeldaRuntimeRegistry.GetGameplayScene(target.gameObject) != gameObject.scene) return;
        // Do not reset the audio cursor when the HUD is rebuilt or re-enabled.
        encounterTarget = target;
    }

    private void Update()
    {
        if (suspended || windSource == null || bossSource == null) return;
        // A restored boss may be removed/dead. Wait for final saved-world state,
        // rather than playing against the authored prefab's temporary full HP.
        if (GameSaveSystem.IsLoading || CharacterPotionBuffs.IsRestoringWorld ||
            RetroSceneLoadReveal.IsPreparingGameplay) return;

        bool battle = IsInEncounter;
        if (encounterTarget != null && ZeldaRuntimeRegistry.GetGameplayScene(encounterTarget.gameObject) != gameObject.scene)
        { encounterTarget = null; battle = false; }
        float dt = Time.unscaledDeltaTime;
        entranceBlend = Mathf.MoveTowards(entranceBlend, 1f, dt / Mathf.Max(.05f, sceneFadeInDuration));
        battleBlend = Mathf.MoveTowards(battleBlend, battle ? 1f : 0f, dt / Mathf.Max(.05f, crossfadeDuration));
        float fade = Mathf.SmoothStep(0f, 1f, battleBlend);
        float entrance = Mathf.SmoothStep(0f, 1f, entranceBlend);
        windSource.volume = Mathf.Lerp(explorationWindVolume, battleWindVolume, fade) * entrance;
        bossSource.volume = bossMusicVolume * fade * entrance;

        if (windSource.clip != null && !windSource.isPlaying) windSource.Play();
        if (battle && bossSource.clip != null && !bossSource.isPlaying) bossSource.Play();
        if (!battle && battleBlend <= 0f && bossSource.isPlaying) bossSource.Stop();
        // Inactive due to distance streaming is deliberately NOT an encounter end.
    }

    public void StopForSceneTransition()
    {
        suspended = true;
        if (windSource != null) { windSource.volume = 0f; windSource.Stop(); }
        if (bossSource != null) { bossSource.volume = 0f; bossSource.Stop(); }
    }

    private void OnDisable()
    {
        RuntimeSandwormBossBar.EncounterStarted -= BeginEncounter;
        StopForSceneTransition();
    }
}
