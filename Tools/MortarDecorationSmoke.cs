using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Actual ExecuteAlways prefab validation in the isolated Unity project.
public static class MortarDecorationSmoke
{
    static int checks;
    static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    public static void Run()
    {
        try
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Decorations/Fortress/Fortress-Mortar.prefab");
            Check(prefab, "prefab imports");
            var obj = UnityEngine.Object.Instantiate(prefab);
            var visual = obj.GetComponent<GeometricFortressDecoration>();
            Check(visual && obj.GetComponent<FortressMortarRemoteInstallation>() != null, "mortar geometry and optional remote installation component");
            var collider = obj.GetComponent<BoxCollider2D>();
            Check(collider && collider.enabled && !collider.isTrigger && collider.size == new Vector2(2.4f, 2.6f), "one simple solid footprint");
            Check(obj.GetComponentsInChildren<MeshRenderer>().Length == 1, "single combined renderer");
            Check(obj.GetComponentsInChildren<ParticleSystem>().Length == 0, "no unwanted magic/fire effects");
            var renderer = obj.GetComponentInChildren<MeshRenderer>();
            var filter = obj.GetComponentInChildren<MeshFilter>();
            var mesh = filter.sharedMesh;
            Check(mesh.vertexCount == 224 && renderer.sortingOrder == -3 && renderer.sortingLayerName == "Default", "compact mesh follows furniture sorting");
            Check(Mathf.Abs(mesh.bounds.center.x) < .001f && Mathf.Abs(mesh.bounds.center.y) < .001f, "centered pivot");
            Check(Mathf.Abs(mesh.bounds.size.x - 2.4f) < .001f && Mathf.Abs(mesh.bounds.size.y - 2.6f) < .001f, "bounds match size");
            var update = typeof(GeometricFortressDecoration).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
            for (int i = 0; i < 100; i++) update.Invoke(visual, null);
            Check(mesh == filter.sharedMesh, "static mesh is not rebuilt per frame");
            obj.SetActive(false); obj.SetActive(true);
            Check(mesh == filter.sharedMesh && renderer.gameObject.activeSelf, "streaming reactivation reuses mesh");
            var camera = new GameObject("Mortar preview camera").AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 1.5f; camera.aspect = 1;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.06f, .09f, .17f);
            var rt = new RenderTexture(512, 512, 24); camera.targetTexture = rt; camera.Render();
            var previous = RenderTexture.active; RenderTexture.active = rt;
            var image = new Texture2D(512, 512, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); image.Apply();
            int red = 0, grey = 0;
            foreach (var c in image.GetPixels())
            { if (c.r > .4f && c.r > c.g * 2) red++; if (c.r > .4f && Mathf.Abs(c.r - c.b) < .15f) grey++; }
            Check(red > 10000 && grey > 25000, "actual GPU render contains distinct grey metal and red carriage");
            foreach (float angle in new[] { 0f, 90f, 180f, 270f })
            {
                obj.transform.rotation = Quaternion.Euler(0, 0, angle); obj.transform.localScale = new Vector3(1.2f, .8f, 1);
                Check(renderer.bounds.size.x > 1.8f && renderer.bounds.size.y > 1.8f, "rotation/scaling remains valid");
            }
            var serialized = new SerializedObject(visual);
            serialized.FindProperty("solid").boolValue = false; serialized.ApplyModifiedPropertiesWithoutUndo(); update.Invoke(visual, null);
            Check(!collider.enabled, "solid option disables collision");
            RenderTexture.active = previous; camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(camera.gameObject); UnityEngine.Object.DestroyImmediate(obj);
            Debug.Log("MORTAR_DECORATION_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
