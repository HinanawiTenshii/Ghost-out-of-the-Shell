using UnityEngine;

/// <summary>
/// Optional body-owned skill data. Only native skill owners carry a concrete
/// component; common character data has no per-skill Inspector switches.
/// Learned player skills are independent of this component.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ZeldaCharacterData))]
public abstract class NativeCharacterSkillData : MonoBehaviour
{
    public abstract string SkillId { get; }
    public abstract string SkillName { get; }
    public abstract Sprite Icon { get; }
    public abstract bool TryUse(ZeldaCharacterData owner);
}
