using UnityEngine;

/// <summary>Shared strip geometry: particles, damage and ghost collision use the same 0.5-wide area.</summary>
public static class ElectricGateRules
{
    public const float Range = 10f;
    public const float Width = .5f;
    public const int ArcsPerPair = 2;
    public const int ArcCount = ArcsPerPair * 2;
    public const int FirstPairDamage = 2;
    public const int SecondPairDamage = 6;
    public const float PairDelay = .25f;
    public const float ArcLifetime = .18f;
    public enum SignalState { Off, Ready, Passage, Warning }

    public static SignalState ResolveSignal(bool powered, bool allowedNearby, bool deniedNearby)
    {
        if (!powered) return SignalState.Off;
        if (deniedNearby) return SignalState.Warning;
        return allowedNearby ? SignalState.Passage : SignalState.Ready;
    }

    public static SignalState MergeSignal(SignalState a, SignalState b)
    {
        // A pillar may belong to multiple links. Warnings take priority over a
        // neighbouring passage indication; inactive links cannot turn a live lamp off.
        return (SignalState)Mathf.Max((int)a, (int)b);
    }

    public static bool NearerCandidate(float squaredDistance, float best, int candidateId, int bestId)
    {
        if (squaredDistance < .01f || squaredDistance > Range * Range || squaredDistance > best) return false;
        return Mathf.Abs(squaredDistance - best) >= .00001f || candidateId < bestId;
    }

    public static bool IntersectsBox(Vector2 start, Vector2 delta, Vector2 min, Vector2 max)
    {
        float enter = 0, exit = 1;
        return Slab(start.x, delta.x, min.x, max.x, ref enter, ref exit)
            && Slab(start.y, delta.y, min.y, max.y, ref enter, ref exit);
    }

    public static bool Allows(bool individual, int threshold, string enrolled, int permission, string identity)
    {
        return individual ? !string.IsNullOrEmpty(enrolled) && enrolled == identity : permission >= threshold;
    }

    public static Vector2 Normal(Vector2 a, Vector2 b)
    {
        Vector2 direction = (b - a).normalized;
        return new Vector2(-direction.y, direction.x);
    }

    // Swept rectangle test prevents fast movers skipping a narrow field between physics ticks.
    public static bool Sweep(Vector2 start, Vector2 delta, Vector2 a, Vector2 b, float padding, out float enter)
    {
        Vector2 axis = (b - a).normalized, normal = Normal(a, b);
        Vector2 p = start - a;
        float exit = 1f;
        enter = 0f;
        return (b - a).sqrMagnitude > .0001f
            && Slab(Vector2.Dot(p, axis), Vector2.Dot(delta, axis), -padding, Vector2.Distance(a, b) + padding, ref enter, ref exit)
            && Slab(Vector2.Dot(p, normal), Vector2.Dot(delta, normal), -Width * .5f - padding, Width * .5f + padding, ref enter, ref exit);
    }

    private static bool Slab(float p, float d, float min, float max, ref float enter, ref float exit)
    {
        if (Mathf.Abs(d) < .000001f) return p >= min && p <= max;
        float lo = (min - p) / d, hi = (max - p) / d;
        if (lo > hi) { float swap = lo; lo = hi; hi = swap; }
        enter = Mathf.Max(enter, lo); exit = Mathf.Min(exit, hi);
        return enter <= exit;
    }

    public static Vector2 BlockGhost(Vector2 start, Vector2 delta, Vector2 a, Vector2 b, float radius)
    {
        Vector2 normal = Normal(a, b);
        float side = Vector2.Dot(start - a, normal);
        if (Mathf.Abs(side) < .00001f && Sweep(start, Vector2.zero, a, b, radius, out _)) return delta;
        // A newly spawned/possessed ghost already overlapping the strip may leave, but not cross it.
        if (Sweep(start, Vector2.zero, a, b, radius, out _)
            && Mathf.Abs(Vector2.Dot(delta, normal)) < .00001f) return delta;
        if (Sweep(start, Vector2.zero, a, b, radius, out _)
            && Vector2.Dot(delta, normal) * side > 0f) return delta;
        if (!Sweep(start, delta, a, b, radius, out float enter)) return delta;
        return delta * Mathf.Max(0f, enter - .002f / Mathf.Max(.002f, delta.magnitude));
    }
}
