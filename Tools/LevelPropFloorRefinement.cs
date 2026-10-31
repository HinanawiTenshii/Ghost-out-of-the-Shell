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
using Object = UnityEngine.Object;

public static partial class LevelPropFloorRefinement
{
    const string Art = "Assets/Art/LevelTerminalStyle";
    public const string ShadowName = "Terminal Prop Contact Shadow";
    static void Isolate()
    { if (!Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/")) throw new Exception("Isolated project only"); }
    static Component[] Components(Scene scene) => scene.GetRootGameObjects()
        .SelectMany(r => r.GetComponentsInChildren<Component>(true)).Where(c => c != null).ToArray();
    public static void Inspect()
    {
        Isolate();
        foreach (var path in LevelVisualRollout.Scenes)
        {
            var scene = EditorSceneManager.OpenScene(path);
            var all = Components(scene);
            foreach (var group in all.OfType<Transform>()
                .Where(t => PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject))
                .GroupBy(t => PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject))
                .Where(g => g.Key.Contains("Decorations")))
            {
                var t = group.First();
                Debug.Log("PROP " + scene.name + " " + group.Key + " count=" + group.Count() +
                    " sample=" + t.name + " types=" + string.Join(",", t.GetComponents<MonoBehaviour>().Where(c => c).Select(c => c.GetType().Name)) +
                    " render=" + string.Join(",", t.GetComponentsInChildren<Renderer>(true).Take(6).Select(r => r.name + ":" + r.sortingOrder + ":" + r.bounds.size)));
            }
            foreach (var s in all.OfType<SpriteRenderer>().Where(s => s.sharedMaterial && s.sharedMaterial.shader.name == "Game/Terminal Floor Detail"))
                Debug.Log("FLOOR " + scene.name + " " + s.name + " parent=" + s.transform.parent?.name + " color=" + s.color + " size=" + s.bounds.size + " mat=" + s.sharedMaterial.name);
        }
    }

