using System.Collections.Generic;
using UnityEngine;

/// <summary>Strict overhead fortress props; fire animates small preallocated meshes.</summary>
[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(BoxCollider2D))]
[AddComponentMenu("Decorations/Geometric Fortress Decoration")]
public sealed class GeometricFortressDecoration : MonoBehaviour
{
    // Append new shapes so existing prefab enum values remain unchanged.
    public enum FurnitureShape { WarTable, Ballista, SupplyPallet, Well, Brazier, RuneSeal, Mortar, Cannon }
    [SerializeField] private FurnitureShape shape;
    [SerializeField] private Vector2 size = new Vector2(2.2f, 1.5f);
    [Header("Fortress Palette / 要塞配色")]
    [SerializeField] private Color woodColor = new Color(0.67f, 0.39f, 0.2f, 1f);
    [SerializeField] private Color metalColor = new Color(0.67f, 0.71f, 0.74f, 1f);
    [SerializeField] private Color accentColor = new Color(.22f, .57f, .56f, 1f);
    [SerializeField] private bool solid = true;
    [SerializeField] private int sortingOrder = -3;
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private Material sourceMaterial;
    [Header("Effects / 装饰特效")]
    [SerializeField, Range(0, 4)] private float runeGlowIntensity = 1.6f;
    [SerializeField, Range(.01f, .2f)] private float runeGlowWidth = .075f;
    [SerializeField, Range(1, 30)] private float flameRate = 12f;
    [SerializeField, Min(.5f)] private float runeBreathPeriod = 3.2f;
    [SerializeField, Range(0, .8f)] private float runeBreathDepth = .35f;
    [SerializeField, Range(0, 8)] private float runeParticleRate = 2.5f;
    [Header("Brazier / 火堆")]
    [SerializeField, Range(.2f, 4f), InspectorName("火焰变化速度")] private float fireAnimationSpeed = 1.6f;
    [SerializeField, Range(0f, 4f), InspectorName("火光泛光强度")] private float fireGlowIntensity = 1.8f;
    [SerializeField, Range(.45f, 1.5f), InspectorName("火光扩散范围")] private float fireGlowRadius = .95f;

    public struct Part
    {
        public Vector2[] Points;
        public Color Color;
        public Part(Vector2[] points, Color color) { Points = points; Color = color; }
    }

    private GameObject visual;
    private Mesh mesh;
    private Material ownedMaterial;
    private Material effectMaterial;
    private Mesh effectMesh;
    private ParticleSystem flames;
    private ParticleSystem runeParticles;
    private readonly Mesh[] runeGlyphMeshes = new Mesh[3];
    private float runeBreathTime;
    private Mesh fireMesh, fireGlowMesh;
    private Material fireGlowMaterial;
    private Vector3[] fireVertices, fireGlowVertices;
    private Color32[] fireColors, fireGlowColors;
    private Vector2 fireScale;
    private float fireTime;
    private static readonly Vector2[] FireCenters = {
        new Vector2(-.15f,.10f), new Vector2(.17f,-.06f), new Vector2(-.04f,-.15f),
        new Vector2(.06f,.17f), new Vector2(.025f,.025f) };
    private static readonly Vector2[] FireSizes = {
        new Vector2(.32f,.37f), new Vector2(.29f,.41f), new Vector2(.30f,.29f),
        new Vector2(.23f,.28f), new Vector2(.24f,.26f) };
    private bool dirty = true;

    // Interaction recoil may offset the generated art without moving the solid collider.
    public Transform GeneratedRoot => visual != null ? visual.transform : null;

