using UnityEngine;

public partial class ZeldaCharacterAiBase
{
    [Header("Pursuit Spacing")]
    [Tooltip("Extra space between pursuing characters, in addition to their collider sizes.")]
    [SerializeField, Min(0f)] private float pursuitSpacing = 0.16f;
    [Tooltip("How far ahead, in seconds of movement, to look for a slower pursuer.")]
    [SerializeField, Range(0.2f, 1.5f)] private float pursuitLookAheadTime = 0.7f;
    [Tooltip("Minimum movement speed advantage required to overtake a moving pursuer.")]
    [SerializeField, Min(0.01f)] private float pursuitOvertakeSpeedAdvantage = 0.15f;

    private ZeldaCharacterAiBase pursuitPassingActor;
    private Vector2 pursuitPassingForward;
    private Vector2 pursuitPassingSide;

    private float PursuitSpeed => characterData != null
        ? characterData.MoveSpeed * Mathf.Max(0f, AiMovementSpeedMultiplier) : 0f;

    private bool IsPursuitTrafficPeer(ZeldaCharacterAiBase other, Vector2 forward)
    {
        if (currentState != ZeldaAiState.Hostile || other == null || other == this ||
            !other.isActiveAndEnabled || other.currentState != ZeldaAiState.Hostile ||
            ZeldaRuntimeRegistry.GetGameplayScene(other.gameObject) !=
                ZeldaRuntimeRegistry.GetGameplayScene(gameObject) || other.characterData == null ||
            other.characterData.IsDead || !other.characterData.ParticipatesInCharacterCollision ||
            other.bodyCollider == null || !other.bodyCollider.enabled ||
            (other.mover != null && other.mover.isActiveAndEnabled)) return false;

        Object target = GetSharedFollowTarget();
        if (target == null || other.GetSharedFollowTarget() != target) return false;
        // Compare planned headings, not the lateral step made while overtaking.
        Vector2 otherForward = other.lastSeparationDesiredDirection;
        if (otherForward.sqrMagnitude < 0.001f) otherForward = other.moveDirection;
        return otherForward.sqrMagnitude < 0.001f ||
               Vector2.Dot(forward, otherForward.normalized) >
                   (other == pursuitPassingActor ? 0.35f : 0.65f);
    }

    private Vector2 PursuitOffsetTo(ZeldaCharacterAiBase other)
    {
        return (Vector2)(other.bodyCollider.bounds.center - bodyCollider.bounds.center);
    }

    private float PursuitBodyGap(Vector2 axis, ZeldaCharacterData other)
    {
        // The existing helper projects onto the perpendicular of its argument.
        return GetCharacterAvoidanceHalfWidth(new Vector2(-axis.y, axis.x), other) +
               Mathf.Max(0f, pursuitSpacing);
    }

