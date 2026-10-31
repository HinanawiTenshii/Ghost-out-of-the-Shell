using UnityEngine;

public partial class HeroZeldaCharacterData
{
    [Header("Directional Shield / 面向格挡")]
    [SerializeField] private bool enableDirectionalShield = true;
    [Tooltip("完整防护夹角；90 表示朝向左右各 45 度。仅持盾版本生效。")]
    [SerializeField, Range(1f, 180f)] private float shieldBlockAngle = 90f;
    [SerializeField] private bool blockMelee = true;
    [SerializeField] private bool blockProjectiles = true;
    [SerializeField] private bool blockExplosions = true;
    [SerializeField] private AudioClip shieldBlockSound;
    [SerializeField, Range(0f, 1f)] private float shieldBlockVolume = .5f;

    private const float ShieldFeedbackDuration = .18f;
    private ZeldaFourWayMover shieldMover;
    private ZeldaCharacterAiBase shieldAi;
    private Collider2D shieldBodyCollider;
    private AudioSource shieldAudioSource;
    private static AudioClip defaultShieldBlockSound;
    private float shieldFeedbackUntil;
    private float nextShieldSoundTime;
    private Sprite[,,] shieldFeedbackFrames;
    private bool shieldFeedbackDirty;
    private bool buildingShieldFeedback;
    private int shieldFeedbackPhase;
    public int ShieldBlockVersion { get; private set; }
    public float ShieldBlockAngle => shieldBlockAngle;
    public bool IsShowingShieldBlockFeedback => HasShield && !IsDead && !IsGhostLike && Time.time < shieldFeedbackUntil;

    protected override void Awake()
    {
        base.Awake();
        shieldMover = GetComponent<ZeldaFourWayMover>();
        shieldAi = GetComponent<ZeldaCharacterAiBase>();
        shieldBodyCollider = GetComponent<Collider2D>();
        if (Application.isPlaying && HasShield && defaultShieldBlockSound == null)
            defaultShieldBlockSound = Resources.Load<AudioClip>("Audio/HeroShieldBlock");
    }

    protected override bool TryBlockDirectionalDamage(DirectionalDamageContext impact)
    {
        if (!enableDirectionalShield || !HasShield || IsDead || IsGhostLike ||
            !impact.TryGetSourceDirection(transform.position, out Vector2 from)) return false;
        switch (impact.Kind)
        {
            case DirectionalDamageKind.Melee: if (!blockMelee) return false; break;
            case DirectionalDamageKind.Projectile: if (!blockProjectiles) return false; break;
            case DirectionalDamageKind.Explosion:
                if (!blockExplosions) return false;
                // A blast originating inside the body's footprint cannot be screened
                // by a forward-facing shield. No extra physics object/query is needed.
                if (shieldBodyCollider != null && shieldBodyCollider.enabled &&
                    shieldBodyCollider.OverlapPoint(impact.Vector)) return false;
                break;
            default: return false;
        }
        Vector2 facing = shieldMover != null && shieldMover.isActiveAndEnabled ? shieldMover.FacingDirection :
            shieldAi != null ? shieldAi.FacingDirection : shieldMover != null ? shieldMover.FacingDirection : Vector2.down;
        // Use exactly the same four-way selection as the displayed body/shield.
        facing = Mathf.Abs(facing.x) > Mathf.Abs(facing.y) ? (facing.x < 0 ? Vector2.left : Vector2.right) :
            (facing.y > 0 ? Vector2.up : Vector2.down);
        float threshold = Mathf.Cos(Mathf.Clamp(shieldBlockAngle, 1f, 180f) * .5f * Mathf.Deg2Rad);
        if (Vector2.Dot(facing, from) + .00001f < threshold) return false;
        ShieldBlockVersion++;
        shieldFeedbackUntil = Time.time + ShieldFeedbackDuration;
        PlayShieldBlockSound();
        return true;
    }

