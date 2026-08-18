using UnityEngine;

/// <summary>
/// Runtime identity assigned from the scene's original named hierarchy.
/// Unlike sibling indices, this value remains unchanged when another object
/// is collected, destroyed, streamed out or moved to DontDestroyOnLoad.
/// </summary>
[DisallowMultipleComponent]
public sealed class SceneTravelStableId : MonoBehaviour
{
    [SerializeField, HideInInspector] private string stableId;

    public string StableId => stableId;

    public void Initialize(string value)
    {
        if (string.IsNullOrEmpty(stableId) && !string.IsNullOrEmpty(value))
        {
            stableId = value;
        }
    }
}
