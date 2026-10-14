using UnityEngine;

/// <summary>Extend for inventory costs, quests or scene operations; the actual speaker/player are supplied.</summary>
public abstract class DialogueOptionEffect : MonoBehaviour
{
    /// <summary>Optional hidden choice; unlike CanSelect, false removes the option entirely.</summary>
    public virtual bool IsVisible(CharacterDialogue speaker, ZeldaFourWayMover player) => true;

    /// <summary>Evaluated when presenting/selecting an option, never polled every frame.</summary>
    public virtual string GetChoiceText(CharacterDialogue speaker, ZeldaFourWayMover player, string authoredText)
    {
        return authoredText;
    }

    public virtual bool CanSelect(CharacterDialogue speaker, ZeldaFourWayMover player, out string unavailableReason)
    {
        unavailableReason = null;
        return true;
    }

    /// <returns>False keeps the current node (for example if an inventory transaction failed).</returns>
    public abstract bool TryExecute(CharacterDialogue speaker, ZeldaFourWayMover player);
}
