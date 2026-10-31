using System;
using UnityEngine;

/// <summary>A tiny preloadable UI resource; no scene geometry or gameplay prefab dependencies.</summary>
public sealed class TerminalSceneLoadStyle : ScriptableObject
{
    [Serializable] public struct Area { public string scene, label; }
    public Font font;
    public AudioClip beginSound, scanSound, readySound;
    [Range(0f, 1f)] public float volume = .25f;
    public Area[] areas;

    public string AreaName(string scene)
    {
        if (areas != null)
            foreach (var area in areas)
                if (string.Equals(scene, area.scene, StringComparison.OrdinalIgnoreCase)) return area.label;
        // Titles and unknown/test scenes do not leak internal scene filenames.
        return string.Empty;
    }
}
