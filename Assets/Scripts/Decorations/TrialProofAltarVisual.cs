using System.Collections.Generic;
using UnityEngine;

/// <summary>Overhead cut-stone plinth, gold inlay and a socket for the existing proof ornament.</summary>
[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(TrialProofAltar))]
[AddComponentMenu("Decorations/Trial Proof Altar Visual")]
public sealed class TrialProofAltarVisual : MonoBehaviour
{
    [SerializeField] private Color stone = new Color(.59f, .65f, .68f, 1);
    [SerializeField] private Color gold = new Color(.87f, .64f, .23f, 1);
    [SerializeField] private Material sourceMaterial;
    [SerializeField] private int sortingOrder = -3;
    private GameObject generated;
    private Mesh mesh;
    private Material ownedMaterial;
    private bool dirty = true, offered;
    private PickupItemBase offeredProof;
    public bool DisplaysOffering => offered;

    public void SetOffered(bool value, PickupItemBase proof)
    {
        if (offered == value && offeredProof == proof) return;
        offered = value; offeredProof = proof; dirty = true;
    }
    private void OnEnable() { dirty = true; }
    private void OnValidate() { dirty = true; }
    private void Update() { if (dirty || generated == null) Rebuild(); }
    private void OnDisable() { if (generated != null) generated.SetActive(false); }
    private void OnDestroy() => Release();
    private static void Dispose(Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying) Destroy(obj); else DestroyImmediate(obj);
    }
    private void Release()
    {
        if (generated != null) generated.SetActive(false);
        Dispose(generated); Dispose(mesh); Dispose(ownedMaterial);
        generated = null; mesh = null; ownedMaterial = null;
    }
    private void Rebuild()
    {
        Release(); dirty = false;
        var material = sourceMaterial;
        if (material == null)
        {
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) return;
            ownedMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            material = ownedMaterial;
        }
        var vertices = new List<Vector3>(256);
        var colors = new List<Color>(256);
        var triangles = new List<int>(512);
        int depth = 0;
        void Polygon(Color color, params Vector2[] points)
        {
            int start = vertices.Count;
            foreach (var p in points) { vertices.Add(new Vector3(p.x, p.y, -.0002f * depth)); colors.Add(color); }
            for (int i = 1; i < points.Length - 1; i++)
            { triangles.Add(start); triangles.Add(start + i + 1); triangles.Add(start + i); }
            depth++;
        }
        void Bevel(float x, float y, float cut, Color color)
        {
            Polygon(color, new Vector2(-x+cut,-y), new Vector2(x-cut,-y), new Vector2(x,-y+cut),
                new Vector2(x,y-cut), new Vector2(x-cut,y), new Vector2(-x+cut,y),
                new Vector2(-x,y-cut), new Vector2(-x,-y+cut));
        }
        void Rect(float x, float y, float w, float h, Color color)
        {
            Polygon(color, new Vector2(x-w/2,y-h/2), new Vector2(x+w/2,y-h/2),
                new Vector2(x+w/2,y+h/2), new Vector2(x-w/2,y+h/2));
        }
        void Diamond(float x, float y, float rx, float ry, Color color)
        {
            Polygon(color, new Vector2(x,y+ry), new Vector2(x+rx,y), new Vector2(x,y-ry), new Vector2(x-rx,y));
        }
        var dark = new Color(.20f, .25f, .28f, 1);
        var face = Color.Lerp(dark, stone, .28f);
        var highlight = Color.Lerp(stone, Color.white, .28f);
        Bevel(1.02f, .90f, .20f, dark);
        Bevel(.97f, .85f, .18f, stone);
        Bevel(.84f, .72f, .16f, dark);
        Bevel(.78f, .66f, .14f, gold);
        Bevel(.73f, .61f, .13f, face);
        // Wide clean facets read as a stone slab, not a modern control panel.
        Rect(0, .795f, 1.52f, .055f, highlight);
        Rect(0, -.795f, 1.52f, .055f, Color.Lerp(stone, dark, .5f));
        var inset = offered ? Color.Lerp(gold, Color.white, .26f) : Color.Lerp(gold, dark, .30f);
        for (int s = -1; s <= 1; s += 2)
        {
            Rect(s * .51f, 0, .13f, .035f, inset);
            Rect(0, s * .44f, .035f, .12f, inset);
            Diamond(s * .57f, .45f, .065f, .065f, inset);
            Diamond(s * .57f, -.45f, .065f, .065f, inset);
        }
        Diamond(0, 0, .35f, .43f, dark);
        Diamond(0, 0, .29f, .37f, Color.Lerp(stone, dark, .28f));
        Diamond(0, 0, .23f, .31f, dark);
        mesh = new Mesh { name = "Trial Altar Cut Stone", hideFlags = HideFlags.HideAndDontSave };
        mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0);
        mesh.uv = new Vector2[vertices.Count]; mesh.RecalculateBounds();
        generated = new GameObject("Generated Trial Altar") { layer = gameObject.layer, hideFlags = HideFlags.HideAndDontSave };
        generated.transform.SetParent(transform, false);
        generated.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = generated.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material; renderer.sortingOrder = sortingOrder;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
        if (offered && offeredProof != null && offeredProof.ItemVisual != null)
        {
            var proof = new GameObject("Offered Trial Proof") { layer = gameObject.layer, hideFlags = HideFlags.HideAndDontSave };
            proof.transform.SetParent(generated.transform, false);
            proof.transform.localPosition = new Vector3(0, 0, -.02f);
            proof.transform.localScale = Vector3.one * .86f;
            var sprite = proof.AddComponent<SpriteRenderer>();
            sprite.sprite = offeredProof.ItemVisual.InventoryIcon;
            sprite.color = Color.white; sprite.sharedMaterial = material; sprite.sortingOrder = sortingOrder + 1;
        }
    }
}