    private void OnEnable()
    {
        if (dirty || visual == null) Rebuild(); else visual.SetActive(true);
        if (Application.isPlaying && flames != null) flames.Play();
        if (Application.isPlaying && runeParticles != null) runeParticles.Play();
    }
    private void OnValidate()
    {
        size = new Vector2(Mathf.Max(0.2f, size.x), Mathf.Max(0.2f, size.y));
        dirty = true;
    }
    private void Update()
    {
        if (dirty || visual == null) Rebuild();
        if (Application.isPlaying && shape == FurnitureShape.RuneSeal && effectMaterial != null)
        {
            runeBreathTime += Time.deltaTime;
            if (SceneVisualWorkBudget.IsVisible(transform, size))
                effectMaterial.SetFloat("_Intensity", runeGlowIntensity * RuneBreathMultiplier(runeBreathTime, runeBreathPeriod, runeBreathDepth));
        }
        if (Application.isPlaying && fireMesh != null && Time.deltaTime > 0f)
        {
            fireTime += Time.deltaTime * fireAnimationSpeed;
            if (SceneVisualWorkBudget.IsVisible(transform, size)) UpdateFireSurface(fireTime);
        }
    }
    private void OnDisable()
    {
        if (flames != null) flames.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (runeParticles != null) runeParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (visual != null) visual.SetActive(false);
    }
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
            // All visible parts are convex and lie in the XY plane for a strict overhead view.
            for (int i = 1; i < part.Points.Length - 1; i++)
            {
                triangles.Add(first); triangles.Add(first + i + 1); triangles.Add(first + i);
            }
        }
        mesh = new Mesh { name = "Geometric " + shape, hideFlags = HideFlags.HideAndDontSave };
        mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0);
        mesh.uv = new Vector2[vertices.Count];
        mesh.RecalculateBounds();
        visual = new GameObject("Generated Fortress Geometry") { hideFlags = HideFlags.HideAndDontSave, layer = gameObject.layer };
        visual.transform.SetParent(transform, false);
        visual.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = visual.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.sortingLayerName = sortingLayerName;
        renderer.sortingOrder = sortingOrder;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        if (shape == FurnitureShape.RuneSeal) BuildRuneGlow(scale);
        if (shape == FurnitureShape.Brazier)
        {
            BuildFireSurface(scale, material); // Also show an intact, glowing editor preview.
            if (Application.isPlaying) BuildFlames(scale, material);
        }
    }

    private void BuildFireSurface(Vector2 scale, Material material)
    {
        fireScale = scale;
        // Different placed braziers should not breathe in lockstep.
        fireTime = Application.isPlaying ? Mathf.Repeat(transform.position.x * .73f + transform.position.y * 1.17f, 11f) : 0f;
        fireVertices = new Vector3[20]; fireColors = new Color32[20];
        var indices = new List<int>();
        for (int i = 0; i < 5; i++) AddFireQuad(indices, i * 4);
        fireMesh = new Mesh { name = "Animated Brazier Core", hideFlags = HideFlags.HideAndDontSave };
        fireMesh.MarkDynamic(); fireMesh.vertices = fireVertices; fireMesh.triangles = indices.ToArray();
        fireMesh.uv = new Vector2[20];
        AddFireRenderer("Living Fire Surface", fireMesh, material, sortingOrder + 1);

        Shader shader = Resources.Load<Shader>("WarmLightGlow");
        if (shader != null && shader.isSupported && fireGlowIntensity > 0f)
        {
            fireGlowMaterial = new Material(shader) { name = "Brazier Warm Firelight", hideFlags = HideFlags.HideAndDontSave };
            fireGlowMaterial.SetFloat("_RadialFalloff", 0f);
            // Five near-square flame patches plus a soft three-ring pool of firelight.
            fireGlowVertices = new Vector3[40 + 1 + 24 * 3];
            fireGlowColors = new Color32[fireGlowVertices.Length];
            indices.Clear();
            for (int i = 0; i < 5; i++)
            {
                int first = i * 8; AddFireQuad(indices, first);
                for (int j = 0; j < 4; j++)
                {
                    int next = (j + 1) % 4;
                    indices.Add(first+j); indices.Add(first+next); indices.Add(first+4+next);
                    indices.Add(first+j); indices.Add(first+4+next); indices.Add(first+4+j);
                }
            }
            for (int j = 0; j < 24; j++)
            {
                int next = (j + 1) % 24;
                indices.Add(40); indices.Add(41+next); indices.Add(41+j);
                for (int ring = 0; ring < 2; ring++)
                {
                    int first = 41 + ring * 24, outer = first + 24;
                    indices.Add(first+j); indices.Add(first+next); indices.Add(outer+next);
                    indices.Add(first+j); indices.Add(outer+next); indices.Add(outer+j);
                }
            }
            fireGlowMesh = new Mesh { name = "Brazier Flame Bloom and Firelight", hideFlags = HideFlags.HideAndDontSave };
            fireGlowMesh.MarkDynamic(); fireGlowMesh.vertices = fireGlowVertices;
            fireGlowMesh.triangles = indices.ToArray(); fireGlowMesh.uv = new Vector2[fireGlowVertices.Length];
            AddFireRenderer("Warm Fire Glow", fireGlowMesh, fireGlowMaterial, sortingOrder + 2);
        }
        UpdateFireSurface(fireTime);
    }

    private static void AddFireQuad(List<int> indices, int first)
    {
        indices.Add(first); indices.Add(first+2); indices.Add(first+1);
        indices.Add(first); indices.Add(first+3); indices.Add(first+2);
    }

    private void AddFireRenderer(string label, Mesh geometry, Material material, int order)
    {
        var go = new GameObject(label) { hideFlags = HideFlags.HideAndDontSave, layer = gameObject.layer };
        go.transform.SetParent(visual.transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = geometry;
        var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        renderer.sortingLayerName = sortingLayerName; renderer.sortingOrder = order;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
    }

    // Each near-square colored patch is a short-lived flame: grow, deform, shrink and vanish.
    // A new, slightly different flame replaces it only while its slot is invisible.
    // Staggered lifetimes keep the fire burning without retaining any static fire plate.
    public static void WriteFireSurface(float time, Vector2 scale, Color accent, Vector3[] vertices, Color32[] colors)
    {
        for (int i = 0; i < 5; i++)
        {
            float age = time / (1.8f + i * .15f) + Mathf.Repeat(.16f + i * .237f, .68f);
            float generation = Mathf.Floor(age), life = age - generation;
            float seed = Mathf.Sin(generation * 12.9898f + i * 7.73f + 1.2f);
            float phase = life * 7f + i * 1.73f + seed;
            float grow = Mathf.SmoothStep(0f, 1f, life / .28f);
            float die = Mathf.SmoothStep(0f, 1f, (life - .5f) / .44f);
            float mass = (.12f + .88f * grow) * (1f - .84f * die);
            float alpha = Mathf.SmoothStep(0f, 1f, life / .13f) * (1f - Mathf.SmoothStep(0f, 1f, (life - .56f) / .38f));
            Vector2 drift = FireCenters[i].normalized * (life * .045f);
            Vector2 center = FireCenters[i] * .70f + drift
                + new Vector2(Mathf.Sin(phase*.83f)*.026f + seed*.025f, Mathf.Cos(phase*1.13f)*.022f);
            // Keep width/height close together throughout the lifecycle. Small shear
            // and independent stretch retain motion without producing long flame tips.
            float side = (FireSizes[i].x + FireSizes[i].y) * .41f * mass;
            float w = side * (1f + .055f*Mathf.Sin(phase));
            float h = side * (1f + .055f*Mathf.Cos(phase*1.19f));
            // Fit the whole patch uniformly so the rim never clips a corner flat.
            float reach = .53f * Mathf.Sqrt(w*w + h*h);
            float fit = Mathf.Min(1f, (.40f-center.magnitude) / reach);
            w *= fit; h *= fit;
            float bend = .055f * Mathf.Sin(phase*1.43f);
            float heading = i == 0 ? .16f : i == 1 ? -.20f : i == 2 ? .12f : i == 3 ? -.10f : .04f;
            float angle = heading + .14f * Mathf.Sin(phase*.71f), sin = Mathf.Sin(angle), cos = Mathf.Cos(angle);
            Color color = i < 2 ? new Color(1f,.24f+.065f*Mathf.Sin(phase),.035f,1f)
                : i < 4 ? Color.Lerp(accent, new Color(1f,.69f,.12f,1f), .28f+.18f*Mathf.Sin(phase))
                : new Color(1f,.88f+.06f*Mathf.Sin(phase),.36f,1f);
            color = Color.Lerp(color, new Color(1f,.22f,.025f,1f), die*.45f);
            color.a = alpha;
            for (int j = 0; j < 4; j++)
            {
                Vector2 tip = new Vector2((j == 0 || j == 3 ? -.5f : .5f) * w, (j < 2 ? -.5f : .5f) * h);
                tip.x += tip.y * bend;
                Vector2 point = center + new Vector2(tip.x*cos-tip.y*sin, tip.x*sin+tip.y*cos);
                vertices[i*4+j] = new Vector3(point.x*scale.x, point.y*scale.y, -.012f-i*.0003f);
                colors[i*4+j] = color;
            }
        }
    }

    private void UpdateFireSurface(float time)
    {
        WriteFireSurface(time, fireScale, accentColor, fireVertices, fireColors);
        fireMesh.vertices = fireVertices; fireMesh.colors32 = fireColors; fireMesh.RecalculateBounds();
        if (fireGlowMesh == null) return;
        for (int i = 0; i < 5; i++)
        {
            Color color = fireColors[i*4]; color.a *= .18f;
            for (int j = 0; j < 4; j++)
            {
                int source = i*4+j, first = i*8;
                Vector3 point = fireVertices[source]; point.z -= .003f;
                fireGlowVertices[first+j] = point; fireGlowColors[first+j] = color;
                Vector2 p = new Vector2(point.x/fireScale.x, point.y/fireScale.y);
                Vector3 pv = fireVertices[i*4+(j+3)%4], nv = fireVertices[i*4+(j+1)%4];
                Vector2 before = (p - new Vector2(pv.x/fireScale.x,pv.y/fireScale.y)).normalized;
                Vector2 after = (new Vector2(nv.x/fireScale.x,nv.y/fireScale.y) - p).normalized;
                Vector2 a = new Vector2(before.y,-before.x), b = new Vector2(after.y,-after.x);
                Vector2 feather = (a+b)*(.075f/Mathf.Max(.2f,1f+Vector2.Dot(a,b)));
                // Cap the fringe so the small fading patches do not gain long glow spikes.
                if (feather.sqrMagnitude > .10f*.10f) feather = feather.normalized * .10f;
                Vector2 expanded = p + feather;
                fireGlowVertices[first+4+j] = new Vector3(expanded.x*fireScale.x, expanded.y*fireScale.y, point.z);
                fireGlowColors[first+4+j] = new Color(color.r,color.g,color.b,0);
            }
        }
        float flicker = 1f + .10f*Mathf.Sin(time*5.3f) + .055f*Mathf.Sin(time*9.7f+1.2f);
        fireGlowVertices[40] = new Vector3(0,0,-.016f);
        fireGlowColors[40] = new Color(1f,.38f,.055f,.22f);
        for (int ring = 0; ring < 3; ring++)
        {
            float radius = fireGlowRadius * (ring == 0 ? .38f : ring == 1 ? .70f : 1f) * (1f+(flicker-1f)*.4f);
            float alpha = ring == 0 ? .15f : ring == 1 ? .05f : 0f;
            for (int j = 0; j < 24; j++)
            {
                float angle = j * Mathf.PI * 2f / 24f;
                int index = 41+ring*24+j;
                fireGlowVertices[index] = new Vector3(Mathf.Cos(angle)*radius*fireScale.x, Mathf.Sin(angle)*radius*fireScale.y, -.016f);
                fireGlowColors[index] = new Color(1f,.38f,.055f,alpha);
            }
        }
        fireGlowMesh.vertices = fireGlowVertices; fireGlowMesh.colors32 = fireGlowColors; fireGlowMesh.RecalculateBounds();
        fireGlowMaterial.SetFloat("_Intensity", fireGlowIntensity*flicker);
    }

    private void BuildFlames(Vector2 scale, Material material)
    {
        var go = new GameObject("Brazier Flames") { hideFlags = HideFlags.HideAndDontSave, layer = gameObject.layer };
        go.transform.SetParent(visual.transform, false);
        go.transform.localPosition = new Vector3(0, 0, -.01f);
        go.transform.localScale = new Vector3(scale.x, scale.y, 1);
        flames = go.AddComponent<ParticleSystem>();
        flames.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = flames.main;
        main.playOnAwake = false; main.loop = true; main.duration = 2;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.useUnscaledTime = false; main.maxParticles = 32;
        main.startLifetime = new ParticleSystem.MinMaxCurve(.55f, .9f);
        main.startSpeed = 0;
        main.startSize = new ParticleSystem.MinMaxCurve(.10f, .18f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
        main.startColor = Color.white;
        var emission = flames.emission; emission.rateOverTime = flameRate;
        var area = flames.shape; area.enabled = true;
        area.shapeType = ParticleSystemShapeType.Circle; area.radius = .22f; area.radiusThickness = 1;
        var velocity = flames.velocityOverLifetime; velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(-.06f, .06f);
        velocity.y = new ParticleSystem.MinMaxCurve(-.06f, .09f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        var color = flames.colorOverLifetime; color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] {
            new GradientColorKey(new Color(1,.91f,.45f),0),
            new GradientColorKey(new Color(1,.49f,.09f),.45f),
            new GradientColorKey(new Color(.88f,.16f,.035f),1)
        }, new[] { new GradientAlphaKey(0,0), new GradientAlphaKey(.95f,.13f),
            new GradientAlphaKey(.8f,.65f), new GradientAlphaKey(0,1) });
        color.color = new ParticleSystem.MinMaxGradient(gradient);
        var sizing = flames.sizeOverLifetime; sizing.enabled = true;
        sizing.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(
            new Keyframe(0,.45f), new Keyframe(.25f,1), new Keyframe(1,.1f)));
        var spin = flames.rotationOverLifetime; spin.enabled = true;
        spin.z = new ParticleSystem.MinMaxCurve(-.6f,.6f);
        // Small square embers match the main flame patches.
        effectMesh = new Mesh { name = "Brazier Square Ember", hideFlags = HideFlags.HideAndDontSave };
        effectMesh.vertices = new[] {new Vector3(-.32f,-.32f,0),new Vector3(.32f,-.32f,0),new Vector3(.32f,.32f,0),new Vector3(-.32f,.32f,0)};
        effectMesh.triangles = new[] {0,2,1,0,3,2};
        effectMesh.colors32 = new Color32[] {Color.white,Color.white,Color.white,Color.white};
        effectMesh.uv = new[] {Vector2.zero,Vector2.right,Vector2.one,Vector2.up};
        effectMesh.RecalculateBounds();
        var renderer = flames.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh; renderer.mesh = effectMesh;
        renderer.alignment = ParticleSystemRenderSpace.Local;
        renderer.enableGPUInstancing = false;
        renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream> {
            ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV });
        renderer.sharedMaterial = material;
        renderer.sortingLayerName = sortingLayerName; renderer.sortingOrder = sortingOrder + 1;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
        flames.Play();
    }

    // Visible red surfaces only: do not glow the dark filled disc hidden beneath the ring.
    public static List<Part> BuildRuneGlowParts(Color accent)
    {
        var p = new List<Part>();
        for (int i = 0; i < 12; i++)
        {
            float a=i*Mathf.PI*2/12, b=(i+1)*Mathf.PI*2/12;
            var u=new Vector2(Mathf.Cos(a),Mathf.Sin(a)); var v=new Vector2(Mathf.Cos(b),Mathf.Sin(b));
            p.Add(new Part(new[]{u*.53f,u*.59f,v*.59f,v*.53f},accent));
        }
        // Exclude the pale central diamond from the emissive shape.
        var outer=Diamond(0,0,.48f,.48f,accent).Points;
        var inner=Diamond(0,0,.22f,.22f,accent).Points;
        for(int i=0;i<4;i++){int n=(i+1)%4;p.Add(new Part(new[]{inner[i],outer[i],outer[n],inner[n]},accent));}
        foreach(float s in new[]{-1f,1f})
        {
            p.Add(Rect(0,s*.67f,.24f,.045f,accent));
            p.Add(Rect(-.10f,s*.69f,.045f,.13f,accent));
            p.Add(Rect(s*.67f,0,.045f,.24f,accent));
            p.Add(Rect(s*.69f,.10f,.13f,.045f,accent));
        }
        return p;
    }

    private void BuildRuneGlow(Vector2 scale)
    {
        if (runeGlowIntensity <= 0) return;
        // Same world-space additive shader as the project's candles and street lamps.
        Shader shader = Resources.Load<Shader>("WarmLightGlow");
        if (shader == null || !shader.isSupported) return;
        effectMaterial = new Material(shader) { name = "Red Rune Glow", hideFlags = HideFlags.HideAndDontSave };
        effectMaterial.SetFloat("_Intensity", runeGlowIntensity);
        effectMaterial.SetFloat("_RadialFalloff", 0);
        effectMesh=CreateGlowMesh(BuildRuneGlowParts(accentColor),scale,runeGlowWidth,.34f,"Rune Soft Glow");
        var go=new GameObject("Rune Glow"){hideFlags=HideFlags.HideAndDontSave,layer=gameObject.layer};
        go.transform.SetParent(visual.transform,false);
        go.AddComponent<MeshFilter>().sharedMesh=effectMesh;
        var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=effectMaterial;
        renderer.sortingLayerName=sortingLayerName;renderer.sortingOrder=sortingOrder+1;
        renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
        runeBreathTime=0;
        if(Application.isPlaying && runeParticleRate>0)BuildRuneParticles(scale);
    }

    private static Mesh CreateGlowMesh(List<Part> parts, Vector2 scale, float width, float alpha, string label)
    {
        var vertices=new List<Vector3>();var colors=new List<Color>();var triangles=new List<int>();
        foreach(Part part in parts)
        {
            int first=vertices.Count, count=part.Points.Length;
            Color core=part.Color;core.a=alpha;
            foreach(Vector2 point in part.Points)
            {
                vertices.Add(new Vector3(point.x*scale.x,point.y*scale.y,-.008f));colors.Add(core);
            }
            for(int i=1;i<count-1;i++){triangles.Add(first);triangles.Add(first+i+1);triangles.Add(first+i);}
            // Transparent feather band outside each convex red segment; no full-screen bloom.
            for(int i=0;i<count;i++)
            {
                Vector2 expanded=ExpandGlowVertex(part.Points,i,width);
                vertices.Add(new Vector3(expanded.x*scale.x,expanded.y*scale.y,-.008f));
                colors.Add(new Color(core.r,core.g,core.b,0));
            }
            for(int i=0;i<count;i++)
            {
                int n=(i+1)%count;
                triangles.Add(first+i);triangles.Add(first+n);triangles.Add(first+count+n);
                triangles.Add(first+i);triangles.Add(first+count+n);triangles.Add(first+count+i);
            }
        }
        var result=new Mesh{name=label,hideFlags=HideFlags.HideAndDontSave};
        result.SetVertices(vertices);result.SetTriangles(triangles,0);
        // Unity 2021 mesh particles require packed UNorm8 colors. Float32 colors are
        // misread as bytes (red 1.0f = 00 00 80 3F becomes blue), including alpha.
        // Color32 preserves BOTH the red hue and the transparent glow feather.
        var packedColors=new Color32[colors.Count];
        for(int i=0;i<packedColors.Length;i++)packedColors[i]=colors[i];
        result.colors32=packedColors;
        result.uv=new Vector2[vertices.Count];result.RecalculateBounds();
        return result;
    }

    public static float RuneBreathMultiplier(float time,float period,float depth)
    {
        return 1 + Mathf.Clamp(depth,0,.8f) * Mathf.Sin(time * Mathf.PI * 2 / Mathf.Max(.5f,period));
    }

    public static List<Part> BuildRuneGlyphParts(int variant, Color color)
    {
        var p=new List<Part>();
        switch(variant)
        {
            case 0:
                p.Add(Rect(-.14f,0,.10f,.78f,color));
                p.Add(Rect(.06f,.32f,.48f,.10f,color));
                p.Add(Rect(.02f,-.24f,.36f,.10f,color));break;
            case 1:
                p.Add(Rect(-.23f,0,.10f,.72f,color));
                p.Add(Rect(.23f,0,.10f,.72f,color));
                Beam(p,-.23f,-.16f,.23f,.16f,.10f,color);break;
            default:
                Beam(p,-.28f,.32f,0,.06f,.10f,color);
                Beam(p,.28f,.32f,0,.06f,.10f,color);
                p.Add(Rect(0,-.16f,.10f,.46f,color));break;
        }
        return p;
    }

    private void BuildRuneParticles(Vector2 scale)
    {
        var go=new GameObject("Floating Rune Glyphs"){hideFlags=HideFlags.HideAndDontSave,layer=gameObject.layer};
        go.transform.SetParent(visual.transform,false);
        go.transform.localPosition=new Vector3(0,0,-.015f);
        go.transform.localScale=new Vector3(scale.x,scale.y,1);
        runeParticles=go.AddComponent<ParticleSystem>();
        runeParticles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=runeParticles.main;
        main.playOnAwake=false;main.loop=true;main.duration=3;
        main.simulationSpace=ParticleSystemSimulationSpace.Local;
        main.scalingMode=ParticleSystemScalingMode.Hierarchy;
        main.useUnscaledTime=false;main.maxParticles=16;
        main.startLifetime=new ParticleSystem.MinMaxCurve(1.4f,2.2f);
        main.startSpeed=0;main.startSize=new ParticleSystem.MinMaxCurve(.20f,.30f);
        main.startRotation=new ParticleSystem.MinMaxCurve(-.3f,.3f);main.startColor=Color.white;
        var emission=runeParticles.emission;emission.rateOverTime=runeParticleRate;
        var area=runeParticles.shape;area.enabled=true;area.shapeType=ParticleSystemShapeType.Circle;
        area.radius=1.02f;area.radiusThickness=.12f;
        var velocity=runeParticles.velocityOverLifetime;velocity.enabled=true;
        velocity.space=ParticleSystemSimulationSpace.Local;
        velocity.x=new ParticleSystem.MinMaxCurve(-.035f,.035f);
        velocity.y=new ParticleSystem.MinMaxCurve(.045f,.09f);
        velocity.z=new ParticleSystem.MinMaxCurve(0f,0f);
        var gradient=new Gradient();
        gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
            new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.85f,.25f),
                new GradientAlphaKey(.75f,.65f),new GradientAlphaKey(0,1)});
        var color=runeParticles.colorOverLifetime;color.enabled=true;color.color=new ParticleSystem.MinMaxGradient(gradient);
        var spin=runeParticles.rotationOverLifetime;spin.enabled=true;spin.z=new ParticleSystem.MinMaxCurve(-.10f,.10f);
        for(int i=0;i<runeGlyphMeshes.Length;i++)
            runeGlyphMeshes[i]=CreateGlowMesh(BuildRuneGlyphParts(i,accentColor),Vector2.one,.09f,.72f,"Floating Rune "+i);
        var renderer=runeParticles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode=ParticleSystemRenderMode.Mesh;renderer.SetMeshes(runeGlyphMeshes);
        renderer.meshDistribution=ParticleSystemMeshDistribution.UniformRandom;
        renderer.alignment=ParticleSystemRenderSpace.Local;renderer.sharedMaterial=effectMaterial;
        renderer.enableGPUInstancing=false;
        renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream>{
            ParticleSystemVertexStream.Position,ParticleSystemVertexStream.Color,ParticleSystemVertexStream.UV});
        renderer.sortingLayerName=sortingLayerName;renderer.sortingOrder=sortingOrder+1;
        renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
        runeParticles.Play();
    }

    public static Vector2 ExpandGlowVertex(Vector2[] points,int i,float width)
    {
        Vector2 before=(points[i]-points[(i+points.Length-1)%points.Length]).normalized;
        Vector2 after=(points[(i+1)%points.Length]-points[i]).normalized;
        Vector2 a=new Vector2(before.y,-before.x),b=new Vector2(after.y,-after.x);
        return points[i]+(a+b)*(width/Mathf.Max(.1f,1+Vector2.Dot(a,b)));
    }



    public static Vector2 ReferenceSize(FurnitureShape shape)
    {
        switch (shape)
        {
            case FurnitureShape.WarTable: return new Vector2(2.4f, 1.6f);
            case FurnitureShape.Ballista: return new Vector2(2.2f, 2.0f);
            case FurnitureShape.SupplyPallet: return new Vector2(2.2f, 1.6f);
            case FurnitureShape.Well: return new Vector2(1.7f, 1.7f);
            case FurnitureShape.Brazier: return new Vector2(1.3f, 1.3f);
            case FurnitureShape.Mortar: return new Vector2(2.4f, 2.6f);
            case FurnitureShape.Cannon: return new Vector2(2.4f, 3f);
            default: return new Vector2(1.8f, 1.8f);
        }
    }
    public static List<Part> BuildParts(FurnitureShape shape, Color wood, Color metal, Color accent)
    {
        var p = new List<Part>();
        Color edge = new Color(.38f,.20f,.11f,1);
        Color iron = new Color(.25f,.30f,.33f,1);
        Color dark = new Color(.10f,.15f,.20f,1);
        Color pale = Color.Lerp(metal,Color.white,.40f);
        Color paper = new Color(.84f,.83f,.65f,1);
        Color gold = new Color(.83f,.57f,.24f,1);
        switch(shape)
        {
            case FurnitureShape.WarTable:
                p.Add(Rect(0,0,2.4f,1.6f,edge));
                p.Add(Rect(0,0,2.24f,1.44f,wood));
                p.Add(Rect(0,0,1.99f,1.20f,paper));
                // Abstract ink strokes only: no buildings, river fills or unit counters.
                Beam(p,-.81f,.30f,-.40f,.38f,.04f,edge);
                Beam(p,-.40f,.38f,-.12f,.15f,.04f,edge);
                Beam(p,-.12f,.15f,.36f,.27f,.04f,edge);
                Beam(p,.36f,.27f,.77f,.12f,.04f,edge);
                Beam(p,-.75f,-.16f,-.24f,-.07f,.04f,accent);
                Beam(p,-.24f,-.07f,.16f,-.35f,.04f,accent);
                Beam(p,.16f,-.35f,.73f,-.25f,.04f,accent);
                Beam(p,-.41f,-.40f,-.33f,-.21f,.035f,edge);
                Beam(p,.44f,-.02f,.53f,-.13f,.035f,edge);
                foreach(float x in new[]{-1.06f,1.06f})
                    foreach(float y in new[]{-.65f,.65f})p.Add(Rect(x,y,.10f,.10f,metal));
                break;
            case FurnitureShape.Ballista:
                // Overhead siege crossbow: carriage rails, swept limbs, taut V string and bolt.
                p.Add(Rect(-.35f,-.20f,.18f,1.35f,edge));
                p.Add(Rect(.35f,-.20f,.18f,1.35f,edge));
                p.Add(Rect(0,-.57f,1.13f,.16f,iron));
                foreach(float x in new[]{-.54f,.54f})
                {p.Add(Rect(x,-.57f,.16f,.50f,iron));p.Add(Rect(x,-.57f,.055f,.43f,metal));}
                p.Add(Disc(0,-.16f,.42f,iron));
                p.Add(Disc(0,-.16f,.32f,wood));
                p.Add(Rect(0,.01f,.30f,1.72f,edge));
                p.Add(Rect(0,.03f,.16f,1.57f,wood));
                Beam(p,0,.43f,-.60f,.33f,.20f,edge);
                Beam(p,-.60f,.33f,-1.02f,.03f,.17f,edge);
                Beam(p,0,.43f,.60f,.33f,.20f,edge);
                Beam(p,.60f,.33f,1.02f,.03f,.17f,edge);
                Beam(p,0,.45f,-.60f,.35f,.095f,wood);
                Beam(p,0,.45f,.60f,.35f,.095f,wood);
                Beam(p,-1.02f,.03f,0,-.42f,.025f,pale);
                Beam(p,1.02f,.03f,0,-.42f,.025f,pale);
                p.Add(Rect(0,.23f,.055f,1.18f,metal));
                p.Add(Diamond(0,.85f,.18f,.27f,pale));
                p.Add(Rect(0,-.61f,.49f,.09f,metal));
                p.Add(Disc(0,-.61f,.08f,iron));
                break;
            case FurnitureShape.Cannon:
                // Strict overhead view: long forward barrel, side wheels and a red rear carriage.
                foreach (float x in new[] { -.55f, .55f })
                {
                    p.Add(Rect(x, -.68f, .27f, 1.64f, iron));
                    p.Add(Rect(x, -.65f, .16f, 1.53f, accent));
                }
                p.Add(Rect(0, -1.31f, 1.28f, .24f, iron));
                p.Add(Rect(0, -1.29f, 1.10f, .10f, accent));
                p.Add(Rect(0, -.24f, 2.18f, .25f, iron));
                foreach (float x in new[] { -1f, 1f })
                {
                    p.Add(Rect(x, -.24f, .4f, 1.1f, dark));
                    p.Add(Rect(x, -.24f, .26f, .98f, iron));
                    p.Add(Rect(x, -.24f, .08f, .98f, metal));
                    p.Add(Rect(x, -.24f, .35f, .16f, accent));
                }
                p.Add(Disc(0, -.66f, .49f, iron));
                p.Add(Disc(0, -.65f, .39f, metal));
                p.Add(Rect(0, .32f, .94f, 2.08f, iron));
                p.Add(Rect(0, .34f, .76f, 2.02f, metal));
                p.Add(Rect(-.19f, .35f, .13f, 1.96f, pale));
                p.Add(Rect(.28f, .35f, .10f, 1.96f, iron));
                foreach (float y in new[] { -.42f, .73f })
                {
                    p.Add(Rect(0, y, 1.03f, .21f, iron));
                    p.Add(Rect(0, y+.02f, .89f, .10f, accent));
                }
                p.Add(Rect(0, 1.36f, 1.10f, .28f, iron));
                p.Add(Rect(0, 1.41f, .94f, .14f, pale));
                p.Add(Rect(0, 1.47f, .71f, .06f, dark));
                p.Add(Disc(0, -1.10f, .13f, iron));
                p.Add(Disc(0, -1.10f, .075f, accent));
                break;
            case FurnitureShape.Mortar:
                // High-elevation, short siege mortar seen from above: a wide
                // open bore, rounded breech and lateral trunnions on a red sled.
                // No side-view wheels, perspective walls or modern control panel.
                Color redShade = Color.Lerp(accent, dark, .35f);
                Color steelShade = Color.Lerp(metal, iron, .55f);
                p.Add(Rect(0, -.10f, 2.12f, 2.20f, iron));
                p.Add(Rect(0, -.10f, 1.84f, 1.95f, steelShade));
                foreach (float x in new[] { -.94f, .94f })
                {
                    p.Add(Rect(x, 0, .36f, 2.46f, redShade));
                    p.Add(Rect(x, .025f, .23f, 2.29f, accent));
                    foreach (float y in new[] { -1.11f, 1.11f })
                    {
                        p.Add(Rect(x, y, .52f, .38f, iron));
                        p.Add(Rect(x, y, .36f, .18f, metal));
                    }
                }
                p.Add(Rect(0, -.99f, 1.55f, .22f, redShade));
                p.Add(Rect(0, -.96f, 1.48f, .09f, accent));
                // Visible rear barrel and short highlight establish its axis.
                p.Add(Disc(0, -.46f, .61f, iron));
                p.Add(Disc(0, -.42f, .51f, metal));
                p.Add(Rect(0, -.04f, 1.24f, 1.05f, iron));
                p.Add(Rect(0, -.02f, 1.09f, .99f, steelShade));
                p.Add(Rect(-.30f, -.13f, .16f, .76f, metal));
                p.Add(Rect(0, -.54f, 1.10f, .18f, redShade));
                p.Add(Rect(0, -.51f, 1.06f, .07f, accent));
                foreach (float x in new[] { -.77f, .77f })
                {
                    p.Add(Rect(x, -.18f, .38f, .41f, iron));
                    p.Add(Disc(x, -.18f, .19f, metal));
                    p.Add(Disc(x, -.18f, .105f, accent));
                }
                // Thick, layered muzzle lip and recessed bore, not a filled lid.
                p.Add(Disc(0, .39f, .80f, iron));
                p.Add(Disc(0, .42f, .75f, metal));
                p.Add(Disc(0, .44f, .64f, pale));
                p.Add(Disc(0, .44f, .56f, steelShade));
                p.Add(Disc(0, .48f, .46f, dark));
                break;
            case FurnitureShape.SupplyPallet:
                p.Add(Rect(0,0,2.2f,1.6f,edge));
                foreach(float y in new[]{-.60f,-.20f,.20f,.60f})p.Add(Rect(0,y,2.08f,.32f,wood));
                Crate(p,-.55f,.03f,.87f,1.19f,edge,wood,metal);
                Crate(p,.51f,.37f,.90f,.57f,edge,wood,metal);
                // Barrel lid and tied sack tops seen vertically from above.
                p.Add(Disc(.40f,-.38f,.27f,iron));
                p.Add(Disc(.40f,-.38f,.22f,wood));
                p.Add(Rect(.40f,-.38f,.37f,.07f,edge));
                p.Add(Disc(.85f,-.42f,.16f,paper));
                p.Add(Disc(.85f,-.42f,.05f,edge));
                break;
            case FurnitureShape.Well:
                p.Add(Rect(0,0,1.7f,1.7f,iron));
                p.Add(Rect(0,0,1.53f,1.53f,metal));
                p.Add(Disc(0,0,.65f,iron));
                p.Add(Disc(0,0,.53f,dark));
                p.Add(Disc(0,0,.43f,accent));
                p.Add(Rect(-.17f,-.23f,.24f,.045f,pale));
                p.Add(Rect(.16f,.08f,.15f,.04f,pale));
                // Winch bridge spans the opening; only its top and post end caps are visible.
                p.Add(Rect(0,.35f,1.45f,.14f,edge));
                p.Add(Rect(0,.37f,1.41f,.06f,wood));
                foreach(float x in new[]{-.67f,.67f})p.Add(Rect(x,.35f,.21f,.24f,wood));
                p.Add(Rect(.08f,.20f,.045f,.35f,paper));
                p.Add(Disc(.08f,-.03f,.16f,metal));
                p.Add(Disc(.08f,-.03f,.105f,edge));
                p.Add(Rect(.08f,-.03f,.035f,.25f,iron));
                foreach(float x in new[]{-.66f,.66f})
                    foreach(float y in new[]{-.67f,.67f})p.Add(Rect(x,y,.16f,.08f,pale));
                break;
            case FurnitureShape.Brazier:
                p.Add(Rect(0,0,1.3f,1.3f,iron));
                p.Add(Rect(0,0,1.12f,1.12f,metal));
                p.Add(Disc(0,0,.52f,iron));
                p.Add(Disc(0,0,.43f,dark));
                Beam(p,-.29f,-.26f,.29f,.26f,.12f,edge);
                Beam(p,-.29f,.26f,.29f,-.26f,.12f,wood);
                // Fire is entirely in the separate animated surface; no static flames.
                foreach(float x in new[]{-.52f,.52f})
                    foreach(float y in new[]{-.52f,.52f})p.Add(Rect(x,y,.09f,.09f,iron));
                break;
            case FurnitureShape.RuneSeal:
                // Flat floor inlay. Small quadrilateral marks deliberately stay coarse under CRT.
                p.Add(Disc(0,0,.90f,iron));
                p.Add(Disc(0,0,.83f,metal));
                p.Add(Disc(0,0,.73f,dark));
                p.Add(Disc(0,0,.59f,accent));
                p.Add(Disc(0,0,.53f,dark));
                p.Add(Diamond(0,0,.65f,.65f,metal));
                p.Add(Diamond(0,0,.48f,.48f,accent));
                p.Add(Diamond(0,0,.22f,.22f,pale));
                foreach(float s in new[]{-1f,1f})
                {
                    p.Add(Rect(0,s*.67f,.24f,.045f,accent));
                    p.Add(Rect(-.10f,s*.69f,.045f,.13f,accent));
                    p.Add(Rect(s*.67f,0,.045f,.24f,accent));
                    p.Add(Rect(s*.69f,.10f,.13f,.045f,accent));
                    foreach(float t in new[]{-1f,1f})
                        p.Add(Diamond(s*.47f,t*.47f,.10f,.10f,gold));
                }
                break;
        }
        return p;
    }
    private static void Crate(List<Part> p,float x,float y,float w,float h,Color edge,Color wood,Color metal)
    {
        p.Add(Rect(x,y,w,h,edge));p.Add(Rect(x,y,w-.12f,h-.12f,wood));
        p.Add(Rect(x-w*.25f,y,.08f,h-.04f,metal));p.Add(Rect(x+w*.25f,y,.08f,h-.04f,metal));
        p.Add(Rect(x,y-h*.38f,.14f,.10f,edge));
    }
    private static void Beam(List<Part> p,float ax,float ay,float bx,float by,float width,Color color)
    {
        float dx=bx-ax,dy=by-ay,len=Mathf.Sqrt(dx*dx+dy*dy);
        float nx=-dy/len*width*.5f,ny=dx/len*width*.5f;
        p.Add(new Part(new[]{new Vector2(ax-nx,ay-ny),new Vector2(bx-nx,by-ny),
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
        if(flames!=null)flames.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        if(runeParticles!=null)runeParticles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        runeParticles=null;
        for(int i=0;i<runeGlyphMeshes.Length;i++){ReleaseObject(runeGlyphMeshes[i]);runeGlyphMeshes[i]=null;}
        ReleaseObject(visual); ReleaseObject(mesh); ReleaseObject(ownedMaterial);
        ReleaseObject(effectMesh); ReleaseObject(effectMaterial);
        ReleaseObject(fireMesh); ReleaseObject(fireGlowMesh); ReleaseObject(fireGlowMaterial);
        fireMesh = null; fireGlowMesh = null; fireGlowMaterial = null;
        fireVertices = fireGlowVertices = null; fireColors = fireGlowColors = null;
        effectMesh=null;effectMaterial=null;flames=null;
        visual = null; mesh = null; ownedMaterial = null;
    }
    private static void ReleaseObject(Object target)
    {
        if (target == null) return;
        if (Application.isPlaying) Destroy(target); else DestroyImmediate(target);
    }
}
