using System.Collections.Generic;
using UnityEngine;

/// <summary>Animated geometric water ripples distributed across a SpriteRenderer's footprint.</summary>
[DisallowMultipleComponent]
[AddComponentMenu("Rendering/Sprite Water Flow Particles")]
public sealed class SpriteWaterFlowParticles : MonoBehaviour
{
    [System.Serializable]
    public sealed class StillWaterSettings
    {
        [Header("Ripples / 原地水纹")]
        [Min(0f), Tooltip("每平方世界单位的同时存活水纹数，不受寿命设置影响。")]
        public float density = 0.65f;
        [Min(0.1f)] public float lifetime = 3.5f;
        [Min(0.01f)] public float length = 0.48f;
        [Min(0.01f), Tooltip("水纹绘制区域高度，实际亮纹比此值更细。")]
        public float width = 0.18f;
        [Range(0f, 2f), Tooltip("仅控制原地伸缩和明暗起伏，不产生定向流动。")]
        public float animationSpeed = 0.3f;
        public Color color = new Color(0.3f, 0.75f, 0.95f, 0.38f);
        [Range(0f, 1f)] public float horizontalVisibilityBoost = 0.65f;
        public int sortingOrderOffset = 1;
        [Header("Surface And Budget / 轮廓与数量限制")]
        public bool followSpriteShape = true;
        public bool checkReadableAlpha = true;
        [Range(0f, 1f)] public float alphaThreshold = 0.1f;
        [Range(4, 512)] public int distributionCells = 64;
        [Range(256, 16384)] public int particleLimit = 2048;
    }

    /// <summary>Configure a private helper for calm, surface-anchored water. Existing flow components never call this.</summary>
    public void ConfigureStillWater(SpriteRenderer surface, StillWaterSettings settings)
    {
        if (settings == null) settings = new StillWaterSettings();
        sourceRenderer = surface;
        stationarySurface = true;
        flowDirection = Vector2.right;
        localDirection = false;
        speed = 0f;
        speedRandomness = 0f;
        directionSpread = 0f;
        currentSway = 0f;
        lifetime = Mathf.Max(0.1f, settings.lifetime);
        particlesPerSquareUnit = Mathf.Max(0f, settings.density) / lifetime;
        ribbonDensityMultiplier = 1f;
        particleSize = Mathf.Max(0.01f, settings.width);
        particleLength = Mathf.Max(0.01f, settings.length) / particleSize;
        ribbonLengthScale = ribbonWidthScale = 1f;
        particleColor = settings.color;
        shapeAnimationSpeed = Mathf.Clamp(settings.animationSpeed, 0f, 2f);
        horizontalVisibilityBoost = Mathf.Clamp01(settings.horizontalVisibilityBoost);
        sortingOrderOffset = settings.sortingOrderOffset;
        followSpriteShape = settings.followSpriteShape;
        checkReadableAlpha = settings.checkReadableAlpha;
        alphaThreshold = Mathf.Clamp01(settings.alphaThreshold);
        keepInsideSprite = true;
        distributionCells = Mathf.Clamp(settings.distributionCells, 4, 512);
        particleSafetyLimit = Mathf.Clamp(settings.particleLimit, 256, 16384);
        maximumParticles = 128;
        refreshRequired = true;
    }

    [Header("Surface / 水流区域")]
    [SerializeField, Tooltip("留空时自动查找自身或子对象的 SpriteRenderer。")]
    private SpriteRenderer sourceRenderer;
    [SerializeField, Tooltip("Simple Sprite 使用精灵网格轮廓；Sliced/Tiled 使用实际渲染矩形。")]
    private bool followSpriteShape = true;
    [SerializeField, Tooltip("贴图开启 Read/Write 时额外避开透明像素；未开启时使用精灵网格，不修改贴图导入设置。")]
    private bool checkReadableAlpha = true;
    [SerializeField, Range(0f, 1f)] private float alphaThreshold = 0.1f;
    [SerializeField] private bool keepInsideSprite = true;

