using UnityEngine;

public partial class ZeldaCharacterAiBase
{
    internal bool AssignedSegmentClear(Vector2 from, Vector2 to) => IsBodyPathSegmentClear(from, to, .02f);
    // Incident-time only. Authored patrol segments can pass beside a wall while
    // their interpolated points fall inside it; never feed those as exact goals.
    internal Vector2 ResolveAssignedDestination(Vector2 point)
    {
        if (AssignedDestinationClear(point)) return point;
        for (int ring = 1; ring <= 10; ring++)
            for (int i = 0; i < 16; i++)
            {
                float angle = (90f + i * 22.5f) * Mathf.Deg2Rad;
                Vector2 candidate = point + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (.25f * ring);
                if (AssignedDestinationClear(candidate)) return candidate;
            }
        return point;
    }
    internal bool AssignedDestinationClear(Vector2 point)
    {
        Vector2 scale = transform.lossyScale;
        Vector2 size = Vector2.Scale(bodyCollider.size, new Vector2(Mathf.Abs(scale.x), Mathf.Abs(scale.y))) + Vector2.one * .24f;
        int count = Physics2D.OverlapBoxNonAlloc(point + (Vector2)transform.TransformVector(bodyCollider.offset),
            size, bodyCollider.transform.eulerAngles.z, gridOverlapHits, solidCollisionLayers);
        for (int i = 0; i < count; i++)
        {
            var hit = gridOverlapHits[i];
            if (hit == null || hit.isTrigger || hit == bodyCollider || hit.transform.IsChildOf(transform) ||
                IsUnlockedDoorCollider(hit) || hit.GetComponentInParent<ZeldaCharacterData>() != null) continue;
            return false;
        }
        return count < gridOverlapHits.Length;
    }
    internal void ResetForAssignedPost()
    {
        if (sharedPlayerTimer != null) sharedPlayerTimer.Leave(this);
        decoyBombTarget = null;
        alarmResponseActive = pendingAlarmResponse = false;
        royalCommandRemaining = 0;
        if (currentState == ZeldaAiState.Idle && targetMover == null && !mover.IsAttacking) return;
        mover.CancelAttackForStun();
        BeginRecovery();
        ChangeState(ZeldaAiState.Idle);
        InvalidateNavigationPlan();
    }
}
