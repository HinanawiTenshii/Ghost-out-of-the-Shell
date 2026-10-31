using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class CharacterSprintVisualSettings
{
    public bool enabled = true;
    [Range(1f, 2f)] public float cadenceMultiplier = 1.5f;
    [Range(.04f, .4f)] public float transitionSeconds = .12f;
    [Range(0, 2)] public int bobPixels = 1;
    [Tooltip("每秒完整起伏次数，独立于脚步动画速度。")]
    [Range(.5f, 4f)] public float bobCyclesPerSecond = 2.5f;
    public bool dust = true;
    [Range(.3f, 3f)] public float dustStepDistance = 1.1f;
    [Range(1, 3)] public int particlesPerStep = 2;
    public Color dustColor = new Color(.69f, .7f, .72f, .48f);
}

/// <summary>Player-only visual layer. No physics, combat, sound/AI events or saved state.
/// Uses fixed-bounds cached pixel frames instead of moving the damage-feedback transform.</summary>
[DisallowMultipleComponent]
public sealed class CharacterSprintVisual : MonoBehaviour
{
    private sealed class Frames
    {
        public Texture2D texture;
        public Sprite low, high;
    }
    private readonly Dictionary<Sprite, Frames> frames = new Dictionary<Sprite, Frames>();
    private readonly HashSet<Sprite> unreadable = new HashSet<Sprite>();
    private ZeldaCharacterData character;
    private ZeldaFourWayMover mover;
    private ZeldaCharacterAiBase ai;
    private Collider2D body;
    private SpriteRenderer renderer;
    private CharacterSprintVisualSettings settings;
    private Sprite originalSprite, appliedSprite;
    private ParticleSystem dust;
    private Material dustMaterial;
    private Texture2D dustTexture;
    private float pendingDistance, dustDistance, lastMotionAt = -100f;
    private Vector2 lastDirection;
    private bool lastStepMoved, animating, movingThisFrame;
    private int prepareFrame = -1, clockFrame = -1, cachedBobPixels = -1;
    private float runBlend, walkPhase, bobPhase;
    private bool clockInitialized;
    public bool IsRunningVisual => animating;
    public float CadenceMultiplier => Mathf.Lerp(1f, Mathf.Clamp(settings.cadenceMultiplier, 1f, 2f), runBlend);

    public static bool Supports(ZeldaCharacterData data) => data != null &&
        !(data is GhostZeldaCharacterData) && !(data is BehemothZeldaCharacterData) &&
        !(data is SandwormZeldaCharacterData) && !(data is TurretZeldaCharacterData) &&
        !(data is OuterRealmVisitorZeldaCharacterData);

    public void Initialize(ZeldaCharacterData data, ZeldaFourWayMover movement, CharacterSprintVisualSettings configuration)
    {
        character = data; mover = movement; settings = configuration;
        ai = GetComponent<ZeldaCharacterAiBase>(); body = GetComponent<Collider2D>();
    }

    // Called with accepted mover displacement, never transform deltas (teleports/knockback).
    public void RecordMovement(Vector2 displacement)
    {
        float distance = displacement.magnitude;
        lastStepMoved = distance > .0001f && !float.IsNaN(distance) && !float.IsInfinity(distance);
        lastMotionAt = Time.time;
        if (!lastStepMoved) return;
        lastDirection = displacement / distance;
        if (mover != null && mover.isActiveAndEnabled && mover.IsSprinting)
            pendingDistance += Mathf.Min(distance, 1f);
    }

    private bool CanAnimate => isActiveAndEnabled && settings != null && settings.enabled && Supports(character) &&
        character.isActiveAndEnabled && !character.IsDead && !character.IsGhostLike &&
        mover != null && mover.isActiveAndEnabled && !mover.IsAttacking && !mover.IsPossessionInProgress &&
        !mover.IsWearingCardboardBox && (ai == null || !ai.IsStunned) &&
        !DocumentReader.IsInputBlocked && !ClockworkPuppetRuntime.BlocksCharacterInput;

