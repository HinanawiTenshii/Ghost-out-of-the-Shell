using System;
using System.Collections.Generic;

public sealed partial class QuestJournalManager
{
    [Serializable] public sealed class SavedDialogueActor
    {
        public string dialogueId;
        public string characterId;
    }

    // Use the body's existing saved IndividualAccessId, not an instance ID,
    // profession, display name, or a scene object reference. The journal already
    // follows persistent travel, save slots, quick restart and new-game reset.
    private readonly Dictionary<string, string> lastDialogueActors =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public bool HasRememberedDialogueActor(string dialogueId)
        => !string.IsNullOrWhiteSpace(dialogueId) && lastDialogueActors.ContainsKey(dialogueId);

    public bool HasDifferentDialogueActor(string dialogueId, string characterId)
    {
        return !string.IsNullOrWhiteSpace(dialogueId) && !string.IsNullOrWhiteSpace(characterId) &&
            lastDialogueActors.TryGetValue(dialogueId, out string previous) && previous != characterId;
    }

    public void RememberDialogueActor(string dialogueId, string characterId)
    {
        if (string.IsNullOrWhiteSpace(dialogueId) || string.IsNullOrWhiteSpace(characterId)) return;
        lastDialogueActors[dialogueId] = characterId;
        // Intentionally no JournalChanged/quest notification: this is hidden
        // conversation memory, not a quest entry or a new tracking selection.
    }

    private void RestoreDialogueActors(List<SavedDialogueActor> saved)
    {
        lastDialogueActors.Clear();
        // Legacy saves do not know who previously talked: start without a false
        // "different person" greeting, then remember the first new conversation.
        if (saved == null) return;
        foreach (var actor in saved)
            if (actor != null) RememberDialogueActor(actor.dialogueId, actor.characterId);
    }
}
