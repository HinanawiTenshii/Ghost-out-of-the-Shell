#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Stage 2 only: non-colliding, baked surface geometry and scene-local color changes.
// Run on a copy of the authored lab, never rebuild it from the original town.
public static class ApplyVisualStyleLabSurfaces
{
    const string ScenePath = "Assets/Scenes/Experiments/VisualStyleLab.unity";
    const string Folder = "Assets/Art/VisualStyleLab/SurfaceStudy";
    const string RootName = "08_SurfaceStudy_Phase2 — 墙地道路空间感（纯静态装饰）";
    static readonly Color WallBody = C(145,159,170), WallCap = C(164,177,187), WallSide = C(119,138,152);
    static readonly Color WallTransition = C(154,168,179), WallJoint = C(136,151,163);
    static readonly Color Indoor = C(35,58,77), Outdoor = C(34,46,59), Road = C(45,60,73);
    static Transform root;
    static Material material;
    static int totalVertices;

    public static void Run() => Apply(false);
    // Refresh only the eight generated meshes; preserve the current scene layout,
    // object identities and any edits to gameplay since the initial experiment.
    public static void Refresh() => Apply(true);
    static void Apply(bool refresh)
    {
        if (!Application.dataPath.Replace('\\','/').Contains("/.codex-temp/")) throw new Exception("Isolated project only");
        VerifyWallContours();
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var before = GameplaySnapshot(scene);
        var existingRoot=scene.GetRootGameObjects().SingleOrDefault(r=>r.name==RootName);
        if (existingRoot && !refresh)
            throw new Exception("Stage 2 is already applied. Restore the baseline copy before regenerating.");
        if(refresh && (!existingRoot || existingRoot.transform.childCount!=8 ||
            existingRoot.GetComponentsInChildren<Collider2D>(true).Length!=0 ||
            existingRoot.GetComponentsInChildren<MonoBehaviour>(true).Length!=0))
            throw new Exception("Unexpected surface group; inspect before refreshing");
        totalVertices=0;
        Directory.CreateDirectory(Folder);
        AssetDatabase.Refresh();
        material = AssetDatabase.LoadAssetAtPath<Material>(Folder+"/SurfaceVertexColor.mat");
        if (!material)
        {
            material=new Material(Shader.Find("Sprites/Default")){name="VisualStyleLab Surface Vertex Color"};
            AssetDatabase.CreateAsset(material,Folder+"/SurfaceVertexColor.mat");
        }
        root = existingRoot ? existingRoot.transform : new GameObject(RootName).transform;
        foreach(var t in root.GetComponentsInChildren<Transform>(true))
            if(t.localPosition!=Vector3.zero || t.localRotation!=Quaternion.identity || t.localScale!=Vector3.one)
                throw new Exception("Preserve manually adjusted detail transforms; refresh aborted: "+t.name);
        var ground = scene.GetRootGameObjects().Single(r=>r.name.StartsWith("01_Grounds"));
        var wallGroup = scene.GetRootGameObjects().Single(r=>r.name.StartsWith("02_Walls"));
        var floor = ground.transform.Find("Square").GetComponent<SpriteRenderer>();
        var exterior = ground.transform.Find("Street Ground").GetComponent<SpriteRenderer>();
        var roads = ground.GetComponentsInChildren<SpriteRenderer>(true).Where(s=>s.name.EndsWith(" Road")).ToArray();
        var bridge = ground.transform.Find("Channel Crossing").GetComponent<SpriteRenderer>();
        var solidWalls = wallGroup.GetComponentsInChildren<SpriteRenderer>(true)
            .Where(s=>s.transform.parent==wallGroup.transform && (s.name.StartsWith("Shop ") && s.name!="Shop Breakable Glass" || s.name.StartsWith("Boundary ")))
            .Where(s=>s.GetComponent<BoxCollider2D>() != null).ToArray();
        if(solidWalls.Length!=12 || roads.Length!=4) throw new Exception("Unexpected authored layout; inspect instead of overwriting");

        floor.color=Indoor; exterior.color=Outdoor;
        foreach(var r in roads)r.color=Road;
        bridge.color=C(62,81,94);
        var faces=new Batch("WallFaces", "墙面 — 连续轮廓与转角收口", 1, "Blocks");
        var joints=new Batch("WallJoints", "墙面 — 低对比疏纹", 2, "Blocks");
        var shadows=new Batch("ContactShadows", "墙根 — 连续贴地阴影", -8, "Default");
        var wallRects=solidWalls.Select(w=>BoundsRect(w.bounds)).ToList();
        // Shade the boundary of the union, not twelve overlapping sprite edges.
        // Shared seams disappear; inner/outer corners share the same miter vertex.
        var contours=WallContours(wallRects);
        foreach(var contour in contours) WallRim(faces,contour);
        foreach(var wall in solidWalls)
        {
            wall.color=WallBody;
            Rect b=BoundsRect(wall.bounds);
            bool horizontal=b.width>b.height;
            if(horizontal)
            {
                for(float x=Mathf.Ceil((b.xMin+.45f)/4.2f)*4.2f; x<b.xMax-.45f; x+=4.2f)
                {
                    var line=new Rect(x,b.yMin+.18f,.02f,b.height-.35f);
                    if(!wallRects.Any(other=>other!=b&&Inset(other,-.25f).Overlaps(line)))joints.Rect(line,WallJoint);
                }
            }
            else
            {
                for(float y=Mathf.Ceil((b.yMin+.45f)/4.2f)*4.2f; y<b.yMax-.45f; y+=4.2f)
                {
                    var line=new Rect(b.xMin+.16f,y,b.width-.34f,.02f);
                    if(!wallRects.Any(other=>other!=b&&Inset(other,-.25f).Overlaps(line)))joints.Rect(line,WallJoint);
                }
            }
        }
        // Union each shadow band and remove the wall footprint and near band.
        // The transparent quads never double-darken an overlapping corner.
        var footprint=Union(wallRects);
        var near=Difference(Union(wallRects.Select(b=>new Rect(b.position+new Vector2(.055f,-.08f),b.size)).ToList()),footprint);
        var far=Difference(Union(wallRects.Select(b=>new Rect(b.position+new Vector2(.14f,-.20f),b.size)).ToList()),footprint.Concat(near));
        foreach(var b in far)shadows.Rect(b,new Color(.025f,.055f,.095f,.10f));
        foreach(var b in near)shadows.Rect(b,new Color(.015f,.035f,.065f,.20f));
        faces.Save();joints.Save();shadows.Save();

        var interior=new Batch("InteriorStone", "室内 — 低对比错缝石板", -9, "Default");
        Rect interiorBounds=BoundsRect(floor.bounds);
        Paving(interior,new[]{interiorBounds},2.25f,1.65f,Indoor,C(27,47,65),.035f,true);
        // A simple inset border defines the room without framing every object.
        Border(interior,Inset(interiorBounds,.18f),.055f,C(50,76,94));
        interior.Save();

        var roadRects=roads.Select(r=>BoundsRect(r.bounds)).ToList();
        var permissionFloor=ground.transform.Find("Permission Test Floor").GetComponent<SpriteRenderer>();
        var roadPieces=Union(roadRects).SelectMany(r=>Subtract(r,BoundsRect(permissionFloor.bounds))).ToList();
        var paving=new Batch("RoadPaving", "道路 — 大块铺石（路口连续）",-19,"Default");
        Paving(paving,roadPieces,2.4f,1.4f,Road,C(40,54,67),.025f,false,false);
        paving.Save();
        var edges=new Batch("RoadEdges", "道路 — 平齐地面的低对比收边",-18,"Default");
        foreach(var r in roadRects) RoadBoundary(edges,r,roadRects);
        edges.Save();

        var outside=new Batch("ExteriorAccents", "室外 — 墙边与路旁零散石片",-24,"Default");
        var random=new System.Random(214);
        var exteriorBounds=BoundsRect(exterior.bounds);
        for(int i=0;i<58;i++)
        {
            float x=Mathf.Lerp(exteriorBounds.xMin+.6f,exteriorBounds.xMax-.6f,(float)random.NextDouble());
            float y=Mathf.Lerp(exteriorBounds.yMin+.6f,exteriorBounds.yMax-.6f,(float)random.NextDouble());
            var p=new Vector2(x,y);
            if(Inset(interiorBounds,-.45f).Contains(p) || roadRects.Any(r=>Inset(r,-.25f).Contains(p)) ||
                BoundsRect(permissionFloor.bounds).Contains(p) || x>16.1f)continue;
            float w=.22f+(float)random.NextDouble()*.42f,h=.07f+(float)random.NextDouble()*.12f;
            outside.Polygon(new[]{p,p+new Vector2(w*.72f,h),p+new Vector2(w,h*.45f),p+new Vector2(w*.88f,-h*.3f)},C(40,54,68));
        }
        outside.Save();
        var crossing=new Batch("BridgePaving", "桥面 — 铺石与两侧边沿",-6,"Visible Non Blocking");
        var bridgeBounds=BoundsRect(bridge.bounds);
        Paving(crossing,new[]{bridgeBounds},1.1f,2f,C(62,81,94),C(43,62,77),.045f,false);
        crossing.Rect(bridgeBounds.xMin,bridgeBounds.yMax-.12f,bridgeBounds.width,.12f,C(101,124,139));
        crossing.Rect(bridgeBounds.xMin,bridgeBounds.yMin,bridgeBounds.width,.12f,C(33,52,68));
        crossing.Save();

        if(GameplaySnapshot(scene)!=before)throw new Exception("Surface edit unexpectedly changed gameplay components or transforms");
        if(root.GetComponentsInChildren<Collider2D>(true).Length!=0 || root.GetComponentsInChildren<MonoBehaviour>(true).Length!=0)
            throw new Exception("Decorations must be static and collision-free");
        if(totalVertices>16000)throw new Exception("Detail mesh budget exceeded");
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        Debug.Log("VISUAL_SURFACES_PASS walls="+solidWalls.Length+" batches="+root.GetComponentsInChildren<MeshRenderer>().Length+
            " vertices="+totalVertices+" gameplay=unchanged");
    }

