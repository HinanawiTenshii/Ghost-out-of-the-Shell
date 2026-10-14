using UnityEngine;

public enum AlchemyPotionKind { Vitality, Spirit, Death, Might, Veil, Berserk, Sight, Weakness, Clamor, Echo }

/// <summary>Order-independent three-ingredient recipes shared by brewing, items and buffs.</summary>
public static class AlchemyRecipeCatalog
{
    public const string RedHerbId = "red_herb_powder", GreenHerbId = "green_herb_powder", BlueHerbId = "blue_herb_powder";
    public static readonly Color UnbrewedColor = new Color(.24f,.22f,.18f,1f);
    public static bool IsIngredient(string id) => id == RedHerbId || id == GreenHerbId || id == BlueHerbId;
    public static bool IsStackableSupply(string id) => IsIngredient(id) || id == GlassBottlePickupItem.DefaultItemId;
    public static AlchemyPotionKind Resolve(int red, int green, int blue)
    {
        if (red < 0 || green < 0 || blue < 0 || red + green + blue != 3)
            throw new System.ArgumentException("A recipe requires exactly three red, green or blue herbs.");
        if (red == 3) return AlchemyPotionKind.Vitality;
        if (blue == 3) return AlchemyPotionKind.Spirit;
        if (green == 3) return AlchemyPotionKind.Death;
        if (red == 2) return blue == 1 ? AlchemyPotionKind.Might : AlchemyPotionKind.Berserk;
        if (blue == 2) return red == 1 ? AlchemyPotionKind.Veil : AlchemyPotionKind.Sight;
        if (green == 2) return red == 1 ? AlchemyPotionKind.Weakness : AlchemyPotionKind.Clamor;
        return AlchemyPotionKind.Echo;
    }
    public static Vector3Int GetIngredients(AlchemyPotionKind kind)
    {
        switch(kind) {
            case AlchemyPotionKind.Vitality: return new Vector3Int(3,0,0);
            case AlchemyPotionKind.Spirit: return new Vector3Int(0,0,3);
            case AlchemyPotionKind.Death: return new Vector3Int(0,3,0);
            case AlchemyPotionKind.Might: return new Vector3Int(2,0,1);
            case AlchemyPotionKind.Veil: return new Vector3Int(1,0,2);
            case AlchemyPotionKind.Berserk: return new Vector3Int(2,1,0);
            case AlchemyPotionKind.Sight: return new Vector3Int(0,1,2);
            case AlchemyPotionKind.Weakness: return new Vector3Int(1,2,0);
            case AlchemyPotionKind.Clamor: return new Vector3Int(0,2,1);
            default: return Vector3Int.one;
        }
    }
    public static Color GetColor(AlchemyPotionKind kind)
    {
        var n = GetIngredients(kind);
        // Match the three powder palettes. Equal quantities become pearl gray;
        // empty cauldrons use a deliberately much darker neutral base.
        Color color = (new Color(.91f,.20f,.23f) * n.x + new Color(.22f,.79f,.35f) * n.y + new Color(.23f,.49f,.95f) * n.z) / 3f;
        color.a = 1f; return color;
    }
    public static float GetDuration(AlchemyPotionKind kind)
    {
        switch(kind) {
            case AlchemyPotionKind.Death: return 0;
            case AlchemyPotionKind.Veil: case AlchemyPotionKind.Clamor: return 15f;
            case AlchemyPotionKind.Berserk: return 22.5f;
            case AlchemyPotionKind.Echo: return 120f;
            default: return 30f;
        }
    }
    public static string GetItemId(AlchemyPotionKind kind) => "alchemy_potion_" + kind.ToString().ToLowerInvariant();
    public static string GetName(AlchemyPotionKind kind)
    {
        switch(kind) {
            case AlchemyPotionKind.Vitality: return "生命药水";
            case AlchemyPotionKind.Spirit: return "灵能药水";
            case AlchemyPotionKind.Death: return "剧毒药水";
            case AlchemyPotionKind.Might: return "巨力药水";
            case AlchemyPotionKind.Veil: return "幻隐药水";
            case AlchemyPotionKind.Berserk: return "狂战药水";
            case AlchemyPotionKind.Sight: return "灵视药水";
            case AlchemyPotionKind.Weakness: return "虚弱药水";
            case AlchemyPotionKind.Clamor: return "喧嚣药水";
            default: return "回响药水";
        }
    }
    // Old saves keep a copy of the original generated text. Only migrate exact
    // defaults for display; authored instance descriptions and saved data stay intact.
    public static string ResolveInventoryDescription(AlchemyPotionKind kind, string stored, string currentDefault)
    {
        if (string.IsNullOrWhiteSpace(stored)) return currentDefault;
        string legacyEffect;
        switch (kind)
        {
            case AlchemyPotionKind.Berserk: legacyEffect = "攻击力增加2点，攻击间隔缩短35%，普通近战可攻击前方多个目标；移动速度降低20%，持续22.5秒。"; break;
            case AlchemyPotionKind.Clamor: legacyEffect = "每3秒发出魔力噪声，吸引8单位内的普通角色前来调查，持续15秒。"; break;
            case AlchemyPotionKind.Death: legacyEffect = "饮用后当前角色立即死亡。"; break;
            case AlchemyPotionKind.Echo: legacyEffect = "记录位置和生命值，120秒内首次受到致命伤害时返回记录位置、恢复记录生命值并获得1秒伤害免疫。回溯时，当前场景中对玩家处于警告或敌对状态的角色会转为返回状态。主动切换附身不会触发，离开当前场景失效。"; break;
            case AlchemyPotionKind.Might: legacyEffect = "力量增加3点，普通近战可击退目标，持续30秒。"; break;
            case AlchemyPotionKind.Sight: legacyEffect = "视野范围增加25%，持续30秒。AI控制时拥有360度视野；玩家控制时可看见墙后角色和机关轮廓，但不能隔墙攻击、附身或交互。"; break;
            case AlchemyPotionKind.Spirit: legacyEffect = "恢复4点附身能量；满能量时，附身能量上限临时增加2点，持续30秒。"; break;
            case AlchemyPotionKind.Veil: legacyEffect = "移动速度提高40%，不会被普通角色的视野发现，持续15秒。攻击或使用其他道具会解除；无法绕过碰撞和电门。"; break;
            case AlchemyPotionKind.Vitality: legacyEffect = "恢复6点生命值；满生命值时，生命值上限临时增加3点，持续30秒。"; break;
            case AlchemyPotionKind.Weakness: legacyEffect = "生命值上限降低3点（最低1点），最终攻击力固定为1，持续30秒。"; break;
            default: return stored;
        }
        string prefix = "以三份草药炼制的" + GetName(kind) + "。按R饮用，饮用后返还一个玻璃瓶。";
        const string suffix = "同名临时效果不叠加，再次饮用刷新时长。";
        bool wasDefault = stored == prefix + legacyEffect + suffix;
        // The Might resource and its old catalog differed on fear immunity.
        if (kind == AlchemyPotionKind.Might)
            wasDefault |= stored == prefix + "力量增加3点，免疫恐惧，普通近战可击退目标，持续30秒。" + suffix;
        return wasDefault && !string.IsNullOrWhiteSpace(currentDefault) ? currentDefault : stored;
    }

