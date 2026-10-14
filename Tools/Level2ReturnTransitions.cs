using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Isolated editor diagnostic/verification tool; never saves the authored scenes.
public static class Level2ReturnTransitions
{
    const string Folder = "Assets/Scenes/Level2/";
    [Serializable] public class Route
    {
        public string name, scene;
        public Vector2 origin, arrival, scale, size, offset;
        public Vector2 centerOffset, worldSize, stairCenter, stairSize, returnOffset, returnSize;
    }
    [Serializable] public class Routes { public List<Route> routes = new List<Route>(); }
    static T[] InScene<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
        .SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
    static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
    static Routes ReadRoutes()
    {
        var scene = EditorSceneManager.OpenScene(Folder + "Level2-Floor1.unity");
        var result = new Routes();
        foreach (var node in InScene<PersistentSceneTransitionPoint>(scene))
        {
            if (node.TargetSceneName != "Level2-Floor2" && node.TargetSceneName != "Level2-Floor-1") continue;
            Require(Mathf.Abs(node.transform.eulerAngles.z) < .01f, "Unexpected rotated source " + node.name);
            result.routes.Add(new Route { name = node.name, scene = node.TargetSceneName,
                origin = node.transform.position, arrival = node.TargetArrivalPosition,
                scale = node.transform.lossyScale, size = node.TriggerSize, offset = node.TriggerOffset,
                centerOffset = node.transform.TransformVector(node.TriggerOffset),
                worldSize = Vector2.Scale(node.TriggerSize, node.transform.lossyScale) });
        }
        return result;
    }
    public static void Verify()
    {
        try
        {
            Require(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "isolated project required");
            var data = ReadRoutes(); int checkedRoutes = 0;
            Require(data.routes.Count == 32, "Expected 32 saved outgoing links");
            foreach (string name in new[] { "Level2-Floor-1", "Level2-Floor2" })
            {
                var scene = EditorSceneManager.OpenScene(Folder + name + ".unity");
                Physics2D.SyncTransforms();
                var nodes = InScene<PersistentSceneTransitionPoint>(scene).Where(n => n.TargetSceneName == "Level2-Floor1").ToArray();
                var routes = data.routes.Where(r => r.scene == name).ToArray();
                var walls = InScene<Collider2D>(scene).Where(c => c.enabled && c.gameObject.activeInHierarchy &&
                    !c.isTrigger && c.gameObject.layer == LayerMask.NameToLayer("Blocks")).ToArray();
                Require(nodes.Length == routes.Length, "One return node per outgoing link in " + name);
                var root = scene.GetRootGameObjects().Single(r => r.name == "Floor1 Return Transitions");
                Require(root.transform.childCount == routes.Length, "Return group contains exactly its routes");
                foreach (var route in routes)
                {
                    var node = nodes.Single(n => n.name == "ReturnToFloor1 - " + route.name);
                    Require(Vector2.Distance(node.transform.position, route.arrival) < .0001f, "Forward arrival matched: " + route.name);
                    Require(Vector2.Distance(node.TargetArrivalPosition, route.origin) < .0001f, "World-space return matched: " + route.name);
                    Require(node.DestinationAreaName == "城镇一层", "Prompt area label");
                    Require(node.isActiveAndEnabled && node.transform.parent == root.transform, "Active grouped transition");
                    var collider = node.GetComponent<BoxCollider2D>();
                    Require(collider.enabled && collider.isTrigger, "Trigger does not block movement");
                    Require(Vector2.Distance(collider.size, node.TriggerSize) < .0001f &&
                        Vector2.Distance(collider.offset, node.TriggerOffset) < .0001f, "Collider, editor outline and interaction share geometry");
                    Require(node.TriggerSize.x >= .35f && node.TriggerSize.y >= .35f, "Usable minimum range");
                    var rect = WallRect(collider);
                    Require(walls.All(w => !Intersects(rect, WallRect(w))), "No Blocks overlap: " + route.name);
                    Require(Vector2.Distance(collider.ClosestPoint(route.arrival), route.arrival) < .16f,
                        "Arrival within reach of aligned trigger: " + route.name);
                    Require(PrefabUtility.GetCorrespondingObjectFromSource(node) != null, "Keeps persistent transition prefab link");
                    route.returnSize = rect.size; route.returnOffset = rect.center-route.arrival;
                    checkedRoutes++;
                }
                DrawRoutes(routes, walls, name);
            }
            Debug.Log("LEVEL2_RETURN_VERIFY_PASS routes=" + checkedRoutes);
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    public static void Inspect()
    {
        try
        {
            Require(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "isolated project required");
            var data = ReadRoutes();
            File.WriteAllText(Application.dataPath + "/../Level2ReturnRoutes.json", JsonUtility.ToJson(data, true));
            foreach (string name in new[] { "Level2-Floor-1", "Level2-Floor2" })
            {
                var scene = EditorSceneManager.OpenScene(Folder + name + ".unity");
                Physics2D.SyncTransforms();
                var walls = InScene<Collider2D>(scene).Where(c => c.enabled && c.gameObject.activeInHierarchy &&
                    !c.isTrigger && c.gameObject.layer == LayerMask.NameToLayer("Blocks")).ToArray();
                var stairs = InScene<SpriteRenderer>(scene).Where(s => s.sharedMaterial != null &&
                    s.sharedMaterial.name.StartsWith("StairFloor", StringComparison.Ordinal)).ToArray();
                foreach (var route in data.routes.Where(r => r.scene == name))
                {
                    var stair = stairs.OrderBy(s => Vector2.Distance(s.bounds.ClosestPoint(route.arrival), route.arrival)).FirstOrDefault();
                    if (stair != null)
                    {
                        route.stairCenter = stair.bounds.center; route.stairSize = stair.bounds.size;
                        Debug.Log("RETURN_STAIR " + route.name + " nearest " + stair.name + " center=" + stair.bounds.center + " size=" + stair.bounds.size);
                    }
                    Vector2 center = route.arrival + route.centerOffset;
                    var neighbors = walls.Where(w => Vector2.Distance(w.ClosestPoint(center), center) < 4).ToArray();
                    var rect = AlignRange(route, neighbors);
                    route.returnOffset = rect.center - route.arrival;
                    route.returnSize = rect.size;
                    Require(rect.width >= .35f && rect.height >= .35f, "Too small return range " + route.name);
                    Require(neighbors.All(w => !Intersects(rect, WallRect(w))), "Return range overlaps Blocks " + route.name);
                    Debug.Log("RETURN_ROUTE " + route.name + " at " + route.arrival + " origin " + route.origin +
                        " size " + route.worldSize + " walls " + string.Join(";", neighbors.Select(w => w.name +
                            " center=" + w.bounds.center + " size=" + w.bounds.size)));
                }
                DrawRoutes(data.routes.Where(r => r.scene == name).ToArray(), walls, name);
            }
            File.WriteAllText(Application.dataPath + "/../Level2ReturnRoutes.json", JsonUtility.ToJson(data, true));
            Debug.Log("LEVEL2_RETURN_INSPECT_PASS routes=" + data.routes.Count);
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static Rect WallRect(Collider2D wall) => new Rect((Vector2)wall.bounds.min, (Vector2)wall.bounds.size);
    static bool Intersects(Rect a, Rect b) => Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin) > .001f &&
        Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin) > .001f;
    static Rect AlignRange(Route route, Collider2D[] walls)
    {
        const float gap = .015f;
        var rect = new Rect(route.arrival + route.centerOffset - route.worldSize * .5f, route.worldSize);
        // Preserve the Floor1 footprint, trimming only portions that enter the destination's walls.
        foreach (var wall in walls.OrderByDescending(w => w.bounds.size.x * w.bounds.size.y))
        {
            var b = WallRect(wall); if (!Intersects(rect, b)) continue;
            var candidates = new[] {
                Rect.MinMaxRect(rect.xMin, rect.yMin, Mathf.Min(rect.xMax, b.xMin-gap), rect.yMax),
                Rect.MinMaxRect(Mathf.Max(rect.xMin, b.xMax+gap), rect.yMin, rect.xMax, rect.yMax),
                Rect.MinMaxRect(rect.xMin, rect.yMin, rect.xMax, Mathf.Min(rect.yMax, b.yMin-gap)),
                Rect.MinMaxRect(rect.xMin, Mathf.Max(rect.yMin, b.yMax+gap), rect.xMax, rect.yMax) };
            var valid = candidates.Where(r => r.width >= .35f && r.height >= .35f).ToArray();
            Require(valid.Length > 0, "Cannot fit " + route.name + " around " + wall.name);
            rect = valid.OrderByDescending(r => r.width*r.height).First();
        }
        // Snap each near edge to a wall's inner face, without spanning a second room.
        // This is authored geometry only: no new alignment work runs during gameplay.
        for (int side = 0; side < 4; side++)
        {
            var candidates = new List<Rect>();
            foreach (var wall in walls)
            {
                Rect b = WallRect(wall); float overlap = side < 2 ?
                    Mathf.Min(rect.yMax,b.yMax)-Mathf.Max(rect.yMin,b.yMin) :
                    Mathf.Min(rect.xMax,b.xMax)-Mathf.Max(rect.xMin,b.xMin);
                if (overlap < .1f) continue;
                Rect candidate = rect;
                if (side == 0 && b.xMax <= rect.xMin+.02f) candidate.xMin = b.xMax+gap;
                if (side == 1 && b.xMin >= rect.xMax-.02f) candidate.xMax = b.xMin-gap;
                if (side == 2 && b.yMax <= rect.yMin+.02f) candidate.yMin = b.yMax+gap;
                if (side == 3 && b.yMin >= rect.yMax-.02f) candidate.yMax = b.yMin-gap;
                float difference = (candidate.size-rect.size).magnitude;
                if (difference > .001f && difference <= .85f && candidate.width >= .35f && candidate.height >= .35f &&
                    walls.All(w => !Intersects(candidate, WallRect(w)))) candidates.Add(candidate);
            }
            if (candidates.Count > 0) rect = candidates.OrderBy(r => (r.size-rect.size).sqrMagnitude).First();
        }
        return rect;
    }
    static void DrawRoutes(Route[] routes, Collider2D[] walls, string sceneName)
    {
        const int tile = 200, columns = 5;
        int rows = (routes.Length + columns - 1) / columns;
        var image = new Texture2D(tile * columns, tile * rows, TextureFormat.RGB24, false);
        var pixels = Enumerable.Repeat(new Color32(20, 29, 48, 255), image.width * image.height).ToArray();
        for (int n = 0; n < routes.Length; n++)
        {
            Route route = routes[n]; Vector2 center = route.arrival + route.returnOffset;
            Vector2 extent = route.returnSize * .5f;
            float view = Mathf.Max(4.5f, Mathf.Max(extent.x, extent.y) + 2);
            var localWalls = walls.Where(c => Vector2.Distance(c.ClosestPoint(center), center) < view * 1.5f).ToArray();
            int x0 = (n % columns) * tile, y0 = (rows - 1 - n / columns) * tile;
            for (int y = 0; y < tile; y++) for (int x = 0; x < tile; x++)
            {
                Vector2 p = center + new Vector2((x / (float)tile * 2 - 1) * view, (y / (float)tile * 2 - 1) * view);
                Color32 color = new Color32(20, 29, 48, 255);
                if (localWalls.Any(w => w.OverlapPoint(p))) color = new Color32(190, 195, 201, 255);
                Vector2 d = p - center;
                if (Mathf.Abs(d.x) <= extent.x && Mathf.Abs(d.y) <= extent.y)
                {
                    color = new Color32((byte)(color.r / 2), (byte)Mathf.Min(255, color.g / 2 + 60), (byte)Mathf.Min(255, color.b / 2 + 95), 255);
                    if (extent.x - Mathf.Abs(d.x) < .055f || extent.y - Mathf.Abs(d.y) < .055f) color = Color.cyan;
                }
                if (Vector2.Distance(p, route.arrival) < .12f) color = Color.yellow;
                if (x == 0 || y == 0) color = Color.white;
                pixels[(y0 + y) * image.width + x0 + x] = color;
            }
            Debug.Log("RETURN_TILE " + sceneName + " " + n + " " + route.name);
        }
        image.SetPixels32(pixels); image.Apply();
        File.WriteAllBytes(Application.dataPath + "/../" + sceneName + "-return-ranges.png", image.EncodeToPNG());
        Object.DestroyImmediate(image);
    }
}
