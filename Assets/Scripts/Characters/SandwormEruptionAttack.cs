using System.Collections.Generic;
using UnityEngine;

/// <summary>Four roaming arrivals, track the player until close, then a fixed segmented breach.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(SandwormBossPrototype), typeof(SandwormStateController))]
[DefaultExecutionOrder(-250)] // Publish framing before camera follow (-200) and vision (-100).
[AddComponentMenu("Characters/Sandworm Eruption Attack")]
public sealed partial class SandwormEruptionAttack : MonoBehaviour
{
    public enum AttackPhase { Roaming, Approaching, Traversing, Settling, Falling, Stunned, ReturningToBurrow,
        RoarRising, RoarStunned, RoarBurrowing }
    [Header("Attack Cycle / 攻击循环")]
    [SerializeField, Min(1)] private int roamingLegsPerAttack = 4;
    [SerializeField, Min(1f)] private float approachTimeout = 20f;
    [SerializeField, Min(.2f)] private float lockRetryInterval = 1.5f;
    [Header("Target Tracking / 目标追踪")]
    [SerializeField, Min(.5f), Tooltip("沙虫头部与玩家的直线距离小于等于此值后锁定破土点（世界单位，不随缩放改变）。")]
    private float targetLockDistance = 8f;
    [SerializeField, Range(.05f, .5f), Tooltip("追踪中的路线重算间隔；红圈仍每帧跟随玩家。")]
    private float trackingRepathInterval = .2f;
    [Header("Warning / 破土警示")]
    [SerializeField, Range(.2f,2f), Tooltip("沿当前路线预计还需此秒数抵达破土点时开始剧烈闪烁，不额外延迟攻击。")]
    private float warningFlashLeadTime = .85f;
    [SerializeField, Range(2f,8f), Tooltip("临近攻击时图标每秒明暗闪烁次数。")]
    private float warningFlashFrequency = 5f;
    [Header("Flight / 跃出路径（随整体大小缩放）")]
    [SerializeField, Min(1f), Tooltip("水平间距的本地单位下限；同时保证不小于沙虫完整直线长度乘下方倍率。")]
    private float landingDistance = 9f;
    [SerializeField, Min(1.05f), Tooltip("包含头尾尺寸的完整直线长度倍率；候选落点不会缩短到此下限以内。")]
    private float minimumSpanBodyLengthMultiplier = 1.15f;
    [SerializeField, Range(.3f, .9f)] private float arcHeightRatio = .55f;
    [SerializeField, Min(.1f)] private float flightSpeed = 3.5f;
    [Header("Damage / 出入口伤害")]
    [SerializeField, Min(0)] private int headEmergenceAttackPower = 10;
    [SerializeField, Min(0)] private int segmentAttackPower = 5;
    [SerializeField, Range(1f, 1.6f), Tooltip("伤害半径为最宽部位半宽乘此倍率；预警和实际伤害共用此半径。")]
    private float radiusMultiplier = 1.2f;

    public AttackPhase Phase { get; private set; }
    public Vector2 LockedAttackPoint { get; private set; }
    public Vector2 LandingPoint { get; private set; }
    public float DamageRadius { get; private set; }
    public bool IsTargetLocked { get; private set; }
    public float TargetLockDistance => targetLockDistance;
    public int EmergencePulses { get; private set; }
    public int LandingPulses { get; private set; }
    public float StraightBodyLength
    {
        get
        {
            if (!Initialize()) return 0f;
            float minimum = float.PositiveInfinity, maximum = float.NegativeInfinity;
            float gap = movement.WorldSegmentSpacing;
            for (int i = 0; i < parts.Length; i++)
            {
                float halfLength = visuals[i].NominalSize.x * Mathf.Abs(parts[i].lossyScale.x) * .5f;
                minimum = Mathf.Min(minimum, -i*gap-halfLength);
                maximum = Mathf.Max(maximum, -i*gap+halfLength);
            }
            return maximum-minimum;
        }
    }
    public float MinimumLandingSpan => Mathf.Max(landingDistance*Scale, StraightBodyLength*minimumSpanBodyLengthMultiplier);
    public int RoamingProgress => movement == null ? 0 : Mathf.Max(0, movement.CompletedRoamingLegs - cycleStart);
    // Per-pulse hook is also useful for future camera/audio integrations, without hard coupling.
    public event System.Action<Vector2, int, int, bool> SegmentPulse;

