using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gives a puppet-filled cardboard box the same A* navigation and physical
/// blocking rules as the deployed puppet. The puppet disembarks near its
/// lever and retains the magic left after transporting the box.
/// </summary>
public sealed class ClockworkPuppetBoxDriver : MonoBehaviour
{
    private static readonly HashSet<ClockworkPuppetBoxDriver> ActiveSet =
        new HashSet<ClockworkPuppetBoxDriver>();

    private CardboardBoxPickupItem box;
    private ClockworkPuppetPickupItem puppet;
    private LeverData target;
    private ZeldaReusableGridNavigator navigator;
    private Rigidbody2D physicsBody;
    private Collider2D drivingCollider;
    private bool originalTriggerState;
    private bool addedPhysicsBody;
    private bool addedNavigator;
    private float magic;
    private float disembarkDistance;
    private float blockedNearTargetTimer;
    private bool disembarking;
    private string itemInstanceId;
    private string itemName;
    private string itemDescription;
    private bool hasVisualColor;
    private Color visualColor;
    private Vector2 previousPhysicsPosition;
    private float lastMovementTime = float.NegativeInfinity;
    private CameraVisionRevealSource revealSource;
    private CameraVisionStreamingExempt streamingExemption;
    private bool addedStreamingExemption;

    public bool IsMoving => !disembarking &&
        Time.time - lastMovementTime <= 0.15f;
    public float RemainingMagic => magic;
    public static IReadOnlyCollection<ClockworkPuppetBoxDriver> ActiveDrivers =>
        ActiveSet;

    public bool DoesCurrentPathIntersect(
        Collider2D targetCollider,
        float lookAheadDistance)
    {
        return !disembarking && box != null && !box.IsDestroyed &&
            navigator != null &&
            navigator.DoesCurrentPathIntersect(
                targetCollider,
                lookAheadDistance);
    }

    private void OnEnable()
    {
        ActiveSet.Add(this);
        if (revealSource != null)
        {
            revealSource.enabled = true;
        }
    }

