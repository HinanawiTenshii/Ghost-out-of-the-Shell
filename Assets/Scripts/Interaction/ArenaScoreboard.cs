using UnityEngine;

/// <summary>Reusable world-space scoreboard; no per-frame UI rebuild.</summary>
[ExecuteAlways, DisallowMultipleComponent]
public sealed class ArenaScoreboard : MonoBehaviour
{
    public TextMesh display;
    public string waitingText = "等待挑战者";
    private void OnEnable() { Display(waitingText, ZeldaUiPalette.Primary); }
    public void Display(string message, Color color)
    {
        if (display == null) return;
        display.text = message; display.color = color;
    }
}
