using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Scene-local, non-persisted actions that must finish before an automatic snapshot.</summary>
public static class AutoSaveTransientActivity
{
    private static readonly HashSet<Component> Activities = new HashSet<Component>();
    private static readonly System.Predicate<Component> DestroyedPredicate = IsDestroyed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Activities.Clear();

    public static void Register(Component activity)
    {
        // Resource/prefab assets have no scene and must never hold up a save.
        // OnEnable may run while its valid scene is still completing a load.
        if (activity != null && activity.gameObject.scene.IsValid() && Activities.Add(activity))
            GameSaveSystem.NotifyAutoSaveTransientAction(activity);
    }

    public static void Unregister(Component activity)
    {
        // ReferenceEquals still allows removal of a Unity destroyed-object key.
        if (!ReferenceEquals(activity, null)) Activities.Remove(activity);
    }

    public static bool HasActivity(Scene scene)
    {
        Activities.RemoveWhere(DestroyedPredicate);
        if (!scene.IsValid() || !scene.isLoaded) return false;
        foreach (var activity in Activities)
        {
            if (!activity.gameObject.activeInHierarchy ||
                (activity is Behaviour behaviour && !behaviour.isActiveAndEnabled)) continue;
            if (ZeldaRuntimeRegistry.GetGameplayScene(activity.gameObject) == scene) return true;
        }
        return SummoningCircle.HasActiveRitual(scene);
    }

    private static bool IsDestroyed(Component activity) => activity == null;
}
