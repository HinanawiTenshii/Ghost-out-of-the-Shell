using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Instant construction. Persistent ownership lives on the resulting turret.</summary>
public static class BuilderSkillRuntime
{
    private static readonly List<RaycastHit2D> hits = new List<RaycastHit2D>(16);
    private static readonly List<Collider2D> overlaps = new List<Collider2D>(16);
    public static int EnergyCost => PlayerGrowthAttributes.Instance != null &&
        PlayerGrowthAttributes.Instance.HasSkill("builder_2") ? 2 : 3;
    public static GameObject TurretPrefab => Resources.Load<SaveGameCatalog>("SaveGameCatalog")?.FindCharacter(
        typeof(TurretZeldaCharacterData).AssemblyQualifiedName, "Turret-level2");

    public static bool TryUse(ZeldaCharacterData owner)
    {
        var mover = owner != null ? owner.GetComponent<ZeldaFourWayMover>() : null;
        if (owner == null || owner.IsDead || owner.IsGhostLike || mover == null || !mover.isActiveAndEnabled ||
            mover != ZeldaRuntimeRegistry.GetControlledMover() || mover.IsAttacking || Time.timeScale <= 0f ||
            DocumentReader.IsInputBlocked || ClockworkPuppetRuntime.BlocksCharacterInput) return false;
        var growth = PlayerGrowthAttributes.Instance;
        if (owner.NativeCharacterSkillId != "builder" && (growth == null || !growth.HasSkill("builder"))) return false;
        var prefab = TurretPrefab;
        var scene = ZeldaRuntimeRegistry.GetGameplayScene(owner.gameObject);
        if (prefab == null || !scene.IsValid() || !scene.isLoaded || owner.FinalPossessionEnergy < EnergyCost) return false;
        if (!TryPlacement(owner, mover.FacingDirection, prefab, out Vector3 position))
        { ZeldaHealthHeartsUI.Instance?.ShowNotificationPopup("前方空间不足，无法建造"); return false; }
        if (!owner.TrySpendPossessionEnergy(EnergyCost)) return false;
        var root = UnityEngine.Object.Instantiate(prefab, position, Quaternion.identity);
        root.name = "Builder Turret";
        SceneManager.MoveGameObjectToScene(root, scene);
        root.AddComponent<SceneTravelStableId>().Initialize("builder_turret_" + Guid.NewGuid().ToString("N"));
        root.GetComponent<TurretCharacterAi>().BindCharacter(owner);
        root.GetComponent<TurretZeldaCharacterData>().AimAt(position + (Vector3)mover.FacingDirection, 10f);
        BuilderTurretBond.Restore(root, owner.IndividualAccessId);
        CharacterPotionBuffs.NotifyAction(owner);
        mover.TryBeginItemAttack(.24f);
        return true;
    }
    private static bool TryPlacement(ZeldaCharacterData owner, Vector2 facing, GameObject prefab, out Vector3 position)
    {
        Vector2 direction = facing.sqrMagnitude > .001f ? facing.normalized : Vector2.down;
        var collider = prefab.GetComponent<BoxCollider2D>();
        Vector2 size = Vector2.Scale(collider.size, prefab.transform.localScale);
        Vector2 offset = Vector2.Scale(collider.offset, prefab.transform.localScale);
        var ownerCollider = owner.GetComponent<Collider2D>();
        Vector2 extents = ownerCollider != null ? (Vector2)ownerCollider.bounds.extents : Vector2.one * .4f;
        float reach = Mathf.Abs(direction.x) * (extents.x + size.x * .5f) +
            Mathf.Abs(direction.y) * (extents.y + size.y * .5f) + .22f;
        position = owner.transform.position + (Vector3)(direction * reach);
        Physics2D.SyncTransforms();
        var filter = new ContactFilter2D { useTriggers = true };
        filter.SetLayerMask(Physics2D.AllLayers);
        hits.Clear(); overlaps.Clear();
        Physics2D.BoxCast((Vector2)owner.transform.position + offset, size + Vector2.one * .04f,
            0, direction, filter, hits, reach);
        foreach (var hit in hits) if (Blocks(hit.collider, owner)) return false;
        Physics2D.OverlapBox((Vector2)position + offset, size + Vector2.one * .04f, 0, filter, overlaps);
        foreach (var hit in overlaps) if (Blocks(hit, owner)) return false;
        return true;
    }
    private static bool Blocks(Collider2D collider, ZeldaCharacterData owner)
    {
        if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy ||
            collider.transform.IsChildOf(owner.transform) ||
            ZeldaRuntimeRegistry.GetGameplayScene(collider.gameObject) != ZeldaRuntimeRegistry.GetGameplayScene(owner.gameObject)) return false;
        return !collider.isTrigger || collider.gameObject.layer == LayerMask.NameToLayer("Blocks") ||
            collider.gameObject.layer == LayerMask.NameToLayer("Hidden Blocks");
    }
}
