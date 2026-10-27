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
        public float potionEnergyRemaining;
        public int potionEnergyCurrent;
        public float areaPotionEnergyRemaining;
        public bool areaPotionEnergyFrozen;
        public string areaPotionEnergyToken;
        public bool exhibitionLoadoutGranted;
    }

    [SerializeField] private bool persistBetweenScenes = true;
    [SerializeField, Min(0)] private int attackPower;
    [SerializeField, Min(0)] private int skillValue;
    [SerializeField, Min(0)] private int possessionEnergy;
    [SerializeField, Min(0)] private int collectibleCount;
    private bool exhibitionLoadoutGranted;
    [SerializeField] private System.Collections.Generic.List<string> unlockedSkills = new System.Collections.Generic.List<string>();

    public enum SkillType { Passive, Active }
    private System.Collections.Generic.List<string> discoveredCharacterSkills = new System.Collections.Generic.List<string>();

    public sealed class SkillDefinition
    {
        public string id, title, description;
        public int cost, healthBonus, attackBonus, energyBonus, skillBonus;
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
        new SkillDefinition { id = "silence_oath", title = "缄口之誓", description = "消耗两点E.G.O发动，约束面前三步内的一名角色，持续三十秒。目标仍会追击与攻击，但不会传递敌意，追击与搜索独立计时。不能对警告、敌对或搜索中的新目标施放；重复施放可刷新时间。", type = SkillType.Active, cost = 4, position = new Vector2(-540, -660), requiredPossessedSkillId = "silence_oath" },
        new SkillDefinition { id = "silence_oath_2", title = "缄口之誓Ⅱ", description = "通过强化，使誓约伴随一次警戒。目标在效果期间进入警告、敌对或搜索时，誓约延续至其恢复平静或开始返回，最长九十秒。仍消耗两点E.G.O，其他角色亲眼发现你的行为时仍会正常反应。", type = SkillType.Active, cost = 2, position = new Vector2(-390, -660), parentId = "silence_oath", replacesSkillId = "silence_oath", requiredPossessedSkillId = "silence_oath" },
        new SkillDefinition { id = "silence_oath_3", title = "缄口之誓Ⅲ", description = "进一步强化誓约，可同时约束两名角色。每次仍只对一个目标施放，消耗两点E.G.O，两道誓约独立计时。对已有目标再次施放会刷新时间，不会占用另一个名额。", type = SkillType.Active, cost = 2, position = new Vector2(-240, -660), parentId = "silence_oath_2", replacesSkillId = "silence_oath_2", requiredPossessedSkillId = "silence_oath" },
        new SkillDefinition { id = "meteor_shower", title = "流星群", description = "消耗五点E.G.O发动，唤来十二颗紫色陨石，依次轰击施放时视野内的位置。六颗锁定不同角色当时的位置，其余随机坠落；可见角色不足时，缺少的落点也会随机选取。每次落地造成半径四、攻击力十的爆炸，不会伤及施术者。", type = SkillType.Active, cost = 6, position = new Vector2(-90, -495), requiredPossessedSkillId = "meteor_shower" },
        new SkillDefinition { id = "meteor_shower_2", title = "流星群Ⅱ", description = "通过强化，牵引更多域外星石。锁定角色的陨石增加两颗，随机坠落的陨石增加一颗，共十五颗。每名角色最多被锁定一次，不足的数量改为随机落点。仍消耗五点E.G.O发动。", type = SkillType.Active, cost = 3, position = new Vector2(100, -495), parentId = "meteor_shower", replacesSkillId = "meteor_shower", requiredPossessedSkillId = "meteor_shower" },
        new SkillDefinition { id = "meteor_shower_3", title = "流星群Ⅲ", description = "进一步强化，使陨石落地后留下燃烧五秒的紫色魔焰。魔焰覆盖爆炸范围，每秒造成一次攻击力二的伤害，不会伤及施术者。仍召唤十五颗陨石，消耗五点E.G.O发动。", type = SkillType.Active, cost = 3, position = new Vector2(280, -495), parentId = "meteor_shower_2", replacesSkillId = "meteor_shower_2", requiredPossessedSkillId = "meteor_shower" },
        new SkillDefinition { id = "holy_shield", title = "圣盾", description = "消耗两点E.G.O发动，以圣盾庇护自身，完全抵御接下来的三次伤害。圣盾不会随时间消散，再次施放会将剩余抵御次数恢复至三次，但不会叠加。", type = SkillType.Active, cost = 4, position = new Vector2(-540, -495), requiredPossessedSkillId = "holy_shield" },
        new SkillDefinition { id = "holy_shield_2", title = "圣盾Ⅱ", description = "通过强化，使圣盾更加坚固，可抵御的伤害增加一次，共四次。仍消耗两点E.G.O发动，再次施放会将剩余抵御次数恢复至四次。", type = SkillType.Active, cost = 2, position = new Vector2(-390, -495), parentId = "holy_shield", replacesSkillId = "holy_shield", requiredPossessedSkillId = "holy_shield" },
        new SkillDefinition { id = "holy_shield_3", title = "圣盾Ⅲ", description = "进一步强化庇护，在圣盾仍有剩余次数时，每次抵御伤害有33%的概率不消耗次数。圣盾拥有四次抵御机会，消耗两点E.G.O可重新施放；次数耗尽后不再触发庇护。", type = SkillType.Active, cost = 2, position = new Vector2(-240, -495), parentId = "holy_shield_2", replacesSkillId = "holy_shield_2", requiredPossessedSkillId = "holy_shield" },
        new SkillDefinition { id = "holy_light", title = "圣光打击", description = "消耗四点E.G.O发动，唤来金白色的圣光，以自身为中心降下一次半径三的光击，攻击力为四。圣光持续两秒后，向四方逐次降下三轮小光柱，每根半径一、攻击力为二。光击不会伤及施术者。", type = SkillType.Active, cost = 4, position = new Vector2(-90, -330), requiredPossessedSkillId = "holy_light" },
        new SkillDefinition { id = "holy_light_2", title = "圣光打击Ⅱ", description = "通过强化，使圣光的余辉向更远处延伸，向四方扩散的小光柱增加一轮，共四轮。仍消耗四点E.G.O发动。", type = SkillType.Active, cost = 2, position = new Vector2(100, -330), parentId = "holy_light", replacesSkillId = "holy_light", requiredPossessedSkillId = "holy_light" },
        new SkillDefinition { id = "holy_light_3", title = "圣光打击Ⅲ", description = "进一步强化祈祷，使每次光击的攻击力增加一点。中央光柱攻击力为五，四轮小光柱攻击力为三。仍消耗四点E.G.O发动，圣光不会伤及施术者。", type = SkillType.Active, cost = 2, position = new Vector2(280, -330), parentId = "holy_light_2", replacesSkillId = "holy_light_2", requiredPossessedSkillId = "holy_light" },
        new SkillDefinition { id = "builder", title = "建造者", description = "消耗三点E.G.O发动，以魔力构筑一座炮台，部署在面前并与自身连接，自动迎击视野中对你怀有敌意的角色。使用者死亡时，炮台也会随之崩解。", type = SkillType.Active, cost = 4, position = new Vector2(-540, -330), requiredPossessedSkillId = "builder" },
        new SkillDefinition { id = "builder_2", title = "建造者Ⅱ", description = "通过强化，改善炮台的魔力结构，使构筑时消耗的E.G.O减少一点。消耗两点E.G.O发动，使用者死亡时炮台仍会随之崩解。", type = SkillType.Active, cost = 2, position = new Vector2(-390, -330), parentId = "builder", replacesSkillId = "builder", requiredPossessedSkillId = "builder" },
        new SkillDefinition { id = "craftsmanship_1", title = "技巧强化", description = "经由强化，掌握更加精密的构造与操作方式，永久提升角色的一点技巧。", type = SkillType.Passive, cost = 4, skillBonus = 1, position = new Vector2(-240, -330), parentId = "builder_2" },
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
        new SkillDefinition { id = "soul_mark", title = "灵魂标记", description = "使用时为一个可以附身的角色施加标记，施加标记后再次使用会将当前角色转化为一个炸弹，同时幽灵会转移到之前施加标记的角色身上\n（注意：施加标记会被视为敌对行为；切换场景时标记会清除）", type = SkillType.Active, cost = 4, position = new Vector2(100, 165), parentId = "health_1" },
        new SkillDefinition { id = "soul_mark_2", title = "灵魂标记Ⅱ", description = "强化后的灵魂标记，增强了自爆时的爆炸威力与范围。", type = SkillType.Active, cost = 2, position = new Vector2(280, 165), parentId = "soul_mark", replacesSkillId = "soul_mark" },
        new SkillDefinition { id = "soul_mark_3", title = "灵魂标记Ⅲ", description = "进一步强化后的灵魂标记，降低了使用时消耗的E.G.O量。", type = SkillType.Active, cost = 2, position = new Vector2(460, 165), parentId = "soul_mark_2", replacesSkillId = "soul_mark_2" },
        new SkillDefinition { id = "ghost_form", title = "幽灵形态", description = "消耗两点E.G.O发动，发动后进入幽灵形态，将会变得无法被察觉，同时也无法与物品进行互动。", type = SkillType.Active, cost = 4, position = new Vector2(100, 0), parentId = "health_1" },
        new SkillDefinition { id = "ghost_form_2", title = "幽灵形态Ⅱ", description = "通过强化，增加幽灵形态持续时间", type = SkillType.Active, cost = 2, position = new Vector2(280, 0), parentId = "ghost_form", replacesSkillId = "ghost_form" },
        new SkillDefinition { id = "ghost_form_3", title = "幽灵形态Ⅲ", description = "通过强化，降低了使用时消耗的E.G.O", type = SkillType.Active, cost = 2, position = new Vector2(460, 0), parentId = "ghost_form_2", replacesSkillId = "ghost_form_2" },
        new SkillDefinition { id = "domino", title = "骨牌", description = "消耗两点E.G.O发动，选中最多两名角色，将他们与当前控制的角色连接起来，共同承受所有的伤害。某位角色死亡不会使其他角色直接死亡；当前控制的角色因切换角色而死亡时，会向其他链接角色传导等同于其死亡前剩余生命值的伤害。", type = SkillType.Active, cost = 3, position = new Vector2(100, -165), parentId = "health_1" },
        new SkillDefinition { id = "domino_2", title = "骨牌Ⅱ", description = "通过强化，可以连接更多的角色", type = SkillType.Active, cost = 2, position = new Vector2(280, -165), parentId = "domino", replacesSkillId = "domino" },
        new SkillDefinition { id = "domino_3", title = "骨牌Ⅲ", description = "通过强化，增大了可以影响的范围", type = SkillType.Active, cost = 2, position = new Vector2(460, -165), parentId = "domino_2", replacesSkillId = "domino_2" }
    };

    public static bool IsCombatExpertiseSkill(string id) => id == "combat_expertise" || id == "combat_expertise_2";
    public static bool IsBuilderSkill(string id) => id == "builder" || id == "builder_2";
    public static bool IsHolyLightSkill(string id) => id == "holy_light" || id == "holy_light_2" || id == "holy_light_3";
    public static bool IsHolyShieldSkill(string id) => id == "holy_shield" || id == "holy_shield_2" || id == "holy_shield_3";
    public static bool IsSilenceOathSkill(string id) => id == "silence_oath" || id == "silence_oath_2" || id == "silence_oath_3";
    public static bool IsMeteorShowerSkill(string id) => id == "meteor_shower" || id == "meteor_shower_2" || id == "meteor_shower_3";
    public static bool IsFearRoarSkill(string id) => id == "fear_roar" || id == "fear_roar_2" || id == "fear_roar_3";
    public static bool IsRoyalCommandSkill(string id) => id == "royal_command" || id == "royal_command_2";
    public int RoyalCommandLevel => HasSkill("royal_command_2") ? 2 : HasSkill("royal_command") ? 1 : 0;
    public int FearRoarLevel => HasSkill("fear_roar_3") ? 3 : HasSkill("fear_roar_2") ? 2 : HasSkill("fear_roar") ? 1 : 0;
    public bool HasSkill(string id) => unlockedSkills.Contains(id);
    public bool IsSkillDiscovered(SkillDefinition skill) => skill != null &&
        (string.IsNullOrEmpty(skill.requiredPossessedSkillId) || HasSkill(skill.id) ||
            HasSkill(skill.requiredPossessedSkillId) || discoveredCharacterSkills.Contains(skill.requiredPossessedSkillId));

    public void RecordPossessedCharacter(ZeldaCharacterData character)
    {
        LevelRunStatistics.RecordBody(character);
        string id = character != null ? character.NativeCharacterSkillId : null;
        if (string.IsNullOrEmpty(id) || discoveredCharacterSkills.Contains(id)) return;
        discoveredCharacterSkills.Add(id);
        AttributesChanged?.Invoke();
        GameSaveSystem.RequestAutoSave("character_skill_discovered:" + id, true);
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
        GameSaveSystem.RequestAutoSave("skill_upgraded:" + id, true);
        return true;
    }

    public static PlayerGrowthAttributes Instance { get; private set; }
    public static event Action InstanceChanged;

    /// <summary>Support directly launched test/level scenes without a growth bootstrap.</summary>
    public static PlayerGrowthAttributes GetOrCreate()
    {
        if (Instance != null) return Instance;
        return new GameObject("Player Growth Attributes").AddComponent<PlayerGrowthAttributes>();
    }

    public int AttackPower
    {
        get
        {
            int value = attackPower;
            foreach (var skill in Skills) if (HasSkill(skill.id)) value += skill.attackBonus;
            return value;
        }
    }
    public int SkillValue
    {
        get { int value = skillValue; foreach (var skill in Skills) if (HasSkill(skill.id)) value += skill.skillBonus; return value; }
    }
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

    // Unlike growth energy spent on an individual body, this temporary reserve follows
    // the player through possession and is serialized independently of base attributes.
    private float potionEnergyExpiresAt;
    private float areaPotionEnergyExpiresAt, areaPotionEnergyFrozenRemaining;
    private bool areaPotionEnergyFrozen;
    private string areaPotionEnergyToken;
    private CharacterPotionBuffs areaPotionEnergyOwner;
    private int potionEnergyCurrent;
    private bool potionEnergyWasActive;
    private float OrdinaryPotionEnergyRemaining => Mathf.Max(0f, potionEnergyExpiresAt - Time.time);
    private float AreaPotionEnergyRemaining => areaPotionEnergyFrozen
        ? areaPotionEnergyFrozenRemaining : Mathf.Max(0f, areaPotionEnergyExpiresAt - Time.time);
    public float PotionEnergyRemaining => Mathf.Max(OrdinaryPotionEnergyRemaining, AreaPotionEnergyRemaining);
    public int PotionEnergyMaximum => PotionEnergyRemaining > 0f ? 2 : 0;
    public int PotionEnergyCurrent => PotionEnergyMaximum > 0 ? potionEnergyCurrent : 0;
    public float PotionEnergyDisplayRemaining(out float duration)
    {
        bool displayArea = AreaPotionEnergyRemaining > 0f &&
            (areaPotionEnergyFrozen || AreaPotionEnergyRemaining >= OrdinaryPotionEnergyRemaining);
        duration = AlchemyRecipeCatalog.GetDuration(AlchemyPotionKind.Spirit) * (displayArea ? 2f : 1f);
        return displayArea ? AreaPotionEnergyRemaining : OrdinaryPotionEnergyRemaining;
    }
    public void RefreshPotionEnergy(float duration)
    {
        if (PotionEnergyMaximum == 0) potionEnergyCurrent = 0;
        potionEnergyExpiresAt = Time.time + Mathf.Clamp(duration, 0f, 30f);
        potionEnergyWasActive = PotionEnergyMaximum > 0;
        AttributesChanged?.Invoke();
    }
    public void RefreshAreaPotionEnergy(string token, CharacterPotionBuffs owner, float duration, bool notify = true)
    {
        if (string.IsNullOrEmpty(token) || owner == null) return;
        if (PotionEnergyMaximum == 0) potionEnergyCurrent = 0;
        areaPotionEnergyToken = token;
        areaPotionEnergyOwner = owner;
        areaPotionEnergyFrozenRemaining = SafeAreaEnergyDuration(duration);
        areaPotionEnergyExpiresAt = Time.time + areaPotionEnergyFrozenRemaining;
        areaPotionEnergyFrozen = false;
        potionEnergyWasActive = PotionEnergyMaximum > 0;
        if (notify) AttributesChanged?.Invoke();
    }
    public void SynchronizeAreaPotionEnergy(string token, CharacterPotionBuffs owner, float remaining, bool frozen)
    {
        if (string.IsNullOrEmpty(token) || token != areaPotionEnergyToken) return;
        bool wasActive = potionEnergyWasActive;
        areaPotionEnergyOwner = owner;
        areaPotionEnergyFrozenRemaining = SafeAreaEnergyDuration(remaining);
        areaPotionEnergyExpiresAt = Time.time + areaPotionEnergyFrozenRemaining;
        areaPotionEnergyFrozen = frozen && areaPotionEnergyFrozenRemaining > 0f;
        potionEnergyWasActive = PotionEnergyMaximum > 0;
        if (wasActive != potionEnergyWasActive)
        {
            if (!potionEnergyWasActive) potionEnergyCurrent = 0;
            AttributesChanged?.Invoke();
        }
    }
    public void ClearAreaPotionEnergy(string token = null, bool notify = true)
    {
        if (token != null && token != areaPotionEnergyToken) return;
        bool wasActive = potionEnergyWasActive;
        areaPotionEnergyToken = null;
        areaPotionEnergyOwner = null;
        areaPotionEnergyFrozen = false;
        areaPotionEnergyExpiresAt = areaPotionEnergyFrozenRemaining = 0f;
        potionEnergyWasActive = PotionEnergyMaximum > 0;
        if (!potionEnergyWasActive) potionEnergyCurrent = 0;
        if (notify && wasActive != potionEnergyWasActive) AttributesChanged?.Invoke();
    }
    public void NotifyPotionEnergyChanged() => AttributesChanged?.Invoke();
    private static float SafeAreaEnergyDuration(float value) => float.IsNaN(value) || float.IsInfinity(value)
        ? 0f : Mathf.Clamp(value, 0f, AlchemyRecipeCatalog.GetDuration(AlchemyPotionKind.Spirit) * 2f);
    public int RestorePotionEnergy(int amount)
    {
        int restored = Mathf.Min(Mathf.Max(0, amount), PotionEnergyMaximum - PotionEnergyCurrent);
        if (restored <= 0) return 0;
        potionEnergyCurrent += restored;
        AttributesChanged?.Invoke();
        return restored;
    }
    public int SpendPotionEnergy(int amount)
    {
        int spent = Mathf.Min(Mathf.Max(0, amount), PotionEnergyCurrent);
        if (spent <= 0) return 0;
        potionEnergyCurrent -= spent;
        AttributesChanged?.Invoke();
        return spent;
    }
    private void Update()
    {
        if (CharacterPotionBuffs.IsRestoringWorld) return;
        // A destroyed owner cannot leave a permanently frozen player reserve.
        // Streaming-disabled owners remain valid references and keep their freeze.
        if (areaPotionEnergyFrozen && (areaPotionEnergyOwner == null ||
            !areaPotionEnergyOwner.OwnsAreaPotionEnergy(areaPotionEnergyToken)))
        {
            areaPotionEnergyExpiresAt = Time.time + areaPotionEnergyFrozenRemaining;
            areaPotionEnergyFrozen = false;
        }
        if (!potionEnergyWasActive || PotionEnergyMaximum > 0) return;
        potionEnergyWasActive = false;
        potionEnergyCurrent = 0;
        AttributesChanged?.Invoke();
    }

    public event Action AttributesChanged;

    internal bool GrantExhibitionLoadout()
    {
        if (!ExhibitionEdition.IsEnabled || exhibitionLoadoutGranted) return false;
        // One grant per campaign, saved with growth. Loading or changing floors
        // must not replenish spent DATA or reapply the passive health increase.
        exhibitionLoadoutGranted = true;
        foreach (string id in new[] { "health_1", "soul_mark", "soul_mark_2", "domino" })
            if (!unlockedSkills.Contains(id)) unlockedSkills.Add(id);
        collectibleCount = 3;
        if (equippedSkills == null || equippedSkills.Length != 5) Array.Resize(ref equippedSkills, 5);
        foreach (string id in new[] { "soul_mark_2", "domino" })
        {
            bool equipped = false;
            for (int i = 0; i < equippedSkills.Length; i++)
                if (GetEquippedSkill(i) == id) { equipped = true; break; }
            if (!equipped)
                for (int i = 0; i < equippedSkills.Length; i++)
                    if (string.IsNullOrEmpty(equippedSkills[i])) { equippedSkills[i] = id; break; }
        }
        ZeldaRuntimeRegistry.GetControlledMover()?.CharacterData?.ApplyPlayerHealthUpgrade();
        AttributesChanged?.Invoke();
        return true;
    }

    public RuntimeState CaptureRuntimeState()
    {
        return new RuntimeState
        {
            attackPower = attackPower,
            skillValue = skillValue,
            possessionEnergy = possessionEnergy,
            collectibleCount = collectibleCount,
            exhibitionLoadoutGranted = exhibitionLoadoutGranted,
            unlockedSkills = unlockedSkills.ToArray(),
            discoveredCharacterSkills = discoveredCharacterSkills.ToArray(),
            potionEnergyRemaining = Mathf.Max(0f, potionEnergyExpiresAt - Time.time),
            potionEnergyCurrent = PotionEnergyCurrent,
            areaPotionEnergyRemaining = AreaPotionEnergyRemaining,
            areaPotionEnergyFrozen = areaPotionEnergyFrozen,
            areaPotionEnergyToken = areaPotionEnergyToken,
            equippedSkills = equippedSkills != null ? (string[])equippedSkills.Clone() : new string[5]
        };
    }

    public void ApplyRuntimeState(RuntimeState state)
    {
        attackPower = Mathf.Max(0, state.attackPower);
        skillValue = Mathf.Max(0, state.skillValue);
        possessionEnergy = Mathf.Max(0, state.possessionEnergy);
        float potionRemaining = float.IsNaN(state.potionEnergyRemaining) || float.IsInfinity(state.potionEnergyRemaining)
            ? 0f : Mathf.Clamp(state.potionEnergyRemaining, 0f, 30f);
        potionEnergyExpiresAt = Time.time + potionRemaining;
        areaPotionEnergyToken = state.areaPotionEnergyToken;
        areaPotionEnergyFrozenRemaining = !string.IsNullOrEmpty(areaPotionEnergyToken)
            ? SafeAreaEnergyDuration(state.areaPotionEnergyRemaining) : 0f;
        areaPotionEnergyExpiresAt = Time.time + areaPotionEnergyFrozenRemaining;
        areaPotionEnergyOwner = null;
        // CharacterPotionBuffs rebinds the saved grant token and real overlap;
        // loading must not manufacture an immortal freeze with no valid source.
        areaPotionEnergyFrozen = false;
        potionEnergyCurrent = PotionEnergyRemaining > 0f ? Mathf.Clamp(state.potionEnergyCurrent, 0, 2) : 0;
        potionEnergyWasActive = PotionEnergyRemaining > 0f;
        collectibleCount = Mathf.Max(0, state.collectibleCount);
        exhibitionLoadoutGranted = state.exhibitionLoadoutGranted;
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
        if (persistBetweenScenes)
        {
            // Level scenes may group PlayerStates under SceneAssets. Unity only
            // preserves root objects; detach this service, not the whole scene group.
            if (transform.parent != null) transform.SetParent(null, true);
            DontDestroyOnLoad(gameObject);
        }
        InstanceChanged?.Invoke();
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