    public override void ApplyCharacterVisual(SpriteRenderer renderer, Vector2 facing, bool moving, bool attacking)
    {
        base.ApplyCharacterVisual(renderer, facing, moving, attacking);
        if (ApplyPickupPresentation(renderer)) return;
        if (shieldFeedbackDirty) { ReleaseShieldFeedbackFrames(); shieldFeedbackDirty = false; }
        if (renderer == null || !IsShowingShieldBlockFeedback) return;
        int direction = Mathf.Abs(facing.x) > Mathf.Abs(facing.y) ? (facing.x < 0 ? 2 : 3) : (facing.y > 0 ? 1 : 0);
        int pose = GetVisualPose(moving, attacking);
        int phase = shieldFeedbackUntil - Time.time > ShieldFeedbackDuration * .55f ? 0 : 1;
        if (shieldFeedbackFrames == null) shieldFeedbackFrames = new Sprite[4, 4, 2];
        if (shieldFeedbackFrames[direction, pose, phase] == null)
        {
            buildingShieldFeedback = true; shieldFeedbackPhase = phase;
            try { shieldFeedbackFrames[direction, pose, phase] = CreateSprite(direction, pose); }
            finally { buildingShieldFeedback = false; }
        }
        renderer.sprite = shieldFeedbackFrames[direction, pose, phase];
    }

    private void PlayShieldBlockSound()
    {
        // Throttle only overlapping sound, never damage protection or visual feedback.
        if (!Application.isPlaying || shieldBlockVolume <= 0 || Time.time < nextShieldSoundTime) return;
        AudioClip clip = shieldBlockSound != null ? shieldBlockSound : defaultShieldBlockSound;
        if (clip == null) return;
        var player = ZeldaRuntimeRegistry.GetControlledMover();
        var camera = player == null ? Camera.main : null;
        Vector2 listener = player != null ? (Vector2)player.transform.position :
            camera != null ? (Vector2)camera.transform.position : (Vector2)transform.position;
        float gain = Mathf.Clamp01(1f - Mathf.Max(0f, Vector2.Distance(listener, transform.position) - 2f) / 18f);
        if (gain <= 0) return;
        if (shieldAudioSource == null)
        {
            shieldAudioSource = gameObject.AddComponent<AudioSource>();
            shieldAudioSource.playOnAwake = false; shieldAudioSource.loop = false;
            shieldAudioSource.spatialBlend = 0; shieldAudioSource.dopplerLevel = 0;
        }
        shieldAudioSource.PlayOneShot(clip, shieldBlockVolume * gain);
        nextShieldSoundTime = Time.time + .045f;
    }

    public override void ApplyRuntimeState(RuntimeState state)
    {
        base.ApplyRuntimeState(state);
        EndPickupPresentation();
        ResetShieldFeedback(); // Cosmetic feedback is not carried across a load/travel.
    }
    private void OnDisable() { EndPickupPresentation(); ResetShieldFeedback(); }
    private void ResetShieldFeedback()
    {
        shieldFeedbackUntil = nextShieldSoundTime = 0;
        ShieldBlockVersion = 0;
        if (shieldAudioSource != null) shieldAudioSource.Stop();
    }
    protected override void OnValidate()
    {
        base.OnValidate();
        shieldBlockAngle = Mathf.Clamp(shieldBlockAngle, 1f, 180f);
        shieldFeedbackDirty = true;
        pickupPosesDirty = true;
    }
    protected override void OnDestroy()
    {
        ReleaseShieldFeedbackFrames();
        EndPickupPresentation();
        ReleasePickupPoses();
        base.OnDestroy();
    }
    private void ReleaseShieldFeedbackFrames()
    {
        if (shieldFeedbackFrames == null) return;
        foreach (Sprite sprite in shieldFeedbackFrames)
        {
            if (sprite == null) continue;
            Texture2D texture = sprite.texture;
            if (Application.isPlaying) { Destroy(sprite); Destroy(texture); }
            else { DestroyImmediate(sprite); DestroyImmediate(texture); }
        }
        shieldFeedbackFrames = null;
    }
}