    public static string GetDescription(AlchemyPotionKind kind)
    {
        string effect;
        switch(kind) {
            case AlchemyPotionKind.Vitality: effect="蕴含旺盛生机的药水，能够修复受伤的身体。使用后恢复6点生命值，满生命值时使用则使生命值上限增加3点，持续30秒。"; break;
            case AlchemyPotionKind.Spirit: effect="凝聚着灵性能量的药水，能够补充维系附身所需的力量。使用后恢复4点附身能量，满能量时使用则使附身能量上限增加2点，持续30秒。"; break;
            case AlchemyPotionKind.Death: effect="一瓶颜色鲜亮的药水，草药中的毒性已被彻底提炼出来。饮用后当前身体会立即死亡。注意，即使回响也无法挽救饮下它的人。"; break;
            case AlchemyPotionKind.Might: effect="能够激发肉体潜力的药水，饮用后力量大增，也不再受恐惧支配。力量增加3点，普通近战攻击可以击退体型不大的目标，持续30秒。"; break;
            case AlchemyPotionKind.Veil: effect="让身形变得半透明的药水，饮用后行动更加轻快，普通人的目光难以捕捉。移动速度提高40%，持续15秒。攻击或使用其他道具会使身形显露。注意，它骗不过骷髅与武装魔像，也无法穿过障碍或绕过电门。"; break;
            case AlchemyPotionKind.Berserk: effect="让饮用者陷入狂战状态的药水，挥出的武器能够横扫前方，但脚步也会变得沉重。力量增加2点，攻击间隔缩短35%，普通近战可同时攻击多个目标；移动速度降低20%，持续22.5秒。"; break;
            case AlchemyPotionKind.Sight: effect="能够拓展感知的药水，让原本难以察觉的事物显露轮廓。视野范围增加25%，持续30秒。受你控制时可以窥见范围内墙后角色与机关的轮廓；由角色自行行动时则能察觉四周的动静。注意，看见墙后的目标并不意味着能够隔墙攻击、附身或交互。"; break;
            case AlchemyPotionKind.Weakness: effect="侵蚀筋骨的药水，饮用后即使紧握武器也难以使出力气。生命值上限降低3点，最低保留1点，最终力量变为1，持续30秒。"; break;
            case AlchemyPotionKind.Clamor: effect="封存着躁动魔力的药水，饮用后身体会不断向外发出异响。每3秒产生一次魔力噪声，吸引8单位范围内尚未进入敌对状态的普通角色前来调查，持续15秒。注意，骷髅与武装魔像不会被这阵声音引开。"; break;
            default: effect="留住饮用瞬间的药水，记录位置与生命值。120秒内首次受到致命伤害时，尝试回到原处附近的安全位置，恢复记录的生命值并免伤1秒。你回溯时，当前场景中对你警告或敌对的角色会暂时放弃追击并返回。再次饮用会重新记录。注意，切换附身与剧毒不会触发，离开当前场景后失效。"; break;
        }
        return effect + "按R饮用，饮用后返还一个玻璃瓶。";
    }
}
