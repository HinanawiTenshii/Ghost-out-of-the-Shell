using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Inert, scene-local death remains. Freeze the actual pose before attack cleanup,
/// then consume it tail-first with the existing character (not bomb) explosion.
/// The living character is destroyed immediately by SandwormZeldaCharacterData.
/// </summary>
public sealed class SandwormDeathSequence : CameraEnvironmentalShakeSource
{
    private struct Part
    {
        public SpriteRenderer Renderer;
        public Vector3 Center;
        public float Scale;
        public bool IsHead;
    }

    private readonly List<Part> parts = new List<Part>();
    private readonly List<ZeldaDeathExplosionVisual> bursts = new List<ZeldaDeathExplosionVisual>();
    private float interval, duration, age;
    private Color tint;
    private int nextPart;
    private float shakeDuration, shakeStrength, headBurstTime = -1f;
    private Bounds headBurstBounds;
    private Transform headBurstTransform;
    private SandwormAudio.DeathPlayback deathAudio;

    public static bool TryCreate(SandwormBossPrototype source, float segmentInterval,
        float burstDuration, float scaleMultiplier, float headMultiplier, Color color,
        float headShakeDuration, float headShakeStrength)
    {
        if (source == null || burstDuration <= 0f || scaleMultiplier <= 0f) return false;
        var ordered = new List<Transform>();
        if (source.Tail != null) ordered.Add(source.Tail);
        if (source.BodySegments != null)
            for (int i = source.BodySegments.Length - 1; i >= 0; i--)
                if (source.BodySegments[i] != null) ordered.Add(source.BodySegments[i]);
        if (source.Head != null) ordered.Add(source.Head);
        if (ordered.Count == 0) return false;

        var root = new GameObject("Sandworm Tail To Head Death");
        SceneManager.MoveGameObjectToScene(root, source.gameObject.scene);
        root.AddComponent<CameraVisionStreamingExempt>();
        var sequence = root.AddComponent<SandwormDeathSequence>();
        var audio = source.GetComponent<SandwormAudio>();
        if (audio != null && audio.HasDeathAudio) sequence.deathAudio = audio.CaptureDeathPlayback();
        sequence.interval = Mathf.Max(.03f, segmentInterval);
        sequence.duration = burstDuration;
        sequence.tint = color;
        sequence.shakeDuration = Mathf.Clamp(headShakeDuration, 0f, .6f);
        sequence.shakeStrength = Mathf.Clamp(headShakeStrength, 0f, .06f);
        var body = new GameObject("Frozen Death Pose");
        body.transform.SetParent(root.transform, false);

        // Underground bodies use a group, while airborne/exposed parts have
        // individual orders. Keep both kinds of death pose in the same depth band.
        var originalGroup = source.GetComponent<SortingGroup>();
        if (originalGroup != null && originalGroup.enabled)
        {
            var group = body.AddComponent<SortingGroup>();
            group.sortingLayerID = originalGroup.sortingLayerID;
            group.sortingOrder = originalGroup.sortingOrder;
        }

        float largestScale = 0f;
        var copied = new HashSet<Transform>();
        foreach (Transform part in ordered)
        {
            if (!copied.Add(part)) continue;
            var visual = part.GetComponent<SandwormSegmentVisual>();
            var renderer = part.GetComponent<SpriteRenderer>();
            if (visual == null || renderer == null || renderer.sprite == null) continue;
            var snapshot = new GameObject(part.name + " Death Remains");
            snapshot.layer = part.gameObject.layer;
            snapshot.transform.SetParent(body.transform, false);
            snapshot.transform.SetPositionAndRotation(part.position, part.rotation);
            snapshot.transform.localScale = part.lossyScale;
            var copy = snapshot.AddComponent<SpriteRenderer>();
            copy.sharedMaterial = renderer.sharedMaterial;
            copy.color = renderer.color;
            copy.flipX = renderer.flipX;
            copy.flipY = renderer.flipY;
            copy.sortingLayerID = renderer.sortingLayerID;
            copy.sortingOrder = renderer.sortingOrder;
            copy.maskInteraction = renderer.maskInteraction;
            copy.enabled = renderer.enabled && part.gameObject.activeInHierarchy;

            // Register an actual visual user before destroying the source. Merely
            // copying the Sprite reference would let the last source component
            // release the shared runtime textures while this sequence still needs them.
            var copyVisual = snapshot.AddComponent<SandwormSegmentVisual>();
            copyVisual.Configure(visual.Kind, visual.Variant);
            copyVisual.SetUndergroundOverlay(visual.HasUndergroundOverlay);
            Vector3 size = renderer.sprite.bounds.size;
            Vector3 scale = part.lossyScale;
            float burstScale = Mathf.Max(Mathf.Abs(size.x * scale.x), Mathf.Abs(size.y * scale.y)) * scaleMultiplier;
            largestScale = Mathf.Max(largestScale, burstScale);
            sequence.parts.Add(new Part {
                Renderer = copy, Center = renderer.bounds.center, Scale = burstScale,
                IsHead = part == source.Head
            });
        }
        if (sequence.parts.Count == 0) { Destroy(root); return false; }
        for (int i = 0; i < sequence.parts.Count; i++)
        {
            Part part = sequence.parts[i];
            if (!part.IsHead) continue;
            part.Scale = largestScale * Mathf.Max(1.1f, headMultiplier);
            sequence.parts[i] = part;
        }
        sequence.BurstNext(); // No initial dead frame before the tail starts.
        return true;
    }

