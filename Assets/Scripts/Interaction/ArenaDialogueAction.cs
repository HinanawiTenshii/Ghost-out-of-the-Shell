using UnityEngine;

/// <summary>Editable registration, withdrawal and reward choices.</summary>
public sealed class ArenaDialogueAction : DialogueOptionEffect
{
    public enum Operation { Register, Abandon, Claim }
    public ArenaChallengeController arena;
    public Operation operation;
    public override bool CanSelect(CharacterDialogue speaker, ZeldaFourWayMover player, out string reason)
    {
        reason = null;
        if (arena == null) { reason = "竞技场尚未绑定。"; return false; }
        if (operation == Operation.Register) return arena.CanRegister(player, out reason);
        if (operation == Operation.Claim) return arena.CanClaim(player, out reason);
        return arena.CurrentPhase == ArenaChallengeController.Phase.Preparing;
    }
    public override bool TryExecute(CharacterDialogue speaker, ZeldaFourWayMover player)
    {
        if (!CanSelect(speaker, player, out _)) return false;
        if (operation == Operation.Register) return arena.Register(player);
        if (operation == Operation.Abandon) return arena.Abandon();
        return arena.Claim(player);
    }
}