    // Prefab identity survives scene-instance renaming (e.g. Level0's desks).
    // Keep flat rune seals, rugs, stairs, hanging art and large shadow masks out.
    public static bool IsProp(Transform t)
    {
        if (t.GetComponentInParent<ZeldaCharacterData>() || t.GetComponentInParent<PickupItemBase>()) return false;
        if (t.GetComponent<GeometricCurvedTable>() || t.GetComponent<GeometricStorageFurniture>() ||
            t.GetComponent<GeometricShopDecoration>() || t.GetComponent<GeometricTreeDecoration>() ||
            t.GetComponent<KeroseneStreetLamp>() || t.GetComponent<GeometricSignpostDecoration>()) return true;
        if (!PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)) return false;
        string id = Path.GetFileNameWithoutExtension(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject));
        if (id.Contains("RuneSeal") || id.Contains("Rug") || id.Contains("Trapdoor")) return false;
        return id.StartsWith("Table-") || id.StartsWith("Chair-") || id.StartsWith("Bed-") ||
            id.StartsWith("Bench-") || id == "Chest" || id == "Shelf" || id == "WorkTable" ||
            id == "Fountain-Round" || id == "喷泉" || id == "GeometricFlowerbed" ||
            id == "GeometricCactus" || id == "GeometricBroadleafShrub" ||
            t.GetComponent<GeometricFortressDecoration>() || t.GetComponent<GeometricFortressInteriorDecoration>() ||
            t.GetComponent<GeometricAlchemyDecoration>();
    }
    static bool Owns(Transform owner, Transform child)
    {
        for (var t = child; t && t != owner; t = t.parent)
            if (IsProp(t) || t.GetComponent<PickupItemBase>() || t.GetComponent<ZeldaCharacterData>()) return false;
        return true;
    }
    static bool BaseOnly(Transform t) => t.GetComponent<GeometricTreeDecoration>() ||
        t.GetComponent<KeroseneStreetLamp>() || t.GetComponent<GeometricSignpostDecoration>() ||
        PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject).Contains("GeometricCactus");

    public static void Apply()
    {
        Isolate(); Directory.CreateDirectory(Art + "/Props"); AssetDatabase.Refresh();
        var mat = AssetDatabase.LoadAssetAtPath<Material>(Art + "/Contact.mat");
        var usedMeshes = new HashSet<string>();
        if (!mat || ShaderUtil.ShaderHasError(Shader.Find("Game/Terminal Floor Detail"))) throw new Exception("Visual dependencies invalid");
        var signature = typeof(ApplyVisualStyleLabActorsProps).GetMethod("GameplayFields", BindingFlags.Static | BindingFlags.NonPublic);
        foreach (var path in LevelVisualRollout.Scenes)
        {
            var scene = EditorSceneManager.OpenScene(path);
            var all = Components(scene);
            var authored = all.Where(c => (c.hideFlags & HideFlags.DontSave) == 0).ToArray();
            var untouched = authored.Where(c => !(c is Renderer) && !(c is MeshFilter) && !(c is LevelContactShadow))
                .ToDictionary(c => c, c => (string)signature.Invoke(null, new object[] { c }));
            var renderers = authored.OfType<SpriteRenderer>().ToDictionary(s => s, s => EditorJsonUtility.ToJson(s));
            var props = all.OfType<Transform>().Where(IsProp).ToArray(); int added = 0, replaced = 0;
            foreach (var owner in props)
            {
                var existing = owner.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.name == ShadowName && Owns(owner, r.transform));
                if (existing) { usedMeshes.Add(AssetDatabase.GetAssetPath(existing.GetComponent<MeshFilter>().sharedMesh)); continue; }
                var sprites = owner.GetComponentsInChildren<SpriteRenderer>(true).Where(s => s.sprite && s.enabled &&
                    s.color.a > .75f && Owns(owner, s.transform) && !s.name.Contains("Shadow")).ToArray();
                var baseSprite = sprites.OrderBy(s => s.sortingOrder).FirstOrDefault();
                var col = owner.GetComponentsInChildren<Collider2D>(true).FirstOrDefault(c => !c.isTrigger && Owns(owner, c.transform));
                // Sprite furniture containers have no spatial component: the
                // streamer manages their base sprites separately. Parent under
                // that base so the shadow is never independently suspended.
                // This also follows destruction when DoorData is on the base.
                var parent = baseSprite ? baseSprite.transform : owner;
                var outline = Footprint(owner, sprites, col);
                if (outline == null || outline.Length < 3) throw new Exception("No reviewed footprint: " + scene.name + "/" + owner.name);
                var shape = new Shape();
                if (BaseOnly(owner))
                {
                    var center = outline.Aggregate(Vector2.zero, (a, b) => a + b) / outline.Length;
                    var radius = new Vector2((outline.Max(p => p.x) - outline.Min(p => p.x)) * .7f + .07f,
                        (outline.Max(p => p.y) - outline.Min(p => p.y)) * .55f + .05f);
                    shape.Ellipse(parent, center + new Vector2(.025f, -.035f), radius);
                }
                else shape.Contact(parent, outline);
                var go = new GameObject(ShadowName, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(parent, false); go.layer = LayerMask.NameToLayer("Default");
                go.GetComponent<MeshFilter>().sharedMesh = shape.Save();
                usedMeshes.Add(AssetDatabase.GetAssetPath(go.GetComponent<MeshFilter>().sharedMesh));
                var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = mat;
                r.sortingLayerID = baseSprite ? baseSprite.sortingLayerID : 0;
                // Projected strips are outside the furniture silhouette. -5 clears
                // roads (-6), while beds/fountains with lower layers keep their
                // own ordering; never move or recolor the original renderer.
                r.sortingOrder = baseSprite ? Mathf.Min(-5, baseSprite.sortingOrder - 1) : -5;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
                r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off; r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                var old = owner.GetComponent<LevelContactShadow>();
                if (old) { Object.DestroyImmediate(old); replaced++; }
                added++;
            }
            if (!scene.name.Contains("Garden") && !scene.name.Contains("Desert"))
            {
                var floor = AssetDatabase.LoadAssetAtPath<Material>(Art + "/" + scene.name + "/Floor.mat");
                if (!floor) throw new Exception("Missing scene floor material");
                ConfigureFloor(floor,scene.name);
            }
            foreach (var p in untouched)
                if (!p.Key || p.Value != (string)signature.Invoke(null, new object[] { p.Key })) throw new Exception("Gameplay changed: " + p.Key);
            foreach (var p in renderers)
                if (!p.Key || p.Value != EditorJsonUtility.ToJson(p.Key)) throw new Exception("Original renderer changed: " + p.Key);
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log("PROP_FLOOR_PASS " + scene.name + " added=" + added + " replaced=" + replaced +
                " gameplay/colors/renderers=unchanged runtimeUpdates=0");
        }
        // This folder contains only this tool's generated footprints, inside
        // the isolated clone. Remove superseded meshes, never source assets.
        foreach (var guid in AssetDatabase.FindAssets("t:Mesh", new[]{Art+"/Props"}))
        {
            string path=AssetDatabase.GUIDToAssetPath(guid);
            if(path.StartsWith(Art+"/Props/Contact_")&&!usedMeshes.Contains(path))AssetDatabase.DeleteAsset(path);
        }
        Debug.Log("PROP_SHARED_MESHES "+usedMeshes.Count);
    }

    public static void RefreshFloors()
    {
        Isolate();
        foreach(var path in LevelVisualRollout.Scenes)
        {
            string name=Path.GetFileNameWithoutExtension(path);
            if(name.Contains("Garden")||name.Contains("Desert"))continue;
            var floor=AssetDatabase.LoadAssetAtPath<Material>(Art+"/"+name+"/Floor.mat");
            if(!floor)throw new Exception("Missing floor: "+name);
            ConfigureFloor(floor,name);
            Debug.Log("FLOOR_THEME "+name+" pattern="+floor.GetFloat("_Pattern")+" tile="+floor.GetVector("_Tile"));
        }
        AssetDatabase.SaveAssets();
    }
    static void ConfigureFloor(Material floor,string name)
    {
        floor.SetFloat("_Contrast", .19f); floor.SetFloat("_SeamWidth", .035f);
        floor.SetFloat("_Variation", .009f); floor.SetFloat("_Facet", .018f);
        Vector2 tile=name.EndsWith("Floor3")?new Vector2(2.6f,2.6f):
            name.EndsWith("Floor-1")?new Vector2(2.65f,1.65f):
            name.EndsWith("Floor2")?new Vector2(2.55f,1.85f):new Vector2(2.25f,1.65f);
        int pattern=name.EndsWith("Floor3")?1:0;
        if(name=="Level0") {pattern=4;tile=new Vector2(3.6f,2.4f);floor.SetFloat("_Facet",.006f);}
        else if(name=="Level2-Floor-1") {pattern=6;tile=new Vector2(2.8f,1.25f);floor.SetFloat("_Facet",.008f);}
        else if(name.StartsWith("Level2-"))
        {pattern=5;tile=Vector2.one*(name.EndsWith("Floor2")?2.1f:1.8f);floor.SetFloat("_Contrast",.17f);}
        floor.SetFloat("_Pattern",pattern);floor.SetVector("_Tile",new Vector4(tile.x,tile.y,0,0));EditorUtility.SetDirty(floor);
    }

    static Vector2[] Footprint(Transform owner, SpriteRenderer[] sprites, Collider2D col)
    {
        bool round = owner.GetComponent<GeometricCurvedTable>() || owner.GetComponent<GeometricStorageFurniture>();
        if ((round || BaseOnly(owner) || sprites.Length == 0) && col)
        {
            if (col is PolygonCollider2D poly) return poly.GetPath(0).Select(p => (Vector2)col.transform.TransformPoint(p + poly.offset)).ToArray();
            if (col is CircleCollider2D circle) return Circle(col.transform, circle.offset, Vector2.one * circle.radius);
            if (col is BoxCollider2D box) return Rectangle(col.transform, box.offset - box.size * .5f, box.offset + box.size * .5f);
        }
        if (sprites.Length == 0) return null;
        var first = sprites.OrderBy(s => s.sortingOrder).First();
        if (first.sprite.name.IndexOf("Circle", StringComparison.OrdinalIgnoreCase) >= 0)
            return Circle(first.transform, first.sprite.bounds.center, first.sprite.bounds.extents);
        // Use owner-local corners, never renderer.worldBounds (rotated chair bug).
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity), max = -min;
        foreach (var s in sprites)
        {
            var bounds = s.drawMode == SpriteDrawMode.Simple ? s.sprite.bounds : new Bounds(Vector3.zero, s.size);
            foreach (var p in Rectangle(s.transform, bounds.min, bounds.max))
            { var local = (Vector2)owner.InverseTransformPoint(p); min = Vector2.Min(min, local); max = Vector2.Max(max, local); }
        }
        return Rectangle(owner, min, max);
    }
    static Vector2[] Rectangle(Transform t, Vector2 min, Vector2 max) => new[] { min, new Vector2(max.x,min.y), max, new Vector2(min.x,max.y) }
        .Select(p => (Vector2)t.TransformPoint(p)).ToArray();
    static Vector2[] Circle(Transform t, Vector2 center, Vector2 radius) => Enumerable.Range(0, 48)
        .Select(i => (Vector2)t.TransformPoint(center + Vector2.Scale(radius, new Vector2(Mathf.Cos(i*Mathf.PI/24),Mathf.Sin(i*Mathf.PI/24))))).ToArray();

    sealed class Shape
    {
        readonly List<Vector3> vertices = new List<Vector3>(); readonly List<Color> colors = new List<Color>(); readonly List<int> triangles = new List<int>();
        static readonly Color Ink = new Color(6/255f,12/255f,21/255f,.27f);
        void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, bool feather = false)
        {
            int n = vertices.Count; vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            var clear = Ink; clear.a = 0; colors.Add(Ink); colors.Add(feather ? clear : Ink); colors.Add(feather ? clear : Ink); colors.Add(Ink);
            triangles.AddRange(new[] {n,n+1,n+2,n,n+2,n+3});
        }
        public void Contact(Transform parent, Vector2[] outline)
        {
            float area = 0;
            for (int i=0;i<outline.Length;i++) { var a=outline[i];var b=outline[(i+1)%outline.Length];area+=a.x*b.y-a.y*b.x; }
            if(area<0)Array.Reverse(outline);
            Vector2 offset=new Vector2(.065f,-.09f);
            for(int i=0;i<outline.Length;i++)
            {
                var a=outline[i];var b=outline[(i+1)%outline.Length];var d=(b-a).normalized;
                if(Vector2.Dot(new Vector2(d.y,-d.x),offset)<=.001f)continue;
                Quad(parent.InverseTransformPoint(a),parent.InverseTransformPoint(a+offset),parent.InverseTransformPoint(b+offset),parent.InverseTransformPoint(b));
            }
        }
        public void Ellipse(Transform parent, Vector2 center, Vector2 radius)
        {
            for(int i=0;i<24;i++)
            {
                Vector2 a=Vector2.Scale(radius,new Vector2(Mathf.Cos(i*Mathf.PI/12),Mathf.Sin(i*Mathf.PI/12)));
                Vector2 b=Vector2.Scale(radius,new Vector2(Mathf.Cos((i+1)*Mathf.PI/12),Mathf.Sin((i+1)*Mathf.PI/12)));
                Quad(parent.InverseTransformPoint(center),parent.InverseTransformPoint(center+a*.7f),parent.InverseTransformPoint(center+b*.7f),parent.InverseTransformPoint(center));
                Quad(parent.InverseTransformPoint(center+a*.7f),parent.InverseTransformPoint(center+a),parent.InverseTransformPoint(center+b),parent.InverseTransformPoint(center+b*.7f),true);
            }
        }
        public Mesh Save()
        {
            // Share identical baked footprints across scene instances; no runtime
            // allocations, scanning, shape generation or per-prop Update calls.
            for(int i=0;i<vertices.Count;i++)vertices[i]=new Vector3(Mathf.Round(vertices[i].x*10000)/10000,Mathf.Round(vertices[i].y*10000)/10000,0);
            string key=string.Join("|",vertices.Select(v=>v.ToString("F4")))+string.Join("|",colors.Select(c=>c.a));
            string path=Art+"/Props/Contact_"+Hash128.Compute(key)+".asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(mesh)return mesh;
            mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);
            mesh.SetUVs(0,Enumerable.Repeat(Vector2.zero,vertices.Count).ToList());mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
    }
}
#endif