    [Header("Flow / 流动")]
    [SerializeField, Tooltip("二维流向：(1,0) 右、(-1,0) 左、(0,1) 上、(0,-1) 下。默认世界坐标；启用 Local Direction 后才随 Sprite 旋转。零向量静止。")]
    private Vector2 flowDirection = Vector2.down;
    [SerializeField, Tooltip("默认关闭，XY 直接代表世界左右/上下。开启后随 Sprite 旋转：例如旋转 90° 的 Sprite，其本地向上实际是世界向左。")]
    private bool localDirection = false;
    [SerializeField, Min(0f)] private float speed = 0.5f;
    [SerializeField, Range(0f, 0.95f)] private float speedRandomness = 0.25f;
    [SerializeField, Range(0f, 90f)] private float directionSpread = 10f;
    [SerializeField, Min(0.1f)] private float lifetime = 1.8f;

    [Header("Density / 分布")]
    [SerializeField, Min(0f), Tooltip("密度基础值。目标水纹数 = 有效世界面积 × 此值 × 水纹密度倍率 × 平均寿命。")]
    private float particlesPerSquareUnit = 8f;
    // Retained as an initial buffer reservation for existing serialized components,
    // not a fixed ceiling that makes larger surfaces disproportionately sparse.
    [SerializeField, HideInInspector] private int maximumParticles = 256;
    [SerializeField, Range(256, 16384), Tooltip("自动按面积扩容时的性能保护上限。仅超过此上限的超大水面会降低实际密度。")]
    private int particleSafetyLimit = 8192;
    [SerializeField, Range(4, 512), Tooltip("随机打乱分区顺序，再在各分区内随机采样，避免纯随机造成明显扎堆。")]
    private int distributionCells = 64;

    [Header("Appearance / 外观")]
    [SerializeField] private Color particleColor = new Color(0.3f, 0.75f, 0.95f, 0.42f);
    [SerializeField, Min(0.001f)] private float particleSize = 0.045f;
    [SerializeField, Range(0f, 0.9f)] private float sizeRandomness = 0.35f;
    [SerializeField, Range(1f, 6f), Tooltip("水纹沿流向的长度比例，不使用拖尾。")]
    private float particleLength = 2f;
    [SerializeField] private int sortingOrderOffset = 1;

    [Header("Water Ribbons / 连续水纹")]
    [SerializeField, Min(1f), Tooltip("几何水纹的长度倍率；每条水纹会具有不同的长度、折角和变化节奏。")]
    private float ribbonLengthScale = 5f;
    [SerializeField, Min(1f)] private float ribbonWidthScale = 4f;
    [SerializeField, Range(0.05f, 1f), Tooltip("长水纹覆盖面积更大，因此降低发射密度，避免变成密集雨线。")]
    private float ribbonDensityMultiplier = 0.1f;
    [SerializeField, Range(0f, 0.5f)] private float currentSway = 0.12f;
    [SerializeField, Range(0f, 1f), Tooltip("补偿 CRT 扫描线对横向水纹的遮盖：适度加粗亮纹并提高不透明度。按屏幕方向平滑过渡，纵向不变；0 关闭。")]
    private float horizontalVisibilityBoost = 0.65f;
    [SerializeField, Range(0f, 2f), Tooltip("几何水纹缓慢伸缩、偏移和明暗变化的速度。0 停止形变，但仍沿流向移动。")]
    private float shapeAnimationSpeed = 1f;

    private ParticleSystem particles;
    private ParticleSystemRenderer particleRenderer;
    private Material runtimeMaterial;
    private ParticleSystem.Particle[] buffer;
    private Sprite cachedSprite;
    private SpriteDrawMode cachedDrawMode;
    private Bounds surfaceBounds;
    private Vector2[] vertices, uvs;
    private ushort[] triangles;
    private int[] cells;
    private int columns, rows, cellCursor;
    private float localSurfaceArea;
    public float SurfaceWorldArea { get; private set; }
    public int TargetParticleCount { get; private set; }
    private bool refreshRequired = true;
    private bool canSampleAlpha;
    private float flowTime;
    private Vector4 uvOrigin, uvAxisX, uvAxisY;
    private bool hasSpriteUv;
    private bool stationarySurface;
    private Matrix4x4 previousSurfaceToWorld;

