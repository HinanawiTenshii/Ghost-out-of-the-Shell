using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Collects E/F candidates and interaction requests, selects one target,
/// and allows dialogue E and possession F to coexist on the same NPC. Selection is
/// deterministic: objects must be in front of the controlled character and
/// the nearest qualifying E or F candidate wins.
/// </summary>
public static class ZeldaInteractionArbiter
{
    private struct Request
    {
        public Component owner;
        public ZeldaFourWayMover mover;
        public KeyCode key;
        public Vector2 position;
        public Action action;
        public int frame;
    }

    private struct Offer
    {
        public Component owner;
        public ZeldaFourWayMover mover;
        public KeyCode key;
        public Vector2 position;
        public Action<bool> setPromptVisible;
        public int frame;
    }

    private static readonly List<Request> Requests = new List<Request>(16);
    private static readonly List<Offer> Offers = new List<Offer>(24);
    private static ZeldaInteractionArbiterRunner runner;
    private static Component selectedOwner;
    private static ZeldaFourWayMover selectedMover;
    private static KeyCode selectedKey;
    private static Component companionOwner;
    private static ZeldaFourWayMover companionMover;
    private static KeyCode companionKey;
    private static bool IsGhostFormInteraction(ZeldaFourWayMover mover, KeyCode key)
    {
        return key != KeyCode.F && mover != null && mover.GetComponent<ZeldaCharacterData>() != null && mover.GetComponent<ZeldaCharacterData>().IsGhostForm;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Requests.Clear();
        Offers.Clear();
        runner = null;
        selectedOwner = null;
        selectedMover = null;
        companionOwner = null;
        companionMover = null;
        companionKey = KeyCode.None;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        EnsureRunner();
    }

    public static void Submit(
        Component owner,
        ZeldaFourWayMover mover,
        KeyCode key,
        Vector2 interactionPosition,
        Action action)
    {
        if (IsGhostFormInteraction(mover, key) || ClockworkPuppetRuntime.BlocksCharacterInput ||
            owner == null || mover == null || action == null ||
            !owner.gameObject.activeInHierarchy || !mover.isActiveAndEnabled)
        {
            return;
        }

        EnsureRunner();
        Requests.Add(new Request
        {
            owner = owner,
            mover = mover,
            key = key,
            position = interactionPosition,
            action = action,
            frame = Time.frameCount
        });
    }

    public static void OfferInteraction(
        Component owner,
        ZeldaFourWayMover mover,
        KeyCode key,
        Vector2 interactionPosition,
        Action<bool> setPromptVisible)
    {
        if (IsGhostFormInteraction(mover, key) || ClockworkPuppetRuntime.BlocksCharacterInput)
        {
            setPromptVisible?.Invoke(false);
            return;
        }

        if (owner == null || mover == null || setPromptVisible == null ||
            !owner.gameObject.activeInHierarchy || !mover.isActiveAndEnabled)
        {
            return;
        }

        EnsureRunner();
        Offers.Add(new Offer
        {
            owner = owner,
            mover = mover,
            key = key,
            position = interactionPosition,
            setPromptVisible = setPromptVisible,
            frame = Time.frameCount
        });
    }

    public static bool IsSelected(
        Component owner,
        ZeldaFourWayMover mover,
        KeyCode key)
    {
        return owner != null && mover != null &&
               ((selectedOwner == owner && selectedMover == mover && selectedKey == key) ||
                (companionOwner == owner && companionMover == mover && companionKey == key));
    }

    private static bool ShareDialogueTarget(Offer a, Offer b)
    {
        if (a.owner == null || b.owner == null || a.mover != b.mover ||
            a.owner.gameObject != b.owner.gameObject) return false;
        return (a.key == KeyCode.E && a.owner is CharacterDialogue &&
                b.key == KeyCode.F && b.owner is ZeldaFourWayMover) ||
               (b.key == KeyCode.E && b.owner is CharacterDialogue &&
                a.key == KeyCode.F && a.owner is ZeldaFourWayMover);
    }

