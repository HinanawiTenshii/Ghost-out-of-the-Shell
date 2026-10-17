using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Six distinct, cross-scene character offerings charge a one-shot summoning seal.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(CameraVisionStreamingExempt), typeof(SummoningCircleVisual))]
[AddComponentMenu("Interaction/Summoning Circle")]
public sealed class SummoningCircle : MonoBehaviour
{
    public const int CrystalCount = 6;
    [Header("Offering Area / 献祭矩形范围")]
    [SerializeField] private Vector2 areaSize = new Vector2(12, 10);
    [SerializeField] private Vector2 areaOffset;
    [Header("Characters / 六名跨场景绑定角色")]
    [SerializeField] private CharacterPersistentIdentity[] boundCharacters = new CharacterPersistentIdentity[CrystalCount];
    [Header("Summon / 召唤角色预制体")]
    [SerializeField] private ZeldaCharacterData summonPrefab;
    [SerializeField, Min(.4f)] private float absorptionDuration = 1.8f;
    [Header("Optional Quest / 可选任务记录")]
    [SerializeField, Tooltip("留空不影响任务。Level2 仪式使用 level2.summoning，阅读召唤需知后才显示任务。")]
    private string journalQuestId;
    public string JournalQuestId => journalQuestId;

    [Serializable] public struct Absorption
    {
        public int crystal, seed;
        public Vector3 origin;
        public Vector2 bodySize;
        public float elapsed, duration;
    }
    [Serializable] public struct RuntimeState
    {
        public int litMask;
        public bool summoned;
        public string[] consumedIds;
        public Absorption[] absorptions;
    }
    private static readonly HashSet<SummoningCircle> Instances = new HashSet<SummoningCircle>();
    private readonly HashSet<string> consumed = new HashSet<string>();
    private readonly List<Absorption> absorptions = new List<Absorption>(CrystalCount);
    private int litMask;
    private bool summoned;
    private SummoningCircleVisual visual;
    public IReadOnlyList<CharacterPersistentIdentity> BoundCharacters => boundCharacters;
    public IReadOnlyList<Absorption> Absorptions => absorptions;
    public ZeldaCharacterData SummonPrefab { get => summonPrefab; set => summonPrefab = value; }
    public Vector2 AreaSize { get => areaSize; set => areaSize = new Vector2(Mathf.Max(.2f, value.x), Mathf.Max(.2f, value.y)); }
    public Vector2 AreaOffset { get => areaOffset; set => areaOffset = value; }
    public bool HasSummoned => summoned;
    public int LitCrystalCount
    {
        get { int count = 0; for (int i = 0; i < CrystalCount; i++) if (IsCrystalLit(i)) count++; return count; }
    }
    public bool IsCrystalLit(int index) => index >= 0 && index < CrystalCount && (litMask & (1 << index)) != 0;
    public Vector3 CrystalPosition(int index) => Visual != null ? Visual.CrystalPosition(index) : transform.position;
    private SummoningCircleVisual Visual => visual != null ? visual : (visual = GetComponent<SummoningCircleVisual>());
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Instances.Clear();
    private void OnEnable() { Instances.Add(this); ReportQuestProgress(); }
    private void OnDisable() => Instances.Remove(this);
    private void OnDestroy() => Instances.Remove(this);
    private void OnValidate()
    {
        AreaSize = areaSize;
        absorptionDuration = Mathf.Max(.4f, absorptionDuration);
        if (boundCharacters == null || boundCharacters.Length != CrystalCount)
            Array.Resize(ref boundCharacters, CrystalCount);
    }
    public void BindCharacter(int slot, CharacterPersistentIdentity identity)
    {
        if (slot < 0 || slot >= CrystalCount) throw new ArgumentOutOfRangeException(nameof(slot));
        if (boundCharacters == null || boundCharacters.Length != CrystalCount) Array.Resize(ref boundCharacters, CrystalCount);
        boundCharacters[slot] = identity;
    }
    public bool Contains(Vector3 position)
    {
        Vector2 point = (Vector2)transform.InverseTransformPoint(position) - areaOffset;
        return Mathf.Abs(point.x) <= areaSize.x * .5f && Mathf.Abs(point.y) <= areaSize.y * .5f;
    }
    private int OccupiedMask
    {
        get { int mask = litMask; foreach (var flight in absorptions) mask |= 1 << flight.crystal; return mask; }
    }
    private bool Accepts(ZeldaCharacterData source, string identity)
    {
        if (!isActiveAndEnabled || summoned || OccupiedMask == 63 || string.IsNullOrEmpty(identity) || consumed.Contains(identity) ||
            ZeldaRuntimeRegistry.GetGameplayScene(source.gameObject) != gameObject.scene || !Contains(source.AttributeWindowAnchor.position)) return false;
        foreach (var bound in boundCharacters ?? Array.Empty<CharacterPersistentIdentity>())
            if (bound != null && bound.StableId == identity) return true;
        return false;
    }
    public static bool TryAbsorbDeath(ZeldaCharacterData source)
    {
        if (source == null || !source.IsDead || source is GhostZeldaCharacterData) return false;
        string identity = source.PersistentCharacterId;
        if (string.IsNullOrEmpty(identity)) return false;
        // Overlapping seals must not duplicate one offering. Prefer the nearest;
        // ties are stable for this session, independent of HashSet iteration.
        SummoningCircle chosen = null;
        float distance = float.PositiveInfinity;
        foreach (var circle in Instances)
        {
            if (circle == null || !circle.Accepts(source, identity)) continue;
            float candidate = (circle.transform.position - source.AttributeWindowAnchor.position).sqrMagnitude;
            if (candidate < distance || (candidate == distance && chosen != null && circle.GetInstanceID() < chosen.GetInstanceID()))
            { chosen = circle; distance = candidate; }
        }
        if (chosen == null) return false;
        chosen.BeginAbsorption(source, identity);
        return true;
    }
    private void BeginAbsorption(ZeldaCharacterData source, string identity)
    {
        int mask = OccupiedMask, free = 0;
        for (int i = 0; i < CrystalCount; i++) if ((mask & (1 << i)) == 0) free++;
        int choice = UnityEngine.Random.Range(0, free), crystal = 0;
        for (int i = 0; i < CrystalCount; i++)
            if ((mask & (1 << i)) == 0 && choice-- == 0) { crystal = i; break; }
        var body = source.transform.Find("Visual")?.GetComponent<SpriteRenderer>();
        if (body == null || body.sprite == null) body = source.GetComponent<SpriteRenderer>();
        Vector3 origin = source.AttributeWindowAnchor.position;
        Vector2 size = new Vector2(.6f, .8f);
        if (body != null && body.sprite != null) { origin = body.bounds.center; size = body.bounds.size; }
        consumed.Add(identity); // Reserve both the identity and destination before another death callback.
        absorptions.Add(new Absorption { crystal = crystal, seed = UnityEngine.Random.Range(1, 1000000), origin = origin,
            bodySize = size, elapsed = 0, duration = absorptionDuration });
        if (journalQuestId == QuestJournalManager.SummoningQuestId)
            QuestJournalManager.GetOrCreate().RecordSummoningMarkerBindings(boundCharacters, consumed);
    }
    private void Update()
    {
        // Restore all devices/characters before resuming progress or creating a new NPC.
        var manager = SceneTravelStateManager.Instance;
        if (GameSaveSystem.IsLoading || (manager != null && manager.IsSceneTravelRestoreInProgress)) return;
        int previousMask = litMask;
        for (int i = absorptions.Count - 1; i >= 0; i--)
        {
            var flight = absorptions[i]; flight.elapsed += Time.deltaTime;
            if (flight.elapsed >= flight.duration)
            {
                litMask |= 1 << flight.crystal;
                absorptions.RemoveAt(i);
            }
            else absorptions[i] = flight;
        }
        if (!summoned && litMask == 63 && summonPrefab != null) Summon();
        else if (litMask != previousMask) ReportQuestProgress();
    }
    private void Summon()
    {
        summoned = true; // Latch before Instantiate invokes any character callbacks.
        GameObject actor = InstantiateNpc(summonPrefab, gameObject.scene, transform.position, Quaternion.identity);
        var stable = actor.GetComponent<SceneTravelStableId>();
        if (stable == null) stable = actor.AddComponent<SceneTravelStableId>();
        stable.Initialize("summoned:" + Guid.NewGuid().ToString("N"));
        Visual?.PlaySummonPulse();
        ReportQuestProgress();
    }

