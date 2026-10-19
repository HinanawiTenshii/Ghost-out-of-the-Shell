using System.Collections.Generic;
using UnityEngine;

public sealed partial class CameraVisionObjectStreaming
{
    private sealed class PatrolGroupState
    {
        internal PatrolStreamingGroup group;
        internal float lastVisibleTime;
        internal bool keepLoaded;
        internal readonly List<ManagedObject> objects = new List<ManagedObject>();
    }

    private readonly List<PatrolGroupState> patrolGroups = new List<PatrolGroupState>();
    private readonly Dictionary<PatrolStreamingGroup, PatrolGroupState> patrolGroupLookup =
        new Dictionary<PatrolStreamingGroup, PatrolGroupState>();
    private readonly HashSet<ManagedObject> groupedPatrolObjects = new HashSet<ManagedObject>();
    private readonly HashSet<ManagedObject> retainedPatrolObjects = new HashSet<ManagedObject>();
    private float nextPatrolGroupCheck;
    private int patrolGroupVersion = -1, patrolManagedCount = -1;

    private ManagedObject FindPatrolManagedObject(Transform member)
    {
        for (Transform current = member; current != null; current = current.parent)
            if (managedLookup.TryGetValue(current.gameObject, out ManagedObject item)) return item;
        return null;
    }

    private bool IsPatrolManuallyDisabled(Transform member)
    {
        if (member.gameObject.activeInHierarchy) return false;
        for (Transform current = member; current != null; current = current.parent)
            if (!current.gameObject.activeSelf &&
                (!managedLookup.TryGetValue(current.gameObject, out ManagedObject item) || !item.suspendedByManager))
                return true;
        return false;
    }

    private void UpdatePatrolStreamingGroups()
    {
        float now = Time.unscaledTime;
        if (now < nextPatrolGroupCheck && patrolGroupVersion == PatrolStreamingGroup.RegistryVersion &&
            patrolManagedCount == managedObjects.Count) return;
        nextPatrolGroupCheck = now + visibilityCheckInterval;
        patrolGroupVersion = PatrolStreamingGroup.RegistryVersion;
        patrolManagedCount = managedObjects.Count;

        // Register by explicit references, without scene scans, physics probes,
        // or per-frame route simulation. Lists/sets retain their capacities.
        for (int i = patrolGroups.Count - 1; i >= 0; i--)
        {
            var state = patrolGroups[i];
            if (state.group != null && state.group.isActiveAndEnabled && state.group.Owner == this) continue;
            if (state.group != null) state.group.Release(this);
            patrolGroupLookup.Remove(state.group);
            patrolGroups.RemoveAt(i);
        }
        foreach (var group in PatrolStreamingGroup.ActiveGroups)
        {
            if (group == null || !group.isActiveAndEnabled || !group.Claim(this) || patrolGroupLookup.ContainsKey(group)) continue;
            var state = new PatrolGroupState { group = group, lastVisibleTime = now, keepLoaded = true };
            patrolGroups.Add(state);
            patrolGroupLookup.Add(group, state);
        }

        groupedPatrolObjects.Clear();
        retainedPatrolObjects.Clear();
        foreach (var state in patrolGroups)
        {
            state.objects.Clear();
            bool inRange = false;
            foreach (var member in state.group.RuntimeMembers)
            {
                if (!member.Participates || IsPatrolManuallyDisabled(member.ai.transform)) continue;
                var item = FindPatrolManagedObject(member.ai.transform);
                if (item != null && !state.objects.Contains(item))
                {
                    state.objects.Add(item);
                    groupedPatrolObjects.Add(item);
                }
                // Combat/investigation/possession keeps the existing independent
                // retention rules, and does not hold a distant idle group awake.
                if (!member.IsOrdinaryPatrol) continue;
                if (!inRange) inRange = IsPatrolRouteInStreamingRange(member.ai);
                if (item != null)
                    inRange |= IsInsideStreamingRetentionRange(item) || HasRuntimeStreamingExemption(item.gameObject.transform);
                else
                {
                    // Discovery is budgeted too. An as-yet unregistered active
                    // member must still wake its group when it is visible.
                    Bounds bounds = member.body != null ? member.body.bounds :
                        new Bounds(member.ai.transform.position, Vector3.one * .1f);
                    inRange |= !hasStreamingPlanes || IntersectsStreamingViewport(bounds, streamingPlanes, currentPreloadPadding) ||
                        HasRuntimeStreamingExemption(member.ai.transform);
                }
            }
            if (inRange) state.lastVisibleTime = now;
            state.keepLoaded = inRange || (!initialPreparationRunning && now - state.lastVisibleTime < unloadDelay);
            // Freeze before any budgeted SetActive call. Only the end-of-frame
            // readiness barrier releases the patrol clocks, all at once.
            if (!state.keepLoaded || !IsPatrolGroupReady(state)) state.group.SetPaused(this, true);
            if (!state.keepLoaded) continue;
            foreach (var item in state.objects)
            {
                retainedPatrolObjects.Add(item);
                item.outsideTimer = 0f;
                if (item.suspendedByManager) QueueActivation(item);
            }
        }
    }