    private void Update() => Advance(Time.deltaTime);

    public void Advance(float deltaTime)
    {
        if (parts.Count == 0) return;
        age += Mathf.Max(0f, deltaTime);
        while (nextPart < parts.Count && age >= nextPart * interval) BurstNext();
        if (nextPart < parts.Count) return;
        // Child effects own their duration; wait for the last cross/fragments
        // instead of duplicating their timing constants or cutting off the finale.
        foreach (var burst in bursts) if (burst != null) return;
        Destroy(gameObject);
    }

    private void BurstNext()
    {
        Part part = parts[nextPart++];
        var burstRoot = new GameObject(part.IsHead ? "Head Final Explosion" : "Segment Death Explosion");
        burstRoot.transform.SetParent(transform, false);
        burstRoot.transform.position = part.Center;
        var burst = burstRoot.AddComponent<ZeldaDeathExplosionVisual>();
        // Death flashes are on the surface, including deaths underground. Keep
        // the usual world/vision pass and do not add damage or physical colliders.
        int sortingLayer = part.Renderer != null ? part.Renderer.sortingLayerID : 0;
        int order = part.Renderer != null ? Mathf.Max(4, part.Renderer.sortingOrder + 2) : 4;
        burst.Configure(duration, part.Scale, tint, sortingLayer, order);
        deathAudio.Play(gameObject.scene, part.Center, part.IsHead);
        bursts.Add(burst);
        if (part.IsHead)
        {
            headBurstTime = age;
            headBurstTransform = burstRoot.transform;
            headBurstBounds = new Bounds(part.Center, new Vector3(part.Scale * .7f, part.Scale * .7f, .1f));
        }
        if (part.Renderer != null)
        {
            part.Renderer.enabled = false;
            Destroy(part.Renderer.gameObject);
        }
    }

    protected override Vector2 GetOffset(Camera camera, CameraCircularVision vision, Plane[] cameraFrustum)
    {
        float elapsed = age - headBurstTime;
        if (headBurstTime < 0f || shakeDuration <= 0f || elapsed >= shakeDuration ||
            shakeStrength <= 0f || headBurstTransform == null || SoulMarkRuntime.IsVisionCovered ||
            (camera.cullingMask & (1 << headBurstTransform.gameObject.layer)) == 0 ||
            !GeometryUtility.TestPlanesAABB(cameraFrustum, headBurstBounds) ||
            (vision != null && vision.isActiveAndEnabled &&
                !vision.IsWorldBoundsVisible(headBurstBounds, headBurstTransform))) return Vector2.zero;

        float remaining = 1f - Mathf.Clamp01(elapsed / shakeDuration);
        float phase = elapsed * 16f * Mathf.PI * 2f;
        Vector2 wave = new Vector2(Mathf.Sin(phase + .45f), Mathf.Sin(phase * 1.31f + 1.1f));
        return Vector2.ClampMagnitude(wave, 1f) * (shakeStrength * camera.orthographicSize * remaining * remaining);
    }
}
