using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Top-down bevelled stone, hovering green facets and a pooled double-helix particle mesh.</summary>
[ExecuteAlways, DisallowMultipleComponent]
public sealed class EnergyCrystalVisual : MonoBehaviour
{
    public const string LinkMeshName = "Energy Crystal Links";
    [SerializeField, Min(0)] private float hoverHeight = .38f;
    [SerializeField, Range(0, .25f)] private float hoverAmplitude = .08f;
    [Header("Link Particles / 螺旋链接粒子")]
    [SerializeField, Range(.5f, 3f)] private float linkParticleScale = 1.65f;
    [SerializeField, Range(.1f, 2f)] private float linkGlowIntensity = 1.25f;
    [SerializeField, Range(.05f, .5f)] private float linkHelixWidth = .22f;
    [Header("Broken Links / 断链四散")]
    [SerializeField, Range(.2f, 2f)] private float linkReleaseDuration = 1.05f;
    [SerializeField, Range(.1f, 3f)] private float linkReleaseDistance = 1.15f;
    private EnergyCrystal crystal;
    private CrystalMesh pedestal, gem, glow, links;
    private Material solidMaterial, glowMaterial, linkMaterial;
    private bool broken;
    private float nextLinkFrame;
    public Vector3 CrystalCenter => gem != null ? gem.Root.transform.position : transform.TransformPoint(new Vector3(0, hoverHeight));

    private void OnEnable() { Build(); SetBroken(GetComponent<EnergyCrystal>()?.IsBroken ?? false); }
    // Streaming hides the whole hierarchy. Keep its small meshes/materials for cheap reactivation.
    private void OnDisable() { if (!Application.isPlaying) Release(); }
    private void OnDestroy() { Release(); }

