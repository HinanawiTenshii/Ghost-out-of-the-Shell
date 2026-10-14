using UnityEngine;

public partial class ZeldaCharacterAiBase
{
    private float ArenaVisionRadius
    {
        get { var arena = ArenaChallengeController.ForAi(this); return arena != null &&
            arena.Opponent == characterData ? arena.VisionRadius : 0f; }
    }
    private bool FreezeForArena()
    {
        var arena = ArenaChallengeController.ForAi(this);
        if (arena == null || !arena.FreezesAi(this)) return false;
        moveDirection = Vector2.zero;
        if (rb != null) { rb.velocity = Vector2.zero; rb.angularVelocity = 0; previousFixedPosition = rb.position; }
        return true;
    }
    private bool UpdateArenaTarget()
    {
        var arena = ArenaChallengeController.ForAi(this);
        if (arena == null || !arena.CombatRunning || arena.Opponent != characterData) return false;
        var player = arena.Entrant;
        if (player != null && CanSeePlayer(player))
        {
            if (targetMover != player || currentState != ZeldaAiState.Hostile) BeginTemporaryHostility(player, false);
            lastKnownTargetPosition = player.transform.position;
        }
        else UpdateTargetAndState(Time.deltaTime);
        return true;
    }
    public void ResetArenaRound()
    {
        if (sharedPlayerTimer != null) sharedPlayerTimer.Leave(this);
        mover.CancelAttackForStun();
        moveDirection = Vector2.zero;
        attackCooldownTimer = 0f;
        regularHostile = hostileUntilTargetDeath = false;
        targetMover = null;
        ClearGridPath();
        InvalidateNavigationPlan();
        previousFixedPosition = rb.position;
        ChangeState(ZeldaAiState.Idle);
        FaceForDialogue(Vector2.left);
    }
}
