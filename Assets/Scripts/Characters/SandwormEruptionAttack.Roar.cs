using System.Collections.Generic;
using UnityEngine;

/// <summary>Fear roars and meteor impacts eject the buried body, stun in place, then allow a harmless return underground.</summary>
public sealed partial class SandwormEruptionAttack
{
    [Header("Fear Roar / 恐惧咆哮震出")]
    [SerializeField, Min(0f)] private float roarStunDuration = 5f;
    [SerializeField, Min(.15f)] private float roarPopDuration = .55f;
    [SerializeField, Min(.05f), Tooltip("震出时向屏幕上方弹起的高度，随沙虫整体尺寸缩放。")]
    private float roarPopHeight = .65f;
    [SerializeField, Min(.15f)] private float roarBurrowDuration = .45f;

    private readonly List<Vector2> roarHeadHistory = new List<Vector2>(512);
    public bool IsRoarRecovery => Phase == AttackPhase.RoarRising || Phase == AttackPhase.RoarStunned || Phase == AttackPhase.RoarBurrowing;

    /// <summary>
    /// Resolve one direct impact against the buried body, whose colliders are disabled.
    /// A multi-section overlap is still one hit. Exposed/airborne bodies retain their
    /// existing physics damage rules and must not restart this recovery sequence.
    /// </summary>
    public bool TryApplyBuriedImpact(Vector2 center, float radius, int attackPower, ZeldaCharacterData attacker, bool independentExplosion = false)
    {
        if (radius <= 0f || attackPower <= 0 || (!independentExplosion && (attacker == null || attacker.IsDead)) ||
            !CanBeEjected() || character == attacker || !character.CanTakeAttackDamage ||
            !TouchesBuriedBody(center, radius)) return false;

        character.TakeDamage(attackPower, attacker);
        // Lethal impacts use the ordinary death sequence, never resurrect/eject remains.
        if (!character.IsDead && isActiveAndEnabled) BeginRoarRecovery();
        return true;
    }

    private bool CanBeEjected()
    {
        return isActiveAndEnabled && Initialize() && !character.IsDead && character.isActiveAndEnabled &&
            movement.isActiveAndEnabled && movement.MovementEnabled && states.IsUnderground &&
            (Phase == AttackPhase.Roaming || Phase == AttackPhase.Approaching || Phase == AttackPhase.Settling);
    }

    private void TryReactToOngoingRoar()
    {
        // State remains Underground during a normal breach, so also check the
        // timeline: airborne / partially exposed / recovering bodies are not buried.
        if (!CanBeEjected()) return;
        foreach (var roar in FearRoarArea.ActiveAreas)
        {
            if (roar == null || !roar.IsOngoing || roar.gameObject.scene != gameObject.scene ||
                !TouchesBuriedBody(roar.transform.position, roar.Radius)) continue;
            if (!roar.TryAffectSandworm(this)) continue;
            BeginRoarRecovery();
            return;
        }
    }

    private bool TouchesBuriedBody(Vector2 center, float radius)
    {
        // Underground colliders are disabled and the root stays at the original
        // roaming-area origin. Test each rotated visible section, ignoring Z.
        foreach (var part in parts)
        {
            var renderer = part.GetComponent<SpriteRenderer>();
            if (renderer == null || renderer.sprite == null) continue;
            Vector3 point = part.InverseTransformPoint(center);
            Bounds bounds = renderer.sprite.bounds;
            point.z = bounds.center.z;
            Vector3 closest = part.TransformPoint(bounds.ClosestPoint(point));
            if (((Vector2)closest - center).sqrMagnitude <= radius * radius) return true;
        }
        return false;
    }

