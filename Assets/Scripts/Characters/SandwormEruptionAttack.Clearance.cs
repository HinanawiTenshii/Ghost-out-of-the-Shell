using System.Collections.Generic;
using UnityEngine;

/// <summary>Non-damaging escape from compound body colliders after an explosive landing.</summary>
public sealed partial class SandwormEruptionAttack
{
    private const float LandingSeparationGap = .08f;
    private readonly List<Collider2D> landingBodies = new List<Collider2D>(12);
    private readonly List<Collider2D> landingOverlaps = new List<Collider2D>(32);
    private readonly List<Collider2D> landingTargets = new List<Collider2D>(16);
    private readonly HashSet<ZeldaCharacterData> landingCharacters = new HashSet<ZeldaCharacterData>();
    private readonly List<RaycastHit2D> landingCastHits = new List<RaycastHit2D>(32);

    private void ClearGroundedCharacterOverlaps()
    {
        if (parts == null || character == null || character.IsDead ||
            (Phase != AttackPhase.Stunned && Phase != AttackPhase.ReturningToBurrow && Phase != AttackPhase.RoarStunned)) return;
        Physics2D.SyncTransforms();
        landingBodies.Clear(); landingTargets.Clear(); landingCharacters.Clear();
        var filter = new ContactFilter2D { useTriggers = false };
        foreach (var part in parts)
        {
            var body = part != null ? part.GetComponent<Collider2D>() : null;
            if (body == null || !body.enabled || body.isTrigger || !body.gameObject.activeInHierarchy) continue;
            landingBodies.Add(body);
            landingOverlaps.Clear(); body.OverlapCollider(filter, landingOverlaps);
            foreach (var overlap in landingOverlaps)
            {
                if (overlap == null || !overlap.enabled || overlap.isTrigger) continue;
                var target = overlap.GetComponentInParent<ZeldaCharacterData>();
                if (target == null || target == character || target.IsDead || !target.ParticipatesInCharacterCollision ||
                    ZeldaRuntimeRegistry.GetGameplayScene(target.gameObject) != gameObject.scene ||
                    !landingCharacters.Add(target)) continue;
                landingTargets.Add(overlap);
            }
        }
        foreach (var body in landingTargets)
        {
            // Resolve against the whole compound worm, not just the first
            // touched section (which could push somebody into its neighbour).
            Collider2D nearest = null;
            float deepest = -.001f;
            foreach (var segment in landingBodies)
            {
                var contact = body.Distance(segment);
                if (!contact.isValid || !contact.isOverlapped || contact.distance >= deepest) continue;
                nearest = segment; deepest = contact.distance;
            }
            if (nearest == null) continue;
            var target = body.GetComponentInParent<ZeldaCharacterData>();
            Vector2 forward = nearest.transform.right;
            Vector2 side = new Vector2(-forward.y, forward.x);
            if (Vector2.Dot((Vector2)body.bounds.center - (Vector2)nearest.bounds.center, side) < 0f) side = -side;
            Vector2 displacement = Vector2.zero;
            float shortest = float.PositiveInfinity;
            ConsiderLandingEscape(body, target, side, ref displacement, ref shortest);
            ConsiderLandingEscape(body, target, -side, ref displacement, ref shortest);
            // Walls can close one or both lateral exits. Only then try oblique
            // or end exits; never teleport through a wall to reach an empty tile.
            if (float.IsPositiveInfinity(shortest))
            {
                ConsiderLandingEscape(body,target,(side+forward).normalized,ref displacement,ref shortest);
                ConsiderLandingEscape(body,target,(side-forward).normalized,ref displacement,ref shortest);
                ConsiderLandingEscape(body,target,(-side+forward).normalized,ref displacement,ref shortest);
                ConsiderLandingEscape(body,target,(-side-forward).normalized,ref displacement,ref shortest);
                ConsiderLandingEscape(body,target,forward,ref displacement,ref shortest);
                ConsiderLandingEscape(body,target,-forward,ref displacement,ref shortest);
            }
            if (float.IsPositiveInfinity(shortest)) continue;
            Vector3 position = target.transform.position + (Vector3)displacement;
            target.transform.position = position;
            var physics = target.GetComponent<Rigidbody2D>();
            if (physics != null) { physics.position = position; physics.velocity = Vector2.zero; }
            // Synchronize before resolving the next actor so actors pushed to
            // the same side cannot be placed into one another.
            Physics2D.SyncTransforms();
        }
    }

    private void ConsiderLandingEscape(Collider2D body, ZeldaCharacterData target, Vector2 direction,
        ref Vector2 selected, ref float shortest)
    {
        Vector2 origin = body.bounds.center;
        float distance = 0f;
        // Union of inflated segment bounds along this ray. AABB inflation is
        // conservative for rotated capsules and includes the actor's footprint.
        for (int pass = 0; pass <= landingBodies.Count; pass++)
        {
            float previous = distance;
            foreach (var segment in landingBodies)
            {
                Bounds expanded = segment.bounds;
                expanded.Expand(body.bounds.size + Vector3.one * LandingSeparationGap * 2f);
                if (LandingRayInterval(origin,direction,expanded,out float enter,out float exit) &&
                    enter <= distance + .001f && exit >= distance)
                    distance = exit + .01f;
            }
            if (distance <= previous) break;
        }
        if (distance <= 0f || distance >= shortest) return;
        landingCastHits.Clear();
        body.Cast(direction,new ContactFilter2D { useTriggers = true },landingCastHits,distance+LandingSeparationGap);
        foreach (var hit in landingCastHits)
        {
            var other = hit.collider;
            if (other == null || other.transform.IsChildOf(target.transform) || other.transform.IsChildOf(transform) ||
                ZeldaRuntimeRegistry.GetGameplayScene(other.gameObject) != gameObject.scene) continue;
            int layer = other.gameObject.layer;
            if (other.isTrigger && layer != LayerMask.NameToLayer("Blocks") && layer != LayerMask.NameToLayer("Hidden Blocks")) continue;
            var otherCharacter = other.GetComponentInParent<ZeldaCharacterData>();
            if (otherCharacter != null && !otherCharacter.ParticipatesInCharacterCollision) continue;
            // A surface reported at contact while moving parallel/away is not an obstacle.
            if (Vector2.Dot(direction,hit.normal) >= -.001f) continue;
            if (hit.distance < distance + LandingSeparationGap) return;
        }
        shortest = distance;
        selected = direction * distance;
    }

    private static bool LandingRayInterval(Vector2 origin, Vector2 direction, Bounds box, out float enter, out float exit)
    {
        enter = float.NegativeInfinity; exit = float.PositiveInfinity;
        for (int axis = 0; axis < 2; axis++)
        {
            if (Mathf.Abs(direction[axis]) < .00001f)
            {
                if (origin[axis] < box.min[axis] || origin[axis] > box.max[axis]) return false;
                continue;
            }
            float a = (box.min[axis]-origin[axis])/direction[axis];
            float b = (box.max[axis]-origin[axis])/direction[axis];
            enter = Mathf.Max(enter,Mathf.Min(a,b)); exit = Mathf.Min(exit,Mathf.Max(a,b));
            if (exit < enter) return false;
        }
        return exit >= 0f;
    }
}
