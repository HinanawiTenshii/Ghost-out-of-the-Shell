using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>First-contact swept bottle: one impact, one damage application, one investigation event.</summary>
[RequireComponent(typeof(SpriteRenderer))]
public sealed class ThrownGlassBottle : MonoBehaviour
{
    public const int AttackPower = 1;
    public const float Lifetime = 6f;
    private const float Radius = .12f;
    private readonly List<RaycastHit2D> hits = new List<RaycastHit2D>(16);
    private readonly List<Collider2D> overlaps = new List<Collider2D>(16);
    private ContactFilter2D filter;
    private ZeldaCharacterData owner;
    private Transform ownerTransform, sourceItem;
    private CameraVisionStreamingRegion retention;
    private Vector2 direction;
    private float speed, investigationRadius, elapsed, retentionTimer;
    private bool finished;
    private Sprite flightSprite;
    private Texture2D flightTexture;

    private void OnEnable() => AutoSaveTransientActivity.Register(this);
    private void OnDisable() => AutoSaveTransientActivity.Unregister(this);

    public static bool Throw(ZeldaCharacterData user, Vector2 facing, float speed, float radius, GameObject source)
    {
        if (user == null || user.IsDead || user.IsGhostLike || facing.sqrMagnitude < .001f ||
            float.IsNaN(speed) || float.IsInfinity(speed) || speed <= 0 ||
            float.IsNaN(radius) || float.IsInfinity(radius) || radius < 0) return false;
        Scene scene = ZeldaRuntimeRegistry.GetGameplayScene(user.gameObject);
        if (!scene.IsValid() || !scene.isLoaded) return false;
        var root = new GameObject("Thrown Glass Bottle"); SceneManager.MoveGameObjectToScene(root, scene);
        root.transform.position = user.transform.position;
        var bottle = root.AddComponent<ThrownGlassBottle>();
        bottle.owner = user; bottle.ownerTransform = user.transform; bottle.sourceItem = source != null ? source.transform : null;
        bottle.direction = facing.normalized; bottle.speed = speed; bottle.investigationRadius = radius;
        bottle.filter = new ContactFilter2D { useTriggers = true }; bottle.filter.SetLayerMask(Physics2D.AllLayers);
        bottle.flightSprite = GlassBottlePickupItemVisual.CreateFlightSprite(out bottle.flightTexture);
        var renderer = root.GetComponent<SpriteRenderer>(); renderer.sprite = bottle.flightSprite; renderer.sortingOrder = 2;
        root.transform.localScale = Vector3.one * .42f;
        root.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg - 90);
        bottle.retention = root.AddComponent<CameraVisionStreamingRegion>();
        bottle.RefreshRetention(.45f);
        // The hand-to-front sweep cannot teleport through a nearby thin wall.
        bottle.MoveSwept(bottle.direction * .45f);
        return true;
    }

    private void RefreshRetention(float extra = 0)
    {
        retention.Configure(speed * .2f + Radius + .5f + extra); retentionTimer = .1f;
    }
    private void FixedUpdate()
    {
        if (finished) return;
        float step = Mathf.Min(Time.fixedDeltaTime, Lifetime - elapsed);
        if (step <= 0) { Shatter(null); return; }
        retentionTimer -= step; if (retentionTimer <= 0) RefreshRetention();
        if (!MoveSwept(direction * (speed * step))) return;
        transform.Rotate(0, 0, -420f * step);
        elapsed += step;
        if (elapsed >= Lifetime) Shatter(null);
    }
    private bool MoveSwept(Vector2 displacement)
    {
        if (finished) return false;
        Physics2D.SyncTransforms(); Vector2 start = transform.position;
        overlaps.Clear(); Physics2D.OverlapCircle(start, Radius, filter, overlaps);
        foreach (var collider in overlaps) if (CanHit(collider)) { Shatter(collider); return false; }
        float distance = displacement.magnitude; if (distance <= .00001f) return true;
        hits.Clear(); Physics2D.CircleCast(start, Radius, displacement / distance, filter, hits, distance);
        int nearest = -1; float hitDistance = float.PositiveInfinity;
        for (int i = 0; i < hits.Count; i++)
            if (CanHit(hits[i].collider) && hits[i].distance < hitDistance) { nearest = i; hitDistance = hits[i].distance; }
        if (nearest >= 0)
        {
            transform.position += (Vector3)(displacement.normalized * hitDistance);
            Shatter(hits[nearest].collider); return false;
        }
        transform.position += (Vector3)displacement; return true;
    }
    private bool CanHit(Collider2D collider)
    {
        if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy ||
            (ownerTransform != null && collider.transform.IsChildOf(ownerTransform)) ||
            (sourceItem != null && collider.transform.IsChildOf(sourceItem)) ||
            ZeldaRuntimeRegistry.GetGameplayScene(collider.gameObject) != gameObject.scene) return false;
        if (FindDamageTarget(collider) != null) return true;
        // Permission/interaction zones are not physical surfaces; trigger walls remain solid.
        return !collider.isTrigger || collider.gameObject.layer == LayerMask.NameToLayer("Blocks") ||
            collider.gameObject.layer == LayerMask.NameToLayer("Hidden Blocks");
    }
    private static Component FindDamageTarget(Collider2D collider)
    {
        Component target = collider.GetComponentInParent<ZeldaCharacterData>();
        if (target == null) target = collider.GetComponentInParent<DoorData>();
        if (target == null) target = collider.GetComponentInParent<CardboardBoxPickupItem>();
        if (target == null) target = collider.GetComponentInParent<BatteryPickupItem>();
        if (target == null) target = collider.GetComponentInParent<BatterySocket>();
        if (target == null) target = collider.GetComponentInParent<EnergyCrystal>();
        if (target == null) target = collider.GetComponentInParent<PotionPickupItem>();
        if (target == null) target = collider.GetComponentInParent<BombPickupItem>();
        if (target == null) target = collider.GetComponentInParent<PlacedBomb>();
        if (target == null) target = collider.GetComponentInParent<StabilityCrystalPickupItem>();
        if (target == null) target = collider.GetComponentInParent<DollAttackShake>();
        return target;
    }
    private void Shatter(Collider2D collider)
    {
        if (finished) return;
        finished = true; // Latch before AI/death/explosion callbacks can reenter.
        Vector3 position = transform.position;
        retention.Configure(Mathf.Max(.6f, investigationRadius + .5f));
        GetComponent<SpriteRenderer>().enabled = false;
        GlassBottleShatterVisual.Spawn(gameObject.scene, position, investigationRadius);
        if (investigationRadius > 0)
        {
            float radiusSquared = investigationRadius * investigationRadius;
            var targets = new List<ZeldaCharacterAiBase>(ZeldaRuntimeRegistry.AiCharacters);
            foreach (var ai in targets)
            {
                if (ai == null || !ai.isActiveAndEnabled || ai.transform == ownerTransform ||
                    ZeldaRuntimeRegistry.GetGameplayScene(ai.gameObject) != gameObject.scene ||
                    ((Vector2)ai.transform.position - (Vector2)position).sqrMagnitude > radiusSquared) continue;
                var mover = ai.GetComponent<ZeldaFourWayMover>();
                if (mover != null && mover.isActiveAndEnabled) continue;
                ai.InvestigatePosition(position);
            }
        }
        if (collider != null) ApplyImpact(FindDamageTarget(collider));
        retention.enabled = false;
        Destroy(gameObject);
    }
    private void ApplyImpact(Component target)
    {
        if (target is ZeldaCharacterData character)
        { if (!character.IsDead && character.CanTakeAttackDamage) character.TakeDamage(AttackPower, owner); }
        else if (target is DoorData door) door.ReceiveAttack(AttackPower, owner, true);
        else if (target is CardboardBoxPickupItem box) box.ReceiveAttack(AttackPower, owner);
        else if (target is BatteryPickupItem battery) battery.ReceiveAttack(AttackPower, owner);
        else if (target is BatterySocket socket) socket.ReceiveAttack(AttackPower, owner);
        else if (target is EnergyCrystal energyCrystal) energyCrystal.ReceiveAttack(AttackPower, owner);
        else if (target is PotionPickupItem potion) potion.ReceiveAttack(AttackPower, owner);
        else if (target is BombPickupItem bomb) bomb.DetonateFromAttack(owner);
        else if (target is PlacedBomb placed) placed.DetonateImmediately();
        else if (target is StabilityCrystalPickupItem crystal) crystal.BurstFromAttack();
        else if (target is DollAttackShake doll) doll.TriggerAttackShake();
    }
    private void OnDestroy()
    {
        if (flightSprite != null) Destroy(flightSprite);
        if (flightTexture != null) Destroy(flightTexture);
    }
}
