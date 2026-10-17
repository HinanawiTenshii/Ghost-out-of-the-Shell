using UnityEngine;

public sealed partial class SquadPatrolCharacterAi
{
    internal KnightCaptainAmbush Ambush { get; set; }
    internal bool HasAmbushOrders => Ambush != null && Ambush.HasOrders(this);
    float postUnseenTime;
    protected override bool UsesSharedPlayerTimer => !HasAmbushOrders && base.UsesSharedPlayerTimer;
    public override bool SharesHostilityWithOtherNpcs => !HasAmbushOrders && base.SharesHostilityWithOtherNpcs;
    protected override bool RespondsToDecoyBombs => !HasAmbushOrders && base.RespondsToDecoyBombs;
    public override bool IsInPossessableState => base.IsInPossessableState &&
        (!HasAmbushOrders || !Ambush.NeedsRetention(this));
    protected override Vector2 RecoveryHomePosition => HasAmbushOrders ? Ambush.PostPosition(this) : base.RecoveryHomePosition;

    protected override void UpdateTargetAndState(float deltaTime)
    {
        if (!HasAmbushOrders) { base.UpdateTargetAndState(deltaTime); return; }
        if (Ambush.NeedsRetention(this))
        {
            // Retreat overrides combat, not death/stun/player possession. Base Update owns those checks.
            ResetForAssignedPost();
            postUnseenTime = 0;
            return;
        }
        base.UpdateTargetAndState(deltaTime);
        Vector2 post = Ambush.PostPosition(this);
        bool visibleThreat = CurrentTarget != null && CanSeeCharacter(CurrentTarget);
        postUnseenTime = visibleThreat ? 0 : postUnseenTime + deltaTime;
        float leash = Ambush.IsCaptain(this) ? 2f : Ambush.guardLeash;
        bool outsideLeash = ((Vector2)transform.position - post).sqrMagnitude > leash * leash ||
            (CurrentTarget != null && ((Vector2)CurrentTarget.transform.position - post).sqrMagnitude > leash * leash);
        // Guards return to their own entrance, while an isolated captain can calm down
        // even if the rest of the city is still participating in a shared incident.
        if (outsideLeash || (!visibleThreat && postUnseenTime >= Ambush.calmDelay) ||
            CurrentState == ZeldaAiState.Recovery)
            ResetForAssignedPost();
    }

    void TickAmbushPost()
    {
        Vector2 destination = Ambush.NextDestination(this);
        float gap = Vector2.Distance(AiPosition, destination);
        AiMoveDirection = gap > .22f ? PlanAiPathTo(destination) : Vector2.zero;
        if (gap <= .22f) SetFacingDirection(Ambush.PostFacing(this));
    }
}
