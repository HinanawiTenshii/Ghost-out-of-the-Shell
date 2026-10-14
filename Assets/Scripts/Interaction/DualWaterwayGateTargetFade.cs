using UnityEngine;

/// <summary>Shared downstream water: hide only when both supplying gates are fully closed.</summary>
[AddComponentMenu("Interaction/Dual Waterway Gate Target Fade")]
public sealed class DualWaterwayGateTargetFade : WaterwayGateTargetFade
{
    [SerializeField, Tooltip("另一座供水水闸。两座闸门的开启/关闭完成事件均绑定此组件的 FadeIn/FadeOut。只有两座都关闭时水流才隐藏。")]
    private WaterwayGate otherGate;

    // Both controllers must wait for this shared water's fade to finish.
    protected override bool IsControlledBy(WaterwayGate gate)
    {
        return base.IsControlledBy(gate) || otherGate == gate;
    }

    protected override void Start()
    {
        if (SourceGate != null || otherGate != null) SetVisibleImmediately(ShouldShowWater());
    }

    // Gate completion UnityEvents also reach inactive objects, allowing a hidden
    // target to wake up without polling gates or adding a scene-wide manager.
    public override void FadeIn() => RefreshFromGates();
    public override void FadeOut() => RefreshFromGates();

    private void RefreshFromGates()
    {
        if (ShouldShowWater()) base.FadeIn();
        else base.FadeOut();
    }

    private bool ShouldShowWater()
    {
        if (SourceGate == null && otherGate == null) return true;
        return SuppliesWater(SourceGate) || SuppliesWater(otherGate);
    }

    private static bool SuppliesWater(WaterwayGate gate)
    {
        // A closing gate still supplies water until its last animation frame.
        // An opening request wins over the other gate's late closing event.
        return gate != null && (gate.IsOpenRequested || !gate.IsFullyClosed);
    }
}
