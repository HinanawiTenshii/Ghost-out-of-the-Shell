using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

/// <summary>Replace the ordinary AI with this script; only the initial leader assigns members.</summary>
[AddComponentMenu("Characters/AI/Squad Patrol Character AI")]
public sealed class SquadPatrolCharacterAi : ZeldaPatrolCharacterAi
{
    [Header("Patrol Squad / 巡逻队")]
    [Tooltip("仅默认队长填写。最多四名队员，数组顺序也是队长递补顺序；队员也需使用本脚本。")]
    [SerializeField] private SquadPatrolCharacterAi[] members = new SquadPatrolCharacterAi[0];
    [Tooltip("民用角色开启：沿用平民巡逻的权限差值规则。士兵等关闭。")]
    [SerializeField] private bool civilianPermissionRules;
    [SerializeField, Min(.6f)] private float formationSpacing = 1.15f;
    [SerializeField, Range(.08f, .4f)] private float formationRefreshInterval = .15f;
    [SerializeField, Min(.05f)] private float arrivalTolerance = .16f;
    [SerializeField, Range(1f, 1.6f)] private float catchUpMultiplier = 1.25f;
    [Tooltip("落后过远时队长减速等待，避免较慢的队员永久掉队。")]
    [SerializeField, Min(2f)] private float regroupDistance = 6f;

    static readonly HashSet<SquadPatrolCharacterAi> instances = new HashSet<SquadPatrolCharacterAi>();
    static readonly ProfilerMarker UpdateMarker = new ProfilerMarker("SquadPatrol.Update");
    static readonly ProfilerMarker FollowMarker = new ProfilerMarker("SquadPatrol.Follow");
    ZeldaCharacterData ownData;
    ZeldaFourWayMover ownMover;
    BoxCollider2D ownBody;
    Group group;
    bool ready;
    float followSpeed = 1f, nextFormationCheck;
    Vector2 followTarget;
    float nextFollowClearanceCheck;
    Vector2 checkedFollowOrigin, checkedFollowTarget;
    SquadPatrolCharacterAi checkedFollowLeader;
    bool followSegmentClear, checkedSingleFile;
    public SquadPatrolCharacterAi CurrentLeader { get { EnsureGroup(); group?.Refresh(); return group?.leader; } }
    public bool IsSquadLeader => CurrentLeader == this;
    public bool IsUsingSingleFile => group != null && Time.time < group.compactUntil;
    public Vector2 CurrentFormationTarget => followTarget;
    protected override int MinimumPermissionViolationDifference => civilianPermissionRules ? 2 : 1;

    // Retain the small squad while any member is on camera, not forever merely
    // because another member happens to remain active after a streaming pass.
    public override bool RequiresVisionStreamingRetention =>
        base.RequiresVisionStreamingRetention || (group != null && group.IsNearCamera());

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetSquads() => instances.Clear();

    void Cache()
    {
        if (ownData == null) ownData = GetComponent<ZeldaCharacterData>();
        if (ownMover == null) ownMover = GetComponent<ZeldaFourWayMover>();
        if (ownBody == null) ownBody = GetComponent<BoxCollider2D>();
    }
    bool Eligible
    {
        get
        {
            Cache();
            return enabled && ownData != null && !ownData.IsDead &&
                ownMover != null && !ownMover.enabled;
        }
    }
    bool SameScene(SquadPatrolCharacterAi other) => other != null &&
        ZeldaRuntimeRegistry.GetGameplayScene(other.gameObject) == ZeldaRuntimeRegistry.GetGameplayScene(gameObject);

    protected override void Awake()
    {
        foreach (var other in GetComponents<ZeldaCharacterAiBase>())
            if (other != this && other.enabled)
            {
                Debug.LogError("巡逻队 AI 必须替换原 AI，不能同时驱动同一角色。请使用 GameObject/AI/Convert To Squad Patrol。", this);
                enabled = false; return;
            }
        Cache(); base.Awake(); ready = true; instances.Add(this);
    }
    protected override void Update()
    {
        using var sample = UpdateMarker.Auto();
        if (CharacterDialogueWindow.BlocksInput || Time.deltaTime <= 0f) return;
        EnsureGroup(); group?.Refresh();
        base.Update();
        if (group != null && group.leader == this && CurrentState == ZeldaAiState.Idle)
            CapturePatrolRoute(group.route);
    }
    protected override void OnDestroy()
    {
        instances.Remove(this);
        base.OnDestroy();
    }

