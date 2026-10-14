using UnityEngine;

[AddComponentMenu("Cogitans/Characters/Native Skills/Builder")]
public sealed class BuilderCharacterSkillData : NativeCharacterSkillData
{
    public override string SkillId => "builder";
    public override string SkillName => "建造者";
    public override Sprite Icon => GetIcon(0);
    public override bool TryUse(ZeldaCharacterData owner) => BuilderSkillRuntime.TryUse(owner);
    private static readonly Sprite[] icons = new Sprite[3];
    public static Sprite GetIcon(int rank)
    {
        rank = Mathf.Clamp(rank, 0, 2);
        if (icons[rank] != null) return icons[rank];
        var texture = new Texture2D(22, 22, TextureFormat.RGBA32, false) {
            name = "Builder Turret Skill", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        // Front-facing silhouette: muzzle, broad gun housing, turntable and feet.
        for (int y = 0; y < 22; y++) for (int x = 0; x < 22; x++)
        {
            bool filled = (x >= 8 && x <= 11 && y >= 11 && y <= 18) ||
                (x >= 7 && x <= 12 && y >= 17 && y <= 19) ||
                (x >= 5 && x <= 14 && y >= 8 && y <= 12) ||
                (x >= 8 && x <= 11 && y >= 5 && y <= 7) ||
                (x >= 3 && x <= 16 && y >= 3 && y <= 5) ||
                ((x >= 3 && x <= 5 || x >= 14 && x <= 16) && y >= 1 && y <= 2);
            if (y == 18 && x >= 8 && x <= 11) filled = false;
            bool numeral = rank > 0 && y >= 1 && y <= 6 && x >= 17 && x <= 21 &&
                (y == 1 || y == 6 || (rank == 1 ? x == 19 : x == 18 || x == 20));
            texture.SetPixel(x, y, filled || numeral ? Color.white : Color.clear);
        }
        texture.Apply(false, true);
        return icons[rank] = Sprite.Create(texture, new Rect(0, 0, 22, 22), Vector2.one * .5f, 22);
    }
}
