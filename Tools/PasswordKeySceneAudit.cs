using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class PasswordKeySceneAudit
{
    [Serializable] public class Report { public List<Key> keys = new List<Key>(); public List<Door> doors = new List<Door>(); public List<Doc> documents = new List<Doc>(); }
    [Serializable] public class Location { public string scene, path, globalId; public long sourceId; public string prefabInstance; public long localId; }
    [Serializable] public class Key { public Location location, visual, renderer; public string identity, instanceId, displayName; public Color color, rendererColor; }
    [Serializable] public class Door { public Location location; public string password; public bool passwordLock, locked; public List<Color> leafColors = new List<Color>(); public List<string> keys = new List<string>(); }
    [Serializable] public class Doc { public Location location; public string text; }
    static string PathOf(Transform t) => t.parent != null ? PathOf(t.parent) + "/" + t.name : t.name;
    static Location Locate(Component obj)
    {
        if (obj == null) return null;
        var id = GlobalObjectId.GetGlobalObjectIdSlow(obj);
        var source = PrefabUtility.GetCorrespondingObjectFromSource(obj);
        long sourceId = 0;
        if (source != null) AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out string _, out sourceId);
        return new Location { scene = obj.gameObject.scene.path, path = PathOf(obj.transform), globalId = id.ToString(),
            sourceId = sourceId, prefabInstance = id.targetPrefabId.ToString(), localId = (long)id.targetObjectId };
    }
    private static bool verify;
    public static void Verify() { verify = true; Run(); }
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/")) throw new Exception("isolated only");
        var report = new Report();
        foreach (string path in Directory.GetFiles("Assets/Scenes", "*.unity", SearchOption.AllDirectories).OrderBy(p => p))
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            foreach (var key in Object.FindObjectsOfType<KeyPickupItem>(true).Where(k => k.gameObject.scene == scene))
            {
                var visual = key.GetComponent<KeyPickupItemVisual>();
                var so = new SerializedObject(key);
                report.keys.Add(new Key { location = Locate(key), visual = Locate(visual), renderer = Locate(key.GetComponent<SpriteRenderer>()),
                    identity = key.PersistentIdentity != null ? key.PersistentIdentity.StableInstanceId : "",
                    instanceId = key.ConfiguredUniqueInstanceId, displayName = key.ItemName,
                    color = visual != null ? visual.DisplayColor : Color.white, rendererColor = key.GetComponent<SpriteRenderer>().color });
            }
            foreach (var door in Object.FindObjectsOfType<DoorHingeInteraction>(true).Where(d => d.gameObject.scene == scene))
            {
                var so = new SerializedObject(door);
                var item = new Door { location = Locate(door), locked = door.IsLocked, passwordLock = door.PasswordLockEnabled,
                    password = so.FindProperty("doorPassword").stringValue };
                foreach (var leaf in door.GetComponentsInChildren<DoorData>(true).Where(l => l.GetComponentInParent<DoorHingeInteraction>() == door))
                    item.leafColors.Add(leaf.GetComponent<SpriteRenderer>().color);
                for (int i = 1; i <= 3; i++)
                {
                    string suffix = i == 1 ? "" : i.ToString();
                    var identity = so.FindProperty("requiredUnlockItemIdentity" + suffix).objectReferenceValue as PickupItemPersistentIdentity;
                    var key = so.FindProperty("requiredUnlockItem" + suffix).objectReferenceValue as KeyPickupItem;
                    string instance = so.FindProperty("requiredUnlockItemInstanceId" + suffix).stringValue;
                    if (identity != null) item.keys.Add("identity:" + identity.StableInstanceId);
                    else if (key != null) item.keys.Add("object:" + GlobalObjectId.GetGlobalObjectIdSlow(key));
                    else if (!string.IsNullOrEmpty(instance)) item.keys.Add("instance:" + instance);
                }
                if (item.keys.Count > 0 || item.passwordLock) report.doors.Add(item);
            }
            if (scene.name.StartsWith("Level2"))
                foreach (var doc in Object.FindObjectsOfType<DocumentReader>(true).Where(d => d.gameObject.scene == scene))
                    report.documents.Add(new Doc { location = Locate(doc), text = doc.DocumentText });
        }
        File.WriteAllText(Path.GetFullPath("../PasswordKeySceneAudit.json"), JsonUtility.ToJson(report, true));
        if (verify)
        {
            foreach (var key in report.keys)
            {
                var matches = report.doors.Where(d => d.keys.Any(k => k == "object:" + key.location.globalId ||
                    (!string.IsNullOrEmpty(key.identity) && (k == "identity:" + key.identity || k == "instance:" + key.identity)) ||
                    (!string.IsNullOrEmpty(key.instanceId) && k == "instance:" + key.instanceId))).ToArray();
                if (matches.Length == 0) throw new Exception("No bound door: " + key.location.path);
                foreach (var door in matches)
                    foreach (var color in door.leafColors)
                        if (!Same(key.color, color) || !Same(key.rendererColor, color)) throw new Exception("Key color mismatch: " + key.location.path);
                Debug.Log("KEY_COLOUR_CHECK " + key.location.scene + " / " + key.location.path);
            }
            if (report.keys.Count != 18) throw new Exception("Key inventory changed; review report");
            var passwordDocs = report.documents.Where(d => d.location.path.EndsWith("密码")).ToArray();
            if (passwordDocs.Length != 3 || passwordDocs.Any(d => d.text.Length < 95)) throw new Exception("Password document coverage mismatch");
            Debug.Log("KEY_COLOUR_PASS keys=" + report.keys.Count + " documents=" + passwordDocs.Length);
        }
        Debug.Log("PASSWORD_KEY_AUDIT keys=" + report.keys.Count + " doors=" + report.doors.Count + " documents=" + report.documents.Count);
        EditorApplication.Exit(0);
    }
    static bool Same(Color a, Color b) => Mathf.Abs(a.r-b.r) < .000001f && Mathf.Abs(a.g-b.g) < .000001f &&
        Mathf.Abs(a.b-b.b) < .000001f && Mathf.Abs(a.a-b.a) < .000001f;
}