    private SandwormBossPrototype movement;
    private SandwormStateController states;
    private SandwormZeldaCharacterData character;
    private Transform[] parts;
    private SandwormSegmentVisual[] visuals;
    private Vector3[] savedPositions;
    private Quaternion[] savedRotations;
    private readonly List<Vector2> departure = new List<Vector2>(512);
    private readonly List<Vector2> history = new List<Vector2>(256);
    private readonly List<Vector2> resumeHistory = new List<Vector2>(256);
    private readonly List<Vector2> passage = new List<Vector2>(768);
    private readonly List<float> lengths = new List<float>(768);
    private SandwormEruptionSite emergenceSite, landingSite;
    private int cycleStart;
    private float elapsed, retryTimer, distance, arcLength, spacing, passageSpeed, trackingRepathTimer;
    private Matrix4x4 lockedRoot;
    private bool initialized, handedOff, lockedPlanReady;
    private int failedLockedPlans;
    private Vector2 rejectedLandingTarget;
    private float rejectedLandingCooldown;
    private PlacedBomb trackedDecoy;
    private bool trackingDecoy;
    public PlacedBomb DecoyTarget => trackedDecoy;
    private float Scale => Mathf.Max(.01f, Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y)));

    private bool Initialize()
    {
        if (initialized) return movement != null && states != null && character != null;
        movement = GetComponent<SandwormBossPrototype>();
        states = GetComponent<SandwormStateController>();
        character = GetComponent<SandwormZeldaCharacterData>();
        if (movement == null || states == null || character == null) return false;
        var ordered = new List<Transform> { movement.Head };
        ordered.AddRange(movement.BodySegments); ordered.Add(movement.Tail);
        parts = ordered.ToArray(); visuals = new SandwormSegmentVisual[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i] == null) return false;
            visuals[i] = parts[i].GetComponent<SandwormSegmentVisual>();
            if (visuals[i] == null) return false;
        }
        cycleStart = movement.CompletedRoamingLegs;
        initialized = true;
        return true;
    }

    private void LateUpdate()
    {
        if (Application.isPlaying) AdvanceAttack(Time.deltaTime);
    }

    public void AdvanceAttack(float deltaTime)
    {
        if (!Initialize() || deltaTime <= 0f) return;
        rejectedLandingCooldown = Mathf.Max(0f, rejectedLandingCooldown - deltaTime);
        if ((!states.IsUnderground && !IsExplosiveRecovery && !IsRoarRecovery) || !states.isActiveAndEnabled ||
            character.IsDead || !movement.isActiveAndEnabled || !movement.MovementEnabled)
        {
            if (Phase != AttackPhase.Roaming) CancelAttack();
            return;
        }
        if (Phase != AttackPhase.Roaming && transform.localToWorldMatrix != lockedRoot)
        { CancelAttack(); return; }
        TryReactToOngoingRoar();
        if (IsRoarRecovery)
        {
            AdvanceImpactVisuals(deltaTime);
            AdvanceRoarRecovery(deltaTime);
            return;
        }
        if (emergenceSite != null)
        {
            emergenceSite.SetWarningImminent(Phase==AttackPhase.Approaching && (!IsTargetLocked || lockedPlanReady) &&
                movement.GetDirectedArrivalSeconds(LockedAttackPoint)<=warningFlashLeadTime,warningFlashFrequency);
            emergenceSite.AdvanceVisuals(deltaTime);
        }
        if (landingSite != null) landingSite.AdvanceVisuals(deltaTime);
        AdvanceImpactVisuals(deltaTime);
        if (Phase == AttackPhase.Roaming)
        {
            retryTimer -= deltaTime;
            if (retryTimer > 0f || (RoamingProgress < roamingLegsPerAttack && FindDecoyTarget() == null)) return;
            retryTimer = lockRetryInterval;
            TryTrackPlayerAttack();
            return;
        }
        if (IsExplosiveRecovery) { AdvanceExplosiveRecovery(deltaTime); return; }
        elapsed += deltaTime;
        if (Phase == AttackPhase.Approaching)
        {
            RefreshDecoyPriority();
            if (elapsed > approachTimeout)
            { CancelAttack(); return; }
            if (!IsTargetLocked) UpdateTrackedTarget(deltaTime);
            if (Phase != AttackPhase.Approaching || !IsTargetLocked) return;
            if (!lockedPlanReady)
            {
                trackingRepathTimer -= deltaTime;
                if (trackingRepathTimer <= 0f) TryPrepareLockedPlan();
                if (Phase != AttackPhase.Approaching || !lockedPlanReady) return;
            }
            bool arrived = movement.HasArrivedAtDirectedPoint(LockedAttackPoint);
            if (!arrived && !movement.IsApproachingDirectedPoint(LockedAttackPoint))
            {
                // Arrival belongs to a specific head position and endpoint, not
                // a stale flag from before retargeting or a pose reset.
                if (!movement.TrySetEruptionDestination(LockedAttackPoint)) { CancelAttack(); return; }
                arrived = movement.HasArrivedAtDirectedPoint(LockedAttackPoint);
            }
            // Telegraphing happens during approach. Reaching the real endpoint
            // hands over to the breach in this frame, with no stationary timer.
            if (arrived) BeginPassage();
            return;
        }
        if (Phase == AttackPhase.Settling)
        {
            if ((emergenceSite == null || emergenceSite.IsFinished) && (landingSite == null || landingSite.IsFinished) && impactSites.Count == 0) CompleteAttack();
            return;
        }

        movement.AdvancePreparedRoaming(deltaTime);
        float end = arcLength + spacing * (parts.Length - 1) + .01f;
        distance = Mathf.Min(end, distance + passageSpeed * deltaTime);
        // Process every crossed threshold, including multiple sections on a slow frame.
        for (int i = 0; i < parts.Length; i++)
        {
            if (EmergencePulses == i && distance >= i * spacing)
            {
                EmitPulse(i, false);
                if (!isActiveAndEnabled || character == null || character.IsDead) { CancelAttack(); return; }
            }
        }
        float overshoot = 0f;
        if (SwallowedExplosiveCount > 0 && distance >= InternalBlastDistance)
        {
            overshoot = (distance - InternalBlastDistance) / passageSpeed;
            distance = InternalBlastDistance;
        }
        for (int i = 0; i < parts.Length; i++)
        {
            if (LandingPulses == i && distance >= arcLength + i * spacing)
            {
                EmitPulse(i, true);
                if (!isActiveAndEnabled || character == null || character.IsDead) { CancelAttack(); return; }
            }
        }
        ApplyPassagePose();
        if (SwallowedExplosiveCount > 0 && distance >= InternalBlastDistance)
        {
            BeginExplosiveFall();
            if (Phase == AttackPhase.Falling && overshoot > 0f) AdvanceExplosiveRecovery(overshoot);
        }
        else if (distance >= end) FinishPassage();
    }

    private void FinishPassage()
    {
        CameraTemporaryFraming.Remove(this);
        if (IsExplosiveRecovery) states.EnterUnderground();
        states.EndSegmentedPassage();
        landingSite.Finish();
        // Tail burial ends movement takeover immediately. Dirt can finish
        // fading independently while the worm follows its prepared route.
        resumeHistory.Clear();
        float keepLength = spacing * (parts.Length + 1);
        float sampleStep = spacing * .08f;
        for (float behind = 0f; behind <= keepLength + sampleStep; behind += sampleStep)
            resumeHistory.Add(Sample(distance - behind));
        if (!movement.ResumePreparedRoaming(resumeHistory)) { CancelAttack(); return; }
        handedOff = true;
        savedPositions = null; savedRotations = null;
        cycleStart = movement.CompletedRoamingLegs;
        Phase = AttackPhase.Settling; elapsed = 0f;
    }

    /// <summary>Explicit test/phase hook. The point is frozen; subsequent player movement cannot retarget it.</summary>
    public bool TryLockAttack(Vector2 target)
    {
        if (!CanStartAttack()) return false;
        if (!movement.IsDestinationAllowed(target)) return false;
        spacing = movement.WorldSegmentSpacing;
        if (!TrySelectLanding(target, out Vector2 landing) || !movement.TrySetEruptionDestination(target)) return false;
        LandingPoint = landing;
        trackedDecoy = null; trackingDecoy = false;
        BeginApproach(target, true);
        return true;
    }

    /// <summary>Normal AI entry: follow the currently controlled player until the head is close enough.</summary>
    public bool TryTrackPlayerAttack()
    {
        if (!CanStartAttack()) return false;
        trackedDecoy = FindDecoyTarget();
        trackingDecoy = trackedDecoy != null;
        var player = GetTrackedPlayer();
        if (!trackingDecoy && player == null) return false;
        Vector2 target = trackingDecoy ? (Vector2)trackedDecoy.transform.position : (Vector2)player.transform.position;
        // An impossible landing should not repeatedly drag the worm back to
        // the same failed attack while running a full search on every retry.
        if (rejectedLandingCooldown > 0f &&
            Vector2.Distance(target, rejectedLandingTarget) < Mathf.Max(1f, movement.WorldSegmentSpacing)) return false;
        if (!movement.TrySetEruptionDestination(target)) return false;
        spacing = movement.WorldSegmentSpacing;
        departure.Clear();
        LandingPoint = target; // No speculative landing warning before the target locks.
        BeginApproach(target, false);
        UpdateTrackedTarget(0f);
        return true;
    }

    private bool CanStartAttack()
    {
        return isActiveAndEnabled && Initialize() && Phase == AttackPhase.Roaming && states.IsUnderground &&
            !character.IsDead && movement.isActiveAndEnabled && movement.MovementEnabled;
    }

    private ZeldaFourWayMover GetTrackedPlayer()
    {
        var player = ZeldaRuntimeRegistry.GetControlledMover();
        return player != null && player.isActiveAndEnabled &&
            ZeldaRuntimeRegistry.GetGameplayScene(player.gameObject) == gameObject.scene ? player : null;
    }

    private void UpdateTrackedTarget(float deltaTime)
    {
        var player = GetTrackedPlayer();
        if (trackedDecoy == null && player == null) { CancelAttack(); return; }
        Vector2 target = trackedDecoy != null ? (Vector2)trackedDecoy.transform.position : (Vector2)player.transform.position;
        // The warning follows every frame; curve searches are throttled separately.
        LockedAttackPoint = target;
        emergenceSite.transform.position = target;
        bool closeEnough = Vector2.Distance(movement.HeadPosition, target) <= targetLockDistance;
        // Distance locking is evaluated every frame, independent of route-search throttling.
        if (closeEnough && movement.IsDestinationAllowed(target))
        {
            IsTargetLocked = true;
            emergenceSite.GetComponent<CameraVisionStreamingRegion>().Configure(DamageRadius+1f);
            TryPrepareLockedPlan();
            return;
        }
        trackingRepathTimer -= deltaTime;
        if (trackingRepathTimer > 0f) return;
        trackingRepathTimer = trackingRepathInterval;
        if (!movement.IsDestinationAllowed(target))
        {
            // If the player moved into an unreachable margin, an old arrived
            // route must not leave the head stopped beneath an unrelated warning.
            if (!movement.HasDestination) CancelAttack();
            return;
        }
        bool sameGoal = movement.IsApproachingDirectedPoint(target) || movement.HasArrivedAtDirectedPoint(target);
        // Failed replans keep the current smooth route, never reset the head/body pose.
        if (!sameGoal && !movement.TrySetEruptionDestination(target) && !movement.HasDestination) CancelAttack();
    }

    private PlacedBomb FindDecoyTarget()
    {
        if (trackedDecoy != null && trackedDecoy.IsActiveDecoy && trackedDecoy.gameObject.scene == gameObject.scene &&
            movement.IsDestinationAllowed(trackedDecoy.transform.position)) return trackedDecoy;
        PlacedBomb closest = null;
        float best = float.PositiveInfinity;
        foreach (var bomb in PlacedBomb.ActiveBombs)
        {
            if (bomb == null || !bomb.IsActiveDecoy || bomb.gameObject.scene != gameObject.scene) continue;
            float sqr = ((Vector2)bomb.transform.position - movement.HeadPosition).sqrMagnitude;
            if (sqr >= best || !movement.IsDestinationAllowed(bomb.transform.position)) continue;
            closest = bomb; best = sqr;
        }
        return closest;
    }

    private void RefreshDecoyPriority()
    {
        var next = FindDecoyTarget();
        if (next == trackedDecoy && trackingDecoy == (next != null)) return;
        // A newly activated beacon can divert even a locked approach, but never
        // teleport an already airborne body. A destroyed lure releases the lock.
        trackedDecoy = next;
        trackingDecoy = next != null;
        IsTargetLocked = lockedPlanReady = false;
        failedLockedPlans = 0;
        elapsed = trackingRepathTimer = 0f;
        if (landingSite != null) landingSite.Release();
        landingSite = null;
        departure.Clear();
    }

    private void TryPrepareLockedPlan()
    {
        trackingRepathTimer = trackingRepathInterval;
        bool sameGoal = movement.IsApproachingDirectedPoint(LockedAttackPoint) || movement.HasArrivedAtDirectedPoint(LockedAttackPoint);
        // A player can dodge laterally just before locking. Never erupt at the
        // previous route's stale endpoint if the final upward approach needs a retry.
        if (!movement.IsDestinationAllowed(LockedAttackPoint)) { CancelAttack(); return; }
        if (!sameGoal && !movement.TrySetEruptionDestination(LockedAttackPoint))
        {
            if (!movement.HasDestination) CancelAttack();
            return;
        }
        if (!TrySelectLanding(LockedAttackPoint, out Vector2 landing))
        {
            failedLockedPlans++;
            if (failedLockedPlans >= 2 || movement.HasArrivedAtDirectedPoint(LockedAttackPoint))
            {
                rejectedLandingTarget = LockedAttackPoint;
                rejectedLandingCooldown = Mathf.Max(5f, lockRetryInterval * 3f);
                CancelAttack();
            }
            return;
        }
        LandingPoint = landing;
        landingSite = SandwormEruptionSite.Create(gameObject.scene, landing, DamageRadius,false);
        lockedPlanReady = true;
    }

    private bool TrySelectLanding(Vector2 target, out Vector2 selected)
    {
        selected = target;
        float needed = spacing * (parts.Length + 1);
        // Try both horizontal directions and longer spans, never a fallback
        // shorter than the complete straight body (including the head/tail extents).
        int firstSide = Random.value < .5f ? -1 : 1;
        float minimumSpan = MinimumLandingSpan;
        for (int attempt = 0; attempt < 6; attempt++)
        {
            float side = attempt % 2 == 0 ? firstSide : -firstSide;
            float span = minimumSpan * (1f + (attempt / 2) * .12f);
            Vector2 landing = target + Vector2.right * (side * span);
            if (!movement.TryPlanBuriedDeparture(landing, needed, departure)) continue;
            selected = landing; return true;
        }
        return false;
    }

    private void BeginApproach(Vector2 target, bool locked)
    {
        LockedAttackPoint = target;
        IsTargetLocked = locked;
        lockedPlanReady = locked;
        failedLockedPlans = 0;
        DamageRadius = .1f;
        for (int i = 0; i < parts.Length; i++)
            DamageRadius = Mathf.Max(DamageRadius, visuals[i].NominalSize.y * Mathf.Abs(parts[i].lossyScale.y) * .5f * radiusMultiplier);
        emergenceSite = SandwormEruptionSite.Create(gameObject.scene, target, DamageRadius);
        if (locked) landingSite = SandwormEruptionSite.Create(gameObject.scene, LandingPoint, DamageRadius,false);
        lockedRoot = transform.localToWorldMatrix;
        elapsed = distance = trackingRepathTimer = 0f;
        handedOff = false;
        ResetSwallowedExplosives();
        EmergencePulses = LandingPulses = 0;
        Phase = AttackPhase.Approaching;
    }

    private void BeginPassage()
    {
        if (!movement.HasArrivedAtDirectedPoint(LockedAttackPoint)) return;
        // A gate may have closed after the warning was placed. Recheck the landing
        // and buried escape before committing to the airborne traversal.
        if (!movement.IsBuriedPathClear(departure) &&
            !movement.TryPlanBuriedDeparture(LandingPoint, spacing*(parts.Length+1), departure)) { CancelAttack(); return; }
        if (!states.BeginSegmentedPassage()) { CancelAttack(); return; }
        savedPositions = new Vector3[parts.Length]; savedRotations = new Quaternion[parts.Length];
        for (int i = 0; i < parts.Length; i++) { savedPositions[i] = parts[i].localPosition; savedRotations[i] = parts[i].localRotation; }
        movement.CopyHeadHistory(history);
        movement.BeginExternalPathControl();
        passage.Clear(); lengths.Clear();
        float span = LandingPoint.x - LockedAttackPoint.x;
        float height = Mathf.Abs(span) * arcHeightRatio;
        // Sample the upper half ellipse then travel by arc length, never by angle.
        // This keeps spacing/speed steady at the apex and at both ground crossings.
        for (int i = 0; i <= 128; i++)
        {
            float angle = Mathf.PI * i / 128f;
            AddPassagePoint(new Vector2(LockedAttackPoint.x + span * (.5f - .5f * Mathf.Cos(angle)),
                LockedAttackPoint.y + height * Mathf.Sin(angle)));
        }
        passage[passage.Count - 1] = LandingPoint;
        arcLength = lengths[lengths.Count - 1];
        for (int i = 1; i < departure.Count; i++) AddPassagePoint(departure[i]);
        if (!movement.PrepareRoamingContinuation(passage, arcLength + spacing*(parts.Length-1) + .01f))
        { CancelAttack(); return; }
        distance = 0f; elapsed = 0f; passageSpeed = flightSpeed * Scale;
        Phase = AttackPhase.Traversing;
        CameraTemporaryFraming.Set(this, 1.3f);
        EmitPulse(0, false);
        if (character != null && !character.IsDead) ApplyPassagePose();
    }

    private void AddPassagePoint(Vector2 point)
    {
        float length = passage.Count == 0 ? 0f : lengths[lengths.Count-1] + Vector2.Distance(passage[passage.Count-1], point);
        passage.Add(point); lengths.Add(length);
    }

    private Vector2 Sample(float along)
    {
        if (along < 0f)
        {
            float behind = -along;
            for (int i = 1; i < history.Count; i++)
            {
                float length = Vector2.Distance(history[i-1], history[i]);
                if (length >= behind && length > .00001f) return Vector2.Lerp(history[i-1], history[i], behind / length);
                behind -= length;
            }
            return history.Count > 0 ? history[history.Count-1] : LockedAttackPoint;
        }
        int lower = 0, upper = lengths.Count - 1;
        while (lower + 1 < upper)
        {
            int middle = (lower + upper) / 2;
            if (lengths[middle] < along) lower = middle; else upper = middle;
        }
        float segmentLength = lengths[upper] - lengths[lower];
        return Vector2.Lerp(passage[lower], passage[upper], segmentLength > .00001f ? (along-lengths[lower]) / segmentLength : 0f);
    }

    private void ApplyPassagePose()
    {
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i] == null) { CancelAttack(); return; }
            float along = distance - spacing * i;
            Vector2 point = Sample(along);
            Vector2 tangent = Sample(along + .04f * spacing) - Sample(along - .04f * spacing);
            parts[i].position = new Vector3(point.x, point.y, parts[i].position.z);
            Vector3 localDirection = parts[i].parent.InverseTransformVector(tangent);
            if (parts[i].localScale.x < 0f) localDirection = -localDirection;
            parts[i].localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(localDirection.y, localDirection.x) * Mathf.Rad2Deg);
            bool returning = Phase == AttackPhase.ReturningToBurrow;
            states.SetSegmentExposed(visuals[i], (returning || along >= 0f) && along < arcLength, returning);
        }
        if (Phase == AttackPhase.ReturningToBurrow) BlendResidualLandingPose();
    }

    private void EmitPulse(int index, bool landing)
    {
        if (landing) LandingPulses++; else EmergencePulses++;
        var site = landing ? landingSite : emergenceSite;
        int power = !landing && index == 0 ? headEmergenceAttackPower : segmentAttackPower;
        site.Pulse(index == 0);
        // Consume BEFORE normal damage creates ground blasts/chain reactions.
        if (!landing && Application.isPlaying) SwallowExplosivesAtEmergence();
        if (Application.isPlaying) ZeldaAttackHitbox.ApplyCircularPulse(site.transform.position, DamageRadius, power, character);
        SegmentPulse?.Invoke(site.transform.position, power, index, landing);
        if (!landing && index == parts.Length - 1 && emergenceSite != null) emergenceSite.Finish();
    }

    private void CompleteAttack()
    {
        trackedDecoy = null; trackingDecoy = false;
        CameraTemporaryFraming.Remove(this);
        ReleaseRecoveryVisuals();
        ReleaseSites(); savedPositions = null; savedRotations = null;
        handedOff = false;
        IsTargetLocked = false;
        lockedPlanReady = false;
        retryTimer = lockRetryInterval; Phase = AttackPhase.Roaming;
    }

    public void CancelAttack()
    {
        trackedDecoy = null; trackingDecoy = false;
        CameraTemporaryFraming.Remove(this);
        if (Phase == AttackPhase.Roaming) return;
        ReleaseRecoveryVisuals();
        if ((IsExplosiveRecovery || IsRoarRecovery) && states != null) states.EnterUnderground();
        // Interrupted phase changes/unloads must not leave airborne pieces or stale warning/retention objects.
        if (savedPositions != null)
            for (int i = 0; i < parts.Length; i++)
                if (parts[i] != null) { parts[i].localPosition = savedPositions[i]; parts[i].localRotation = savedRotations[i]; }
        if (states != null) states.EndSegmentedPassage();
        ReleaseSites(); savedPositions = null; savedRotations = null;
        if (movement != null)
        {
            if (!handedOff) movement.ResumeRoamingFromCurrentPose();
            cycleStart = movement.CompletedRoamingLegs;
        }
        handedOff = false;
        IsTargetLocked = false;
        lockedPlanReady = false;
        Phase = AttackPhase.Roaming; retryTimer = lockRetryInterval;
    }

    private void ReleaseSites()
    {
        if (emergenceSite != null) emergenceSite.Release();
        if (landingSite != null) landingSite.Release();
        emergenceSite = landingSite = null;
    }
    private void OnDisable() { CancelAttack(); }
    private void OnDestroy() { CancelAttack(); }
    private void OnValidate()
    {
        roamingLegsPerAttack = Mathf.Max(1, roamingLegsPerAttack);
        approachTimeout = Mathf.Max(1f, approachTimeout);
        lockRetryInterval = Mathf.Max(.2f, lockRetryInterval); landingDistance = Mathf.Max(1f, landingDistance);
        targetLockDistance = Mathf.Max(.5f, targetLockDistance);
        trackingRepathInterval = Mathf.Clamp(trackingRepathInterval, .05f, .5f);
        warningFlashLeadTime = Mathf.Clamp(warningFlashLeadTime,.2f,2f);
        warningFlashFrequency = Mathf.Clamp(warningFlashFrequency,2f,8f);
        minimumSpanBodyLengthMultiplier = Mathf.Max(1.05f, minimumSpanBodyLengthMultiplier);
        flightSpeed = Mathf.Max(.1f, flightSpeed); radiusMultiplier = Mathf.Clamp(radiusMultiplier, 1f, 1.6f);
        arcHeightRatio = Mathf.Clamp(arcHeightRatio, .3f, .9f);
        headEmergenceAttackPower = Mathf.Max(0, headEmergenceAttackPower); segmentAttackPower = Mathf.Max(0, segmentAttackPower);
        swallowedStunDuration = Mathf.Max(0f, swallowedStunDuration);
        fallDuration = Mathf.Max(.1f, fallDuration);
        fallBendAngle = Mathf.Clamp(fallBendAngle,15f,75f);
        roarStunDuration = Mathf.Max(0f, roarStunDuration);
        roarPopDuration = Mathf.Max(.15f, roarPopDuration);
        roarPopHeight = Mathf.Max(.05f, roarPopHeight);
        roarBurrowDuration = Mathf.Max(.15f, roarBurrowDuration);
    }
}
