using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Prebaked granular/retro audio. Three bounded loops; discrete sounds follow actual attack events.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(SandwormBossPrototype), typeof(SandwormEruptionAttack))]
[DefaultExecutionOrder(-215)]
public sealed class SandwormAudio : MonoBehaviour
{
    [Header("Sandworm Audio / 沙虫音效")]
    [SerializeField, Range(0f, 1f)] private float movementVolume = .28f;
    [SerializeField, Range(0f, 1f)] private float breachVolume = .8f;
    [SerializeField, Range(0f, 1f)] private float emergenceVolume = .4f;
    [SerializeField, Range(0f, 1f)] private float burrowVolume = .6f;
    [SerializeField, Range(0f, 1f)] private float blastFallVolume = .85f;
    [SerializeField, Range(0f, 1f)] private float impactVolume = .75f;
    [SerializeField, Range(0f, 1f)] private float deathSegmentVolume = .62f;
    [SerializeField, Range(0f, 1f)] private float deathHeadVolume = .9f;
    [SerializeField, Min(0f), Tooltip("玩家距离沙虫最近身体部位在此范围内时，移动声保持完整音量（世界单位）。")]
    private float fullVolumeDistance = 5f;
    [SerializeField, Min(1f), Tooltip("移动声随距离平滑衰减，到达此距离后静音；不受摄像机高度影响。")]
    private float audibleDistance = 36f;
    private static readonly string[] Names = { "MovementLoop", "EmergenceLoop", "BurrowLoop", "Breach", "BurrowImpact", "BlastFall", "GroundImpact", "DeathSegment", "DeathHead" };
    private static readonly AudioClip[] clips = new AudioClip[9];
    private readonly AudioSource[] loops = new AudioSource[3];
    private readonly float[] blends = new float[3];
    private SandwormBossPrototype movement;
    private SandwormEruptionAttack attack;
    private SandwormZeldaCharacterData data;
    private SandwormStateController state;
    private Transform[] parts;
    private Vector2 previousHead;
    private bool paused;
    private int recoveryCount;
    public float MovementGain => loops[0] != null ? loops[0].volume : 0f;
    public float EmergenceGain => loops[1] != null ? loops[1].volume : 0f;
    public float BurrowGain => loops[2] != null ? loops[2].volume : 0f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetClips() { System.Array.Clear(clips, 0, clips.Length); }
    private void OnValidate()
    {
        fullVolumeDistance = Mathf.Max(0f, fullVolumeDistance);
        audibleDistance = Mathf.Max(fullVolumeDistance + 1f, audibleDistance);
    }
    private void Awake()
    {
        movement = GetComponent<SandwormBossPrototype>(); attack = GetComponent<SandwormEruptionAttack>();
        data = GetComponent<SandwormZeldaCharacterData>(); state = GetComponent<SandwormStateController>();
        parts = new Transform[movement.BodySegments.Length + 2]; parts[0] = movement.Head;
        System.Array.Copy(movement.BodySegments, 0, parts, 1, movement.BodySegments.Length); parts[parts.Length - 1] = movement.Tail;
        for (int i = 0; i < clips.Length; i++)
            if (clips[i] == null) clips[i] = Resources.Load<AudioClip>("Audio/Sandworm/" + Names[i]);
        for (int i = 0; i < loops.Length; i++)
        {
            var source = gameObject.AddComponent<AudioSource>();
            Configure(source); source.clip = clips[i]; source.loop = true; source.priority = 170;
            loops[i] = source;
        }
    }
    internal static void Configure(AudioSource source)
    {
        source.playOnAwake = false; source.spatialBlend = 0f; source.dopplerLevel = 0f;
        source.volume = 0f; source.ignoreListenerPause = false;
    }
    private void OnEnable()
    {
        previousHead = movement.HeadPosition; recoveryCount = attack.RecoveryBurialPulses;
        attack.SegmentPulse += OnSegmentPulse;
    }
    private void OnDisable()
    {
        if (attack != null) attack.SegmentPulse -= OnSegmentPulse;
        StopLoops(); paused = false;
    }
    private void LateUpdate() => AdvanceAudio(Time.deltaTime);

