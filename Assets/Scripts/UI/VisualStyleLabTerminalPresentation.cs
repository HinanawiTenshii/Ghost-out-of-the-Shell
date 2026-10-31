using System.Collections.Generic;
using UnityEngine;

/// <summary>Scene-local experiment. Never changes the production palette or UI prefabs.</summary>
[DefaultExecutionOrder(-32000), DisallowMultipleComponent]
public sealed class VisualStyleLabTerminalPresentation : MonoBehaviour
{
    private readonly List<VisualStyleLabTerminalScope> scopes = new List<VisualStyleLabTerminalScope>();

    private void OnEnable()
    {
        if (!Application.isPlaying) return;
        CRTScreenEffect.CanvasRegistered += Register;
        // Once on entry, also cover persistent HUDs created by a previous scene.
        foreach (var canvas in FindObjectsOfType<Canvas>(true)) Register(canvas);
        StartCoroutine(RegisterAfterSceneHandoff());
    }

    private System.Collections.IEnumerator RegisterAfterSceneHandoff()
    {
        // Old scene scopes are destroyed at end-of-frame; persistent HUDs must be reacquired afterwards.
        yield return null;
        if (!isActiveAndEnabled) yield break;
        foreach (var canvas in FindObjectsOfType<Canvas>(true)) Register(canvas);
    }

    private void Register(Canvas canvas)
    {
        if (canvas == null || canvas.renderMode == RenderMode.WorldSpace) return;
        var existing = canvas.GetComponent<VisualStyleLabTerminalScope>();
        if (existing != null) { existing.CaptureGlowScope(); return; }
        // Whitelist gameplay UI; never theme load masks, silhouette highlights or title screens.
        string n = canvas.name;
        if (canvas.GetComponent<ZeldaHealthHeartsUI>() == null &&
            canvas.GetComponent<PauseMenuController>() == null &&
            n != "Item Description Canvas" && n != "Character Dialogue Window" &&
            !n.EndsWith(" Reading Window") && !n.EndsWith(" Character Attribute Screen Overlay") &&
            !n.EndsWith(" Requirement Screen Overlay")) return;
        var scope = canvas.gameObject.AddComponent<VisualStyleLabTerminalScope>();
        scopes.Add(scope);
    }

    private void OnDisable()
    {
        CRTScreenEffect.CanvasRegistered -= Register;
        foreach (var scope in scopes)
        {
            if (scope == null) continue;
            scope.Restore();
            Destroy(scope);
        }
        scopes.Clear();
    }
}