    private bool IsPatrolRouteInStreamingRange(ZeldaPatrolCharacterAi ai)
    {
        int count = ai.StreamingRoutePointCount;
        if (count == 0) return false;
        if (!hasStreamingPlanes) return true;
        float z = ai.transform.position.z;
        Bounds bounds = ai.StreamingRouteBounds;
        bounds.center = new Vector3(bounds.center.x, bounds.center.y, z);
        bool nearViewport = IntersectsStreamingViewport(bounds, streamingPlanes, currentPreloadPadding);
        var scene = ZeldaRuntimeRegistry.GetGameplayScene(ai.gameObject);
        bool nearRegion = CameraVisionStreamingRegion.Intersects(scene, bounds);
        if (!nearViewport && !nearRegion) return false;

        // A route's large bounding box is only a broad-phase test: the camera
        // inside an empty loop must not retain the group unless a leg is near it.
        for (int i = 0; i < count; i++)
        {
            Vector2 a = ai.GetStreamingRoutePoint(i);
            Vector2 b = ai.GetStreamingRoutePoint((i + 1) % count);
            if (nearViewport && IntersectsStreamingSegment(new Vector3(a.x, a.y, z),
                new Vector3(b.x, b.y, z), streamingPlanes, currentPreloadPadding)) return true;
            if (nearRegion && CameraVisionStreamingRegion.IntersectsSegment(scene, a, b)) return true;
        }
        return false;
    }

    private static bool IntersectsStreamingSegment(Vector3 a, Vector3 b, Plane[] planes, float padding)
    {
        float enter = 0f, exit = 1f;
        for (int i = 0; i < 6; i++)
        {
            float margin = i < 4 ? padding : 0f;
            float start = planes[i].GetDistanceToPoint(a) + margin;
            float end = planes[i].GetDistanceToPoint(b) + margin;
            if (start < 0f && end < 0f) return false;
            if (start < 0f) enter = Mathf.Max(enter, start / (start - end));
            else if (end < 0f) exit = Mathf.Min(exit, start / (start - end));
            if (enter > exit) return false;
        }
        return true;
    }

    private void CompletePatrolGroupActivations()
    {
        foreach (var state in patrolGroups)
        {
            if (state.group == null || !state.keepLoaded || !state.group.PatrolPaused) continue;
            if (IsPatrolGroupReady(state)) state.group.SetPaused(this, false);
        }
    }

    private bool IsPatrolGroupReady(PatrolGroupState state)
    {
        foreach (var member in state.group.RuntimeMembers)
        {
            if (!member.IsOrdinaryPatrol || IsPatrolManuallyDisabled(member.ai.transform)) continue;
            if (!member.ai.gameObject.activeInHierarchy) return false;
        }
        return true;
    }

    private void ReleasePatrolStreamingGroups()
    {
        foreach (var state in patrolGroups)
            if (state.group != null) state.group.Release(this);
        patrolGroups.Clear();
        patrolGroupLookup.Clear();
        groupedPatrolObjects.Clear();
        retainedPatrolObjects.Clear();
        patrolGroupVersion = patrolManagedCount = -1;
        nextPatrolGroupCheck = 0f;
    }
}
