/// <summary>A unique sky-blue gunpowder pile intended for item submission.</summary>
public sealed class SkyBlueGunpowderPickupItem : PickupItemBase
{
    protected override bool ApplyUseEffect(ZeldaCharacterData user)
    {
        // Submission materials cannot be consumed accidentally with R.
        return false;
    }
}
