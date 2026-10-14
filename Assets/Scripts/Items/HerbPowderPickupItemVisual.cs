using UnityEngine;

/// <summary>Original irregular powder clusters with loose grains, shared by three colour variants.</summary>
public sealed class HerbPowderPickupItemVisual : PickupItemVisualBase
{
    [SerializeField] private Color powderColor = new Color(.91f, .20f, .23f, 1);

    [Header("Rising Magic Particles")]
    [SerializeField] private bool animateParticles = true;
    [SerializeField, Range(1f, 16f)] private float particlesPerSecond = 8f;
    [SerializeField, Range(.3f, 3f)] private float particleLifetime = 1.6f;
    [SerializeField, Min(.01f)] private float riseSpeed = .6f;
    [SerializeField, Min(0f)] private float spreadSpeed = .24f;
    [Tooltip("Particle size before the item's world scale is applied.")]
    [SerializeField, Range(.01f, .2f)] private float particleSize = .075f;

    private SpriteRenderer sourceRenderer;
    private ParticleSystem magicParticles;
    private ParticleSystemRenderer particleRenderer;
    private Material particleMaterial;

    protected override void Awake()
    {
        base.Awake();
        sourceRenderer = GetComponent<SpriteRenderer>();
        // InventoryIcon only builds the sprite; particle systems belong to live world items.
        if (Application.isPlaying)
        {
            CreateParticles();
            ConfigureParticles();
            SyncParticleVisibility();
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        SyncParticleVisibility();
    }

    protected override void LateUpdate()
    {
        base.LateUpdate();
        SyncParticleVisibility();
    }

    protected override void OnDisable()
    {
        StopParticles();
        base.OnDisable();
    }

    protected override void OnDestroy()
    {
        if (magicParticles != null) Destroy(magicParticles.gameObject);
        if (particleMaterial != null) Destroy(particleMaterial);
        base.OnDestroy();
    }

    private void CreateParticles()
    {
        if (magicParticles != null) return;
        var child = new GameObject("Herbal Powder Magic Motes");
        child.SetActive(false);
        child.transform.SetParent(transform, false);
        child.layer = gameObject.layer;
        child.hideFlags = HideFlags.DontSave;
        magicParticles = child.AddComponent<ParticleSystem>();
        magicParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        particleRenderer = child.GetComponent<ParticleSystemRenderer>();
        particleMaterial = new Material(Shader.Find("Sprites/Default"))
        {
            name = "Herbal Powder Motes",
            hideFlags = HideFlags.HideAndDontSave,
            color = Color.white
        };
        particleRenderer.sharedMaterial = particleMaterial;
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        particleRenderer.receiveShadows = false;
        particleRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        particleRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        child.SetActive(true);
    }

    private void ConfigureParticles()
    {
        if (magicParticles == null) return;
        StopParticles();
        var main = magicParticles.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 3f;
        main.maxParticles = 32;
        main.prewarm = false;
        main.useUnscaledTime = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.cullingMode = ParticleSystemCullingMode.Pause;
        main.startLifetime = new ParticleSystem.MinMaxCurve(particleLifetime * .8f, particleLifetime * 1.15f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(particleSize * .7f, particleSize * 1.25f);
        main.startRotation = new ParticleSystem.MinMaxCurve(-.25f, .25f);
        main.startColor = Color.white;
        main.gravityModifier = 0f;
        var emission = magicParticles.emission;
        emission.enabled = true;
        emission.rateOverTime = particlesPerSecond;
        var shape = magicParticles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(.55f, .24f, .001f);
        shape.position = new Vector3(0, -.02f, 0);
        var velocity = magicParticles.velocityOverLifetime;
        velocity.enabled = true;
        // Screen-up motion stays upright even when the pickup is rotated in the map.
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-spreadSpeed, spreadSpeed);
        velocity.y = new ParticleSystem.MinMaxCurve(riseSpeed * .8f, riseSpeed * 1.2f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        var color = magicParticles.colorOverLifetime;
        color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] {
            new GradientColorKey(Color.Lerp(powderColor, Color.white, .2f), 0f),
            new GradientColorKey(powderColor, .65f),
            new GradientColorKey(powderColor, 1f)
        }, new[] {
            new GradientAlphaKey(0f, 0f), new GradientAlphaKey(.95f, .12f),
            new GradientAlphaKey(.95f, .6f), new GradientAlphaKey(0f, 1f)
        });
        color.color = gradient;
        var size = magicParticles.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, .65f), new Keyframe(.2f, 1f), new Keyframe(.4f, 1f), new Keyframe(1f, .25f)));
        var collision = magicParticles.collision;
        collision.enabled = false;
        var triggers = magicParticles.trigger;
        triggers.enabled = false;
    }

    private void SyncParticleVisibility()
    {
        if (magicParticles == null || sourceRenderer == null) return;
        particleRenderer.gameObject.layer = gameObject.layer;
        particleRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
        particleRenderer.sortingOrder = sourceRenderer.sortingOrder + 1;
        particleRenderer.maskInteraction = sourceRenderer.maskInteraction;
        bool visible = animateParticles && isActiveAndEnabled && sourceRenderer.enabled;
        particleRenderer.enabled = visible;
        if (visible)
        {
            if (magicParticles.isStopped) magicParticles.Play();
        }
        else StopParticles();
    }

    private void StopParticles()
    {
        if (magicParticles != null && !magicParticles.isStopped)
            magicParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (particleRenderer != null) particleRenderer.enabled = false;
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        particlesPerSecond = Mathf.Clamp(particlesPerSecond, 1f, 16f);
        particleLifetime = Mathf.Clamp(particleLifetime, .3f, 3f);
        riseSpeed = Mathf.Max(.01f, riseSpeed);
        spreadSpeed = Mathf.Max(0f, spreadSpeed);
        particleSize = Mathf.Clamp(particleSize, .01f, .2f);
        if (Application.isPlaying && magicParticles != null)
        {
            ConfigureParticles();
            SyncParticleVisibility();
        }
    }
#endif

    protected override string RuntimeSpriteName => "Herbal Powder Clusters";
    protected override bool UsesEmbeddedColors => true;
    protected override Color GetPixelColor(char pixel)
    {
        switch (pixel)
        {
            case 'D': return Color.Lerp(powderColor, new Color(.10f,.08f,.15f,1), .48f);
            case 'M': return powderColor;
            case 'H': return Color.Lerp(powderColor, Color.white, .38f);
            default: return Color.clear;
        }
    }
    protected override string[] GetPixelRows() => new[] {
        "................", "...........DD...", ".....DD...DMHD..", "....DMHD..DMMMD.",
        "....DHMMDDMMMD..", "...DDMMMHMMMD...", "..DMHMMHMMMD....", ".DHMMMDMMMHMDD..",
        ".DMMMMDMMHMMMMD.", "..DMMHMMMMMHMMD.", "..DDMMMMMMMMDD..", "....DDMMDDDD....",
        "..DD..DD....MD..", "..MD.......DD...", "................", "................"
    };
}
