using System.Collections.Generic;
using UnityEngine;

/// <summary>Top-down fortress warning bell in an open iron frame; one inexpensive mesh.</summary>
[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(AlarmDevice))]
public sealed class AlarmDeviceVisual : MonoBehaviour
{
    [SerializeField] private int sortingOrder = 5;
    private GameObject generated;
    private Mesh mesh;
    private Material material;
    private AlarmDevice alarm;
    private Color[] colors;
    private int runeStart;
    private Color lastRune;
    private bool dirty = true;

    private void OnEnable() { alarm = GetComponent<AlarmDevice>(); dirty = true; }
    private void OnValidate() { dirty = true; }
    private void LateUpdate()
    {
        if (dirty || generated == null) Rebuild();
        if (mesh == null) return;
        bool powered = Application.isPlaying && alarm != null && alarm.HasPower;
        Color rune = powered ? new Color(.95f, .12f, .09f) : new Color(.36f, .40f, .42f);
        if (powered && alarm.IsFlashing && Mathf.Sin(Time.time * 22f) > 0f)
            rune = new Color(1f, .67f, .48f);
        if (rune == lastRune) return;
        lastRune = rune;
        for (int i = runeStart; i < runeStart + 4; i++) colors[i] = rune;
        mesh.colors = colors;
    }

