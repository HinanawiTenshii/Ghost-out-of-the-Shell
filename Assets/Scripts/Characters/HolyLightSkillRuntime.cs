using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>A scene-local, fixed-origin sequence. A column hits each target at most once as its shockwave expands.</summary>
public sealed class HolyLightSkillRuntime : MonoBehaviour
{
    public const int EnergyCost = 4;
    public const float CentralDuration = 2f;
    public const float DescentDuration = .16f;
    public const float RoundInterval = .36f;
    public const float FirstRoundDistance = 4f;
    public const float RoundSpacing = 2f;
    public const float SmallDuration = .65f;
    public const float FadeDuration = .25f;
    private static readonly Vector2[] Directions = { Vector2.up, Vector2.right, Vector2.down, Vector2.left };
    private sealed class Strike
    {
        public Vector2 center;
        public float at, radius, hold;
        public bool damageFinished;
        public ZeldaAttackHitbox resolver;
        public HolyLightColumnVisual visual;
    }
    private readonly List<Strike> strikes = new List<Strike>(17);
    private ZeldaCharacterData owner;
    private Vector2 origin;
    private int rounds, roundsIssued, bonus;
    private float elapsed;
    private bool cancelled;
    private HolyLightSkillAudio skillAudio;

    // The returned value is the OUTER edge, including the ring's stroke width.
    // Both physics and every ground ring use this value; the nominal radii remain 3 and 1.
    public static float GetShockwaveRadius(float maximumRadius, float impactAge, float duration)
    {
        if (impactAge < 0f) return 0f;
        float spreadTime = Mathf.Min(.72f, Mathf.Max(.01f, duration * .65f));
        return Mathf.Max(0f, maximumRadius) * Mathf.Lerp(.06f, 1f, Mathf.Clamp01(impactAge / spreadTime));
    }

    public static bool TryUse(ZeldaCharacterData caster)
    {
        var mover = caster != null ? caster.GetComponent<ZeldaFourWayMover>() : null;
        if (caster == null || caster.IsDead || caster.IsGhostLike || mover == null || !mover.isActiveAndEnabled ||
            mover != ZeldaRuntimeRegistry.GetControlledMover() || mover.IsAttacking || Time.timeScale <= 0f ||
            DocumentReader.IsInputBlocked || ClockworkPuppetRuntime.BlocksCharacterInput) return false;
        var growth = PlayerGrowthAttributes.Instance;
        if (caster.NativeCharacterSkillId != "holy_light" && (growth == null || !growth.HasSkill("holy_light"))) return false;
        var scene = ZeldaRuntimeRegistry.GetGameplayScene(caster.gameObject);
        if (!scene.IsValid() || !scene.isLoaded || !caster.TrySpendPossessionEnergy(EnergyCost)) return false;
        var root = new GameObject("Holy Light Strike");
        SceneManager.MoveGameObjectToScene(root, scene);
        root.transform.position = caster.transform.position;
        var sequence = root.AddComponent<HolyLightSkillRuntime>();
        sequence.owner = caster;
        sequence.origin = caster.transform.position;
        sequence.rounds = growth != null && growth.HasSkill("holy_light_2") ? 4 : 3;
        sequence.bonus = growth != null && growth.HasSkill("holy_light_3") ? 1 : 0;
        sequence.skillAudio = new HolyLightSkillAudio(root, sequence.origin, sequence.rounds);
        sequence.skillAudio.RenderAt(0f, false);
        // Wake suspended targets before the first pulse; retain the full outgoing cross until it finishes.
        root.AddComponent<CameraVisionStreamingRegion>().Configure(FirstRoundDistance + (sequence.rounds - 1) * RoundSpacing + 1f);
        sequence.AddStrike(sequence.origin, 0f, 3f, 4 + sequence.bonus, CentralDuration);
        CharacterPotionBuffs.NotifyAction(caster);
        mover.TryBeginItemAttack(.24f);
        return true;
    }

    private void AddStrike(Vector2 center, float at, float radius, int power, float hold)
    {
        var go = new GameObject("Holy Light Column"); go.transform.SetParent(transform, false);
        go.transform.position = center;
        var visual = go.AddComponent<HolyLightColumnVisual>(); visual.Configure(radius, hold);
        strikes.Add(new Strike { center = center, at = at, radius = radius, hold = hold, visual = visual,
            resolver = ZeldaAttackHitbox.CreateCircularAreaResolver(power, owner, go.transform) });
    }

    private void Update() => Advance(Time.deltaTime);
    private void OnEnable() => AutoSaveTransientActivity.Register(this);
    private void OnDisable()
    {
        AutoSaveTransientActivity.Unregister(this);
        skillAudio?.Stop();
    }
    private void Advance(float delta)
    {
        if (delta <= 0f)
        {
            skillAudio?.RenderAt(elapsed, cancelled || owner == null || owner.IsDead || owner.IsGhostLike ||
                ZeldaRuntimeRegistry.GetGameplayScene(owner.gameObject) != gameObject.scene);
            return;
        }
        elapsed += delta;
        // Travel must not send delayed damage into the next scene. Already visible light can fade after death.
        if (owner == null || owner.IsDead || owner.IsGhostLike ||
            ZeldaRuntimeRegistry.GetGameplayScene(owner.gameObject) != gameObject.scene) cancelled = true;
        while (!cancelled && roundsIssued < rounds &&
            elapsed >= DescentDuration + CentralDuration + roundsIssued * RoundInterval)
        {
            float at = DescentDuration + CentralDuration + roundsIssued * RoundInterval;
            float distance = FirstRoundDistance + roundsIssued * RoundSpacing;
            foreach (var direction in Directions) AddStrike(origin + direction * distance, at, 1f, 2 + bonus, SmallDuration);
            roundsIssued++;
        }
        // All columns sample the same physics state. The inactive resolvers reuse their
        // contact buffers and hit sets instead of allocating a new hitbox on every frame.
        if (!cancelled) Physics2D.SyncTransforms();
        foreach (var strike in strikes)
        {
            float age = elapsed - strike.at;
            float impactAge = age - DescentDuration;
            if (!strike.damageFinished && impactAge >= 0f)
            {
                bool endDamage = cancelled || impactAge >= strike.hold;
                // Mark before callbacks. A frame crossing the end still samples the final
                // radius once, so a low frame rate cannot skip the last part of the wave.
                strike.damageFinished = endDamage;
                if (!cancelled && owner != null && !owner.IsDead && strike.resolver != null)
                    strike.resolver.ResolveCircularArea(strike.center,
                        GetShockwaveRadius(strike.radius, Mathf.Min(impactAge, strike.hold), strike.hold), false);
                if (endDamage && strike.resolver != null) Destroy(strike.resolver.gameObject);
            }
            strike.visual.RenderAt(age);
        }
        skillAudio?.RenderAt(elapsed, cancelled);
        float end = DescentDuration + CentralDuration + (rounds - 1) * RoundInterval +
            DescentDuration + SmallDuration + FadeDuration;
        if (elapsed >= end) Destroy(gameObject);
    }
}
