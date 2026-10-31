#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

// Opt-in scene authoring only; no production prefab or shared art is modified.
public static class ApplyVisualStyleLabActorsProps
{
    const string ScenePath="Assets/Scenes/Experiments/VisualStyleLab.unity";
    const string Folder="Assets/Art/VisualStyleLab/ActorPropStudy";
    const string Prefix="Lab4 ";
    static Material material;
    static int props,vertices;
    static Color C(int r,int g,int b,float a=1)=>new Color(r/255f,g/255f,b/255f,a);
    static readonly Color Wood=C(112,78,48),WoodLight=C(143,110,76),WoodDark=C(66,48,35);
    static readonly Color Cloth=C(47,75,81),ClothLight=C(58,88,94);

    public static void Run()
    {
        if(!Application.dataPath.Replace('\\','/').Contains("/.codex-temp/"))throw new Exception("Isolated project only");
        var scene=EditorSceneManager.OpenScene(ScenePath);
        var components=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Component>(true))
            .Where(c=>c!=null&&(c.hideFlags&HideFlags.DontSave)==0).ToArray();
        if(components.OfType<VisualStyleLabContactShadow>().Any()||components.Any(c=>c.name.StartsWith(Prefix)))throw new Exception("Already applied; preserve edits");
        // Preserve every original transform/collider, and every non-color gameplay field.
        var snapshots=components.Where(c=>!(c is Renderer)&&!(c is MeshFilter))
            .ToDictionary(c=>c,GameplayFields);
        if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/Art/VisualStyleLab","ActorPropStudy");
        material=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/ContactAndRim.mat");
        if(!material){material=new Material(Shader.Find("Sprites/Default")){name="ContactAndRim"};AssetDatabase.CreateAsset(material,Folder+"/ContactAndRim.mat");}
        var foot=new Geometry();
        // Two flat tones with a narrow fading fringe; no halo or body outline.
        foot.Ellipse(.33f,.115f,C(5,12,22,.32f));
        var footMesh=foot.Save("ActorContact");
        var actors=components.OfType<ZeldaCharacterData>().ToArray();
        if(actors.Length!=5)throw new Exception("Unexpected lab actor list");
        foreach(var actor in actors)
        {
            ActorPalette(actor);
            var node=Visual(actor.transform,"Foot contact",footMesh,-7);
            node.transform.localPosition=new Vector3(0,-.125f,0);
            var controller=actor.gameObject.AddComponent<VisualStyleLabContactShadow>();
            Set(controller,"character",p=>p.objectReferenceValue=actor);
            Set(controller,"body",p=>p.objectReferenceValue=actor.GetComponent<SpriteRenderer>());
            Set(controller,"shadow",p=>p.objectReferenceValue=node.GetComponent<MeshRenderer>());
        }
        var shop=scene.GetRootGameObjects().Single(r=>r.name.StartsWith("03_Shop"));
        foreach(Transform prop in shop.transform)
        {
            bool table=prop.name.StartsWith("Table"),chair=prop.name.StartsWith("Chair");
            var curved=prop.GetComponent<GeometricCurvedTable>();
            var alchemy=prop.GetComponent<GeometricAlchemyDecoration>();
            if(!table&&!chair&&!alchemy)continue;
            if(curved)
            {
                ColorField(curved,"woodColor",Wood);ColorField(curved,"edgeColor",WoodDark);
                ColorField(curved,"surfaceColor",Cloth);ColorField(curved,"insetEdgeColor",ClothLight);
            }
            else if(alchemy)ColorField(alchemy,"stone",C(110,132,144));
            else if(chair)ChairPalette(prop);
            else foreach(var sprite in prop.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if(sprite.name=="Square")sprite.color=WoodDark;
                else if(sprite.name=="Table-TopPanel")sprite.color=ClothLight;
                else if(sprite.name=="Table-FrontRail")sprite.color=Wood;
                else if(sprite.name=="Square (1)")sprite.color=table?Cloth:Wood;
                else if(sprite.name=="Square (2)")sprite.color=WoodDark;
                else if(sprite.name.StartsWith("Chair-"))sprite.color=Wood;
            }
            var collider=prop.GetComponent<Collider2D>();
            if(collider==null)collider=prop.GetComponentInChildren<Collider2D>();
            // Cauldron physics is rectangular, but the visual vessel is circular.
            var outline=alchemy?Circle(prop,.67f,20):collider?Outline(prop,collider):VisualOutline(prop);
            if(alchemy)Visual(prop,"Contact shadow",MechanismContact(alchemy).Save("Prop"+(props++)+"Contact"),-7);
            else AddProp(prop,outline,table);
        }
        // Ground small devices as well, without recoloring status displays or liquids.
        foreach(var device in components.Where(c=>c is BatterySocket||c is WaterwayGateController||c is LeverData))
        {
            var col=device.GetComponent<Collider2D>();
            if(col!=null)Visual(device.transform,"Contact shadow",MechanismContact(device).Save("Prop"+(props++)+"Contact"),-7);
        }
        foreach(var saved in snapshots)
            if(saved.Key==null||saved.Value!=GameplayFields(saved.Key))throw new Exception("Gameplay changed: "+saved.Key);
        if(vertices>6000)throw new Exception("Prop detail budget exceeded");
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        Debug.Log("LAB_ACTOR_PROP_PASS actors="+actors.Length+" props="+props+" vertices="+vertices+" gameplay=unchanged outlines=none");
    }
    public static void RefineChairsAndStreet()
    {
        if(!Application.dataPath.Replace('\\','/').Contains("/.codex-temp/"))throw new Exception("Isolated project only");
        var scene=EditorSceneManager.OpenScene(ScenePath);
        var components=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Component>(true))
            .Where(c=>c!=null&&(c.hideFlags&HideFlags.DontSave)==0).ToArray();
        var snapshots=components.Where(c=>!(c is Renderer)&&!(c is MeshFilter)).ToDictionary(c=>c,GameplayFields);
        material=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/ContactAndRim.mat");
        if(!material)throw new Exception("Phase four must be installed first");
        var street=scene.GetRootGameObjects().Single(r=>r.name.StartsWith("04_Street"));
        if(street.GetComponentsInChildren<MeshRenderer>(true).Any(r=>r.name.StartsWith(Prefix)&&r.GetComponentInParent<LeverData>()==null))throw new Exception("Street already refined; preserve edits");
        var shop=scene.GetRootGameObjects().Single(r=>r.name.StartsWith("03_Shop"));
        int chairs=0;vertices=0;
        foreach(Transform chair in shop.transform)
        {
            if(!chair.name.StartsWith("Chair"))continue;
            ChairPalette(chair);
            var outline=VisualOutline(chair);
            var shadow=chair.Find(Prefix+"Contact shadow");
            if(!shadow)throw new Exception("Missing chair contact mesh");
            var geometry=new Geometry();BuildContact(chair,outline,geometry);
            geometry.Save(System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(shadow.GetComponent<MeshFilter>().sharedMesh)));
            // Original backrest/arms already express the chair. A generic enclosing
            // rim makes it look like a box, and its old world AABB was wrong at 45°.
            var rim=chair.Find(Prefix+"Upper-left rim");
            if(rim)
            {
                new Geometry().Save(System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(rim.GetComponent<MeshFilter>().sharedMesh)));
                rim.GetComponent<MeshRenderer>().enabled=false;
            }
            chairs++;
        }
        props=20;int outdoor=0;
        foreach(Transform prop in street.transform)
        {
            var tree=prop.GetComponent<GeometricTreeDecoration>();
            var lamp=prop.GetComponent<KeroseneStreetLamp>();
            var barrel=prop.GetComponent<GeometricStorageFurniture>();
            var sign=prop.GetComponent<GeometricSignpostDecoration>();
            bool bush=prop.name=="BreakableBush",bed=prop.name=="GeometricFlowerbed";
            if(tree)
            {
                ColorField(tree,"trunkColor",C(109,78,46));ColorField(tree,"crownColor",C(56,112,86));
                ColorField(tree,"upperCrownColor",C(80,139,99));ColorField(tree,"leafAccentColor",C(65,122,88));
                BaseShadow(prop,.70f,.22f,new Vector2(0,.08f),"OutdoorTreeContact");
            }
            else if(bush)
            {
                foreach(var sprite in prop.GetComponentsInChildren<SpriteRenderer>(true))
                    sprite.color=sprite.color.g>.76f?C(79,137,94):sprite.color.g>.60f?C(56,111,80):C(35,75,63);
                BaseShadow(prop,.95f,.25f,new Vector2(.06f,-.34f),"OutdoorBushContact");
            }
            else if(lamp)
            {
                ColorField(lamp,"metalColor",C(41,57,70));
                AddProp(prop,Outline(prop,prop.GetComponent<Collider2D>()),false);
            }
            else if(barrel)
            {
                ColorField(barrel,"woodColor",Wood);ColorField(barrel,"edgeColor",WoodDark);
                ColorField(barrel,"lightWoodColor",WoodLight);ColorField(barrel,"metalColor",C(119,138,150));
                AddProp(prop,Outline(prop,prop.GetComponent<Collider2D>()),false);
            }
            else if(sign)
            {
                ColorField(sign,"poleColor",C(91,69,43));ColorField(sign,"boardColor",C(160,116,65));
                AddProp(prop,Outline(prop,prop.GetComponent<Collider2D>()),false);
            }
            else if(bed)
            {
                foreach(var sprite in prop.GetComponentsInChildren<SpriteRenderer>(true))
                    sprite.color=sprite.name=="Soil"?C(67,49,37):sprite.name=="Inner Stone Edge"?C(63,82,96):C(128,146,155);
                var col=prop.GetComponentInChildren<Collider2D>();
                AddProp(prop,col?Outline(prop,col):VisualOutline(prop),false);
            }
            else continue;
            outdoor++;
        }
        foreach(var saved in snapshots)if(saved.Key==null||saved.Value!=GameplayFields(saved.Key))throw new Exception("Gameplay changed: "+saved.Key);
        if(chairs!=11||outdoor!=8)throw new Exception("Unexpected chair/street coverage "+chairs+" / "+outdoor);
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        Debug.Log("LAB_CHAIR_STREET_PASS chairs="+chairs+" outdoor="+outdoor+" gameplay=unchanged");
    }
    static void ChairPalette(Transform chair)
    {
        foreach(var sprite in chair.GetComponentsInChildren<SpriteRenderer>(true))
            sprite.color=sprite.name=="Square"?C(60,43,31):sprite.name=="Square (1)"?C(128,86,49):
                sprite.name=="Square (2)"?C(179,128,77):C(153,106,62);
    }
    // Collision footprints include clearance and moving parts, not just the base.
    // Do not extrude them into visible rectangular frames round small mechanisms.
    public static void RefineMechanismContacts()
    {
        if(!Application.dataPath.Replace('\\','/').Contains("/.codex-temp/"))throw new Exception("Isolated project only");
        var scene=EditorSceneManager.OpenScene(ScenePath);
        var components=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Component>(true))
            .Where(c=>c!=null&&(c.hideFlags&HideFlags.DontSave)==0).ToArray();
        var snapshots=components.Where(c=>!(c is Renderer)&&!(c is MeshFilter)&&!(c is VisualStyleLabMechanismSignal)).ToDictionary(c=>c,GameplayFields);
        int count=0;
        foreach(var owner in components.Where(c=>c is BatterySocket||c is WaterwayGateController||c is LeverData||c is GeometricAlchemyDecoration))
        {
            var contact=owner.transform.Find(Prefix+"Contact shadow");
            if(contact==null)throw new Exception("Missing existing contact: "+owner.name);
            var mesh=contact.GetComponent<MeshFilter>().sharedMesh;
            var shape=MechanismContact(owner);
            // Preserve mesh GUIDs and all owner/collider transforms.
            shape.Save(System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(mesh)));
            contact.GetComponent<MeshRenderer>().enabled=true;
            var indicator=owner.GetComponent<VisualStyleLabMechanismSignal>();
            if(owner is LeverData&&indicator!=null)
                Set(indicator,"localPosition",p=>p.vector2Value=new Vector2(0,-.025f));
            count++;
        }
        if(count!=4)throw new Exception("Unexpected mechanism coverage: "+count);
        foreach(var saved in snapshots)if(saved.Key==null||saved.Value!=GameplayFields(saved.Key))throw new Exception("Gameplay changed: "+saved.Key);
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        Debug.Log("LAB_MECHANISM_CONTACT_PASS count=4 gameplay=unchanged collider_extrusions=removed");
    }
    static Geometry MechanismContact(Component owner)
    {
        var radii=owner is BatterySocket?new Vector2(.39f,.49f):
            owner is WaterwayGateController?new Vector2(.445f,.65f):
            owner is LeverData?new Vector2(.37f,.14f):new Vector2(.735f,.735f);
        var center=owner is LeverData?new Vector2(.015f,-.035f):new Vector2(.025f,-.035f);
        var shape=new Geometry();shape.Ellipse(radii.x,radii.y,C(6,12,21,.23f));shape.Offset(center);return shape;
    }
    static void BaseShadow(Transform prop,float width,float height,Vector2 offset,string id)
    {
        var mesh=new Geometry();mesh.Ellipse(width,height,C(6,12,21,.28f));
        var go=Visual(prop,"Contact shadow",mesh.Save(id),-7);go.transform.localPosition=offset;
    }
    static string GameplayFields(Component component)
    {
        // Appearance colors are the only permitted changes on existing components.
        var serialized=new SerializedObject(component);var it=serialized.GetIterator();var fields=new List<string>();
        bool enterChildren=true;
        while(it.Next(enterChildren))
        {
            enterChildren=it.propertyType!=SerializedPropertyType.Color;
            if(!enterChildren)continue;
            // Color channels are children in some Unity serialization versions.
            if(it.propertyPath.EndsWith(".r")||it.propertyPath.EndsWith(".g")||it.propertyPath.EndsWith(".b")||it.propertyPath.EndsWith(".a"))continue;
            if(it.propertyType==SerializedPropertyType.Integer)fields.Add(it.propertyPath+":"+it.longValue);
            else if(it.propertyType==SerializedPropertyType.Float)fields.Add(it.propertyPath+":"+it.doubleValue.ToString("R"));
            else if(it.propertyType==SerializedPropertyType.Boolean)fields.Add(it.propertyPath+":"+it.boolValue);
            else if(it.propertyType==SerializedPropertyType.String)fields.Add(it.propertyPath+":"+it.stringValue);
            else if(it.propertyType==SerializedPropertyType.Enum)fields.Add(it.propertyPath+":"+it.enumValueIndex);
            else if(it.propertyType==SerializedPropertyType.ObjectReference)fields.Add(it.propertyPath+":"+it.objectReferenceInstanceIDValue);
            else if(it.propertyType==SerializedPropertyType.Vector2)fields.Add(it.propertyPath+":"+it.vector2Value.ToString("R"));
            else if(it.propertyType==SerializedPropertyType.Vector3)fields.Add(it.propertyPath+":"+it.vector3Value.ToString("R"));
            else if(it.propertyType==SerializedPropertyType.Quaternion)fields.Add(it.propertyPath+":"+it.quaternionValue.ToString("R"));
        }
        // New children are decorative; compare existing transforms directly instead.
        if(component is Transform t)return t.localPosition.ToString("R")+t.localScale.ToString("R")+t.localRotation.ToString("R")+t.parent?.GetInstanceID();
        return string.Join("\n",fields);
    }
    static void ActorPalette(ZeldaCharacterData actor)
    {
        if(actor is EngineerZeldaCharacterData)
        {
            ColorField(actor,"shirtColor",C(45,103,116));ColorField(actor,"apronColor",C(121,78,45));
            ColorField(actor,"capColor",C(38,76,91));ColorField(actor,"skinColor",C(199,147,105));
            ColorField(actor,"brassColor",C(173,129,62));ColorField(actor,"lensColor",C(88,166,183));
            ColorField(actor,"toolColor",C(155,178,188));ColorField(actor,"hammerColor",C(120,147,163));
            ColorField(actor,"scarfColor",C(156,57,43));
        }
        else if(actor is ArabSoldierZeldaCharacterData)
        {
            ColorField(actor,"armorColor",C(88,109,123));ColorField(actor,"armorShadow",C(49,64,78));
            ColorField(actor,"armorHighlight",C(139,160,171));ColorField(actor,"headclothColor",C(163,157,139));
            ColorField(actor,"headclothHighlight",C(191,184,162));ColorField(actor,"headclothShadow",C(108,104,91));
            ColorField(actor,"scarfColor",C(128,139,144));ColorField(actor,"scarfShadow",C(73,86,96));
        }
        else if(actor is CivilianZeldaCharacterData)
        {
            ColorField(actor,"headclothColor",actor is DesertMerchantZeldaCharacterData?C(192,180,151):C(177,177,163));
            ColorField(actor,"clothingColor",actor is DesertMerchantZeldaCharacterData?C(33,111,103):C(68,107,123));
            ColorField(actor,"skinColor",C(187,135,92));
            if(actor is DesertMerchantZeldaCharacterData)ColorField(actor,"trimColor",C(178,132,57));
        }
        else throw new Exception("Unreviewed actor palette: "+actor.GetType().Name);
    }
    static void Set(Object o,string field,Action<SerializedProperty> apply)
    {var so=new SerializedObject(o);var p=so.FindProperty(field);if(p==null)throw new Exception(o.name+" lacks "+field);apply(p);so.ApplyModifiedPropertiesWithoutUndo();}
    static void ColorField(Object o,string field,Color color)=>Set(o,field,p=>p.colorValue=color);
    static Vector2[] Circle(Transform parent,float radius,int count)=>Enumerable.Range(0,count).Select(i=>(Vector2)parent.TransformPoint(new Vector3(Mathf.Cos(i*Mathf.PI*2/count)*radius,Mathf.Sin(i*Mathf.PI*2/count)*radius,0))).ToArray();
    static Vector2[] Outline(Transform parent,Collider2D col)
    {
        if(col is PolygonCollider2D polygon)return polygon.GetPath(0).Select(p=>(Vector2)polygon.transform.TransformPoint(p+polygon.offset)).ToArray();
        if(col is BoxCollider2D box)
        {
            var half=box.size*.5f;return new[]{new Vector2(-half.x,-half.y),new Vector2(half.x,-half.y),half,new Vector2(-half.x,half.y)}
                .Select(p=>(Vector2)box.transform.TransformPoint(p+box.offset)).ToArray();
        }
        var b=col.bounds;return new[]{new Vector2(b.min.x,b.min.y),new Vector2(b.max.x,b.min.y),new Vector2(b.max.x,b.max.y),new Vector2(b.min.x,b.max.y)};
    }
    static Vector2[] VisualOutline(Transform prop)
    {
        var sprites=prop.GetComponentsInChildren<SpriteRenderer>(true);if(sprites.Length==0)throw new Exception("Missing visual footprint: "+prop.name);
        // Accumulate sprite corners in owner-local coordinates, then transform the
        // outline once. World renderer.bounds would put an axis-aligned box round
        // a rotated chair, leaving bright triangular gaps outside the actual art.
        var min=new Vector2(float.PositiveInfinity,float.PositiveInfinity);var max=-min;
        foreach(var s in sprites)
        {
            if(!s.sprite)continue;var b=s.sprite.bounds;
            foreach(var corner in new[]{new Vector2(b.min.x,b.min.y),new Vector2(b.max.x,b.min.y),new Vector2(b.max.x,b.max.y),new Vector2(b.min.x,b.max.y)})
            {
                var p=(Vector2)prop.InverseTransformPoint(s.transform.TransformPoint(corner));
                min=Vector2.Min(min,p);max=Vector2.Max(max,p);
            }
        }
        return new[]{min,new Vector2(max.x,min.y),max,new Vector2(min.x,max.y)}.Select(p=>(Vector2)prop.TransformPoint(p)).ToArray();
    }
    static void BuildContact(Transform parent,Vector2[] outline,Geometry shadow)
    {
        float area=0;for(int i=0;i<outline.Length;i++){var a=outline[i];var b=outline[(i+1)%outline.Length];area+=a.x*b.y-a.y*b.x;}
        if(area<0)Array.Reverse(outline);
        var offset=new Vector2(.065f,-.09f);
        for(int i=0;i<outline.Length;i++)
        {
            var a=outline[i];var b=outline[(i+1)%outline.Length];var d=(b-a).normalized;
            if(Vector2.Dot(new Vector2(d.y,-d.x),offset)<=.001f)continue;
            shadow.Polygon(new[]{(Vector2)parent.InverseTransformPoint(a),(Vector2)parent.InverseTransformPoint(a+offset),
                (Vector2)parent.InverseTransformPoint(b+offset),(Vector2)parent.InverseTransformPoint(b)},C(6,12,21,.27f));
        }
    }
    static void AddProp(Transform parent,Vector2[] outline,bool addRim)
    {
        float area=0;for(int i=0;i<outline.Length;i++){var a=outline[i];var b=outline[(i+1)%outline.Length];area+=a.x*b.y-a.y*b.x;}
        if(area<0)Array.Reverse(outline);
        var shadow=new Geometry();var rim=new Geometry();
        BuildContact(parent,outline,shadow);
        Func<Vector2,Vector2> local=p=>parent.InverseTransformPoint(p);
        for(int i=0;i<outline.Length;i++)
        {
            var a=outline[i];var b=outline[(i+1)%outline.Length];var d=(b-a).normalized;var inward=new Vector2(-d.y,d.x);
            if(addRim && (inward.x>.1f||inward.y<-.1f))
                rim.Polygon(new[]{local(a),local(b),local(b+inward*.025f),local(a+inward*.025f)},WoodLight);
        }
        Visual(parent,"Contact shadow",shadow.Save("Prop"+props+"Contact"),-7);
        if(addRim)Visual(parent,"Upper-left rim",rim.Save("Prop"+props+"Rim"),-1);
        props++;
    }
    static GameObject Visual(Transform parent,string label,Mesh mesh,int order)
    {
        var go=new GameObject(Prefix+label,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
        go.layer=LayerMask.NameToLayer("Default");go.GetComponent<MeshFilter>().sharedMesh=mesh;
        var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.sortingOrder=order;
        renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
        renderer.lightProbeUsage=UnityEngine.Rendering.LightProbeUsage.Off;renderer.reflectionProbeUsage=UnityEngine.Rendering.ReflectionProbeUsage.Off;
        return go;
    }
    sealed class Geometry
    {
        readonly List<Vector3> v=new List<Vector3>();readonly List<Color> c=new List<Color>();readonly List<int> t=new List<int>();
        public void Polygon(Vector2[] points,Color color)
        {int start=v.Count;foreach(var p in points){v.Add(p);c.Add(color);}for(int i=1;i<points.Length-1;i++){t.Add(start);t.Add(start+i);t.Add(start+i+1);}}
        public void Ellipse(float x,float y,Color color)
        {
            const int n=12;var inner=new Vector2[n];var outer=new Vector2[n];
            for(int i=0;i<n;i++){var d=new Vector2(Mathf.Cos(i*Mathf.PI*2/n)*x,Mathf.Sin(i*Mathf.PI*2/n)*y);inner[i]=d*.70f;outer[i]=d;}
            Polygon(inner,color);
            for(int i=0;i<n;i++)
            {
                int j=(i+1)%n,start=v.Count;
                v.Add(inner[i]);v.Add(outer[i]);v.Add(outer[j]);v.Add(inner[j]);
                var clear=color;clear.a=0;c.Add(color);c.Add(clear);c.Add(clear);c.Add(color);
                t.Add(start);t.Add(start+1);t.Add(start+2);t.Add(start);t.Add(start+2);t.Add(start+3);
            }
        }
        public void Offset(Vector2 offset)
        {for(int i=0;i<v.Count;i++)v[i]+=(Vector3)offset;}
        public Mesh Save(string id)
        {
            var mesh=new Mesh{name=id};mesh.SetVertices(v);mesh.SetColors(c);mesh.SetTriangles(t,0);mesh.SetUVs(0,Enumerable.Repeat(Vector2.zero,v.Count).ToList());mesh.RecalculateBounds();
            string path=Folder+"/"+id+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing){EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);mesh=existing;}else AssetDatabase.CreateAsset(mesh,path);
            vertices+=v.Count;return mesh;
        }
    }
}
#endif
