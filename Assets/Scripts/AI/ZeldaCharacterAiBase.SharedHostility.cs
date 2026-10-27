using UnityEngine;

public partial class ZeldaCharacterAiBase
{
    [Header("Shared Hostility Search Movement")]
    [SerializeField, Min(.1f), InspectorName("全局搜索到点停留时间"), Tooltip("仅在全局敌对后的搜索中生效；停留仍计入全局搜索总时间。")]
    private float sharedSearchWaypointPauseDuration = 2f;
    [SerializeField, Min(.1f), InspectorName("全局搜索最小移动距离"), Tooltip("从当前位置选取下一个搜索点的最短移动距离。空间不足时停留后重试，不缩短距离；不会改变声响吸引的调查。")]
    private float sharedSearchMinimumMoveDistance = 1.2f;
    private float sharedSearchPauseRemaining;
    private SharedPlayerHostilityTimer sharedPlayerTimer;
    private bool sharedReturnAfterStun;
    internal void SetSharedPlayerTimer(SharedPlayerHostilityTimer timer)
    {
        sharedPlayerTimer = timer;
        if (timer == null) sharedSearchPauseRemaining = 0f;
    }
    private bool UsesSharedSearchMovement => sharedPlayerTimer != null && UsesSharedPlayerTimer &&
        sharedPlayerTimer.Phase == SharedPlayerHostilityTimer.IncidentPhase.Search;

