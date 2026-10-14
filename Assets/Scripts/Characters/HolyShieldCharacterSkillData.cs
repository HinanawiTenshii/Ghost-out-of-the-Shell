using UnityEngine;

[AddComponentMenu("Cogitans/Characters/Native Skills/Holy Shield")]
public sealed class HolyShieldCharacterSkillData : NativeCharacterSkillData
{
    public override string SkillId => "holy_shield";
    public override string SkillName => "圣盾";
    public override Sprite Icon => GetIcon(0);
    public override bool TryUse(ZeldaCharacterData owner) => HolyShieldRuntime.TryUse(owner);
    private static readonly Sprite[] icons = new Sprite[3];
    private static Sprite hudIcon;
    private static readonly string[] Shield = {
        ".#######.", "##.....##", "#...#...#", "#..###..#", "#...#...#",
        ".#.....#.", ".#.....#.", "..#...#..", "...#.#...", "....#...."
    };
    public static Sprite HudIcon => hudIcon != null ? hudIcon : (hudIcon = Create(-1));
    public static Sprite GetIcon(int rank)
    {
        rank = Mathf.Clamp(rank, 0, 2);
        return icons[rank] != null ? icons[rank] : (icons[rank] = Create(rank));
    }
    private static Sprite Create(int rank)
    {
        bool hud = rank < 0; int width = hud ? 9 : 22, height = hud ? 10 : 22;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) {
            name = "Holy Shield Glyph", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave };
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
        {
            int px = hud ? x : (x - 2) / 2, py = hud ? y : (y - 1) / 2;
            bool filled = (hud || x >= 2 && y >= 1) && px >= 0 && px < 9 && py >= 0 && py < 10 && Shield[9 - py][px] == '#';
            bool numeral = rank > 0 && y >= 0 && y <= 5 && x >= 17 && x <= 21 &&
                (y == 0 || y == 5 || (rank == 1 ? x == 19 : x == 18 || x == 20));
            texture.SetPixel(x, y, filled || numeral ? Color.white : Color.clear);
        }
        texture.Apply(false, true);
        var sprite = Sprite.Create(texture, new Rect(0, 0, width, height), Vector2.one * .5f, height);
        sprite.name = texture.name; sprite.hideFlags = HideFlags.HideAndDontSave; return sprite;
    }
}
