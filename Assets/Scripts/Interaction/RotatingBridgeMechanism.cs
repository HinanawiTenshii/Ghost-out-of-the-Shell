using UnityEngine;

/// <summary>Interlocks two passage pairs with the bridge's actual hinge motion.</summary>
[DisallowMultipleComponent]
[AddComponentMenu("Interaction/Rotating Bridge Mechanism")]
public sealed class RotatingBridgeMechanism : MonoBehaviour
{
    [SerializeField] private DoorHingeInteraction bridge;
    [Header("Initially open: mechanisms 1 and 3")]
    [SerializeField] private DoubleSlidingDoor mechanism1;
    [SerializeField] private DoubleSlidingDoor mechanism3;
    [Header("Initially closed: mechanisms 2 and 4")]
    [SerializeField] private DoubleSlidingDoor mechanism2;
    [SerializeField] private DoubleSlidingDoor mechanism4;

    [Header("Safety Interlock")]
    [Min(0.01f), SerializeField] private float closeDuration = 0.25f;
    private bool closingPassages;

    public bool IsRotating => bridge != null && bridge.IsChangingOpenState;
    public DoorHingeInteraction BridgeHinge => bridge;
    // IsOpen is the requested endpoint; retain the previous layout in transit.
    public bool MapUsesRotatedLayout => bridge != null && (IsRotating ? !bridge.IsOpen : bridge.IsOpen);

    public DoubleSlidingDoor GetMapPassage(int index)
    {
        switch (index)
        {
            case 0: return mechanism1;
            case 1: return mechanism2;
            case 2: return mechanism3;
            case 3: return mechanism4;
            default: return null;
        }
    }

    private void Start()
    {
        SynchronizePassages(true);
    }

    public void ToggleFromExternal()
    {
        if (!isActiveAndEnabled || bridge == null || IsRotating || closingPassages)
            return;

        // Block the entire passage before either the leaves or bridge move.
        // Visible leaves close briefly while the invisible barrier guarantees safety.
        ClosePassagesSafely();
        bridge.ToggleFromExternal();
    }

    private void LateUpdate()
    {
        // The hinge updates in Update. Never use a timer: speed, pause and
        // persistence can all change when the bridge actually reaches its end.
        SynchronizePassages(false);
    }

    private void SynchronizePassages(bool immediate)
    {
        if (bridge == null) return;
        if (IsRotating || (closingPassages && !ArePassagesClosed()))
        {
            ClosePassagesSafely();
            return;
        }

        closingPassages = false;

        // Derive the pair from the persisted hinge state, so scene travel/load
        // also recovers the correct passages without a second saved toggle bit.
        SetPassages(!bridge.IsOpen, bridge.IsOpen, immediate);
    }

    private void ClosePassagesSafely()
    {
        closingPassages = true;
        if (mechanism1 != null) mechanism1.CloseWithSafetyBarrier(closeDuration);
        if (mechanism3 != null) mechanism3.CloseWithSafetyBarrier(closeDuration);
        if (mechanism2 != null) mechanism2.CloseWithSafetyBarrier(closeDuration);
        if (mechanism4 != null) mechanism4.CloseWithSafetyBarrier(closeDuration);
    }

    private bool ArePassagesClosed()
    {
        return (mechanism1 == null || mechanism1.IsFullyClosed)
            && (mechanism3 == null || mechanism3.IsFullyClosed)
            && (mechanism2 == null || mechanism2.IsFullyClosed)
            && (mechanism4 == null || mechanism4.IsFullyClosed);
    }

    private void SetPassages(bool firstPairOpen, bool secondPairOpen, bool immediate)
    {
        SetDoor(mechanism1, firstPairOpen, immediate);
        SetDoor(mechanism3, firstPairOpen, immediate);
        SetDoor(mechanism2, secondPairOpen, immediate);
        SetDoor(mechanism4, secondPairOpen, immediate);
    }

    private static void SetDoor(DoubleSlidingDoor door, bool open, bool immediate)
    {
        if (door == null) return;
        if (immediate)
        {
            if (door.IsOpenRequested != open || (open ? !door.IsFullyOpen : !door.IsFullyClosed))
                door.SetOpenImmediately(open);
        }
        else if (door.IsOpenRequested != open)
        {
            door.SetOpen(open);
        }
    }
}