    static Color C(int r,int g,int b) => new Color32((byte)r,(byte)g,(byte)b,255);
    static Rect BoundsRect(Bounds b)=>Rect.MinMaxRect(b.min.x,b.min.y,b.max.x,b.max.y);
    static Rect Inset(Rect r,float amount)=>Rect.MinMaxRect(r.xMin+amount,r.yMin+amount,r.xMax-amount,r.yMax-amount);
    static Rect Intersection(Rect a,Rect b)=>Rect.MinMaxRect(Mathf.Max(a.xMin,b.xMin),Mathf.Max(a.yMin,b.yMin),Mathf.Min(a.xMax,b.xMax),Mathf.Min(a.yMax,b.yMax));
    static bool Valid(Rect r)=>r.width>.001f&&r.height>.001f;
    static IEnumerable<Rect> Subtract(Rect a,Rect b)
    {
        Rect cut=Intersection(a,b);
        if(!Valid(cut)){yield return a;yield break;}
        var pieces=new[]{Rect.MinMaxRect(a.xMin,a.yMin,a.xMax,cut.yMin),Rect.MinMaxRect(a.xMin,cut.yMax,a.xMax,a.yMax),
            Rect.MinMaxRect(a.xMin,cut.yMin,cut.xMin,cut.yMax),Rect.MinMaxRect(cut.xMax,cut.yMin,a.xMax,cut.yMax)};
        foreach(var p in pieces)if(Valid(p))yield return p;
    }
    static List<Rect> Union(List<Rect> source)
    {
        var result=new List<Rect>();
        foreach(var r in source)
        {
            var pieces=new List<Rect>{r};
            foreach(var existing in result)pieces=pieces.SelectMany(p=>Subtract(p,existing)).ToList();
            result.AddRange(pieces);
        }
        return result;
    }
    static List<Rect> Difference(IEnumerable<Rect> source,IEnumerable<Rect> cuts)
    {
        var result=source.ToList();
        foreach(var cut in cuts)result=result.SelectMany(r=>Subtract(r,cut)).ToList();
        return result;
    }
    // Coordinate-compressed grid, editor-only. Boundary edges keep wall material
    // on their left, including clockwise loops around doorways/courtyards.
    static List<Vector2[]> WallContours(List<Rect> rectangles)
    {
        Func<float,float> snap=v=>Mathf.Round(v*10000)/10000;
        var xs=rectangles.SelectMany(r=>new[]{snap(r.xMin),snap(r.xMax)}).Distinct().OrderBy(x=>x).ToArray();
        var ys=rectangles.SelectMany(r=>new[]{snap(r.yMin),snap(r.yMax)}).Distinct().OrderBy(y=>y).ToArray();
        int nx=xs.Length-1,ny=ys.Length-1;
        var cells=new bool[nx,ny];
        for(int x=0;x<nx;x++)for(int y=0;y<ny;y++)
            cells[x,y]=rectangles.Any(r=>r.Contains(new Vector2((xs[x]+xs[x+1])*.5f,(ys[y]+ys[y+1])*.5f)));
        var ends=new Dictionary<Vector2Int,List<Vector2Int>>();
        Action<int,int,int,int> edge=(x,y,ex,ey)=>
        {
            var a=new Vector2Int(x,y);
            if(!ends.TryGetValue(a,out var list))ends[a]=list=new List<Vector2Int>();
            list.Add(new Vector2Int(ex,ey));
        };
        for(int x=0;x<nx;x++)for(int y=0;y<ny;y++)if(cells[x,y])
        {
            if(y==0||!cells[x,y-1])edge(x,y,x+1,y);
            if(x==nx-1||!cells[x+1,y])edge(x+1,y,x+1,y+1);
            if(y==ny-1||!cells[x,y+1])edge(x+1,y+1,x,y+1);
            if(x==0||!cells[x-1,y])edge(x,y+1,x,y);
        }
        var result=new List<Vector2[]>();
        int budget=ends.Sum(p=>p.Value.Count);
        while(ends.Count>0)
        {
            var start=ends.Keys.OrderBy(p=>p.x).ThenBy(p=>p.y).First();
            var current=start;var incoming=Vector2Int.right;var points=new List<Vector2>();
            do
            {
                if(--budget<0||!ends.TryGetValue(current,out var choices))throw new Exception("Open wall contour");
                points.Add(new Vector2(xs[current.x],ys[current.y]));
                // At corner-touching walls, turn left to keep separate loops.
                var next=choices.OrderByDescending(p=>
                {
                    var d=p-current;int cross=incoming.x*d.y-incoming.y*d.x;
                    return cross>0?2:cross==0?1:0;
                }).First();
                choices.Remove(next);if(choices.Count==0)ends.Remove(current);
                incoming=next-current;current=next;
            }while(current!=start);
            var simplified=new List<Vector2>();
            for(int i=0;i<points.Count;i++)
            {
                var a=points[i]-points[(i+points.Count-1)%points.Count];
                var b=points[(i+1)%points.Count]-points[i];
                if(Mathf.Abs(a.x*b.y-a.y*b.x)>.000001f)simplified.Add(points[i]);
            }
            if(simplified.Count<4)throw new Exception("Degenerate wall contour");
            result.Add(simplified.ToArray());
        }
        return result;
    }
    static void WallRim(Batch mesh,Vector2[] contour)
    {
        int n=contour.Length;
        var normals=new Vector2[n];var lit=new bool[n];var widths=new float[n];
        for(int i=0;i<n;i++)
        {
            var d=(contour[(i+1)%n]-contour[i]).normalized;
            normals[i]=new Vector2(-d.y,d.x);
            lit[i]=normals[i].x>0||normals[i].y<0; // outward west/north
            widths[i]=lit[i]?.075f:.105f;
        }
        var inner=new Vector2[n];var transition=new Vector2[n];
        for(int i=0;i<n;i++)
        {
            int prev=(i+n-1)%n;
            inner[i]=contour[i]+normals[prev]*widths[prev]+normals[i]*widths[i];
            transition[i]=inner[i]+(normals[prev]+normals[i])*.035f;
        }
        for(int i=0;i<n;i++)
        {
            int j=(i+1)%n;
            mesh.Polygon(new[]{contour[i],contour[j],inner[j],inner[i]},lit[i]?WallCap:WallSide);
            mesh.Polygon(new[]{inner[i],inner[j],transition[j],transition[i]},lit[i]?WallTransition:C(135,151,164));
        }
    }
    static void VerifyWallContours()
    {
        var cases=new[]{
            new[]{new Rect(0,0,4,.5f)},
            new[]{new Rect(0,0,2,.5f),new Rect(2,0,2,.5f)},
            new[]{new Rect(0,0,4,.5f),new Rect(0,0,.5f,4)},
            new[]{new Rect(0,0,4,.5f),new Rect(1.5f,0,.5f,4)},
            new[]{new Rect(0,0,4,.5f),new Rect(0,3.5f,4,.5f),new Rect(0,0,.5f,4),new Rect(3.5f,0,.5f,4)},
            new[]{new Rect(0,0,1,1),new Rect(1,1,1,1)}
        };
        var expectedCorners=new[]{4,4,6,8,8,8};var expectedLoops=new[]{1,1,1,1,2,2};
        for(int k=0;k<cases.Length;k++)
        {
            var rects=cases[k].ToList();var loops=WallContours(rects);
            if(loops.Count!=expectedLoops[k]||loops.Sum(p=>p.Length)!=expectedCorners[k])throw new Exception("Wall contour topology test "+k);
            float area=0;
            foreach(var loop in loops)for(int i=0;i<loop.Length;i++)
            {var a=loop[i];var b=loop[(i+1)%loop.Length];area+=(a.x*b.y-a.y*b.x)*.5f;}
            if(Mathf.Abs(area-Union(rects).Sum(r=>r.width*r.height))>.001f)throw new Exception("Wall contour coverage test "+k);
        }
        Debug.Log("WALL_CONTOURS_PASS straight-split/L/T/courtyard/corner-touch coverage");
    }
    static void Paving(Batch mesh,IEnumerable<Rect> regions,float tileW,float tileH,Color baseColor,Color seam,float line,bool stagger,bool edgeFacets=true)
    {
        foreach(var region in regions)
        for(int row=Mathf.FloorToInt(region.yMin/tileH);row<=Mathf.CeilToInt(region.yMax/tileH);row++)
        {
            float offset=stagger&&(row&1)==1?tileW*.5f:0;
            for(int col=Mathf.FloorToInt((region.xMin-offset)/tileW);col<=Mathf.CeilToInt((region.xMax-offset)/tileW);col++)
            {
                Rect tile=new Rect(col*tileW+offset,row*tileH,tileW,tileH);
                Rect clipped=Intersection(tile,region);if(!Valid(clipped))continue;
                int variant=Mathf.Abs(row*17+col*31)%7;
                if(variant==0||variant==3)
                {
                    float shift=edgeFacets?(variant==0?.009f:-.008f):(variant==0?.004f:-.004f);
                    mesh.Rect(clipped,new Color(baseColor.r+shift,baseColor.g+shift,baseColor.b+shift,1));
                }
                mesh.Rect(Intersection(region,new Rect(tile.xMin,tile.yMin,tileW,line)),seam);
                mesh.Rect(Intersection(region,new Rect(tile.xMin,tile.yMin,line,tileH)),seam);
                // Sparse, short edge facets, not a bevel around every tile.
                if(edgeFacets&&variant==0)mesh.Rect(Intersection(region,new Rect(tile.xMin+.12f,tile.yMin+line,tileW*.35f,line*.65f)),
                    new Color(baseColor.r+.015f,baseColor.g+.018f,baseColor.b+.02f,1));
            }
        }
    }
    static void Border(Batch batch,Rect b,float thickness,Color color)
    {
        batch.Rect(b.xMin,b.yMin,b.width,thickness,color);batch.Rect(b.xMin,b.yMax-thickness,b.width,thickness,color);
        batch.Rect(b.xMin,b.yMin,thickness,b.height,color);batch.Rect(b.xMax-thickness,b.yMin,thickness,b.height,color);
    }
    static void RoadBoundary(Batch batch,Rect r,List<Rect> all)
    {
        foreach(bool horizontal in new[]{true,false})foreach(bool positive in new[]{false,true})
        {
            float constant=horizontal?(positive?r.yMax:r.yMin):(positive?r.xMax:r.xMin);
            float min=horizontal?r.xMin:r.yMin,max=horizontal?r.xMax:r.yMax;
            var cuts=new List<float>{min,max};
            foreach(var other in all){cuts.Add(Mathf.Clamp(horizontal?other.xMin:other.yMin,min,max));cuts.Add(Mathf.Clamp(horizontal?other.xMax:other.yMax,min,max));}
            cuts=cuts.Distinct().OrderBy(x=>x).ToList();
            for(int i=0;i<cuts.Count-1;i++)
            {
                float length=cuts[i+1]-cuts[i];if(length<.01f)continue;
                float mid=(cuts[i]+cuts[i+1])*.5f,delta=positive?.025f:-.025f;
                Vector2 outside=horizontal?new Vector2(mid,constant+delta):new Vector2(constant+delta,mid);
                if(all.Any(o=>o.Contains(outside)))continue; // No curb across intersections.
                // A flat material boundary: same tone on every side, fully inside
                // the road, no bright bevel or cast shadow suggesting a raised slab.
                Color curb=C(40,54,67);
                if(horizontal)
                {
                    batch.Rect(cuts[i],constant+(positive?-.035f:0),length,.035f,curb);
                }
                else
                {
                    batch.Rect(constant+(positive?-.035f:0),cuts[i],.035f,length,curb);
                }
            }
        }
    }
    static string GameplaySnapshot(Scene scene)
    {
        // Includes positions/scales and every gameplay component, including the
        // camera/CRT, AI, gate events, map tags and collider configuration.
        return string.Join("\n",scene.GetRootGameObjects().Where(r=>r.name!=RootName)
            .SelectMany(r=>r.GetComponentsInChildren<Component>(true))
            .Where(c=>c!=null && (c is Transform||c is Collider2D||c is Rigidbody2D||c is MonoBehaviour||c is Camera))
            .Select(c=>c.GetInstanceID()+":"+EditorJsonUtility.ToJson(c)));
    }
    sealed class Batch
    {
        readonly List<Vector3> vertices=new List<Vector3>();readonly List<Color> colors=new List<Color>();
        readonly List<Vector2> uv=new List<Vector2>();readonly List<int> triangles=new List<int>();
        readonly string id,label,layer;readonly int order;
        public Batch(string id,string label,int order,string layer){this.id=id;this.label=label;this.order=order;this.layer=layer;}
        public void Rect(float x,float y,float w,float h,Color color)=>Rect(new Rect(x,y,w,h),color);
        public void Rect(Rect r,Color color)
        {
            if(!Valid(r))return;
            Polygon(new[]{new Vector2(r.xMin,r.yMin),new Vector2(r.xMax,r.yMin),new Vector2(r.xMax,r.yMax),new Vector2(r.xMin,r.yMax)},color);
        }
        public void Polygon(Vector2[] points,Color color)
        {
            int start=vertices.Count;
            foreach(var p in points){vertices.Add(new Vector3(p.x,p.y,0));colors.Add(color);uv.Add(Vector2.zero);}
            for(int i=1;i<points.Length-1;i++){triangles.Add(start);triangles.Add(start+i);triangles.Add(start+i+1);}
        }
        public void Save()
        {
            if(vertices.Count==0)return;
            var mesh=new Mesh{name="Lab "+id};mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
            var path=Folder+"/"+id+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing){EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);mesh=existing;}else AssetDatabase.CreateAsset(mesh,path);
            var reused=root.GetComponentsInChildren<MeshFilter>(true).SingleOrDefault(f=>f.sharedMesh==mesh);
            var go=reused ? reused.gameObject : new GameObject(label,typeof(MeshFilter),typeof(MeshRenderer));
            go.name=label;go.transform.SetParent(root,false);go.layer=LayerMask.NameToLayer(layer);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.sortingOrder=order;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
            renderer.lightProbeUsage=UnityEngine.Rendering.LightProbeUsage.Off;renderer.reflectionProbeUsage=UnityEngine.Rendering.ReflectionProbeUsage.Off;
            totalVertices+=vertices.Count;
        }
    }
}
#endif