    public void AdvanceAudio(float deltaTime)
    {
        bool shouldPause = Time.timeScale <= 0f || GameSaveSystem.IsLoading;
        if (paused != shouldPause)
        {
            paused = shouldPause;
            foreach (var source in loops) { if (paused) source.Pause(); else source.UnPause(); }
        }
        if (shouldPause) { previousHead = movement.HeadPosition; return; }
        bool moved = (movement.HeadPosition - previousHead).sqrMagnitude > .00000001f;
        previousHead = movement.HeadPosition;
        if (data == null || data.IsDead || !movement.isActiveAndEnabled || !movement.MovementEnabled ||
            !TryGetEar(gameObject.scene, out Vector2 ear)) { StopLoops(); return; }
        var phase = attack.Phase;
        bool travel = state.IsUnderground && (phase == SandwormEruptionAttack.AttackPhase.Roaming ||
            phase == SandwormEruptionAttack.AttackPhase.Approaching || phase == SandwormEruptionAttack.AttackPhase.Settling);
        bool emerging = phase == SandwormEruptionAttack.AttackPhase.Traversing && attack.EmergencePulses > 0 && attack.EmergencePulses < parts.Length;
        bool landing = phase == SandwormEruptionAttack.AttackPhase.Traversing && attack.LandingPulses > 0 && attack.LandingPulses < parts.Length;
        bool recovering = phase == SandwormEruptionAttack.AttackPhase.ReturningToBurrow && attack.RecoveryBurialPulses > 0 && attack.RecoveryBurialPulses < parts.Length;
        if (attack.RecoveryBurialPulses > recoveryCount && recoveryCount == 0)
            PlayBurst(4, attack.LandingPoint, burrowVolume * .5f);
        recoveryCount = attack.RecoveryBurialPulses;
        Vector2 nearest = parts[0].position; float best = float.PositiveInfinity;
        foreach (var part in parts)
        {
            if (part == null) continue;
            float d = ((Vector2)part.position - ear).sqrMagnitude;
            if (d < best) { best = d; nearest = part.position; }
        }
        UpdateLoop(0, travel && moved, nearest, ear, movementVolume, deltaTime);
        UpdateLoop(1, emerging, attack.LockedAttackPoint, ear, emergenceVolume, deltaTime);
        UpdateLoop(2, landing || recovering, attack.LandingPoint, ear, burrowVolume * (recovering ? .3f : .55f), deltaTime);
    }
    private void UpdateLoop(int index, bool active, Vector2 origin, Vector2 ear, float volume, float dt)
    {
        blends[index] = Mathf.MoveTowards(blends[index], active ? 1f : 0f, Mathf.Max(0f, dt) / .1f);
        var source = loops[index];
        source.volume = volume * blends[index] * ElectricGateAudio.DistanceGain(Vector2.Distance(origin, ear), fullVolumeDistance, audibleDistance);
        source.panStereo = Mathf.Clamp((origin.x - ear.x) / audibleDistance, -.65f, .65f);
        if (source.volume > .0001f && source.clip != null) { if (!source.isPlaying) source.Play(); }
        else if (source.isPlaying) source.Stop();
    }
    private void StopLoops()
    {
        for (int i = 0; i < loops.Length; i++)
        { if (loops[i] != null) { loops[i].Stop(); loops[i].volume = 0f; } blends[i] = 0f; }
    }
    private void OnSegmentPulse(Vector2 point, int power, int index, bool landing)
    {
        // Do not layer eight full explosions: later sections use the shared grit loop.
        if (index == 0) PlayBurst(landing ? 4 : 3, point, landing ? burrowVolume : breachVolume);
    }
    public bool PlayBlastFall(Vector2 point)
    {
        if (!isActiveAndEnabled || clips[5] == null) return false;
        PlayBurst(5, point, blastFallVolume); return true;
    }
    public void PlayGroundImpact(Vector2 point) => PlayBurst(6, point, impactVolume);
    // A value snapshot, not a component reference: the living root is destroyed
    // before the remaining tail-to-head explosions are emitted.
    internal struct DeathPlayback
    {
        public AudioClip Segment, Head;
        public float SegmentVolume, HeadVolume, Near, Far;
        public void Play(Scene scene, Vector2 point, bool head)
        {
            PlayDetached(scene, head ? Head : Segment, head ? "DeathHead" : "DeathSegment",
                point, head ? HeadVolume : SegmentVolume, Near, Far);
        }
    }
    internal DeathPlayback CaptureDeathPlayback() => new DeathPlayback {
        Segment = clips[7], Head = clips[8], SegmentVolume = deathSegmentVolume,
        HeadVolume = deathHeadVolume, Near = fullVolumeDistance, Far = audibleDistance
    };
    public bool HasDeathAudio => isActiveAndEnabled && clips[7] != null && clips[8] != null;
    private void PlayBurst(int clipIndex, Vector2 point, float volume)
    {
        if (!isActiveAndEnabled) return;
        PlayDetached(gameObject.scene, clips[clipIndex], Names[clipIndex], point, volume, fullVolumeDistance, audibleDistance);
    }
    private static void PlayDetached(Scene scene, AudioClip clip, string name, Vector2 point, float volume, float near, float far)
    {
        if (Time.timeScale <= 0f || GameSaveSystem.IsLoading || clip == null || volume <= 0f ||
            !TryGetEar(scene, out Vector2 ear) || Vector2.Distance(point, ear) >= far) return;
        // Detached tails survive a lethal swallowed bomb, but not leaving the scene.
        var root = new GameObject("Sandworm Audio " + name);
        SceneManager.MoveGameObjectToScene(root, scene); root.transform.position = point;
        root.AddComponent<CameraVisionStreamingExempt>();
        root.AddComponent<SandwormAudioTail>().Initialize(clip, volume, near, far);
    }
    internal static bool TryGetEar(Scene scene, out Vector2 point)
    {
        point = Vector2.zero;
        if (SceneManager.GetActiveScene() != scene) return false;
        var player = ZeldaRuntimeRegistry.GetControlledMover();
        if (player != null && ZeldaRuntimeRegistry.GetGameplayScene(player.gameObject) == scene)
        { point = player.transform.position; return true; }
        var camera = Camera.main; if (camera == null) return false;
        point = camera.transform.position; return true;
    }
}

