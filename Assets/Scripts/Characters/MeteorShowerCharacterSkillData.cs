using UnityEngine;

[AddComponentMenu("Cogitans/Characters/Native Skills/Meteor Shower")]
public sealed class MeteorShowerCharacterSkillData : NativeCharacterSkillData
{
    public override string SkillId => "meteor_shower";
    public override string SkillName => "流星群";
    public override Sprite Icon => GetIcon(0);
    public override bool TryUse(ZeldaCharacterData owner) => MeteorShowerSkillRuntime.TryUse(owner);
    private static readonly Sprite[] icons = new Sprite[3];

    public static Sprite GetIcon(int rank)
    {
        rank = Mathf.Clamp(rank, 0, 2);
        if (icons[rank] != null) return icons[rank];
        var texture = new Texture2D(22, 22, TextureFormat.RGBA32, false) {
            name = "Meteor Shower Glyph", filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        var pixels = new Color[22 * 22];
        // Three falling stones with stepped tails; silhouettes use the same
        // monochrome pixels and UI tint as the other native skill glyphs.
        Vector2Int[] heads = { new Vector2Int(4, 7), new Vector2Int(11, 12), new Vector2Int(14, 4) };
        foreach (var head in heads)
        {
            for (int y = -2; y <= 2; y++) for (int x = -2; x <= 2; x++)
                if (Mathf.Abs(x) + Mathf.Abs(y) <= 3)
                    pixels[(head.y + y) * 22 + head.x + x] = Color.white;
            for (int t = 2; t <= 7; t++)
                for (int w = 0; w < (t < 5 ? 2 : 1); w++)
                    pixels[(head.y + t) * 22 + head.x + t - w] = Color.white;
        }
        for (int y = 0; y < 5; y++) for (int x = 18; x < 22; x++)
            if (rank > 0 && (y == 0 || y == 4 || (rank == 1 ? x == 19 : x == 18 || x == 20)))
                pixels[y * 22 + x] = Color.white;
        texture.SetPixels(pixels); texture.Apply(false, true);
        var sprite = Sprite.Create(texture, new Rect(0, 0, 22, 22), Vector2.one * .5f, 22);
        sprite.name = texture.name; sprite.hideFlags = HideFlags.HideAndDontSave;
        return icons[rank] = sprite;
    }
}
