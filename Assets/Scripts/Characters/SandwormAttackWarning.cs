using System.Collections.Generic;
using UnityEngine;

/// <summary>One batched geometric danger glyph; only color/alpha blink, never the damage footprint.</summary>
public sealed class SandwormAttackWarning : MonoBehaviour
{
    private static readonly int TintId = Shader.PropertyToID("_Color");
    private MeshRenderer visual;
    private Mesh mesh;
    private MaterialPropertyBlock properties;
    private float age, flashAge, flashFrequency = 5f;
    public bool IsImminent { get; private set; }
    public float DisplayOpacity { get; private set; }

    public void Configure(float radius, Material material)
    {
        var vertices = new List<Vector3>(256);
        var colors = new List<Color32>(256);
        var triangles = new List<int>(256);
        Color32 red = new Color32(255,45,44,255);
        Color32 core = new Color32(255,193,170,255);
        // The entire range silhouette is the worm's open maw, not a small worm
        // replacing the exclamation mark: two thick jaws and inward-pointing fangs.
        // Keep the jaw strokes inside the real circular damage-radius envelope.
        var jaw=new[] { new Vector2(0f,-.955f),new Vector2(.57f,-.755f),
            new Vector2(.855f,-.43f),new Vector2(.955f,0f),new Vector2(.835f,.465f),
            new Vector2(.565f,.765f),new Vector2(.17f,.945f) };
        var backing=new Color32(15,26,48,100);
        for (int sign=-1;sign<=1;sign+=2)
        {
            for (int i=1;i<jaw.Length;i++)
                Triangle(Vector2.zero,Vector2.Scale(jaw[i-1],new Vector2(sign,1))*radius,
                    Vector2.Scale(jaw[i],new Vector2(sign,1))*radius,backing,vertices,colors,triangles);
        }
        Triangle(Vector2.zero,new Vector2(-.17f,.945f)*radius,new Vector2(.17f,.945f)*radius,
            backing,vertices,colors,triangles);
        for (int sign=-1;sign<=1;sign+=2)
        {
            for (int i=1;i<jaw.Length;i++)
                Stroke(Vector2.Scale(jaw[i-1],new Vector2(sign,1))*radius,
                    Vector2.Scale(jaw[i],new Vector2(sign,1))*radius,radius*.085f,red,vertices,colors,triangles);
            // The large upper mandibles are the dominant identifying feature.
            Triangle(new Vector2(sign*.17f,.945f)*radius,new Vector2(sign*.565f,.765f)*radius,
                new Vector2(sign*.20f,.43f)*radius,core,vertices,colors,triangles);
            Triangle(new Vector2(sign*.94f,.08f)*radius,new Vector2(sign*.91f,-.17f)*radius,
                new Vector2(sign*.66f,-.015f)*radius,red,vertices,colors,triangles);
            Triangle(new Vector2(sign*.76f,-.54f)*radius,new Vector2(sign*.57f,-.755f)*radius,
                new Vector2(sign*.43f,-.44f)*radius,red,vertices,colors,triangles);
        }
        // Preserve the central ! as the immediately readable danger symbol.
        Stroke(Vector2.up*radius*.30f,Vector2.down*radius*.08f,radius*.14f,core,vertices,colors,triangles);
        Stroke(new Vector2(-.07f,-.29f)*radius,new Vector2(.07f,-.29f)*radius,
            radius*.14f,core,vertices,colors,triangles);
        mesh=new Mesh { name="Sandworm Geometric Warning Glyph", hideFlags=HideFlags.HideAndDontSave };
        mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(triangles,0); mesh.RecalculateBounds();
        gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
        visual=gameObject.AddComponent<MeshRenderer>(); visual.sharedMaterial=material;
        // Ground decal: above floors (-10), below character/ghost visuals (1+).
        // Keep dust and eruption particles on their independent foreground orders.
        visual.sortingOrder=-1;
        visual.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off; visual.receiveShadows=false;
        properties=new MaterialPropertyBlock();
        Advance(0f,false,1f);
    }

    private static void Triangle(Vector2 a,Vector2 b,Vector2 c,Color32 color,
        List<Vector3> vertices,List<Color32> colors,List<int> triangles)
    {
        int start=vertices.Count;
        vertices.Add(a); vertices.Add(b); vertices.Add(c);
        colors.Add(color); colors.Add(color); colors.Add(color);
        triangles.Add(start); triangles.Add(start+1); triangles.Add(start+2);
    }
    private static void Stroke(Vector2 a,Vector2 b,float width,Color32 color,
        List<Vector3> vertices,List<Color32> colors,List<int> triangles)
    {
        Vector2 offset=new Vector2(-(b-a).y,(b-a).x).normalized*width*.5f;
        Triangle(a-offset,a+offset,b+offset,color,vertices,colors,triangles);
        Triangle(a-offset,b+offset,b-offset,color,vertices,colors,triangles);
    }

    public void SetImminent(bool imminent,float frequency)
    {
        if (imminent && !IsImminent) flashAge=0f;
        IsImminent=imminent; flashFrequency=Mathf.Clamp(frequency,2f,8f);
    }

    public void Advance(float deltaTime,bool erupting,float fade)
    {
        age+=deltaTime;
        if (IsImminent) flashAge+=deltaTime;
        bool urgent=IsImminent && !erupting;
        DisplayOpacity=fade*(erupting ? .28f : urgent ?
            (Mathf.Repeat(flashAge*flashFrequency,1f)<.5f ? 1f : .13f) : .78f+.16f*Mathf.Sin(age*4f));
        properties.SetColor(TintId,new Color(1f,urgent?1.35f:1f,urgent?1.2f:1f,DisplayOpacity));
        visual.SetPropertyBlock(properties); visual.enabled=DisplayOpacity>.001f;
    }

    private void OnDestroy()
    {
        if (mesh == null) return;
        if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
    }
}