    public bool Prepare(SpriteRenderer source, bool moving, bool attacking)
    {
        RestoreSprite();
        renderer = source;
        if (attacking || !CanAnimate)
        { ResetVisual(); return moving; }
        if (prepareFrame == Time.frameCount) return movingThisFrame;
        prepareFrame = Time.frameCount;
        bool actualMovement = moving && lastStepMoved && Time.time - lastMotionAt <= Mathf.Max(.08f, Time.fixedDeltaTime * 2.5f);
        bool running = mover.IsSprinting && actualMovement;
        movingThisFrame = moving && (!mover.IsSprinting || actualMovement);
        if (!moving || (mover.IsSprinting && !actualMovement)) runBlend = 0f;
        else runBlend = Mathf.MoveTowards(runBlend, running ? 1f : 0f,
            Time.deltaTime / Mathf.Max(.04f, settings.transitionSeconds));
        animating = actualMovement && runBlend > .001f;
        // One slow rise/landing cycle, independent of the much faster foot-frame clock.
        // Begin grounded and preserve the phase while easing between running and walking.
        if (animating)
            bobPhase = Mathf.Repeat(bobPhase + Time.deltaTime * Mathf.Clamp(settings.bobCyclesPerSecond, .5f, 4f), 1f);
        else bobPhase = 0f;
        if (!running) { pendingDistance = dustDistance = 0f; }
        if (Time.timeScale > 0 && running && settings.dust && source != null && source.enabled)
        {
            // Veil must not leave an opaque dust trail exposing its wearer.
            if (!CharacterPotionBuffs.Has(character, AlchemyPotionKind.Veil))
            {
                dustDistance += pendingDistance;
                float spacing = Mathf.Max(.3f, settings.dustStepDistance) * SizeFactor();
                int bursts = 0;
                while (dustDistance >= spacing && bursts++ < 3)
                { dustDistance -= spacing; EmitDust(source); }
                dustDistance = Mathf.Min(dustDistance, spacing);
            }
            else dustDistance = 0f;
        }
        pendingDistance = 0f;
        if (dust != null && (!settings.dust || CharacterPotionBuffs.Has(character, AlchemyPotionKind.Veil)))
            dust.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        return movingThisFrame;
    }

    // Both authored walkers and generated foot frames use one continuous phase.
    public int WalkFrame(float framesPerSecond)
    {
        if (!clockInitialized)
        { walkPhase = Mathf.Repeat(Time.time * framesPerSecond, 2f); clockInitialized = true; clockFrame = Time.frameCount; }
        if (clockFrame != Time.frameCount)
        {
            clockFrame = Time.frameCount;
            if (movingThisFrame) walkPhase = Mathf.Repeat(walkPhase + Time.deltaTime * framesPerSecond * CadenceMultiplier, 2f);
        }
        return Mathf.FloorToInt(walkPhase) % 2;
    }

    public void Apply(SpriteRenderer source)
    {
        if (!animating || source == null || source.sprite == null || settings.bobPixels <= 0) return;
        int pixels = Mathf.Clamp(settings.bobPixels, 0, 2);
        if (cachedBobPixels != pixels || frames.Count >= 64)
        { ReleaseFrames(); cachedBobPixels = pixels; }
        Sprite original = source.sprite;
        if (!frames.TryGetValue(original, out Frames pair))
        {
            if (unreadable.Contains(original)) return;
            if (!original.texture.isReadable || original.packed)
            { unreadable.Add(original); return; }
            pair = BuildFrames(original, pixels);
            frames.Add(original, pair);
        }
        originalSprite = original;
        // Ease the height in/out with the run transition, retaining crisp whole pixels.
        float lift = (1f - Mathf.Cos(bobPhase * Mathf.PI * 2f)) * .5f * runBlend;
        appliedSprite = lift >= .5f ? pair.high : pair.low;
        source.sprite = appliedSprite;
        renderer = source;
    }

