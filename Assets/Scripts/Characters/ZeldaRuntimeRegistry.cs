using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Allocation-free runtime lookup for frequently queried Zelda scene objects.
/// Components register once instead of callers repeatedly scanning the scene.
/// </summary>
public static class ZeldaRuntimeRegistry
{
    public readonly struct SetView<T> : IReadOnlyCollection<T>
    {
        private readonly HashSet<T> set;
        public SetView(HashSet<T> value) { set = value; }
        public int Count => set.Count;
        public HashSet<T>.Enumerator GetEnumerator() => set.GetEnumerator();
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => set.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => set.GetEnumerator();
    }
    private static readonly HashSet<ZeldaFourWayMover> controlledSet = new HashSet<ZeldaFourWayMover>();
    private static readonly SpatialBoundsIndex2D<ZeldaFourWayMover> characterIndex = new SpatialBoundsIndex2D<ZeldaFourWayMover>(8f);
    private static int spatialFrame = -1;
    private static float spatialPhysicsTime = -1f;

    public static void InvalidateSpatialSnapshot() { spatialFrame = -1; }
    public static void QueryNearbyMovers(Vector2 center, float radius, List<ZeldaFourWayMover> result)
    {
        if (spatialFrame != Time.frameCount || spatialPhysicsTime != Time.fixedTime)
        {
            spatialFrame = Time.frameCount; spatialPhysicsTime = Time.fixedTime;
            foreach (var actor in moverSet)
            {
                if (actor != null && actor.gameObject.activeInHierarchy)
                    characterIndex.Set(actor, new Bounds(actor.transform.position, Vector3.zero));
                else if (!ReferenceEquals(actor, null)) characterIndex.Remove(actor);
            }
        }
        // Broad-phase padding accommodates movement within this frame; callers
        // still use live positions for exact vision, scene and distance tests.
        characterIndex.Query(new Bounds(center, Vector3.one * (Mathf.Max(0f, radius) + 1f) * 2f), result);
    }
    private static readonly HashSet<ZeldaFourWayMover> moverSet =
        new HashSet<ZeldaFourWayMover>();
    private static readonly HashSet<ZeldaCharacterAiBase> aiSet =
        new HashSet<ZeldaCharacterAiBase>();
    private static readonly HashSet<PermissionArea> permissionAreaSet =
        new HashSet<PermissionArea>();

    private static ZeldaFourWayMover controlledMover;

    public static SetView<ZeldaFourWayMover> Movers => new SetView<ZeldaFourWayMover>(moverSet);
    public static SetView<ZeldaFourWayMover> ControlledMovers => new SetView<ZeldaFourWayMover>(controlledSet);
    public static SetView<ZeldaCharacterAiBase> AiCharacters => new SetView<ZeldaCharacterAiBase>(aiSet);
    public static SetView<PermissionArea> PermissionAreas => new SetView<PermissionArea>(permissionAreaSet);
    public static event Action ControlledMoverChanged;
    public static event Action PermissionAreasChanged;

    /// <summary>Live command status for the HUD, without boxing an interface enumerator.</summary>
    public static float GetRoyalCommandBuffRemaining(Scene scene)
    {
        float remaining = 0f;
        foreach (var ai in aiSet)
        {
            if (ai == null) continue;
            float candidate = ai.RoyalCommandBuffRemaining;
            if (candidate > remaining && GetGameplayScene(ai.gameObject) == scene)
                remaining = candidate;
        }
        return remaining;
    }

    /// <summary>
    /// Traveling bodies live in DontDestroyOnLoad, but act in the active level.
    /// Keep ordinary/additively loaded scenes distinct instead of removing scene
    /// checks from skills (which could otherwise target another loaded level).
    /// </summary>
    public static Scene GetGameplayScene(GameObject actor)
    {
        if (actor == null) return default;
        Scene scene = actor.scene;
        return scene.IsValid() && scene.name == "DontDestroyOnLoad"
            ? SceneManager.GetActiveScene()
            : scene;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        moverSet.Clear();
        controlledSet.Clear();
        characterIndex.Clear(); InvalidateSpatialSnapshot();
        aiSet.Clear();
        permissionAreaSet.Clear();
        controlledMover = null;
        ControlledMoverChanged = null;
        PermissionAreasChanged = null;
    }

    public static void Register(ZeldaFourWayMover mover)
    {
        if (mover != null)
        {
            if (moverSet.Add(mover))
            {
                InvalidateSpatialSnapshot();
                LevelSceneVisualProfile.DecorateCharacter(mover);
            }
            if (mover.isActiveAndEnabled) controlledSet.Add(mover);
        }
    }

    public static void Unregister(ZeldaFourWayMover mover)
    {
        if (mover == null)
            return;

        moverSet.Remove(mover);
        controlledSet.Remove(mover); characterIndex.Remove(mover); InvalidateSpatialSnapshot();
        if (controlledMover == mover)
        {
            controlledMover = null;
            ControlledMoverChanged?.Invoke();
        }
    }

    public static void NotifyMoverEnabled(ZeldaFourWayMover mover)
    {
        Register(mover);
        if (mover != null && mover.isActiveAndEnabled)
        {
            bool changed = controlledMover != mover;
            controlledMover = mover;
            if (changed)
            {
                ControlledMoverChanged?.Invoke();
            }
        }
    }

    public static void NotifyMoverDisabled(ZeldaFourWayMover mover)
    {
        controlledSet.Remove(mover);
        InvalidateSpatialSnapshot();
        if (controlledMover == mover)
        {
            controlledMover = null;
            ControlledMoverChanged?.Invoke();
        }
    }

    public static void ClaimControlledMover(ZeldaFourWayMover mover)
    {
        if (mover == null || !mover.isActiveAndEnabled)
        {
            return;
        }

        Register(mover);
        bool changed = controlledMover != mover;
        controlledMover = mover;
        if (changed)
        {
            ControlledMoverChanged?.Invoke();
        }
    }

    public static ZeldaFourWayMover GetControlledMover()
    {
        if (controlledMover != null && controlledMover.isActiveAndEnabled)
        {
            return controlledMover;
        }

        controlledMover = null;
        foreach (ZeldaFourWayMover mover in moverSet)
        {
            if (mover != null && mover.isActiveAndEnabled)
            {
                controlledMover = mover;
                break;
            }
        }

        return controlledMover;
    }

    public static void Register(ZeldaCharacterAiBase ai)
    {
        if (ai != null)
        {
            if (aiSet.Add(ai)) LevelSceneVisualProfile.DecorateVision(ai);
        }
    }

    public static void Unregister(ZeldaCharacterAiBase ai)
    {
        if (ai != null)
        {
            aiSet.Remove(ai);
        }
    }

    public static void Register(PermissionArea area)
    {
        if (area != null && permissionAreaSet.Add(area))
        {
            PermissionAreasChanged?.Invoke();
        }
    }

    public static void Unregister(PermissionArea area)
    {
        if (area != null && permissionAreaSet.Remove(area))
        {
            PermissionAreasChanged?.Invoke();
        }
    }
}