    private void OnEnable()
    {
        refreshRequired = true;
        if (sourceRenderer == null) sourceRenderer = GetComponent<SpriteRenderer>();
        if (sourceRenderer == null) sourceRenderer = GetComponentInChildren<SpriteRenderer>(true);
    }

    private void Update()
    {
        if (sourceRenderer == null || sourceRenderer.sprite == null || !sourceRenderer.enabled ||
            !sourceRenderer.gameObject.activeInHierarchy)
        {
            if (particles != null) particles.Clear();
            SurfaceWorldArea = 0f;
            TargetParticleCount = 0;
            return;
        }
        Bounds bounds = sourceRenderer.localBounds;
        if (refreshRequired || cachedSprite != sourceRenderer.sprite || cachedDrawMode != sourceRenderer.drawMode ||
            bounds != surfaceBounds)
            Rebuild(bounds);
        if (particles == null) return;
        UpdateAreaAndCapacity();

        // Only the renderer is followed: the separate child never takes over an
        // authored ParticleSystem or changes the sprite's material/geometry.
        particles.gameObject.layer = sourceRenderer.gameObject.layer;
        particleRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
        particleRenderer.sortingOrder = sourceRenderer.sortingOrder + sortingOrderOffset;
        particleRenderer.maskInteraction = sourceRenderer.maskInteraction;
        flowTime += Time.deltaTime;
        UpdateRibbonMaterial();
        int liveCount = UpdateCurrentParticles();

        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        // Maintain an area-scaled population, not a fixed ceiling. The initial
        // population ramps in briefly instead of emitting a large one-frame burst.
        int spawnBudget = Mathf.Clamp(Mathf.CeilToInt(TargetParticleCount * Mathf.Min(dt, 0.1f) * 8f), 8, 512);
        int requests = Mathf.Clamp(TargetParticleCount - liveCount, 0, spawnBudget);
        for (int i = 0; i < requests; i++) EmitParticle();
    }

    private void UpdateAreaAndCapacity()
    {
        Transform surface = sourceRenderer.transform;
        // Cross-product area handles rotated parents, nonuniform/negative scale
        // and transformed XY planes without an inflated world-axis bounding box.
        float scaleArea = Vector3.Cross(surface.TransformVector(Vector3.right),
            surface.TransformVector(Vector3.up)).magnitude;
        SurfaceWorldArea = localSurfaceArea * scaleArea;
        float desiredCount = SurfaceWorldArea * particlesPerSquareUnit * ribbonDensityMultiplier * lifetime;
        TargetParticleCount = Mathf.RoundToInt(Mathf.Clamp(desiredCount, 0f, particleSafetyLimit));
        int capacity = Mathf.Min(particleSafetyLimit,
            Mathf.NextPowerOfTwo(Mathf.Max(maximumParticles, Mathf.Max(8, TargetParticleCount))));
        if (buffer == null || buffer.Length < capacity)
        {
            buffer = new ParticleSystem.Particle[capacity];
            var main = particles.main;
            main.maxParticles = capacity;
        }
    }

