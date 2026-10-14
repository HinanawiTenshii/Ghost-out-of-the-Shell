using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Scene-local meteor sequence. All landing positions are snapshotted once, never homing.</summary>
public sealed class MeteorShowerSkillRuntime : MonoBehaviour
{
    public const int EnergyCost = 5;
    public const float Radius = 4f, Interval = .28f, FallDuration = .8f, FireDuration = 5f;
    private sealed class Strike
    {
        public Vector2 point;
        public float at;
        public bool targeted, impacted;
        public int fireTicks;
        public MeteorShowerVisual visual;
    }
    private readonly List<Strike> strikes = new List<Strike>(15);
    private readonly List<SandwormZeldaCharacterData> buriedTargets = new List<SandwormZeldaCharacterData>(2);
    private ZeldaCharacterData owner;
    private CameraFollowActiveZeldaMover shake;
    private float elapsed;
    private bool fire, cancelled;
    private int impacts;
    private MeteorShowerAudio skillAudio;

    public static bool TryUse(ZeldaCharacterData caster)
    {
        var mover = caster != null ? caster.GetComponent<ZeldaFourWayMover>() : null;
        if (caster == null || caster.IsDead || caster.IsGhostLike || mover == null || !mover.isActiveAndEnabled ||
            mover != ZeldaRuntimeRegistry.GetControlledMover() || mover.IsAttacking || Time.timeScale <= 0f ||
            GameSaveSystem.IsLoading || DocumentReader.IsInputBlocked || ClockworkPuppetRuntime.BlocksCharacterInput) return false;
        var growth = PlayerGrowthAttributes.Instance;
        if (caster.NativeCharacterSkillId != "meteor_shower" && (growth == null || !growth.HasSkill("meteor_shower"))) return false;
        var scene = ZeldaRuntimeRegistry.GetGameplayScene(caster.gameObject);
        if (!scene.IsValid() || !scene.isLoaded || caster.FinalPossessionEnergy < EnergyCost) return false;
        var camera = Camera.main;
        var vision = camera != null ? camera.GetComponent<CameraCircularVision>() : null;
        if (vision != null && !vision.isActiveAndEnabled) vision = null;
        float radius = vision != null ? vision.VisionRadius : caster.GetPlayerVisionRadius(scene.path);
        int blocks = vision != null ? vision.BlockLayers.value : LayerMask.GetMask("Blocks", "Hidden Blocks");
        Vector2 origin = caster.transform.position;
        Physics2D.SyncTransforms();
        Vector2 safeOrigin = new CameraVisionOriginResolver().Resolve(caster.transform, origin, blocks, .03f, 0);
        // Invalid geometry must not spend energy or place an invisible meteor inside a wall.
        if (!Visible(safeOrigin, origin, radius, blocks, vision, null)) return false;
        bool upgraded = growth != null && growth.HasSkill("meteor_shower_2");
        int targetCount = upgraded ? 8 : 6, randomCount = upgraded ? 7 : 6;
        var candidates = new List<ZeldaCharacterData>();
        var movers = new List<ZeldaFourWayMover>();
        ZeldaRuntimeRegistry.QueryNearbyMovers(origin, radius, movers);
        foreach (var candidate in movers)
        {
            if (candidate == null || candidate.isActiveAndEnabled || !candidate.gameObject.activeInHierarchy ||
                ZeldaRuntimeRegistry.GetGameplayScene(candidate.gameObject) != scene) continue;
            var data = candidate.GetComponent<ZeldaCharacterData>();
            if (data == null || data == caster || data.IsDead || data.IsGhostLike || !data.CanTakeAttackDamage ||
                !Visible(data.transform.position, origin, radius, blocks, vision, data.transform)) continue;
            candidates.Add(data);
        }
        // Partial Fisher-Yates: each actor can own only one targeted landing.
        for (int i = 0; i < Mathf.Min(targetCount, candidates.Count); i++)
        {
            int j = Random.Range(i, candidates.Count);
            var swap = candidates[i]; candidates[i] = candidates[j]; candidates[j] = swap;
        }
        if (!caster.TrySpendPossessionEnergy(EnergyCost)) return false;
        var root = new GameObject("Meteor Shower"); SceneManager.MoveGameObjectToScene(root, scene);
        root.transform.position = origin;
        var sequence = root.AddComponent<MeteorShowerSkillRuntime>();
        sequence.owner = caster;
        sequence.fire = growth != null && growth.HasSkill("meteor_shower_3");
        sequence.skillAudio = new MeteorShowerAudio(root, sequence.fire);
        sequence.shake = camera != null ? camera.GetComponent<CameraFollowActiveZeldaMover>() : null;
        // One region/query for the entire shower, rather than scanning the level per meteor.
        root.AddComponent<CameraVisionStreamingRegion>().Configure(radius + Radius);
        for (int i = 0; i < Mathf.Max(targetCount, randomCount); i++)
        {
            if (i < targetCount)
            {
                bool targeted = i < candidates.Count;
                Vector2 point = targeted ? (Vector2)candidates[i].transform.position :
                    RandomPoint(origin, safeOrigin, radius, blocks, vision);
                sequence.Add(point, targeted);
            }
            if (i < randomCount) sequence.Add(RandomPoint(origin, safeOrigin, radius, blocks, vision), false);
        }
        CharacterPotionBuffs.NotifyAction(caster);
        mover.TryBeginItemAttack(.24f);
        sequence.Advance(0f);
        return true;
    }

