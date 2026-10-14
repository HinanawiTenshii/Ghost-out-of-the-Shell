using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct RuntimeMiniMapPathData
{
    public Vector2[] points;
}

[Serializable]
public struct RuntimeMiniMapBridgeData
{
    public string persistentId;
    public RuntimeMiniMapPathData[] originalPaths;
    public RuntimeMiniMapPathData[] rotatedPaths;
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

[Serializable]
public struct RuntimeMiniMapSurfaceData
{
    public Vector2[] points;
    public MiniMapSurfaceGroup.SurfaceKind kind;
    public string visibilityId;
    public bool initiallyVisible;
}

/// <summary>Editor-generated terrain, wall and marker geometry for an unloaded scene.</summary>
public sealed class RuntimeMiniMapSceneData : ScriptableObject
{
    // Version 6 also captures Visible Non Blocking walls in unloaded-scene maps.
    private const int CurrentSchemaVersion = 6;

    [SerializeField] private int schemaVersion;
    [SerializeField] private string sourceRevision;
    public string SourceRevision => sourceRevision;
#if UNITY_EDITOR
    public void SetSourceRevision(string value) { sourceRevision = value; }
#endif
    [SerializeField] private string sceneName;
    [SerializeField] private string displayName;
    [SerializeField] private RuntimeMiniMapPathData[] blockPaths;
    [SerializeField] private RuntimeMiniMapTransitionData[] transitions;
    [SerializeField] private RuntimeMiniMapQuestTargetData[] questTargets;
    [SerializeField] private RuntimeMiniMapBridgeData[] bridges;
    [SerializeField] private RuntimeMiniMapSurfaceData[] surfaces;

    public string SceneName => sceneName;
    public bool HasExplicitDisplayName =>
        !string.IsNullOrWhiteSpace(displayName);
    public string DisplayName => string.IsNullOrWhiteSpace(displayName)
        ? sceneName
        : displayName.Trim();
    public RuntimeMiniMapPathData[] BlockPaths => blockPaths;
    public RuntimeMiniMapTransitionData[] Transitions => transitions;
    public RuntimeMiniMapQuestTargetData[] QuestTargets => questTargets;
    public RuntimeMiniMapBridgeData[] Bridges => bridges;
    public RuntimeMiniMapSurfaceData[] Surfaces => surfaces;
    public bool UsesCurrentSchema => schemaVersion >= CurrentSchemaVersion;

    public void Configure(
        string configuredSceneName,
        string configuredDisplayName,
        IList<Vector2[]> configuredPaths,
        IList<RuntimeMiniMapTransitionData> configuredTransitions,
        IList<RuntimeMiniMapQuestTargetData> configuredQuestTargets,
        IList<RuntimeMiniMapBridgeData> configuredBridges = null,
        IList<RuntimeMiniMapSurfaceData> configuredSurfaces = null)
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
        bridges = configuredBridges != null
            ? new List<RuntimeMiniMapBridgeData>(configuredBridges).ToArray()
            : new RuntimeMiniMapBridgeData[0];
        surfaces = configuredSurfaces != null
            ? new List<RuntimeMiniMapSurfaceData>(configuredSurfaces).ToArray()
            : new RuntimeMiniMapSurfaceData[0];
    }

    public static Vector2[] CreateBoxPath(Matrix4x4 matrix, Vector2 center, Vector2 size)
    {
        Vector2 half = size * 0.5f;
        Vector2[] points = new Vector2[5];
        points[0] = matrix.MultiplyPoint3x4(center + new Vector2(-half.x, -half.y));
        points[1] = matrix.MultiplyPoint3x4(center + new Vector2(-half.x, half.y));
        points[2] = matrix.MultiplyPoint3x4(center + new Vector2(half.x, half.y));
        points[3] = matrix.MultiplyPoint3x4(center + new Vector2(half.x, -half.y));
        points[4] = points[0];
        return points;
    }
}