    void EnsureGroup()
    {
        if (group != null || !ready) return;
        // Wait for every Awake, then resolve declarative ownership independently
        // of which member Update happens first. Overlapping rosters choose one
        // owner deterministically; nested/cyclic bindings never recurse.
        SquadPatrolCharacterAi owner = members.Length > 0 ? this : null;
        foreach (var candidate in instances)
        {
            if (!SameScene(candidate) || !candidate.ready) continue;
            bool contains = false;
            foreach (var member in candidate.members) if (member == this) { contains = true; break; }
            if (contains && (owner == null || candidate.GetInstanceID() < owner.GetInstanceID())) owner = candidate;
        }
        if (owner == null) owner = this;
        if (owner.group == null) owner.CreateGroup();
        // A conflicting/nested binding cannot merge or steal an existing squad.
        if (group == null) CreateGroup();
    }
    void CreateGroup()
    {
        group = new Group(this);
        foreach (var member in members)
        {
            if (group.roster.Count == 5) break;
            if (member == null || member == this || !SameScene(member) ||
                group.roster.Contains(member) || member.group != null) continue;
            member.Cache(); member.group = group; group.roster.Add(member);
        }
        CapturePatrolRoute(group.route);
    }

    protected override float AiMovementSpeedMultiplier
    {
        get
        {
            if (CurrentState != ZeldaAiState.Idle || group == null) return base.AiMovementSpeedMultiplier;
            if (group.leader != this) return followSpeed;
            float multiplier = base.AiMovementSpeedMultiplier;
            foreach (var member in group.roster)
            {
                if (member == null || member == this || !SameScene(member) || !member.Eligible ||
                    member.CurrentState != ZeldaAiState.Idle) continue;
                float distance = Vector2.Distance(AiPosition, member.AiPosition);
                if (distance > group.regroup) multiplier *= .35f;
                // Cruise at a speed the slowest idle member can sustain.
                if (ownData != null && ownData.MoveSpeed > .01f)
                    multiplier = Mathf.Min(multiplier, member.ownData.MoveSpeed / ownData.MoveSpeed * group.route.speed);
            }
            return multiplier;
        }
    }

    protected override void TickIdle(float deltaTime)
    {
        EnsureGroup(); group?.Refresh();
        var leader = group?.leader;
        if (leader == null) { AiMoveDirection = Vector2.zero; return; }
        // Update captures the leader's final route state once, after base.Update.
        if (leader == this) { base.TickIdle(deltaTime); return; }
        if (!leader.gameObject.activeInHierarchy)
        { AiMoveDirection = Vector2.zero; return; } // Streaming is not captain death.

        int slot = group.SlotOf(this, out int count);
        if (slot < 0) { AiMoveDirection = Vector2.zero; return; }
        using var sample = FollowMarker.Auto();
        float spacing = group.Spacing;
        if (Time.time >= nextFormationCheck)
        {
            nextFormationCheck = Time.time + formationRefreshInterval;
            Vector2 local = FormationOffset(count, slot) * spacing;
            Vector2 right = new Vector2(group.heading.y, -group.heading.x);
            Vector2 target = leader.AiPosition + right * local.x + group.heading * local.y;
            bool clear = IsBodyPathSegmentClear(leader.AiPosition, target, .08f) &&
                IsBodyPathSegmentClear(AiPosition, target, .02f);
            if (!clear)
                group.compactUntil = Time.time + .8f;
            followTarget = target;
            // Reuse this body's formation-clearance result when the target is
            // unchanged by single-file mode; no second cast for the same ray.
            if (!IsUsingSingleFile) RememberFollowClearance(leader, clear);
        }
        if (IsUsingSingleFile) followTarget = group.TrailPoint((slot + 1) * spacing);
        float gap = Vector2.Distance(AiPosition, followTarget);
        if (gap <= arrivalTolerance)
        {
            AiMoveDirection = Vector2.zero;
            SetFacingDirection(group.heading);
            return;
        }
        float leaderSpeed = leader.ownData.MoveSpeed * group.route.speed;
        followSpeed = Mathf.Clamp(leaderSpeed / Mathf.Max(.05f, ownData.MoveSpeed),
            .05f, 1f) * (gap > spacing ? catchUpMultiplier : 1f);
        // Clamp the last step rather than oscillating across the slot.
        followSpeed = Mathf.Min(followSpeed, gap / Mathf.Max(.001f, ownData.MoveSpeed * Time.fixedDeltaTime));
        bool singleFile = IsUsingSingleFile;
        if (Time.time >= nextFollowClearanceCheck || checkedFollowLeader != leader ||
            checkedSingleFile != singleFile ||
            (AiPosition - checkedFollowOrigin).sqrMagnitude > .36f ||
            (followTarget - checkedFollowTarget).sqrMagnitude > .36f)
        {
            RememberFollowClearance(leader, IsBodyPathSegmentClear(AiPosition, followTarget, .02f));
        }
        AiMoveDirection = followSegmentClear
            ? FollowValidatedAiSegment(followTarget)
            : PlanAiPathTo(followTarget);
    }

