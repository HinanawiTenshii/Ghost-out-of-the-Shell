using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed partial class SceneTravelStateManager
{
    // Refreshed during the existing hierarchy pass, not every frame. One native
    // GetComponents call per object replaces repeated native queries for every
    // supported device type. Refreshing preserves scripts added at runtime.
    private sealed class StateComponents
    {
        public readonly List<Component> components = new List<Component>(5);
        public string hierarchyPath;
        public bool tracked, transformOnly;
        public T Get<T>() where T : Component
        {
            foreach (var component in components) if (component is T result) return result;
            return null;
        }
        public void Refresh(Transform target)
        {
            components.Clear(); target.GetComponents(components);
            tracked = false; transformOnly = true;
            foreach (var c in components)
            {
                if (c is Renderer || c is Collider2D || c is GrowthCollectible) tracked = true;
                if (c is ZeldaCharacterData || c is ZeldaFourWayMover || c is ZeldaCharacterAiBase || c is DoorHingeInteraction || c is TrapdoorInteraction ||
                    c is DoorData || c is LeverData || c is BatterySocket || c is EnergyCrystal || c is SummoningCircle || c is ElectricGatePillar ||
                    c is TemporaryCharacterEffectArea || c is LiquidInjector || c is AlchemyCauldron || c is SpecificItemSubmissionStation ||
                    c is PickupItemBase || c is DocumentReader || c is TimedLeverObjectToggle || c is PlacedBomb ||
                    c is ClockworkPuppetRuntime || c is BuilderTurretBond || c is TrialProofAltar || c is FortressMortarRemoteInstallation || c is ArenaChallengeController || c is KnightCaptainAmbush)
                { tracked = true; transformOnly = false; }
            }
        }
    }
    private static readonly Dictionary<int, Dictionary<Transform, StateComponents>> stateComponentCaches =
        new Dictionary<int, Dictionary<Transform, StateComponents>>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStateComponentCaches()
    {
        stateComponentCaches.Clear();
        SceneManager.sceneUnloaded -= ReleaseStateComponentCache;
        SceneManager.sceneUnloaded += ReleaseStateComponentCache;
    }
    private static void ReleaseStateComponentCache(Scene scene) => stateComponentCaches.Remove(scene.handle);

    private static StateComponents CachedComponents(Transform target, bool refresh = false)
    {
        int handle = target.gameObject.scene.handle;
        if (!stateComponentCaches.TryGetValue(handle, out var scene))
            stateComponentCaches[handle] = scene = new Dictionary<Transform, StateComponents>();
        if (!scene.TryGetValue(target, out var entry))
        { scene[target] = entry = new StateComponents(); refresh = true; }
        if (refresh) entry.Refresh(target);
        return entry;
    }
    private static T StateComponent<T>(Transform target) where T : Component => CachedComponents(target).Get<T>();
    private static string CachedHierarchyPath(Transform target)
    {
        return stateComponentCaches.TryGetValue(target.gameObject.scene.handle, out var scene) && scene.TryGetValue(target, out var entry)
            ? entry.hierarchyPath : null;
    }
}
