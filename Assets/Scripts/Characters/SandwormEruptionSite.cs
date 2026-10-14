using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Geometric range warning, a ground opening and pooled billowing dust/ballistic clods.</summary>
public sealed class SandwormEruptionSite : MonoBehaviour
{
    private struct Fragment
    {
        public SpriteRenderer renderer;
        public Vector2 start, velocity;
        public float age, lifetime, size, spin, aspect, phase;
        public Color color;
        public bool dust, lowCloud;
    }
    private const int DustCapacity = 42;
    private const int DebrisCapacity = 18;
    private const float GroundFadeDuration = .8f;
    private const float RippleDuration = .55f;
    private static Sprite disk, square, cloud;
    private static Material lineMaterial;
    private static int users;
    // Separate pools prevent the small fast clods from replacing all the lingering dust.
    // 60 sprites + the opening + 2 lines + 1 batched glyph = at most 64 renderers per site.
    private readonly Fragment[] fragments = new Fragment[DustCapacity + DebrisCapacity];
    private LineRenderer rim, ripple;
    private SandwormAttackWarning warning;
    private SpriteRenderer opening;
    private float radius, elapsed, finishTime, rippleAge = 10f, rippleStrength = 1f;
    private bool begun, finishing, registered;
    private bool showOpening = true;
    public float Radius => radius;
    public bool HasWarning => warning != null;
    public bool IsWarningImminent => warning != null && warning.IsImminent;
    public int ActiveFragmentCount { get; private set; }
    public bool IsFinished => finishing && finishTime >= GroundFadeDuration && ActiveFragmentCount == 0;

    public static SandwormEruptionSite Create(Scene scene, Vector2 point, float radius,
        bool showWarning = true, bool showGroundOpening = true)
    {
        var root = new GameObject("Sandworm Eruption Site");
        SceneManager.MoveGameObjectToScene(root, scene);
        root.transform.position = point;
        root.AddComponent<CameraVisionStreamingRegion>().Configure(radius + 1f);
        var site = root.AddComponent<SandwormEruptionSite>();
        site.Configure(radius,showWarning);
        site.showOpening = showGroundOpening;
        return site;
    }

