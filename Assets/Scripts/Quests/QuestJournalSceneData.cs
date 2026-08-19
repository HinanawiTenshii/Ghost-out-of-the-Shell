using UnityEngine;

/// <summary>
/// Scene-authored initial journal entries and inspector-callable journal actions.
/// Add this component to a scene object when that scene has journal content.
/// </summary>
public sealed class QuestJournalSceneData : MonoBehaviour
{
    [SerializeField] private QuestJournalEntryDefinition[] initialEntries;

    public int EntryCount => initialEntries != null ? initialEntries.Length : 0;

    public void AddOrUpdateEntry(int entryIndex)
    {
        if (!TryGetDefinition(entryIndex, out QuestJournalEntryDefinition definition))
        {
            return;
        }

        QuestJournalManager.GetOrCreate().AddOrUpdateEntry(
            BuildStableId(definition, entryIndex),
            definition.title,
            definition.details,
            gameObject.scene.name);
    }

    public void RemoveEntry(int entryIndex)
    {
        if (!TryGetDefinition(entryIndex, out QuestJournalEntryDefinition definition))
        {
            return;
        }

        QuestJournalManager.GetOrCreate().RemoveEntry(
            BuildStableId(definition, entryIndex));
    }

    public void RemoveEntryById(string entryId)
    {
        QuestJournalManager.GetOrCreate().RemoveEntry(entryId);
    }

    internal bool LoadInitialEntriesWithoutNotification(
        QuestJournalManager manager,
        string sceneName)
    {
        if (manager == null || initialEntries == null)
        {
            return false;
        }

        bool changed = false;
        for (int index = 0; index < initialEntries.Length; index++)
        {
            QuestJournalEntryDefinition definition = initialEntries[index];
            if (definition.addOnlyWhenTriggered)
            {
                continue;
            }

            changed |= manager.AddOrUpdateWithoutNotification(
                BuildStableId(definition, index),
                definition.title,
                definition.details,
                sceneName);
        }

        return changed;
    }

    private bool TryGetDefinition(
        int entryIndex,
        out QuestJournalEntryDefinition definition)
    {
        if (initialEntries == null ||
            entryIndex < 0 ||
            entryIndex >= initialEntries.Length)
        {
            definition = default;
            return false;
        }

        definition = initialEntries[entryIndex];
        return true;
    }

    private string BuildStableId(
        QuestJournalEntryDefinition definition,
        int entryIndex)
    {
        if (!string.IsNullOrWhiteSpace(definition.entryId))
        {
            return definition.entryId.Trim();
        }

        return gameObject.scene.name + ":" + gameObject.name + ":" + entryIndex;
    }
}
