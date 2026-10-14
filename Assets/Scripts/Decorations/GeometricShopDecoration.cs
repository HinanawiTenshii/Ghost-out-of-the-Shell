using System.Collections.Generic;
using UnityEngine;

/// <summary>Strict overhead shop displays, without per-frame geometry generation.</summary>
[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(BoxCollider2D))]
[AddComponentMenu("Decorations/Geometric Shop Decoration")]
public sealed class GeometricShopDecoration : MonoBehaviour
{
    public enum FurnitureShape { SwordDisplay, PolearmDisplay, ToolWorkbench, SupplyTray, ShieldDisplay, HelmetDisplay }
    [SerializeField] private FurnitureShape shape;
    [SerializeField] private Vector2 size = new Vector2(2.2f, 1.5f);
    [Header("Shop Palette / 店铺配色")]
    [SerializeField] private Color woodColor = new Color(0.67f, 0.39f, 0.2f, 1f);
    [SerializeField] private Color metalColor = new Color(0.67f, 0.71f, 0.74f, 1f);
    [SerializeField] private Color accentColor = new Color(.22f, .57f, .56f, 1f);
    [SerializeField] private bool solid = true;
    [SerializeField] private int sortingOrder = -3;
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private Material sourceMaterial;

    public struct Part
    {
        public Vector2[] Points;
        public Color Color;
        public Part(Vector2[] points, Color color) { Points = points; Color = color; }
    }

    private GameObject visual;
    private Mesh mesh;
    private Material ownedMaterial;
    private bool dirty = true;

    private void OnEnable() { if (dirty || visual == null) Rebuild(); else visual.SetActive(true); }
    private void OnValidate()
    {
        size = new Vector2(Mathf.Max(0.2f, size.x), Mathf.Max(0.2f, size.y));
        dirty = true;
    }
    private void Update() { if (dirty || visual == null) Rebuild(); }
    private void OnDisable() { if (visual != null) visual.SetActive(false); }
    private void OnDestroy() { Release(); }

