using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Three-state stationary sentry; no pursuit paths or global incident timer.</summary>
public sealed class TurretCharacterAi : ZeldaCharacterAiBase
{
    [Header("Sentry / 炮台")]
    [SerializeField, Tooltip("可选。绑定后不检查权限，只保护该角色并反击实际攻击者。跨场景保留角色身份，返回炮台所在场景后继续保护。")]
    private ZeldaCharacterData protectedCharacter;
    [SerializeField, Min(.1f)] private float idleTurnInterval = 3f;
    [SerializeField, Min(.3f)] private float burstInterval = 1.5f;
    [SerializeField, Min(.1f)] private float burstShotSpacing = .12f;
    private TurretZeldaCharacterData turret;
    private ZeldaCharacterData subscribedCharacter;
    private ZeldaFourWayMover subscribedMover;
    private readonly List<ZeldaFourWayMover> candidates = new List<ZeldaFourWayMover>(24);
    private float idleTimer = 3f, idleAngle, warningElapsed, burstTimer, shotTimer, scanTimer, resolveTimer;
    private int burstRemaining;
    private string boundId, forcedTargetId;
    private ZeldaFourWayMover forcedTarget;
    // Both incident types are sight-limited when firing. Protection incidents keep
    // their target identity out of sight; ordinary witnessed offences do not.
    private string witnessedTargetId;
    private ZeldaFourWayMover witnessedTarget;
    private bool followPossessingPlayer;
    public ZeldaCharacterData ProtectedCharacter => protectedCharacter;
    public bool HasBinding => protectedCharacter != null || !string.IsNullOrEmpty(boundId);
    protected override bool RemainsStationary => true;
    protected override bool RespondsToDecoyBombs => false;
    protected override bool EnforcesPermissionAreas => !HasBinding;
    // A turret is not a roaming guard; its short-range engagement is independent.
    public override bool SharesHostilityWithOtherNpcs => false;
    public override bool RequiresVisionStreamingRetention => HasBinding || forcedTarget != null || base.RequiresVisionStreamingRetention;

