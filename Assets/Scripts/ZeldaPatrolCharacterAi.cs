using UnityEngine;

/// <summary>
/// Patrol variant whose Idle state walks a four-point loop. Point 1 is always
/// the character's position when the scene loads; the other three are offsets
/// from that position so the route remains portable with prefab instances.
/// </summary>
public class ZeldaPatrolCharacterAi : ZeldaCharacterAiBase
{
    [Header("Four Point Idle Patrol")]
    [Tooltip("Movement speed multiplier used while following the patrol route. Other AI states continue to use full movement speed.")]
    [SerializeField, Min(0.01f)] private float patrolSpeedMultiplier = 0.75f;
    [Tooltip("Patrol point 2, relative to the character's scene-load position.")]
    [SerializeField] private Vector2 patrolPoint2Offset = new Vector2(2f, 0f);
    [Tooltip("Patrol point 3, relative to the character's scene-load position.")]
    [SerializeField] private Vector2 patrolPoint3Offset = new Vector2(2f, 2f);
    [Tooltip("Patrol point 4, relative to the character's scene-load position.")]
    [SerializeField] private Vector2 patrolPoint4Offset = new Vector2(0f, 2f);
    [SerializeField, Min(0.01f)] private float patrolPointTolerance = 0.12f;
    [Header("Patrol Point Wait Times")]
    [Tooltip("Seconds to wait at the scene-load position (patrol point 1).")]
    [SerializeField, Min(0f)] private float waitAtPoint1Duration = 1f;
    [Tooltip("Seconds to wait at patrol point 2.")]
    [SerializeField, Min(0f)] private float waitAtPoint2Duration = 1f;
    [Tooltip("Seconds to wait at patrol point 3.")]
    [SerializeField, Min(0f)] private float waitAtPoint3Duration = 1f;
    [Tooltip("Seconds to wait at patrol point 4.")]
    [SerializeField, Min(0f)] private float waitAtPoint4Duration = 1f;

    private readonly Vector2[] patrolPoints = new Vector2[4];
    private int currentPatrolPointIndex;
    private float waitTimer;

    protected override float AiMovementSpeedMultiplier =>
        CurrentState == ZeldaAiState.Idle ? patrolSpeedMultiplier : 1f;

    protected override void Awake()
    {
        base.Awake();
        RebuildPatrolPoints();
        // The character already starts at point 1, so its first destination is point 2.
        currentPatrolPointIndex = 1;
    }

    protected override void TickIdle(float deltaTime)
    {
        if (waitTimer > 0f)
        {
            waitTimer = Mathf.Max(0f, waitTimer - deltaTime);
            AiMoveDirection = Vector2.zero;
            return;
        }

        Vector2 toPatrolPoint = patrolPoints[currentPatrolPointIndex] - AiPosition;
        if (toPatrolPoint.sqrMagnitude <= patrolPointTolerance * patrolPointTolerance)
        {
            // Stop at the current destination before selecting and travelling
            // toward the next point in the loop.
            AiMoveDirection = Vector2.zero;
            waitTimer = GetWaitDuration(currentPatrolPointIndex);
            currentPatrolPointIndex = (currentPatrolPointIndex + 1) % patrolPoints.Length;
            return;
        }

        AiMoveDirection = PlanAiPathTo(patrolPoints[currentPatrolPointIndex]);
    }

    protected override void OnStateEntered(ZeldaAiState previousState, ZeldaAiState newState)
    {
        base.OnStateEntered(previousState, newState);
        if (newState == ZeldaAiState.Idle && previousState == ZeldaAiState.Recovery)
        {
            // Recovery returns to point 1; resume the normal 1 -> 2 -> 3 -> 4 loop.
            RebuildPatrolPoints();
            currentPatrolPointIndex = 1;
            waitTimer = waitAtPoint1Duration;
        }
    }

    private float GetWaitDuration(int pointIndex)
    {
        switch (pointIndex)
        {
            case 0: return waitAtPoint1Duration;
            case 1: return waitAtPoint2Duration;
            case 2: return waitAtPoint3Duration;
            default: return waitAtPoint4Duration;
        }
    }

    private void RebuildPatrolPoints()
    {
        patrolPoints[0] = InitialScenePosition;
        patrolPoints[1] = InitialScenePosition + patrolPoint2Offset;
        patrolPoints[2] = InitialScenePosition + patrolPoint3Offset;
        patrolPoints[3] = InitialScenePosition + patrolPoint4Offset;
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        patrolSpeedMultiplier = Mathf.Max(0.01f, patrolSpeedMultiplier);
        patrolPointTolerance = Mathf.Max(0.01f, patrolPointTolerance);
        waitAtPoint1Duration = Mathf.Max(0f, waitAtPoint1Duration);
        waitAtPoint2Duration = Mathf.Max(0f, waitAtPoint2Duration);
        waitAtPoint3Duration = Mathf.Max(0f, waitAtPoint3Duration);
        waitAtPoint4Duration = Mathf.Max(0f, waitAtPoint4Duration);
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 origin = transform.position;
        Vector3[] previewPoints =
        {
            origin,
            origin + (Vector3)patrolPoint2Offset,
            origin + (Vector3)patrolPoint3Offset,
            origin + (Vector3)patrolPoint4Offset
        };

        Color previousColor = Gizmos.color;
        Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.9f);
        for (int i = 0; i < previewPoints.Length; i++)
        {
            Gizmos.DrawWireSphere(previewPoints[i], 0.12f);
            Gizmos.DrawLine(previewPoints[i], previewPoints[(i + 1) % previewPoints.Length]);
        }

        Gizmos.color = previousColor;
    }
}