    private void Rebuild()
    {
        Release();
        dirty = false;
        Vector2 reference = ReferenceSize(shape);
        Vector2 scale = new Vector2(size.x / reference.x, size.y / reference.y);
        BoxCollider2D collider = GetComponent<BoxCollider2D>();
        collider.enabled = solid;
        collider.isTrigger = false;
        collider.offset = Vector2.zero;
        collider.size = size;

        Material material = sourceMaterial;
        if (material == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) return;
            ownedMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            material = ownedMaterial;
        }
        List<Part> parts = BuildParts(shape, woodColor, metalColor, accentColor);
        var vertices = new List<Vector3>();
        var colors = new List<Color>();
        var triangles = new List<int>();
        for (int p = 0; p < parts.Count; p++)
        {
            Part part = parts[p];
            int first = vertices.Count;
            foreach (Vector2 point in part.Points)
            {
                vertices.Add(new Vector3(point.x * scale.x, point.y * scale.y, -p * 0.0001f));
                colors.Add(part.Color);
            }
            // All visible parts are convex and lie flat on the XY tabletop.
            for (int i = 1; i < part.Points.Length - 1; i++)
            {
                triangles.Add(first); triangles.Add(first + i + 1); triangles.Add(first + i);
            }
        }
        mesh = new Mesh { name = "Geometric " + shape, hideFlags = HideFlags.HideAndDontSave };
        mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0);
        mesh.uv = new Vector2[vertices.Count];
        mesh.RecalculateBounds();
        visual = new GameObject("Generated Shop Geometry") { hideFlags = HideFlags.HideAndDontSave, layer = gameObject.layer };
        visual.transform.SetParent(transform, false);
        visual.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = visual.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.sortingLayerName = sortingLayerName;
        renderer.sortingOrder = sortingOrder;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }


    public static Vector2 ReferenceSize(FurnitureShape shape)
    {
        switch (shape)
        {
            case FurnitureShape.SwordDisplay: return new Vector2(2.2f, 1.5f);
            case FurnitureShape.PolearmDisplay: return new Vector2(2.6f, 1.3f);
            case FurnitureShape.ToolWorkbench: return new Vector2(2.2f, 1.5f);
            case FurnitureShape.SupplyTray: return new Vector2(1.8f, 1.3f);
            case FurnitureShape.ShieldDisplay: return new Vector2(2.2f, 1.5f);
            default: return new Vector2(2.0f, 1.4f);
        }
    }

    public static List<Part> BuildParts(FurnitureShape shape, Color wood, Color metal, Color accent)
    {
        var p = new List<Part>();
        Color edge = new Color(.38f, .20f, .11f, 1);
        Color iron = new Color(.25f, .30f, .33f, 1);
        Color light = Color.Lerp(metal, Color.white, .48f);
        Color leather = new Color(.51f, .28f, .14f, 1);
        Color brass = new Color(.82f, .57f, .24f, 1);
        Vector2 size = ReferenceSize(shape);
        p.Add(Rect(0, 0, size.x, size.y, edge));
        p.Add(Rect(0, 0, size.x - .14f, size.y - .14f, wood));
        if (shape != FurnitureShape.SupplyTray)
            p.Add(Rect(0, 0, size.x - .32f, size.y - .32f, accent));
        switch (shape)
        {
            case FurnitureShape.SwordDisplay:
                // Three swords lying flat on cloth. No upright rack/front elevation.
                Sword(p, -.65f, -.52f, 1.02f, metal, light, iron, leather, brass);
                Sword(p, 0, -.56f, 1.13f, metal, light, iron, leather, brass);
                Sword(p, .65f, -.45f, .86f, metal, light, iron, leather, brass);
                break;
            case FurnitureShape.PolearmDisplay:
                p.Add(Rect(-.15f, .27f, 1.84f, .09f, leather));
                p.Add(Rect(.58f, .27f, .19f, .14f, iron));
                p.Add(Diamond(.88f, .27f, .46f, .25f, metal));
                p.Add(Rect(.83f, .27f, .23f, .045f, light));
                p.Add(Rect(-.98f, .27f, .10f, .13f, brass));
                // Axe haft along X, broad blade spreads along Y.
                p.Add(Rect(-.12f, -.25f, 1.86f, .10f, leather));
                p.Add(Rect(-.81f, -.25f, .30f, .14f, edge));
                p.Add(Rect(.64f, -.25f, .15f, .22f, iron));
                p.Add(new Part(new[] { new Vector2(.53f,-.17f), new Vector2(.76f,-.17f),
                    new Vector2(.91f,-.02f), new Vector2(.43f,-.02f) }, metal));
                p.Add(new Part(new[] { new Vector2(.43f,-.49f), new Vector2(.91f,-.49f),
                    new Vector2(.76f,-.33f), new Vector2(.53f,-.33f) }, metal));
                p.Add(Rect(.67f, -.045f, .43f, .045f, light));
                p.Add(Rect(.67f, -.465f, .43f, .045f, light));
                break;
            case FurnitureShape.ToolWorkbench:
                // Hammer, hand saw and pliers all rest flat on the bench.
                p.Add(Rect(-.61f, -.20f, .12f, .65f, leather));
                p.Add(Rect(-.61f, .18f, .55f, .23f, iron));
                p.Add(Rect(-.61f, .21f, .48f, .12f, metal));
                p.Add(Rect(-.61f, -.40f, .16f, .22f, edge));
                p.Add(Rect(.04f, -.27f, .28f, .35f, leather));
                p.Add(Rect(.01f, -.27f, .11f, .18f, accent));
                p.Add(Rect(.57f, -.23f, .80f, .23f, metal));
                p.Add(Rect(.57f, -.15f, .80f, .055f, light));
                foreach(float x in new[] { .27f, .47f, .67f, .87f })
                    p.Add(new Part(new[] {new Vector2(x,-.43f),new Vector2(x+.09f,-.34f),new Vector2(x-.09f,-.34f)}, metal));
                p.Add(new Part(new[] {new Vector2(.28f,.03f),new Vector2(.36f,.01f),
                    new Vector2(.64f,.46f),new Vector2(.56f,.50f)}, iron));
                p.Add(new Part(new[] {new Vector2(.55f,.01f),new Vector2(.64f,.04f),
                    new Vector2(.37f,.50f),new Vector2(.28f,.46f)}, iron));
                p.Add(Disc(.46f, .26f, .065f, metal));
                break;
            case FurnitureShape.SupplyTray:
                // Open divided tray, contents visible from above.
                p.Add(Rect(-.35f, 0, .86f, 1.03f, edge));
                p.Add(Rect(.45f, .26f, .58f, .49f, edge));
                p.Add(Rect(.45f, -.29f, .58f, .44f, edge));
                foreach(float x in new[] { -.61f, -.35f, -.09f })
                {
                    p.Add(Rect(x, -.20f, .12f, .44f, leather));
                    p.Add(Rect(x, .18f, .09f, .34f, metal));
                    p.Add(Rect(x, .38f, .15f, .06f, light));
                }
                // Rope coil: concentric overhead bands, no cylinder front.
                p.Add(Disc(.45f, .26f, .20f, wood));
                p.Add(Disc(.45f, .26f, .15f, edge));
                p.Add(Disc(.45f, .26f, .11f, wood));
                p.Add(Disc(.45f, .26f, .055f, edge));
                foreach(float x in new[] { .28f, .43f, .58f })
                {
                    p.Add(Rect(x, -.29f, .045f, .25f, metal));
                    p.Add(Rect(x, -.19f, .11f, .045f, light));
                }
                break;
            case FurnitureShape.ShieldDisplay:
                // Shields laid face-up on the countertop.
                p.Add(Disc(-.52f, 0, .48f, iron));
                p.Add(Disc(-.52f, 0, .42f, metal));
                p.Add(Disc(-.52f, 0, .35f, leather));
                p.Add(Rect(-.52f, 0, .08f, .64f, brass));
                p.Add(Rect(-.52f, 0, .64f, .08f, brass));
                p.Add(Disc(-.52f, 0, .12f, metal));
                p.Add(new Part(new[] {new Vector2(.48f,-.51f),new Vector2(.87f,-.05f),
                    new Vector2(.87f,.45f),new Vector2(.09f,.45f),new Vector2(.09f,-.05f)}, iron));
                p.Add(new Part(new[] {new Vector2(.48f,-.42f),new Vector2(.79f,-.02f),
                    new Vector2(.79f,.37f),new Vector2(.17f,.37f),new Vector2(.17f,-.02f)}, metal));
                p.Add(new Part(new[] {new Vector2(.48f,-.31f),new Vector2(.69f,.01f),
                    new Vector2(.69f,.28f),new Vector2(.27f,.28f),new Vector2(.27f,.01f)}, accent));
                p.Add(Rect(.48f, .02f, .08f, .49f, brass));
                break;
            case FurnitureShape.HelmetDisplay:
                // Helmets are crown views, NOT faces/masks. Gauntlets lie alongside.
                Helmet(p, -.47f, .14f, .32f, iron, metal, light, brass);
                Helmet(p, .37f, .18f, .28f, iron, metal, light, brass);
                foreach(float x in new[] { -.43f, .33f })
                {
                    p.Add(Rect(x, -.40f, .34f, .22f, iron));
                    p.Add(Rect(x, -.39f, .26f, .14f, metal));
                    p.Add(Rect(x, -.23f, .23f, .14f, metal));
                    p.Add(Rect(x, -.27f, .23f, .04f, light));
                    p.Add(Rect(x+.16f, -.29f, .08f, .13f, iron));
                }
                break;
        }
        return p;
    }
    private static void Sword(List<Part> p, float x, float y, float length, Color metal, Color light, Color iron, Color leather, Color brass)
    {
        p.Add(Rect(x, y+.13f, .11f, .22f, leather));
        p.Add(Rect(x, y, .18f, .08f, brass));
        p.Add(Rect(x, y+.25f, .42f, .10f, iron));
        float baseY=y+.30f, tip=y+length;
        p.Add(new Part(new[] {new Vector2(x-.10f,baseY),new Vector2(x+.10f,baseY),
            new Vector2(x+.10f,tip-.14f),new Vector2(x,tip),new Vector2(x-.10f,tip-.14f)},metal));
        p.Add(Rect(x-.025f, (baseY+tip-.14f)*.5f, .045f, tip-.14f-baseY, light));
    }
    private static void Helmet(List<Part> p, float x, float y, float r, Color iron, Color metal, Color light, Color brass)
    {
        p.Add(Disc(x,y,r,iron));
        p.Add(Disc(x,y,r*.86f,metal));
        p.Add(Disc(x,y,r*.57f,light));
        p.Add(Rect(x,y,r*.18f,r*1.66f,brass));
    }
    private static Part Rect(float x, float y, float w, float h, Color color) => new Part(new[] {
        new Vector2(x-w/2,y-h/2),new Vector2(x+w/2,y-h/2),
        new Vector2(x+w/2,y+h/2),new Vector2(x-w/2,y+h/2)},color);
    private static Part Diamond(float x, float y, float w, float h, Color color) => new Part(new[] {
        new Vector2(x,y-h/2),new Vector2(x+w/2,y),new Vector2(x,y+h/2),new Vector2(x-w/2,y)},color);
    private static Part Disc(float x, float y, float radius, Color color)
    {
        var points=new Vector2[12];
        for(int i=0;i<12;i++){float a=i*Mathf.PI*2/12;points[i]=new Vector2(x+Mathf.Cos(a)*radius,y+Mathf.Sin(a)*radius);}
        return new Part(points,color);
    }

    private void Release()
    {
        if (visual != null) visual.SetActive(false);
        ReleaseObject(visual); ReleaseObject(mesh); ReleaseObject(ownedMaterial);
        visual = null; mesh = null; ownedMaterial = null;
    }
    private static void ReleaseObject(Object target)
    {
        if (target == null) return;
        if (Application.isPlaying) Destroy(target); else DestroyImmediate(target);
    }
}
