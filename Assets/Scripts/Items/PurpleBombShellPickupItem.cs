/// <summary>A unique purple bomb shell intended for item submission.</summary>
public sealed class PurpleBombShellPickupItem : PickupItemBase
{
    public override bool CanBeStoredInCardboardBox(ZeldaCharacterData user) => false;

    protected override bool ApplyUseEffect(ZeldaCharacterData user)
    {
        // Submission materials cannot be consumed accidentally with R.
        return false;
    }
}