    private float MeasureLocalSurfaceArea()
    {
        float rectangleArea = Mathf.Max(0f, surfaceBounds.size.x * surfaceBounds.size.y);
        if (!followSpriteShape || cachedDrawMode != SpriteDrawMode.Simple) return rectangleArea;
        if (canSampleAlpha)
        {
            // Cached once per shape change; never scan the texture every frame.
            const int samples = 32;
            int inside = 0;
            for (int y = 0; y < samples; y++)
                for (int x = 0; x < samples; x++)
                {
                    Vector3 p = surfaceBounds.min + new Vector3((x + 0.5f) / samples * surfaceBounds.size.x,
                        (y + 0.5f) / samples * surfaceBounds.size.y, 0f);
                    if (IsInsideSurface(p)) inside++;
                }
            if (canSampleAlpha) return rectangleArea * inside / (samples * samples);
        }
        float meshArea = 0f;
        for (int i = 0; i + 2 < triangles.Length; i += 3)
        {
            Vector2 a = vertices[triangles[i + 1]] - vertices[triangles[i]];
            Vector2 b = vertices[triangles[i + 2]] - vertices[triangles[i]];
            meshArea += Mathf.Abs(a.x * b.y - a.y * b.x) * 0.5f;
        }
        return Mathf.Min(rectangleArea, meshArea);
    }