    private void OnDisable()
    {
        ActiveSet.Remove(this);
        if (revealSource != null)
        {
            revealSource.enabled = false;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        ActiveSet.Clear();
    }

    public void Configure(
        CardboardBoxPickupItem owner,
        ClockworkPuppetPickupItem source,
        float initialMagic,
        string configuredInstanceId,
        string configuredItemName,
        string configuredItemDescription,
        bool configuredHasVisualColor,
        Color configuredVisualColor)
    {
        box = owner;
        puppet = source;
        magic = source != null
            ? Mathf.Clamp(initialMagic, 0f, source.MaximumMagic)
            : 0f;
        itemInstanceId = configuredInstanceId;
        itemName = configuredItemName;
        itemDescription = configuredItemDescription;
        hasVisualColor = configuredHasVisualColor;
        visualColor = configuredVisualColor;
        drivingCollider = GetComponent<Collider2D>();
        if (drivingCollider != null)
        {
            originalTriggerState = drivingCollider.isTrigger;
            drivingCollider.isTrigger = false;
        }

        physicsBody = GetComponent<Rigidbody2D>();
        if (physicsBody == null)
        {
            physicsBody = gameObject.AddComponent<Rigidbody2D>();
            addedPhysicsBody = true;
        }
        physicsBody.bodyType = RigidbodyType2D.Kinematic;
        physicsBody.gravityScale = 0f;
        physicsBody.constraints = RigidbodyConstraints2D.FreezeRotation;
        physicsBody.interpolation = RigidbodyInterpolation2D.Interpolate;
        physicsBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        navigator = GetComponent<ZeldaReusableGridNavigator>();
        if (navigator == null)
        {
            navigator = gameObject.AddComponent<ZeldaReusableGridNavigator>();
            addedNavigator = true;
        }
        navigator.Configure(physicsBody, drivingCollider);
        navigator.ConfigureNavigationDebug(
            source != null && source.ShowNavigationDebug,
            source != null
                ? source.NavigationPathColor
                : new Color(0.15f, 0.85f, 1f, 0.9f),
            source != null
                ? source.NavigationTargetColor
                : new Color(1f, 0.25f, 0.75f, 1f),
            source != null ? source.NavigationPathWidth : 0.035f,
            source != null ? source.NavigationTargetMarkerSize : 0.18f,
            source != null ? source.NavigationDebugSortingOrder : 200);
        revealSource = GetComponent<CameraVisionRevealSource>();
        if (revealSource == null)
        {
            revealSource = gameObject.AddComponent<CameraVisionRevealSource>();
        }
        revealSource.Configure(
            source != null ? source.VisionRevealRadius : 3f);
        streamingExemption = GetComponent<CameraVisionStreamingExempt>();
        if (streamingExemption == null)
        {
            streamingExemption =
                gameObject.AddComponent<CameraVisionStreamingExempt>();
            addedStreamingExemption = true;
        }
        previousPhysicsPosition = physicsBody.position;

        float boxRadius = drivingCollider != null
            ? Mathf.Max(drivingCollider.bounds.extents.x,
                drivingCollider.bounds.extents.y)
            : 0.35f;
        // Leave the container before it is pressed directly against the
        // mechanism. The smaller deployed puppet completes the final approach.
        disembarkDistance = Mathf.Max(1.05f, boxRadius + 0.62f);
        SelectNearestTarget();
    }

    private void FixedUpdate()
    {
        if (physicsBody != null)
        {
            Vector2 currentPosition = physicsBody.position;
            if ((currentPosition - previousPhysicsPosition).sqrMagnitude >
                0.000001f)
            {
                lastMovementTime = Time.time;
            }
            previousPhysicsPosition = currentPosition;
        }

        if (disembarking || box == null || puppet == null ||
            box.IsDestroyed || !box.HasStoredItem)
        {
            return;
        }
        if (magic <= 0f)
        {
            box.DestroyStoredClockworkPuppet();
            return;
        }
        if (target == null || !target.isActiveAndEnabled)
        {
            SelectNearestTarget();
        }
        if (target == null)
        {
            return;
        }

        Vector2 destination = target.PuppetMountPoint;
        if (IsCloseEnoughToDisembark(destination))
        {
            DisembarkPuppet(destination);
            return;
        }

        Vector2 direction = navigator != null
            ? navigator.GetDirection(destination, disembarkDistance)
            : (destination - (Vector2)transform.position).normalized;
        Vector2 displacement = navigator != null
            ? navigator.GetSafeDisplacement(
                direction,
                puppet.MoveSpeed * 0.65f * Time.fixedDeltaTime)
            : direction * puppet.MoveSpeed * 0.65f * Time.fixedDeltaTime;
        if (displacement.sqrMagnitude <= 0.000001f &&
            Vector2.Distance(transform.position, destination) <=
                disembarkDistance + 0.55f)
        {
            blockedNearTargetTimer += Time.fixedDeltaTime;
            if (blockedNearTargetTimer >= 0.22f)
            {
                DisembarkPuppet(destination);
                return;
            }
        }
        else
        {
            blockedNearTargetTimer = 0f;
        }
        box.MoveGroundBoxWithNavigation(physicsBody, displacement);
        if (displacement.sqrMagnitude > 0.000001f)
        {
            // Keep the AI-visible movement state continuous across the one
            // physics step between MovePosition and the resulting position.
            lastMovementTime = Time.time;
            magic = Mathf.Max(
                0f,
                magic - puppet.MovementMagicPerSecond * Time.fixedDeltaTime);
            if (magic <= 0f)
            {
                box.DestroyStoredClockworkPuppet();
            }
        }
    }

    private bool IsCloseEnoughToDisembark(Vector2 leverPosition)
    {
        if (Vector2.Distance(transform.position, leverPosition) <=
            disembarkDistance)
        {
            return true;
        }
        if (drivingCollider == null || target == null)
        {
            return false;
        }

        // A solid box reaches collider contact before its centre can reach the
        // lever head. Use surface-to-surface separation so that collision is
        // treated as a successful arrival rather than a permanent blockage.
        Collider2D[] leverColliders =
            target.GetComponentsInChildren<Collider2D>(true);
        float allowedSurfaceGap = Mathf.Max(
            0.08f,
            puppet != null ? puppet.LeverContactDistance + 0.12f : 0.3f);
        for (int i = 0; i < leverColliders.Length; i++)
        {
            Collider2D leverCollider = leverColliders[i];
            if (leverCollider == null || leverCollider.isTrigger ||
                !leverCollider.enabled)
            {
                continue;
            }
            ColliderDistance2D separation =
                drivingCollider.Distance(leverCollider);
            if (separation.isOverlapped || separation.distance <= allowedSurfaceGap)
            {
                return true;
            }
        }
        return false;
    }

    private void DisembarkPuppet(Vector2 leverPosition)
    {
        if (disembarking || box == null || puppet == null)
        {
            return;
        }
        disembarking = true;
        // Spawn at the box centre rather than between the box and lever. The
        // box has already proven this position is free, and the puppet's
        // collider is smaller than the box collider.
        Vector3 spawnPosition = drivingCollider != null
            ? drivingCollider.bounds.center
            : (Vector3)box.WorldCenter;
        ClockworkPuppetPickupItem source = puppet;
        LeverData destinationLever = target;
        float transferredMagic = magic;
        if (!box.TryDetachStoredClockworkPuppet(source))
        {
            disembarking = false;
            return;
        }
        // Clear the temporary solid container immediately. Waiting for this
        // component's deferred OnDestroy would make the newly spawned puppet
        // collide with its own box during its first physics step.
        if (drivingCollider != null)
        {
            drivingCollider.isTrigger = originalTriggerState;
        }
        if (physicsBody != null)
        {
            physicsBody.velocity = Vector2.zero;
        }
        Physics2D.SyncTransforms();
        source.DeployFromCardboardBox(
            spawnPosition,
            destinationLever,
            transferredMagic,
            itemInstanceId,
            itemName,
            itemDescription,
            hasVisualColor,
            visualColor);
        enabled = false;
    }

    private void SelectNearestTarget()
    {
        target = null;
        float searchRadius = puppet != null
            ? puppet.LeverSearchRadius
            : 0f;
        float best = searchRadius > 0f
            ? searchRadius * searchRadius
            : float.MaxValue;
        foreach (LeverData lever in LeverData.WorldLevers)
        {
            if (lever == null || !lever.isActiveAndEnabled) continue;
            float sqr = (lever.PuppetMountPoint -
                         (Vector2)transform.position).sqrMagnitude;
            if (sqr <= best)
            {
                best = sqr;
                target = lever;
            }
        }
        if (navigator != null) navigator.InvalidatePath();
    }

    private void OnDestroy()
    {
        ActiveSet.Remove(this);
        if (revealSource != null)
        {
            Destroy(revealSource);
        }
        if (addedStreamingExemption && streamingExemption != null)
        {
            Destroy(streamingExemption);
        }
        if (drivingCollider != null)
        {
            drivingCollider.isTrigger = originalTriggerState;
        }
        if (addedNavigator && navigator != null)
        {
            Destroy(navigator);
        }
        if (addedPhysicsBody && physicsBody != null)
        {
            Destroy(physicsBody);
        }
    }
}
