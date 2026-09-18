using UnityEngine;

/// <summary>Native skill data carried by King, not by ordinary Noble bodies.</summary>
[AddComponentMenu("Cogitans/Characters/Native Skills/Royal Command")]
public sealed class RoyalCommandCharacterSkillData : NativeCharacterSkillData
{
    public override string SkillId => "royal_command";
    public override string SkillName => "发号施令";
    public override Sprite Icon => ZeldaCharacterData.CommandIcon;
    public override bool TryUse(ZeldaCharacterData owner) => owner != null && owner.TryUseRoyalCommand();
}
