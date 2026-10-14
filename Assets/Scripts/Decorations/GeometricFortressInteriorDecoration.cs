using System.Collections.Generic;
using UnityEngine;

/// <summary>Strict overhead fortress interiors. One combined mesh per decoration.</summary>
[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(BoxCollider2D))]
[AddComponentMenu("Decorations/Geometric Fortress Interior Decoration")]
public sealed class GeometricFortressInteriorDecoration : MonoBehaviour
{
    // Append new shapes: existing prefab enum values must remain unchanged.
    public enum FurnitureShape { WritingDesk, ArchiveCabinet, MessTable, WashStand, HeraldicRug, Trapdoor }
    [SerializeField] private FurnitureShape shape;
    [SerializeField] private Vector2 size = new Vector2(2.2f, 1.5f);
    [Header("Interior Palette / 室内配色")]
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
            // All visible parts are convex and viewed directly from above.
            for (int i = 1; i < part.Points.Length - 1; i++)
            {
                triangles.Add(first); triangles.Add(first + i + 1); triangles.Add(first + i);
            }
        }
        mesh = new Mesh { name = "Geometric " + shape, hideFlags = HideFlags.HideAndDontSave };
        mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0);
        mesh.uv = new Vector2[vertices.Count];
        mesh.RecalculateBounds();
        visual = new GameObject("Generated Fortress Interior Geometry") { hideFlags = HideFlags.HideAndDontSave, layer = gameObject.layer };
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
            case FurnitureShape.WritingDesk: return new Vector2(2.2f, 1.5f);
            case FurnitureShape.ArchiveCabinet: return new Vector2(1.8f, 1.3f);
            case FurnitureShape.MessTable: return new Vector2(2.8f, 1.6f);
            case FurnitureShape.WashStand: return new Vector2(1.65f, 1.25f);
            case FurnitureShape.Trapdoor: return new Vector2(1.8f, 1.8f);
            default: return new Vector2(2.2f, 2.8f);
        }
    }

    public static List<Part> BuildParts(FurnitureShape shape, Color wood, Color metal, Color accent)
    {
        var p = new List<Part>();
        Color edge = new Color(.38f, .20f, .11f, 1);
        Color iron = new Color(.25f, .30f, .33f, 1);
        Color light = Color.Lerp(metal, Color.white, .48f);
        Color paper = new Color(.88f, .85f, .68f, 1);
        Color paperShade = new Color(.66f, .57f, .37f, 1);
        Color ink = new Color(.19f, .24f, .28f, 1);
        Color brass = new Color(.81f, .59f, .27f, 1);
        Vector2 dimensions = ReferenceSize(shape);
        switch (shape)
        {
            case FurnitureShape.WritingDesk:
                WoodenTop(p, dimensions, wood, edge);
                p.Add(Rect(-.16f, -.04f, 1.37f, 1.08f, accent));
                // Unrolled parchment with scroll ends, coarse handwriting and wax seal.
                p.Add(Rect(-.22f, -.05f, .86f, .84f, paperShade));
                p.Add(Rect(-.22f, -.02f, .78f, .75f, paper));
                p.Add(Rect(-.22f, .38f, .94f, .10f, paperShade));
                p.Add(Rect(-.22f, .40f, .88f, .06f, paper));
                p.Add(Rect(-.22f, -.43f, .94f, .10f, paperShade));
                p.Add(Rect(-.22f, -.41f, .88f, .06f, paper));
                p.Add(Rect(-.29f, .20f, .46f, .035f, ink));
                p.Add(Rect(-.25f, .08f, .54f, .035f, ink));
                p.Add(Rect(-.32f, -.04f, .40f, .035f, ink));
                p.Add(Rect(-.39f, -.16f, .26f, .035f, ink));
                p.Add(Disc(.03f, -.23f, .09f, new Color(.79f, .14f, .14f, 1)));
                // Inkwell opening and a feather lying flat beside it.
                p.Add(Rect(.71f, -.24f, .29f, .29f, iron));
                p.Add(Disc(.71f, -.24f, .095f, ink));
                p.Add(Disc(.71f, -.24f, .042f, new Color(.06f, .09f, .13f, 1)));
                p.Add(new Part(new[] { new Vector2(.57f,.00f), new Vector2(.75f,.12f),
                    new Vector2(.85f,.49f), new Vector2(.62f,.32f) }, paper));
                Beam(p, .55f, -.06f, .81f, .43f, .032f, paperShade);
                break;
            case FurnitureShape.ArchiveCabinet:
                // Open-top archive drawers: only rims, horizontal volumes and rolled records.
                WoodenTop(p, dimensions, wood, edge);
                p.Add(Rect(-.38f, 0, .77f, 1.04f, edge));
                p.Add(Rect(.43f, 0, .65f, 1.04f, edge));
                p.Add(Rect(.43f, 0, .65f, .065f, wood));
                Book(p, -.40f, .25f, .56f, .39f, accent, paper, brass);
                Book(p, -.37f, -.25f, .59f, .39f, new Color(.65f,.16f,.22f,1), paper, brass);
                foreach (float y in new[] { .25f, -.25f })
                {
                    p.Add(Rect(.43f, y, .48f, .20f, paperShade));
                    p.Add(Rect(.43f, y+.02f, .48f, .13f, paper));
                    p.Add(Rect(.20f, y, .06f, .24f, paper));
                    p.Add(Rect(.66f, y, .06f, .24f, paper));
                    p.Add(Rect(.43f, y, .065f, .20f, accent));
                }
                // Small label tabs lie on the inner rim, not on a vertical front.
                p.Add(Rect(-.38f, -.56f, .24f, .055f, brass));
                p.Add(Rect(.43f, -.56f, .24f, .055f, brass));
                break;
            case FurnitureShape.MessTable:
                WoodenTop(p, dimensions, wood, edge);
                p.Add(Rect(0, 0, 2.54f, .58f, accent));
                p.Add(Rect(0, -.24f, 2.54f, .04f, paper));
                p.Add(Rect(0, .24f, 2.54f, .04f, paper));
                // Pewter dishes and cup openings, arranged along both sides.
                foreach (float x in new[] { -.91f, .91f })
                {
                    foreach (float y in new[] { -.44f, .44f })
                    {
                        p.Add(Disc(x, y, .225f, metal));
                        p.Add(Disc(x, y, .17f, light));
                        float cx = x > 0 ? x-.34f : x+.34f;
                        p.Add(Rect(cx+.058f, y, .13f, .07f, metal));
                        p.Add(Disc(cx, y, .10f, metal));
                        p.Add(Disc(cx, y, .065f, edge));
                    }
                }
                // Loaf on a narrow shared serving board.
                p.Add(Rect(0, 0, .69f, .82f, edge));
                p.Add(Rect(0, 0, .59f, .72f, wood));
                p.Add(new Part(new[] { new Vector2(-.14f,-.26f), new Vector2(.14f,-.26f),
                    new Vector2(.22f,-.16f), new Vector2(.22f,.16f), new Vector2(.14f,.26f),
                    new Vector2(-.14f,.26f), new Vector2(-.22f,.16f), new Vector2(-.22f,-.16f) }, brass));
                foreach (float y in new[] { -.14f, 0f, .14f })
                    Beam(p, -.12f, y-.025f, .12f, y+.025f, .04f, paper);
                break;
            case FurnitureShape.WashStand:
                // Stone counter and recessed water basin; no raised side silhouette.
                p.Add(Rect(0, 0, dimensions.x, dimensions.y, iron));
                p.Add(Rect(0, 0, dimensions.x-.10f, dimensions.y-.10f, metal));
                p.Add(Rect(0, .54f, dimensions.x-.10f, .06f, light));
                p.Add(Rect(-.21f, -.01f, .94f, .87f, light));
                p.Add(Rect(-.21f, -.01f, .80f, .73f, iron));
                p.Add(Rect(-.21f, -.01f, .66f, .59f, new Color(.12f,.44f,.55f,1)));
                p.Add(Rect(-.21f, .025f, .66f, .52f, new Color(.20f,.70f,.73f,1)));
                p.Add(Rect(-.36f, .16f, .21f, .035f, light));
                p.Add(Rect(-.08f, -.13f, .12f, .035f, light));
                // Overhead jug mouth, loop handle, and spout pointing toward the basin.
                p.Add(Disc(.59f, .31f, .125f, edge));
                p.Add(Disc(.59f, .31f, .065f, metal));
                p.Add(Disc(.47f, .30f, .20f, edge));
                p.Add(Disc(.47f, .30f, .16f, wood));
                p.Add(Disc(.47f, .30f, .10f, iron));
                p.Add(Disc(.47f, .30f, .065f, new Color(.16f,.52f,.59f,1)));
                p.Add(new Part(new[] {new Vector2(.28f,.20f), new Vector2(.36f,.26f),
                    new Vector2(.36f,.34f),new Vector2(.23f,.31f)},wood));
                p.Add(Rect(.49f, -.27f, .41f, .38f, accent));
                p.Add(Rect(.49f, -.37f, .41f, .045f, paper));
                p.Add(Rect(.47f, -.20f, .23f, .12f, paper));
                break;
            case FurnitureShape.Trapdoor:
                // A closed hatch viewed straight down: recessed rim, flat boards,
                // two hinges and a ring lying on the lid. No elevated front/side face.
                p.Add(Rect(0, 0, 1.8f, 1.8f, iron));
                p.Add(Rect(0, 0, 1.7f, 1.7f, metal));
                p.Add(Rect(0, 0, 1.52f, 1.52f, ink));
                p.Add(Rect(0, 0, 1.44f, 1.44f, edge));
                p.Add(Rect(0, .82f, 1.7f, .045f, light));
                p.Add(Rect(-.82f, 0, .035f, 1.61f, light));
                for (int plank = 0; plank < 5; plank++)
                {
                    Color board = plank == 2 ? wood : Color.Lerp(wood,
                        plank % 2 == 0 ? edge : new Color(.84f, .55f, .28f, 1), .16f);
                    p.Add(Rect((plank-2)*.28f, 0, .26f, 1.36f, board));
                }
                foreach (float x in new[] { -.44f, .44f })
                {
                    p.Add(Rect(x, .48f, .16f, .43f, iron));
                    p.Add(Rect(x, .49f, .10f, .37f, metal));
                    p.Add(Rect(x, .71f, .28f, .10f, iron));
                    p.Add(Rect(x, .73f, .25f, .04f, light));
                    p.Add(Rect(x, .34f, .04f, .04f, iron));
                }
                // One broad cross brace and coarse rivets, readable at gameplay scale.
                p.Add(Rect(0, .02f, 1.36f, .13f, iron));
                p.Add(Rect(0, .035f, 1.32f, .075f, metal));
                foreach (float x in new[] { -.57f, .57f })
                    p.Add(Rect(x, .035f, .04f, .04f, light));
                p.Add(Rect(0, -.25f, .18f, .14f, iron));
                p.Add(Rect(0, -.25f, .10f, .09f, metal));
                p.Add(Disc(0, -.42f, .175f, iron));
                p.Add(Disc(0, -.415f, .14f, light));
                p.Add(Disc(0, -.415f, .088f, wood));
                break;
            case FurnitureShape.HeraldicRug:
                Color darkCloth = Color.Lerp(accent, ink, .55f);
                // Flat floor textile, with restrained fringes and a geometric tower crest.
                p.Add(Rect(0, 0, 2.2f, 2.56f, darkCloth));
                p.Add(Rect(0, 0, 2.06f, 2.42f, brass));
                p.Add(Rect(0, 0, 1.92f, 2.28f, accent));
                p.Add(Rect(0, 0, 1.68f, 2.04f, darkCloth));
                p.Add(Rect(0, 0, 1.60f, 1.96f, accent));
                foreach (float x in new[] { -.9f,-.6f,-.3f,0f,.3f,.6f,.9f })
                {
                    p.Add(Rect(x, -1.34f, .10f, .12f, paper));
                    p.Add(Rect(x, 1.34f, .10f, .12f, paper));
                }
                p.Add(Diamond(0, 0, 1.24f, 1.61f, brass));
                p.Add(Diamond(0, 0, 1.06f, 1.40f, darkCloth));
                p.Add(Rect(0, -.11f, .52f, .60f, paper));
                p.Add(Rect(0, .19f, .64f, .14f, paper));
                foreach (float x in new[] { -.23f,0f,.23f })
                    p.Add(Rect(x, .30f, .17f, .13f, paper));
                p.Add(Rect(0, -.27f, .17f, .28f, darkCloth));
                p.Add(Rect(0, .03f, .10f, .15f, darkCloth));
                foreach (float y in new[] { -.88f, .88f })
                    p.Add(Diamond(0, y, .20f, .16f, paper));
                break;
        }
        return p;
    }

    private static void WoodenTop(List<Part> p, Vector2 size, Color wood, Color edge)
    {
        p.Add(Rect(0, 0, size.x, size.y, edge));
        p.Add(Rect(0, 0, size.x-.12f, size.y-.12f, wood));
        p.Add(Rect(0, size.y*.5f-.10f, size.x-.12f, .045f, Color.Lerp(wood, Color.white, .18f)));
    }

    private static void Book(List<Part> p, float x, float y, float w, float h, Color cover, Color paper, Color brass)
    {
        p.Add(Rect(x, y, w, h, cover));
        p.Add(Rect(x+.018f, y-.015f, w-.09f, h-.06f, paper));
        p.Add(Rect(x-.014f, y+.024f, w-.04f, h-.055f, cover));
        p.Add(Rect(x-w*.5f+.052f, y+.013f, .045f, h-.03f, brass));
        p.Add(Rect(x+.075f, y+.02f, w*.32f, .07f, brass));
    }

    private static void Beam(List<Part> p, float ax, float ay, float bx, float by, float width, Color color)
    {
        float dx=bx-ax, dy=by-ay, length=Mathf.Sqrt(dx*dx+dy*dy);
        float nx=-dy/length*width*.5f, ny=dx/length*width*.5f;
        p.Add(new Part(new[] { new Vector2(ax-nx,ay-ny),new Vector2(bx-nx,by-ny),
            new Vector2(bx+nx,by+ny),new Vector2(ax+nx,ay+ny)},color));
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
