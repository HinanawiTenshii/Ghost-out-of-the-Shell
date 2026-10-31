#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class LevelPropFloorSmoke
{
    static int checks;
    static void Check(bool value, string description) { checks++; if (!value) throw new Exception(description); }
    public static IEnumerable<object> VerifyScene(Scene scene)
    {
        var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        var shadows = all.Where(t => t.name == LevelPropFloorRefinement.ShadowName).ToArray();
        Check(shadows.Length > 0, "Decoration shadows installed: " + scene.name);
        foreach (var shadow in shadows)
        {
            Check(shadow.GetComponent<Collider2D>() == null && shadow.GetComponents<MonoBehaviour>().All(c=>c is SceneTravelStableId),
                "No new collision/update loop: "+string.Join(",",shadow.GetComponents<MonoBehaviour>().Select(c=>c.GetType().Name)));
            var mesh = shadow.GetComponent<MeshFilter>().sharedMesh;
            Check(mesh && mesh.vertexCount > 0 && AssetDatabase.Contains(mesh), "Shared baked geometry survives reload");
            Check(mesh.colors.All(c => c.a <= .271f), "Subtle contact opacity");
            Check(shadow.GetComponent<MeshRenderer>().sortingOrder <= -5, "Shadows stay below actors");
        }
        var floors = all.Select(t => t.GetComponent<SpriteRenderer>()).Where(s => s && s.sharedMaterial &&
            s.sharedMaterial.shader.name == "Game/Terminal Floor Detail").ToArray();
        foreach (var mat in floors.Select(s => s.sharedMaterial).Distinct())
        {
            bool indoor = !scene.name.Contains("Garden") && !scene.name.Contains("Desert") && AssetDatabase.GetAssetPath(mat).EndsWith("/Floor.mat");
            Check(Mathf.Approximately(mat.GetFloat("_Variation"), indoor ? .009f : 0), "Indoor-only tone variation");
            Check(Mathf.Approximately(mat.GetFloat("_SeamWidth"), indoor ? .035f : 0), "Indoor-only lab joints");
            Check(!ShaderUtil.ShaderHasError(mat.shader), "Floor shader compiles");
            if(indoor)
            {
                int pattern=scene.name=="Level0"?4:scene.name=="Level2-Floor-1"?6:scene.name.StartsWith("Level2-")?5:scene.name.EndsWith("Floor3")?1:0;
                Check(Mathf.Approximately(mat.GetFloat("_Pattern"),pattern),"Region-specific flooring family");
            }
        }
        if(scene.name=="Level0")CheckFloorTones(floors.First().sharedMaterial);
        var table = all.FirstOrDefault(t => t.name.StartsWith("Table-Normal") && t.GetComponentsInChildren<MeshRenderer>(true).Any(r => r.name == LevelPropFloorRefinement.ShadowName)) ??
            shadows[0].parent;
        // Move the active camera/streaming region to authored furniture first.
        var mover = ZeldaRuntimeRegistry.GetControlledMover();
        if (mover)
        {
            typeof(VisualStyleLabSmoke).GetMethod("Move",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{mover,(Vector2)table.position+Vector2.down});
            for(int i=0;i<15;i++)yield return null;
        }
        // Unload/reload cannot leave a detached contact patch behind.
        var sample = table.GetComponentsInChildren<Transform>(true).First(t=>t.name==LevelPropFloorRefinement.ShadowName);
        var ancestorStates = new Dictionary<GameObject,bool>();
        for(var t=sample.parent;t;t=t.parent){ancestorStates[t.gameObject]=t.gameObject.activeSelf;t.gameObject.SetActive(true);}
        var parent = sample.parent; bool active = parent.gameObject.activeSelf;
        parent.gameObject.SetActive(false); Check(!sample.gameObject.activeInHierarchy,"Shadow unloads with base");
        parent.gameObject.SetActive(true); Check(sample.gameObject.activeInHierarchy,"Shadow returns with base");
        parent.gameObject.SetActive(active);
        foreach(var state in ancestorStates)state.Key.SetActive(state.Value);
        var simple = table.GetComponentsInChildren<SpriteRenderer>(true).Where(s=>s.sprite).ToArray();
        if(simple.Length>0)
        {
            CaptureComparison(table, "Props-"+scene.name);
            var chair = all.Where(t=>t&&t.name.StartsWith("Chair-Normal"))
                .OrderByDescending(t=>Mathf.Abs(Mathf.Sin(t.eulerAngles.z*2*Mathf.Deg2Rad))).FirstOrDefault();
            if(chair)CaptureComparison(chair,"Chair-"+scene.name);
        }
        // Concave tables must retain the inner curve, not an enclosing rectangle.
        if(scene.name=="Level2-Floor1")
        {
            var curve=all.FirstOrDefault(t=>t&&t.GetComponent<GeometricCurvedTable>()&&t.name.Contains("Quarter"));
            if(curve){curve.gameObject.SetActive(true);CaptureComparison(curve,"Curve-Level2-Floor1");}
        }
        typeof(VisualStyleLabSmoke).GetMethod("Capture",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{"PropsGameplay-"+scene.name});
        Debug.Log("PROP_FLOOR_RUNTIME_PASS "+scene.name+" shadows="+shadows.Length+" checks="+checks);
    }

    static void CaptureComparison(Transform target, string name)
    {
        var enabledBefore = new Dictionary<GameObject,bool>();
        for(var t=target;t;t=t.parent){enabledBefore[t.gameObject]=t.gameObject.activeSelf;t.gameObject.SetActive(true);}
        // Far-away furniture sprite parts can be individually suspended by
        // streaming; a diagnostic camera alone does not wake those parts.
        foreach(var t in target.GetComponentsInChildren<Transform>(true))
            if(!enabledBefore.ContainsKey(t.gameObject)){enabledBefore[t.gameObject]=t.gameObject.activeSelf;t.gameObject.SetActive(true);}
        var masks=Object.FindObjectsOfType<MeshRenderer>().Where(r=>r.enabled&&(r.name=="Circular Vision Overlay"||r.name=="Visibility Polygon Stencil"||r.name=="AI Vision Cone")).ToArray();
        foreach(var r in masks)r.enabled=false;
        var renderers=target.GetComponentsInChildren<Renderer>(true).Where(r=>r.name!=LevelPropFloorRefinement.ShadowName&&!(r is ParticleSystemRenderer)).ToArray();
        if(renderers.Length==0)return;
        var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
        var go=new GameObject("Prop diagnostic camera");var camera=go.AddComponent<Camera>();camera.enabled=false;
        camera.orthographic=true;camera.orthographicSize=Mathf.Max(.75f,Mathf.Max(bounds.size.x,bounds.size.y)*.65f);camera.aspect=1;
        camera.transform.position=bounds.center+Vector3.back*30;camera.cullingMask=~(1<<LayerMask.NameToLayer("UI"));
        camera.backgroundColor=new Color(.1f,.18f,.27f);camera.clearFlags=CameraClearFlags.SolidColor;
        var rt=new RenderTexture(640,640,24);camera.targetTexture=rt;
        var contacts=target.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.name==LevelPropFloorRefinement.ShadowName).ToArray();
        foreach(var r in contacts)r.enabled=false;
        var before=Render(camera,rt);
        foreach(var r in contacts)r.enabled=true;
        var after=Render(camera,rt);
        int darkened=0,brightened=0;
        for(int i=0;i<before.Length;i++)
        {
            float a=before[i].r+before[i].g+before[i].b,b=after[i].r+after[i].g+after[i].b;
            if(b<a-.005f)darkened++;else if(b>a+.005f)brightened++;
        }
        Check(contacts.Length>0,"Diagnostic has contact shadow");
        Check(darkened>15,"Contact visibly darkens ground outside object: "+name+" pixels="+darkened);
        Check(brightened==0,"Contact never generates bright abnormal edges: "+name);
        typeof(VisualStyleLabSmoke).GetMethod("Write",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{rt,name});
        Debug.Log("PROP_PIXEL_PASS "+name+" shadedPixels="+darkened+" brightArtifacts="+brightened);
        camera.targetTexture=null;rt.Release();Object.Destroy(rt);Object.Destroy(go);
        foreach(var r in masks)if(r)r.enabled=true;
        foreach(var state in enabledBefore)if(state.Key)state.Key.SetActive(state.Value);
    }
    static Color[] Render(Camera camera, RenderTexture rt)
    {
        camera.Render();var old=RenderTexture.active;RenderTexture.active=rt;
        var tex=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);tex.Apply();
        var pixels=tex.GetPixels();RenderTexture.active=old;Object.Destroy(tex);return pixels;
    }
    static void CheckFloorTones(Material original)
    {
        // Render an isolated test surface. Compare tone variation only, with
        // identical seam/facet settings; it must add both neighboring tones,
        // leave most of the base unchanged, and remain stationary over frames.
        var material=new Material(original);
        var surface=new GameObject("Floor color QA",typeof(MeshFilter),typeof(MeshRenderer));surface.layer=31;
        var mesh=new Mesh();mesh.vertices=new[]{new Vector3(-32,-32),new Vector3(32,-32),new Vector3(32,32),new Vector3(-32,32)};
        mesh.triangles=new[]{0,1,2,0,2,3};mesh.colors=Enumerable.Repeat(new Color(.14f,.23f,.3f),4).ToArray();mesh.uv=new Vector2[4];mesh.RecalculateBounds();
        surface.GetComponent<MeshFilter>().sharedMesh=mesh;surface.GetComponent<MeshRenderer>().sharedMaterial=material;
        var go=new GameObject("Floor QA camera");var camera=go.AddComponent<Camera>();camera.enabled=false;
        camera.orthographic=true;camera.orthographicSize=28;camera.aspect=1;camera.cullingMask=1<<31;camera.transform.position=Vector3.back*20;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
        var rt=new RenderTexture(256,256,24);camera.targetTexture=rt;
        material.SetFloat("_Variation",0);var plain=Render(camera,rt);
        material.SetFloat("_Variation",.009f);var varied=Render(camera,rt);var repeat=Render(camera,rt);
        int up=0,down=0,same=0;bool stationary=true,subtle=true;
        for(int i=0;i<plain.Length;i++)
        {
            float difference=varied[i].grayscale-plain[i].grayscale;
            if(difference>.002f)up++;else if(difference<-.002f)down++;else same++;
            stationary&=varied[i]==repeat[i];subtle&=Mathf.Abs(difference)<.025f;
        }
        Check(stationary,"Floor texture is static, no temporal shimmer");
        Check(subtle,"Neighboring tones remain subtle");
        Check(up>1000&&down>1000&&same>plain.Length/2,"Both neighboring tones are sparse, base dominates: "+up+"/"+down+"/"+same);
        Debug.Log("FLOOR_PIXEL_PASS brighter="+up+" darker="+down+" base="+same);
        camera.targetTexture=null;rt.Release();foreach(var o in new Object[]{go,surface,mesh,material,rt})Object.Destroy(o);
    }
}
#endif
