/// <summary>A unique fuse intended to be consumed by an item submission station.</summary>
public sealed class FusePickupItem : PickupItemBase
{
    public override bool CanBeStoredInCardboardBox(ZeldaCharacterData user) => false;

    protected override bool ApplyUseEffect(ZeldaCharacterData user)
    {
        // Submission materials cannot be consumed accidentally with R.
        return false;
    }
}