    private void Rebuild(Bounds bounds)
    {
        refreshRequired = false;
        cachedSprite = sourceRenderer.sprite;
        cachedDrawMode = sourceRenderer.drawMode;
        surfaceBounds = bounds;
        previousSurfaceToWorld = sourceRenderer.transform.localToWorldMatrix;
        vertices = cachedSprite.vertices;
        uvs = cachedSprite.uv;
        triangles = cachedSprite.triangles;
        CacheSpriteUvMapping();
        canSampleAlpha = checkReadableAlpha && cachedSprite.texture != null && cachedSprite.texture.isReadable;
        localSurfaceArea = MeasureLocalSurfaceArea();
        float aspect = Mathf.Max(0.001f, bounds.size.x) / Mathf.Max(0.001f, bounds.size.y);
        columns = Mathf.Clamp(Mathf.RoundToInt(Mathf.Sqrt(distributionCells * aspect)), 1, distributionCells);
        rows = Mathf.Clamp(Mathf.CeilToInt((float)distributionCells / columns), 1, distributionCells);
        cells = new int[columns * rows];
        for (int i = 0; i < cells.Length; i++) cells[i] = i;
        ShuffleCells();

        if (particles == null)
        {
            var child = new GameObject("Sprite Water Flow (Runtime)");
            child.SetActive(false);
            child.transform.SetParent(transform, false);
            particles = child.AddComponent<ParticleSystem>();
            particleRenderer = child.GetComponent<ParticleSystemRenderer>();
            Shader shader = Shader.Find("Hidden/Cogitans/SpriteWaterFlow");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                Debug.LogWarning("SpriteWaterFlowParticles requires the Sprites/Default shader.", this);
                enabled = false;
                return;
            }
            runtimeMaterial = new Material(shader) { name = "Water Flow Ribbons", hideFlags = HideFlags.HideAndDontSave };
            particleRenderer.sharedMaterial = runtimeMaterial;
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            // Rotation below is derived from world XY, not the camera or emitter.
            particleRenderer.alignment = ParticleSystemRenderSpace.World;
            // StableRandomX is packed after UV.xy into TEXCOORD0.z. It remains
            // constant for each particle through movement, wrapping and compaction.
            particleRenderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
            {
                ParticleSystemVertexStream.Position,
                ParticleSystemVertexStream.Color,
                ParticleSystemVertexStream.UV,
                ParticleSystemVertexStream.StableRandomX
            });
        }
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        buffer = new ParticleSystem.Particle[maximumParticles];
        var main = particles.main;
        main.playOnAwake = false;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Shape;
        main.maxParticles = maximumParticles;
        main.startSize3D = true;
        main.gravityModifier = 0f;
        var emission = particles.emission;
        emission.enabled = false;
        var shape = particles.shape;
        shape.enabled = false;
        var fade = particles.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f),
                new GradientAlphaKey(1f, 0.65f), new GradientAlphaKey(0f, 1f) });
        fade.color = gradient;
        particles.gameObject.SetActive(true);
        particles.Play();
    }

    private void ShuffleCells()
    {
        for (int i = cells.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int value = cells[i]; cells[i] = cells[j]; cells[j] = value;
        }
        cellCursor = 0;
    }

    private void EmitParticle()
    {
        // Rejection keeps transparent/outside cells empty, with bounded work even
        // for extremely sparse sprites. Each trial consumes a different cell.
        for (int attempt = 0; attempt < 12; attempt++)
        {
            if (cellCursor >= cells.Length) ShuffleCells();
            int cell = cells[cellCursor++];
            Vector3 point = surfaceBounds.min + new Vector3(
                (cell % columns + Random.value) / columns * surfaceBounds.size.x,
                (cell / columns + Random.value) / rows * surfaceBounds.size.y, 0f);
            point.z = surfaceBounds.center.z;
            if (!IsInsideSurface(point)) continue;
            Vector3 flow = WorldFlowDirection();
            Vector3 direction = flow;
            direction = Quaternion.AngleAxis(Random.Range(-directionSpread, directionSpread) * 0.25f, Vector3.forward) * direction;
            float size = particleSize * Random.Range(1f - sizeRandomness, 1f + sizeRandomness);
            var emit = new ParticleSystem.EmitParams
            {
                position = sourceRenderer.transform.TransformPoint(point),
                velocity = direction * speed * Random.Range(1f - speedRandomness, 1f + speedRandomness),
                startColor = particleColor,
                startLifetime = lifetime * Random.Range(0.8f, 1.2f),
                startSize3D = new Vector3(size * particleLength * ribbonLengthScale, size * ribbonWidthScale, size),
                rotation = -Mathf.Atan2(flow.y, flow.x) * Mathf.Rad2Deg
            };
            particles.Emit(emit, 1);
            return;
        }
    }

    private bool IsInsideSurface(Vector3 localPoint)
    {
        if (localPoint.x < surfaceBounds.min.x || localPoint.x > surfaceBounds.max.x ||
            localPoint.y < surfaceBounds.min.y || localPoint.y > surfaceBounds.max.y) return false;
        // Sliced/tiled renderers stretch or repeat their geometry, not the original
        // sprite mesh. Their actual rectangular local bounds are authoritative.
        if (!followSpriteShape || cachedDrawMode != SpriteDrawMode.Simple) return true;
        Vector2 p = new Vector2(sourceRenderer.flipX ? -localPoint.x : localPoint.x,
            sourceRenderer.flipY ? -localPoint.y : localPoint.y);
        for (int i = 0; i + 2 < triangles.Length; i += 3)
        {
            int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
            Vector2 v = vertices[b] - vertices[a], w = vertices[c] - vertices[a], q = p - vertices[a];
            float det = v.x * w.y - v.y * w.x;
            if (Mathf.Abs(det) < 0.0000001f) continue;
            float u = (q.x * w.y - q.y * w.x) / det;
            float t = (v.x * q.y - v.y * q.x) / det;
            if (u < -0.0001f || t < -0.0001f || u + t > 1.0001f) continue;
            if (canSampleAlpha)
            {
                Vector2 uv = uvs[a] * (1f - u - t) + uvs[b] * u + uvs[c] * t;
                try { return cachedSprite.texture.GetPixelBilinear(uv.x, uv.y).a >= alphaThreshold; }
                catch (UnityException)
                {
                    // Some compressed texture formats still disallow CPU sampling.
                    // Fall back once to the sprite mesh, never throw each frame.
                    canSampleAlpha = false;
                }
            }
            return true;
        }
        return false;
    }

    private Vector3 WorldFlowDirection()
    {
        Vector3 direction = flowDirection.normalized;
        return localDirection ? sourceRenderer.transform.TransformVector(direction).normalized : direction;
    }

    /// <summary>Sets an unambiguous world XY current, independent of sprite rotation/scale.</summary>
    public void SetWorldFlowDirection(Vector2 direction)
    {
        localDirection = false;
        flowDirection = direction;
        // Existing particles adopt the new velocity/orientation next Update;
        // do not clear them or restart their fade-in.
    }

    [ContextMenu("Flow Direction/Up (World) / 向上")]
    private void FlowUp() => SetWorldFlowDirection(Vector2.up);

    [ContextMenu("Flow Direction/Down (World) / 向下")]
    private void FlowDown() => SetWorldFlowDirection(Vector2.down);

    [ContextMenu("Flow Direction/Left (World) / 向左")]
    private void FlowLeft() => SetWorldFlowDirection(Vector2.left);

    [ContextMenu("Flow Direction/Right (World) / 向右")]
    private void FlowRight() => SetWorldFlowDirection(Vector2.right);

    private int UpdateCurrentParticles()
    {
        int count = particles.GetParticles(buffer);
        int active = 0;
        Matrix4x4 surfaceToWorld = sourceRenderer.transform.localToWorldMatrix;
        Matrix4x4 surfaceDelta = stationarySurface
            ? surfaceToWorld * previousSurfaceToWorld.inverse : Matrix4x4.identity;
        Vector3 forward = WorldFlowDirection();
        Vector3 sideways = new Vector3(-forward.y, forward.x, 0f);
        float rippleRotation = -Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg;
        for (int i = 0; i < count; i++)
        {
            if (buffer[i].remainingLifetime <= 0f) continue;
            // Still water belongs to its surface: moving/scaling a pool never leaves ripples behind.
            if (stationarySurface) buffer[i].position = surfaceDelta.MultiplyPoint3x4(buffer[i].position);
            if (keepInsideSprite && !IsInsideSurface(sourceRenderer.transform.InverseTransformPoint(buffer[i].position)))
            {
                // Re-enter at the opposite edge without restarting the fade/lifetime.
                // Killing every exit makes narrow sprites disproportionately full of
                // invisible newborn particles, even at the same target population.
                Vector3 p = sourceRenderer.transform.InverseTransformPoint(buffer[i].position);
                p.x = surfaceBounds.min.x + Mathf.Repeat(p.x - surfaceBounds.min.x, Mathf.Max(0.0001f, surfaceBounds.size.x));
                p.y = surfaceBounds.min.y + Mathf.Repeat(p.y - surfaceBounds.min.y, Mathf.Max(0.0001f, surfaceBounds.size.y));
                if (!IsInsideSurface(p))
                {
                    bool found = false;
                    for (int trial = 0; trial < 12; trial++)
                    {
                        p.x = Random.Range(surfaceBounds.min.x, surfaceBounds.max.x);
                        p.y = Random.Range(surfaceBounds.min.y, surfaceBounds.max.y);
                        if (IsInsideSurface(p)) { found = true; break; }
                    }
                    if (!found) continue;
                }
                buffer[i].position = sourceRenderer.transform.TransformPoint(p);
            }
            // Shrinking surfaces/density values also converge immediately.
            if (active >= TargetParticleCount) continue;
            // Neighbours share a gently undulating current instead of independently
            // flying in random directions like snow/sparks. Seed fixes each speed.
            Vector3 relative = buffer[i].position - sourceRenderer.bounds.center;
            float phase = Vector3.Dot(relative, forward) * 2.2f + Vector3.Dot(relative, sideways) * 1.4f - flowTime * 1.3f;
            Vector3 direction = (forward + sideways * (Mathf.Sin(phase) * currentSway)).normalized;
            float variation = (buffer[i].randomSeed % 997u) / 996f;
            buffer[i].velocity = direction * speed * Mathf.Lerp(1f - speedRandomness, 1f + speedRandomness, variation);
            // Keep the geometric pattern aligned with the authored flow. Continuously
            // rotating thin crests produces crawling bright edges after CRT sampling.
            buffer[i].rotation = rippleRotation;
            buffer[active++] = buffer[i];
        }
        if (count > 0) particles.SetParticles(buffer, active);
        previousSurfaceToWorld = surfaceToWorld;
        return active;
    }

    private void CacheSpriteUvMapping()
    {
        hasSpriteUv = false;
        for (int i = 0; i + 2 < triangles.Length; i += 3)
        {
            int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
            Vector2 x = vertices[b] - vertices[a], y = vertices[c] - vertices[a];
            float det = x.x * y.y - x.y * y.x;
            if (Mathf.Abs(det) < 0.0000001f) continue;
            Vector2 u = uvs[b] - uvs[a], v = uvs[c] - uvs[a];
            Vector2 dx = (u * y.y - v * x.y) / det;
            Vector2 dy = (v * x.x - u * y.x) / det;
            Vector2 origin = uvs[a] - dx * vertices[a].x - dy * vertices[a].y;
            uvOrigin = new Vector4(origin.x, origin.y, 0f, 0f);
            uvAxisX = new Vector4(dx.x, dx.y, 0f, 0f);
            uvAxisY = new Vector4(dy.x, dy.y, 0f, 0f);
            hasSpriteUv = true;
            break;
        }
    }

    private void UpdateRibbonMaterial()
    {
        if (runtimeMaterial == null) return;
        Vector3 flow = WorldFlowDirection();
        if (flow.sqrMagnitude < 0.0001f) flow = Vector3.right;
        runtimeMaterial.SetVector("_WorldFlowDirection", new Vector4(flow.x, flow.y, flow.z, 0f));
        runtimeMaterial.SetFloat("_HorizontalVisibilityBoost", horizontalVisibilityBoost);
        runtimeMaterial.SetFloat("_FlowTime", flowTime * shapeAnimationSpeed);
        runtimeMaterial.SetFloat("_ClipToSurface", keepInsideSprite ? 1f : 0f);
        runtimeMaterial.SetMatrix("_WorldToSurface", sourceRenderer.transform.worldToLocalMatrix);
        runtimeMaterial.SetVector("_SurfaceBounds", new Vector4(surfaceBounds.min.x, surfaceBounds.min.y, surfaceBounds.max.x, surfaceBounds.max.y));
        runtimeMaterial.SetVector("_SpriteFlip", new Vector4(sourceRenderer.flipX ? -1f : 1f, sourceRenderer.flipY ? -1f : 1f, 0f, 0f));
        runtimeMaterial.SetFloat("_UseSpriteAlpha", followSpriteShape && cachedDrawMode == SpriteDrawMode.Simple && hasSpriteUv ? 1f : 0f);
        runtimeMaterial.SetTexture("_SpriteTex", cachedSprite.texture);
        runtimeMaterial.SetVector("_UVOrigin", uvOrigin);
        runtimeMaterial.SetVector("_UVAxisX", uvAxisX);
        runtimeMaterial.SetVector("_UVAxisY", uvAxisY);
    }

    [ContextMenu("Refresh Sprite Shape / 刷新区域")]
    public void RefreshSpriteShape() => refreshRequired = true;

    private void OnDisable()
    {
        if (particles != null)
        {
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (particles != null) Destroy(particles.gameObject);
        if (runtimeMaterial != null) Destroy(runtimeMaterial);
    }

    private void OnValidate()
    {
        speed = Mathf.Max(0f, speed);
        lifetime = Mathf.Max(0.1f, lifetime);
        particleSize = Mathf.Max(0.001f, particleSize);
        particlesPerSquareUnit = Mathf.Max(0f, particlesPerSquareUnit);
        maximumParticles = Mathf.Clamp(maximumParticles, 8, 1024);
        particleSafetyLimit = Mathf.Clamp(particleSafetyLimit, 256, 16384);
        distributionCells = Mathf.Clamp(distributionCells, 4, 512);
        ribbonLengthScale = Mathf.Max(1f, ribbonLengthScale);
        ribbonWidthScale = Mathf.Max(1f, ribbonWidthScale);
        refreshRequired = true;
    }
}
