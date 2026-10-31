using UnityEngine;

public enum DirectionalDamageKind { None, Melee, Projectile, Explosion }
public enum CharacterDamageResult { Ignored, Damaged, HolyShieldBlocked, DirectionalShieldBlocked }

/// <summary>Impact geometry, deliberately separate from attacker identity/evidence.
/// Default is unblockable: ambient damage and legacy scripts never invent a direction.</summary>
public readonly struct DirectionalDamageContext
{
    public DirectionalDamageKind Kind { get; }
    // World origin for melee/explosions; travel direction for projectiles.
    public Vector2 Vector { get; }
    private DirectionalDamageContext(DirectionalDamageKind kind, Vector2 vector)
    { Kind = kind; Vector = vector; }

    public static DirectionalDamageContext Melee(Vector2 origin)
        => new DirectionalDamageContext(DirectionalDamageKind.Melee, origin);
    public static DirectionalDamageContext Projectile(Vector2 travelDirection)
        => new DirectionalDamageContext(DirectionalDamageKind.Projectile, travelDirection);
    public static DirectionalDamageContext Explosion(Vector2 center)
        => new DirectionalDamageContext(DirectionalDamageKind.Explosion, center);

    public bool TryGetSourceDirection(Vector2 target, out Vector2 direction)
    {
        direction = Kind == DirectionalDamageKind.Projectile ? -Vector : Vector - target;
        float lengthSquared = direction.sqrMagnitude;
        if (Kind == DirectionalDamageKind.None || float.IsNaN(lengthSquared) ||
            float.IsInfinity(lengthSquared) || lengthSquared < .000001f) return false;
        direction /= Mathf.Sqrt(lengthSquared);
        return true;
    }
}