    protected override void Awake()
    {
        turret = GetComponent<TurretZeldaCharacterData>();
        ConfigureOmnidirectionalVision(); base.Awake();
        idleAngle = Mathf.Atan2(turret.AimDirection.y, turret.AimDirection.x) * Mathf.Rad2Deg;
        idleTimer = idleTurnInterval;
    }
    protected override void OnEnable()
    {
        base.OnEnable();
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        SceneManager.sceneLoaded += OnSceneLoaded;
        ZeldaRuntimeRegistry.ControlledMoverChanged += RefreshProtectionBinding;
        RefreshProtectionBinding();
    }
    protected override void OnDisable()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        ZeldaRuntimeRegistry.ControlledMoverChanged -= RefreshProtectionBinding;
        Unsubscribe(); burstRemaining = 0; base.OnDisable();
    }
    private void OnActiveSceneChanged(Scene previous, Scene current) => RefreshProtectionBinding();
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => RefreshProtectionBinding();

    // Also called after scene/player identities have all been restored. Unlike an
    // AI tick, this must run while loading, paused or stunned, before the next hit.
    public void RefreshProtectionBinding()
    {
        if (!isActiveAndEnabled) return;
        resolveTimer = 0f;
        ResolveSavedReferences(0f);
    }
    protected override void OnValidate() { base.OnValidate(); ConfigureOmnidirectionalVision(); }
    protected override void Update()
    {
        if (Time.deltaTime <= 0f) return;
        Subscribe();
        base.Update();
    }
    public void BindCharacter(ZeldaCharacterData character)
    {
        Unsubscribe(); protectedCharacter = character;
        boundId = character != null ? character.IndividualAccessId : null;
        forcedTarget = null; forcedTargetId = null; followPossessingPlayer = false;
        ClearWitnessedTarget();
        warningElapsed = 0; burstRemaining = 0; scanTimer = 0;
        ResetPermissionResponse();
        if (isActiveAndEnabled) Subscribe();
    }
    private void Subscribe()
    {
        if (protectedCharacter != null)
        {
            if (string.IsNullOrEmpty(boundId)) boundId = protectedCharacter.IndividualAccessId;
            if (protectedCharacter.IndividualAccessId != boundId ||
                ZeldaRuntimeRegistry.GetGameplayScene(protectedCharacter.gameObject) !=
                    ZeldaRuntimeRegistry.GetGameplayScene(gameObject))
            {
                // Keep the identity, not a reference to the departed body or an
                // authored replacement that merely has the same name/prefab.
                Unsubscribe(); protectedCharacter = null;
            }
        }
        if (subscribedCharacter == protectedCharacter) return;
        Unsubscribe();
        if (protectedCharacter == null) return;
        subscribedCharacter = protectedCharacter;
        subscribedMover = protectedCharacter.GetComponent<ZeldaFourWayMover>();
        subscribedCharacter.AttackedBy += ProtectedAttacked;
        if (subscribedMover != null) subscribedMover.PossessionAttempted += ProtectedPossessionAttempt;
    }
    private void Unsubscribe()
    {
        if (subscribedCharacter != null) subscribedCharacter.AttackedBy -= ProtectedAttacked;
        if (subscribedMover != null) subscribedMover.PossessionAttempted -= ProtectedPossessionAttempt;
        subscribedCharacter = null; subscribedMover = null;
    }
    private void ProtectedAttacked(ZeldaCharacterData attacker)
    {
        if (HasLocalProtectionSubscription && attacker != null)
            ProtectAgainst(attacker.GetComponent<ZeldaFourWayMover>(), false);
    }
    private void ProtectedPossessionAttempt(ZeldaFourWayMover player)
    {
        if (HasLocalProtectionSubscription) ProtectAgainst(player, true);
    }
    private bool HasLocalProtectionSubscription => subscribedCharacter != null &&
        IsProtectedCharacter(subscribedCharacter) &&
        ZeldaRuntimeRegistry.GetGameplayScene(subscribedCharacter.gameObject) ==
            ZeldaRuntimeRegistry.GetGameplayScene(gameObject);
    private bool IsProtectedCharacter(ZeldaCharacterData character) => character != null &&
        (character == protectedCharacter || (!string.IsNullOrEmpty(boundId) && character.IndividualAccessId == boundId));
    private bool ValidTarget(ZeldaFourWayMover target) => target != null && target.gameObject != gameObject &&
        target.CharacterData != null && !target.CharacterData.IsDead && !target.CharacterData.IsGhostLike &&
        !IsProtectedCharacter(target.CharacterData) &&
        ZeldaRuntimeRegistry.GetGameplayScene(target.gameObject) == ZeldaRuntimeRegistry.GetGameplayScene(gameObject);
    private void ClearForcedTarget()
    {
        forcedTarget = null; forcedTargetId = null; followPossessingPlayer = false;
        burstRemaining = 0;
    }
    private void ClearWitnessedTarget() { witnessedTarget = null; witnessedTargetId = null; }
    private bool CanWitnessOffence => isActiveAndEnabled && !IsStunned && !IsPossessionLocked &&
        !IsEchoRecoveryFrame && !IsIgnoringPlayer && turret != null && !turret.IsDead;
    private void RememberWitnessedTarget(ZeldaFourWayMover target)
    {
        witnessedTarget = target; witnessedTargetId = target.CharacterData.IndividualAccessId;
        warningElapsed = 0f;
        // A remembered but unseen aggressor must not suppress a visible offence.
        if (!ValidTarget(forcedTarget) || !CanSeeCharacter(forcedTarget))
            SetSentryResponse(target, ZeldaAiState.Hostile);
    }
    private bool TryWitnessPlayerOffence(ZeldaCharacterData offender, bool ignoreCardboardConcealment = false)
    {
        if (!CanWitnessOffence || offender == null) return false;
        var player = offender.GetComponent<ZeldaFourWayMover>();
        if (!ValidTarget(player) || player != ZeldaRuntimeRegistry.GetControlledMover() || !player.isActiveAndEnabled)
            return false;
        bool visible = ignoreCardboardConcealment
            ? CanSeeWorldPoint(player.transform.position, player.transform) : CanSeePlayer(player);
        if (!visible) return false;
        RememberWitnessedTarget(player);
        return true;
    }
    private void ProtectAgainst(ZeldaFourWayMover target, bool possession)
    {
        if (!ValidTarget(target) || turret == null || turret.IsDead) return;
        forcedTarget = target; forcedTargetId = target.CharacterData.IndividualAccessId;
        followPossessingPlayer = possession;
        // Remember the incident while stunned, without cancelling the stun.
        if (!IsStunned && !IsPossessionLocked && !IsEchoRecoveryFrame && CanSeeCharacter(target))
            SetSentryResponse(target, ZeldaAiState.Hostile);
    }
    private void ResolveSavedReferences(float dt)
    {
        Subscribe();
        // A travelling/loaded player may be ready before the rest of the scene.
        // Check this one reference first; normal frames never scan all characters.
        if (protectedCharacter == null && !string.IsNullOrEmpty(boundId))
        {
            var player = ZeldaRuntimeRegistry.GetControlledMover();
            if (player != null && player.CharacterData != null &&
                player.CharacterData.IndividualAccessId == boundId &&
                ZeldaRuntimeRegistry.GetGameplayScene(player.gameObject) == ZeldaRuntimeRegistry.GetGameplayScene(gameObject))
            { protectedCharacter = player.CharacterData; Subscribe(); }
        }
        resolveTimer -= dt;
        if (resolveTimer > 0f) return;
        resolveTimer = .5f;
        if ((protectedCharacter != null || string.IsNullOrEmpty(boundId)) &&
            (forcedTarget != null || string.IsNullOrEmpty(forcedTargetId)) &&
            (witnessedTarget != null || string.IsNullOrEmpty(witnessedTargetId))) return;
        // Only restoration/missing references use the registry; normal targeting
        // uses the shared spatial query, never a scene-wide FindObjects call.
        foreach (var candidate in ZeldaRuntimeRegistry.Movers)
        {
            if (candidate == null || candidate.CharacterData == null ||
                ZeldaRuntimeRegistry.GetGameplayScene(candidate.gameObject) != ZeldaRuntimeRegistry.GetGameplayScene(gameObject)) continue;
            string id = candidate.CharacterData.IndividualAccessId;
            if (protectedCharacter == null && id == boundId) protectedCharacter = candidate.CharacterData;
            if (id == forcedTargetId) forcedTarget = candidate;
            if (id == witnessedTargetId) witnessedTarget = candidate;
        }
        Subscribe();
    }
    protected override void UpdateTargetAndState(float deltaTime)
    {
        if (IsEchoRecoveryFrame) return;
        ResolveSavedReferences(deltaTime);
        // Do this before following control changes: a dead body must not pass its
        // hostility on to the resulting ghost (or that ghost's next possession).
        // ReferenceEquals also catches Unity's destroyed-object "fake null".
        if (!ReferenceEquals(forcedTarget, null) && !ValidTarget(forcedTarget)) ClearForcedTarget();
        if (followPossessingPlayer)
        {
            var player = ZeldaRuntimeRegistry.GetControlledMover();
            if (!ValidTarget(player)) ClearForcedTarget();
            else if (ValidTarget(forcedTarget))
            { forcedTarget = player; forcedTargetId = player.CharacterData.IndividualAccessId; }
        }
        // Losing sight only suspends engagement: leave forcedTarget/forcedTargetId
        // intact so returning or reloaded bodies are recognized without a new hit.
        if (ValidTarget(forcedTarget) && CanSeeCharacter(forcedTarget))
        {
            SetSentryResponse(forcedTarget, ZeldaAiState.Hostile); return;
        }
        var possessingPlayer = ZeldaRuntimeRegistry.GetControlledMover();
        if (possessingPlayer != null && possessingPlayer.IsPossessionInProgress)
            TryWitnessPlayerOffence(possessingPlayer.CharacterData);
        if (ValidTarget(witnessedTarget) && CanSeeCharacter(witnessedTarget))
        { SetSentryResponse(witnessedTarget, ZeldaAiState.Hostile); return; }
        if (!string.IsNullOrEmpty(witnessedTargetId))
        {
            ClearWitnessedTarget();
            burstRemaining = 0;
            SetSentryResponse(null, ZeldaAiState.Idle);
        }
        if (HasBinding)
        {
            var owner = protectedCharacter != null ? protectedCharacter.GetComponent<ZeldaFourWayMover>() : null;
            if (owner != null && owner == ZeldaRuntimeRegistry.GetControlledMover() && !protectedCharacter.IsDead)
            { WatchPlayerEnemies(owner, deltaTime); return; }
            var guard = protectedCharacter != null ? protectedCharacter.GetComponent<ZeldaCharacterAiBase>() : null;
            if (guard != null && guard.isActiveAndEnabled && guard.CurrentState == ZeldaAiState.Hostile &&
                ValidTarget(guard.CurrentTarget) && CanSeeCharacter(guard.CurrentTarget))
            { ProtectAgainst(guard.CurrentTarget, false); return; }
            SetSentryResponse(null, ZeldaAiState.Idle); return;
        }
        if (CurrentState == ZeldaAiState.Hostile && ValidTarget(CurrentTarget) && CanSeeCharacter(CurrentTarget)) return;
        // Normal sentries stop firing immediately upon losing sight, with no search/pursuit phase.
        if (CurrentState == ZeldaAiState.Hostile) SetSentryResponse(null, ZeldaAiState.Idle);
        scanTimer -= deltaTime;
        if (scanTimer <= 0f)
        {
            scanTimer = .12f;
            QueryNearbyVisionCandidates(candidates);
            ZeldaFourWayMover nearest = null; float distance = float.MaxValue;
            foreach (var candidate in candidates)
            {
                if (!ValidTarget(candidate)) continue;
                var ai = candidate.GetComponent<ZeldaCharacterAiBase>();
                if (ai == null || !ai.IsUniversalThreat || !CanSeeCharacter(candidate)) continue;
                float d = ((Vector2)candidate.transform.position - AiPosition).sqrMagnitude;
                if (d < distance) { nearest = candidate; distance = d; }
            }
            if (nearest != null) { SetSentryResponse(nearest, ZeldaAiState.Hostile); return; }
        }
        var controlled = ZeldaRuntimeRegistry.GetControlledMover();
        if (IsIgnoringPlayer || !ValidTarget(controlled) || !CanSeePlayer(controlled))
        { warningElapsed = 0; SetSentryResponse(null, ZeldaAiState.Idle); return; }
        int required = PermissionArea.DefaultPermissionLevel;
        foreach (var area in ZeldaRuntimeRegistry.PermissionAreas)
            if (area != null && area.isActiveAndEnabled && area.gameObject.scene == gameObject.scene && area.Contains(controlled.transform.position))
                required = Mathf.Max(required, area.PermissionLevel);
        int difference = required - controlled.CharacterData.PermissionLevel;
        if (difference <= 0) { warningElapsed = 0; SetSentryResponse(null, ZeldaAiState.Idle); return; }
        if (CurrentTarget != controlled || CurrentState != ZeldaAiState.Alert) warningElapsed = 0;
        warningElapsed += deltaTime;
        SetSentryResponse(controlled, difference >= 2 || warningElapsed >= PermissionWarningDuration
            ? ZeldaAiState.Hostile : ZeldaAiState.Alert);
    }
    private void WatchPlayerEnemies(ZeldaFourWayMover owner, float deltaTime)
    {
        // An unseen remembered aggressor does not prevent intercepting other visible
        // enemies. Automatic interception never replaces the protection memory.
        if (IsVisiblePlayerEnemy(CurrentTarget, owner)) return;
        SetSentryResponse(null, ZeldaAiState.Idle);
        scanTimer -= deltaTime;
        if (scanTimer > 0f) return;
        scanTimer = .12f;
        QueryNearbyVisionCandidates(candidates);
        ZeldaFourWayMover nearest = null; float distance = float.MaxValue;
        foreach (var candidate in candidates)
        {
            if (!IsVisiblePlayerEnemy(candidate, owner)) continue;
            float d = ((Vector2)candidate.transform.position - AiPosition).sqrMagnitude;
            if (d < distance) { nearest = candidate; distance = d; }
        }
        if (nearest != null) SetSentryResponse(nearest, ZeldaAiState.Hostile);
    }
    private bool IsVisiblePlayerEnemy(ZeldaFourWayMover candidate, ZeldaFourWayMover owner)
    {
        if (!ValidTarget(candidate)) return false;
        var ai = candidate.GetComponent<ZeldaCharacterAiBase>();
        return ai != null && ai.isActiveAndEnabled && ai.IsActivelyHostileTo(owner) && CanSeeCharacter(candidate);
    }
    protected override void TickIdle(float deltaTime)
    {
        idleTimer -= deltaTime;
        if (idleTimer <= 0f) { idleTimer = idleTurnInterval; idleAngle = UnityEngine.Random.Range(0f, 360f); }
        turret.AimAt(AiPosition + new Vector2(Mathf.Cos(idleAngle * Mathf.Deg2Rad), Mathf.Sin(idleAngle * Mathf.Deg2Rad)), deltaTime);
    }
    protected override void TickAlert(float deltaTime)
    {
        if (CurrentTarget != null) turret.AimAt(CurrentTarget.transform.position, deltaTime);
    }
    protected override void TickHostile(float deltaTime)
    {
        // Recheck every shot, including the remaining shots of an active burst.
        // No protection event or restored pending burst grants unlimited range.
        if (!ValidTarget(CurrentTarget) || !CanSeeCharacter(CurrentTarget))
        { burstRemaining = 0; SetSentryResponse(null, ZeldaAiState.Idle); return; }
        turret.AimAt(CurrentTarget.transform.position, deltaTime);
        burstTimer -= deltaTime; shotTimer -= deltaTime;
        if (burstRemaining == 0 && burstTimer <= 0f) { burstRemaining = 3; shotTimer = 0; burstTimer = burstInterval; }
        if (burstRemaining > 0 && shotTimer <= 0f && turret.TryFire(true))
        { burstRemaining--; shotTimer = burstShotSpacing; }
    }
    protected override void UpdateFacingFromMovement() { if (turret != null) SetFacingDirection(turret.AimDirection); }
    protected override void ChangeState(ZeldaAiState state, bool playAwarenessCue = true)
    {
        if (state == ZeldaAiState.Suspicious || state == ZeldaAiState.Search || state == ZeldaAiState.Recovery)
            state = IsEchoRecoveryFrame ? ZeldaAiState.Recovery : ZeldaAiState.Idle;
        base.ChangeState(state, playAwarenessCue);
    }
    protected override void OnStateEntered(ZeldaAiState oldState, ZeldaAiState state)
    {
        burstRemaining = 0;
        if (state == ZeldaAiState.Hostile) burstTimer = 0;
        if (state == ZeldaAiState.Idle || state == ZeldaAiState.Recovery) ClearWitnessedTarget();
        if (state == ZeldaAiState.Idle) warningElapsed = 0;
    }
    public override void OnCharacterDamagedBy(ZeldaCharacterData attacker)
    {
        if (!isActiveAndEnabled || IsStunned || IsPossessionLocked || IsEchoRecoveryFrame ||
            turret == null || turret.IsDead) return;
        var target = attacker != null ? attacker.GetComponent<ZeldaFourWayMover>() : null;
        if (ValidTarget(target) && CanSeeCharacter(target)) RememberWitnessedTarget(target);
    }
    public override void OnPlayerAttackedCharacter(ZeldaCharacterData attacker, ZeldaCharacterData victim)
    {
        if (attacker == null || victim == null || attacker == victim ||
            victim == turret || attacker == turret || attacker.IsGhostLike) return;
        var victimAi = victim.GetComponent<ZeldaCharacterAiBase>();
        if (victimAi != null && !victimAi.SharesHostilityWithOtherNpcs) return;
        if (ZeldaRuntimeRegistry.GetGameplayScene(victim.gameObject) != ZeldaRuntimeRegistry.GetGameplayScene(gameObject)) return;
        TryWitnessPlayerOffence(attacker);
    }
    public override void OnPlayerPlacedBomb(ZeldaCharacterData user)
    {
        if (CurrentState != ZeldaAiState.Hostile) TryWitnessPlayerOffence(user);
    }
    public override bool OnPlayerDestroyedDoor(ZeldaCharacterData attacker) =>
        TryWitnessPlayerOffence(attacker, true);
    public override void OnPossessionWitnessed(ZeldaFourWayMover from, ZeldaFourWayMover to)
    {
        // Match ordinary NPC witnesses: neither ghosts nor temporary ghost form
        // reveal a possession incident, even when the new body is visible.
        if (from == null || from.CharacterData == null || from.CharacterData.IsGhostLike ||
            IsProtectedCharacter(from.CharacterData)) return;
        if (CanWitnessOffence && ValidTarget(to) &&
            (witnessedTarget == from || CanSeeCharacter(from) || CanSeeCharacter(to)))
            RememberWitnessedTarget(to);
    }
    public override void InvestigatePosition(Vector2 position) { }
    public override void InvestigateHandgunShot(Vector2 position) { }
    [Serializable] public sealed class PersistentState
    {
        public string bound, target, witnessed;
        public bool followPlayer;
        public float warning, idle, angle, burst, shot;
        public int remaining;
    }
    public PersistentState CaptureTurretState() => new PersistentState {
        bound = !string.IsNullOrEmpty(boundId) ? boundId : protectedCharacter != null ? protectedCharacter.IndividualAccessId : null,
        target = forcedTarget != null ? forcedTarget.CharacterData.IndividualAccessId : forcedTargetId,
        witnessed = witnessedTarget != null ? witnessedTarget.CharacterData.IndividualAccessId : witnessedTargetId,
        followPlayer = followPossessingPlayer, warning = warningElapsed, idle = idleTimer, angle = idleAngle,
        burst = burstTimer, shot = shotTimer, remaining = burstRemaining };
    public void ApplyTurretState(PersistentState state)
    {
        if (state == null) return;
        Unsubscribe(); protectedCharacter = null; forcedTarget = null; witnessedTarget = null;
        boundId = state.bound; forcedTargetId = state.target; followPossessingPlayer = state.followPlayer;
        witnessedTargetId = state.witnessed;
        warningElapsed = state.warning; idleTimer = state.idle; idleAngle = state.angle;
        burstTimer = state.burst; shotTimer = state.shot; burstRemaining = state.remaining; resolveTimer = 0f;
    }
}
