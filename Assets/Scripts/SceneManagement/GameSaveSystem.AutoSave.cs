using UnityEngine;
using UnityEngine.SceneManagement;

public sealed partial class GameSaveSystem
{
    [Header("Automatic checkpoints / 自动存档")]
    [SerializeField, Min(5f)] private float ordinaryAutoSaveCooldown = 30f;
    [SerializeField, Min(1f)] private float autoSaveSafeSeconds = 5f;
    [SerializeField, Min(.5f)] private float autoSaveSettleSeconds = 2f;
    private const float SafetyPollSeconds = .5f;
    private float autoPlayClock, lastAutoSaveClock, nextSafetyPoll, safeSince = -1f;
    private float lastAutoRequest, nextAutoRetry, damageHoldUntil;
    private bool autoProgressDirty, autoSaveRequested, importantAutoSave;
    private bool autoSaveCompletionPending;
    private ZeldaFourWayMover autoObservedMover;
    private Vector2 autoObservedPosition;
    private int autoObservedHealth, autoObservedShieldVersion;

    // Requests are deliberately non-transactional: item transfer, world removal,
    // quest progress and the reward may all be committed later in the same frame.
    public static void RequestAutoSave(string reason, bool important = false)
    {
        var save = Instance;
        if (save == null || !save.AcceptAutoSaveProgress()) return;
        save.autoProgressDirty = true;
        save.autoSaveRequested = true;
        save.importantAutoSave |= important;
        save.lastAutoRequest = save.autoPlayClock;
    }

    public static void MarkAutoSaveProgress()
    {
        // Progress alone never schedules a checkpoint. Only an explicit event
        // request may commit it; ordinary movement/items have no timed fallback.
        var save = Instance;
        if (save != null && save.AcceptAutoSaveProgress()) save.autoProgressDirty = true;
    }

    internal static void NotifyAutoSaveTransientAction(Component action)
    {
        var save = Instance;
        if (save == null || action == null || !save.AcceptAutoSaveProgress() ||
            ZeldaRuntimeRegistry.GetGameplayScene(action.gameObject) != SceneManager.GetActiveScene()) return;
        // Very short hitboxes can appear and disappear between safety polls.
        save.safeSince = -1f;
        save.autoProgressDirty = true;
    }

    private bool AcceptAutoSaveProgress()
    {
        if (!Application.isPlaying || loading || writing || restoreFailed ||
            RuntimeUiVisibility.IsPauseOnlyScene || LevelSettlementScreen.IsOpen ||
            DevlogDemoSceneSettings.SuppressesAutomaticSaves(SceneManager.GetActiveScene()) ||
            IsTitle(SceneManager.GetActiveScene().name)) return false;
        var travel = SceneTravelStateManager.Instance;
        return travel == null || (!travel.IsSceneTravelRestoreInProgress && !travel.IsQuickRestartPending);
    }

    private void ResetAutoSaveSession()
    {
        autoPlayClock = lastAutoSaveClock = nextSafetyPoll = lastAutoRequest = nextAutoRetry = damageHoldUntil = 0f;
        safeSince = -1f;
        autoProgressDirty = autoSaveRequested = importantAutoSave = false;
        autoSaveCompletionPending = false;
        autoObservedMover = null;
    }

    private void AutoSaveCommitted()
    {
        lastAutoSaveClock = autoPlayClock;
        autoProgressDirty = autoSaveRequested = importantAutoSave = false;
        nextAutoRetry = 0f;
    }

    private bool IsAutoSaveGameplayReady()
    {
        return AcceptAutoSaveProgress() && !preparingSceneSave && !RetroSceneLoadReveal.IsPreparingGameplay &&
            !RetroSceneLoadReveal.IsBlockingInput && Time.timeScale > 0f && !DocumentReader.IsInputBlocked &&
            !ClockworkPuppetRuntime.BlocksCharacterInput;
    }

