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
    private static readonly HashSet<ZeldaFourWayMover> moverSet =
        new HashSet<ZeldaFourWayMover>();
    private static readonly HashSet<ZeldaCharacterAiBase> aiSet =
        new HashSet<ZeldaCharacterAiBase>();
    private static readonly HashSet<PermissionArea> permissionAreaSet =
        new HashSet<PermissionArea>();

    private static ZeldaFourWayMover controlledMover;

    public static IReadOnlyCollection<ZeldaFourWayMover> Movers => moverSet;
    public static IReadOnlyCollection<ZeldaCharacterAiBase> AiCharacters => aiSet;
    public static IReadOnlyCollection<PermissionArea> PermissionAreas => permissionAreaSet;
    public static event Action ControlledMoverChanged;
    public static event Action PermissionAreasChanged;

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
            moverSet.Add(mover);
        }
    }

    public static void Unregister(ZeldaFourWayMover mover)
    {
        if (mover == null)
            return;

        moverSet.Remove(mover);
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
            aiSet.Add(ai);
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