    // Runs on the existing staggered separation clock, not every physics step.
    // It only steers locally; the wall path, collision casts and attack AI remain intact.
    private bool UpdatePursuitSpacing(Vector2 forward, out Vector2 steering, out float speedLimit)
    {
        steering = forward;
        speedLimit = 1f;
        float ownSpeed = PursuitSpeed;
        if (currentState != ZeldaAiState.Hostile || ownSpeed <= 0.001f ||
            !characterData.ParticipatesInCharacterCollision)
        {
            pursuitPassingActor = null;
            return false;
        }

        if (pursuitPassingActor != null &&
            (!IsPursuitTrafficPeer(pursuitPassingActor, forward) ||
             Vector2.Dot(forward, pursuitPassingForward) < 0.35f ||
             Vector2.Dot(PursuitOffsetTo(pursuitPassingActor), pursuitPassingForward) <
                -PursuitBodyGap(pursuitPassingForward, pursuitPassingActor.characterData)))
        {
            pursuitPassingActor = null;
        }

        ZeldaCharacterAiBase leader = pursuitPassingActor;
        float nearestForward = float.PositiveInfinity;
        Vector2 sideSeparation = Vector2.zero;
        float lookAhead = Mathf.Max(characterAvoidanceLookAheadDistance,
            ownSpeed * Mathf.Clamp(pursuitLookAheadTime, 0.2f, 1.5f));
        foreach (ZeldaCharacterAiBase other in ZeldaRuntimeRegistry.AiCharacters)
        {
            if (!IsPursuitTrafficPeer(other, forward)) continue;
            Vector2 offset = PursuitOffsetTo(other);
            float front = Vector2.Dot(offset, forward);
            Vector2 left = new Vector2(-forward.y, forward.x);
            float lateral = Vector2.Dot(offset, left);
            float sideGap = PursuitBodyGap(left, other.characterData);
            float frontGap = PursuitBodyGap(forward, other.characterData);
            if (Mathf.Abs(front) < frontGap * 0.5f && Mathf.Abs(lateral) < sideGap &&
                other != pursuitPassingActor && GetInstanceID() > other.GetInstanceID())
            {
                // Only one of an abreast pair yields, and never into a wall.
                Vector2 side = left * (Mathf.Abs(lateral) > 0.01f ? -Mathf.Sign(lateral) : 1f);
                float shift = sideGap - Mathf.Abs(lateral);
                if (IsBodyPathSegmentClear(rb.position, rb.position + side * shift, collisionSkinWidth))
                    sideSeparation += side * Mathf.Clamp01(shift / sideGap);
            }
            if (pursuitPassingActor == null && front > 0.01f && front < frontGap + lookAhead &&
                Mathf.Abs(lateral) < sideGap && front < nearestForward)
            {
                nearestForward = front;
                leader = other;
            }
        }

        if (leader == null)
        {
            steering = (forward + sideSeparation).normalized;
            return sideSeparation.sqrMagnitude > 0.001f;
        }

        Vector2 travel = leader == pursuitPassingActor ? pursuitPassingForward : forward;
        Vector2 toLeader = PursuitOffsetTo(leader);
        float longitudinal = Vector2.Dot(toLeader, travel);
        float longitudinalGap = PursuitBodyGap(travel, leader.characterData);
        Vector2 perpendicular = new Vector2(-travel.y, travel.x);
        float lateralGap = PursuitBodyGap(perpendicular, leader.characterData);
        float leaderSpeed = leader.IsStunned || leader.RemainsStationary ||
            (leader.mover != null && leader.mover.IsAttacking) ? 0f : leader.PursuitSpeed;
        float actualLeaderSpeed = leaderSpeed * Mathf.Clamp01(Vector2.Dot(leader.moveDirection, travel));
        bool wantsToPass = ownSpeed > leaderSpeed + Mathf.Max(0.01f, pursuitOvertakeSpeedAdvantage) ||
                           leader.moveDirection.sqrMagnitude < 0.001f;

        if (leader != pursuitPassingActor && wantsToPass)
        {
            float lateral = Vector2.Dot(toLeader, perpendicular);
            Vector2 preferred = perpendicular * (Mathf.Abs(lateral) > collisionSkinWidth
                ? -Mathf.Sign(lateral) : (GetInstanceID() < leader.GetInstanceID() ? -1f : 1f));
            if (IsPursuitPassingLaneClear(leader, travel, preferred, lateralGap, longitudinalGap))
                pursuitPassingSide = preferred;
            else if (IsPursuitPassingLaneClear(leader, travel, -preferred, lateralGap, longitudinalGap))
                pursuitPassingSide = -preferred;
            else
                preferred = Vector2.zero;

            if (preferred != Vector2.zero)
            {
                pursuitPassingActor = leader;
                pursuitPassingForward = travel;
            }
        }

        bool passingLaneOpen = false;
        if (leader == pursuitPassingActor)
        {
            // Retain the chosen side until the entire body has passed. If that
            // lane becomes blocked, follow/wait; do not oscillate between sides.
            if (IsPursuitPassingLaneClear(leader, travel, pursuitPassingSide, lateralGap, longitudinalGap))
            {
                passingLaneOpen = true;
                float lateralShift = Mathf.Max(0f, lateralGap + Vector2.Dot(toLeader, pursuitPassingSide));
                float sideAmount = Mathf.Clamp(lateralShift /
                    Mathf.Max(0.2f, longitudinal - longitudinalGap * 0.7f), 0f, 3f);
                steering = (travel + pursuitPassingSide * sideAmount + sideSeparation * 0.3f).normalized;
            }
        }

        // A follower matches the leader's actual velocity before contact. While
        // sidestepping, cap only forward progress so the lateral exit stays brisk.
        if (longitudinal > 0f && Mathf.Abs(Vector2.Dot(toLeader, perpendicular)) < lateralGap * 0.98f)
        {
            float responseTime = Mathf.Max(0.4f, characterSeparationRefreshInterval * 3f);
            float allowedForwardSpeed = Mathf.Max(0f,
                actualLeaderSpeed + (longitudinal - longitudinalGap) / responseTime);
            speedLimit = Mathf.Clamp01(allowedForwardSpeed /
                Mathf.Max(0.01f, ownSpeed * Mathf.Max(0.05f, Vector2.Dot(steering, travel))));
            if (passingLaneOpen && allowedForwardSpeed < 0.05f)
            {
                // Already close to a stopped actor: exit sideways first instead
                // of braking the lateral component to zero along with forward motion.
                steering = pursuitPassingSide;
                speedLimit = 1f;
            }
        }
        return true;
    }

