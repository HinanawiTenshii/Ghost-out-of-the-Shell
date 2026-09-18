using UnityEngine;

/// <summary>Native skill data for Warden and LegendaryGuardian bodies.</summary>
[AddComponentMenu("Cogitans/Characters/Native Skills/Combat Expertise")]
public sealed class CombatExpertiseCharacterSkillData : NativeCharacterSkillData
{
    public override string SkillId => "combat_expertise";
    public override string SkillName => "战斗专精";
    public override Sprite Icon => ZeldaHealthHeartsUI.GetCombatExpertiseIcon();
    public override bool TryUse(ZeldaCharacterData owner) => owner != null && owner.TryUseCombatExpertise();
}
