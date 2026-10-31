using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Restrained, flat-colour polygon debris. No raster detail, glow or physical fragments.</summary>
public static class DoorDestructionVisual
{
    public enum Surface { Wood, Glass, Metal }
    private static Material material;
    private static readonly Mesh[] meshes = new Mesh[6];
    private static readonly List<ParticleSystemVertexStream> vertexStreams = new List<ParticleSystemVertexStream> {
        ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache()
    {
        if (material != null) Object.Destroy(material);
        material = null;
        for (int i = 0; i < meshes.Length; i++)
        {
            if (meshes[i] != null) Object.Destroy(meshes[i]);
            meshes[i] = null;
        }
    }

    public static GameObject Play(SpriteRenderer source, Surface surface, int count, float lifetime, float speed, float size)
    {
        if (source == null || !Application.isPlaying || count <= 0 || lifetime <= 0 || size <= 0) return null;
        EnsureGeometry();
        count = Mathf.Clamp(count, 1, 48);
        lifetime = Mathf.Clamp(lifetime, .1f, 3f);
        size = Mathf.Min(size, 1.5f);
        speed = Mathf.Clamp(speed, 0, 15);
        var root = new GameObject(surface + " Door Debris") { hideFlags = HideFlags.DontSave, layer = source.gameObject.layer };
        root.SetActive(false);
        SceneManager.MoveGameObjectToScene(root, source.gameObject.scene);
        root.transform.position = source.bounds.center;
        root.AddComponent<CameraVisionStreamingExempt>();
        int smallCount = Mathf.Clamp(count / 2, 1, 18);
        int speckCount = Mathf.Clamp(count / 3, 1, 8);
        int chunkShape = surface == Surface.Wood ? 0 : surface == Surface.Glass ? 2 : 4;
        int chipShape = surface == Surface.Glass ? 3 : 1;
        var chunks = Create(root, "Fragments", meshes[chunkShape], count, source, 2, lifetime, .32f, false);
        var chips = Create(root, "Small Chips", meshes[chipShape], smallCount, source, 3, lifetime, .18f, false);
        var specks = Create(root, "Fine Debris", meshes[surface == Surface.Glass ? 3 : 5], speckCount,
            source, 1, lifetime, .05f, true);
        root.SetActive(true);
        chunks.Play(); chips.Play(); specks.Play();
        Emit(chunks, source, surface, count, lifetime, speed, size, 0);
        Emit(chips, source, surface, smallCount, lifetime, speed, size, 1);
        Emit(specks, source, surface, speckCount, lifetime, speed, size, 2);
        Object.Destroy(root, lifetime + .15f);
        return root;
    }

    private static ParticleSystem Create(GameObject root, string name, Mesh mesh, int count, SpriteRenderer source,
        int orderOffset, float lifetime, float gravity, bool fine)
    {
        var go = new GameObject(name) { layer = root.layer };
        go.transform.SetParent(root.transform, false);
        var system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = system.main;
        main.playOnAwake = false; main.loop = false; main.duration = lifetime;
        main.startSpeed = 0; main.startSize = 1; main.startLifetime = lifetime; main.maxParticles = count;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate; main.gravityModifier = gravity;
        var emission = system.emission; emission.enabled = false;
        var shape = system.shape; shape.enabled = false;
        var collision = system.collision; collision.enabled = false;
        var rotation = system.rotationOverLifetime; rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-7f, 7f);
        var velocity = system.velocityOverLifetime; velocity.enabled = true;
        velocity.speedModifier = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0,1,1,.32f));
        var shrink = system.sizeOverLifetime; shrink.enabled = true;
        shrink.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0,1), new Keyframe(.6f,1), new Keyframe(1,.65f)));
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white,0), new GradientColorKey(Color.white,1) },
            new[] { new GradientAlphaKey(fine ? .45f : 1,0), new GradientAlphaKey(fine ? .3f : 1,.45f), new GradientAlphaKey(0,1) });
        var fade = system.colorOverLifetime; fade.enabled = true; fade.color = gradient;
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material; renderer.renderMode = ParticleSystemRenderMode.Mesh; renderer.mesh = mesh;
        // Explicit vertex colour layout, with no sprite-renderer tint or lighting.
        renderer.enableGPUInstancing = false;
        renderer.SetActiveVertexStreams(vertexStreams);
        renderer.alignment = ParticleSystemRenderSpace.World;
        renderer.sortingLayerID = source.sortingLayerID; renderer.sortingOrder = source.sortingOrder + orderOffset;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
        return system;
    }

    private static void Emit(ParticleSystem system, SpriteRenderer source, Surface surface, int count,
        float lifetime, float speed, float size, int kind)
    {
        // Sample the oriented source rectangle, not its world AABB: narrow and
        // rotated doors/window panes should split along their own silhouettes.
        Bounds local = source.sprite != null ? source.sprite.bounds : new Bounds(Vector3.zero, Vector3.one);
        if (source.drawMode != SpriteDrawMode.Simple) local.size = new Vector3(source.size.x,source.size.y,0);
        bool alongX = source.transform.TransformVector(Vector3.right*local.size.x).sqrMagnitude >=
            source.transform.TransformVector(Vector3.up*local.size.y).sqrMagnitude;
        Color baseColor = source.color; baseColor.a = 1;
        for (int i = 0; i < count; i++)
        {
            // De-correlate flight angle from the ordered panel samples; otherwise
            // a long door's debris forms an artificial curled ribbon.
            float angle = i * 2.39996323f + Random.Range(-.3f,.3f);
            Vector3 outward = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)*.65f, 0);
            // Stratification spreads pieces along the whole panel, even at low counts.
            float fraction = (i + Random.Range(.15f,.85f)) / count - .5f;
            Vector3 point = local.center + new Vector3(
                local.size.x * fraction, local.size.y * Random.Range(-.48f,.48f), 0);
            if (!alongX)
                point = local.center + new Vector3(local.size.x * Random.Range(-.48f,.48f), local.size.y * fraction, 0);
            Vector3 position = source.transform.TransformPoint(point);
            position.z = source.transform.position.z;
            Color color = Color.Lerp(baseColor * .78f, baseColor, Random.value);
            if (surface == Surface.Glass) color = Color.Lerp(baseColor, Color.white, Random.Range(.08f,.32f));
            else if (surface == Surface.Metal) color = Color.Lerp(color, Color.white, Random.Range(0,.16f));
            color.a = 1;
            float particleSize = size * (kind == 0 ? Random.Range(1,1.65f) : kind == 1 ? Random.Range(.6f,1f) : Random.Range(.24f,.5f));
            system.Emit(new ParticleSystem.EmitParams {
                position = position,
                velocity = (outward * Random.Range(.45f,.8f) + Vector3.up * Random.Range(.12f,.3f)) * speed * (kind == 2 ? .4f : .65f),
                startSize = particleSize,
                startLifetime = lifetime * Random.Range(kind == 2 ? .45f : .75f, 1),
                rotation = Random.Range(0,360), startColor = color
            }, 1);
        }
    }

    private static void EnsureGeometry()
    {
        if (material != null) return;
        material = new Material(Shader.Find("Sprites/Default")) {
            name = "Flat Door Debris", mainTexture = Texture2D.whiteTexture, hideFlags = HideFlags.HideAndDontSave
        };
        // Convex silhouettes rendered as actual triangles: crisp vector edges at every zoom.
        meshes[0] = Polygon("Wood Chunk", new Vector2(-.25f,-.5f), new Vector2(.14f,-.43f), new Vector2(.27f,.35f),
            new Vector2(.1f,.5f), new Vector2(-.24f,.43f));
        meshes[1] = Polygon("Splinter", new Vector2(-.08f,-.5f), new Vector2(.1f,-.23f), new Vector2(.06f,.5f), new Vector2(-.1f,.24f));
        meshes[2] = Polygon("Glass Shard", new Vector2(-.42f,-.5f), new Vector2(.44f,-.22f), new Vector2(.3f,.5f));
        meshes[3] = Polygon("Small Shard", new Vector2(-.35f,-.3f), new Vector2(.4f,-.2f), new Vector2(-.1f,.5f));
        meshes[4] = Polygon("Panel Fragment", new Vector2(-.4f,-.32f), new Vector2(.16f,-.42f), new Vector2(.4f,-.17f),
            new Vector2(.37f,.38f), new Vector2(-.33f,.32f));
        meshes[5] = Polygon("Fine Chip", new Vector2(-.2f,-.4f), new Vector2(.26f,-.34f), new Vector2(.4f,.08f),
            new Vector2(.17f,.4f), new Vector2(-.35f,.25f), new Vector2(-.4f,-.08f));
    }

    private static Mesh Polygon(string name, params Vector2[] points)
    {
        // Mesh particles in this Unity version consume packed UNorm8 colours.
        // Float mesh colours are interpreted as packed bytes (white becomes dark blue).
        var vertices = new Vector3[points.Length]; var colors = new Color32[points.Length]; var uv = new Vector2[points.Length];
        for (int i = 0; i < points.Length; i++) { vertices[i] = points[i]; colors[i] = Color.white; uv[i] = Vector2.one*.5f; }
        var triangles = new int[(points.Length-2)*3];
        for (int i = 0; i < points.Length-2; i++) { triangles[i*3] = 0; triangles[i*3+1] = i+1; triangles[i*3+2] = i+2; }
        var mesh = new Mesh { name = name, hideFlags = HideFlags.HideAndDontSave };
        mesh.vertices = vertices; mesh.colors32 = colors; mesh.uv = uv; mesh.triangles = triangles; mesh.RecalculateBounds();
        return mesh;
    }
}
