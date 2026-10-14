using UnityEngine;

/// <summary>A read-only view of a live skill effect, not a second gameplay timer.</summary>
public readonly struct SkillBuffState
{
    public readonly Sprite Icon;
    public readonly float Remaining;
    public readonly float Duration;
    public bool IsTimed => Duration > 0f;
    public float NormalizedRemaining => IsTimed ? Mathf.Clamp01(Remaining / Duration) : 1f;

    public SkillBuffState(Sprite icon, float remaining = 0f, float duration = 0f)
    {
        Icon = icon;
        Remaining = remaining;
        Duration = duration;
    }
}

/// <summary>
/// Adapts existing skill state to the common buff HUD. Ownership, expiration,
/// damage links and save restoration remain in their original systems.
/// Stun, instant casts and permanent growth upgrades are deliberately excluded.
/// </summary>
public static class CharacterSkillBuffs
{
    public static bool TryGet(ZeldaCharacterData character, BuffDurationIcon kind, out SkillBuffState state)
    {
        state = default;
        if (character == null || character.IsDead) return false;
        var growth = PlayerGrowthAttributes.Instance;
        switch (kind)
        {
            case BuffDurationIcon.HolyShield:
                if (character.HolyShieldCharges <= 0) return false;
                state = new SkillBuffState(HolyShieldCharacterSkillData.GetIcon(character.HolyShieldPreservesCharges
                    ? 2 : character.HolyShieldCapacity >= 4 ? 1 : 0));
                return true;
            case BuffDurationIcon.CombatExpertise:
                if (!character.IsCombatExpertiseActive) return false;
                state = new SkillBuffState(character.CombatExpertiseBuffIcon,
                    character.CombatExpertiseProgress * ZeldaCharacterData.CombatExpertiseDuration,
                    ZeldaCharacterData.CombatExpertiseDuration);
                return true;
            case BuffDurationIcon.GhostForm:
                var form = character.GhostForm;
                if (form == null || form.Remaining <= 0f) return false;
                state = new SkillBuffState(SkillPageArt.GetAbilitySprite(GhostFormId(growth)),
                    form.Remaining, form.TotalDuration);
                return true;
            case BuffDurationIcon.Domino:
                if (!DominoSkillRuntime.HasActiveMark(character)) return false;
                state = new SkillBuffState(SkillPageArt.GetAbilitySprite(DominoId(growth)));
                return true;
            case BuffDurationIcon.SoulMark:
                if (!IsControlled(character) || !SoulMarkRuntime.HasPendingMarkInScene(
                    ZeldaRuntimeRegistry.GetGameplayScene(character.gameObject))) return false;
                state = new SkillBuffState(SkillPageArt.GetAbilitySprite(SoulMarkId(growth)));
                return true;
            case BuffDurationIcon.RoyalCommand:
                if (!IsControlled(character)) return false;
                // The command suppresses hostility to the player, not to the
                // original body. Follow actual NPC timers even after possession.
                float remaining = ZeldaRuntimeRegistry.GetRoyalCommandBuffRemaining(
                    ZeldaRuntimeRegistry.GetGameplayScene(character.gameObject));
                if (remaining <= 0f) return false;
                int rank = growth != null ? growth.RoyalCommandLevel : 0;
                Sprite commandIcon = rank > 0 ? SkillPageArt.GetAbilitySprite(rank >= 2
                    ? "royal_command_2" : "royal_command") : ZeldaCharacterData.CommandIcon;
                state = new SkillBuffState(commandIcon, remaining, ZeldaCharacterAiBase.RoyalCommandDuration);
                return true;
            default:
                return false;
        }
    }

    private static bool IsControlled(ZeldaCharacterData character)
    {
        var mover = ZeldaRuntimeRegistry.GetControlledMover();
        return mover != null && mover.gameObject == character.gameObject;
    }

    // Return interned identifiers; querying a frame never constructs a string,
    // texture, gameplay component or saved-state array.
    private static string GhostFormId(PlayerGrowthAttributes growth) => growth != null && growth.GhostFormLevel >= 3
        ? "ghost_form_3" : growth != null && growth.GhostFormLevel >= 2 ? "ghost_form_2" : "ghost_form";
    private static string DominoId(PlayerGrowthAttributes growth) => growth != null && growth.DominoLevel >= 3
        ? "domino_3" : growth != null && growth.DominoLevel >= 2 ? "domino_2" : "domino";
    private static string SoulMarkId(PlayerGrowthAttributes growth) => growth != null && growth.SoulMarkLevel >= 3
        ? "soul_mark_3" : growth != null && growth.SoulMarkLevel >= 2 ? "soul_mark_2" : "soul_mark";
}
