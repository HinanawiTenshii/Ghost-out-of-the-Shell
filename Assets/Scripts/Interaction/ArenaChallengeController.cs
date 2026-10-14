using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Scene-authored, body-bound nonlethal tournament. Only the arena layout is rolled back.</summary>
[DisallowMultipleComponent, DefaultExecutionOrder(-250)]
[RequireComponent(typeof(CameraVisionStreamingExempt))]
public sealed class ArenaChallengeController : MonoBehaviour
{
    public enum Phase { Idle, Preparing, Fighting, Result, Completed }
    [Serializable] public sealed class RuntimeState
    {
        public Phase phase;
        public string entrantId, winnerId, layoutSnapshot;
        public Vector3 returnPosition;
        public int[] roster;
        public int wins;
        public bool claimed, lastWon, pendingLayout, returning;
        public bool unclaimedWinnerDied;
        public float resultRemaining;
        public bool hasPotion;
        public AlchemyPotionKind potion;
    }

    [Header("Arena / 竞技场（世界坐标）")]
    public Rect region = new Rect(64.8f, -16f, 45.5f, 30f);
    public Rect fightingArea = new Rect(76f, -7.25f, 23.5f, 14.5f);
    public Vector2 guardianPosition = new Vector2(95.5f, 0f);
    public CharacterDialogue registrar;
    public ZeldaCharacterData[] guardians = new ZeldaCharacterData[3];
    public ZeldaCharacterAiBase[] residents = new ZeldaCharacterAiBase[0];
    public TemporaryCharacterEffectArea playerPool, guardianPool;
    public DoorHingeInteraction entrance;
    public DoorHingeInteraction[] sealedDoors = new DoorHingeInteraction[0];
    public ArenaScoreboard scoreboard;
    public TrialProofPickupItem reward;
    [Min(.2f)] public float resultPause = 3f;
    [Header("Round end / 回合结束铃声")]
    public AudioClip roundEndSound;
    [Range(0f, 1f)] public float roundEndSoundVolume = .55f;

