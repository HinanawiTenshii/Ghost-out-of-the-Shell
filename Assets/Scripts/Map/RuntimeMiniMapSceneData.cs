using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct RuntimeMiniMapPathData
{
    public Vector2[] points;
}

[Serializable]
public struct RuntimeMiniMapTransitionData
{
    public Vector2 position;
    public string targetSceneName;
    public bool preservesSceneState;
}

[Serializable]
public struct RuntimeMiniMapQuestTargetData
{
    public string questId;
    public Vector2 position;
}

/// <summary>
/// Editor-generated Blocks and transition geometry for an unloaded scene.
/// This ScriptableObject intentionally lives in a matching file so Unity can
/// serialize a stable MonoScript reference into Player builds.
/// </summary>
public sealed class RuntimeMiniMapSceneData : ScriptableObject
{
    private const int CurrentSchemaVersion = 3;

    [SerializeField] private int schemaVersion;
    [SerializeField] private string sceneName;
    [SerializeField] private string displayName;
    [SerializeField] private RuntimeMiniMapPathData[] blockPaths;
    [SerializeField] private RuntimeMiniMapTransitionData[] transitions;
    [SerializeField] private RuntimeMiniMapQuestTargetData[] questTargets;

    public string SceneName => sceneName;
    public bool HasExplicitDisplayName =>
        !string.IsNullOrWhiteSpace(displayName);
    public string DisplayName => string.IsNullOrWhiteSpace(displayName)
        ? sceneName
        : displayName.Trim();
    public RuntimeMiniMapPathData[] BlockPaths => blockPaths;
    public RuntimeMiniMapTransitionData[] Transitions => transitions;
    public RuntimeMiniMapQuestTargetData[] QuestTargets => questTargets;
    public bool UsesCurrentSchema => schemaVersion >= CurrentSchemaVersion;

    public void Configure(
        string configuredSceneName,
        string configuredDisplayName,
        IList<Vector2[]> configuredPaths,
        IList<RuntimeMiniMapTransitionData> configuredTransitions,
        IList<RuntimeMiniMapQuestTargetData> configuredQuestTargets)
    {
        schemaVersion = CurrentSchemaVersion;
        sceneName = configuredSceneName;
        displayName = string.IsNullOrWhiteSpace(configuredDisplayName)
            ? configuredSceneName
            : configuredDisplayName.Trim();
        blockPaths = new RuntimeMiniMapPathData[configuredPaths.Count];
        for (int index = 0; index < configuredPaths.Count; index++)
        {
            Vector2[] source = configuredPaths[index];
            Vector2[] copy = source != null
                ? (Vector2[])source.Clone()
                : new Vector2[0];
            blockPaths[index] = new RuntimeMiniMapPathData { points = copy };
        }

        transitions = new RuntimeMiniMapTransitionData[
            configuredTransitions.Count];
        for (int index = 0; index < configuredTransitions.Count; index++)
        {
            transitions[index] = configuredTransitions[index];
        }

        questTargets = new RuntimeMiniMapQuestTargetData[
            configuredQuestTargets != null ? configuredQuestTargets.Count : 0];
        for (int index = 0; index < questTargets.Length; index++)
        {
            questTargets[index] = configuredQuestTargets[index];
        }
    }
}
