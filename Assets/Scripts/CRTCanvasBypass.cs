using UnityEngine;

/// <summary>
/// Keeps a critical canvas as a true screen overlay after the CRT camera
/// composite, so later camera layers cannot cover it.
/// </summary>
[DisallowMultipleComponent]
public sealed class CRTCanvasBypass : MonoBehaviour
{
}
