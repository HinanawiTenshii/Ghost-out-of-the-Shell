using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ArenaPresentationSmoke
{
    public static void Run()
    {
        try
        {
            if(!Application.dataPath.Replace('\\','/').Contains("/.codex-temp/")) throw new Exception("isolated only");
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/Level2/Level2-Floor-1.unity");
            foreach(var root in scene.GetRootGameObjects()) foreach(var sr in root.GetComponentsInChildren<SpriteRenderer>(true))
                sr.maskInteraction=SpriteMaskInteraction.None;
            foreach(var camera in UnityEngine.Object.FindObjectsOfType<Camera>()) camera.enabled=false;
            var go=new GameObject("Arena preview camera"); var cam=go.AddComponent<Camera>();cam.enabled=false;
            cam.orthographic=true;cam.orthographicSize=17f;cam.transform.position=new Vector3(87.7f,-1.4f,-100f);
            cam.nearClipPlane=.1f;cam.farClipPlane=250f;cam.backgroundColor=new Color(.03f,.05f,.10f);cam.clearFlags=CameraClearFlags.SolidColor;
            var rt=new RenderTexture(1440,1080,24);cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;
            var image=new Texture2D(1440,1080,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1440,1080),0,0);image.Apply();
            File.WriteAllBytes(Path.GetFullPath("../Arena-overview.png"),image.EncodeToPNG());
            var arena=UnityEngine.Object.FindObjectOfType<ArenaChallengeController>();
            foreach(var root in scene.GetRootGameObjects()) foreach(var c in root.GetComponentsInChildren<Collider2D>(true))
                if(!c.isTrigger&&c.OverlapPoint(arena.guardianPosition)) Debug.LogWarning("ARENA spawn overlaps "+c.name+" "+c.bounds);
            Debug.Log("ARENA_PRESENTATION_PASS");EditorApplication.Exit(0);
        }
        catch(Exception e) { Debug.LogException(e);EditorApplication.Exit(1); }
    }
}
