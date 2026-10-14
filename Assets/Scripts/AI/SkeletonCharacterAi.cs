using System.Collections.Generic;
using UnityEngine;

/// <summary>Always hunts living non-skeleton bodies; no permission/faction alliance.</summary>
public sealed class SkeletonCharacterAi : ZeldaPatrolCharacterAi
{
    [Header("Skeleton Patrol / 骷髅巡逻")]
    [SerializeField, Tooltip("启用时复用普通巡逻路径；关闭时保持原版骷髅静止待机。索敌、复原和水晶限制不变。")]
    private bool enableIdlePatrol;
    public override bool HasIdlePatrol => enableIdlePatrol;
    private SkeletonZeldaCharacterData skeleton;
    private ZeldaFourWayMover ownMover;
    // Separate from the base AI's scratch list, which is also used for avoidance
    // and universal threats. Reuse this buffer; do not allocate per perception tick.
    private readonly List<ZeldaFourWayMover> nearbyEnemies = new List<ZeldaFourWayMover>(16);
    private EnergyCrystal Leash => skeleton != null && skeleton.CanReassemble &&
        ownMover != null && !ownMover.enabled ? skeleton.BoundCrystal : null;
    protected override void Awake()
    {
        skeleton = GetComponent<SkeletonZeldaCharacterData>();
        ownMover = GetComponent<ZeldaFourWayMover>();
        base.Awake();
    }
    protected override Vector2 RecoveryHomePosition => Leash != null ? Leash.ClampPosition(InitialScenePosition,.6f) : base.RecoveryHomePosition;
    protected override Vector2 ConstrainAiDestination(Vector2 point) => Leash != null ? Leash.ClampPosition(point,.1f) : point;
    protected override bool IsAiDestinationAllowed(Vector2 point)
    {
        var leash=Leash;if(leash==null || leash.Contains(point))return true;
        // A possessed body can be left outside the rectangle; let it return without teleporting.
        return !leash.Contains(AiPosition) && leash.DistanceToAreaSquared(point) < leash.DistanceToAreaSquared(AiPosition);
    }
    protected override Vector2 ConstrainAiDisplacement(Vector2 displacement)
    {
        var leash=Leash;
        return leash != null ? leash.ConstrainDisplacement(AiPosition, displacement) : displacement;
    }
    private bool MustReturnToCrystal()
    {
        var leash=Leash;if(leash==null)return false;
        return !leash.Contains(AiPosition) || (CurrentTarget!=null && !leash.Contains(CurrentTarget.transform.position)) ||
            (HasDecoyBombTarget && !leash.Contains(DecoyBombTarget.transform.position));
    }
    private void ReturnToCrystal()
    {
        ownMover.CancelAttackForStun();BeginRecovery();
    }
    public override bool SharesHostilityWithOtherNpcs => false;
    public override bool IsUniversalThreat => isActiveAndEnabled &&
        GetComponent<ZeldaCharacterData>() != null && !GetComponent<ZeldaCharacterData>().IsDead &&
        !GetComponent<ZeldaFourWayMover>().isActiveAndEnabled;
    protected override bool EnforcesPermissionAreas => false;
    protected override bool CanSeeCharacter(ZeldaFourWayMover candidate) => IsVisibleEnemy(candidate, Leash);

    private bool IsVisibleEnemy(ZeldaFourWayMover candidate, EnergyCrystal leash)
    {
        // Distance streaming leaves movers registered. Reject inactive objects
        // before component/transform/crystal work; NPC mover.enabled is normally
        // false and must NOT be used as an activity filter.
        if (candidate == null || candidate == ownMover || !candidate.gameObject.activeInHierarchy)
            return false;
        var data = candidate.CharacterData;
        if (data == null || data is SkeletonZeldaCharacterData || data.IsDead || !data.CanBeDetectedByAi)
            return false;
        return (leash == null || leash.Contains(candidate.transform.position)) &&
            base.CanSeeCharacter(candidate);
    }

    public void ResumeAfterReassembly()
    {
        if (!isActiveAndEnabled || GetComponent<ZeldaCharacterData>().IsDead ||
            GetComponent<ZeldaFourWayMover>().isActiveAndEnabled) return;
        // Awake's initialScenePosition remains the home across every resurrection.
        // Scan immediately: a visible enemy takes precedence over returning home.
        BeginRecovery();
        UpdateTargetAndState(0f);
    }

    protected override void UpdateTargetAndState(float deltaTime)
    {
        if (IsEchoRecoveryFrame) return;
        if (MustReturnToCrystal()) { ReturnToCrystal(); return; }
        // Retain an existing visible enemy rather than switching targets every frame.
        var leash = Leash;
        var target = IsVisibleEnemy(CurrentTarget, leash) ? CurrentTarget : null;
        if (target == null)
        {
            float nearest = float.MaxValue;
            QueryNearbyVisionCandidates(nearbyEnemies);
            foreach (var candidate in nearbyEnemies)
            {
                if (candidate == null || !candidate.gameObject.activeInHierarchy) continue;
                float distance = ((Vector2)candidate.transform.position - AiPosition).sqrMagnitude;
                if (distance >= nearest || !IsVisibleEnemy(candidate, leash)) continue;
                nearest = distance;
                target = candidate;
            }
        }
        if (target != null)
        {
            BeginTemporaryHostility(target, false);
            return;
        }
        // Use normal hostile memory/search instead of immediately forgetting an enemy.
        if (CurrentTarget != null && CurrentTarget.CharacterData is SkeletonZeldaCharacterData)
            ResetPermissionResponse();
        base.UpdateTargetAndState(deltaTime);
        if (CurrentTarget != null && CurrentTarget.CharacterData is SkeletonZeldaCharacterData)
            ResetPermissionResponse();
    }

    protected override bool CanSeePlayer(ZeldaFourWayMover candidate) => CanSeeCharacter(candidate);
    protected override void TickHostile(float deltaTime)
    {
        if(MustReturnToCrystal()) { ReturnToCrystal();return; }
        base.TickHostile(deltaTime);
    }
    public override void OnCharacterDamagedBy(ZeldaCharacterData attacker)
    {
        if (attacker is SkeletonZeldaCharacterData) return;
        if (Leash != null && attacker != null && !Leash.Contains(attacker.transform.position))
        { ReturnToCrystal();return; }
        base.OnCharacterDamagedBy(attacker);
    }
    public override bool CanReceiveRoyalCommand(ZeldaFourWayMover player) => false;
    public override void ReceiveRoyalCommand() { }
}