    internal static void ResolveCurrentFrame()
    {
        int frame = Time.frameCount;
        if (ClockworkPuppetRuntime.BlocksCharacterInput || SignpostInteraction.BlocksInput || CharacterDialogueWindow.BlocksInput)
        {
            for (int i = 0; i < Offers.Count; i++)
            {
                Offers[i].setPromptVisible?.Invoke(false);
            }
            Offers.Clear();
            Requests.Clear();
            selectedOwner = null;
            selectedMover = null;
            selectedKey = KeyCode.None;
            companionOwner = null;
            companionMover = null;
            companionKey = KeyCode.None;
            return;
        }

        Offers.RemoveAll(offer => offer.frame < frame);
        Requests.RemoveAll(request => request.frame < frame);
        int bestIndex = -1;
        float bestDistanceSquared = float.PositiveInfinity;
        float bestFacingDot = float.NegativeInfinity;
        int bestOwnerId = int.MaxValue;

        for (int i = 0; i < Offers.Count; i++)
        {
            Offer offer = Offers[i];
            if (IsGhostFormInteraction(offer.mover, offer.key) || offer.frame != frame || offer.owner == null ||
                offer.mover == null ||
                !offer.owner.gameObject.activeInHierarchy ||
                !offer.mover.isActiveAndEnabled)
            {
                continue;
            }

            Vector2 offset = offer.position -
                             (Vector2)offer.mover.transform.position;
            float distanceSquared = offset.sqrMagnitude;
            float facingDot = distanceSquared <= 0.0001f
                ? 1f
                : Vector2.Dot(
                    offer.mover.FacingDirection.normalized,
                    offset.normalized);
            if (facingDot <= 0.001f)
            {
                continue;
            }

            int ownerId = offer.owner.GetInstanceID();
            bool isBetter = distanceSquared < bestDistanceSquared - 0.0001f ||
                (Mathf.Abs(distanceSquared - bestDistanceSquared) <= 0.0001f &&
                 (facingDot > bestFacingDot + 0.0001f ||
                  (Mathf.Abs(facingDot - bestFacingDot) <= 0.0001f &&
                   ownerId < bestOwnerId)));
            if (!isBetter)
            {
                continue;
            }

            if (!HasClearInteractionPath(offer.owner, offer.mover, offer.key, offer.position)) continue;

            bestIndex = i;
            bestDistanceSquared = distanceSquared;
            bestFacingDot = facingDot;
            bestOwnerId = ownerId;
        }

        selectedOwner = bestIndex >= 0 ? Offers[bestIndex].owner : null;
        selectedMover = bestIndex >= 0 ? Offers[bestIndex].mover : null;
        selectedKey = bestIndex >= 0 ? Offers[bestIndex].key : KeyCode.None;

        // Preserve nearest-target arbitration for every other interaction. Only the
        // same NPC's dialogue/possession pair may share selection, regardless of component IDs.
        int companionIndex = -1;
        if (bestIndex >= 0)
            for (int i = 0; i < Offers.Count; i++)
                if (i != bestIndex && Offers[i].frame == frame &&
                    !IsGhostFormInteraction(Offers[i].mover, Offers[i].key) &&
                    ShareDialogueTarget(Offers[bestIndex], Offers[i]) &&
                    HasClearInteractionPath(Offers[i].owner, Offers[i].mover, Offers[i].key, Offers[i].position))
                { companionIndex = i; break; }
        companionOwner = companionIndex >= 0 ? Offers[companionIndex].owner : null;
        companionMover = companionIndex >= 0 ? Offers[companionIndex].mover : null;
        companionKey = companionIndex >= 0 ? Offers[companionIndex].key : KeyCode.None;

        for (int i = 0; i < Offers.Count; i++)
        {
            Offer offer = Offers[i];
            if (offer.frame == frame && offer.owner != null &&
                offer.setPromptVisible != null)
            {
                offer.setPromptVisible.Invoke(i == bestIndex || i == companionIndex);
            }
        }

        if (bestIndex >= 0)
        {
            for (int i = 0; i < Requests.Count; i++)
            {
                Request request = Requests[i];
                if (Input.GetKeyDown(request.key) && TryExecuteSelectedRequest(request))
                {
                    break;
                }
            }
        }

        Offers.RemoveAll(offer => offer.frame <= frame);
        Requests.RemoveAll(request => request.frame <= frame);
    }

    private static bool HasClearInteractionPath(Component owner, ZeldaFourWayMover mover, KeyCode key, Vector2 position)
    {
        // Keep the intentional shop-counter dialogue policy. Possession already
        // validates its own centre-to-centre path throughout the channel.
        return owner is CharacterDialogue || (key == KeyCode.F && owner is ZeldaFourWayMover) ||
            ZeldaInteractionObstruction.HasClearPath(owner, mover, position);
    }

    private static bool TryExecuteSelectedRequest(Request request)
    {
        if (request.frame != Time.frameCount || request.action == null || request.owner == null || request.mover == null ||
            !request.owner.gameObject.activeInHierarchy || !request.mover.isActiveAndEnabled ||
            !IsSelected(request.owner, request.mover, request.key) ||
            !HasClearInteractionPath(request.owner, request.mover, request.key, request.position)) return false;
        request.action.Invoke();
        return true;
    }

    private static void EnsureRunner()
    {
        if (runner != null)
        {
            return;
        }

        GameObject runnerObject = new GameObject("Zelda Interaction Arbiter");
        UnityEngine.Object.DontDestroyOnLoad(runnerObject);
        runner = runnerObject.AddComponent<ZeldaInteractionArbiterRunner>();
    }
}

[DefaultExecutionOrder(32000)]
public sealed class ZeldaInteractionArbiterRunner : MonoBehaviour
{
    private void LateUpdate()
    {
        ZeldaInteractionArbiter.ResolveCurrentFrame();
    }
}
