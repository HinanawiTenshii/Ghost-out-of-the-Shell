using System;
using UnityEngine;

/// <summary>One asset per specific character, like a key's persistent pickup identity.</summary>
[CreateAssetMenu(fileName = "CharacterIdentity", menuName = "Cogitans & Extensa/Character Persistent Identity")]
public sealed class CharacterPersistentIdentity : ScriptableObject
{
    [SerializeField] private string displayName = "角色";
    [SerializeField, HideInInspector] private string stableId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName.Trim();
    public string StableId { get { EnsureId(); return stableId; } }
    private void OnEnable() => EnsureId();
    private void OnValidate() => EnsureId();
    private void EnsureId()
    {
#if UNITY_EDITOR
        // Duplicating an identity asset must create a new identity, not copy the old ID.
        string path = UnityEditor.AssetDatabase.GetAssetPath(this);
        string assetId = string.IsNullOrEmpty(path) ? null : UnityEditor.AssetDatabase.AssetPathToGUID(path);
        if (!string.IsNullOrEmpty(assetId))
        {
            if (stableId != assetId) { stableId = assetId; UnityEditor.EditorUtility.SetDirty(this); }
            return;
        }
#endif
        if (!string.IsNullOrWhiteSpace(stableId)) return;
        stableId = Guid.NewGuid().ToString("N");
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }
}