    private static Frames BuildFrames(Sprite original, int lift)
    {
        Rect rect = original.rect;
        int w = Mathf.RoundToInt(rect.width), h = Mathf.RoundToInt(rect.height), paddedHeight = h + lift;
        var input = original.texture.GetPixels(Mathf.RoundToInt(rect.x), Mathf.RoundToInt(rect.y), w, h);
        var pixels = new Color[w * 2 * paddedHeight];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
        {
            pixels[y * w * 2 + x] = input[y * w + x];
            pixels[(y + lift) * w * 2 + w + x] = input[y * w + x];
        }
        var texture = new Texture2D(w * 2, paddedHeight, TextureFormat.RGBA32, false) {
            name = original.name + " Run", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave };
        texture.SetPixels(pixels); texture.Apply(false, false);
        var pivot = new Vector2(original.pivot.x / w, original.pivot.y / paddedHeight);
        var pair = new Frames { texture = texture };
        pair.low = Sprite.Create(texture, new Rect(0,0,w,paddedHeight), pivot, original.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        pair.high = Sprite.Create(texture, new Rect(w,0,w,paddedHeight), pivot, original.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        pair.low.name = original.name + " Run Land"; pair.high.name = original.name + " Run Rise";
        pair.low.hideFlags = pair.high.hideFlags = HideFlags.HideAndDontSave;
        // Identical bounds/pivot in both frames: nameplates/attribute windows do not bounce.
        return pair;
    }

    private float SizeFactor() => body != null ? Mathf.Clamp(body.bounds.size.x / .6f, .75f, 1.6f) : 1f;
    private void EmitDust(SpriteRenderer source)
    {
        if (dust == null) CreateDust(source);
        if (!dust.isPlaying) dust.Play(false);
        var dustRenderer = dust.GetComponent<ParticleSystemRenderer>();
        dustRenderer.sortingLayerID = source.sortingLayerID; dustRenderer.sortingOrder = source.sortingOrder - 1;
        float scale = SizeFactor();
        Vector3 foot = body != null ? new Vector3(body.bounds.center.x, body.bounds.min.y + .04f, transform.position.z) : transform.position;
        Vector2 side = new Vector2(-lastDirection.y, lastDirection.x);
        for (int i = 0; i < Mathf.Clamp(settings.particlesPerStep,1,3); i++)
        {
            Vector2 drift = -lastDirection * .4f + side * UnityEngine.Random.Range(-.3f,.3f);
            dust.Emit(new ParticleSystem.EmitParams {
                position = foot - (Vector3)lastDirection * .12f * scale + (Vector3)side * (i % 2 == 0 ? -.1f : .1f) * scale,
                velocity = drift * scale, startSize = UnityEngine.Random.Range(.055f,.09f) * scale,
                startLifetime = UnityEngine.Random.Range(.22f,.33f), startColor = settings.dustColor,
                rotation = 0f
            }, 1);
        }
    }

    private void CreateDust(SpriteRenderer source)
    {
        dustTexture = new Texture2D(4,4,TextureFormat.RGBA32,false) {
            name = "Run Dust Pixel", filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
        var pixels = new Color[16];
        for (int y = 0; y < 4; y++) for (int x = 0; x < 4; x++)
            pixels[y*4+x] = (x == 0 || x == 3) && (y == 0 || y == 3) ? Color.clear : Color.white;
        dustTexture.SetPixels(pixels); dustTexture.Apply(false,true);
        dustMaterial = new Material(Shader.Find("Sprites/Default")) {
            name = "Run Dust", mainTexture = dustTexture, hideFlags = HideFlags.HideAndDontSave };
        var go = new GameObject("Sprint Dust") { layer = gameObject.layer, hideFlags = HideFlags.DontSave };
        go.SetActive(false); go.transform.SetParent(transform,false);
        dust = go.AddComponent<ParticleSystem>();
        var main = dust.main;
        main.playOnAwake = false; main.loop = true; main.startSpeed = 0;
        main.maxParticles = 24; main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Shape; main.useUnscaledTime = false;
        var emission = dust.emission; emission.enabled = false;
        var shape = dust.shape; shape.enabled = false;
        var collision = dust.collision; collision.enabled = false;
        var size = dust.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f,AnimationCurve.Linear(0,1,1,.3f));
        var gradient = new Gradient();
        gradient.SetKeys(new[] {new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
            new[] {new GradientAlphaKey(1,0),new GradientAlphaKey(0,1)});
        var color = dust.colorOverLifetime; color.enabled = true; color.color = gradient;
        var draw = go.GetComponent<ParticleSystemRenderer>(); draw.sharedMaterial = dustMaterial;
        draw.sortingLayerID = source.sortingLayerID; draw.sortingOrder = source.sortingOrder - 1;
        draw.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; draw.receiveShadows = false;
        go.SetActive(true);
    }

    private void RestoreSprite()
    {
        if (renderer != null && renderer.sprite == appliedSprite && originalSprite != null) renderer.sprite = originalSprite;
        appliedSprite = originalSprite = null;
    }
    public void ResetVisual()
    {
        RestoreSprite();
        animating = movingThisFrame = lastStepMoved = false;
        pendingDistance = dustDistance = runBlend = bobPhase = 0;
        lastMotionAt = -100; prepareFrame = -1;
        // Keep the local phase while stopping; switching control returns to the caller's clock.
        if (dust != null) dust.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
    }
    public bool OwnsPlayerClock => CanAnimate;
    private void OnDisable() => ResetVisual();
    private void OnDestroy()
    {
        RestoreSprite(); ReleaseFrames();
        if (dust != null) Destroy(dust.gameObject);
        if (dustMaterial != null) Destroy(dustMaterial);
        if (dustTexture != null) Destroy(dustTexture);
    }
    private void ReleaseFrames()
    {
        RestoreSprite();
        foreach (var pair in frames.Values)
        { Destroy(pair.low); Destroy(pair.high); Destroy(pair.texture); }
        frames.Clear(); unreadable.Clear();
    }
}
