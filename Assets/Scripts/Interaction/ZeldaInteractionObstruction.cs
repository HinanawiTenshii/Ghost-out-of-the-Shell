using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Shared reach test for physical interactions, not remote dialogue or possession.</summary>
public static class ZeldaInteractionObstruction
{
    private static RaycastHit2D[] hits = new RaycastHit2D[32];
    private static Collider2D[] overlaps = new Collider2D[16];
    private static readonly List<Collider2D> targetColliders = new List<Collider2D>(8);
    private static readonly List<Renderer> targetRenderers = new List<Renderer>(8);
    private static readonly int pickupWallLayers = LayerMask.GetMask("Blocks", "Hidden Blocks");

    public static bool HasClearPath(Component target, ZeldaFourWayMover actor, Vector2 interactionPosition)
    {
        return actor != null && HasClearPath(target, actor.transform, actor.InteractionBodyCenter, interactionPosition);
    }

    // Puppet/box controls do not use ZeldaFourWayMover. Their local interactions
    // must follow the same obstruction rule without testing from the remote host.
    public static bool HasClearPath(Component target, Transform actorRoot, Vector2 origin, Vector2 interactionPosition)
    {
        if (target == null || actorRoot == null || !target.gameObject.activeInHierarchy || !actorRoot.gameObject.activeInHierarchy ||
            ZeldaRuntimeRegistry.GetGameplayScene(target.gameObject) != ZeldaRuntimeRegistry.GetGameplayScene(actorRoot.gameObject)) return false;
        Vector2 destination = GetApproachPoint(target, origin, interactionPosition);
        var filter = new ContactFilter2D { useTriggers = true };
        filter.SetLayerMask(Physics2D.AllLayers);
        bool previousHitTriggers = Physics2D.queriesHitTriggers;
        try
        {
            if (!previousHitTriggers) Physics2D.queriesHitTriggers = true;
            while (true)
            {
                int count = Physics2D.Linecast(origin, destination, filter, hits);
                for (int i = 0; i < count; i++)
                    if (Blocks(hits[i].collider, target, actorRoot)) return false;
                if (count < hits.Length) break;
                Array.Resize(ref hits, hits.Length * 2);
            }
            // Explicit endpoints also work when queriesStartInColliders is off.
            return !PointBlocked(origin, filter, target, actorRoot) && !PointBlocked(destination, filter, target, actorRoot);
        }
        finally
        {
            if (!previousHitTriggers) Physics2D.queriesHitTriggers = false;
        }
    }

    private static bool PointBlocked(Vector2 point, ContactFilter2D filter, Component target, Transform actorRoot)
    {
        while (true)
        {
            int count = Physics2D.OverlapPoint(point, filter, overlaps);
            for (int i = 0; i < count; i++)
                if (Blocks(overlaps[i], target, actorRoot)) return true;
            if (count < overlaps.Length) return false;
            Array.Resize(ref overlaps, overlaps.Length * 2);
        }
    }

    private static bool Blocks(Collider2D hit, Component target, Transform actorRoot)
    {
        // Only this interactable and its children are exempt, never the common
        // scene/group parent: a neighbouring wall may share that parent.
        if (hit == null || hit.transform.IsChildOf(target.transform) || hit.transform.IsChildOf(actorRoot) ||
            !ZeldaFourWayMover.ColliderBlocksPossession(hit) || !hit.gameObject.activeInHierarchy ||
            ZeldaRuntimeRegistry.GetGameplayScene(hit.gameObject) != ZeldaRuntimeRegistry.GetGameplayScene(actorRoot.gameObject)) return false;

        // Items can be reached across furniture (including items placed on a
        // solid tabletop). Reading/mechanisms keep the stricter solid-obstacle
        // rule. Use the same policy for both the prompt and the final E request.
        if (!(target is PickupItemBase) && !(target is GrowthCollectible)) return true;
        if ((pickupWallLayers & (1 << hit.gameObject.layer)) != 0) return true;

        // Glass can be authored on Default or Visible Non Blocking, and its
        // collider may live on a child. Identify the material, not its name or
        // durability, so reinforced glass blocks pickups just like ordinary glass.
        var glass = hit.GetComponentInParent<DoorData>();
        return glass != null && glass.IsBreakableGlass;
    }

    private static Vector2 GetApproachPoint(Component target, Vector2 origin, Vector2 offeredPoint)
    {
        if (target is SceneTransitionPoint || target is PersistentSceneTransitionPoint)
        {
            // These offer at the player's position intentionally. Their overlap
            // area is the destination, not a pivot which may lie inside a wall.
            var area = target.GetComponent<Collider2D>();
            return area != null && area.enabled ? area.ClosestPoint(origin) : offeredPoint;
        }
        if (target is TrapdoorInteraction) offeredPoint = target.transform.position;
        // Rear operating points (cannons/ballistas), cable ports and a readable's
        // authored surface must retain their meaning rather than switching sides.
        if ((offeredPoint - (Vector2)target.transform.position).sqrMagnitude > .000001f) return offeredPoint;

        // Reach the physical surface, not a hinge/pivot buried in a wall. Pure
        // trigger areas must NOT shorten this ray (e.g. a pickup across a wall).
        target.GetComponentsInChildren(false, targetColliders);
        float best = float.PositiveInfinity;
        Vector2 point = offeredPoint;
        for (int i = 0; i < targetColliders.Count; i++)
        {
            var collider = targetColliders[i];
            if (collider == null || !collider.enabled || collider.isTrigger ||
                (collider.attachedRigidbody != null && !collider.attachedRigidbody.simulated)) continue;
            Vector2 surface = collider.ClosestPoint(origin);
            float distance = (surface - origin).sqrMagnitude;
            if (distance < best) { best = distance; point = surface; }
        }
        targetColliders.Clear();
        if (float.IsPositiveInfinity(best) && target is DoorHingeInteraction)
        {
            // Open doors disable their colliders but remain closable from their
            // visible surface, matching the door's existing proximity rule.
            target.GetComponentsInChildren(false, targetRenderers);
            for (int i = 0; i < targetRenderers.Count; i++)
            {
                var renderer = targetRenderers[i];
                if (renderer == null || !renderer.enabled) continue;
                Vector2 surface = renderer.bounds.ClosestPoint(origin);
                float distance = (surface - origin).sqrMagnitude;
                if (distance < best) { best = distance; point = surface; }
            }
            targetRenderers.Clear();
        }
        return point;
    }
}
