using System;
using UnityEngine;

/// <summary>
/// Asset-backed identity shared by one placed pickup and objects in other scenes.
/// Create one asset per specific pickup instance that must be referenced across scenes.
/// </summary>
[CreateAssetMenu(
    fileName = "PickupItemIdentity",
    menuName = "Cogitans & Extensa/Pickup Item Persistent Identity")]
public sealed class PickupItemPersistentIdentity : ScriptableObject
{
    [SerializeField] private string displayName = "Key";
    [SerializeField, HideInInspector] private string stableInstanceId;

    public string DisplayName => string.IsNullOrWhiteSpace(displayName)
        ? name
        : displayName.Trim();

    public string StableInstanceId
    {
        get
        {
            EnsureStableInstanceId();
            return stableInstanceId;
        }
    }

    private void OnEnable()
    {
        EnsureStableInstanceId();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        EnsureStableInstanceId();
    }
#endif

    private void EnsureStableInstanceId()
    {
        if (!string.IsNullOrWhiteSpace(stableInstanceId))
        {
            stableInstanceId = stableInstanceId.Trim();
            return;
        }

        stableInstanceId = Guid.NewGuid().ToString("N");
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }
}