    private void Rebuild()
    {
        Release();
        dirty = false;
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) return;
        material = new Material(shader) { name = "Alarm Surface", hideFlags = HideFlags.HideAndDontSave };
        generated = new GameObject("Generated Alarm Bell", typeof(MeshFilter), typeof(MeshRenderer))
            { hideFlags = HideFlags.HideAndDontSave, layer = gameObject.layer };
        generated.transform.SetParent(transform, false);
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        var tint = new List<Color>();
        Color edge = new Color(.23f, .27f, .29f), steel = new Color(.59f, .65f, .67f);
        Color light = new Color(.82f, .87f, .86f), inset = new Color(.15f, .18f, .20f);
        Color redShadow = new Color(.57f, .07f, .055f), red = new Color(.85f, .13f, .09f);
        // Open forged-iron trestle: visible ground between members, not a sealed
        // electrical cabinet. Keep the original footprint and bottom cable port.
        for (int side = -1; side <= 1; side += 2)
        {
            Rect(vertices, triangles, tint, side * .37f, 0f, .16f, 1.1f, edge);
            Rect(vertices, triangles, tint, side * .37f, .01f, .08f, .96f, steel);
        }
        Rect(vertices, triangles, tint, 0f, .43f, .82f, .16f, edge);
        Rect(vertices, triangles, tint, 0f, .45f, .74f, .07f, light);
        Rect(vertices, triangles, tint, 0f, -.43f, .82f, .16f, edge);
        Rect(vertices, triangles, tint, 0f, -.41f, .70f, .07f, steel);
        // Red binding on the upper yoke, with a visible hanger beneath it.
        Rect(vertices, triangles, tint, 0f, .43f, .22f, .17f, redShadow);
        Rect(vertices, triangles, tint, -.02f, .445f, .13f, .14f, red);
        Rect(vertices, triangles, tint, 0f, .28f, .09f, .25f, edge);
        // Seen from ABOVE: a faceted red dome and exposed iron rim, not a
        // side-facing bell silhouette or front-facing control panel.
        Disc(vertices, triangles, tint, 0f, .03f, .325f, inset);
        Disc(vertices, triangles, tint, 0f, .035f, .295f, light);
        Disc(vertices, triangles, tint, 0f, .045f, .255f, redShadow);
        Disc(vertices, triangles, tint, -.02f, .065f, .215f, red);
        Rect(vertices, triangles, tint, 0f, .10f, .11f, .18f, steel);
        Rect(vertices, triangles, tint, 0f, .105f, .05f, .08f, redShadow);
        // Exposed pivot and wooden-handled iron striker replace the modern
        // vents and pushbutton. Keep coarse, readable construction under CRT.
        Disc(vertices, triangles, tint, .27f, -.33f, .07f, inset);
        Disc(vertices, triangles, tint, .27f, -.33f, .04f, steel);
        Bar(vertices, triangles, tint, new Vector2(.27f, -.33f), new Vector2(.22f, -.12f), .085f, edge);
        Bar(vertices, triangles, tint, new Vector2(.27f, -.33f), new Vector2(.22f, -.12f), .045f, new Color(.53f, .27f, .10f));
        Rect(vertices, triangles, tint, .22f, -.12f, .19f, .13f, edge);
        Rect(vertices, triangles, tint, .205f, -.10f, .14f, .07f, light);
        // Inset magic stone retains power/activation feedback without an LED panel.
        Diamond(vertices, triangles, tint, 0f, -.41f, .16f, .14f, inset);
        runeStart = vertices.Count;
        Diamond(vertices, triangles, tint, 0f, -.41f, .09f, .085f, steel);
        Rect(vertices, triangles, tint, 0f, -.52f, .14f, .06f, edge);
        mesh = new Mesh { name = "Top Down Alarm", hideFlags = HideFlags.HideAndDontSave };
        mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
        colors = tint.ToArray(); mesh.colors = colors; mesh.RecalculateBounds();
        generated.GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = generated.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.sortingOrder = sortingOrder;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        lastRune = Color.clear;
    }

    private static void Rect(List<Vector3> v, List<int> t, List<Color> c, float x, float y, float w, float h, Color color)
    {
        int n = v.Count; float z = -.0001f * n;
        v.Add(new Vector3(x-w*.5f, y-h*.5f, z)); v.Add(new Vector3(x-w*.5f, y+h*.5f, z));
        v.Add(new Vector3(x+w*.5f, y+h*.5f, z)); v.Add(new Vector3(x+w*.5f, y-h*.5f, z));
        for (int i = 0; i < 4; i++) c.Add(color);
        t.Add(n); t.Add(n+1); t.Add(n+2); t.Add(n); t.Add(n+2); t.Add(n+3);
    }

    private static void Disc(List<Vector3> v, List<int> t, List<Color> c, float x, float y, float r, Color color)
    {
        const int sides = 12;
        int n = v.Count; float z = -.0001f * n;
        v.Add(new Vector3(x, y, z)); c.Add(color);
        for (int i = 0; i < sides; i++)
        {
            float angle = i * Mathf.PI * 2f / sides;
            v.Add(new Vector3(x + r * Mathf.Cos(angle), y + r * Mathf.Sin(angle), z)); c.Add(color);
            t.Add(n); t.Add(n+1+(i+1)%sides); t.Add(n+1+i);
        }
    }

    private static void Bar(List<Vector3> v, List<int> t, List<Color> c, Vector2 a, Vector2 b, float width, Color color)
    {
        Vector2 delta = (b - a).normalized;
        Vector2 side = new Vector2(-delta.y, delta.x) * (width * .5f);
        Quad(v, t, c, a - side, a + side, b + side, b - side, color);
    }

    private static void Diamond(List<Vector3> v, List<int> t, List<Color> c, float x, float y, float w, float h, Color color)
    {
        Quad(v, t, c, new Vector2(x, y-h*.5f), new Vector2(x-w*.5f, y),
            new Vector2(x, y+h*.5f), new Vector2(x+w*.5f, y), color);
    }

    private static void Quad(List<Vector3> v, List<int> t, List<Color> c, Vector2 a, Vector2 b, Vector2 d, Vector2 e, Color color)
    {
        int n = v.Count; float z = -.0001f * n;
        v.Add(new Vector3(a.x, a.y, z)); v.Add(new Vector3(b.x, b.y, z));
        v.Add(new Vector3(d.x, d.y, z)); v.Add(new Vector3(e.x, e.y, z));
        for (int i = 0; i < 4; i++) c.Add(color);
        t.Add(n); t.Add(n+1); t.Add(n+2); t.Add(n); t.Add(n+2); t.Add(n+3);
    }

    private void Release()
    {
        if (generated != null) generated.SetActive(false);
        Dispose(generated); Dispose(mesh); Dispose(material);
        generated = null; mesh = null; material = null;
    }
    private static void Dispose(Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying) Destroy(obj); else DestroyImmediate(obj);
    }
    private void OnDisable() { Release(); }
    private void OnDestroy() { Release(); }
}
