using System.Collections.Generic;
using UnityEngine;

/// <summary>Collection icons show the ability itself, not a purchased skill rank.</summary>
public static class LevelSettlementSkillIcons
{
    private static readonly Dictionary<string, Sprite> icons = new Dictionary<string, Sprite>();

    public static Sprite Get(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (icons.TryGetValue(id, out var cached) && cached != null) return cached;

        Sprite source;
        Rect glyph;
        // Pixel bounds of the authored, unranked glyphs. Crop transparent margins
        // so Image.preserveAspect centers the artwork, not the former rank space.
        // Reuse native textures without readback or changing other UI's sprites.
        switch (id)
        {
            case "combat_expertise":
                source = ZeldaHealthHeartsUI.GetCombatExpertiseIcon(); glyph = new Rect(1, 2, 32, 32); break;
            case "royal_command":
                source = ZeldaCharacterData.CommandIcon; glyph = new Rect(2, 4, 13, 12); break;
            case "fear_roar":
                source = FearRoarArea.Icon; glyph = new Rect(0, 4, 17, 9); break;
            case "builder":
                source = BuilderCharacterSkillData.GetIcon(0); glyph = new Rect(3, 5, 18, 11); break;
            case "holy_light":
                source = HolyLightCharacterSkillData.GetIcon(0); glyph = new Rect(2, 2, 16, 19); break;
            case "holy_shield":
                source = HolyShieldCharacterSkillData.GetIcon(0); glyph = new Rect(2, 1, 18, 20); break;
            case "meteor_shower":
                source = MeteorShowerCharacterSkillData.GetIcon(0); glyph = new Rect(2, 2, 20, 18); break;
            case "silence_oath":
                source = SilenceOathRuntime.GetIcon(0); glyph = new Rect(2, 5, 16, 16); break;
            default:
                return SkillPageArt.GetAbilitySprite(id);
        }
        var sprite = Sprite.Create(source.texture, glyph, Vector2.one * .5f,
            source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        sprite.name = "Settlement Skill " + id;
        sprite.hideFlags = HideFlags.HideAndDontSave;
        icons[id] = sprite;
        return sprite;
    }
}