    private void TickAutoSave()
    {
        if (!IsAutoSaveGameplayReady()) { safeSince = -1f; return; }
        // This clock only measures event cooldowns, settling and safety windows.
        // Elapsed time alone must never create a new automatic-save request.
        autoPlayClock += Mathf.Min(Time.unscaledDeltaTime, .5f);
        var player = ZeldaRuntimeRegistry.GetControlledMover();
        if (player == null || player.CharacterData == null) { safeSince = -1f; return; }
        var data = player.CharacterData;
        if (autoObservedMover != player)
        {
            // Unity's destroyed-object null must not hide a possession switch.
            if (!ReferenceEquals(autoObservedMover, null)) autoProgressDirty = true;
            autoObservedMover = player;
            autoObservedPosition = player.transform.position;
            autoObservedHealth = data.CurrentHealth;
            autoObservedShieldVersion = data.HolyShieldBlockVersion;
            safeSince = -1f;
        }
        if (data.CurrentHealth < autoObservedHealth || data.HolyShieldBlockVersion != autoObservedShieldVersion)
        {
            damageHoldUntil = autoPlayClock + 8f;
            safeSince = -1f;
            autoProgressDirty = true;
        }
        autoObservedHealth = data.CurrentHealth;
        autoObservedShieldVersion = data.HolyShieldBlockVersion;
        if (((Vector2)player.transform.position - autoObservedPosition).sqrMagnitude >= 9f)
        {
            autoObservedPosition = player.transform.position;
            autoProgressDirty = true;
        }
        if (player.IsAttacking || player.IsPossessionInProgress || data.IsDead || data.IsGhostLike)
        { safeSince = -1f; return; }
        // No pending event: skip the AI/projectile/bomb safety registry queries.
        if (!autoSaveRequested || !autoProgressDirty) { safeSince = -1f; return; }
        // Fixed low-frequency registry queries, no scene scans or disk reads in Update.
        if (autoPlayClock < nextSafetyPoll) return;
        nextSafetyPoll = autoPlayClock + SafetyPollSeconds;
        if (!IsAutomaticSaveSafe(player)) { safeSince = -1f; return; }
        if (safeSince < 0f) safeSince = autoPlayClock;
        if (!autoSaveRequested || autoPlayClock < nextAutoRetry || !CanCommitAutomaticSave()) return;
        // Important milestones skip the ordinary 30-second throttle, never safety.
        if (!importantAutoSave && autoPlayClock - lastAutoSaveClock < ordinaryAutoSaveCooldown) return;
        if (autoPlayClock - lastAutoRequest < autoSaveSettleSeconds) return;
        using (SceneLoadMetrics.Measure("Save.Automatic"))
        {
            if (!SaveCore(0, false)) nextAutoRetry = sessionBound ? autoPlayClock + 30f : float.PositiveInfinity;
        }
    }

    private void TickAutoSaveCompletionNotification()
    {
        if (!autoSaveCompletionPending) return;
        if (IsTitle(SceneManager.GetActiveScene().name) || RuntimeUiVisibility.IsPauseOnlyScene ||
            LevelSettlementScreen.IsOpen)
        { autoSaveCompletionPending = false; return; }
        // Entry saves finish under the loading curtain. Keep the notice until
        // gameplay is visible, instead of letting its fade elapse offscreen.
        if (loading || preparingSceneSave || RetroSceneLoadReveal.IsPreparingGameplay ||
            RetroSceneLoadReveal.IsBlockingInput || Time.timeScale <= 0f || DocumentReader.IsInputBlocked ||
            ZeldaHealthHeartsUI.Instance == null || !ZeldaHealthHeartsUI.Instance.gameObject.activeInHierarchy) return;
        autoSaveCompletionPending = false;
        Message("自动存档已完成");
    }

    private bool CanCommitAutomaticSave()
    {
        return IsAutoSaveGameplayReady() && safeSince >= 0f && autoPlayClock - safeSince >= autoSaveSafeSeconds &&
            IsAutomaticSaveSafe(ZeldaRuntimeRegistry.GetControlledMover());
    }

    private bool IsAutomaticSaveSafe(ZeldaFourWayMover player)
    {
        if (player == null || player.CharacterData == null || player.CharacterData.IsDead ||
            player.CharacterData.IsGhostLike || player.IsAttacking || player.IsPossessionInProgress ||
            (player.CharacterData.CurrentHealth <= 1 && player.CharacterData.Health > 1) ||
            player.CharacterData.IsShowingDamageFeedback || autoPlayClock < damageHoldUntil ||
            ArenaChallengeController.BlocksAutomaticSave || WaterwayGateTargetFade.BlocksAutomaticSave ||
            TrialProofAltar.BlocksAutomaticSave) return false;
        var scene = SceneManager.GetActiveScene();
        if (SharedPlayerHostilityTimer.TryGetForScene(scene, out var timer) &&
            timer.Phase != SharedPlayerHostilityTimer.IncidentPhase.Idle) return false;
        foreach (var ai in ZeldaRuntimeRegistry.AiCharacters)
        {
            if (ai != null && ZeldaRuntimeRegistry.GetGameplayScene(ai.gameObject) == scene &&
                ai.BlocksAutomaticSaveFor(player)) return false;
        }
        if (AutoSaveTransientActivity.HasActivity(scene)) return false;
        foreach (var bomb in PlacedBomb.ActiveBombs)
        {
            if (bomb == null || !bomb.IsArmed || ZeldaRuntimeRegistry.GetGameplayScene(bomb.gameObject) != scene) continue;
            var bounds = bomb.ExplosionBounds;
            // Also avoid saving immediately beside a dormant proximity/decoy bomb.
            bounds.Expand(new Vector3(4f, 4f, 2f));
            if (bounds.Contains(player.transform.position)) return false;
        }
        foreach (var worm in SandwormZeldaCharacterData.ActiveSandworms)
        {
            if (worm == null || !worm.isActiveAndEnabled || ZeldaRuntimeRegistry.GetGameplayScene(worm.gameObject) != scene) continue;
            // The death sequence must finish spawning its persistent reward too.
            if (worm.IsDead) return false;
            var attack = worm.GetComponent<SandwormEruptionAttack>();
            if (attack != null && attack.isActiveAndEnabled && attack.Phase != SandwormEruptionAttack.AttackPhase.Roaming) return false;
        }
        return true;
    }
}
