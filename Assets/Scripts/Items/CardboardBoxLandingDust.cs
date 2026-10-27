using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Short, visual-only pixel dust pushed out from the box's ground footprint.</summary>
public static class CardboardBoxLandingDust
{
    private static Texture2D dustTexture;
    private static Material dustMaterial;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache()
    {
        if (dustMaterial != null) Object.Destroy(dustMaterial);
        if (dustTexture != null) Object.Destroy(dustTexture);
        dustMaterial = null;
        dustTexture = null;
    }

    public static ParticleSystem Play(SpriteRenderer source, GameObject owner)
    {
        if (source == null || source.sprite == null || owner == null || !Application.isPlaying) return null;
        EnsureMaterial();
        Bounds bounds = source.bounds;
        float width = Mathf.Max(0.2f, bounds.size.x);
        Vector3 center = new Vector3(bounds.center.x, bounds.min.y + bounds.size.y * .12f, source.transform.position.z);
        var root = new GameObject("Cardboard Landing Dust") { hideFlags = HideFlags.DontSave, layer = owner.layer };
        root.SetActive(false);
        Scene scene = ZeldaRuntimeRegistry.GetGameplayScene(owner);
        if (scene.IsValid() && scene.isLoaded) SceneManager.MoveGameObjectToScene(root, scene);
        root.transform.position = center;

        var system = root.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = system.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = .7f;
        main.startSpeed = 0f;
        main.startLifetime = .6f;
        main.startSize = .12f;
        main.maxParticles = 18;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Shape;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        main.useUnscaledTime = false;
        var emission = system.emission; emission.enabled = false;
        var shape = system.shape; shape.enabled = false;
        var collision = system.collision; collision.enabled = false;
        var speed = system.velocityOverLifetime; speed.enabled = true;
        speed.space = ParticleSystemSimulationSpace.World;
        speed.speedModifier = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, .15f));
        var size = system.sizeOverLifetime; size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, .55f), new Keyframe(.3f, 1.35f), new Keyframe(1f, 1.8f)));
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(.7f, 0f), new GradientAlphaKey(.55f, .3f), new GradientAlphaKey(0f, 1f) });
        var fade = system.colorOverLifetime; fade.enabled = true; fade.color = gradient;
        var renderer = root.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = dustMaterial;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortingLayerID = source.sortingLayerID;
        renderer.sortingOrder = source.sortingOrder - 1;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        root.SetActive(true);
        system.Play(false);
        for (int i = 0; i < 18; i++)
        {
            float angle = (i + Random.Range(-.2f, .2f)) * Mathf.PI * 2f / 18f;
            Vector2 outward = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * .48f);
            system.Emit(new ParticleSystem.EmitParams {
                position = center + (Vector3)(outward * width * .43f),
                velocity = (Vector3)(outward * Random.Range(.7f, 1.25f) * width) + Vector3.up * .08f,
                startSize = width * Random.Range(.12f, .21f),
                startLifetime = Random.Range(.38f, .65f),
                rotation = Random.Range(-15f, 15f),
                startColor = Color.Lerp(new Color(.52f, .46f, .36f), new Color(.82f, .75f, .61f), Random.value)
            }, 1);
        }
        // No per-particle GameObjects, gameplay collision, sound attraction or persistent state.
        Object.Destroy(root, .85f);
        return system;
    }

    private static void EnsureMaterial()
    {
        if (dustMaterial != null) return;
        const int side = 8;
        dustTexture = new Texture2D(side, side, TextureFormat.RGBA32, false) {
            name = "Cardboard Pixel Dust", filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave
        };
        var pixels = new Color[side * side];
        for (int y = 0; y < side; y++)
        for (int x = 0; x < side; x++)
        {
            float radius = new Vector2(x - 3.5f, y - 3.5f).magnitude;
            pixels[y * side + x] = new Color(1f, 1f, 1f, radius < 2.5f ? 1f : radius < 3.6f ? .45f : 0f);
        }
        dustTexture.SetPixels(pixels); dustTexture.Apply(false, true);
        dustMaterial = new Material(Shader.Find("Sprites/Default")) {
            name = "Cardboard Dust Material", mainTexture = dustTexture, hideFlags = HideFlags.HideAndDontSave
        };
    }
}
