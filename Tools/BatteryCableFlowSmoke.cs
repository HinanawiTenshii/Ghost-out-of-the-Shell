// Isolated Unity GPU smoke test. Include BatteryCableFlow.shader and the production
// BuildFlowStrip method as FlowGeometry.BuildFlowStrip in the test project only.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
public static class BatteryCableFlowSmoke
{
    static Camera camera;
    static Material material;
    static RenderTexture target;
    static Texture2D image;
    static int checks;
    static void Check(bool value, string message)
    {
        checks++;
        if (!value) throw new Exception(message);
    }
    static Color[] Render(float distance, bool visible = true)
    {
        material.SetFloat("_FlowTravel", distance);
        surface.GetComponent<MeshRenderer>().enabled = visible;
        camera.Render();
        RenderTexture.active = target;
        image.ReadPixels(new Rect(0, 0, 800, 300), 0, 0); image.Apply();
        return image.GetPixels();
    }
    static GameObject surface;
    static Vector2 Center(Color[] colors)
    {
        float count=0,x=0,y=0;
        for(int i=0;i<colors.Length;i++)
        {
            Color c=colors[i];
            if(c.r<.12f)continue;
            Check(c.r>=c.b && c.r>=c.g,"Warm red flow; never blue");
            count++; x+=i%800; y+=i/800;
        }
        Check(count>20,"Visible pulse should occupy enough pixels");
        return new Vector2(x/count,y/count);
    }
    public static void Run()
    {
        try
        {
            Shader shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/BatteryCableFlow.shader");
            Check(shader!=null && shader.isSupported,"Flow shader supported");
            foreach(var msg in ShaderUtil.GetShaderMessages(shader))
                Check(msg.severity!=UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error,msg.message);
            material=new Material(shader);
            material.SetFloat("_PulseSpacing",4.8f);
            material.SetFloat("_PulseLength",.75f);
            material.SetFloat("_Intensity",1.2f);
            var positions=new List<Vector2>();var uv=new List<Vector2>();var tris=new List<int>();
            FlowGeometry.BuildFlowStrip(new[]{new Vector2(0,0),new Vector2(3,0),new Vector2(3,2),new Vector2(8,2)},.256f,positions,uv,tris);
            var vertices=new List<Vector3>();foreach(var p in positions)vertices.Add(new Vector3(p.x,p.y,0));
            var mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(tris,0);mesh.RecalculateBounds();
            surface=new GameObject("Flow strip",typeof(MeshFilter),typeof(MeshRenderer));surface.layer=30;
            surface.GetComponent<MeshFilter>().sharedMesh=mesh;
            surface.GetComponent<MeshRenderer>().sharedMaterial=material;
            camera=new GameObject("Camera").AddComponent<Camera>();camera.transform.position=new Vector3(4,1,-10);
            camera.orthographic=true;camera.orthographicSize=1.5f;camera.aspect=8f/3f;
            camera.cullingMask=1<<30;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
            target=new RenderTexture(800,300,24,RenderTextureFormat.ARGB32);camera.targetTexture=target;
            image=new Texture2D(800,300,TextureFormat.RGBA32,false);
            var zero=Render(0);foreach(var c in zero)Check(c.r<.01f,"No pre-lit waves ahead of source");
            var early=Center(Render(1.1f));var later=Center(Render(2.1f));
            Check(later.x>early.x+85 && Mathf.Abs(later.y-early.y)<1,"Flows battery to device, not backwards");
            var corner=Center(Render(3.9f));
            Check(corner.y>later.y+45 && corner.x>290 && corner.x<310,"Follows right-angle turn without diagonal shortcut");
            Render(3.9f);System.IO.File.WriteAllBytes(Application.dataPath+"/../flow-corner.png",image.EncodeToPNG());
            var periodic=Render(6.3f);int source=0,destination=0;
            for(int i=0;i<periodic.Length;i++)if(periodic[i].r>.12f){if(i%800<240)source++;if(i%800>380)destination++;}
            Check(source>20 && destination>20,"Separated periodic pulses on long cable");
            Render(6.3f);System.IO.File.WriteAllBytes(Application.dataPath+"/../flow-periodic.png",image.EncodeToPNG());
            var dark=Render(6.3f,false);foreach(var c in dark)Check(c.r<.01f,"Power-off renderer must leave no residual glow");
            Debug.Log("BATTERY_FLOW_GPU_PASS checks="+checks+" early="+early+" later="+later+" corner="+corner);
            EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
