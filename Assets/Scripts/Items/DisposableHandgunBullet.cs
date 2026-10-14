using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>A swept, first-contact-only projectile, independent of the shooter's later state.</summary>
[RequireComponent(typeof(SpriteRenderer))]
public sealed class DisposableHandgunBullet : MonoBehaviour
{
    public const int AttackPower = 2;
    public const float Lifetime = 5f;
    private const float Radius = .06f;
    private readonly List<RaycastHit2D> hits = new List<RaycastHit2D>(16);
    private ContactFilter2D filter;
    private ZeldaCharacterData owner;
    private Vector2 firingPosition;
    private Transform ownerTransform;
    private CameraVisionStreamingRegion retention;
    private Vector2 direction;
    private float speed, elapsed, retentionTimer;
    private bool finished;
    private bool anonymousHit = true;
    private static Sprite bulletSprite;

    public static bool Fire(ZeldaCharacterData user, Vector2 facing, float speed)
    {
        return user != null && FireFromMuzzle(user, facing, speed, user.transform.position);
    }

    public static bool FireFromMuzzle(ZeldaCharacterData user, Vector2 facing, float speed, Vector3 muzzle,
        bool playHandgunSound = true, bool anonymousHit = true)
    {
        if (user == null || user.IsDead || user.IsGhostLike || facing.sqrMagnitude < .001f ||
            float.IsNaN(speed) || float.IsInfinity(speed) || speed <= 0f) return false;
        Scene scene = ZeldaRuntimeRegistry.GetGameplayScene(user.gameObject);
        if (!scene.IsValid() || !scene.isLoaded) return false;
        var root = new GameObject("Disposable Hand Cannon Bullet");
        SceneManager.MoveGameObjectToScene(root, scene);
        var bullet = root.AddComponent<DisposableHandgunBullet>();
        bullet.owner = user;
        bullet.anonymousHit = anonymousHit;
        bullet.firingPosition = user.transform.position;
        bullet.ownerTransform = user.transform;
        bullet.direction = facing.normalized;
        bullet.speed = speed;
        // Sweep body-to-muzzle once, so the visible muzzle cannot bypass thin walls.
        root.transform.position = user.transform.position;
        root.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg);
        bullet.filter = new ContactFilter2D { useTriggers = true };
        bullet.filter.SetLayerMask(Physics2D.AllLayers);
        bullet.ConfigureVisual();
        bullet.retention = root.AddComponent<CameraVisionStreamingRegion>();
        bullet.retention.Configure(Vector2.Distance(user.transform.position, muzzle) + speed * .2f + Radius + .5f);
        bullet.retentionTimer = .1f;
        if (playHandgunSound) DisposableHandgunAudio.Play(scene, muzzle);
        bullet.MoveSwept((Vector2)(muzzle - user.transform.position));
        return true;
    }

    private void ConfigureVisual()
    {
        if (bulletSprite == null)
        {
            var texture = new Texture2D(4, 2, TextureFormat.RGBA32, false);
            texture.name = "Hand Cannon Bullet";
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < 2; y++) for (int x = 0; x < 4; x++)
                texture.SetPixel(x, y, x < 2 ? new Color(.95f, .57f, .16f) : new Color(1f, .92f, .65f));
            texture.Apply(false, true);
            bulletSprite = Sprite.Create(texture, new Rect(0, 0, 4, 2), Vector2.one * .5f, 16f);
        }
        var visual = GetComponent<SpriteRenderer>();
        visual.sprite = bulletSprite;
        visual.sortingOrder = 2;
        // Default world layer/material: the camera's normal visibility mask still applies.
    }

    private void RefreshRetention()
    {
        // Wake a short corridor ahead, not the entire 5-second travel range. Refresh
        // immediately before physics queries so streamed-out colliders cannot be skipped.
        retention.Configure(speed * .2f + Radius + .5f);
        retentionTimer = .1f;
    }

    private void FixedUpdate()
    {
        if (finished) return;
        float step = Mathf.Min(Time.fixedDeltaTime, Lifetime - elapsed);
        if (step <= 0f) { Finish(); return; }
        retentionTimer -= step;
        if (retentionTimer <= 0f) RefreshRetention();
        if (!MoveSwept(direction * (speed * step))) return;
        elapsed += step;
        if (elapsed >= Lifetime) Finish();
    }

    private bool MoveSwept(Vector2 displacement)
    {
        Physics2D.SyncTransforms();
        Vector2 start = transform.position;
        hits.Clear();
        float distance = displacement.magnitude;
        // Keep the existing body-origin Fire API's first sweep on its first physics tick.
        if (distance <= .00001f) return true;
        Vector2 travelDirection = displacement / distance;
        Physics2D.CircleCast(start, Radius, travelDirection, filter, hits, distance);
        int nearest = FindFirstContact(hits);
        if (nearest >= 0)
        {
            transform.position += (Vector3)(travelDirection * hits[nearest].distance);
            Hit(hits[nearest].collider);
            return false;
        }
        transform.position += (Vector3)displacement;
        return true;
    }

    private int FindFirstContact(List<RaycastHit2D> contacts)
    {
        int nearest = -1;
        float distance = float.PositiveInfinity;
        for (int i = 0; i < contacts.Count; i++)
        {
            var collider = contacts[i].collider;
            if (!CanHit(collider) || contacts[i].distance >= distance) continue;
            nearest = i;
            distance = contacts[i].distance;
        }
        return nearest;
    }

    private bool CanHit(Collider2D collider)
    {
        if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy ||
            (ownerTransform != null && collider.transform.IsChildOf(ownerTransform)) ||
            ZeldaRuntimeRegistry.GetGameplayScene(collider.gameObject) != gameObject.scene) return false;
        if (FindDamageTarget(collider) != null) return true;
        // Area/interaction triggers are not physical obstacles. Block-layer triggers
        // remain barriers, just like regular solid colliders on other layers.
        int layer = collider.gameObject.layer;
        return !collider.isTrigger || layer == LayerMask.NameToLayer("Blocks") ||
            layer == LayerMask.NameToLayer("Hidden Blocks");
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

    private void Hit(Collider2D collider)
    {
        if (finished || !CanHit(collider)) return;
        // Latch before callbacks: battery/bomb explosions can synchronously cause more hits.
        Finish();
        Component target = FindDamageTarget(collider);
        if (target is ZeldaCharacterData character)
        {
            if (!character.IsDead && character.CanTakeAttackDamage)
            {
                if (anonymousHit) character.TakeHandgunDamage(AttackPower, firingPosition, owner);
                else character.TakeDamage(AttackPower, owner);
            }
        }
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

    private void Finish()
    {
        if (finished) return;
        finished = true;
        if (retention != null) retention.enabled = false;
        GetComponent<SpriteRenderer>().enabled = false;
        Destroy(gameObject);
    }
}
