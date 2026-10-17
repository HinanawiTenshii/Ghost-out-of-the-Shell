using UnityEngine;

[AddComponentMenu("Interaction/Gravekeeper Quest Action (守墓人委托)")]
public sealed class GravekeeperDialogueEffect : DialogueOptionEffect
{
    public enum Operation { Accept, Report }
    [SerializeField] private Operation operation;
    [SerializeField] private DialogueQuestProgression progression;
    [SerializeField, Tooltip("仅绑定埋骨地被污染的镇抚水晶。")]
    private GraveyardCrystalQuestTarget crystalTarget;

    public override bool CanSelect(CharacterDialogue speaker, ZeldaFourWayMover player, out string reason)
    {
        reason = null;
        if (!isActiveAndEnabled || progression == null || !progression.IsConfigured ||
            speaker == null || speaker.gameObject != gameObject || player == null)
        { reason = "暂时无法交谈。"; return false; }
        if (crystalTarget != null) crystalTarget.SyncEvidence();
        if (!progression.CanTrigger || progression.IsCompleted)
        { reason = "这项委托已经结束。"; return false; }
        if (operation == Operation.Accept)
        {
            if (!progression.IsAccepted) return true;
            reason = "已经接下了这项委托。"; return false;
        }
        if (!progression.IsAccepted) { reason = "需要先接下守墓人的委托。"; return false; }
        if (!QuestJournalManager.GetOrCreate().GraveyardCrystalDestroyed)
        { reason = "尚未摧毁被污染的镇抚水晶。"; return false; }
        return true;
    }

    public override bool TryExecute(CharacterDialogue speaker, ZeldaFourWayMover player)
    {
        if (!CanSelect(speaker, player, out _)) return false;
        if (operation == Operation.Accept) return progression.AcceptQuest();
        progression.CompleteQuest();
        if (!progression.IsCompleted) return false;
        QuestJournalManager.GetOrCreate().UnlockChurchRelicPassword();
        return true;
    }
}