    private void BeginRoarRecovery()
    {
        CancelAttack(); // Cancels a pending target/warning without damage or teleporting.
        ResetSwallowedExplosives();
        EmergencePulses = LandingPulses = 0;
        DamageRadius = .1f;
        for (int i = 0; i < parts.Length; i++)
            DamageRadius = Mathf.Max(DamageRadius, visuals[i].NominalSize.y * Mathf.Abs(parts[i].lossyScale.y) * .5f * radiusMultiplier);

        // Prepare from the real ground pose before the visual upward offset.
        // The same history/route is handed back after sinking, with no fresh turn.
        movement.ResumeRoamingFromCurrentPose();
        movement.CopyHeadHistory(roarHeadHistory);
        movement.BeginExternalPathControl();
        movement.TryPickDestination();
        savedPositions = new Vector3[parts.Length]; savedRotations = new Quaternion[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        { savedPositions[i] = parts[i].localPosition; savedRotations[i] = parts[i].localRotation; }
        lockedRoot = transform.localToWorldMatrix;
        Phase = AttackPhase.RoarRising; elapsed = 0f;
        if (!states.BeginSegmentedPassage()) { CancelAttack(); return; }
        EmitRoarDust(true);
    }

    private void EmitRoarDust(bool rising)
    {
        // Only three dust sites, never one particle system per segment. No damage,
        // red attack warning, bomb visual, or swallowing explosives on this path.
        for (int sample = 0; sample < 3; sample++)
        {
            int i = sample * (parts.Length - 1) / 2;
            Vector2 ground = transform.TransformPoint(savedPositions[i]);
            var site = SandwormEruptionSite.Create(gameObject.scene, ground, DamageRadius * (rising ? .7f : .4f), false);
            site.gameObject.name = rising ? "Sandworm Roar Ejection Dust" : "Sandworm Roar Recovery Dust";
            site.Pulse(rising); site.Finish(); impactSites.Add(site);
        }
    }

    private void ApplyRoarPose(float height)
    {
        for (int i = 0; i < parts.Length; i++)
        {
            parts[i].localPosition = savedPositions[i];
            parts[i].position += Vector3.up * height;
            parts[i].localRotation = savedRotations[i];
        }
    }

    private void AdvanceRoarRecovery(float deltaTime)
    {
        movement.AdvancePreparedRoaming(deltaTime);
        float remaining = deltaTime;
        if (Phase == AttackPhase.RoarRising)
        {
            float duration = Mathf.Max(.15f, roarPopDuration);
            float step = Mathf.Min(remaining, Mathf.Max(0f, duration - elapsed));
            elapsed += step; remaining -= step;
            float t = Mathf.Clamp01(elapsed / duration);
            // A quick launch and a slower fall, then a little landing bounce.
            float lift = t < .82f ? Mathf.Sin(Mathf.PI * Mathf.Pow(t / .82f, .65f)) :
                .08f * Mathf.Sin(Mathf.PI * (t - .82f) / .18f);
            ApplyRoarPose(Mathf.Max(0f, lift) * roarPopHeight * Scale);
            for (int i = 0; i < parts.Length; i++) states.SetSegmentExposed(visuals[i], t >= .06f + .06f * i / Mathf.Max(1f, parts.Length - 1));
            if (t < 1f) return;
            ApplyRoarPose(0f);
            states.EnterAboveGround(); states.EndSegmentedPassage();
            Phase = AttackPhase.RoarStunned; elapsed = 0f;
            ShowStunIndicator(true);
            ClearGroundedCharacterOverlaps();
        }
        if (Phase == AttackPhase.RoarStunned)
        {
            float step = Mathf.Min(remaining, Mathf.Max(0f, roarStunDuration - elapsed));
            elapsed += step; remaining -= step;
            ApplyRoarPose(0f); ClearGroundedCharacterOverlaps(); PositionStunIndicator();
            if (elapsed < roarStunDuration) return;
            ShowStunIndicator(false);
            states.BeginSegmentedPassage(true); states.EnterUnderground();
            Phase = AttackPhase.RoarBurrowing; elapsed = 0f;
            EmitRoarDust(false);
        }
        if (Phase != AttackPhase.RoarBurrowing) return;
        float burrowDuration = Mathf.Max(.15f, roarBurrowDuration);
        float burialStep = Mathf.Min(remaining, Mathf.Max(0f, burrowDuration - elapsed));
        elapsed += burialStep; remaining -= burialStep;
        float progress = Mathf.Clamp01(elapsed / burrowDuration);
        ApplyRoarPose(-roarPopHeight * Scale * .2f * Mathf.Sin(Mathf.PI * progress));
        for (int i = 0; i < parts.Length; i++) states.SetSegmentExposed(visuals[i], progress < .35f + .25f * i / Mathf.Max(1f, parts.Length - 1));
        if (progress < 1f) return;
        ApplyRoarPose(0f); states.EndSegmentedPassage();
        if (!movement.ResumePreparedRoaming(roarHeadHistory)) movement.ResumeRoamingFromCurrentPose();
        savedPositions = null; savedRotations = null; roarHeadHistory.Clear();
        cycleStart = movement.CompletedRoamingLegs;
        handedOff = true; Phase = AttackPhase.Settling; elapsed = 0f;
        if (remaining > 0f) movement.Advance(remaining);
    }
}
