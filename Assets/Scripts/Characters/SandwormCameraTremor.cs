using UnityEngine;

/// <summary>Visible ground movement rumbles; airborne/falling/stunned phases are silent.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SandwormBossPrototype), typeof(SandwormEruptionAttack))]
[DefaultExecutionOrder(-225)] // Attack (-250), tremor, camera follow (-200), vision (-100).
[AddComponentMenu("Characters/Sandworm Camera Tremor")]
public sealed class SandwormCameraTremor : CameraEnvironmentalShakeSource
{
    [Header("Earthquake / 地震（幅度相对于摄像机半高）")]
    [SerializeField, Range(0f,.03f), Tooltip("可见且贴地移动时的持续震动。")]
    private float movementStrength = .009f;
    [SerializeField, Range(0f,.06f), Tooltip("头部出土和入土瞬间的较强震动；后续部位较弱。")]
    private float impactStrength = .028f;
    [SerializeField, Range(.1f,.6f)] private float impactDuration = .26f;
    [SerializeField, Range(3f,25f)] private float movementFrequency = 9f;
    [SerializeField, Range(3f,30f)] private float impactFrequency = 16f;

    private SandwormBossPrototype movement;
    private SandwormEruptionAttack attack;
    private SandwormZeldaCharacterData character;
    private SpriteRenderer[] segments;
    private Vector3[] previousPositions;
    private int emergenceCount, landingCount, recoveryCount;
    private float clock, movingWeight, impactRemaining, impactWeight;
    public float CurrentStrength => Mathf.Max(movingWeight*movementStrength,
        impactStrength*impactWeight*Mathf.Clamp01(impactRemaining/impactDuration));

    protected override void OnEnable()
    {
        movement = GetComponent<SandwormBossPrototype>();
        attack = GetComponent<SandwormEruptionAttack>();
        character = GetComponent<SandwormZeldaCharacterData>();
        var visuals = GetComponentsInChildren<SandwormSegmentVisual>(true);
        segments = new SpriteRenderer[visuals.Length]; previousPositions = new Vector3[visuals.Length];
        for (int i=0;i<visuals.Length;i++)
        { segments[i]=visuals[i].GetComponent<SpriteRenderer>(); previousPositions[i]=visuals[i].transform.position; }
        clock = movingWeight = impactRemaining = impactWeight = 0f;
        ReadPulseCounts();
        base.OnEnable();
    }

    protected override void OnDisable()
    {
        movingWeight = impactRemaining = impactWeight = 0f;
        base.OnDisable();
    }

    private void LateUpdate() { AdvanceTremor(Time.deltaTime); }

    private bool CanRumble()
    {
        if (movement == null || !movement.isActiveAndEnabled || !movement.MovementEnabled ||
            attack == null || attack.IsRoarRecovery || character == null || character.IsDead) return false;
        switch (attack.Phase)
        {
            case SandwormEruptionAttack.AttackPhase.Falling:
            case SandwormEruptionAttack.AttackPhase.Stunned: return false;
            case SandwormEruptionAttack.AttackPhase.Traversing:
                // No lingering impact tail once EVERY section is off the ground.
                return attack.EmergencePulses < segments.Length || attack.LandingPulses > 0;
            default: return true;
        }
    }

    public void AdvanceTremor(float deltaTime)
    {
        if (deltaTime <= 0f || segments == null) return;
        bool moved = false;
        for (int i=0;i<segments.Length;i++)
        {
            if (segments[i] == null) continue;
            Vector3 position = segments[i].transform.position;
            moved |= (position-previousPositions[i]).sqrMagnitude > .00000001f;
            previousPositions[i] = position;
        }
        float pulse = 0f;
        if (attack.EmergencePulses > emergenceCount) pulse = emergenceCount==0 ? 1f : .45f;
        if (attack.LandingPulses > landingCount) pulse = Mathf.Max(pulse,landingCount==0 ? 1f : .45f);
        if (attack.RecoveryBurialPulses > recoveryCount)
            pulse = Mathf.Max(pulse,recoveryCount==0 ? .65f : .3f);
        ReadPulseCounts();
        if (!CanRumble()) { movingWeight = impactRemaining = impactWeight = 0f; return; }
        movingWeight = moved ? Mathf.MoveTowards(movingWeight,1f,deltaTime*7f) : 0f;
        impactRemaining = Mathf.Max(0f,impactRemaining-deltaTime);
        if (pulse > 0f)
        {
            impactWeight = Mathf.Max(pulse,impactWeight*impactRemaining/impactDuration);
            impactRemaining = impactDuration;
        }
        // Integrate frequency instead of multiplying elapsed lifetime by a
        // changing frequency, which would cause discontinuous high-frequency jumps.
        clock += deltaTime*Mathf.Lerp(movementFrequency,impactFrequency,
            Mathf.Clamp01(impactRemaining/impactDuration)*impactWeight)*Mathf.PI*2f;
    }

    private void ReadPulseCounts()
    {
        if (attack == null) return;
        emergenceCount=attack.EmergencePulses; landingCount=attack.LandingPulses;
        recoveryCount=attack.RecoveryBurialPulses;
    }

    protected override Vector2 GetOffset(Camera camera, CameraCircularVision vision, Plane[] cameraFrustum)
    {
        if (!CanRumble() || CurrentStrength <= 0f || !HasVisibleSegment(camera,vision,cameraFrustum))
            return Vector2.zero;
        float phase = clock;
        Vector2 wave = new Vector2(
            (Mathf.Sin(phase)+.3f*Mathf.Sin(phase*1.71f+.4f))*.65f,
            (Mathf.Sin(phase*1.27f+1.1f)+.25f*Mathf.Sin(phase*2.13f))*.72f);
        return Vector2.ClampMagnitude(wave,1f)*(CurrentStrength*camera.orthographicSize);
    }

    private bool HasVisibleSegment(Camera camera, CameraCircularVision vision, Plane[] cameraFrustum)
    {
        if (SoulMarkRuntime.IsVisionCovered) return false;
        foreach (var segment in segments)
        {
            if (segment == null || !segment.enabled || !segment.gameObject.activeInHierarchy ||
                (camera.cullingMask & (1<<segment.gameObject.layer))==0 ||
                !GeometryUtility.TestPlanesAABB(cameraFrustum,segment.bounds)) continue;
            // Per-section bounds: the empty space inside a coiled worm is not a visible part.
            if (vision == null || !vision.isActiveAndEnabled ||
                vision.IsWorldBoundsVisible(segment.bounds,segment.transform)) return true;
        }
        return false;
    }

    private void OnValidate()
    {
        movementStrength=Mathf.Clamp(movementStrength,0f,.03f);
        impactStrength=Mathf.Clamp(impactStrength,0f,.06f);
        impactDuration=Mathf.Clamp(impactDuration,.1f,.6f);
        movementFrequency=Mathf.Clamp(movementFrequency,3f,25f);
        impactFrequency=Mathf.Clamp(impactFrequency,3f,30f);
    }
}
