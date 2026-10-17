using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Scene-local assault/evacuation director. Movement remains owned by squad AI.</summary>
[DisallowMultipleComponent]
public sealed partial class KnightCaptainAmbush : MonoBehaviour
{
    public enum Phase { Patrol, Retreat, Sheltered, Possessed, CaptainDead }
    [Serializable] public sealed class SafeHouse
    {
        public string name;
        public PermissionArea permission;
        public DoorHingeInteraction entrance;
        public Vector2 approach;
        public Vector2[] captainPath;
        public Vector2 captainFacing = Vector2.down;
        public GuardPost[] guards = new GuardPost[3];
    }
    [Serializable] public sealed class GuardPost
    {
        public Vector2[] path;
        public Vector2 facing = Vector2.down;
    }
    [Serializable] public sealed class Route
    {
        public Vector2[] points = new Vector2[0];
        public int next;
    }
    [Serializable] public sealed class Hit
    {
        public string victim;
        public int damage;
        public float age;
    }
    [Serializable] public sealed class RuntimeState
    {
        public Phase phase;
        public int house = -1;
        public string captainId;
        public Vector2 marker;
        public Route[] routes;
        public List<Hit> hits = new List<Hit>();
    }
    public SquadPatrolCharacterAi captain;
    public SquadPatrolCharacterAi[] guards = new SquadPatrolCharacterAi[3];
    public SafeHouse[] safeHouses = new SafeHouse[2];
    [Header("足够规模的袭击：护卫阵亡，或时间窗内多人实际失血达到门槛")]
    [Min(1)] public int damageThreshold = 12;
    [Range(2, 4)] public int damagedMembersRequired = 2;
    [Min(1)] public float attackWindow = 12;
    [Header("驻守")]
    [Min(.1f)] public float retreatSpeedMultiplier = 1.2f;
    [Min(1)] public float guardLeash = 6;
    [Min(.5f)] public float calmDelay = 3;
    [Tooltip("地图标点采样间隔；场景内标点独立实时跟随。")]
    [Min(.1f)] public float markerInterval = .7f;
    RuntimeState state = new RuntimeState();
    SquadPatrolCharacterAi[] actors;
    ZeldaCharacterData[] data;
    ZeldaFourWayMover[] movers;
    float nextMarkerTime;
    bool started, restorePending;
    int originalPermission = -1;
    Vector2[] patrolRoute;
    static readonly Dictionary<string, KnightCaptainAmbush> live = new Dictionary<string, KnightCaptainAmbush>();
    public Phase CurrentPhase => state.phase;
    public int SelectedHouse => state.house;
    public Vector2 CachedMarker => state.marker;
    public float OrderedSpeedMultiplier => state.phase == Phase.Retreat ? Mathf.Max(.1f, retreatSpeedMultiplier) : 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => live.Clear();
    void Awake() { live[gameObject.scene.name] = this; }
    void Start()
    {
        actors = new SquadPatrolCharacterAi[guards.Length + 1];
        actors[0] = captain; Array.Copy(guards, 0, actors, 1, guards.Length);
        data = new ZeldaCharacterData[actors.Length];
        movers = new ZeldaFourWayMover[actors.Length];
        for (int i = 0; i < actors.Length; i++)
        {
            if (actors[i] == null) continue;
            actors[i].Ambush = this;
            data[i] = actors[i].GetComponent<ZeldaCharacterData>();
            movers[i] = actors[i].GetComponent<ZeldaFourWayMover>();
            data[i].HealthDamaged += OnDamage;
        }
        if (captain != null)
        {
            var route = new ZeldaPatrolCharacterAi.PatrolRouteSnapshot();
            captain.CapturePatrolRoute(route);
            patrolRoute = route.points;
            if (string.IsNullOrEmpty(state.captainId)) state.captainId = data[0].IndividualAccessId;
            if (!restorePending) state.marker = captain.transform.position;
        }
        ZeldaCharacterData.AnyCharacterDied += OnDeath;
        ZeldaRuntimeRegistry.ControlledMoverChanged += OnControlledChanged;
        started = true;
        RestoreOrders();
        OnControlledChanged();
    }
    void OnDestroy()
    {
        if (live.TryGetValue(gameObject.scene.name, out var current) && current == this) live.Remove(gameObject.scene.name);
        if (data != null) foreach (var actor in data) if (actor != null) actor.HealthDamaged -= OnDamage;
        ZeldaCharacterData.AnyCharacterDied -= OnDeath;
        ZeldaRuntimeRegistry.ControlledMoverChanged -= OnControlledChanged;
        if (actors != null) foreach (var ai in actors) if (ai != null && ai.Ambush == this) ai.Ambush = null;
    }
    void Update()
    {
        if (!started || GameSaveSystem.IsLoading) return;
        if (restorePending) RestoreOrders();
        if (state.phase == Phase.Patrol)
        {
            for (int i = state.hits.Count - 1; i >= 0; i--)
            { state.hits[i].age += Time.deltaTime; if (state.hits[i].age > attackWindow) state.hits.RemoveAt(i); }
        }
        if (Time.unscaledTime >= nextMarkerTime)
        {
            nextMarkerTime = Time.unscaledTime + Mathf.Max(.1f, markerInterval);
            if (captain != null && data[0] != null && !data[0].IsDead &&
                ZeldaRuntimeRegistry.GetGameplayScene(captain.gameObject) == gameObject.scene)
                state.marker = captain.transform.position;
        }
        if (state.phase == Phase.Retreat && AllArrived()) state.phase = Phase.Sheltered;
    }
    void OnDamage(ZeldaCharacterData victim, int damage, ZeldaCharacterData source)
    {
        if (GameSaveSystem.IsLoading || state.phase != Phase.Patrol || !CaptainAlive() || damage <= 0) return;
        if (victim != data[0] && victim.CurrentHealth <= 0) { BeginRetreat(source); return; }
        state.hits.Add(new Hit { victim = victim.IndividualAccessId, damage = damage });
        int total = 0; var distinct = new HashSet<string>();
        foreach (var hit in state.hits)
            if (hit.age <= attackWindow) { total += hit.damage; distinct.Add(hit.victim); }
        if (total >= damageThreshold && distinct.Count >= damagedMembersRequired) BeginRetreat(source);
    }
    void OnDeath(ZeldaCharacterData victim)
    {
        if (data == null || GameSaveSystem.IsLoading) return;
        if (victim == data[0])
        {
            if (state.phase != Phase.Possessed) state.phase = Phase.CaptainDead;
            return;
        }
        if (state.phase != Phase.Patrol || !CaptainAlive()) return;
        for (int i = 1; i < data.Length; i++) if (victim == data[i]) { BeginRetreat(null); return; }
    }
    bool CaptainAlive() => data != null && data[0] != null && !data[0].IsDead && data[0].CurrentHealth > 0 &&
        captain != null && movers[0] != null && !movers[0].enabled;
    void OnControlledChanged()
    {
        if (!started || GameSaveSystem.IsLoading) return;
        var mover = ZeldaRuntimeRegistry.GetControlledMover();
        if (mover == null || mover.CharacterData == null || mover.CharacterData.IndividualAccessId != state.captainId) return;
        state.phase = Phase.Possessed;
        QuestJournalManager.GetOrCreate().RecordQuestCompletion(QuestJournalManager.KnightAmbushQuestId, true);
    }
    public bool IsCaptain(SquadPatrolCharacterAi ai) => ai == captain;
    int Index(SquadPatrolCharacterAi ai) => actors == null ? -1 : Array.IndexOf(actors, ai);
    public bool HasOrders(SquadPatrolCharacterAi ai)
    {
        if (!started || state.house < 0 || state.house >= safeHouses.Length ||
            state.phase == Phase.Patrol || state.phase == Phase.CaptainDead) return false;
        int i = Index(ai);
        return i >= 0 && movers[i] != null && !movers[i].enabled;
    }
    public bool NeedsRetention(SquadPatrolCharacterAi ai)
    {
        int i = Index(ai);
        return HasOrders(ai) && state.routes != null && i < state.routes.Length && state.routes[i] != null &&
            state.routes[i].next < state.routes[i].points.Length;
    }
    public Vector2 PostPosition(SquadPatrolCharacterAi ai)
    {
        int index = Index(ai);
        var route = state.routes != null && index >= 0 && index < state.routes.Length ? state.routes[index] : null;
        return route != null && route.points.Length > 0 ? route.points[route.points.Length - 1] : (Vector2)ai.transform.position;
    }
    public Vector2 PostFacing(SquadPatrolCharacterAi ai)
    {
        var house = safeHouses[state.house]; int i = Index(ai);
        return i <= 0 ? house.captainFacing : house.guards[Mathf.Min(i - 1, house.guards.Length - 1)].facing;
    }
    public Vector2 NextDestination(SquadPatrolCharacterAi ai)
    {
        int index = Index(ai);
        var route = state.routes[index];
        while (route.next < route.points.Length)
        {
            float distance = Vector2.Distance(ai.transform.position, route.points[route.next]);
            // Transit points are not seats: a stationed teammate must not block
            // the whole procession from continuing to its distinct entrance.
            bool canContinue = route.next + 1 < route.points.Length && distance <= 1.5f &&
                ai.AssignedSegmentClear(ai.transform.position, route.points[route.next + 1]);
            if (distance > .35f && !canContinue) break;
            route.next++;
        }
        return route.next < route.points.Length ? route.points[route.next] : PostPosition(ai);
    }
    bool AllArrived()
    {
        for (int i = 0; i < actors.Length; i++)
            if (actors[i] != null && data[i] != null && !data[i].IsDead && NeedsRetention(actors[i])) return false;
        return true;
    }
    void BeginRetreat(ZeldaCharacterData attacker)
    {
        if (!CaptainAlive() || safeHouses.Length == 0) return;
        int best = -1; float bestCost = float.PositiveInfinity;
        for (int i = 0; i < safeHouses.Length; i++)
        {
            var house = safeHouses[i];
            if (house == null || house.captainPath == null || house.captainPath.Length == 0 ||
                house.guards == null || house.guards.Length < guards.Length ||
                (house.entrance != null && house.entrance.IsLocked)) continue;
            bool validPosts = true;
            foreach (var post in house.guards) if (post == null || post.path == null || post.path.Length == 0) validPosts = false;
            if (!validPosts) continue;
            var route = BuildRoadRoute(captain.transform.position, house.approach, patrolRoute);
            float cost = RouteLength(captain.transform.position, route);
            if (attacker != null && Vector2.Distance(attacker.transform.position, house.approach) < 8) cost += 20;
            if (cost < bestCost) { best = i; bestCost = cost; }
        }
        if (best < 0) return;
        state.house = best; state.phase = Phase.Retreat; state.hits.Clear();
        state.routes = new Route[actors.Length];
        for (int i = 0; i < actors.Length; i++)
        {
            var house = safeHouses[best];
            var route = BuildRoadRoute(actors[i] != null ? (Vector2)actors[i].transform.position : house.approach,
                house.approach, patrolRoute);
            var end = i == 0 ? house.captainPath : house.guards[Mathf.Min(i - 1, house.guards.Length - 1)].path;
            route.AddRange(end);
            if (actors[i] != null)
                for (int p = 0; p < route.Count; p++) route[p] = actors[i].ResolveAssignedDestination(route[p]);
            state.routes[i] = new Route { points = route.ToArray() };
        }
        RestoreOrders();
    }
    void RestoreOrders()
    {
        restorePending = false;
        if (!started || state.house < 0 || state.house >= safeHouses.Length) return;
        var house = safeHouses[state.house];
        if (house.permission != null)
        {
            if (originalPermission < 0) originalPermission = house.permission.PermissionLevel;
            house.permission.SetPermissionLevel(5);
        }
        for (int i = 0; i < actors.Length; i++)
        {
            var ai = actors[i];
            if (ai == null || data[i] == null || data[i].IsDead || !HasOrders(ai)) continue;
            if (NeedsRetention(ai)) ai.gameObject.SetActive(true);
            ai.ResetForAssignedPost();
        }
    }
    public RuntimeState CaptureRuntimeState() => JsonUtility.FromJson<RuntimeState>(JsonUtility.ToJson(state));
    public void ApplyRuntimeState(RuntimeState saved)
    {
        if (saved == null) return;
        if (originalPermission >= 0 && state.house >= 0 && state.house < safeHouses.Length && safeHouses[state.house].permission != null)
            safeHouses[state.house].permission.SetPermissionLevel(originalPermission);
        originalPermission = -1;
        state = JsonUtility.FromJson<RuntimeState>(JsonUtility.ToJson(saved));
        if (state.hits == null) state.hits = new List<Hit>();
        restorePending = true;
    }
    public static bool TryGetMarker(string sceneName, out Vector2 position)
    {
        if (!string.IsNullOrEmpty(sceneName) && live.TryGetValue(sceneName, out var director) && director != null)
        { position = director.started ? director.state.marker : director.captain != null ? (Vector2)director.captain.transform.position : Vector2.zero; return true; }
        if (SceneTravelStateManager.Instance != null && SceneTravelStateManager.Instance.TryGetSavedKnightMarker(sceneName, out position)) return true;
        position = default; return false;
    }
    /// <summary>Live world marker only. Never changes the throttled map/save position.</summary>
    public static bool TryGetWorldMarker(string sceneName, out Vector2 position)
    {
        position = default;
        if (string.IsNullOrEmpty(sceneName) || !live.TryGetValue(sceneName, out var director) ||
            director == null || !director.started || !director.isActiveAndEnabled ||
            director.captain == null || !director.captain.gameObject.activeInHierarchy ||
            director.data[0] == null || director.data[0].IsDead ||
            ZeldaRuntimeRegistry.GetGameplayScene(director.captain.gameObject) != director.gameObject.scene)
            return false;
        position = director.captain.transform.position;
        return true;
    }
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        foreach (var house in safeHouses)
        {
            if (house == null) continue;
            DrawPath(house.approach, house.captainPath);
            if (house.guards != null) foreach (var post in house.guards) if (post != null) DrawPath(house.approach, post.path);
        }
    }
    static void DrawPath(Vector2 from, Vector2[] points)
    {
        if (points == null) return;
        foreach (var point in points) { Gizmos.DrawLine(from, point); Gizmos.DrawWireSphere(point, .25f); from = point; }
    }
}