    private bool TickSharedSearchPause(float deltaTime)
    {
        if (!UsesSharedSearchMovement || sharedSearchPauseRemaining <= 0f) return false;
        moveDirection = Vector2.zero;
        sharedSearchPauseRemaining = Mathf.Max(0f, sharedSearchPauseRemaining - Mathf.Max(0f, deltaTime));
        if (sharedSearchPauseRemaining > 0f) return true;
        ChooseNextSearchWaypoint();
        return false;
    }
    private void BeginSharedSearchPause()
    {
        moveDirection = Vector2.zero;
        cornerEscapeTimer = cornerStuckTimer = 0f;
        cornerEscapeDirection = Vector2.zero;
        ResetCornerOscillationWindow(rb.position);
        sharedSearchPauseRemaining = Mathf.Max(.1f, sharedSearchWaypointPauseDuration);
        InvalidateNavigationPlan();
    }
    protected virtual bool UsesSharedPlayerTimer => !IsSilenceOathIncidentPrivate && ArenaChallengeController.ForAi(this) == null && !(this is AutomatonCharacterAi) && !(this is SkeletonCharacterAi) && !(this is TurretCharacterAi);
    private void SyncSharedPlayerIncident()
    {
        var player = ZeldaRuntimeRegistry.GetControlledMover();
        if (sharedPlayerTimer != null && !RetainsSharedPlayerIncident(player)) sharedPlayerTimer.Leave(this);
        if (!GameSaveSystem.IsLoading && UsesSharedPlayerTimer && isActiveAndEnabled && mover != null && !mover.enabled &&
            !IsIgnoringPlayer && !IsEchoRecoveryFrame && characterData != null && !characterData.IsDead &&
            player != null && targetMover == player && IsActivelyHostileTo(player) &&
            ZeldaRuntimeRegistry.GetGameplayScene(player.gameObject) == gameObject.scene)
            SharedPlayerHostilityTimer.ForScene(gameObject.scene).Join(this);
    }
    internal bool RetainsSharedPlayerIncident(ZeldaFourWayMover player)
    {
        return UsesSharedPlayerTimer && enabled && mover != null && !mover.enabled && characterData != null &&
            !characterData.IsDead && !IsIgnoringPlayer && !IsEchoRecoveryFrame && player != null && targetMover == player &&
            player.CharacterData != null && !player.CharacterData.IsDead &&
            ZeldaRuntimeRegistry.GetGameplayScene(player.gameObject) == gameObject.scene &&
            (currentState == ZeldaAiState.Hostile || currentState == ZeldaAiState.Search ||
                (IsStunned && (stateBeforeStun == ZeldaAiState.Hostile || stateBeforeStun == ZeldaAiState.Search)));
    }
    internal bool CanObserveSharedPlayer(ZeldaFourWayMover player)
    {
        // Independent creatures and guards occupied with them cannot refresh a player incident.
        bool fightingCreature = targetMover != null && targetMover != player &&
            (targetMover.GetComponent<SkeletonZeldaCharacterData>() != null ||
             targetMover.GetComponent<AutomatonZeldaCharacterData>() != null);
        return UsesSharedPlayerTimer && !fightingCreature && isActiveAndEnabled && mover != null && !mover.enabled && characterData != null && !characterData.IsDead &&
            !IsStunned && !IsIgnoringPlayer && !possessionLocked && !IsEchoRecoveryFrame &&
            (!(this is AutomatonCharacterAi automaton) || automaton.HostilityActivated) &&
            ZeldaRuntimeRegistry.GetGameplayScene(player.gameObject) == gameObject.scene && CanSeePlayer(player);
    }
    private bool TickSharedPlayerIncident()
    {
        if (sharedPlayerTimer == null || !RetainsSharedPlayerIncident(ZeldaRuntimeRegistry.GetControlledMover())) return false;
        ApplySharedPlayerPhase(sharedPlayerTimer.Phase, sharedPlayerTimer.LastSeenPosition);
        return true;
    }
    internal void ApplySharedPlayerPhase(SharedPlayerHostilityTimer.IncidentPhase phase, Vector2 position)
    {
        if (!isActiveAndEnabled || IsStunned || possessionLocked || IsEchoRecoveryFrame || IsIgnoringPlayer) return;
        lastKnownTargetPosition = position;
        if (phase == SharedPlayerHostilityTimer.IncidentPhase.Hostile)
        {
            if (currentState != ZeldaAiState.Hostile)
                BeginTemporaryHostility(ZeldaRuntimeRegistry.GetControlledMover(), true);
            lastKnownTargetPosition = position;
        }
        else if (phase == SharedPlayerHostilityTimer.IncidentPhase.Search && currentState != ZeldaAiState.Search)
        {
            mover.CancelAttackForStun();
            // BeginSearch captures this guard's current position once and samples
            // its own searchRadius. Only timing is shared, not the search centre.
            BeginSearch();
        }
    }
    internal void FinishSharedPlayerSearch()
    {
        if (IsStunned) { sharedReturnAfterStun = true; return; }
        if (mover != null && !mover.enabled && characterData != null && !characterData.IsDead)
        { mover.CancelAttackForStun(); BeginRecovery(); }
    }
    internal void EndSharedPlayerIncident(ZeldaFourWayMover player)
    {
        // Do not overwrite a new NPC-vs-NPC target or take over a possessed body.
        if (targetMover == null || targetMover == player || targetMover.CharacterData == null ||
            targetMover.CharacterData.IsDead || !targetMover.isActiveAndEnabled)
            FinishSharedPlayerSearch();
    }
    private void RestoreSharedPlayerIncident(SaveState state)
    {
        sharedSearchPauseRemaining = 0f;
        if (sharedPlayerTimer != null) sharedPlayerTimer.Leave(this);
        sharedReturnAfterStun = state.sharedReturnAfterStun;
        if (!RetainsSharedPlayerIncident(ZeldaRuntimeRegistry.GetControlledMover())) return;
        if (state.sharedPlayerPhase > 0)
            SharedPlayerHostilityTimer.ForScene(gameObject.scene).Restore(this, state.sharedPlayerPhase,
                state.sharedPlayerRemaining, state.sharedPlayerLastSeen);
        else if (IsActivelyHostileTo(targetMover))
        {
            var timer = SharedPlayerHostilityTimer.ForScene(gameObject.scene);
            timer.Restore(this, 1, timer.HostileDuration, lastKnownTargetPosition);
        }
        // Missing in older saves => zero: resume the current leg, then use the
        // new cadence. New saves preserve a partially completed pause exactly.
        sharedSearchPauseRemaining = UsesSharedSearchMovement &&
            (currentState == ZeldaAiState.Search || (IsStunned && stateBeforeStun == ZeldaAiState.Search))
            ? Mathf.Clamp(state.sharedSearchPauseRemaining, 0f, Mathf.Max(.1f, sharedSearchWaypointPauseDuration)) : 0f;
        if (sharedSearchPauseRemaining > 0f)
        {
            cornerEscapeTimer = cornerStuckTimer = 0f;
            cornerEscapeDirection = Vector2.zero;
        }
    }
}