    private void ReportQuestProgress()
    {
        if (journalQuestId == QuestJournalManager.SummoningQuestId)
        {
            QuestJournalManager.GetOrCreate().RecordSummoningMarkerBindings(boundCharacters, consumed);
            QuestJournalManager.GetOrCreate().RecordSummoningQuestProgress(LitCrystalCount, summoned);
        }
    }

    public static void ReportLoadedQuestProgress()
    {
        // Invoked on reading the notice, not every frame. Includes progress from older saves.
        foreach (var circle in Instances) if (circle != null) circle.ReportQuestProgress();
    }
    internal static GameObject InstantiateNpc(ZeldaCharacterData prefab, Scene scene, Vector3 position, Quaternion rotation)
    {
        // Disable player input before OnEnable can register a cloned mover as the player.
        var staging = new GameObject("Summon Staging") { hideFlags = HideFlags.HideAndDontSave };
        staging.SetActive(false);
        GameObject actor = Instantiate(prefab.gameObject, position, rotation, staging.transform);
        actor.name = prefab.name;
        var origin = actor.GetComponent<SummonedCharacterOrigin>();
        if (origin == null) origin = actor.AddComponent<SummonedCharacterOrigin>();
        origin.Initialize(prefab.name);
        actor.GetComponent<ZeldaCharacterData>().ClearPersistentIdentityForSummon();
        var mover = actor.GetComponent<ZeldaFourWayMover>();
        if (mover != null) mover.enabled = false;
        var ai = actor.GetComponent<ZeldaCharacterAiBase>();
        if (ai != null) ai.enabled = true;
        actor.SetActive(false);
        actor.transform.SetParent(null, true);
        SceneManager.MoveGameObjectToScene(actor, scene);
        Destroy(staging);
        actor.SetActive(true);
        return actor;
    }
    public RuntimeState CaptureRuntimeState() => new RuntimeState
    {
        litMask = litMask, summoned = summoned, consumedIds = new List<string>(consumed).ToArray(), absorptions = absorptions.ToArray()
    };
    public void ApplyRuntimeState(RuntimeState state)
    {
        litMask = state.litMask & 63; summoned = state.summoned;
        consumed.Clear();
        foreach (string id in state.consumedIds ?? Array.Empty<string>()) if (!string.IsNullOrEmpty(id)) consumed.Add(id);
        absorptions.Clear(); int occupied = litMask;
        foreach (var saved in state.absorptions ?? Array.Empty<Absorption>())
        {
            if (saved.crystal < 0 || saved.crystal >= CrystalCount || (occupied & (1 << saved.crystal)) != 0) continue;
            var flight = saved; flight.duration = Mathf.Max(.4f, flight.duration);
            flight.elapsed = Mathf.Clamp(flight.elapsed, 0, flight.duration);
            absorptions.Add(flight); occupied |= 1 << flight.crystal;
        }
        Visual?.RefreshImmediately();
        ReportQuestProgress();
    }
}