    private void Configure(float size,bool showWarning)
    {
        radius = size; users++; registered = true; EnsureAssets();
        if (showWarning)
        {
            var glyph=new GameObject("Geometric Emergence Warning"); glyph.transform.SetParent(transform,false);
            warning=glyph.AddComponent<SandwormAttackWarning>(); warning.Configure(radius,lineMaterial);
        }
        opening = new GameObject("Burrow Opening", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
        opening.transform.SetParent(transform, false);
        opening.sprite = disk; opening.color = new Color(.065f,.055f,.05f,.95f); opening.sortingOrder = -8;
        opening.transform.localScale = new Vector3(radius*1.5f, radius*.52f, 1);
        opening.enabled = false;
        rim = Ring("Broken Earth Rim", 24, radius*.75f, radius*.26f, radius*.075f, new Color(.57f,.37f,.18f,1), -7);
        rim.enabled = false;
        ripple = Ring("Ground Pressure Ring", 48, radius, radius, radius*.045f, new Color(.73f,.61f,.39f,.55f), -6);
        ripple.enabled = false;
        for (int i = 0; i < fragments.Length; i++)
        {
            bool dust = i < DustCapacity;
            var renderer = new GameObject(dust ? "Pooled Billowing Dust" : "Pooled Earth Clod", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            renderer.transform.SetParent(transform, false); renderer.sprite = dust ? cloud : square;
            renderer.sortingOrder = dust ? 8 : 10; renderer.enabled = false;
            fragments[i].renderer = renderer; fragments[i].dust = dust;
        }
    }

    private LineRenderer Ring(string label, int count, float rx, float ry, float width, Color color, int order)
    {
        var line = new GameObject(label, typeof(LineRenderer)).GetComponent<LineRenderer>();
        line.transform.SetParent(transform, false); line.useWorldSpace = false;
        line.sharedMaterial = lineMaterial; line.loop = true; line.positionCount = count;
        line.widthMultiplier = width; line.startColor = line.endColor = color;
        line.sortingOrder = order; line.numCornerVertices = 1; line.numCapVertices = 1;
        for (int i = 0; i < count; i++)
        {
            float angle = 2f * Mathf.PI * i / count;
            float jagged = label == "Broken Earth Rim" ? (i%2 == 0 ? 1f : .91f) : 1f;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle)*rx*jagged, Mathf.Sin(angle)*ry*jagged, 0));
        }
        return line;
    }

    public void Pulse(bool firstSection)
    {
        if (finishing) return;
        if (!begun) { begun = true; opening.enabled = rim.enabled = showOpening; }
        if (warning != null) { warning.SetImminent(false,5f); warning.Advance(0f,true,1f); }
        rippleAge = 0f; ripple.enabled = true;
        rippleStrength = firstSection ? 1f : .45f;
        ripple.widthMultiplier = radius * (firstSection ? .09f : .04f);
        EmitFragments(firstSection ? 24 : 8, true, firstSection);
        EmitFragments(firstSection ? 14 : 3, false, firstSection);
    }

    private void EmitFragments(int count, bool dust, bool firstSection)
    {
        int first = dust ? 0 : DustCapacity;
        int last = dust ? DustCapacity : fragments.Length;
        float strength = firstSection ? 1f : .72f;
        for (int n = 0; n < count; n++)
        {
            int slot = -1; float oldest = -1;
            for (int i = first; i < last; i++)
            {
                if (!fragments[i].renderer.enabled) { slot = i; break; }
                float fraction = fragments[i].age / fragments[i].lifetime;
                if (fraction > oldest) { oldest = fraction; slot = i; }
            }
            Fragment f = fragments[slot];
            if (!f.renderer.enabled) ActiveFragmentCount++;
            float angle = 2f*Mathf.PI*(n/(float)count + Random.Range(-.04f,.04f));
            f.start = new Vector2(Mathf.Cos(angle)*radius*.68f, Mathf.Sin(angle)*radius*.23f);
            f.age = 0f; f.phase = Random.Range(0f, Mathf.PI*2f);
            if (dust)
            {
                // Broad low rolls spread across the ground; taller puffs peel upwards.
                // Dust slows in air rather than following the clods' gravity/parabola.
                f.lowCloud = n%3 == 0;
                f.velocity = new Vector2(Mathf.Cos(angle)*Random.Range(1.2f,2.1f),
                    f.lowCloud ? Random.Range(.08f,.28f) : Random.Range(1.1f,2.2f)) * radius * strength;
                f.lifetime = Random.Range(1.65f,2.35f);
                f.size = radius * Random.Range(.65f,1.05f) * strength;
                f.aspect = f.lowCloud ? Random.Range(1.5f,1.9f) : Random.Range(.95f,1.3f);
                f.spin = f.lowCloud ? Random.Range(-6f,6f) : Random.Range(-22f,22f);
                f.color = Color.Lerp(new Color(.60f,.57f,.51f,.48f), new Color(.81f,.74f,.61f,.58f), Random.value);
                f.renderer.sortingOrder = f.lowCloud ? 6 : 8;
            }
            else
            {
                f.velocity = new Vector2(Mathf.Cos(angle)*Random.Range(1.6f,2.8f), Random.Range(1.7f,3.3f)) * radius * strength;
                f.lifetime = Random.Range(.6f,.95f);
                f.size = radius * Random.Range(.07f,.16f) * strength;
                f.aspect = Random.Range(.65f,1.35f);
                f.spin = Random.Range(-240f,240f);
                f.color = n%2 == 0 ? new Color(.49f,.38f,.26f,1) : new Color(.76f,.66f,.48f,1);
            }
            f.renderer.enabled = true;
            Color initialColor = f.color; if (dust) initialColor.a = 0f;
            f.renderer.color = initialColor;
            f.renderer.transform.localPosition = f.start;
            f.renderer.transform.localRotation = Quaternion.Euler(0,0,f.dust && f.lowCloud ? 0f : f.phase*Mathf.Rad2Deg);
            f.renderer.transform.localScale = new Vector3(f.size*f.aspect,f.size,1f);
            fragments[slot] = f;
        }
    }

    public void Finish() { if (finishing) return; finishing = true; finishTime = 0f; }

    public void SetWarningImminent(bool imminent,float frequency)
    { if (warning != null) warning.SetImminent(imminent && !begun && !finishing,frequency); }

    public void AdvanceVisuals(float deltaTime)
    {
        if (deltaTime <= 0f) return;
        elapsed += deltaTime; rippleAge += deltaTime;
        if (finishing) finishTime += deltaTime;
        float fade = finishing ? 1f - Mathf.Clamp01(finishTime/GroundFadeDuration) : 1f;
        // Blink the glyph without resizing the indicated damage footprint.
        if (warning != null) warning.Advance(deltaTime,begun,fade);
        Color ground = opening.color; ground.a = .95f * fade; opening.color = ground;
        rim.startColor = rim.endColor = new Color(.57f,.37f,.18f,fade);
        if (rippleAge < RippleDuration)
        {
            float t = rippleAge/RippleDuration;
            ripple.transform.localScale = Vector3.one * Mathf.Lerp(.48f,1f,t);
            ripple.startColor = ripple.endColor = new Color(.73f,.65f,.51f, (1f-t)*.45f*fade*rippleStrength);
        }
        else ripple.enabled = false;
        ActiveFragmentCount = 0;
        for (int i = 0; i < fragments.Length; i++)
        {
            Fragment f = fragments[i];
            if (!f.renderer.enabled) continue;
            f.age += deltaTime;
            if (f.age >= f.lifetime) f.renderer.enabled = false;
            else
            {
                float t = f.age/f.lifetime;
                float size;
                Color color = f.color;
                if (f.dust)
                {
                    float driftTime = (1f-Mathf.Exp(-1.6f*f.age))/1.6f;
                    float curl = Mathf.Sin(f.age*2.5f+f.phase)*Mathf.Sin(Mathf.PI*t)*radius*.1f;
                    f.renderer.transform.localPosition = f.start + f.velocity*driftTime
                        + new Vector2(curl, radius*(f.lowCloud ? .025f : .12f)*f.age);
                    size = f.size*(1f+1.6f*Mathf.SmoothStep(0f,1f,t));
                    // Quick soft onset and a long dissipating tail; Finish never cuts dust off.
                    color.a *= Mathf.SmoothStep(0f,1f,f.age/.1f) * (1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.25f,1f,t)));
                }
                else
                {
                    f.renderer.transform.localPosition = f.start + f.velocity*f.age - Vector2.up*(radius*3.2f*f.age*f.age);
                    size = f.size*(1f-.4f*t);
                    color.a *= 1f-t*t;
                }
                float rotation = f.dust && f.lowCloud ? 0f : f.phase*Mathf.Rad2Deg;
                f.renderer.transform.localRotation = Quaternion.Euler(0,0,rotation+f.spin*f.age);
                f.renderer.transform.localScale = new Vector3(size*f.aspect,size,1f);
                f.renderer.color = color;
                ActiveFragmentCount++;
            }
            fragments[i] = f;
        }
    }

    public void Release()
    {
        gameObject.SetActive(false); // Release streaming retention immediately, including scene transitions.
        if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
    }

    private static void EnsureAssets()
    {
        if (disk != null) return;
        var texture = new Texture2D(32,32,TextureFormat.RGBA32,false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        var pixels = new Color[1024];
        for (int y=0;y<32;y++) for (int x=0;x<32;x++)
        {
            float distance = Vector2.Distance(new Vector2(x+.5f,y+.5f), Vector2.one*16f);
            pixels[y*32+x] = new Color(1,1,1,Mathf.Clamp01(16f-distance));
        }
        texture.SetPixels(pixels); texture.Apply(false,true);
        disk = Sprite.Create(texture,new Rect(0,0,32,32),Vector2.one*.5f,32,0,SpriteMeshType.FullRect);
        disk.hideFlags = HideFlags.HideAndDontSave;
        var block = new Texture2D(1,1) { filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
        block.SetPixel(0,0,Color.white); block.Apply(false,true);
        square = Sprite.Create(block,new Rect(0,0,1,1),Vector2.one*.5f,1);
        square.hideFlags = HideFlags.HideAndDontSave;
        // Shared, soft lobed silhouette: warm dust reads as clouds, not hard circular sparks.
        var haze = new Texture2D(64,64,TextureFormat.RGBA32,false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        var hazePixels = new Color[64*64];
        for (int y=0;y<64;y++) for (int x=0;x<64;x++)
        {
            Vector2 p = new Vector2((x+.5f)/32f-1f,(y+.5f)/32f-1f);
            float density = CloudLobe(p,new Vector2(-.3f,-.04f),.64f);
            density = Mathf.Max(density,CloudLobe(p,new Vector2(.27f,-.13f),.67f));
            density = Mathf.Max(density,CloudLobe(p,new Vector2(.02f,.3f),.62f));
            density = Mathf.Max(density,CloudLobe(p,new Vector2(-.06f,-.32f),.55f));
            hazePixels[y*64+x] = new Color(1,1,1,density);
        }
        haze.SetPixels(hazePixels); haze.Apply(false,true);
        cloud = Sprite.Create(haze,new Rect(0,0,64,64),Vector2.one*.5f,64,0,SpriteMeshType.FullRect);
        cloud.hideFlags = HideFlags.HideAndDontSave;
        lineMaterial = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave };
    }

    private static float CloudLobe(Vector2 point, Vector2 center, float size)
    {
        return Mathf.SmoothStep(0f,1f,Mathf.Clamp01((1f-Vector2.Distance(point,center)/size)*1.6f));
    }

    private void OnDestroy()
    {
        if (!registered) return;
        registered = false; users = Mathf.Max(0,users-1);
        if (users != 0) return;
        if (disk != null) { Free(disk.texture); Free(disk); }
        if (square != null) { Free(square.texture); Free(square); }
        if (cloud != null) { Free(cloud.texture); Free(cloud); }
        Free(lineMaterial); disk = square = cloud = null; lineMaterial = null;
    }
    private static void Free(Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying) Destroy(obj); else DestroyImmediate(obj);
    }
}
