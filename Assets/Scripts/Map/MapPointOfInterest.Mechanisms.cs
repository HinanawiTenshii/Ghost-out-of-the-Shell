using UnityEngine;

public sealed partial class MapPointOfInterest
{
    private bool isMechanism;
    private BoxCollider2D mechanismCollider;

    // Attach from the owning mechanism's lifecycle, covering old scenes as well
    // as newly placed prefabs without scanning the scene or rewriting assets.
    public static void AttachMechanism(Component owner)
    {
        if (!Application.isPlaying || owner == null) return;
        if (owner.GetComponent<MapPointOfInterest>() == null)
            owner.gameObject.AddComponent<MapPointOfInterest>();
    }

    private void ConfigureMechanism()
    {
        if (GetComponent<ElectricGatePillar>() != null) SetMechanism("电门", MapPointIconShape.ElectricGate);
        else if (GetComponent<AlchemyCauldron>() != null) SetMechanism("炼药锅", MapPointIconShape.Cauldron);
        else if (GetComponent<BatterySocket>() != null) SetMechanism("电池槽", MapPointIconShape.BatterySocket);
        else if (GetComponent<WaterwayGate>() is WaterwayGate gate) SetMechanism("水闸 " + gate.Identifier, MapPointIconShape.WaterwayGate);
        else if (GetComponent<WaterwayGateController>() is WaterwayGateController control)
            SetMechanism("水闸控制器 " + control.DisplayedIdentifier, MapPointIconShape.WaterwayController);
        else if (GetComponent<FortressBallista>() != null) SetMechanism("弩炮", MapPointIconShape.Ballista);
        else if (GetComponent<DoorData>() is DoorData glass && glass.IsBreakableGlass)
        {
            SetMechanism("可打破玻璃", MapPointIconShape.BreakableGlass);
            visibleOnMap = !glass.IsDestroyed;
        }
        if (isMechanism) mechanismCollider = GetComponent<BoxCollider2D>();
    }

    private void SetMechanism(string title, MapPointIconShape shape)
    {
        isMechanism = true;
        pointTitle = title; description = ""; iconShape = shape;
        iconColor = new Color32(91, 211, 241, 255); visibleOnMap = true;
    }

    public Vector3 MapWorldPosition => isMechanism ? MechanismBounds.center : transform.position;

    private Bounds MechanismBounds
    {
        get
        {
            // Collider.bounds is empty on opened sluices and distance-unloaded
            // objects. Transform authored bounds instead, including rotation/scale.
            if (mechanismCollider == null) return new Bounds(transform.position, Vector3.one * .2f);
            Vector2 size = mechanismCollider.size * .5f;
            Vector3 x = transform.TransformVector(new Vector3(size.x, 0, 0));
            Vector3 y = transform.TransformVector(new Vector3(0, size.y, 0));
            return new Bounds(transform.TransformPoint(mechanismCollider.offset),
                new Vector3(Mathf.Abs(x.x) + Mathf.Abs(y.x), Mathf.Abs(x.y) + Mathf.Abs(y.y), .01f) * 2f);
        }
    }

    private bool IsMechanismInViewport(Camera camera)
    {
        Bounds bounds = MechanismBounds;
        Vector3 a = camera.WorldToViewportPoint(bounds.min), b = camera.WorldToViewportPoint(bounds.max);
        return b.z > 0 && Mathf.Max(a.x, b.x) >= 0 && Mathf.Min(a.x, b.x) <= 1 &&
            Mathf.Max(a.y, b.y) >= 0 && Mathf.Min(a.y, b.y) <= 1;
    }

    // Called only by actual breakage, never by streaming/scene-unload callbacks.
    public void NotifyDestroyed() => SetVisibleOnMap(false);

    internal static bool IsElectricFieldVisible(ElectricGatePillar owner, Vector2 start, Vector2 end)
    {
        if (GameSaveSystem.IsLoading) return false;
        var player = ZeldaRuntimeRegistry.GetControlledMover();
        var camera = Camera.main;
        if (player == null || camera == null || ZeldaRuntimeRegistry.GetGameplayScene(player.gameObject) != owner.gameObject.scene) return false;
        var vision = camera.GetComponent<CameraCircularVision>();
        for (int i = 0; i <= 4; i++)
        {
            Vector2 p = Vector2.Lerp(start, end, i * .25f);
            Vector3 v = camera.WorldToViewportPoint(p);
            if (v.z > 0 && v.x >= 0 && v.x <= 1 && v.y >= 0 && v.y <= 1 &&
                (vision == null || !vision.isActiveAndEnabled || vision.IsWorldPositionVisible(p, owner.transform))) return true;
        }
        return false;
    }
}
