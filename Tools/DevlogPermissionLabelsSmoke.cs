using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class DevlogPermissionLabelsSmoke
{
    static int checks;
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    public static void Run()
    {
        try
        {
            if (!Application.dataPath.Replace('\\','/').Contains("/.codex-temp/")) throw new Exception("Isolated project only");
            EditorSceneManager.OpenScene("Assets/Scenes/DevlogDemo.unity");
            var grounds = GameObject.Find("Grounds").transform;
            var floor = grounds.GetComponentInChildren<SpriteRenderer>();
            foreach (int level in new[] { 1, 2 })
            {
                var label = grounds.Find("Permission Floor Number " + level);
                Check(label != null, "Number is a child of Grounds");
                Check(label.GetComponentsInChildren<Collider2D>(true).Length == 0 && label.GetComponentsInChildren<Collider>(true).Length == 0, "No collisions");
                Check(label.GetComponentsInChildren<MonoBehaviour>(true).Length == 0, "No runtime logic or interaction");
                var text = label.GetComponent<TextMesh>();
                Check(text != null && text.text == level.ToString(), "Correct digit");
                Check(text.color.b > text.color.g && text.color.g > text.color.r && text.color.a > .99f, "Solid blue digit");
                var renderer = label.GetComponent<MeshRenderer>();
                Check(renderer.sortingLayerID == floor.sortingLayerID && renderer.sortingOrder == floor.sortingOrder, "Matches ground sorting layer and order");
                var area = Object.FindObjectsOfType<PermissionArea>().First(x => x.transform.parent != null && x.transform.parent.name == "PermissionAreas" && x.PermissionLevel == level);
                var collider = area.GetComponent<BoxCollider2D>();
                Vector2 center = collider.transform.TransformPoint(collider.offset);
                Check(Vector2.Distance(label.position, center) < .01f, "Centered in corresponding permission area");
                text.font.RequestCharactersInTexture(text.text, text.fontSize, text.fontStyle);
                Debug.Log("DEVLOG_LABEL " + level + " position=" + label.position + " bounds=" + renderer.bounds + " sorting=" + renderer.sortingOrder);
                Check(renderer.bounds.size.y > 2 && renderer.bounds.size.y < 4, "Readable floor marking size");
            }
            var camera = new GameObject("Label verification camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(-.5f,-27.7f,-10);
            camera.orthographic = true; camera.orthographicSize = 10.8f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.1f,.12f,.16f);
            camera.cullingMask = ~(1 << LayerMask.NameToLayer("UI"));
            var rt = new RenderTexture(1024,1024,24); camera.targetTexture = rt;
            camera.Render(); RenderTexture.active = rt;
            var image = new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0); image.Apply();
            File.WriteAllBytes(Path.Combine(Directory.GetParent(Application.dataPath).FullName,"DevlogPermissionLabels.png"),image.EncodeToPNG());
            RenderTexture.active = null; camera.targetTexture = null;
            Object.DestroyImmediate(image); Object.DestroyImmediate(rt); Object.DestroyImmediate(camera.gameObject);
            Debug.Log("DEVLOG_PERMISSION_LABELS_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
