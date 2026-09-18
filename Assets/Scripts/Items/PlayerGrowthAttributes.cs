using System;
using UnityEngine;

/// <summary>
/// Stores character-independent player growth bonuses.
/// Add one instance to a scene; duplicate instances are discarded automatically.
/// </summary>
[DefaultExecutionOrder(-200)]
public sealed class PlayerGrowthAttributes : MonoBehaviour
{
    [Serializable]
    public struct RuntimeState
    {
        public int attackPower;
        public int skillValue;
        public int possessionEnergy;
        public int collectibleCount;
        public string[] unlockedSkills;
        public string[] equippedSkills;
        public string[] discoveredCharacterSkills;
    }

    [SerializeField] private bool persistBetweenScenes = true;
    [SerializeField, Min(0)] private int attackPower;
    [SerializeField, Min(0)] private int skillValue;
    [SerializeField, Min(0)] private int possessionEnergy;
    [SerializeField, Min(0)] private int collectibleCount;
    [SerializeField] private System.Collections.Generic.List<string> unlockedSkills = new System.Collections.Generic.List<string>();

    public enum SkillType { Passive, Active }
    private System.Collections.Generic.List<string> discoveredCharacterSkills = new System.Collections.Generic.List<string>();

    public sealed class SkillDefinition
    {
        public string id, title, description;
        public int cost, healthBonus, attackBonus, energyBonus;
        public bool locked;
        public SkillType type;
        public Vector2 position;
        public string parentId;
        public string replacesSkillId;
        public string requiredPossessedSkillId;
    }