    void RememberFollowClearance(SquadPatrolCharacterAi leader, bool clear)
    {
        followSegmentClear = clear;
        checkedFollowLeader = leader;
        checkedFollowOrigin = AiPosition;
        checkedFollowTarget = followTarget;
        checkedSingleFile = IsUsingSingleFile;
        // Smooth steering still runs every frame. Only obstacle probes are
        // cached briefly; sudden movement, mode/leader changes force a recheck.
        float stagger = Mathf.Lerp(.9f, 1.1f, Mathf.Abs(GetInstanceID() % 17) / 17f);
        nextFollowClearanceCheck = Time.time + Mathf.Min(.12f, formationRefreshInterval) * stagger;
    }

    protected override void OnStateEntered(ZeldaAiState previousState, ZeldaAiState newState)
    {
        checkedFollowLeader = null;
        nextFollowClearanceCheck = 0f;
        base.OnStateEntered(previousState, newState);
        if (newState == ZeldaAiState.Idle && group != null && group.leader == this)
            ApplyPatrolRoute(group.route);
    }

    // Count means followers; the head circle in the reference is the captain.
    public static Vector2 FormationOffset(int count, int slot)
    {
        count = Mathf.Clamp(count, 1, 4); slot = Mathf.Clamp(slot, 0, count - 1);
        if (count == 1) return new Vector2(0, -1);
        if (count == 3 && slot == 2) return new Vector2(0, -2);
        return new Vector2(slot % 2 == 0 ? -.8f : .8f, slot < 2 ? -1 : -2);
    }

