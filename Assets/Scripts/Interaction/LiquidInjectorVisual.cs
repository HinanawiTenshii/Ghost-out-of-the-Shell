using System.Collections.Generic;
using UnityEngine;

/// <summary>Overhead iron reservoir and manual pressure pump, built as a single colored mesh.</summary>
[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(LiquidInjector))]
public sealed class LiquidInjectorVisual : MonoBehaviour
{
    [SerializeField] private int sortingOrder = 5;
    private LiquidInjector injector;
    private GameObject generated;
    private Mesh mesh;
    private Material material;
    private Color[] colors;
    private int liquidStart, liquidEnd, highlightStart, highlightEnd;
    private Color lastColor;
    private bool dirty = true;

    private void OnEnable() { injector = GetComponent<LiquidInjector>(); dirty = true; }
    private void OnValidate() { dirty = true; }
    private void LateUpdate()
    {
        if (dirty || generated == null) Rebuild();
        if (mesh == null || injector == null) return;
        Color color = injector.CurrentEffectColor;
        if (color == lastColor) return;
        lastColor = color;
        for (int i = liquidStart; i < liquidEnd; i++) colors[i] = color;
        for (int i = highlightStart; i < highlightEnd; i++) colors[i] = Color.Lerp(color, Color.white, .16f);
        mesh.colors = colors;
    }

    private void Rebuild()
    {
        Release(); dirty = false;
        var shader = Shader.Find("Sprites/Default");
        if (shader == null) return;
        material = new Material(shader) { name = "Liquid Injector Iron", hideFlags = HideFlags.HideAndDontSave };
        generated = new GameObject("Generated Liquid Injector", typeof(MeshFilter), typeof(MeshRenderer))
            { hideFlags = HideFlags.HideAndDontSave, layer = gameObject.layer };
        generated.transform.SetParent(transform, false);
        var v = new List<Vector3>(); var t = new List<int>(); var c = new List<Color>();
        Color edge = new Color(.23f,.27f,.29f), steel = new Color(.60f,.66f,.68f);
        Color light = new Color(.83f,.88f,.87f), inset = new Color(.12f,.16f,.18f);
        Color red = new Color(.86f,.13f,.095f), redShade = new Color(.56f,.075f,.055f);
        // Two parallel feet and transverse braces reveal the floor between them.
        for (int side = -1; side <= 1; side += 2)
        {
            Rect(v,t,c,side*.40f,0,.16f,1.34f,edge);
            Rect(v,t,c,side*.40f,.02f,.08f,1.22f,steel);
        }
        Rect(v,t,c,0,.54f,.82f,.17f,edge); Rect(v,t,c,0,.56f,.76f,.08f,light);
        Rect(v,t,c,0,-.56f,.82f,.17f,edge); Rect(v,t,c,0,-.54f,.76f,.08f,steel);
        // Short horizontal distribution outlets, seen from above, not vertical wall piping.
        Rect(v,t,c,0,.10f,1.18f,.15f,edge); Rect(v,t,c,0,.12f,1.18f,.06f,steel);
        for (int side = -1; side <= 1; side += 2)
        {
            Rect(v,t,c,side*.54f,.10f,.09f,.23f,redShade);
            Rect(v,t,c,side*.55f,.12f,.055f,.19f,red);
        }
        // Circular rim and inset liquid aperture establish the top-down tank view.
        Disc(v,t,c,0,.17f,.45f,edge); Disc(v,t,c,0,.19f,.41f,light);
        Disc(v,t,c,0,.19f,.35f,steel); Disc(v,t,c,0,.19f,.30f,inset);
        liquidStart=v.Count; Disc(v,t,c,0,.19f,.265f,Color.white); liquidEnd=v.Count;
        highlightStart=v.Count;
        Rect(v,t,c,-.10f,.25f,.045f,.17f,Color.white);
        Rect(v,t,c,-.05f,.34f,.13f,.04f,Color.white);
        highlightEnd=v.Count;
        // Red clamps and a chunky manual T-plunger; no electronic screen/keyboard.
        for (int side = -1; side <= 1; side += 2)
        {
            Rect(v,t,c,side*.34f,.18f,.13f,.22f,redShade);
            Rect(v,t,c,side*.34f,.20f,.09f,.16f,red);
        }
        Rect(v,t,c,0,-.27f,.25f,.29f,edge); Rect(v,t,c,0,-.25f,.15f,.24f,steel);
        Rect(v,t,c,0,-.40f,.10f,.24f,light);
        Rect(v,t,c,0,-.50f,.50f,.15f,redShade); Rect(v,t,c,0,-.48f,.44f,.085f,red);
        Rect(v,t,c,0,-.66f,.18f,.10f,edge); Rect(v,t,c,0,-.65f,.10f,.07f,steel);
        mesh = new Mesh { name = "Top Down Liquid Injector", hideFlags = HideFlags.HideAndDontSave };
        mesh.SetVertices(v); mesh.SetTriangles(t,0); colors=c.ToArray(); mesh.colors=colors; mesh.RecalculateBounds();
        generated.GetComponent<MeshFilter>().sharedMesh=mesh;
        var renderer=generated.GetComponent<MeshRenderer>(); renderer.sharedMaterial=material; renderer.sortingOrder=sortingOrder;
        renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows=false;
        lastColor=Color.clear;
    }

    private static void Rect(List<Vector3> v,List<int> t,List<Color> c,float x,float y,float w,float h,Color color)
    {
        int n=v.Count; float z=-.0001f*n;
        v.Add(new Vector3(x-w*.5f,y-h*.5f,z)); v.Add(new Vector3(x-w*.5f,y+h*.5f,z));
        v.Add(new Vector3(x+w*.5f,y+h*.5f,z)); v.Add(new Vector3(x+w*.5f,y-h*.5f,z));
        for(int i=0;i<4;i++)c.Add(color);
        t.Add(n);t.Add(n+1);t.Add(n+2);t.Add(n);t.Add(n+2);t.Add(n+3);
    }
    private static void Disc(List<Vector3> v,List<int> t,List<Color> c,float x,float y,float r,Color color)
    {
        const int sides=12; int n=v.Count; float z=-.0001f*n;
        v.Add(new Vector3(x,y,z));c.Add(color);
        for(int i=0;i<sides;i++)
        {
            float a=i*Mathf.PI*2f/sides;
            v.Add(new Vector3(x+r*Mathf.Cos(a),y+r*Mathf.Sin(a),z));c.Add(color);
            t.Add(n);t.Add(n+1+(i+1)%sides);t.Add(n+1+i);
        }
    }
    private void Release()
    {
        if(generated!=null)generated.SetActive(false);
        Dispose(generated);Dispose(mesh);Dispose(material);generated=null;mesh=null;material=null;
    }
    private static void Dispose(Object obj)
    {
        if(obj==null)return;
        if(Application.isPlaying)Destroy(obj);else DestroyImmediate(obj);
    }
    private void OnDisable(){Release();}
    private void OnDestroy(){Release();}
}
