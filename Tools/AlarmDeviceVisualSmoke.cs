using System;
using System.Reflection;
using UnityEngine;
using UnityEditor;

// Run in an isolated Unity project with the production visual and a minimal
// AlarmDevice power stub. This verifies real mesh/material output, not AI.
public static class AlarmDeviceVisualSmoke
{
    public static void Run()
    {
        try
        {
            var go = new GameObject("Alarm Preview");
            var visual = go.AddComponent<AlarmDeviceVisual>();
            typeof(AlarmDeviceVisual).GetMethod("LateUpdate", BindingFlags.NonPublic|BindingFlags.Instance).Invoke(visual, null);
            var renderers = go.GetComponentsInChildren<MeshRenderer>();
            if (renderers.Length != 1) throw new Exception("Expected one combined renderer");
            Mesh mesh = go.GetComponentInChildren<MeshFilter>().sharedMesh;
            if (Mathf.Abs(mesh.bounds.size.x-.9f)>.001f || Mathf.Abs(mesh.bounds.size.y-1.1f)>.001f)
                throw new Exception("Visual and collider/cable bounds differ");
            if (mesh.vertexCount > 180) throw new Exception("Excess geometry");
            var camera = new GameObject("Preview Camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(0,0,-10);
            camera.orthographic = true; camera.orthographicSize = .75f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.06f,.08f,.12f);
            var target = new RenderTexture(480,600,24);
            camera.targetTexture = target; camera.aspect = .8f; camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(480,600,TextureFormat.RGBA32,false);
            image.ReadPixels(new Rect(0,0,480,600),0,0); image.Apply();
            int red=0,gray=0;
            foreach (Color c in image.GetPixels())
            {
                if(c.r>c.g+.25f&&c.r>c.b+.25f) red++;
                if(c.r>.4f&&Mathf.Abs(c.r-c.g)<.1f&&Mathf.Abs(c.r-c.b)<.12f) gray++;
            }
            if(red<10000||gray<10000) throw new Exception("Missing red bell or gray housing");
            System.IO.File.WriteAllBytes(Application.dataPath+"/../alarm-preview.png",image.EncodeToPNG());
            Debug.Log("ALARM_VISUAL_GPU_PASS vertices="+mesh.vertexCount+" red="+red+" gray="+gray);
            EditorApplication.Exit(0);
        }
        catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