    private void Build()
    {
        if (pedestal != null) return;
        crystal = GetComponent<EnergyCrystal>();
        solidMaterial = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave };
        Shader shader = Resources.Load<Shader>("ElectricGateGlow");
        glowMaterial = new Material(shader != null ? shader : Shader.Find("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave };
        glowMaterial.SetFloat("_Radial", 1); glowMaterial.SetFloat("_Intensity", .85f); glowMaterial.SetFloat("_CoreFraction", .22f);
        // Separate instance: brighter/broader beads must not amplify the pedestal glow or other devices.
        linkMaterial = new Material(glowMaterial) { hideFlags = HideFlags.HideAndDontSave };
        linkMaterial.SetFloat("_CoreFraction", .42f);
        linkMaterial.SetFloat("_Intensity", linkGlowIntensity);
        pedestal = new CrystalMesh(transform, "Crystal Stone Pedestal", solidMaterial, -3);
        gem = new CrystalMesh(transform, "Floating Green Crystal", solidMaterial, 1);
        glow = new CrystalMesh(transform, "Crystal Soft Glow", glowMaterial, 0);
        links = new CrystalMesh(transform, LinkMeshName, linkMaterial, -1);
        // Clipped corners keep the same simple top-down language as the other furniture.
        pedestal.Polygon(new Color32(58, 74, 80, 255), new Vector2(-.85f,-.5f), new Vector2(-.65f,-.7f),
            new Vector2(.65f,-.7f), new Vector2(.85f,-.5f), new Vector2(.85f,.5f), new Vector2(.65f,.7f),
            new Vector2(-.65f,.7f), new Vector2(-.85f,.5f));
        pedestal.Polygon(new Color32(150, 171, 177, 255), new Vector2(-.78f,-.38f), new Vector2(-.57f,-.58f),
            new Vector2(.57f,-.58f), new Vector2(.78f,-.38f), new Vector2(.78f,.46f), new Vector2(.57f,.65f),
            new Vector2(-.57f,.65f), new Vector2(-.78f,.46f));
        pedestal.Polygon(new Color32(67, 86, 91, 255), new Vector2(-.58f,-.29f), new Vector2(-.4f,-.45f),
            new Vector2(.4f,-.45f), new Vector2(.58f,-.29f), new Vector2(.58f,.32f), new Vector2(.4f,.48f),
            new Vector2(-.4f,.48f), new Vector2(-.58f,.32f));
        pedestal.Quad(new Vector2(0,-.05f), new Vector2(.67f,.24f), new Color32(23, 42, 44, 255));
        for (int i = -1; i <= 1; i += 2)
        {
            pedestal.Quad(new Vector2(i*.65f,.03f), new Vector2(.08f,.24f), new Color32(191, 207, 201, 255));
            pedestal.Quad(new Vector2(i*.26f,-.5f), new Vector2(.16f,.045f), new Color32(111, 148, 129, 255));
        }
        pedestal.Upload();
        gem.Polygon(new Color32(16, 88, 58, 255), new Vector2(0,-.62f), new Vector2(-.37f,-.15f),
            new Vector2(-.34f,.3f), new Vector2(0,.66f), new Vector2(.34f,.3f), new Vector2(.37f,-.15f));
        gem.Polygon(new Color32(32, 218, 105, 255), new Vector2(0,-.52f), new Vector2(-.28f,-.12f),
            new Vector2(-.26f,.28f), new Vector2(0,.56f));
        gem.Polygon(new Color32(20, 164, 79, 255), new Vector2(0,-.52f), new Vector2(.28f,-.12f),
            new Vector2(.26f,.28f), new Vector2(0,.56f));
        gem.Polygon(new Color32(141, 255, 178, 255), new Vector2(0,.56f), new Vector2(-.26f,.28f),
            new Vector2(-.11f,.19f), new Vector2(.1f,.24f));
        gem.Polygon(new Color32(79, 246, 130, 255), new Vector2(-.11f,.19f), new Vector2(.1f,.24f),
            new Vector2(.08f,-.17f), new Vector2(0,-.52f), new Vector2(-.11f,-.1f));
        gem.Upload(); UpdateFrame(0);
    }

    public void SetBroken(bool value)
    {
        broken = value;
        if (gem != null) gem.Root.SetActive(!broken);
        if (glow != null) glow.Root.SetActive(!broken);
        if (links != null) links.Root.SetActive(!broken);
    }
    private void Update()
    {
        if (pedestal == null) Build();
        if (broken) return;
        float time = Application.isPlaying ? Time.time : Time.realtimeSinceStartup;
        UpdateFrame(time);
        if (time < nextLinkFrame) return;
        nextLinkFrame = time + 1f / 30f;
        WriteLinks(time);
    }
    private void UpdateFrame(float time)
    {
        gem.Root.transform.localPosition = new Vector3(0, hoverHeight + Mathf.Sin(time * 1.9f) * hoverAmplitude, 0);
        glow.Clear();
        glow.Quad(gem.Root.transform.localPosition, Vector2.one * 1.55f,
            new Color(.13f, 1, .38f, .18f + .035f * Mathf.Sin(time * 2.2f)));
        glow.Upload();
    }
    private void WriteLinks(float time)
    {
        links.Clear();
        linkMaterial.SetFloat("_Intensity", linkGlowIntensity);
        Vector3 scale = transform.lossyScale;
        Vector2 particleScale = new Vector2(linkParticleScale / Mathf.Max(.01f,Mathf.Abs(scale.x)),
            linkParticleScale / Mathf.Max(.01f,Mathf.Abs(scale.y)));
        if (crystal != null && !broken)
        {
            foreach (var skeleton in crystal.Skeletons)
            {
                if (skeleton == null || !skeleton.gameObject.activeInHierarchy || !crystal.Powers(skeleton)) continue;
                Vector2 start = CrystalCenter, end = skeleton.LinkCenter;
                Vector2 axis = end-start;
                float length = axis.magnitude;
                if (length < .15f) continue;
                Vector2 normal = new Vector2(-axis.y, axis.x) / length;
                int count = Mathf.Clamp(Mathf.CeilToInt(length / .12f), 8, 128);
                for (int i = 0; i <= count; i++)
                {
                    float t = (float)i/count;
                    float phase = t*length*6.2f - time*3.5f;
                    float wave = Mathf.Sin(phase) * linkHelixWidth * Mathf.Sin(t*Mathf.PI);
                    Vector2 center = Vector2.Lerp(start, end, t);
                    for (int strand = -1; strand <= 1; strand += 2)
                    {
                        Vector3 local = transform.InverseTransformPoint(center + normal * wave * strand);
                        float front = (Mathf.Cos(phase)*strand+1)*.5f;
                        // Individually separated luminous beads, not a solid laser beam.
                        links.Quad(local, particleScale * (.13f + front*.03f),
                            new Color(.12f + front*.26f, 1, .28f + front*.2f, .72f + front*.23f));
                    }
                    if (i % 4 == 0)
                        for (int rung = 1; rung <= 2; rung++)
                            links.Quad(transform.InverseTransformPoint(center + normal*wave*(rung*2f/3f-1)),
                                particleScale*.055f, new Color(.2f,1,.38f,.65f));
                }
            }
        }
        links.Upload();
    }
    public void PlayShatter(AudioClip clip, float volume)
    {
        // Copy the last displayed beads, not a new helix sampled after the power was lost.
        // Restore/streaming only calls SetBroken, so neither can replay this transient effect.
        if (links != null)
            EnergyCrystalLinkReleaseVisual.Spawn(links.Root.GetComponent<MeshFilter>(),
                linkReleaseDuration, linkReleaseDistance);
        EnergyCrystalShatterVisual.Spawn(gameObject.scene, CrystalCenter,
            Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y)), clip, volume);
    }
    private void Release()
    {
        pedestal?.Dispose(); gem?.Dispose(); glow?.Dispose(); links?.Dispose();
        pedestal = gem = glow = links = null;
        CrystalMesh.Release(solidMaterial); CrystalMesh.Release(glowMaterial); CrystalMesh.Release(linkMaterial);
        solidMaterial = glowMaterial = linkMaterial = null;
    }
}