    // Add future nodes and effects here; saved progress is keyed by stable IDs.
    public static readonly SkillDefinition[] Skills =
    {
        new SkillDefinition { id = "essence_1", title = "本质强化", description = "强化自身的本质，永久增加一点E.G.O上限。", type = SkillType.Passive, cost = 6, energyBonus = 1, position = new Vector2(-540, -165), parentId = "royal_command_2" },
        new SkillDefinition { id = "royal_command", title = "发号施令", description = "消耗一点E.G.O发动，使用自身的权威压制对方，使得受影响的角色放弃对你的敌意，并在一段时间内无视你的行为。", type = SkillType.Active, cost = 4, position = new Vector2(-540, 165), requiredPossessedSkillId = "royal_command" },
        new SkillDefinition { id = "royal_command_2", title = "发号施令Ⅱ", description = "通过强化，增强表演能力，使得该技能可以影响更多的角色。", type = SkillType.Active, cost = 2, position = new Vector2(-540, 0), parentId = "royal_command", replacesSkillId = "royal_command", requiredPossessedSkillId = "royal_command" },
        new SkillDefinition { id = "fear_roar", title = "恐惧咆哮", description = "消耗三点E.G.O发动，释放出震耳欲聋的咆哮，使附近所有角色进入眩晕状态。", type = SkillType.Active, cost = 4, position = new Vector2(-390, 165), requiredPossessedSkillId = "fear_roar" },
        new SkillDefinition { id = "fear_roar_2", title = "恐惧咆哮Ⅱ", description = "消耗三点E.G.O发动，通过强化，增大了咆哮的影响范围。", type = SkillType.Active, cost = 2, position = new Vector2(-390, 0), parentId = "fear_roar", replacesSkillId = "fear_roar", requiredPossessedSkillId = "fear_roar" },
        new SkillDefinition { id = "fear_roar_3", title = "恐惧咆哮Ⅲ", description = "消耗三点E.G.O发动，为声音中添加令人恐惧的咒语，延长眩晕的持续时间。", type = SkillType.Active, cost = 2, position = new Vector2(-390, -165), parentId = "fear_roar_2", replacesSkillId = "fear_roar_2", requiredPossessedSkillId = "fear_roar" },
        new SkillDefinition { id = "combat_expertise", title = "战斗专精", description = "消耗两点E.G.O发动，使自身在短时间内专注于攻击，在使用后一段时间内的力量值翻倍", type = SkillType.Active, cost = 4, position = new Vector2(-240, 165), requiredPossessedSkillId = "combat_expertise" },
        new SkillDefinition { id = "combat_expertise_2", title = "战斗专精Ⅱ", description = "消耗两点E.G.O发动，经由强化，可以在技能生效时恢复生命值。", type = SkillType.Active, cost = 2, position = new Vector2(-240, 0), parentId = "combat_expertise", replacesSkillId = "combat_expertise", requiredPossessedSkillId = "combat_expertise" },
        new SkillDefinition { id = "strength_1", title = "力量强化", description = "经由强化，永久提升角色的一点力量。", type = SkillType.Passive, cost = 4, attackBonus = 1, position = new Vector2(-240, -165), parentId = "combat_expertise_2" },
        new SkillDefinition { id = "health_1", title = "生命强化", description = "经由强化，永久增加角色的两点生命上限。", cost = 2, healthBonus = 2, position = new Vector2(-90, 0) },
        new SkillDefinition { id = "soul_mark", title = "灵魂标记", description = "使用时为一个可以附身的角色施加标记，施加标记后再次使用会将当前角色转化为一个炸弹，同时幽灵会转移到之前施加标记的角色身上\n（注意：切换场景时标记会清除）", type = SkillType.Active, cost = 4, position = new Vector2(100, 165), parentId = "health_1" },
        new SkillDefinition { id = "soul_mark_2", title = "灵魂标记Ⅱ", description = "强化后的灵魂标记，增强了自爆时的爆炸威力与范围。", type = SkillType.Active, cost = 2, position = new Vector2(280, 165), parentId = "soul_mark", replacesSkillId = "soul_mark" },
        new SkillDefinition { id = "soul_mark_3", title = "灵魂标记Ⅲ", description = "进一步强化后的灵魂标记，降低了使用时消耗的E.G.O量。", type = SkillType.Active, cost = 2, position = new Vector2(460, 165), parentId = "soul_mark_2", replacesSkillId = "soul_mark_2" },
        new SkillDefinition { id = "ghost_form", title = "幽灵形态", description = "消耗两点E.G.O发动，发动后进入幽灵形态，将会变得无法被察觉，同时也无法与物品进行互动。", type = SkillType.Active, cost = 4, position = new Vector2(100, 0), parentId = "health_1" },
        new SkillDefinition { id = "ghost_form_2", title = "幽灵形态Ⅱ", description = "通过强化，增加幽灵形态持续时间", type = SkillType.Active, cost = 2, position = new Vector2(280, 0), parentId = "ghost_form", replacesSkillId = "ghost_form" },
        new SkillDefinition { id = "ghost_form_3", title = "幽灵形态Ⅲ", description = "通过强化，降低了使用时消耗的E.G.O", type = SkillType.Active, cost = 2, position = new Vector2(460, 0), parentId = "ghost_form_2", replacesSkillId = "ghost_form_2" },
        new SkillDefinition { id = "domino", title = "骨牌", description = "消耗两点E.G.O发动，选中最多两名角色，将他们与当前控制的角色连接起来，共同承受所有的伤害，并且被绑定者其中一人死亡时，其他人也会死亡。", type = SkillType.Active, cost = 3, position = new Vector2(100, -165), parentId = "health_1" },
        new SkillDefinition { id = "domino_2", title = "骨牌Ⅱ", description = "通过强化，可以连接更多的角色", type = SkillType.Active, cost = 2, position = new Vector2(280, -165), parentId = "domino", replacesSkillId = "domino" },
        new SkillDefinition { id = "domino_3", title = "骨牌Ⅲ", description = "通过强化，增大了可以影响的范围", type = SkillType.Active, cost = 2, position = new Vector2(460, -165), parentId = "domino_2", replacesSkillId = "domino_2" }
    };

