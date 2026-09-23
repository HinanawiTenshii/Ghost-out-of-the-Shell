using UnityEngine;

/// <summary>Inventory icon only: never resize, breathe, tint or replace the character renderer.</summary>
public sealed class ArmedGolemPickupItemVisual : PickupItemVisualBase
{
    protected override bool UsesEmbeddedColors => true;
    public override Sprite InventoryIcon => GetComponent<AutomatonZeldaCharacterData>().GetInventorySprite();
    protected override void Awake() { }
    protected override void OnEnable() { }
    protected override void OnDisable() { }
    protected override void LateUpdate() { }
    public override void SetDisplayColor(Color color) { }
}
