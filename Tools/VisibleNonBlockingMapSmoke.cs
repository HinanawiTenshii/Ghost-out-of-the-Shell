using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Called by Level2MapSurfacesSmoke in the isolated validation project.
public static class VisibleNonBlockingMapSmoke
{
    static void Check(bool value, string label)
    {
        if (!value) throw new Exception("Visible Non Blocking map: " + label);
    }
    static RuntimeMiniMapGraphic Graphic() => new GameObject("Layer Map Test", typeof(RectTransform), typeof(CanvasRenderer)).AddComponent<RuntimeMiniMapGraphic>();
    static bool Same(Vector2[] a, Vector2[] b) => a.Length == b.Length && a.Zip(b, (x,y) => (x-y).sqrMagnitude < .000001f).All(v => v);
    public static void CheckScene(Scene scene, RuntimeMiniMapSceneData data)
    {
        int layer = LayerMask.NameToLayer("Visible Non Blocking");
        var boxes = Object.FindObjectsOfType<BoxCollider2D>(true).Where(c => c.gameObject.scene == scene && c.gameObject.layer == layer && !c.isTrigger && c.GetComponentInParent<DoorHingeInteraction>(true) == null).ToArray();
        foreach (var box in boxes)
        {
            var path = RuntimeMiniMapSceneData.CreateBoxPath(box.transform.localToWorldMatrix, box.offset, box.size);
            Check(data.BlockPaths.Count(p => Same(p.points, path)) == 1, "exactly one transformed footprint: " + box.name);
        }
        // Scene authoring can add/remove walls; each current collider is checked
        // above rather than pinning the test to an obsolete scene object count.
        if (scene.name == "Level2-Floor2") Check(boxes.Length > 0, "Floor2 non-blocking walls present");
        Debug.Log("VISIBLE_NON_BLOCKING_GEOMETRY " + scene.name + " walls=" + boxes.Length);
    }
    public static void CheckFixtures()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        int layer = LayerMask.NameToLayer("Visible Non Blocking");
        var box = new GameObject("Inactive Low Wall").AddComponent<BoxCollider2D>();
        box.gameObject.layer = layer; box.offset = new Vector2(.2f, -.3f); box.size = new Vector2(4, .5f);
        box.transform.position = new Vector3(3, 6, 0); box.transform.rotation = Quaternion.Euler(0, 0, 37);
        box.transform.localScale = new Vector3(2, 1, 1); box.gameObject.SetActive(false);
        var trigger = new GameObject("Interaction Trigger").AddComponent<BoxCollider2D>(); trigger.gameObject.layer = layer; trigger.isTrigger = true;
        var hidden = new GameObject("Hidden Wall").AddComponent<BoxCollider2D>(); hidden.gameObject.layer = LayerMask.NameToLayer("Hidden Blocks");
        var door = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Decorations/DoorHinge.prefab"));
        Check(door != null, "door fixture prefab exists");
        foreach (var c in door.GetComponentsInChildren<Collider2D>(true)) c.gameObject.layer = layer;
        var compositeRoot = new GameObject("Composite Low Wall"); compositeRoot.layer = layer;
        compositeRoot.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
        var composite = compositeRoot.AddComponent<CompositeCollider2D>(); composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
        compositeRoot.AddComponent<BoxCollider2D>().usedByComposite = true;
        Physics2D.SyncTransforms(); composite.GenerateGeometry();
        var g = Graphic(); g.RebuildFromScene(scene);
        var data = ScriptableObject.CreateInstance<RuntimeMiniMapSceneData>(); g.CopyCurrentGeometryTo(data, "test", "test");
        Check(data.BlockPaths.Length == 1 + composite.pathCount, "inactive wall captured, composite deduplicated, triggers/hidden walls/doors excluded");
        var expected = RuntimeMiniMapSceneData.CreateBoxPath(box.transform.localToWorldMatrix, box.offset, box.size);
        Check(data.BlockPaths.Any(p => Same(p.points, expected)), "rotation, scale, offset preserved while inactive");
        var copy = Graphic(); copy.RebuildFromSnapshot(data);
        var snapshot = ScriptableObject.CreateInstance<RuntimeMiniMapSceneData>(); copy.CopyCurrentGeometryTo(snapshot, "test", "test");
        Check(snapshot.BlockPaths.Length == data.BlockPaths.Length, "unloaded geometry parity");
        Check(snapshot.UsesCurrentSchema, "new schema invalidates old snapshots");
        Object.DestroyImmediate(data); Object.DestroyImmediate(snapshot); Object.DestroyImmediate(g.gameObject); Object.DestroyImmediate(copy.gameObject);
        Debug.Log("VISIBLE_NON_BLOCKING_FIXTURES_PASS");
    }
}
