using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Isolated Unity editor integration: production cable, router and shader;
// device bindings use tiny stubs. Never execute against a user scene.
public static class BatteryCableRoutingSmoke
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static int checks;
    static void Check(bool ok, string label) { checks++; if (!ok) throw new Exception(label); }
    static void Set(object o, string field, object value) => o.GetType().GetField(field,Flags).SetValue(o,value);
    static T Get<T>(object o, string field) => (T)o.GetType().GetField(field,Flags).GetValue(o);
    static void Call(object o, string method) => o.GetType().GetMethod(method,Flags).Invoke(o,null);
    static List<Vector2> Route(BatterySocketCable c) => Get<List<Vector2>>(c,"route");
    static bool Equal(List<Vector2> a,List<Vector2> b)
    {
        if(a.Count!=b.Count)return false;
        for(int i=0;i<a.Count;i++)if((a[i]-b[i]).sqrMagnitude>1e-8f)return false;
        return true;
    }
    static GameObject Wall(int order)
    {
        var go=new GameObject("Blocks Wall",typeof(SpriteRenderer),typeof(BoxCollider2D));go.layer=7;
        var texture=new Texture2D(1,1);texture.SetPixel(0,0,Color.white);texture.Apply();
        var sprite=Sprite.Create(texture,new Rect(0,0,1,1),new Vector2(.5f,.5f),1);
        var renderer=go.GetComponent<SpriteRenderer>();renderer.sprite=sprite;
        renderer.color=new Color(.65f,.72f,.78f);renderer.sortingOrder=order;
        go.transform.localScale=new Vector3(.8f,8,1);return go;
    }
    static Color[] Render(Camera cam,Camera overlay,RenderTexture rt)
    {
        cam.Render();if(overlay!=null)overlay.Render();RenderTexture.active=rt;
        var tex=new Texture2D(rt.width,rt.height,TextureFormat.RGBA32,false);
        tex.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);tex.Apply();
        var pixels=tex.GetPixels();UnityEngine.Object.DestroyImmediate(tex);return pixels;
    }
    static void TestOcclusion(BatterySocketCable cable,GameObject wall)
    {
        var cam=new GameObject("Camera").AddComponent<Camera>();cam.orthographic=true;
        cam.orthographicSize=4;cam.aspect=2;cam.transform.position=new Vector3(0,-1,-10);
        cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.035f,.06f,.14f);
        var rt=new RenderTexture(800,400,24);cam.targetTexture=rt;
        var glow=new GameObject("Current probe",typeof(MeshFilter),typeof(MeshRenderer));
        var mesh=new Mesh();mesh.vertices=new[]{new Vector3(-3,-3.1f,.016f),new Vector3(-3,-2.9f,.016f),new Vector3(3,-2.9f,.016f),new Vector3(3,-3.1f,.016f)};
        mesh.uv=new[]{new Vector2(0,0),new Vector2(0,1),new Vector2(0,1),new Vector2(0,0)};mesh.triangles=new[]{0,1,2,0,2,3};
        glow.GetComponent<MeshFilter>().sharedMesh=mesh;
        var glowRenderer=glow.GetComponent<MeshRenderer>();glowRenderer.sortingOrder=-1;
        var mat=new Material(Resources.Load<Shader>("BatteryCableFlow"));mat.SetFloat("_FlowTravel",.2f);glowRenderer.sharedMaterial=mat;
        var cableRenderer=Get<MeshRenderer>(cable,"cableRenderer");
        Check(cableRenderer.sortingOrder<=-1&&cableRenderer.gameObject.layer==0,"Ground render order and mask layer");
        var visible=Render(cam,null,rt);
        cableRenderer.enabled=false;glowRenderer.enabled=false;var hidden=Render(cam,null,rt);
        int outside=0;
        for(int y=0;y<400;y++)for(int x=0;x<800;x++)
        {
            float worldX=(x+.5f)/800*16-8;int i=y*800+x;
            float delta=Mathf.Abs(visible[i].r-hidden[i].r)+Mathf.Abs(visible[i].g-hidden[i].g)+Mathf.Abs(visible[i].b-hidden[i].b);
            if(Mathf.Abs(worldX)<.35f)Check(delta<.015f,"Ordinary Blocks covers casing and additive current");
            else if(delta>.1f)outside++;
        }
        Check(outside>500,"Wire/current still visible outside wall");
        // Project's existing Blocks overlay renders AFTER world/mask, even when
        // a special pillar has a negative sorting order (e.g. -5 in Floor1).
        var overlay=new GameObject("Blocks Overlay Camera").AddComponent<Camera>();overlay.CopyFrom(cam);
        overlay.cullingMask=1<<7;overlay.clearFlags=CameraClearFlags.Depth;
        wall.GetComponent<SpriteRenderer>().sortingOrder=-5;
        cableRenderer.enabled=true;glowRenderer.enabled=true;visible=Render(cam,overlay,rt);
        cableRenderer.enabled=false;glowRenderer.enabled=false;hidden=Render(cam,overlay,rt);
        for(int y=0;y<400;y++)for(int x=384;x<416;x++)
        {
            int i=y*800+x;float worldY=(y+.5f)/400*8-5;
            if(worldY>-.0f-3.8f&&worldY<3.8f)
                Check(Mathf.Abs(visible[i].r-hidden[i].r)<.015f,"Blocks overlay also covers negative-order special pillars");
        }
        cableRenderer.enabled=true;glowRenderer.enabled=true;var final=Render(cam,overlay,rt);
        var preview=new Texture2D(800,400);preview.SetPixels(final);preview.Apply();
        System.IO.File.WriteAllBytes(Application.dataPath+"/../cable-occlusion-preview.png",preview.EncodeToPNG());
    }
    public static void Run()
    {
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var source=new GameObject("Source");source.transform.position=new Vector3(-4,1,0);
            var target=new GameObject("Target");target.transform.position=new Vector3(4,1,0);
            var mechanism=target.AddComponent<WaterwayGateController>();
            var socket=source.AddComponent<BatterySocket>();socket.LinkedMechanism=mechanism;
            var cable=source.AddComponent<BatterySocketCable>();Set(cable,"naturalStagger",0f);
            Call(cable,"LateUpdate");var original=new List<Vector2>(Route(cable));
            var wall=Wall(0);cable.RebuildCable();Check(Equal(original,Route(cable)),"Adding wall does not change generated path");
            Check(cable.HasRoute,"Wall crossing does not hide cable");
            var legacy=new List<Vector2>{original[0],new Vector2(original[0].x,-3),new Vector2(original[original.Count-1].x,-3),original[original.Count-1]};
            Route(cable).Clear();Route(cable).AddRange(legacy);Call(cable,"DrawRoute");
            // Simulate hot reload from old code: no saved points or managed mesh references.
            Route(cable).Clear();Set(cable,"savedRoute",new List<Vector2>());Set(cable,"savedBinding",null);Set(cable,"lastBinding",null);
            Set(cable,"generated",null);Set(cable,"mesh",null);Set(cable,"cableRenderer",null);Set(cable,"material",null);
            Call(cable,"Refresh");Check(Equal(legacy,Route(cable)),"Existing old-mesh bends recovered unchanged");
            Check(source.GetComponentsInChildren<MeshFilter>(true).Length==1,"Reload adopts mesh instead of duplicating it");
            wall.transform.position=new Vector3(2,0,0);Call(cable,"Refresh");Check(Equal(legacy,Route(cable)),"Moving wall cannot reshuffle old cable");
            Set(cable,"cableWidth",.2f);Call(cable,"OnValidate");Call(cable,"Refresh");
            Check(Equal(legacy,Route(cable)),"Visual edit changes width without changing bends");
            source.SetActive(false);source.SetActive(true);Call(cable,"LateUpdate");
            Check(Equal(legacy,Route(cable)),"Streaming disable/enable preserves bends");
            // Persist and reopen in an isolated scene, proving actual Unity serialization.
            EditorSceneManager.SaveScene(source.scene,"Assets/RouteCache.unity");
            EditorSceneManager.OpenScene("Assets/RouteCache.unity",OpenSceneMode.Single);
            source=GameObject.Find("Source");target=GameObject.Find("Target");cable=source.GetComponent<BatterySocketCable>();
            Call(cable,"LateUpdate");Check(Equal(legacy,Route(cable)),"Saved scene restores cached route exactly");
            target.transform.position+=Vector3.up;Call(cable,"LateUpdate");
            Check(!Equal(legacy,Route(cable))&&Mathf.Abs(Route(cable)[Route(cable).Count-1].y-1.38f)<.001f,"Endpoint movement regenerates using current port");
            // Put saved detour back for render comparison.
            target.transform.position-=Vector3.up;Call(cable,"LateUpdate");Route(cable).Clear();Route(cable).AddRange(legacy);Call(cable,"DrawRoute");
            wall=GameObject.Find("Blocks Wall");wall.transform.position=Vector3.zero;
            // Sprites created at runtime are not disk assets; restore for reopened scene.
            UnityEngine.Object.DestroyImmediate(wall);wall=Wall(0);
            TestOcclusion(cable,wall);
            cable.RebuildCable();Check(!Equal(legacy,Route(cable)),"Explicit regenerate opts into new unblocked route");
            Debug.Log("CABLE_ROUTING_GPU_PASS checks="+checks);
            EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
