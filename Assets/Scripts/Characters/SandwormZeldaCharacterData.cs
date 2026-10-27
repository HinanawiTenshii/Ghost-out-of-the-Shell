using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Shared character attributes for the articulated sandworm. Roaming and sprites
/// remain owned by SandwormBossPrototype and SandwormSegmentVisual respectively.
/// The prefab owns segment colliders; this component adds no mover or attack AI.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Characters/Sandworm Character Data")]
public sealed class SandwormZeldaCharacterData : ZeldaCharacterData
{
    private static readonly System.Collections.Generic.HashSet<SandwormZeldaCharacterData> activeSandworms =
        new System.Collections.Generic.HashSet<SandwormZeldaCharacterData>();
    public static System.Collections.Generic.IReadOnlyCollection<SandwormZeldaCharacterData> ActiveSandworms => activeSandworms;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetActiveSandworms() => activeSandworms.Clear();
    private void OnEnable() { if (!IsDead) activeSandworms.Add(this); }

    public bool HasVisibleBodyPart(Camera camera, CameraCircularVision vision, Plane[] frustum)
    {
        if (!isActiveAndEnabled || IsDead || camera == null || feedbackRenderers == null || SoulMarkRuntime.IsVisionCovered) return false;
        foreach (var renderer in feedbackRenderers)
        {
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                (camera.cullingMask & (1 << renderer.gameObject.layer)) == 0 ||
                !GeometryUtility.TestPlanesAABB(frustum, renderer.bounds)) continue;
            if (vision == null || !vision.isActiveAndEnabled || vision.IsWorldBoundsVisible(renderer.bounds, renderer.transform)) return true;
        }
        return false;
    }
    [Header("Sandworm Combat / 沙虫战斗属性")]
    [SerializeField, Min(0)] private int attackPower = 10;

    [Header("Death Sequence / 逐节死亡爆炸")]
    [SerializeField, Min(.03f), Tooltip("从尾到头相邻两节爆炸的间隔（秒）。")]
    private float deathSegmentInterval = .14f;
    [SerializeField, Min(.1f)] private float deathSegmentScale = .8f;
    [SerializeField, Min(1.1f), Tooltip("头部爆炸相对于最大身体爆炸的尺寸倍率。")]
    private float deathHeadScaleMultiplier = 2f;
    [SerializeField, Range(0f, .6f)] private float deathHeadShakeDuration = .28f;
    [SerializeField, Range(0f, .06f), Tooltip("头部最终爆炸震动幅度，相对于摄像机半高；仅爆炸可见时生效。")]
    private float deathHeadShakeStrength = .025f;

    private SandwormBossPrototype prototype;
    private SpriteRenderer[] feedbackRenderers;
    private Color[] feedbackBaseColors;
    private Material[] feedbackBaseMaterials;
    private Material feedbackMaterial;
    private bool feedbackApplied;
    private static readonly int ShakeOffsetId = Shader.PropertyToID("_WorldShakeOffset");
    protected override bool UsesSingleRendererDamageFeedback => false;
    private SandwormStateController stateController;
    private SandwormStateController States => stateController != null ? stateController :
        (stateController = GetComponent<SandwormStateController>());

    [System.Serializable]
    private struct SavedSandwormState { public int version; public SandwormState state; }

    public override int AttackPower => attackPower;
    public override Transform AttributeWindowAnchor
    {
        get
        {
            if (prototype == null) prototype = GetComponent<SandwormBossPrototype>();
            return prototype != null && prototype.Head != null ? prototype.Head : transform;
        }
    }
    public override bool ParticipatesInCharacterCollision => !IsDead && base.ParticipatesInCharacterCollision &&
        (States == null || !States.IsUnderground);

    public override RuntimeState CaptureRuntimeState()
    {
        RuntimeState saved = base.CaptureRuntimeState();
        if (States != null)
        {
            var attack = GetComponent<SandwormEruptionAttack>();
            // Attack paths/airborne poses are transient, not saved. Never restore
            // just the exposed stun state without the timeline that ends it.
            SandwormState savedState = attack != null && (attack.IsExplosiveRecovery || attack.IsRoarRecovery)
                ? SandwormState.Underground : States.State;
            saved.characterSpecificState = JsonUtility.ToJson(new SavedSandwormState { version = 1, state = savedState });
        }
        return saved;
    }

    public override void ApplyRuntimeState(RuntimeState saved)
    {
        RestoreWholeBodyFeedback();
        GetComponent<SandwormEruptionAttack>()?.CancelAttack();
        base.ApplyRuntimeState(saved);
        // Old/manual snapshots can contain an active root with zero health.
        // Such a restored corpse has no pending death/reward transaction and
        // must not permanently block every future automatic checkpoint.
        if (!IsDead && isActiveAndEnabled) activeSandworms.Add(this);
        else activeSandworms.Remove(this);
        if (States == null || string.IsNullOrEmpty(saved.characterSpecificState)) return;
        SavedSandwormState extra = JsonUtility.FromJson<SavedSandwormState>(saved.characterSpecificState);
        if (extra.version == 1) States.SetState(extra.state);
    }

    private void Reset()
    {
        health = 60;
        attackPower = 10;
        skillValue = 0;
        possessionEnergy = 5;
        possessionCost = 6;
        moveSpeed = 3f;
    }

    protected override void Awake()
    {
        base.Awake();
        prototype = GetComponent<SandwormBossPrototype>();
        var parts = GetComponentsInChildren<SandwormSegmentVisual>(true);
        feedbackRenderers = new SpriteRenderer[parts.Length];
        feedbackBaseColors = new Color[parts.Length];
        feedbackBaseMaterials = new Material[parts.Length];
        for (int i = 0; i < parts.Length; i++)
            feedbackRenderers[i] = parts[i].GetComponent<SpriteRenderer>();
    }

    private void LateUpdate()
    {
        ApplyWholeBodyFeedback();
    }

    private void ApplyWholeBodyFeedback()
    {
        if (!IsShowingDamageFeedback || IsDead)
        {
            RestoreWholeBodyFeedback();
            return;
        }
        if (feedbackRenderers == null) return;
        if (!feedbackApplied)
        {
            if (feedbackMaterial == null)
            {
                var shader = Resources.Load<Shader>("SandwormDamageFeedback");
                if (shader != null)
                    feedbackMaterial = new Material(shader) {
                        name = "Sandworm Whole Body Hit Feedback", hideFlags = HideFlags.HideAndDontSave
                    };
            }
            for (int i = 0; i < feedbackRenderers.Length; i++)
            {
                var renderer = feedbackRenderers[i];
                if (renderer == null) continue;
                feedbackBaseColors[i] = renderer.color;
                feedbackBaseMaterials[i] = renderer.sharedMaterial;
                if (feedbackMaterial != null) renderer.sharedMaterial = feedbackMaterial;
            }
            feedbackApplied = true;
        }
        // One shared world-space displacement keeps all joints connected, even
        // while their local rotations differ. Never move root/segment transforms,
        // colliders, navigation history, or the eruption/fall animation pose.
        float scale = Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y));
        if (feedbackMaterial != null)
            feedbackMaterial.SetVector(ShakeOffsetId, GetDamageShakeOffset() * scale);
        for (int i = 0; i < feedbackRenderers.Length; i++)
            if (feedbackRenderers[i] != null)
                feedbackRenderers[i].color = GetDamageFeedbackTint(feedbackBaseColors[i]);
    }

    private void RestoreWholeBodyFeedback()
    {
        if (!feedbackApplied || feedbackRenderers == null) return;
        for (int i = 0; i < feedbackRenderers.Length; i++)
        {
            var renderer = feedbackRenderers[i];
            if (renderer == null) continue;
            renderer.color = feedbackBaseColors[i];
            renderer.sharedMaterial = feedbackBaseMaterials[i];
        }
        if (feedbackMaterial != null) feedbackMaterial.SetVector(ShakeOffsetId, Vector4.zero);
        feedbackApplied = false;
    }

    public override void ApplyCharacterVisual(SpriteRenderer renderer, Vector2 facing, bool moving, bool attacking)
    {
        // No humanoid sprite replacement or idle green tint on the authored parts.
        if (Application.isPlaying) ApplyWholeBodyFeedback();
    }

    protected override void Die()
    {
        if (IsDead) return;
        // Attack cleanup restores the pre-flight pose. Capture the actual head
        // before base.Die disables/destroys the body, not the stationary root.
        Vector3 dropPosition = AttributeWindowAnchor.position;
        Scene dropScene = gameObject.scene;
        RestoreWholeBodyFeedback();
        if (prototype != null) prototype.SetMovementEnabled(false);
        base.Die(); // The one root owns the whole body and one shared health pool.
        if (IsDead) DropTrialProof(dropPosition, dropScene);
    }

    private void DropTrialProof(Vector3 position, Scene scene)
    {
        var prefab = Resources.Load<TrialProofPickupItem>("PickupItems/TrialProofPickupItem");
        if (prefab == null)
        {
            Debug.LogError("Sandworm death reward prefab is missing: PickupItems/TrialProofPickupItem", this);
            return;
        }
        // Award at confirmed death rather than the end of the cosmetic sequence:
        // saving/traveling during the explosions must not lose the reward.
        var proof = Instantiate(prefab, position, Quaternion.identity);
        SceneManager.MoveGameObjectToScene(proof.gameObject, scene);
        string identity = TrialProofPickupItem.SandwormRewardIdentityPrefix + System.Guid.NewGuid().ToString("N");
        proof.SetUniqueInstanceId(identity);
        proof.gameObject.AddComponent<SceneTravelStableId>().Initialize(identity);
        proof.MarkAsDropped();
        // Death, stable reward identity and dropped-item registration have all
        // committed. This queues a safe save; it never captures a damage callback.
        GameSaveSystem.RequestAutoSave("sandworm_defeated_reward_spawned", true);
    }

    protected override void PlayDeathSound()
    {
        var audio = GetComponent<SandwormAudio>();
        // Per-part death sounds replace the ordinary one-shot at the stationary root.
        if (audio == null || !audio.HasDeathAudio) base.PlayDeathSound();
    }

    protected override bool TryHandleDeathRemains()
    {
        if (prototype == null) prototype = GetComponent<SandwormBossPrototype>();
        if (!SandwormDeathSequence.TryCreate(prototype, deathSegmentInterval,
            deathExplosionDuration, deathExplosionScale * deathSegmentScale,
            deathHeadScaleMultiplier, deathExplosionTint,
            deathHeadShakeDuration, deathHeadShakeStrength)) return false;

        // Snapshot first: disabling the attack restores its pre-flight pose.
        // Only inert visual copies survive; no colliders, attacks, camera framing,
        // character/save state or AI remain active during the death sequence.
        gameObject.SetActive(false);
        Destroy(gameObject);
        return true;
    }

    private void OnDisable()
    {
        activeSandworms.Remove(this);
        RestoreWholeBodyFeedback();
    }

    private void OnDestroy()
    {
        activeSandworms.Remove(this);
        RestoreWholeBodyFeedback();
        if (feedbackMaterial != null) Destroy(feedbackMaterial);
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        attackPower = Mathf.Max(0, attackPower);
        deathSegmentInterval = Mathf.Max(.03f, deathSegmentInterval);
        deathSegmentScale = Mathf.Max(.1f, deathSegmentScale);
        deathHeadScaleMultiplier = Mathf.Max(1.1f, deathHeadScaleMultiplier);
        deathHeadShakeDuration = Mathf.Clamp(deathHeadShakeDuration, 0f, .6f);
        deathHeadShakeStrength = Mathf.Clamp(deathHeadShakeStrength, 0f, .06f);
    }
}