/// <summary>Short, scene-owned one-shot with pause-safe lifetime and 2D distance attenuation.</summary>
public sealed class SandwormAudioTail : MonoBehaviour
{
    private AudioSource source;
    private float volume, near, far, remaining;
    private bool paused;
    public void Initialize(AudioClip clip, float gain, float minimum, float maximum)
    {
        volume = gain; near = minimum; far = maximum; remaining = clip.length + .08f;
        source = gameObject.AddComponent<AudioSource>(); SandwormAudio.Configure(source);
        source.clip = clip; source.priority = 90;
        UpdateGain(); source.Play();
    }
    private void UpdateGain()
    {
        if (!SandwormAudio.TryGetEar(gameObject.scene, out Vector2 ear)) { Destroy(gameObject); return; }
        Vector2 point = transform.position;
        source.volume = volume * ElectricGateAudio.DistanceGain(Vector2.Distance(point, ear), near, far);
        source.panStereo = Mathf.Clamp((point.x - ear.x) / far, -.65f, .65f);
    }
    private void Update()
    {
        if (source == null) return;
        UpdateGain();
        bool freeze = Time.timeScale <= 0f || GameSaveSystem.IsLoading;
        if (freeze != paused) { paused = freeze; if (freeze) source.Pause(); else source.UnPause(); }
        if (!freeze) { remaining -= Time.deltaTime; if (remaining <= 0f) Destroy(gameObject); }
    }
}