// Reuses its buffers after warming up; at most one renderer for all links, independent of bindings.
internal sealed class CrystalMesh
{
    public readonly GameObject Root;
    private readonly Mesh mesh;
    private readonly List<Vector3> vertices = new List<Vector3>(256);
    private readonly List<Color> colors = new List<Color>(256);
    private readonly List<Vector2> uvs = new List<Vector2>(256);
    private readonly List<int> triangles = new List<int>(384);
    internal CrystalMesh(Transform parent, string name, Material material, int order)
    {
        Root = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave, layer = parent.gameObject.layer };
        Root.transform.SetParent(parent, false);
        mesh = new Mesh { name = name, hideFlags = HideFlags.HideAndDontSave, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.MarkDynamic();
        Root.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = Root.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material; renderer.sortingOrder = order;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
    }
    internal void Clear() { vertices.Clear(); colors.Clear(); uvs.Clear(); triangles.Clear(); }
    internal void Quad(Vector2 center, Vector2 size, Color color)
        => Quad(center, new Vector2(size.x * .5f, 0), new Vector2(0, size.y * .5f), color);
    internal void Quad(Vector2 center, Vector2 halfRight, Vector2 halfUp, Color color)
    {
        int n = vertices.Count;
        vertices.Add(center - halfRight - halfUp);
        vertices.Add(center - halfRight + halfUp);
        vertices.Add(center + halfRight + halfUp);
        vertices.Add(center + halfRight - halfUp);
        for(int i=0;i<4;i++) colors.Add(color);
        uvs.Add(new Vector2(0,0));uvs.Add(new Vector2(0,1));uvs.Add(new Vector2(1,1));uvs.Add(new Vector2(1,0));
        triangles.Add(n); triangles.Add(n+1); triangles.Add(n+2); triangles.Add(n); triangles.Add(n+2); triangles.Add(n+3);
    }
    internal void Polygon(Color color, params Vector2[] points)
    {
        int n = vertices.Count;
        foreach(var p in points) { vertices.Add(p); colors.Add(color); uvs.Add(Vector2.zero); }
        for(int i=1;i<points.Length-1;i++) { triangles.Add(n); triangles.Add(n+i); triangles.Add(n+i+1); }
    }
    internal void Triangle(Vector2 a, Vector2 b, Vector2 c, Color color)
    {
        int n=vertices.Count;
        vertices.Add(a);vertices.Add(b);vertices.Add(c);
        for(int i=0;i<3;i++) { colors.Add(color);uvs.Add(Vector2.zero);triangles.Add(n+i); }
    }
    internal void Upload()
    {
        mesh.Clear();mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetUVs(0,uvs);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
    }
    internal void Dispose() { if(Root!=null)Root.SetActive(false); Release(mesh); Release(Root); }
    internal static void Release(Object obj)
    {
        if(obj == null)return;
        if(Application.isPlaying)Object.Destroy(obj);else Object.DestroyImmediate(obj);
    }
}
