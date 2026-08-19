public sealed class KeyPickupItemVisual : PickupItemVisualBase
{
    protected override string RuntimeSpriteName => "Runtime Pixel Key";

    protected override string[] GetPixelRows()
    {
        return new[]
        {
            ".###.........",
            "#...#........",
            "#...#########",
            "#...#.....#..",
            ".###......###"
        };
    }
}
