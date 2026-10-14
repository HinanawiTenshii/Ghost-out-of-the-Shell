using System.Collections.Generic;
using UnityEngine;

/// <summary>Body-owned lifetime, independent of turret AI/control or distance activation.</summary>
[DisallowMultipleComponent]
public sealed class BuilderTurretBond : MonoBehaviour
{
    private static readonly HashSet<BuilderTurretBond> bonds = new HashSet<BuilderTurretBond>();
    private static readonly HashSet<string> owners = new HashSet<string>();
    public string OwnerId { get; private set; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        bonds.Clear(); owners.Clear();
        ZeldaCharacterData.AnyCharacterDied -= OwnerDied;
        ZeldaCharacterData.AnyCharacterDied += OwnerDied;
    }
    public static void TrackOwner(string id) { if (!string.IsNullOrEmpty(id)) owners.Add(id); }
    public static void Restore(GameObject turret, string id)
    {
        if (turret == null) return;
        var bond = turret.GetComponent<BuilderTurretBond>();
        if (string.IsNullOrEmpty(id))
        {
            if (bond != null) { bond.OwnerId = null; bonds.Remove(bond); }
            return;
        }
        if (bond == null) bond = turret.AddComponent<BuilderTurretBond>();
        bond.OwnerId = id; bonds.Add(bond); TrackOwner(id);
    }
    private static void OwnerDied(ZeldaCharacterData owner)
    {
        if (owner == null || !owners.Remove(owner.IndividualAccessId)) return;
        string id = owner.IndividualAccessId;
        SceneTravelStateManager.Instance?.RemoveBuilderTurretsForOwner(id);
        // Death may cascade (a possessed turret can itself be a builder). Snapshot
        // once per death, not per frame; never destroy on scene unload/streaming.
        foreach (var bond in new List<BuilderTurretBond>(bonds))
            if (bond != null && bond.OwnerId == id)
                bond.GetComponent<ZeldaCharacterData>()?.KillFromPotion();
    }
    private void OnDestroy() { bonds.Remove(this); }
}
