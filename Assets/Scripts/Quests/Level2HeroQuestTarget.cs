using UnityEngine;

/// <summary>Explicit scene-authored stage target; included in cached minimaps even when streamed out.</summary>
[DisallowMultipleComponent, AddComponentMenu("Quests/Level2 Hero Quest Target (英雄之证目标)")]
public sealed class Level2HeroQuestTarget : MonoBehaviour
{
    public enum Destination { BorderCity, TrialGrounds }
    [SerializeField, InspectorName("目标阶段")] private Destination destination;
    public bool IsTrialEntrance => destination == Destination.TrialGrounds;
    public string MapTargetId => IsTrialEntrance ? QuestJournalManager.Level2TrialTargetId : QuestJournalManager.Level2CityTargetId;
}
