using System;
using System.Reflection;
using UnityEngine;
using UnityEditor;
public static class ElectricGateSignalSmoke
{
    static readonly BindingFlags Flags = BindingFlags.Instance|BindingFlags.NonPublic;
    static GameObject Lamp(float x, ElectricGateRules.SignalState state)
    {
        var go=new GameObject("Lamp "+state);go.transform.position=new Vector3(x,0,0);
        var pillar=go.AddComponent<ElectricGatePillar>();pillar.Signal=state;
        var visual=go.AddComponent<ElectricGatePillarVisual>();
        var texture=new Texture2D(2,2);texture.SetPixels(new[]{Color.white,Color.white,Color.white,Color.white});texture.Apply();
        var sprite=Sprite.Create(texture,new Rect(0,0,2,2),new Vector2(.5f,.5f),2);
        typeof(ElectricGatePillarVisual).GetField("square",Flags).SetValue(visual,sprite);
        typeof(ElectricGatePillarVisual).GetMethod("Rebuild",Flags).Invoke(visual,null);
        // The CPU suite exercises ApplySignal in playing mode; this editor render
        // enables the exact generated halo without entering or touching a game scene.
        var halo=(MeshRenderer)typeof(ElectricGatePillarVisual).GetField("glowRenderer",Flags).GetValue(visual);
        halo.enabled=state!=ElectricGateRules.SignalState.Off;
        halo.sharedMaterial.SetColor("_Color",ElectricGatePillarVisual.IndicatorColor(state));
        halo.sharedMaterial.SetFloat("_Intensity",1.15f);
        return go;
    }
    static void SampleGlow(float x, Color color, Shader shader, bool radial)
    {
        var go=new GameObject("Current shader probe",typeof(MeshFilter),typeof(MeshRenderer));
        go.transform.position=new Vector3(x,.95f,0);
        var mesh=new Mesh();
        float w=radial?.2f:.55f,h=radial?.2f:.16f;
        mesh.vertices=new[]{new Vector3(-w,-h),new Vector3(-w,h),new Vector3(w,h),new Vector3(w,-h)};
        mesh.uv=new[]{Vector2.zero,Vector2.up,Vector2.one,Vector2.right};
        mesh.colors32=new Color32[]{color,color,color,color};mesh.triangles=new[]{0,1,2,0,2,3};
        go.GetComponent<MeshFilter>().sharedMesh=mesh;
        var mat=new Material(shader);mat.SetFloat("_Radial",radial?1:0);mat.SetFloat("_CoreFraction",.2f);
        go.GetComponent<MeshRenderer>().sharedMaterial=mat;
    }
    public static void Run()
    {
        try
        {
            Shader shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/ElectricGateGlow.shader");
            if(shader==null||!shader.isSupported)throw new Exception("Gate shader unsupported");
            Lamp(-1.7f,ElectricGateRules.SignalState.Off);Lamp(0,ElectricGateRules.SignalState.Ready);Lamp(1.7f,ElectricGateRules.SignalState.Passage);
            SampleGlow(0,new Color(1,.14f,.08f),shader,false);SampleGlow(1.7f,new Color(.12f,.72f,1),shader,false);
            SampleGlow(-.65f,new Color(1,.14f,.08f),shader,true);SampleGlow(2.35f,new Color(.12f,.72f,1),shader,true);
            foreach(var s in new[]{shader,Resources.Load<Shader>("WarmLightGlow")})
                foreach(var msg in ShaderUtil.GetShaderMessages(s))
                    if(msg.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)throw new Exception(msg.message);
            var camera=new GameObject("Camera").AddComponent<Camera>();camera.transform.position=new Vector3(.15f,0,-10);
            camera.orthographic=true;camera.orthographicSize=1.4f;camera.aspect=2;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.04f,.055f,.08f);
            var target=new RenderTexture(800,400,24);camera.targetTexture=target;camera.Render();
            RenderTexture.active=target;var image=new Texture2D(800,400,TextureFormat.RGBA32,false);
            image.ReadPixels(new Rect(0,0,800,400),0,0);image.Apply();
            int red=0,blue=0,wrongBlue=0,wrongRed=0,offBright=0,cyanProbe=0;
            var pixels=image.GetPixels();
            for(int i=0;i<pixels.Length;i++){
                float x=(i%800+.5f)/800*5.6f-2.65f,y=(i/800+.5f)/400*2.8f-1.4f;Color c=pixels[i];
                if(y<.6f&&y>-.6f){
                    if(Mathf.Abs(x)<.55f){if(c.r>c.b+.15f)red++;if(c.b>c.r+.15f)wrongBlue++;}
                    if(Mathf.Abs(x-1.7f)<.55f){if(c.b>c.r+.15f)blue++;if(c.r>c.b+.15f)wrongRed++;}
                    if(Mathf.Abs(x+1.7f)<.12f&&Mathf.Abs(y)<.10f&&c.r>.25f)offBright++;
                }
                if(y>.8f&&x>1.1f&&c.b>c.r+.1f)cyanProbe++;
            }
            if(red<200||blue<200||wrongBlue>0||wrongRed>0||offBright>0||cyanProbe<100)throw new Exception("Incorrect state color / glow output");
            System.IO.File.WriteAllBytes(Application.dataPath+"/../signal-preview.png",image.EncodeToPNG());
            Debug.Log("GATE_SIGNAL_GPU_PASS red="+red+" blue="+blue+" wrongBlue="+wrongBlue+" wrongRed="+wrongRed+" offBright="+offBright+" cyanCurrent="+cyanProbe);
            EditorApplication.Exit(0);
        }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
