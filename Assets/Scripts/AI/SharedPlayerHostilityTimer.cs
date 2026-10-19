using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>One pursuit/search clock per scene, not one clock per guard.</summary>
[DisallowMultipleComponent, DefaultExecutionOrder(-50)]
public sealed class SharedPlayerHostilityTimer : MonoBehaviour
{
    public enum IncidentPhase { Idle, Hostile, Search }
    [SerializeField, Min(.1f), Tooltip("无人看见玩家后，所有追兵保持敌对的共同时间。")]
    private float hostileDuration = 9f;
    [SerializeField, Min(.1f), Tooltip("所有追兵共同搜索的时间，包含移动到搜索地点的时间。")]
    private float searchDuration = 12f;
    public IncidentPhase Phase { get; private set; }
    public float Remaining { get; private set; }
    public Vector2 LastSeenPosition { get; private set; }
    public float HostileDuration => hostileDuration;
    public float SearchDuration => searchDuration;
    private readonly HashSet<ZeldaCharacterAiBase> members = new HashSet<ZeldaCharacterAiBase>();
    private readonly List<ZeldaCharacterAiBase> snapshot = new List<ZeldaCharacterAiBase>(32);
    private bool restoringSave;
    private static readonly Dictionary<int, SharedPlayerHostilityTimer> scenes = new Dictionary<int, SharedPlayerHostilityTimer>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => scenes.Clear();
    private void Awake()
    {
        if (scenes.TryGetValue(gameObject.scene.handle, out var other) && other != null && other != this)
        { Destroy(gameObject); return; }
        scenes[gameObject.scene.handle] = this;
    }
    private void OnDestroy()
    {
        if (scenes.TryGetValue(gameObject.scene.handle, out var value) && value == this) scenes.Remove(gameObject.scene.handle);
    }
    private void OnValidate() { hostileDuration = Mathf.Max(.1f, hostileDuration); searchDuration = Mathf.Max(.1f, searchDuration); }
    /// <summary>Read-only lookup for HUDs; viewing a scene never creates or restarts an incident.</summary>
    public static bool TryGetForScene(Scene scene, out SharedPlayerHostilityTimer timer)
    {
        timer = null;
        return scene.IsValid() && scenes.TryGetValue(scene.handle, out timer) && timer != null;
    }
    public static SharedPlayerHostilityTimer ForScene(Scene scene)
    {
        if (scenes.TryGetValue(scene.handle, out var value) && value != null) return value;
        // Authored component may not have received Awake yet. Do not create a duplicate.
        foreach (var root in scene.GetRootGameObjects())
        {
            value = root.GetComponentInChildren<SharedPlayerHostilityTimer>(true);
            if (value != null) { scenes[scene.handle] = value; return value; }
        }
        var go = new GameObject("Shared Player Hostility Timer");
        SceneManager.MoveGameObjectToScene(go, scene);
        return go.AddComponent<SharedPlayerHostilityTimer>();
    }
    internal void Join(ZeldaCharacterAiBase ai)
    {
        if (ai == null || !ai.RetainsSharedPlayerIncident(ZeldaRuntimeRegistry.GetControlledMover())) return;
        if (!members.Add(ai))
        {
            // A new attack against a searching guard is a new hostile incident,
            // even though that guard is already a member of this shared search.
            if (Phase == IncidentPhase.Search && ai.CurrentState == ZeldaAiState.Hostile)
            {
                LastSeenPosition = ai.CurrentTarget.transform.position;
                SetPhase(IncidentPhase.Hostile);
            }
            return;
        }
        ai.SetSharedPlayerTimer(this);
        LastSeenPosition = ai.CurrentTarget.transform.position;
        if (Phase != IncidentPhase.Hostile) SetPhase(IncidentPhase.Hostile);
        else Remaining = hostileDuration; // A genuinely new hostile incident renews the shared alert.
    }
    internal void Leave(ZeldaCharacterAiBase ai)
    {
        members.Remove(ai);
        ai.SetSharedPlayerTimer(null);
        if (members.Count == 0) { Phase = IncidentPhase.Idle; Remaining = 0f; }
    }
    internal void Restore(ZeldaCharacterAiBase ai, int phase, float remaining, Vector2 position)
    {
        if (GameSaveSystem.IsLoading && !restoringSave)
        {
            members.Clear(); Phase = IncidentPhase.Idle; Remaining = 0;
            restoringSave = true;
        }
        bool first = members.Count == 0;
        members.Add(ai); ai.SetSharedPlayerTimer(this);
        if (!first) return; // All members store the SAME snapshot; never restart once per restored NPC.
        Phase = phase == (int)IncidentPhase.Search ? IncidentPhase.Search : IncidentPhase.Hostile;
        Remaining = Mathf.Clamp(remaining, 0f, Phase == IncidentPhase.Hostile ? hostileDuration : searchDuration);
        LastSeenPosition = position;
    }
    private void Update() => Advance(Time.deltaTime);
    public void Advance(float deltaTime)
    {
        if (RetroSceneLoadReveal.IsPreparingGameplay) return;
        if (!GameSaveSystem.IsLoading) restoringSave = false;
        if (deltaTime <= 0f || Time.timeScale <= 0f || GameSaveSystem.IsLoading || CharacterDialogueWindow.BlocksInput ||
            SceneManager.GetActiveScene() != gameObject.scene || Phase == IncidentPhase.Idle) return;
        var player = ZeldaRuntimeRegistry.GetControlledMover();
        CopyMembers();
        foreach (var ai in snapshot)
        {
            if (ai == null) { members.Remove(ai); continue; }
            if (!ai.RetainsSharedPlayerIncident(player))
            { ai.EndSharedPlayerIncident(player); Leave(ai); }
        }
        if (members.Count == 0) { Phase = IncidentPhase.Idle; Remaining = 0; return; }
        bool seen = false;
        if (player != null && player.CharacterData != null && !player.CharacterData.IsDead &&
            ZeldaRuntimeRegistry.GetGameplayScene(player.gameObject) == gameObject.scene)
            foreach (var observer in ZeldaRuntimeRegistry.AiCharacters)
                if (observer != null && observer.CanObserveSharedPlayer(player)) { seen = true; break; }
        if (seen)
        {
            LastSeenPosition = player.transform.position;
            if (Phase == IncidentPhase.Search) SetPhase(IncidentPhase.Hostile);
            Remaining = hostileDuration;
        }
        else
        {
            Remaining = Mathf.Max(0, Remaining - deltaTime);
            if (Remaining <= 0)
            {
                if (Phase == IncidentPhase.Hostile) SetPhase(IncidentPhase.Search);
                else
                {
                    CopyMembers();
                    foreach (var ai in snapshot) if (ai != null) { ai.FinishSharedPlayerSearch(); Leave(ai); }
                    Phase = IncidentPhase.Idle; Remaining = 0;
                    return;
                }
            }
        }
        CopyMembers();
        foreach (var ai in snapshot) if (ai != null) ai.ApplySharedPlayerPhase(Phase, LastSeenPosition);
    }
    private void CopyMembers() { snapshot.Clear(); snapshot.AddRange(members); }
    private void SetPhase(IncidentPhase phase)
    {
        Phase = phase; Remaining = phase == IncidentPhase.Hostile ? hostileDuration : searchDuration;
        // State changes can register new nearby allies. Only iterate a snapshot.
        CopyMembers();
        foreach (var ai in snapshot) if (ai != null) ai.ApplySharedPlayerPhase(Phase, LastSeenPosition);
    }
}