    public static bool IsCombatExpertiseSkill(string id) => id == "combat_expertise" || id == "combat_expertise_2";
    public static bool IsFearRoarSkill(string id) => id == "fear_roar" || id == "fear_roar_2" || id == "fear_roar_3";
    public static bool IsRoyalCommandSkill(string id) => id == "royal_command" || id == "royal_command_2";
    public int RoyalCommandLevel => HasSkill("royal_command_2") ? 2 : HasSkill("royal_command") ? 1 : 0;
    public int FearRoarLevel => HasSkill("fear_roar_3") ? 3 : HasSkill("fear_roar_2") ? 2 : HasSkill("fear_roar") ? 1 : 0;
    public bool HasSkill(string id) => unlockedSkills.Contains(id);
    public bool IsSkillDiscovered(SkillDefinition skill) => skill != null &&
        (string.IsNullOrEmpty(skill.requiredPossessedSkillId) || HasSkill(skill.id) || discoveredCharacterSkills.Contains(skill.requiredPossessedSkillId));

    public void RecordPossessedCharacter(ZeldaCharacterData character)
    {
        string id = character != null ? character.NativeCharacterSkillId : null;
        if (string.IsNullOrEmpty(id) || discoveredCharacterSkills.Contains(id)) return;
        discoveredCharacterSkills.Add(id);
        AttributesChanged?.Invoke();
    }
    [SerializeField] private string[] equippedSkills = new string[5];
    public int SoulMarkLevel => HasSkill("soul_mark_3") ? 3 : HasSkill("soul_mark_2") ? 2 : HasSkill("soul_mark") ? 1 : 0;
    public int DominoLevel => HasSkill("domino_3") ? 3 : HasSkill("domino_2") ? 2 : HasSkill("domino") ? 1 : 0;
    public int GhostFormLevel => HasSkill("ghost_form_3") ? 3 : HasSkill("ghost_form_2") ? 2 : HasSkill("ghost_form") ? 1 : 0;
    public static bool IsGhostFormSkill(string id) => id == "ghost_form" || id == "ghost_form_2" || id == "ghost_form_3";
    public static bool IsDominoSkill(string id) => id == "domino" || id == "domino_2" || id == "domino_3";
    public static bool IsSoulMarkSkill(string id) => id == "soul_mark" || id == "soul_mark_2" || id == "soul_mark_3";
    public string GetEffectiveSkillId(string id)
    {
        if (string.IsNullOrEmpty(id)) return id;
        for (int i = 0; i < Skills.Length; i++)
        {
            var upgrade = Array.Find(Skills, s => s.replacesSkillId == id && HasSkill(s.id));
            if (upgrade == null) break;
            id = upgrade.id;
        }
        return id;
    }
    public string GetEquippedSkill(int slot) => equippedSkills != null && slot >= 0 && slot < equippedSkills.Length ? GetEffectiveSkillId(equippedSkills[slot]) : null;
    public bool ToggleEquippedSkill(int slot, string id)
    {
        var skill = Array.Find(Skills, s => s.id == id);
        if (slot < 0 || slot >= 5 || skill == null || skill.type != SkillType.Active || !HasSkill(id)) return false;
        if (equippedSkills == null || equippedSkills.Length != 5) Array.Resize(ref equippedSkills, 5);
        id = GetEffectiveSkillId(id);
        bool unequip = GetEquippedSkill(slot) == id;
        // A skill belongs to only one slot. Move it atomically before notifying UI.
        for (int i = 0; i < equippedSkills.Length; i++)
            if (GetEquippedSkill(i) == id) equippedSkills[i] = null;
        if (!unequip) equippedSkills[slot] = id;
        AttributesChanged?.Invoke();
        return true;
    }
    public int HealthBonus
    {
        get { int value = 0; foreach (var skill in Skills) if (HasSkill(skill.id)) value += skill.healthBonus; return value; }
    }
    public bool HasSkillPrerequisite(SkillDefinition skill)
    {
        return skill != null && (string.IsNullOrEmpty(skill.parentId) || HasSkill(skill.parentId));
    }

    public bool TryUpgradeSkill(string id)
    {
        var skill = Array.Find(Skills, s => s.id == id);
        if (skill == null || skill.locked || !IsSkillDiscovered(skill) || HasSkill(id) || !HasSkillPrerequisite(skill) || collectibleCount < skill.cost) return false;
        collectibleCount -= skill.cost;
        unlockedSkills.Add(id);
        var mover = ZeldaRuntimeRegistry.GetControlledMover();
        if (mover != null) mover.GetComponent<ZeldaCharacterData>()?.ApplyPlayerHealthUpgrade();
        AttributesChanged?.Invoke();
        return true;
    }