    private static bool Visible(Vector2 point, Vector2 origin, float radius, int blocks,
        CameraCircularVision vision, Transform target)
    {
        if ((point - origin).sqrMagnitude > radius * radius) return false;
        if (vision != null) return vision.IsWorldPositionVisible(point, target);
        var hit = Physics2D.Linecast(origin, point, blocks);
        return hit.collider == null || target != null &&
            (hit.transform == target || hit.transform.IsChildOf(target));
    }

    private static Vector2 RandomPoint(Vector2 origin, Vector2 fallback, float radius, int blocks, CameraCircularVision vision)
    {
        // Bounded rejection sampling remains cheap in cramped rooms. A valid
        // near-body fallback prevents an endless search when almost all rays hit walls.
        for (int attempt = 0; attempt < 64; attempt++)
        {
            Vector2 point = origin + Random.insideUnitCircle * radius;
            if (Physics2D.OverlapPoint(point, blocks) == null && Visible(point, origin, radius, blocks, vision, null)) return point;
        }
        return fallback;
    }

    private void Add(Vector2 point, bool targeted)
    {
        float at = strikes.Count * Interval;
        strikes.Add(new Strike { point = point, targeted = targeted, at = at });
        skillAudio?.AddLanding(point, at);
    }
    private void Update() => Advance(Time.deltaTime);
    private void OnDisable() => skillAudio?.Stop();
    private void OnDestroy() => skillAudio?.Stop();
    private bool InvalidOwner => owner == null || owner.IsDead || owner.IsGhostLike || GameSaveSystem.IsLoading ||
        ZeldaRuntimeRegistry.GetGameplayScene(owner.gameObject) != gameObject.scene;

    private void Advance(float delta)
    {
        if (cancelled) return;
        if (InvalidOwner) { cancelled = true; skillAudio?.Stop(); Destroy(gameObject); return; }
        elapsed += Mathf.Max(0f, delta);
        foreach (var strike in strikes)
        {
            if (elapsed < strike.at) break;
            float age = elapsed - strike.at, impactAge = age - FallDuration;
            if (strike.visual == null)
            {
                var go = new GameObject("Purple Meteor"); go.transform.SetParent(transform, false); go.transform.position = strike.point;
                strike.visual = go.AddComponent<MeteorShowerVisual>(); strike.visual.Configure(fire, strike.at);
            }
            if (!strike.impacted && impactAge >= 0f)
            {
                strike.impacted = true; impacts++;
                ZeldaAttackHitbox.ApplyCircularPulse(strike.point, Radius, 10, owner);
                ApplyBuriedSandwormImpact(strike.point);
                if (shake != null) shake.PlayShake(.16f, .12f, 27f);
            }
            // Five independent pulses at +1,+2,+3,+4,+5 seconds. Never reuse a
            // strike's hit history between seconds; multi-collider bodies still take one hit per pulse.
            while (fire && strike.fireTicks < 5 && impactAge + .0001f >= strike.fireTicks + 1)
            {
                strike.fireTicks++;
                ZeldaAttackHitbox.ApplyCircularPulse(strike.point, Radius, 2, owner);
            }
            if (InvalidOwner) { cancelled = true; skillAudio?.Stop(); Destroy(gameObject); return; }
            strike.visual.RenderAt(age);
        }
        skillAudio?.RenderAt(elapsed);
        if (elapsed >= (strikes.Count - 1) * Interval + FallDuration + (fire ? FireDuration : .65f) + .25f)
            Destroy(gameObject);
    }

    private void ApplyBuriedSandwormImpact(Vector2 point)
    {
        // Only run on landing, not on fire ticks or every frame. The existing active
        // registry avoids scanning the scene; snapshot it because lethal hits remove entries.
        if (InvalidOwner) return;
        buriedTargets.Clear();
        foreach (var worm in SandwormZeldaCharacterData.ActiveSandworms)
            if (worm != null && worm != owner && worm.isActiveAndEnabled && !worm.IsDead &&
                ZeldaRuntimeRegistry.GetGameplayScene(worm.gameObject) == gameObject.scene)
                buriedTargets.Add(worm);
        foreach (var worm in buriedTargets)
        {
            if (InvalidOwner) break;
            if (worm != null) worm.GetComponent<SandwormEruptionAttack>()?.TryApplyBuriedImpact(point, Radius, 10, owner);
        }
        buriedTargets.Clear();
    }
}
