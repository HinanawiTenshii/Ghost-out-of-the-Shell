using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Isolated Unity editor/GPU test using production pillar, saved state and visual code.
public static class ElectricGatePermissionLabelSmoke
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static int checks;
    static readonly List<GameObject> roots = new List<GameObject>();
    static T Get<T>(object obj, string field) => (T)obj.GetType().GetField(field, Private).GetValue(obj);
    static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Private).SetValue(obj, value);
    static void Call(object obj, string method, params object[] args) => obj.GetType().GetMethod(method, Private).Invoke(obj, args);
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    static ElectricGatePillarVisual Make(Sprite square, Vector2 position)
    {
        var root = new GameObject("Electric Pillar Permission Test"); roots.Add(root); root.transform.position = position;
        root.AddComponent<ElectricGatePillar>();
        var visual = root.AddComponent<ElectricGatePillarVisual>(); Set(visual,"square",square); Call(visual,"Rebuild");
        return visual;
    }
    static void State(ElectricGatePillarVisual visual, int level, ElectricGateRules.SignalState signal,
        ElectricGatePillar.PassageMode mode = ElectricGatePillar.PassageMode.Permission)
    {
        var pillar=visual.GetComponent<ElectricGatePillar>();
        pillar.ApplyRuntimeState(new ElectricGatePillar.RuntimeState { mode=mode, minimumPermission=level, individualId="test", individualName="Test" });
        Call(pillar,"ResetSignal"); Call(pillar,"ReportSignal",signal); Call(visual,"LateUpdate");
    }
    public static void Run()
    {
        try
        {
            var texture=new Texture2D(2,2); texture.SetPixels(new[]{Color.white,Color.white,Color.white,Color.white}); texture.Apply();
            var square=Sprite.Create(texture,new Rect(0,0,2,2),Vector2.one*.5f,2);
            var visual=Make(square,Vector2.zero); var pillar=visual.GetComponent<ElectricGatePillar>();
            var originalCollider=pillar.GetComponent<BoxCollider2D>(); Vector2 colliderSize=originalCollider.size;
            var mesh=Get<Mesh>(visual,"permissionMesh"); var material=Get<Material>(visual,"permissionMaterial");
            var renderer=Get<MeshRenderer>(visual,"permissionRenderer");
            var patterns=GetStaticPatterns(); var observed=new HashSet<string>();
            for (int level=-2;level<=9;level++)
            {
                State(visual,level,ElectricGateRules.SignalState.Ready);
                Check(renderer.enabled && pillar.MinimumPermission==level && Get<int>(visual,"displayedPermission")==level,"All supported levels update");
                Check(mesh==Get<Mesh>(visual,"permissionMesh") && material==Get<Material>(visual,"permissionMaterial"),"Changing number reuses mesh/material");
                int cells=0; string text=level.ToString();
                foreach(char c in text) foreach(char pixel in c=='-'?"000000111000000":patterns[c-'0']) if(pixel=='1') cells++;
                Check(mesh.vertexCount==cells*4 && mesh.triangles.Length==cells*6,"Correct cell count for "+level);
                Check(mesh.vertexCount<=80 && mesh.bounds.size.x<=.281f && mesh.bounds.size.y<=.301f,"One small mesh stays inside lamp");
                Check(Mathf.Abs(mesh.bounds.center.x)<.0001f && Mathf.Abs(mesh.bounds.center.y)<.0001f,"Signed and unsigned numbers centered");
                foreach(var vertex in mesh.vertices) Check(Mathf.Abs(vertex.x)<=.141f && Mathf.Abs(vertex.y)<=.151f,"Digits cannot cover lamp caps");
                var triangles=mesh.triangles; var vertices=mesh.vertices;
                for(int i=0;i<triangles.Length;i+=3)
                    Check(Vector3.Cross(vertices[triangles[i+1]]-vertices[triangles[i]],vertices[triangles[i+2]]-vertices[triangles[i]]).z<0,"Triangles face game camera");
                observed.Add(string.Join(";",Array.ConvertAll(mesh.vertices,v=>v.ToString("F3"))));
            }
            Check(observed.Count==12,"All twelve permission values have distinct shapes");
            foreach(ElectricGateRules.SignalState signal in Enum.GetValues(typeof(ElectricGateRules.SignalState)))
            {
                State(visual,3,signal);
                Color lamp=ElectricGatePillarVisual.IndicatorColor(signal), digit=material.color;
                Check(digit.r>=lamp.r && digit.g>=lamp.g && digit.b>=lamp.b,"Digits paler than lamp in "+signal);
                Check(Mathf.Abs(digit.r-Mathf.Lerp(lamp.r,1,.48f))<.0001f && Mathf.Abs(digit.g-Mathf.Lerp(lamp.g,1,.48f))<.0001f,"Digits derive from current signal hue");
                Check(digit.a==1 && renderer.enabled,"Opaque readable label, including unpowered state");
            }
            State(visual,5,ElectricGateRules.SignalState.Passage,ElectricGatePillar.PassageMode.Individual);
            Check(!renderer.enabled,"Individual mode hides level");
            string saved=JsonUtility.ToJson(new ElectricGatePillar.RuntimeState { mode=ElectricGatePillar.PassageMode.Permission, minimumPermission=-1 });
            pillar.ApplyRuntimeState(JsonUtility.FromJson<ElectricGatePillar.RuntimeState>(saved)); Call(visual,"LateUpdate");
            Check(renderer.enabled && Get<int>(visual,"displayedPermission")==-1,"Save-state restoration updates label");
            Set(pillar,"minimumPermission",7); Call(visual,"LateUpdate");
            Check(Get<int>(visual,"displayedPermission")==7,"Inspector changes update existing scene instances");
            State(visual,100,ElectricGateRules.SignalState.Ready);
            Check(Get<int>(visual,"displayedPermission")==9,"Runtime clamp respected");
            Check(originalCollider==pillar.GetComponent<BoxCollider2D>() && originalCollider.size==colliderSize && roots[0].GetComponentsInChildren<Collider2D>().Length==1,"No collider changes");
            Check(renderer.sortingOrder==7 && renderer.sortingOrder>Get<MeshRenderer>(visual,"glowRenderer").sortingOrder && renderer.sortingOrder<30000,"Digits above local halo, below vision mask");
            roots[0].transform.rotation=Quaternion.Euler(0,0,90); roots[0].transform.localScale=new Vector3(1.3f,.7f,1);
            Check(renderer.transform.IsChildOf(pillar.transform) && Vector3.Distance(renderer.transform.position,pillar.transform.position)<.001f,"Transform follows lamp center");
            roots[0].SetActive(false); Check(mesh==null && material==null,"Disable releases display assets");
            roots[0].SetActive(true); Check(roots[0].GetComponentsInChildren<MeshRenderer>().Length==2,"Re-enable creates only halo and one digit renderer");
            UnityEngine.Object.DestroyImmediate(roots[0]); roots.Clear();

            // Six production lamp states in a single real GPU preview.
            int[] levels={-2,3,3,0,9,3};
            var signals=new[]{ElectricGateRules.SignalState.Off,ElectricGateRules.SignalState.Ready,ElectricGateRules.SignalState.Passage,
                ElectricGateRules.SignalState.Ready,ElectricGateRules.SignalState.Warning,ElectricGateRules.SignalState.Ready};
            for(int i=0;i<6;i++)
            {
                var lamp=Make(square,new Vector2((i%3-1)*1.4f,i<3?.72f:-.72f));
                State(lamp,levels[i],signals[i],i==5?ElectricGatePillar.PassageMode.Individual:ElectricGatePillar.PassageMode.Permission);
                // Display the production halo while remaining in edit mode.
                var halo=Get<MeshRenderer>(lamp,"glowRenderer"); halo.enabled=i!=0;
                halo.sharedMaterial.SetColor("_Color",ElectricGatePillarVisual.IndicatorColor(signals[i])); halo.sharedMaterial.SetFloat("_Intensity",1.15f);
            }
            var camera=new GameObject("Label Preview Camera").AddComponent<Camera>(); camera.orthographic=true; camera.orthographicSize=1.5f;
            camera.transform.position=new Vector3(0,0,-10); camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.065f,.09f,.15f);
            var target=new RenderTexture(960,600,24); camera.targetTexture=target; camera.aspect=1.6f;
            var image=new Texture2D(960,600,TextureFormat.RGBA32,false); Capture(camera,target,image);
            var withDigits=image.GetPixels(); File.WriteAllBytes(Application.dataPath+"/../ElectricGatePermission-preview.png",image.EncodeToPNG());
            foreach(var root in roots) Get<MeshRenderer>(root.GetComponent<ElectricGatePillarVisual>(),"permissionRenderer").enabled=false;
            Capture(camera,target,image); var withoutDigits=image.GetPixels();
            for(int column=0;column<3;column++)
            {
                int changed=0;
                for(int y=390;y<500;y++) for(int x=150+column*280;x<250+column*280;x++)
                { int p=y*960+x; if(withDigits[p].r>withoutDigits[p].r+.03f || withDigits[p].g>withoutDigits[p].g+.03f) changed++; }
                Check(changed>150,"Actual GPU draws readable pale digits in lamp state "+signals[column]);
            }
            foreach(var root in roots) Call(root.GetComponent<ElectricGatePillarVisual>(),"UpdatePermissionLabel");
            var mask=GameObject.CreatePrimitive(PrimitiveType.Quad); mask.transform.localScale=new Vector3(6,4,1);
            var maskMaterial=new Material(Shader.Find("Sprites/Default")); maskMaterial.color=Color.black;
            mask.GetComponent<MeshRenderer>().sharedMaterial=maskMaterial; mask.GetComponent<MeshRenderer>().sortingOrder=30000;
            Capture(camera,target,image); Check(image.GetPixel(480,444).r<.01f && image.GetPixel(480,444).g<.01f,"Vision overlay covers numeric display");
            UnityEngine.Object.DestroyImmediate(mask); UnityEngine.Object.DestroyImmediate(maskMaterial);
            foreach(var root in roots) UnityEngine.Object.DestroyImmediate(root); roots.Clear();
            camera.targetTexture=null; RenderTexture.active=null;
            UnityEngine.Object.DestroyImmediate(camera.gameObject); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(square); UnityEngine.Object.DestroyImmediate(texture);
            Debug.Log("GATE_PERMISSION_LABEL_PASS checks="+checks); EditorApplication.Exit(0);
        }
        catch(Exception e){ Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static string[] GetStaticPatterns() => (string[])typeof(ElectricGatePillarVisual).GetField("DigitPixels",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
    static void Capture(Camera camera, RenderTexture target, Texture2D image)
    {
        camera.Render(); RenderTexture.active=target; image.ReadPixels(new Rect(0,0,960,600),0,0); image.Apply();
    }
}
