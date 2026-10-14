using UnityEngine;

/// <summary>Allows a runtime-summoned NPC to be recreated by the existing scene save system.</summary>
[DisallowMultipleComponent]
public sealed class SummonedCharacterOrigin : MonoBehaviour
{
    public string PrefabName { get; private set; }
    public void Initialize(string prefabName) => PrefabName = prefabName;
}
