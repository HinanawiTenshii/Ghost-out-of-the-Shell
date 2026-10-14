using UnityEngine;

[AddComponentMenu("Cogitans/Characters/Native Skills/Holy Light Strike")]
public sealed class HolyLightCharacterSkillData : NativeCharacterSkillData
{
    public override string SkillId => "holy_light";
    public override string SkillName => "圣光打击";
    public override Sprite Icon => GetIcon(0);
    public override bool TryUse(ZeldaCharacterData owner) => HolyLightSkillRuntime.TryUse(owner);

    private static readonly Sprite[] icons = new Sprite[3];
    public static Sprite GetIcon(int rank)
    {
        rank = Mathf.Clamp(rank, 0, 2);
        if (icons[rank] != null) return icons[rank];
        var texture = new Texture2D(22, 22, TextureFormat.RGBA32, false) {
            name = "Holy Light Strike Glyph", filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        for (int y = 0; y < 22; y++) for (int x = 0; x < 22; x++)
        {
            // Falling shaft, open impact ring, and a solid person against the shaft's negative space.
            bool beam = (x == 6 || x == 13) && y >= 8 && y <= 20 ||
                (x == 9 || x == 10) && y >= 15 && y <= 20;
            bool person = x >= 9 && x <= 10 && y >= 10 && y <= 12 ||
                x >= 8 && x <= 11 && y >= 6 && y <= 8 ||
                (x == 8 || x == 11) && y >= 3 && y <= 5;
            bool impact = y == 2 && x >= 4 && x <= 15 ||
                y == 3 && (x == 3 || x == 16) || y == 4 && (x == 2 || x == 17) ||
                y == 5 && (x == 3 || x == 16) || y == 6 && (x >= 4 && x <= 5 || x >= 14 && x <= 15);
            bool numeral = rank > 0 && y >= 0 && y <= 4 && x >= 18 &&
                (y == 0 || y == 4 || (rank == 1 ? x == 19 : x == 18 || x == 20));
            texture.SetPixel(x, y, beam || person || impact || numeral ? Color.white : Color.clear);
        }
        texture.Apply(false, true);
        var sprite = Sprite.Create(texture, new Rect(0, 0, 22, 22), Vector2.one * .5f, 22);
        sprite.name = texture.name; sprite.hideFlags = HideFlags.HideAndDontSave;
        return icons[rank] = sprite;
    }
}
