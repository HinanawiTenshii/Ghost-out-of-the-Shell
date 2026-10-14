using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Local physics query shared by exposed and boxed proximity bombs.</summary>
internal static class BombProximitySensor
{
    internal const float CheckInterval = .05f;
    private static readonly List<Collider2D> overlaps = new List<Collider2D>(32);

    internal static bool HasTarget(Vector2 center, Vector2 size, Scene scene,
        ZeldaCharacterData owner, string ownerId)
    {
        // Include character trigger bodies, independent of queriesHitTriggers.
        // Use a reusable list, not a scene scan or a bounded array that can miss
        // characters when scenery/pickups fill the first few overlap results.
        overlaps.Clear();
        Physics2D.OverlapBox(center, size, 0f, new ContactFilter2D { useTriggers = true }, overlaps);
        bool found = false;
        foreach (var collider in overlaps)
        {
            var actor = collider != null ? collider.GetComponentInParent<ZeldaCharacterData>() : null;
            if (actor == null || !actor.gameObject.activeInHierarchy || actor.IsDead || actor.IsGhostLike ||
                ZeldaRuntimeRegistry.GetGameplayScene(actor.gameObject) != scene ||
                actor == owner || (!string.IsNullOrEmpty(ownerId) && actor.IndividualAccessId == ownerId)) continue;
            // Child awareness/interaction triggers are not a character's body.
            if (collider.isTrigger && collider.gameObject != actor.gameObject) continue;
            found = true;
            break;
        }
        overlaps.Clear();
        return found;
    }
}
