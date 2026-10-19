using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Run-local, event-driven counters. A snapshot follows save/restore and arena layout reloads.</summary>
[DefaultExecutionOrder(12000)]
public sealed class LevelRunStatistics : MonoBehaviour
{
    [Serializable] public sealed class State
    {
        public int level = -1;
        public bool completeHistory = true, singleBodyFailed;
        public string firstBody, lastBody;
        public int switches, deaths, itemUses, warnings, hostilities, skillPoints, expansions;
        // Older saves lack these fields. Migrate them to an active, explicitly
        // partial timer instead of letting a missing flag disable time forever.
        public bool hasElapsedTime;
        public bool elapsedTimeIncomplete;
        public double elapsedSeconds;
        public List<string> skillIds = new List<string>(), expansionIds = new List<string>(), ownedBodies = new List<string>();
        public List<string> warningObservers = new List<string>(), hostileObservers = new List<string>();
    }
    private static LevelRunStatistics instance;
    private State state = new State();
    private readonly HashSet<string> warningObservers = new HashSet<string>(), hostileObservers = new HashSet<string>();
    private ZeldaCharacterData observedBody;
    public static State Current => GetOrCreate().state;
    public static bool Restoring => GameSaveSystem.IsLoading ||
        (SceneTravelStateManager.Instance != null && (SceneTravelStateManager.Instance.IsSceneTravelRestoreInProgress || SceneTravelStateManager.Instance.IsQuickRestartPending));
    private bool CanRecord => state.level >= 0 && !Restoring && !LevelSettlementScreen.IsOpen;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap() { GetOrCreate(); }
    private static LevelRunStatistics GetOrCreate()
    {
        if (instance == null) instance = new GameObject("Level Run Statistics").AddComponent<LevelRunStatistics>();
        return instance;
    }
    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this; DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += SceneLoaded;
    }
    private void OnDestroy() { SceneManager.sceneLoaded -= SceneLoaded; if(instance==this)instance=null; }
    private void Start()
    {
        // Direct Play-mode entry can initialize this late-ordered service after
        // the first sceneLoaded notification. Do not miss that level's timer.
        if(state.level<0 && !Restoring)
        {
            int level=LevelSettlementCatalog.LevelIndex(SceneManager.GetActiveScene().name);
            if(level>=0)ResetRun(level);
        }
    }
    private void SceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        if (Restoring)
        {
            // A floor change retains counters, not the previous floor's active observers.
            // Disk/checkpoint restoration rebuilds membership from its own saved snapshot.
            if(!GameSaveSystem.IsLoading && !SceneTravelStateManager.Instance.IsQuickRestartPending)
            { observedBody=null;warningObservers.Clear();hostileObservers.Clear(); }
            return;
        }
        int level = LevelSettlementCatalog.LevelIndex(scene.name);
        if (state.level != level) ResetRun(level);
        observedBody = null; warningObservers.Clear(); hostileObservers.Clear();
    }
    private void ResetRun(int level) { state = new State { level=level, hasElapsedTime=level>=0 }; observedBody=null; warningObservers.Clear(); hostileObservers.Clear(); }
    public static void ResetSession() { if(instance!=null)instance.ResetRun(-1); }
    public static State Capture()
    {
        var service=GetOrCreate();
        service.EnsureElapsedTimeRecording();
        service.state.warningObservers=new List<string>(service.warningObservers);
        service.state.hostileObservers=new List<string>(service.hostileObservers);
        return JsonUtility.FromJson<State>(JsonUtility.ToJson(service.state));
    }
    public static void Restore(State saved)
    {
        var service=GetOrCreate();
        service.state=saved != null ? JsonUtility.FromJson<State>(JsonUtility.ToJson(saved)) :
            new State { level=LevelSettlementCatalog.LevelIndex(SceneManager.GetActiveScene().name), completeHistory=false };
        service.EnsureElapsedTimeRecording();
        service.observedBody=null; service.warningObservers.Clear(); service.hostileObservers.Clear();
        if(service.state.warningObservers!=null)service.warningObservers.UnionWith(service.state.warningObservers);
        if(service.state.hostileObservers!=null)service.hostileObservers.UnionWith(service.state.hostileObservers);
    }
    private void LateUpdate()
    {
        if (!CanRecord || RetroSceneLoadReveal.IsBlockingInput) return;
        // Also repairs a live pre-timer state after an editor script reload.
        EnsureElapsedTimeRecording();
        // Real playing seconds, independent of slow-motion. Menus/dialogues use
        // timeScale=0; loading/restoring and the frozen report are gated above.
        if(state.hasElapsedTime && Time.timeScale>0f)
            state.elapsedSeconds+=Time.unscaledDeltaTime;
        var mover=ZeldaRuntimeRegistry.GetControlledMover();
        var body=mover != null ? mover.CharacterData : null;
        if(body==null || body==observedBody || body.IsDead ||
            ZeldaRuntimeRegistry.GetGameplayScene(body.gameObject)!=SceneManager.GetActiveScene())return;
        observedBody=body; RecordBody(body);
    }
    private void EnsureElapsedTimeRecording()
    {
        bool valid = !double.IsNaN(state.elapsedSeconds) && !double.IsInfinity(state.elapsedSeconds) && state.elapsedSeconds >= 0;
        if (!valid) state.elapsedSeconds = 0;
        if (state.level < 0 || (state.hasElapsedTime && valid)) return;
        state.hasElapsedTime = true;
        // Keep valid seconds, but never invent the missing earlier play time from
        // save timestamps (which also include pauses, retries and offline time).
        state.elapsedTimeIncomplete = true;
    }
    public static void RecordBody(ZeldaCharacterData body)
    {
        var service=GetOrCreate(); if(!service.CanRecord || body==null)return;
        var s=service.state;
        if(body is GhostZeldaCharacterData) { if(!string.IsNullOrEmpty(s.firstBody))s.singleBodyFailed=true; return; }
        string id=body.IndividualAccessId;
        if(!s.ownedBodies.Contains(id))s.ownedBodies.Add(id);
        if(string.IsNullOrEmpty(s.firstBody))s.firstBody=id;
        else if(s.lastBody!=id) { s.switches++; s.singleBodyFailed=true; }
        s.lastBody=id;
    }
    public static bool IsPlayerOwned(ZeldaCharacterData body)
    {
        if(body==null)return false;
        var mover=ZeldaRuntimeRegistry.GetControlledMover();
        if(mover!=null && mover.CharacterData==body)return true;
        if(instance==null)return false;
        if(instance.state.ownedBodies.Contains(body.IndividualAccessId))return true;
        var bond=body.GetComponent<BuilderTurretBond>();
        return bond!=null && instance.state.ownedBodies.Contains(bond.OwnerId);
    }
    public static void RecordDeath(ZeldaCharacterData body, bool causedByPlayer, bool abandonedBody)
    {
        var service=GetOrCreate();if(!service.CanRecord || body==null || body is GhostZeldaCharacterData || abandonedBody)return;
        if(IsPlayerOwned(body)) { service.state.singleBodyFailed=true; causedByPlayer=true; }
        if(causedByPlayer)service.state.deaths++;
    }
    public static void RecordItemUse() { var service=GetOrCreate();if(service.CanRecord)service.state.itemUses++; }
    public static void RecordSkill(GrowthCollectible item, int amount)
    {
        var service=GetOrCreate();if(!service.CanRecord||item==null)return;
        string id=item.gameObject.scene.name+":"+SceneTravelStateManager.GetMapObjectId(item.transform);
        if(service.state.skillIds.Contains(id))return;
        service.state.skillIds.Add(id);service.state.skillPoints+=Mathf.Max(1,amount);
    }
    public static void RecordExpansion(PickupItemBase item)
    {
        var service=GetOrCreate();if(!service.CanRecord || !(item is MemoPickupItem))return;
        string id=item.UniqueInstanceId;
        if(service.state.expansionIds.Contains(id))return;
        service.state.expansionIds.Add(id);service.state.expansions++;
    }
    public static void ForgetObserver(ZeldaCharacterAiBase observer)
    {
        if(instance==null||observer==null||Restoring)return;
        var body=observer.GetComponent<ZeldaCharacterData>();if(body==null)return;
        instance.warningObservers.Remove(body.IndividualAccessId);instance.hostileObservers.Remove(body.IndividualAccessId);
    }
    public static void Awareness(ZeldaCharacterAiBase observer, ZeldaFourWayMover target, ZeldaAiState next, bool live)
    {
        var service=GetOrCreate();if(observer==null||Restoring)return;
        var body=observer.GetComponent<ZeldaCharacterData>();if(body==null)return;
        string id=body.IndividualAccessId;
        bool warning=next==ZeldaAiState.Alert, hostile=next==ZeldaAiState.Hostile;
        if(!warning)service.warningObservers.Remove(id);
        if(!hostile)service.hostileObservers.Remove(id);
        if(target==null || target!=ZeldaRuntimeRegistry.GetControlledMover() || target.CharacterData==null || target.CharacterData.IsGhostLike)
        { ForgetObserver(observer);return; }
        bool count=service.CanRecord && live;
        // One episode, not one count per guard in a propagated group alert.
        if(warning && service.warningObservers.Add(id) && service.warningObservers.Count==1 && count)service.state.warnings++;
        if(hostile && service.hostileObservers.Add(id) && service.hostileObservers.Count==1 && count)service.state.hostilities++;
    }
}
