using UnityEngine;

/// <summary>
/// Inspector-configurable journal event for levers, doors, cutscenes and any
/// other scene behaviour. Invoke TriggerJournalChanges from a UnityEvent,
/// animation event, SendMessage or another gameplay script.
/// </summary>
public sealed class QuestJournalEventTrigger : MonoBehaviour
{
    [SerializeField] private bool triggerOnlyOnce = true;
    [SerializeField] private QuestJournalEventChange[] journalChanges;

    private bool hasTriggered;

    public bool HasTriggered => hasTriggered;

    private void Awake()
    {
        QuestJournalInteractionMarker.Configure(
            gameObject,
            false,
            string.Empty,
            journalChanges);
    }

    public void TriggerJournalChanges()
    {
        if (triggerOnlyOnce && hasTriggered)
            return;

        bool changed = QuestJournalManager.GetOrCreate().ApplyEventChanges(
            journalChanges,
            gameObject.scene.name);
        if (changed || journalChanges == null || journalChanges.Length == 0)
        {
            hasTriggered = true;
        }
    }

    public void ResetTriggerState()
    {
        hasTriggered = false;
    }
}
