using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class ArenaSceneInspection
{
    public static void Run()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Level2/Level2-Floor-1.unity");
        var left = GameObject.Find("左侧恢复池");
        Debug.Log("ARENA left=" + left.transform.position + " parent=" + Path(left.transform.parent));
        foreach (var root in scene.GetRootGameObjects())
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            var p = t.position;
            if (Vector2.Distance(p, left.transform.position) > 50) continue;
            var data = t.GetComponent<ZeldaCharacterData>();
            var door = t.GetComponent<DoorHingeInteraction>();
            var transition = t.GetComponent<PersistentSceneTransitionPoint>();
            var col = t.GetComponent<Collider2D>();
            if (data == null && door == null && transition == null && t.childCount == 0) continue;
            if (t.GetComponent<Renderer>() != null && data == null && door == null) continue;
            var id = GlobalObjectId.GetGlobalObjectIdSlow(t.gameObject);
            Debug.Log("ARENA " + Path(t) + " pos=" + p + " id=" + id.targetObjectId +
                " data=" + (data != null ? data.GetType().Name + "/hp" + data.Health : "") +
                " door=" + (door != null) + " transition=" + (transition != null) +
                " bounds=" + (col != null ? col.bounds.ToString() : ""));
        }
        EditorApplication.Exit(0);
    }
    static string Path(Transform t) => t == null ? "" : (t.parent != null ? Path(t.parent) + "/" : "") + t.name;
}
