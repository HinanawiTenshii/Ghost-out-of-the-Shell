using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class KnightSceneInspection
{
    public static void Run()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Level2/Level2-Floor2.unity");
        foreach (var root in scene.GetRootGameObjects())
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            var ai = t.GetComponent<SquadPatrolCharacterAi>();
            var area = t.GetComponent<PermissionArea>();
            var door = t.GetComponent<DoorHingeInteraction>();
            var doc = t.GetComponent<DocumentReader>();
            if (ai == null && area == null && door == null && doc == null &&
                !t.name.Contains("安全") && !t.name.Contains("骑士") && !t.name.Contains("圣骑")) continue;
            var id = GlobalObjectId.GetGlobalObjectIdSlow(t.gameObject);
            Debug.Log("KNIGHT " + Path(t) + " pos=" + t.position + " id=" + id +
                " ai=" + (ai != null ? EditorJsonUtility.ToJson(ai) : "") +
                " area=" + (area != null ? area.AreaBounds + " perm=" + area.PermissionLevel : "") +
                " door=" + (door != null ? EditorJsonUtility.ToJson(door) : "") + " doc=" + (doc != null));
            if (ai != null && Path(t).Contains("骑士长小队"))
            {
                var members = new SerializedObject(ai).FindProperty("members");
                for (int i=0;i<members.arraySize;i++) Debug.Log("MEMBER " + t.name + " -> " + members.GetArrayElementAtIndex(i).objectReferenceValue);
            }
            if (door != null && ((t.position.x > 55 && t.position.x < 70 && t.position.y < -34) ||
                (t.position.x > 55 && t.position.x < 67 && t.position.y > 76)))
                foreach (var c in t.GetComponentsInChildren<Collider2D>(true)) Debug.Log("DOORCOL " + Path(c.transform) + " " + c.bounds);
        }
        EditorApplication.Exit(0);
    }
    static string Path(Transform t) => t == null ? "" : (t.parent != null ? Path(t.parent) + "/" : "") + t.name;
}
