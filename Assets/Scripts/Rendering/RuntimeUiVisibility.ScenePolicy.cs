using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed partial class RuntimeUiVisibility
{
    // Derive this from the active scene, never from serialized progress. Older
    // saves, direct editor starts and both transition components share the policy.
    public static bool IsPauseOnlyScene => Application.isPlaying &&
        string.Equals(SceneManager.GetActiveScene().name, "LastScene", StringComparison.OrdinalIgnoreCase);

    private struct SceneGroupState
    {
        public float alpha;
        public bool interactable;
        public bool blocksRaycasts;
    }

    private readonly Dictionary<CanvasGroup, SceneGroupState> sceneHiddenGroups =
        new Dictionary<CanvasGroup, SceneGroupState>();

    private void ActiveSceneChanged(Scene previous, Scene current)
    {
        RefreshScenePolicy();
    }

    private void RefreshScenePolicy()
    {
        // Undo the temporary F12 mask before saving the scene-policy baseline.
        RestoreGroups();
        if (!IsPauseOnlyScene)
        {
            RestoreSceneGroups();
            return;
        }

        foreach (var canvas in canvases)
            if (canvas != null) ApplySceneCanvasPolicy(canvas);
    }

    private void ApplySceneCanvasPolicy(Canvas canvas)
    {
        // SaveSlotPanel is a child of PauseMenuController. Do not turn off the
        // Canvas GameObject: the persistent HUD also supplies fonts and data.
        if (canvas.GetComponentInParent<PauseMenuController>(true) != null ||
            canvas.GetComponentInParent<RetroSceneLoadReveal>(true) != null)
            return;

        var group = canvas.GetComponent<CanvasGroup>();
        if (group == null) group = canvas.gameObject.AddComponent<CanvasGroup>();
        if (!sceneHiddenGroups.ContainsKey(group))
            sceneHiddenGroups.Add(group, new SceneGroupState
            {
                alpha = group.alpha,
                interactable = group.interactable,
                blocksRaycasts = group.blocksRaycasts
            });
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
    }

    private void RestoreSceneGroups()
    {
        foreach (var entry in sceneHiddenGroups)
        {
            if (entry.Key == null) continue;
            entry.Key.alpha = entry.Value.alpha;
            entry.Key.interactable = entry.Value.interactable;
            entry.Key.blocksRaycasts = entry.Value.blocksRaycasts;
        }
        sceneHiddenGroups.Clear();
    }
}
