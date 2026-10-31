#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static partial class LevelVisualRollout
{
    const string Art="Assets/Art/LevelTerminalStyle";
    static Material contact;
    static Mesh foot;
    static Shader vision, floorShader;
    static readonly BindingFlags Private=BindingFlags.Static|BindingFlags.NonPublic;
    static void Set(UnityEngine.Object target,string name,Action<SerializedProperty> write)
    {
        var so=new SerializedObject(target); var p=so.FindProperty(name);
        if(p==null)throw new Exception(target.GetType().Name+" missing "+name);
        write(p); so.ApplyModifiedPropertiesWithoutUndo();
        if(PrefabUtility.IsPartOfPrefabInstance(target))PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }
    static void Number(UnityEngine.Object c,string n,float f)=>Set(c,n,p=>p.floatValue=f);
    static void Ref(UnityEngine.Object c,string n,UnityEngine.Object value)=>Set(c,n,p=>p.objectReferenceValue=value);
    static bool Under(Transform t,params string[] names)
    { for(;t!=null;t=t.parent) if(names.Any(n=>t.name.Equals(n,StringComparison.OrdinalIgnoreCase))) return true; return false; }
    static bool Wall(SpriteRenderer s)=>s.enabled&&s.sprite&&s.sprite.name=="Square"&&s.gameObject.layer==LayerMask.NameToLayer("Blocks")&&
        s.color.a>.9f&&Under(s.transform,"Walls")&&!s.GetComponentInParent<DoorData>()&&s.GetComponent<BoxCollider2D>()&&!s.GetComponent<BoxCollider2D>().isTrigger;
    static bool Floor(SpriteRenderer s)=>s.enabled&&s.sprite&&s.sprite.name=="Square"&&s.sortingOrder<=-6&&
        Under(s.transform,"Ground","Grounds","Roads")&&!s.GetComponentInParent<DoorData>()&&
        !s.GetComponent<SpriteWaterFlowParticles>()&&!s.GetComponent<SpriteStillWaterSurface>()&&
        (s.sharedMaterial==null||s.sharedMaterial.shader.name=="Sprites/Default");
    static Color Theme(string n)
    {
        if(n=="Level0")return new Color32(153,166,177,255);
        if(n=="Level1-Floor-1")return new Color32(157,165,174,255);
        if(n=="Level1-Floor1")return new Color32(177,176,165,255);
        if(n=="Level1-Floor2")return new Color32(173,178,177,255);
        if(n=="Level1-Floor3")return new Color32(182,184,184,255);
        if(n=="Level1-Garden")return new Color32(166,177,166,255);
        if(n=="Level2-Floor-1")return new Color32(151,166,174,255);
        if(n=="Level2-Floor1")return new Color32(177,174,160,255);
        if(n=="Level2-Floor2")return new Color32(174,181,186,255);
        return new Color32(187,175,151,255);
    }
    static Vector2 Tile(string n)
    {
        if(n=="Level0")return new Vector2(2.8f,1.9f);
        if(n.EndsWith("-1"))return new Vector2(3.4f,1.8f);
        if(n.Contains("Garden"))return new Vector2(2.8f,2.6f);
        if(n.Contains("Desert"))return new Vector2(4.5f,3.2f);
        if(n=="Level1-Floor3")return new Vector2(3.8f,3.8f);
        if(n=="Level1-Floor2")return new Vector2(3.2f,2.4f);
        if(n=="Level2-Floor2")return new Vector2(3.6f,2.6f);
        return new Vector2(3.2f,2.2f);
    }
    public static void Apply()
    {
        Isolate(); Directory.CreateDirectory(Art); AssetDatabase.Refresh();
        contact=AssetDatabase.LoadAssetAtPath<Material>(Art+"/Contact.mat");
        if(!contact){contact=new Material(Shader.Find("Sprites/Default"));AssetDatabase.CreateAsset(contact,Art+"/Contact.mat");}
        foot=AssetDatabase.LoadAssetAtPath<Mesh>(Art+"/Contact.asset");
        if(!foot){foot=ContactMesh();AssetDatabase.CreateAsset(foot,Art+"/Contact.asset");}
        vision=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Art/VisualStyleLab/VisionStudy.shader");
        floorShader=Shader.Find("Game/Terminal Floor Detail");
        if(!floorShader||!vision||ShaderUtil.ShaderHasError(floorShader))throw new Exception("Missing/invalid shaders");
        var signature=typeof(ApplyVisualStyleLabActorsProps).GetMethod("GameplayFields",Private);
        foreach(string path in Scenes)
        {
            var scene=EditorSceneManager.OpenScene(path);
            var all=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Component>(true)).Where(c=>c!=null&&(c.hideFlags&HideFlags.DontSave)==0).ToArray();
            if(all.OfType<LevelSceneVisualProfile>().Any()){Debug.Log("LEVEL_STYLE_ALREADY_APPLIED "+path);continue;}
            var unchanged=all.Where(c=>!(c is Renderer)&&!(c is MeshFilter)&&!(c is KeroseneStreetLamp)&&!(c is SpriteWaterFlowParticles)&&!(c is BatterySocketCable))
                .ToDictionary(c=>c,c=>(string)signature.Invoke(null,new object[]{c}));
            var colors=all.OfType<SpriteRenderer>().ToDictionary(s=>s,s=>s.color);
            var walls=all.OfType<SpriteRenderer>().Where(Wall).ToArray();
            var floors=all.OfType<SpriteRenderer>().Where(Floor).ToArray();
            var root=new GameObject("Level Terminal Presentation");
            var profile=root.AddComponent<LevelSceneVisualProfile>();profile.visionShader=vision;profile.contactMaterial=contact;profile.contactMesh=foot;
            root.AddComponent<VisualStyleLabTerminalPresentation>();
            string folder=Art+"/"+scene.name; Directory.CreateDirectory(folder);AssetDatabase.Refresh();
            foreach(var wall in walls)
            {
                wall.color=Theme(scene.name);
                if(PrefabUtility.IsPartOfPrefabInstance(wall))PrefabUtility.RecordPrefabInstancePropertyModifications(wall);
            }
            var groundMat=AssetDatabase.LoadAssetAtPath<Material>(folder+"/Floor.mat");
            if(!groundMat){groundMat=new Material(floorShader){name=scene.name+" restrained floor seams"};AssetDatabase.CreateAsset(groundMat,folder+"/Floor.mat");}
            var tile=Tile(scene.name);groundMat.SetVector("_Tile",new Vector4(tile.x,tile.y,0,0));
            groundMat.SetFloat("_Contrast",.055f);
            groundMat.SetFloat("_Pattern",scene.name.Contains("Garden")?2:scene.name.Contains("Desert")?3:scene.name.EndsWith("Floor3")?1:0);
            var roadMat=AssetDatabase.LoadAssetAtPath<Material>(folder+"/Road.mat");
            if(!roadMat){roadMat=new Material(groundMat);AssetDatabase.CreateAsset(roadMat,folder+"/Road.mat");}
            roadMat.SetVector("_Tile",new Vector4(3.2f,1.6f,0,0));roadMat.SetFloat("_Pattern",0);roadMat.SetFloat("_Contrast",.045f);
            foreach(var floor in floors)
            {
                floor.sharedMaterial=Under(floor.transform,"Roads")?roadMat:groundMat;
                if(PrefabUtility.IsPartOfPrefabInstance(floor))PrefabUtility.RecordPrefabInstancePropertyModifications(floor);
            }
            BakeWalls(root.transform,walls,folder,Theme(scene.name));
            int plants=0,signals=0,shadows=0;
            foreach(var c in all)
            {
                if(c is GeometricTreeDecoration)
                {
                    var wind=c.gameObject.AddComponent<VisualStyleLabPlantMotion>();
                    Set(wind,"meshCanopy",p=>p.boolValue=true);Number(wind,"phase",Mathf.Abs(c.transform.position.x+c.transform.position.y)%8);plants++;
                }
                if(c is KeroseneStreetLamp)
                {
                    Number(c,"radiusBreathing",.018f);Number(c,"alphaBreathing",.07f);Number(c,"breathingPeriod",4.6f);
                    Number(c,"glowIntensity",2.3f);Number(c,"glowRadiusMultiplier",1.2f);
                }
                if(c is SpriteWaterFlowParticles)
                {
                    Number(c,"speed",.42f);Number(c,"speedRandomness",.16f);Number(c,"directionSpread",4);
                    Number(c,"lifetime",3);Number(c,"particlesPerSquareUnit",1.4f);Number(c,"ribbonDensityMultiplier",.12f);
                    Number(c,"particleSize",.04f);Number(c,"ribbonLengthScale",6);Number(c,"ribbonWidthScale",2);
                    Number(c,"shapeAnimationSpeed",.45f);Number(c,"currentSway",.035f);
                }
                if(c is BatterySocketCable)
                {Number(c,"currentFlowLength",.35f);Number(c,"currentFlowInterval",1.6f);Number(c,"currentFlowSpeed",1.8f);Number(c,"currentFlowIntensity",.65f);}
                if(c is BatterySocket||c is WaterwayGateController||c is WaterwayGate||c is LeverData)
                {
                    var signal=c.gameObject.AddComponent<VisualStyleLabMechanismSignal>();Ref(signal,"displayMaterial",contact);
                    string field=c is BatterySocket?"socket":c is WaterwayGateController?"controller":c is WaterwayGate?"gate":"lever";
                    Ref(signal,field,c);
                    Vector2 pos=c is BatterySocket?new Vector2(0,.425f):c is WaterwayGateController?new Vector2(0,-.5f):c is WaterwayGate?new Vector2(-1.71f,.4f):new Vector2(0,-.025f);
                    Set(signal,"localPosition",p=>p.vector2Value=pos);
                    if(c is LeverData)
                    {
                        var so=new SerializedObject(c);
                        foreach(string link in new[]{"linkedObject","linkedObject2","linkedObject3"})
                        {
                            var target=so.FindProperty(link).objectReferenceValue as GameObject;
                            if(target&&target.TryGetComponent<DoubleSlidingDoor>(out var door)) {Ref(signal,"door",door);break;}
                        }
                    }
                    signals++;
                }
                // Movable/breakable props keep their own contact; never bake a shadow into the ground.
                if(c is DoorData && !c.GetComponentInParent<ZeldaCharacterData>() && c.name.Contains("Bush"))
                {
                    var wind=c.gameObject.AddComponent<VisualStyleLabPlantMotion>();Number(wind,"amplitude",.035f);plants++;
                }
            }
            foreach(var s in all.OfType<SpriteRenderer>())
            {
                if(!s.enabled||!s.sprite||s.color.a<.75f||s.GetComponentInParent<ZeldaCharacterData>()||s.GetComponentInParent<PickupItemBase>())continue;
                bool prop=s.transform.parent!=null&&(s.transform.parent.name.StartsWith("Table")||s.transform.parent.name.StartsWith("Chair"))&&s.name=="Square";
                if(!prop)continue;
                var owner=s.transform.parent.gameObject;
                if(owner.GetComponent<LevelContactShadow>())continue;
                owner.AddComponent<LevelContactShadow>().Configure(foot,contact,false);shadows++;
            }
            foreach(var p in unchanged)
                if(!p.Key||p.Value!=(string)signature.Invoke(null,new object[]{p.Key}))throw new Exception("Gameplay changed "+scene.name+" "+p.Key);
            foreach(var pair in colors)if(!walls.Contains(pair.Key)&&pair.Key.color!=pair.Value)throw new Exception("Non-wall palette changed: "+pair.Key.name);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Debug.Log("LEVEL_STYLE_PASS "+scene.name+" walls="+walls.Length+" floors="+floors.Length+" plants="+plants+" signals="+signals+" propShadows="+shadows+" originalGameplay=unchanged nonWallColors=unchanged");
        }
    }
    static Mesh ContactMesh()
    {
        var v=new List<Vector3>();var co=new List<Color>();var t=new List<int>();
        v.Add(Vector3.zero);co.Add(new Color(.02f,.035f,.06f,.25f));
        for(int ring=0;ring<2;ring++)for(int i=0;i<16;i++)
        {float a=i*Mathf.PI/8;float r=ring==0?.38f:.5f;v.Add(new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r));co.Add(new Color(.02f,.035f,.06f,ring==0?.25f:0));}
        for(int i=0;i<16;i++){int a=1+i,b=1+(i+1)%16;t.AddRange(new[]{0,a,b,a,a+16,b+16,a,b+16,b});}
        var m=new Mesh{name="Shared soft contact"};m.SetVertices(v);m.SetColors(co);m.SetTriangles(t,0);m.RecalculateBounds();return m;
    }
    static void BakeWalls(Transform root,SpriteRenderer[] walls,string folder,Color body)
    {
        // Only axis-aligned, solid wall faces. Door leaves/glass/invisible colliders are excluded.
        var rects=walls.Where(w=>Mathf.Abs(Mathf.Sin(w.transform.eulerAngles.z*Mathf.Deg2Rad*2))<.001f)
            .Select(w=>Rect.MinMaxRect(Snap(w.bounds.min.x),Snap(w.bounds.min.y),Snap(w.bounds.max.x),Snap(w.bounds.max.y)))
            .Where(r=>r.width>=.05f&&r.height>=.05f).ToList();
        if(rects.Count==0)return;
        var contours=(List<Vector2[]>)typeof(ApplyVisualStyleLabSurfaces).GetMethod("WallContours",Private).Invoke(null,new object[]{rects});
        var batches=new Dictionary<Vector2Int,Tuple<List<Vector3>,List<Color>,List<int>>>();
        Action<Vector2[],Color> poly=(points,color)=>
        {
            var outer=points[1]-points[0];
            if(Mathf.Abs(outer.x)>.001f&&Mathf.Abs(outer.y)>.001f)throw new Exception("Invalid diagonal wall contour; refusing to save");
            var mid=points.Aggregate(Vector2.zero,(a,b)=>a+b)/points.Length;
            var key=new Vector2Int(Mathf.FloorToInt(mid.x/32),Mathf.FloorToInt(mid.y/32));
            if(!batches.TryGetValue(key,out var b))batches[key]=b=Tuple.Create(new List<Vector3>(),new List<Color>(),new List<int>());
            int start=b.Item1.Count;foreach(var p in points){b.Item1.Add(p);b.Item2.Add(color);}
            for(int j=1;j<points.Length-1;j++)b.Item3.AddRange(new[]{start,start+j,start+j+1});
        };
        foreach(var loop in contours)
        {
            var normal=new Vector2[loop.Length];var inset=new Vector2[loop.Length];
            for(int i=0;i<loop.Length;i++){var d=(loop[(i+1)%loop.Length]-loop[i]).normalized;normal[i]=new Vector2(-d.y,d.x);}
            for(int i=0;i<loop.Length;i++)inset[i]=loop[i]+(normal[i]+normal[(i+loop.Length-1)%loop.Length])*.055f;
            for(int i=0;i<loop.Length;i++)
            {
                int j=(i+1)%loop.Length;
                Color edge=normal[i].x>0||normal[i].y<0?Color.Lerp(body,Color.white,.13f):body*.87f;edge.a=1;
                poly(new[]{loop[i],loop[j],inset[j],inset[i]},edge);
            }
        }
        foreach(var pair in batches)
        {
            var m=new Mesh{name="Continuous wall rim "+pair.Key};m.SetVertices(pair.Value.Item1);m.SetColors(pair.Value.Item2);m.SetTriangles(pair.Value.Item3,0);m.RecalculateBounds();
            string assetPath=folder+"/Wall_"+pair.Key.x+"_"+pair.Key.y+".asset";
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if(existing){EditorUtility.CopySerialized(m,existing);UnityEngine.Object.DestroyImmediate(m);m=existing;EditorUtility.SetDirty(m);}
            else AssetDatabase.CreateAsset(m,assetPath);
            var node=new GameObject(m.name);node.transform.SetParent(root,false);node.layer=LayerMask.NameToLayer("Blocks");
            node.AddComponent<MeshFilter>().sharedMesh=m;var renderer=node.AddComponent<MeshRenderer>();renderer.sharedMaterial=contact;renderer.sortingOrder=1;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
        }
    }
    static float Snap(float v)=>Mathf.Round(v*100)/100;
}
#endif
