using UnityEngine;

/// <summary>Explicit map walls whose gameplay layer must stay unchanged.</summary>
[DisallowMultipleComponent]
public sealed class MiniMapWallGroup : MonoBehaviour
{
    [SerializeField] private Collider2D[] walls;
    public Collider2D[] Walls => walls;
}