    sealed class Group
    {
        internal readonly List<SquadPatrolCharacterAi> roster = new List<SquadPatrolCharacterAi>(5);
        internal readonly PatrolRouteSnapshot route = new PatrolRouteSnapshot();
        internal SquadPatrolCharacterAi leader;
        internal Vector2 heading, previous;
        internal float compactUntil, regroup;
        readonly float spacing;
        readonly int sceneHandle;
        readonly Vector2[] trail = new Vector2[256];
        int trailHead, trailCount;
        int frame = -1, leaderIndex, cameraFrame = -1, spacingFrame = -1;
        float cachedSpacing;
        bool nearCamera;
        internal Group(SquadPatrolCharacterAi owner)
        {
            roster.Add(owner); leader = owner; spacing = owner.formationSpacing;
            sceneHandle = ZeldaRuntimeRegistry.GetGameplayScene(owner.gameObject).handle;
            regroup = owner.regroupDistance;
            heading = owner.FacingDirection; previous = owner.AiPosition; ResetTrail(previous);
        }
        internal float Spacing
        {
            get
            {
                if (spacingFrame == Time.frameCount) return cachedSpacing;
                spacingFrame = Time.frameCount;
                float result = spacing;
                foreach (var member in roster)
                    if (member != null && member.Eligible && member.SameScene(leader) && member.ownBody != null)
                        result = Mathf.Max(result, member.ownBody.bounds.size.magnitude + .22f);
                return cachedSpacing = result;
            }
        }
        internal void Refresh()
        {
            if (frame == Time.frameCount) return;
            frame = Time.frameCount;
            if (leader == null || !leader.Eligible ||
                ZeldaRuntimeRegistry.GetGameplayScene(leader.gameObject).handle != sceneHandle)
            {
                leader = null;
                for (int step = 1; step <= roster.Count; step++)
                {
                    int index = (leaderIndex + step) % roster.Count;
                    var candidate = roster[index];
                    if (candidate == null || !candidate.Eligible ||
                        ZeldaRuntimeRegistry.GetGameplayScene(candidate.gameObject).handle != sceneHandle) continue;
                    leader = candidate; leaderIndex = index; break;
                }
                if (leader == null) return;
                leader.ApplyPatrolRoute(route);
                heading = leader.FacingDirection; previous = leader.AiPosition;
                ResetTrail(previous); compactUntil = Time.time + .8f;
                spacingFrame = -1;
            }
            Vector2 position = leader.AiPosition;
            Vector2 delta = position - previous;
            if (delta.sqrMagnitude > .000001f)
                heading = Vector3.RotateTowards(heading, delta.normalized, 5f * Time.deltaTime, 0f);
            if (Vector2.Distance(trail[trailHead], position) >= .12f)
            {
                // Same newest-to-oldest trail, without shifting up to 256
                // positions for every captain movement sample.
                trailHead = (trailHead + trail.Length - 1) % trail.Length;
                trail[trailHead] = position;
                trailCount = Mathf.Min(trailCount + 1, trail.Length);
            }
            previous = position;
        }
        internal int SlotOf(SquadPatrolCharacterAi member, out int count)
        {
            int index = -1; count = 0;
            foreach (var candidate in roster)
            {
                if (candidate == null || candidate == leader || !candidate.Eligible || !candidate.SameScene(leader)) continue;
                if (candidate == member) index = count;
                count++;
            }
            return index;
        }
        internal Vector2 TrailPoint(float distance)
        {
            Vector2 from = leader.AiPosition;
            for (int i = 0; i < trailCount; i++)
            {
                Vector2 point = trail[(trailHead + i) % trail.Length];
                float length = Vector2.Distance(from, point);
                if (length >= distance && length > .001f) return Vector2.Lerp(from, point, distance / length);
                distance -= length; from = point;
            }
            // Seed a separated line when the squad has only just started or
            // changed captain; extend only through a verified clear corridor.
            Vector2 extension = from - heading * distance;
            return leader.IsBodyPathSegmentClear(from, extension, .03f) ? extension : from;
        }
        void ResetTrail(Vector2 position)
        {
            trailHead = 0; trailCount = 1; trail[0] = position;
        }
        internal bool IsNearCamera()
        {
            if (cameraFrame == Time.frameCount) return nearCamera;
            cameraFrame = Time.frameCount; nearCamera = false;
            var camera = Camera.main;
            if (camera == null || leader == null ||
                ZeldaRuntimeRegistry.GetGameplayScene(camera.gameObject) != ZeldaRuntimeRegistry.GetGameplayScene(leader.gameObject)) return false;
            foreach (var member in roster)
            {
                if (member == null || !member.Eligible || !member.SameScene(leader)) continue;
                Vector3 view = camera.WorldToViewportPoint(member.transform.position);
                if (view.z > 0 && view.x > -.1f && view.x < 1.1f && view.y > -.1f && view.y < 1.1f)
                { nearCamera = true; break; }
            }
            return nearCamera;
        }
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        if (members == null) members = new SquadPatrolCharacterAi[0];
        if (members.Length > 4) System.Array.Resize(ref members, 4);
        for (int i = 0; i < members.Length; i++)
        {
            if (members[i] == this) members[i] = null;
            for (int j = 0; j < i; j++) if (members[i] == members[j]) members[i] = null;
        }
        formationSpacing = Mathf.Max(.6f, formationSpacing);
        regroupDistance = Mathf.Max(2f, regroupDistance);
    }
    void OnDrawGizmosSelected()
    {
        Cache();
        Vector2 forward = Application.isPlaying && group != null ? group.heading :
            ownMover != null ? ownMover.InitialFacingVector : Vector2.up;
        if (forward.sqrMagnitude < .01f) forward = Vector2.up;
        Vector2 right = new Vector2(forward.y, -forward.x);
        int count = 0; foreach (var member in members) if (member != null && member != this) count++;
        Gizmos.color = new Color(.2f, .85f, 1f, .8f);
        for (int i = 0; i < count; i++)
        {
            Vector2 offset = FormationOffset(count, i) * formationSpacing;
            Vector3 target = transform.position + (Vector3)(right * offset.x + forward * offset.y);
            Gizmos.DrawWireSphere(target, .2f); Gizmos.DrawLine(transform.position, target);
        }
    }
}
