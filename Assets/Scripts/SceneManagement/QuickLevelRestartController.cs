using UnityEngine;

/// <summary>
/// Compatibility component for existing scenes. Holding T to restart was removed.
/// Disk saves use SceneTravelStateManager's restore infrastructure directly;
/// no extra entry checkpoint or persistent controller is needed.
/// </summary>
public sealed class QuickLevelRestartController : MonoBehaviour
{
}
