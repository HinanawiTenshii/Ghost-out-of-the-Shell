using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Isolated Unity 2021 editor GPU check; no production scene is opened or saved.
public static class BrazierFireSmoke
{
    static int checks;
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
    static void Set(object owner, string name, object value) { owner.GetType().GetField(name, Private).SetValue(owner, value); }
    static T Get<T>(object owner, string name) { return (T)owner.GetType().GetField(name, Private).GetValue(owner); }
    static void Call(object owner, string name, params object[] args) { owner.GetType().GetMethod(name, Private).Invoke(owner, args); }

    public static void Run()
    {
        try
        {
            var root = new GameObject("Brazier lifecycle preview");
            var visual = root.AddComponent<GeometricFortressDecoration>();
            Set(visual,"shape",GeometricFortressDecoration.FurnitureShape.Brazier);
            Set(visual,"size",new Vector2(1.3f,1.3f));
            Set(visual,"accentColor",new Color(1,.56f,.12f,1));
            Call(visual,"Rebuild");
            var body = Get<Mesh>(visual,"mesh");
            var core = Get<Mesh>(visual,"fireMesh");
            var glow = Get<Mesh>(visual,"fireGlowMesh");
            Check(body.vertexCount==56,"No static fire remains on the stone/log mesh");
            Check(core.vertexCount==20 && glow.vertexCount==113,"Compact reusable geometry");
            Check(root.GetComponent<BoxCollider2D>().size==new Vector2(1.3f,1.3f),"Original collision size");
            var bodyBefore = body.vertices;
            var minAlpha = new byte[] {255,255,255,255,255};
            var maxAlpha = new byte[5];
            var minArea = new float[] {100,100,100,100,100};
            var maxArea = new float[5];
            for(int frame=0;frame<1000;frame++)
            {
                Call(visual,"UpdateFireSurface",frame*.023f);
                var v = core.vertices; var c = core.colors32; var g = glow.colors32;
                int visible=0;
                for(int i=0;i<5;i++)
                {
                    minAlpha[i]=Math.Min(minAlpha[i],c[i*4].a); maxAlpha[i]=Math.Max(maxAlpha[i],c[i*4].a);
                    if(c[i*4].a>50) visible++;
                    Check(g[i*8].a <= c[i*4].a*.18f+1,"A dying flame cannot leave a solid glowing ghost");
                    float area=0;
                    for(int j=0;j<4;j++)
                    {
                        Vector3 p=v[i*4+j], n=v[i*4+(j+1)%4];
                        Check(!float.IsNaN(p.x) && new Vector2(p.x,p.y).magnitude<=.4001f,"Flame stays in basin");
                        area+=p.x*n.y-p.y*n.x;
                        Vector3 after=v[i*4+(j+2)%4];
                        Check(Vector3.Cross(n-p,after-n).z>0,"Convex distorted flame, no flipped/folded triangles");
                        Vector3 previous=v[i*4+(j+3)%4];
                        Check(Mathf.Abs(Vector3.Dot((n-p).normalized,(previous-p).normalized))<.06f,"Flame corners stay near right angles throughout the lifecycle");
                        float ratio=(n-p).magnitude/(previous-p).magnitude;
                        Check(ratio>.88f && ratio<1.14f,"Flame stays near square rather than stretching into a long diamond");
                    }
                    minArea[i]=Mathf.Min(minArea[i],area);maxArea[i]=Mathf.Max(maxArea[i],area);
                }
                Check(visible>0,"Staggered fire never fully extinguishes");
                for(int j=89;j<113;j++) Check(g[j].a==0,"Firelight feather ends transparently");
            }
            for(int i=0;i<5;i++)
            {
                Check(minAlpha[i]==0 && maxAlpha[i]==255,"Every large flame including yellow center fully appears and disappears");
                Check(maxArea[i]>minArea[i]*8,"Large lifecycle shape change, not just jitter");
            }
            for(int i=0;i<bodyBefore.Length;i++) Check(bodyBefore[i]==body.vertices[i],"Stone and wood remain fixed");
            foreach(var r in root.GetComponentsInChildren<MeshRenderer>())
                Check(r.gameObject.layer==root.layer && r.sortingOrder<0,"World mask layer and under-character ordering");

            var camera=new GameObject("Preview Camera").AddComponent<Camera>();
            camera.transform.position=new Vector3(0,0,-10);camera.orthographic=true;camera.orthographicSize=1.02f;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.05f,.07f,.115f);
            var target=new RenderTexture(480,480,24);camera.targetTexture=target;camera.aspect=1;
            var image=new Texture2D(480,480,TextureFormat.RGBA32,false);
            string output=Path.GetFullPath(Application.dataPath+"/../frames");Directory.CreateDirectory(output);
            Call(visual,"UpdateFireSurface",.4f);
            var glowRenderer=Get<GameObject>(visual,"visual").transform.Find("Warm Fire Glow").GetComponent<MeshRenderer>();
            var lit=Capture(camera,target,image);
            glowRenderer.enabled=false;var unlit=Capture(camera,target,image);glowRenderer.enabled=true;
            int warmed=0;
            for(int y=0;y<480;y++) for(int x=0;x<480;x++)
            {
                float radius=new Vector2(x-240,y-240).magnitude/480*2.04f;
                int index=y*480+x;
                if(radius>.70f && radius<.86f && lit[index].r>unlit[index].r+.003f) warmed++;
            }
            Check(warmed>1000,"Warm halo lights beyond the stone silhouette");
            // A late-rendered world-space visibility mask must cover the additive firelight too.
            var mask=GameObject.CreatePrimitive(PrimitiveType.Quad);
            mask.transform.position=new Vector3(0,0,-1);mask.transform.localScale=new Vector3(4,4,1);
            var maskRenderer=mask.GetComponent<MeshRenderer>();maskRenderer.sharedMaterial=new Material(Shader.Find("Sprites/Default"));
            maskRenderer.sharedMaterial.color=Color.black;maskRenderer.sortingOrder=30000;
            var masked=Capture(camera,target,image);
            Check(masked[240*480+240].maxColorComponent<.01f || (masked[240*480+240].r<.01f && masked[240*480+240].g<.01f),"Vision mask covers fire and glow");
            UnityEngine.Object.DestroyImmediate(mask);
            Call(visual,"BuildFlames",Vector2.one,Get<Material>(visual,"ownedMaterial"));
            var particles=Get<ParticleSystem>(visual,"flames");particles.useAutoRandomSeed=false;particles.randomSeed=42;
            var ember=particles.GetComponent<ParticleSystemRenderer>().mesh;
            Check(ember.vertexCount==4 && Mathf.Abs(ember.bounds.size.x-ember.bounds.size.y)<.001f,"Small embers also use square geometry");
            for(int frame=0;frame<60;frame++)
            {
                Call(visual,"UpdateFireSurface",frame*.05f*1.6f);
                particles.Simulate(.05f,true,false,true);
                Capture(camera,target,image);
                File.WriteAllBytes(Path.Combine(output,"fire-"+frame.ToString("000")+".png"),image.EncodeToPNG());
            }
            root.transform.localScale=new Vector3(1.7f,.6f,1);root.transform.rotation=Quaternion.Euler(0,0,35);
            Call(visual,"UpdateFireSurface",.6f);
            Check(core.bounds.size.x<.81f && core.bounds.size.y<.81f,"Geometry follows root transform without double scaling");
            UnityEngine.Object.DestroyImmediate(root);
            Check(core==null && glow==null && body==null,"Generated meshes cleaned on destroy");
            Debug.Log("BRAZIER_FIRE_GPU_PASS checks="+checks+" haloPixels="+warmed+" frames="+output);
            EditorApplication.Exit(0);
        }
        catch(Exception e) { Debug.LogException(e);EditorApplication.Exit(1); }
    }
    static Color[] Capture(Camera camera,RenderTexture target,Texture2D image)
    {
        camera.Render();RenderTexture.active=target;
        image.ReadPixels(new Rect(0,0,480,480),0,0);image.Apply();return image.GetPixels();
    }
}
