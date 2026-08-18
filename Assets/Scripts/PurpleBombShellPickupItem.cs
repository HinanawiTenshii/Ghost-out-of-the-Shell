/// <summary>A unique purple bomb shell intended for item submission.</summary>
public sealed class PurpleBombShellPickupItem : PickupItemBase
{
    protected override bool ApplyUseEffect(ZeldaCharacterData user)
    {
        // Submission materials cannot be consumed accidentally with R.
        return false;
    }
}