    private bool IsPursuitPassingLaneClear(ZeldaCharacterAiBase leader, Vector2 forward,
        Vector2 side, float lateralGap, float longitudinalGap)
    {
        Vector2 offset = PursuitOffsetTo(leader);
        float shift = Mathf.Max(0f, lateralGap + Vector2.Dot(offset, side));
        Vector2 laneStart = rb.position + side * shift;
        Vector2 laneEnd = laneStart + forward * Mathf.Max(longitudinalGap,
            Vector2.Dot(offset, forward) + longitudinalGap);
        if (!IsBodyPathSegmentClear(rb.position, laneStart, collisionSkinWidth) ||
            !IsBodyPathSegmentClear(laneStart, laneEnd, collisionSkinWidth)) return false;

        // A free wall corridor can still contain a third character. Reuse the
        // registry and collider data to check the two short local segments.
        foreach (ZeldaFourWayMover otherMover in ZeldaRuntimeRegistry.Movers)
        {
            if (otherMover == null || otherMover == mover || otherMover == targetMover ||
                otherMover == leader.mover || !otherMover.gameObject.activeInHierarchy ||
                ZeldaRuntimeRegistry.GetGameplayScene(otherMover.gameObject) !=
                    ZeldaRuntimeRegistry.GetGameplayScene(gameObject)) continue;
            ZeldaCharacterData other = otherMover.CharacterData;
            if (other == null || other.IsDead || !other.ParticipatesInCharacterCollision) continue;
            Collider2D collider = other.GetComponent<Collider2D>();
            if (collider == null || !collider.enabled || collider.isTrigger) continue;
            if (PursuitLaneOverlapsCharacter(rb.position, laneStart, collider) ||
                PursuitLaneOverlapsCharacter(laneStart, laneEnd, collider)) return false;
        }
        return true;
    }

    private bool PursuitLaneOverlapsCharacter(Vector2 start, Vector2 end, Collider2D other)
    {
        Vector2 delta = end - start;
        float length = delta.magnitude;
        if (length < 0.001f) return false;
        Vector2 axis = delta / length;
        Vector2 normal = new Vector2(-axis.y, axis.x);
        Vector2 colliderOffset = (Vector2)bodyCollider.bounds.center - rb.position;
        Vector2 offset = (Vector2)other.bounds.center - (start + colliderOffset);
        Vector2 extents = (Vector2)(bodyCollider.bounds.extents + other.bounds.extents);
        float alongPadding = Mathf.Abs(axis.x) * extents.x + Mathf.Abs(axis.y) * extents.y;
        float sidePadding = Mathf.Abs(normal.x) * extents.x + Mathf.Abs(normal.y) * extents.y + collisionSkinWidth;
        float along = Vector2.Dot(offset, axis);
        return along > -alongPadding && along < length + alongPadding &&
               Mathf.Abs(Vector2.Dot(offset, normal)) < sidePadding;
    }
}