    private RuntimeState state = new RuntimeState();
    private readonly HashSet<ZeldaCharacterAiBase> residentSet = new HashSet<ZeldaCharacterAiBase>();
    private readonly HashSet<DoorHingeInteraction> doorSet = new HashSet<DoorHingeInteraction>();
    private readonly HashSet<DoorData> doorPanels = new HashSet<DoorData>();
    private ZeldaFourWayMover entrant;
    private bool needsRestore, transitionRequested;
    private AudioSource roundEndVoice;
    private bool roundEndVoicePaused;
    private static readonly List<ArenaChallengeController> active = new List<ArenaChallengeController>();
    public Phase CurrentPhase => state.phase;
    public int Wins => state.wins;
    public int RoundCount => state.roster != null ? state.roster.Length : 0;
    public bool ChallengeActive => state.phase == Phase.Preparing || state.phase == Phase.Fighting || state.phase == Phase.Result || state.returning;
    public ZeldaCharacterData Opponent => state.roster != null && state.wins < state.roster.Length &&
        state.roster[state.wins] >= 0 && state.roster[state.wins] < guardians.Length ? guardians[state.roster[state.wins]] : null;
    public ZeldaFourWayMover Entrant => entrant;
    public float VisionRadius => fightingArea.size.magnitude + 2f;
    public bool CombatRunning => state.phase == Phase.Fighting && !state.pendingLayout && !transitionRequested;
    internal bool HasAvailableQuest => state.phase == Phase.Idle && !state.claimed &&
        !QuestJournalManager.GetOrCreate().ContainsEntry(QuestJournalManager.ArenaQuestId) &&
        !QuestJournalManager.GetOrCreate().IsEntryCompleted(QuestJournalManager.ArenaQuestId);
    public static bool BlocksPlayerInput
    {
        get { foreach (var arena in active) if (arena != null && arena.ChallengeActive &&
            (arena.state.phase == Phase.Result || arena.state.pendingLayout || arena.transitionRequested)) return true; return false; }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => active.Clear();
    private void Awake()
    {
        if (roundEndSound == null) roundEndSound = Resources.Load<AudioClip>("Audio/ArenaRoundEndBell");
        foreach (var ai in residents) if (ai != null) residentSet.Add(ai);
        foreach (var guardian in guardians) if (guardian != null && guardian.TryGetComponent(out ZeldaCharacterAiBase ai)) residentSet.Add(ai);
        foreach (var door in sealedDoors) if (door != null) doorSet.Add(door);
        if (entrance != null) doorSet.Add(entrance);
        foreach (var door in doorSet) foreach (var panel in door.GetComponentsInChildren<DoorData>(true)) doorPanels.Add(panel);
    }
    private void OnEnable() { if (!active.Contains(this)) active.Add(this); }
    private void OnDisable() { active.Remove(this); StopRoundEndSound(); }

    public string DialogueNode(ZeldaFourWayMover actor)
    {
        bool winner = actor != null && actor.CharacterData != null && actor.CharacterData.IndividualAccessId == state.winnerId;
        if (state.claimed) return winner ? "arena_claimed" : "arena_closed";
        if (ChallengeActive) return "arena_prepare";
        if (state.phase != Phase.Completed) return state.unclaimedWinnerDied ? "arena_retry" : "arena_start";
        return winner ? "arena_victory" : "arena_other";
    }
    public bool CanRegister(ZeldaFourWayMover actor, out string reason)
    {
        reason = null;
        if (state.claimed) reason = "试炼证明已经颁发，挑战不再开放。";
        else if (ChallengeActive) reason = "本次挑战尚未结束。";
        else if (state.phase == Phase.Completed) reason = "请等待获胜的挑战者本人领取证明。";
        else if (actor == null || actor.CharacterData == null || actor.CharacterData.IsDead || actor.CharacterData.IsGhostLike) reason = "需要以实体角色报名。";
        else if (actor.CharacterData.CurrentHealth <= 1) reason = "请先在恢复池恢复体力。";
        else if (entrance == null || playerPool == null || guardianPool == null || reward == null) reason = "竞技场配置尚未完成。";
        else
        {
            bool any = false;
            foreach (var guard in guardians) if (guard != null && !guard.IsDead && guard != actor.CharacterData) any = true;
            if (!any) reason = "目前没有可以出场的守护者。";
        }
        return reason == null;
    }
    public bool Register(ZeldaFourWayMover actor)
    {
        if (!CanRegister(actor, out _)) return false;
        var roster = new List<int>();
        for (int i = 0; i < guardians.Length; i++)
            if (guardians[i] != null && !guardians[i].IsDead && guardians[i] != actor.CharacterData) roster.Add(i);
        bool poison = guardianPool.AppliesPotion && guardianPool.PotionKind == AlchemyPotionKind.Death;
        if (poison && roster.Count > 0) roster.RemoveAt(roster.Count - 1);
        // Capture before modifying doors, AI, positions or the tournament state.
        string snapshot = SceneTravelStateManager.GetOrCreate().CaptureArenaLayout(this, actor);
        var previousState = state;
        var previousEntrant = entrant;
        state = new RuntimeState {
            phase = Phase.Preparing, entrantId = actor.CharacterData.IndividualAccessId,
            returnPosition = actor.transform.position, roster = roster.ToArray(), layoutSnapshot = snapshot,
            hasPotion = guardianPool.AppliesPotion && !poison, potion = guardianPool.PotionKind
        };
        entrant = actor;
        if (roster.Count == 0) { state.winnerId = state.entrantId; state.phase = Phase.Completed; }
        if (RequestLayout(roster.Count == 0)) return true;
        // A rejected load is not a registration and must not create a quest or
        // strand the participant in a preparation layout that was never loaded.
        state = previousState; entrant = previousEntrant;
        return false;
    }
    public bool Abandon()
    {
        if (!ChallengeActive || state.phase == Phase.Result || transitionRequested) return false;
        state.phase = Phase.Idle;
        return RequestLayout(true);
    }
    public bool CanClaim(ZeldaFourWayMover actor, out string reason)
    {
        reason = null;
        if (state.phase != Phase.Completed || state.claimed || actor == null || actor.CharacterData == null || actor.CharacterData.IsDead || actor.CharacterData.IsGhostLike ||
            actor.CharacterData.IndividualAccessId != state.winnerId) { reason = "证明只授予完成挑战的参赛者。"; return false; }
        if (PersistentInventory.Instance == null || reward == null) { reason = "无法领取证明。"; return false; }
        for (int i = 0; i < PersistentInventory.Instance.SlotCount; i++)
            if (PersistentInventory.Instance.GetSlot(i).IsEmpty) return true;
        reason = "物品栏空间不足。"; return false;
    }
    public bool Claim(ZeldaFourWayMover actor)
    {
        if (!CanClaim(actor, out _)) return false;
        state.claimed = true; // Commit before inventory notifications can save.
        if (PersistentInventory.Instance.TryAddUniqueItem(reward.ItemId, Guid.NewGuid().ToString("N"),
            1, reward.MaxStackSize, reward.ItemName, reward.ItemDescription))
        { QuestJournalManager.GetOrCreate().RecordArenaQuestProgress(state); return true; }
        state.claimed = false; return false;
    }

    private bool RequestLayout(bool restore)
    {
        if (transitionRequested) return false;
        state.pendingLayout = true;
        state.returning = restore;
        bool started = GameSaveSystem.Instance != null && GameSaveSystem.Instance.ReloadArenaLayout(this, restore);
        transitionRequested = started;
        // The covered loader captures the journal after its reveal animation.
        // Commit progression now, so arena rollback never restores an old task.
        if (started) QuestJournalManager.GetOrCreate().RecordArenaQuestProgress(state);
        if (!started)
        {
            state.pendingLayout = false; state.returning = false;
            ZeldaHealthHeartsUI.Instance?.ShowNotificationPopup("竞技场载入未能开始，请稍后再试。");
        }
        return started;
    }
    public RuntimeState CaptureRuntimeState() => JsonUtility.FromJson<RuntimeState>(JsonUtility.ToJson(state));
    public void CancelLayoutLoad()
    {
        transitionRequested = false; state.pendingLayout = false;
        if (state.returning) { state.returning = false; state.phase = Phase.Preparing; }
        QuestJournalManager.GetOrCreate().RecordArenaQuestProgress(state);
    }
    public void ApplyRuntimeState(RuntimeState value)
    {
        StopRoundEndSound(); // Restoring a result is not a new round-ending event.
        // Do not let live dialogue/reward changes mutate a loaded restart checkpoint.
        state = value != null ? JsonUtility.FromJson<RuntimeState>(JsonUtility.ToJson(value)) : new RuntimeState();
        transitionRequested = false; needsRestore = true;
    }
    public static bool ReopenForWinnerDeath(RuntimeState saved, string identity)
    {
        if (saved == null || saved.claimed || saved.phase != Phase.Completed || string.IsNullOrEmpty(identity) || saved.winnerId != identity) return false;
        saved.phase = Phase.Idle;
        saved.winnerId = null;
        saved.wins = 0;
        saved.roster = null;
        saved.unclaimedWinnerDied = true;
        return true;
    }
    public static void NotifyWinnerDeath(string identity)
    {
        foreach (var arena in active)
            if (arena != null && ReopenForWinnerDeath(arena.state, identity)) arena.RefreshScore();
    }
    public void CompleteRestoration()
    {
        if (!needsRestore) { RefreshScore(); return; }
        needsRestore = false;
        entrant = ZeldaRuntimeRegistry.GetControlledMover();
        if (state.pendingLayout)
        {
            if (state.returning)
            {
                if (MatchesEntrant(entrant)) Teleport(entrant, state.returnPosition);
                state.layoutSnapshot = null; state.entrantId = null;
                state.returning = false; state.pendingLayout = false;
            }
            else
            {
                state.pendingLayout = false;
                state.phase = Phase.Preparing;
                if (MatchesEntrant(entrant)) Teleport(entrant, playerPool.transform.position);
                ConfigureDoors();
                var guard = Opponent;
                if (guard != null)
                {
                    CameraVisionObjectStreaming.RestoreForGameplay(guard.gameObject);
                    guard.gameObject.SetActive(true);
                    Teleport(guard.GetComponent<ZeldaFourWayMover>(), guardianPosition);
                    guard.GetComponent<ZeldaCharacterAiBase>()?.ResetArenaRound();
                    // Poison has already reduced the roster, never kill a competitor here.
                    if (state.hasPotion) CharacterPotionBuffs.ApplyExtendedSplash(guard, state.potion, 2f);
                }
            }
        }
        else if (ChallengeActive) ConfigureDoors();
        RefreshScore();
    }
    public static void FinalizeSceneRestoration()
    { foreach (var arena in active) if (arena != null) arena.CompleteRestoration(); }
    public static bool HasControllerInScene(int sceneHandle)
    { foreach (var arena in active) if (arena != null && arena.gameObject.scene.handle == sceneHandle) return true; return false; }

    private void Update()
    {
        RefreshRoundEndSoundPause();
        if (CharacterPotionBuffs.IsRestoringWorld) return;
        if (needsRestore) CompleteRestoration();
        if (!ChallengeActive || transitionRequested || state.pendingLayout || DocumentReader.IsDocumentOpen || Time.timeScale <= 0f) return;
        var player = ZeldaRuntimeRegistry.GetControlledMover();
        if (!MatchesEntrant(player))
        {
            // A control transfer cannot carry another body into the same registration.
            state.phase = Phase.Idle; RequestLayout(true); return;
        }
        entrant = player;
        if (state.phase == Phase.Result)
        {
            StopBody(entrant);
            state.resultRemaining = Mathf.Max(0f, state.resultRemaining - Time.deltaTime);
            if (state.resultRemaining > 0f || RetroSceneLoadReveal.IsBlockingInput) return;
            if (!state.lastWon) { state.phase = Phase.Idle; RequestLayout(true); }
            else if (state.wins >= RoundCount)
            { state.phase = Phase.Completed; state.winnerId = state.entrantId; RequestLayout(true); }
            else { state.phase = Phase.Preparing; RequestLayout(false); }
            return;
        }
        if (!region.Contains(entrant.transform.position)) { state.phase = Phase.Idle; RequestLayout(true); return; }
        if (state.phase == Phase.Preparing && fightingArea.Contains(entrant.transform.position))
        {
            state.phase = Phase.Fighting;
            ConfigureDoors();
            RefreshScore();
        }
        if (state.phase == Phase.Fighting)
        {
            if (entrant.CharacterData.CurrentHealth <= 1) FinishRound(false);
            else if (Opponent == null || Opponent.IsDead || Opponent.CurrentHealth <= 1) FinishRound(true);
        }
    }
    private bool MatchesEntrant(ZeldaFourWayMover actor) => actor != null && actor.CharacterData != null &&
        !actor.CharacterData.IsDead && actor.CharacterData.IndividualAccessId == state.entrantId;
    private void ConfigureDoors()
    {
        foreach (var door in sealedDoors) if (door != null) door.ApplyPersistentState(true, false);
        if (entrance != null) entrance.ApplyPersistentState(state.phase != Phase.Preparing, state.phase == Phase.Preparing);
    }
    private void FinishRound(bool won)
    {
        if (state.phase != Phase.Fighting) return;
        var opponent = Opponent;
        state.phase = Phase.Result; state.lastWon = won; state.resultRemaining = resultPause;
        // Keep the current roster index throughout the result pause; increment after caching the combatant.
        if (won) state.wins++;
        StopBody(entrant);
        if (opponent != null) StopBody(opponent.GetComponent<ZeldaFourWayMover>());
        RefreshScore();
        PlayRoundEndSound();
    }
    private void PlayRoundEndSound()
    {
        if (roundEndSound == null || roundEndSoundVolume <= 0f) return;
        if (roundEndVoice == null)
        {
            roundEndVoice = gameObject.AddComponent<AudioSource>();
            roundEndVoice.playOnAwake = false;
            roundEndVoice.loop = false;
            roundEndVoice.spatialBlend = 0f; // Round feedback stays audible across the arena.
            roundEndVoice.dopplerLevel = 0f;
            roundEndVoice.priority = 80;
            roundEndVoice.ignoreListenerPause = false;
        }
        // Both strikes are in one clip: no delayed callback can survive a reload.
        roundEndVoice.clip = roundEndSound;
        roundEndVoice.volume = Mathf.Clamp01(roundEndSoundVolume);
        roundEndVoice.pitch = 1f;
        roundEndVoicePaused = false;
        roundEndVoice.Play();
        RefreshRoundEndSoundPause();
    }
    private void RefreshRoundEndSoundPause()
    {
        if (roundEndVoice == null) return;
        bool pause = Time.timeScale <= 0f || AudioListener.pause || GameSaveSystem.IsLoading;
        if (pause == roundEndVoicePaused) return;
        if (pause) roundEndVoice.Pause(); else roundEndVoice.UnPause();
        roundEndVoicePaused = pause;
    }
    private void StopRoundEndSound()
    {
        if (roundEndVoice != null) roundEndVoice.Stop();
        roundEndVoicePaused = false;
    }
    private void RefreshScore()
    {
        QuestJournalManager.GetOrCreate().RecordArenaQuestProgress(state);
        if (scoreboard == null) return;
        if (state.phase == Phase.Result && !state.lastWon) scoreboard.Display("×", new Color(1f,.15f,.15f));
        else if (state.phase == Phase.Completed) scoreboard.Display("挑战完成  " + state.wins, ZeldaUiPalette.Primary);
        else if (ChallengeActive) scoreboard.Display("击败  " + state.wins + " / " + RoundCount, ZeldaUiPalette.Primary);
        else scoreboard.Display("等待挑战者", ZeldaUiPalette.Primary);
    }
    private static void StopBody(ZeldaFourWayMover body)
    {
        if (body == null) return;
        body.CancelAttackForStun();
        if (body.TryGetComponent(out Rigidbody2D rb)) { rb.velocity = Vector2.zero; rb.angularVelocity = 0f; }
    }
    private static void Teleport(ZeldaFourWayMover body, Vector3 position)
    {
        if (body == null) return;
        StopBody(body);
        position.z = body.transform.position.z;
        body.transform.position = position;
        if (body.TryGetComponent(out Rigidbody2D rb)) rb.position = position;
        Physics2D.SyncTransforms();
    }
    public static ArenaChallengeController ForAi(ZeldaCharacterAiBase ai)
    {
        foreach (var arena in active) if (arena != null && arena.ChallengeActive && arena.residentSet.Contains(ai)) return arena;
        return null;
    }
    public bool FreezesAi(ZeldaCharacterAiBase ai) => !CombatRunning || Opponent == null || ai.gameObject != Opponent.gameObject;
    public static bool BlocksDoor(DoorHingeInteraction door)
    {
        foreach (var arena in active) if (arena != null && arena.ChallengeActive && arena.doorSet.Contains(door))
            return door != arena.entrance || arena.state.phase != Phase.Preparing;
        return false;
    }
    public static bool ProtectsDoor(DoorData door)
    { foreach (var arena in active) if (arena != null && arena.ChallengeActive && arena.doorPanels.Contains(door)) return true; return false; }
    public static bool BlocksTransition(Component target)
    {
        foreach (var arena in active) if (arena != null && arena.ChallengeActive && target.gameObject.scene == arena.gameObject.scene &&
            arena.region.Contains(target.transform.position)) return true;
        return false;
    }
    private bool IsCombatant(ZeldaCharacterData data) => data != null &&
        (data.IndividualAccessId == state.entrantId || Array.IndexOf(guardians, data) >= 0);
    public static int LimitDamage(ZeldaCharacterData data, int damage)
    {
        foreach (var arena in active)
        {
            if (arena == null || !arena.ChallengeActive || !arena.IsCombatant(data)) continue;
            if (arena.state.phase == Phase.Result || arena.state.pendingLayout || arena.transitionRequested) return 0;
            if (data != null && data != (arena.entrant != null ? arena.entrant.CharacterData : null) && !arena.CombatRunning) return 0;
            return Mathf.Min(damage, Mathf.Max(0, data.CurrentHealth - 1));
        }
        return damage;
    }
    public static bool PreventDeath(ZeldaCharacterData data)
    {
        foreach (var arena in active) if (arena != null && arena.ChallengeActive && arena.IsCombatant(data)) return true;
        return false;
    }
    public static void HealthChanged(ZeldaCharacterData data)
    {
        if (data.CurrentHealth > 1) return;
        foreach (var arena in active) if (arena != null && arena.CombatRunning &&
            (data == arena.Opponent || data.IndividualAccessId == arena.state.entrantId))
        { arena.FinishRound(data.IndividualAccessId != arena.state.entrantId); return; }
    }
    public static bool IsSanctionedAttack(ZeldaCharacterData attacker, ZeldaCharacterData victim)
    {
        foreach (var arena in active) if (arena != null && arena.ChallengeActive &&
            arena.IsCombatant(attacker) && arena.IsCombatant(victim)) return true;
        return false;
    }
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan; Gizmos.DrawWireCube(region.center, region.size);
        Gizmos.color = Color.red; Gizmos.DrawWireCube(fightingArea.center, fightingArea.size);
        Gizmos.DrawWireSphere(guardianPosition, .4f);
    }
}
