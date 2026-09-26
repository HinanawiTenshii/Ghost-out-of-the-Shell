using UnityEngine;

public sealed class BatteryPickupItem : PickupItemBase
{
    public const string DefaultItemId = "battery";
    public const string DefaultItemName = "魔力电池";
    public const string DefaultDescription = "封存着魔力的便携式能源，装入电池槽后可以驱动与其相连的机关。靠近电池槽按E安装或取回，无法直接使用。";
    public override bool CanUseFromInventory => false;
    protected override bool ApplyUseEffect(ZeldaCharacterData user) => false;
}
