using System;
using UnityEngine;

/// <summary>Untimed, body-owned skill buff. State is embedded in the character save DTO.</summary>
public static class HolyShieldRuntime
{
    public const int EnergyCost = 2;
    public const float PreserveChance = .33f;
    [Serializable]
    public struct State
    {
        public int charges;
        public int capacity;
        public bool preserveCharges;
    }

    public static bool TryUse(ZeldaCharacterData owner)
    {
        var mover = owner != null ? owner.GetComponent<ZeldaFourWayMover>() : null;
        if (owner == null || owner.IsDead || owner.IsGhostLike || mover == null || !mover.isActiveAndEnabled ||
            mover != ZeldaRuntimeRegistry.GetControlledMover() || mover.IsAttacking || Time.timeScale <= 0f ||
            DocumentReader.IsInputBlocked || ClockworkPuppetRuntime.BlocksCharacterInput) return false;
        var growth = PlayerGrowthAttributes.Instance;
        if (owner.NativeCharacterSkillId != "holy_shield" && (growth == null || !growth.HasSkill("holy_shield"))) return false;
        if (!owner.TrySpendPossessionEnergy(EnergyCost)) return false;
        bool advanced = growth != null && growth.HasSkill("holy_shield_3");
        int capacity = advanced || growth != null && growth.HasSkill("holy_shield_2") ? 4 : 3;
        owner.RefreshHolyShield(capacity, advanced);
        CharacterPotionBuffs.NotifyAction(owner);
        mover.TryBeginItemAttack(.24f);
        return true;
    }

    public static State Sanitize(State state)
    {
        if (state.capacity <= 0) return default;
        state.capacity = Mathf.Clamp(state.capacity, 3, 4);
        state.charges = Mathf.Clamp(state.charges, 0, state.capacity);
        return state;
    }

    public static bool TryAbsorb(ref State state)
    {
        if (state.charges <= 0) return false;
        // One roll per actual hit, not per point of incoming damage. Empty shields cannot proc.
        if (!state.preserveCharges || UnityEngine.Random.value >= PreserveChance) state.charges--;
        return true;
    }
}
