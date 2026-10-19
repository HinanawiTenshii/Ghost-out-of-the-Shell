using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed partial class RuntimeMiniMapGraphic
{
    private sealed class SceneGeometry
    {
        public RuntimeMiniMapSceneData data;
        public bool generated;
        public readonly Dictionary<string, WaterwayGateTargetFade> faders = new Dictionary<string, WaterwayGateTargetFade>();
        public readonly Dictionary<string, RotatingBridgeMechanism> bridges = new Dictionary<string, RotatingBridgeMechanism>();
    }
    private static readonly Dictionary<int, SceneGeometry> sceneGeometry = new Dictionary<int, SceneGeometry>();
    // Keep mesh sources strongly owned through Unity's scene-unload GC. Weak
    // entries proved unstable during repeated load/restart on the Mono runtime.
    // Bound this cache as journal previews can also introduce off-scene paths.
    private const int MaximumCachedPaths = 8192;
    private static readonly Dictionary<Vector2[], Vector2[]> triangleCache = new Dictionary<Vector2[], Vector2[]>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetGeometryCache()
    {
        foreach (var value in sceneGeometry.Values)
            if (value.generated && value.data != null) Destroy(value.data);
        sceneGeometry.Clear();
        triangleCache.Clear();
        SceneManager.sceneUnloaded -= ReleaseSceneGeometry;
        SceneManager.sceneUnloaded += ReleaseSceneGeometry;
    }
    private static void ReleaseSceneGeometry(Scene scene)
    {
        triangleCache.Clear();
        if (!sceneGeometry.TryGetValue(scene.handle, out var value)) return;
        if (value.generated && value.data != null) Destroy(value.data);
        sceneGeometry.Remove(scene.handle);
    }
    private static Vector2[] GetCachedTriangles(Vector2[] path)
    {
        if (triangleCache.TryGetValue(path, out var triangles)) return triangles;
        var result = new List<Vector2>();
        TriangulateClosedPath(path, result);
        triangles = result.ToArray();
        if (triangleCache.Count >= MaximumCachedPaths) triangleCache.Clear();
        triangleCache.Add(path, triangles);
        return triangles;
    }
    private bool TryLoadCachedScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return false;
        if (!sceneGeometry.TryGetValue(scene.handle, out var geometry))
        {
            var snapshot = Resources.Load<RuntimeMiniMapSceneData>("MiniMaps/" + scene.name);
            bool usable = snapshot != null && snapshot.UsesCurrentSchema && !string.IsNullOrEmpty(snapshot.SourceRevision);
#if UNITY_EDITOR
            // Unsaved scene edits and prefab edits must appear immediately in Play mode.
            usable = usable && !scene.isDirty && !string.IsNullOrEmpty(scene.path) &&
                snapshot.SourceRevision == UnityEditor.AssetDatabase.GetAssetDependencyHash(scene.path).ToString();
#endif
            geometry = new SceneGeometry();
            if (!usable)
            {
                RebuildFromSceneCore(scene);
                snapshot = ScriptableObject.CreateInstance<RuntimeMiniMapSceneData>();
                snapshot.hideFlags = HideFlags.HideAndDontSave;
                CopyCurrentGeometryTo(snapshot, scene.name, scene.name);
                geometry.generated = true;
            }
            geometry.data = snapshot;
            // One targeted lookup per scene, including disabled water. No wall/sprite
            // scan for baked geometry; HUD and Tab share this index and triangle data.
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var fader in root.GetComponentsInChildren<WaterwayGateTargetFade>(true))
                    geometry.faders[SceneTravelStateManager.GetMapObjectId(fader.transform)] = fader;
                foreach (var bridge in root.GetComponentsInChildren<RotatingBridgeMechanism>(true))
                    if (bridge.BridgeHinge != null)
                        geometry.bridges[SceneTravelStateManager.GetMapObjectId(bridge.BridgeHinge.transform)] = bridge;
            }
            sceneGeometry.Add(scene.handle, geometry);
        }
        RebuildFromSnapshot(geometry.data);
        for (int i = 0; i < surfaces.Count; i++)
            if (!string.IsNullOrEmpty(surfaces[i].visibilityId) && geometry.faders.TryGetValue(surfaces[i].visibilityId, out var fader))
                surfaceFaders[i] = fader;
        for (int i = 0; i < bridgeLayouts.Count; i++)
            if (geometry.bridges.TryGetValue(bridgeLayouts[i].persistentId, out var bridge)) liveBridges[i] = bridge;
        RefreshBridgeLayouts(true);
        RefreshSurfaceVisibility(true);
        if (Camera.main != null) SetCameraPosition(Camera.main.transform.position);
        return true;
    }
}
