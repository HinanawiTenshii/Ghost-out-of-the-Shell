using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Leaf scatter, snapped twigs and dry ground dust, independent of the destroyed bush.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(DoorData))]
public sealed class BushDestructionEffects : MonoBehaviour, IDoorDestructionEffect
{
    [SerializeField] private AudioClip breakSound;
    [SerializeField, Range(0f, 1f)] private float volume = .7f;
    [SerializeField, Min(1f)] private float soundDistance = 18f;
    [SerializeField, Range(8, 64)] private int leafCount = 30;
    private bool played;
    private static readonly Material[] materials = new Material[3];
    private static readonly Texture2D[] textures = new Texture2D[3];

    private void Awake()
    {
        if (breakSound == null) breakSound = Resources.Load<AudioClip>("Audio/BushBreak");
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache()
    {
        for (int i = 0; i < materials.Length; i++)
        {
            if (materials[i] != null) Destroy(materials[i]);
            if (textures[i] != null) Destroy(textures[i]);
            materials[i] = null; textures[i] = null;
        }
    }

    public void PlayDestruction(Vector3 center, SpriteRenderer source)
    {
        if (played || !Application.isPlaying) return;
        played = true;
        var root = new GameObject("Bush Destruction") { hideFlags = HideFlags.DontSave, layer = gameObject.layer };
        root.SetActive(false);
        SceneManager.MoveGameObjectToScene(root, gameObject.scene);
        root.transform.position = center;
        root.AddComponent<CameraVisionStreamingExempt>();
        float scale = source != null ? Mathf.Max(.3f, source.bounds.size.x / 2.4f) : 1f;
        int layer = source != null ? source.sortingLayerID : 0;
        int order = source != null ? source.sortingOrder : 5;
        var leaves = CreateSystem(root, "Scattered Leaves", 0, leafCount, layer, order + 4, .2f);
        var twigs = CreateSystem(root, "Snapped Twigs", 1, 8, layer, order + 3, .45f);
        var dust = CreateSystem(root, "Ground Dust", 2, 12, layer, order - 1, 0f);
        AudioSource audio = null;
        if (breakSound != null && volume > 0)
        {
            audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false; audio.loop = false; audio.clip = breakSound;
            audio.volume = volume; audio.spatialBlend = 1f; audio.dopplerLevel = 0;
            audio.rolloffMode = AudioRolloffMode.Linear; audio.minDistance = 2;
            audio.maxDistance = Mathf.Max(2.01f, soundDistance);
        }
        root.SetActive(true);
        leaves.Play(); twigs.Play(); dust.Play();
        if (audio != null) audio.Play();
        Emit(leaves, center, scale, leafCount, 0);
        Emit(twigs, center, scale, 8, 1);
        Emit(dust, center + Vector3.down * .5f * scale, scale, 12, 2);
        Destroy(root, Mathf.Max(1.35f, breakSound != null ? breakSound.length + .15f : 0));
    }

    private static ParticleSystem CreateSystem(GameObject parent, string name, int kind, int count, int layer, int order, float gravity)
    {
        EnsureMaterial(kind);
        var go = new GameObject(name); go.transform.SetParent(parent.transform, false); go.layer = parent.layer;
        var system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = system.main;
        main.playOnAwake = false; main.loop = false; main.duration = 1.1f;
        main.startSpeed = 0; main.startLifetime = 1; main.startSize = .15f;
        main.maxParticles = count; main.gravityModifier = gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        var emission = system.emission; emission.enabled = false;
        var shape = system.shape; shape.enabled = false;
        var collision = system.collision; collision.enabled = false;
        var rotation = system.rotationOverLifetime; rotation.enabled = kind != 2;
        rotation.z = new ParticleSystem.MinMaxCurve(-5f, 5f);
        var speed = system.velocityOverLifetime; speed.enabled = true;
        speed.speedModifier = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0,1,1,.3f));
        var size = system.sizeOverLifetime; size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1, kind == 2 ? AnimationCurve.Linear(0,.6f,1,2f) :
            new AnimationCurve(new Keyframe(0,1), new Keyframe(.65f,1), new Keyframe(1,.4f)));
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white,0), new GradientColorKey(Color.white,1) },
            new[] { new GradientAlphaKey(kind == 2 ? .4f : 1,0), new GradientAlphaKey(kind == 2 ? .2f : 1,.4f), new GradientAlphaKey(0,1) });
        var fade = system.colorOverLifetime; fade.enabled = true; fade.color = gradient;
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = materials[kind]; renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortingLayerID = layer; renderer.sortingOrder = order;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
        return system;
    }

    private static void Emit(ParticleSystem system, Vector3 center, float scale, int count, int kind)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = (i + Random.Range(-.3f,.3f)) * Mathf.PI * 2 / count;
            Vector3 outward = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle) * .55f, 0);
            Color color = kind == 0 ? Color.Lerp(new Color(.10f,.48f,.28f), new Color(.28f,.83f,.43f), Random.value) :
                kind == 1 ? new Color(.42f,.27f,.12f) : new Color(.56f,.50f,.32f);
            system.Emit(new ParticleSystem.EmitParams {
                position = center + new Vector3(Random.Range(-.9f,.9f), Random.Range(-.35f,.45f), 0) * scale,
                velocity = (outward * Random.Range(kind == 2 ? .5f : 1.8f, kind == 2 ? 1.3f : 3.3f) +
                    Vector3.up * (kind == 2 ? .08f : Random.Range(.6f,1.5f))) * scale,
                startLifetime = Random.Range(kind == 2 ? .4f : .65f, kind == 2 ? .7f : 1.05f),
                startSize = Random.Range(kind == 2 ? .25f : .12f, kind == 2 ? .45f : .25f) * scale,
                rotation = Random.Range(0,360f), startColor = color
            }, 1);
        }
    }

    private static void EnsureMaterial(int kind)
    {
        if (materials[kind] != null) return;
        var texture = new Texture2D(8,8,TextureFormat.RGBA32,false) {
            name = "Bush Debris " + kind, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        var pixels = new Color[64];
        for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++)
        {
            bool solid = kind == 0 ? (Mathf.Abs(x-y) <= 2 && x+y >= 3 && x+y <= 11) :
                kind == 1 ? (x == 3 || (y >= 4 && x == y-1)) : new Vector2(x-3.5f,y-3.5f).sqrMagnitude < 13;
            pixels[y*8+x] = solid ? Color.white : Color.clear;
        }
        texture.SetPixels(pixels); texture.Apply(false,true); textures[kind] = texture;
        materials[kind] = new Material(Shader.Find("Sprites/Default")) {
            name = "Bush Debris Material " + kind, mainTexture = texture, hideFlags = HideFlags.HideAndDontSave
        };
    }
}
