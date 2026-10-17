using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>World-only offering indicator. No map registration, physics, or scene scans.</summary>
[DisallowMultipleComponent, DefaultExecutionOrder(201)]
public sealed class SummoningQuestCharacterMarker : MonoBehaviour
{
    private ZeldaCharacterData actor;
    private QuestJournalManager journal;
    private GameObject visual;

    public static void EnsureFor(ZeldaCharacterData actor)
    {
        if (actor == null || string.IsNullOrEmpty(actor.PersistentCharacterId)) return;
        var marker = actor.GetComponent<SummoningQuestCharacterMarker>();
        if (marker == null) marker = actor.gameObject.AddComponent<SummoningQuestCharacterMarker>();
        marker.actor = actor;
    }
    private void LateUpdate()
    {
        if (actor == null) actor = GetComponent<ZeldaCharacterData>();
        if (journal == null) journal = QuestJournalManager.Instance;
        bool show = actor != null && !actor.IsDead && !actor.IsGhostLike && journal != null &&
            journal.IsTrackedSummoningTarget(actor.PersistentCharacterId);
        if (show)
        {
            if (visual == null)
            {
                visual = QuestJournalInteractionMarker.CreateWorldVisual(gameObject.scene);
                visual.name = "Summoning Offering World Marker";
            }
            if (visual.scene != gameObject.scene) SceneManager.MoveGameObjectToScene(visual, gameObject.scene);
            visual.transform.position = actor.AttributeWindowAnchor.position + Vector3.up * 1.25f;
        }
        if (visual != null && visual.activeSelf != show) visual.SetActive(show);
    }
    private void OnDisable() { if (visual != null) visual.SetActive(false); }
    private void OnDestroy() { if (visual != null) Destroy(visual); }
}