    public static PlayerGrowthAttributes Instance { get; private set; }
    public static event Action InstanceChanged;

    public int AttackPower
    {
        get
        {
            int value = attackPower;
            foreach (var skill in Skills) if (HasSkill(skill.id)) value += skill.attackBonus;
            return value;
        }
    }
    public int SkillValue => skillValue;
    public int PossessionEnergy
    {
        get
        {
            int value = possessionEnergy;
            foreach (var skill in Skills) if (HasSkill(skill.id)) value += skill.energyBonus;
            return value;
        }
    }
    public int CollectibleCount => collectibleCount;

    public event Action AttributesChanged;

    public RuntimeState CaptureRuntimeState()
    {
        return new RuntimeState
        {
            attackPower = attackPower,
            skillValue = skillValue,
            possessionEnergy = possessionEnergy,
            collectibleCount = collectibleCount,
            unlockedSkills = unlockedSkills.ToArray(),
            discoveredCharacterSkills = discoveredCharacterSkills.ToArray(),
            equippedSkills = equippedSkills != null ? (string[])equippedSkills.Clone() : new string[5]
        };
    }

    public void ApplyRuntimeState(RuntimeState state)
    {
        attackPower = Mathf.Max(0, state.attackPower);
        skillValue = Mathf.Max(0, state.skillValue);
        possessionEnergy = Mathf.Max(0, state.possessionEnergy);
        collectibleCount = Mathf.Max(0, state.collectibleCount);
        unlockedSkills = new System.Collections.Generic.List<string>(state.unlockedSkills ?? Array.Empty<string>());
        discoveredCharacterSkills = new System.Collections.Generic.List<string>(state.discoveredCharacterSkills ?? Array.Empty<string>());
        equippedSkills = new string[5];
        if (state.equippedSkills != null) Array.Copy(state.equippedSkills, equippedSkills, Mathf.Min(5, state.equippedSkills.Length));
        AttributesChanged?.Invoke();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ClampValues();
        InstanceChanged?.Invoke();
        if (persistBetweenScenes)
        {
            DontDestroyOnLoad(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            InstanceChanged?.Invoke();
        }
    }

    public void SetAttributes(
        int newAttackPower,
        int newSkillValue,
        int newPossessionEnergy)
    {
        newAttackPower = Mathf.Max(0, newAttackPower);
        newSkillValue = Mathf.Max(0, newSkillValue);
        newPossessionEnergy = Mathf.Max(0, newPossessionEnergy);

        if (attackPower == newAttackPower &&
            skillValue == newSkillValue &&
            possessionEnergy == newPossessionEnergy)
        {
            return;
        }

        attackPower = newAttackPower;
        skillValue = newSkillValue;
        possessionEnergy = newPossessionEnergy;
        AttributesChanged?.Invoke();
    }

    public void AddAttackPower(int amount)
    {
        SetAttributes(
            Mathf.Max(0, attackPower + amount),
            skillValue,
            possessionEnergy);
    }

    public void AddSkillValue(int amount)
    {
        SetAttributes(
            attackPower,
            Mathf.Max(0, skillValue + amount),
            possessionEnergy);
    }

    public void AddPossessionEnergy(int amount)
    {
        SetAttributes(
            attackPower,
            skillValue,
            Mathf.Max(0, possessionEnergy + amount));
    }

    public void AddCollectibles(int amount)
    {
        int nextCount = Mathf.Max(0, collectibleCount + amount);
        if (nextCount == collectibleCount)
        {
            return;
        }

        collectibleCount = nextCount;
        AttributesChanged?.Invoke();
    }

    private void ClampValues()
    {
        attackPower = Mathf.Max(0, attackPower);
        skillValue = Mathf.Max(0, skillValue);
        possessionEnergy = Mathf.Max(0, possessionEnergy);
        collectibleCount = Mathf.Max(0, collectibleCount);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ClampValues();
    }
#endif
}
