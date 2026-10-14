using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Presentation and periodic perception effects only. No visibility query, map discovery,
/// collision layer, permission check or interaction distance is overridden here.
/// </summary>
[DisallowMultipleComponent]
public sealed class PotionPerceptionEffects : MonoBehaviour
{
    private sealed class Target
    {
        public Component root;
        public SpriteRenderer sprite;
        public Collider2D collider;
        public bool character;
    }

    private const float NoiseRadius = 8f;
    private const int MaximumOutlines = 128;
    private const float VeilOpacity = .45f;
    private static readonly int SpriteTintId = Shader.PropertyToID("_Color");
    private static readonly int SpriteTextureId = Shader.PropertyToID("_MainTex");
    private static readonly int SpriteAlphaTextureId = Shader.PropertyToID("_AlphaTex");
    private static readonly int SpriteExternalAlphaId = Shader.PropertyToID("_EnableExternalAlpha");
    private static readonly int SpriteRendererColorId = Shader.PropertyToID("_RendererColor");
    private static readonly int SpriteFlipId = Shader.PropertyToID("_Flip");
    private SpriteRenderer veilRenderer;
    private Material veilMaterial;
    private MaterialPropertyBlock veilOriginalProperties, veilProperties;
    private Color veilBaseTint;
    private ZeldaCharacterData character;
    private float nextNoiseTime = -1f, nextTargetRefresh, nextRetentionRefresh;
    private GameObject retentionRoot, outlineRoot;
    private CameraVisionStreamingRegion retention;
    private Mesh outlineMesh;
    private MeshRenderer outlineRenderer;
    private Material outlineMaterial;
    private readonly List<Collider2D> overlaps = new List<Collider2D>(128);
    private readonly HashSet<Component> uniqueTargets = new HashSet<Component>();
    private readonly List<Target> targets = new List<Target>(32);
    private readonly List<Vector3> vertices = new List<Vector3>(2048);
    private readonly List<int> indices = new List<int>(3072);
    private readonly List<Color> colors = new List<Color>(2048);

    public static void Ensure(ZeldaCharacterData owner)
    {
        if (owner == null) return;
        var effects = owner.GetComponent<PotionPerceptionEffects>();
        if (effects == null) effects = owner.gameObject.AddComponent<PotionPerceptionEffects>();
        effects.character = owner;
        effects.enabled = true;
        effects.UpdateVeilVisual();
    }

    private void Awake() { character = GetComponent<ZeldaCharacterData>(); }
    private void LateUpdate()
    {
        UpdateVeilVisual();
        if (character == null || character.IsDead || character.IsGhostLike || GameSaveSystem.IsLoading)
        {
            ReleaseOutlines(); ReleaseNoise(); return;
        }
        UpdateClamor();
        var player = ZeldaRuntimeRegistry.GetControlledMover();
        bool canReveal = player != null && player.CharacterData == character &&
            CharacterPotionBuffs.Has(character, AlchemyPotionKind.Sight) &&
            !ClockworkPuppetRuntime.IsRemoteControlActive && !SoulMarkRuntime.IsVisionCovered;
        var camera = Camera.main;
        var vision = canReveal && camera != null ? camera.GetComponent<CameraCircularVision>() : null;
        if (vision == null || !vision.isActiveAndEnabled)
        {
            ReleaseOutlines(); return;
        }
        float radius = vision.VisionRadius;
        if (Time.time >= nextTargetRefresh || outlineRoot == null)
        {
            RefreshTargets(radius);
            nextTargetRefresh = Time.time + .25f;
        }
        DrawOutlines(vision, radius);
    }

    private void UpdateVeilVisual()
    {
        bool active = character != null && !character.IsDead && !character.IsGhostLike &&
            CharacterPotionBuffs.Has(character, AlchemyPotionKind.Veil);
        if (!active) { RestoreVeilVisual(); return; }

        // Only the body becomes translucent, not speech/skill icons, vision cones,
        // attack objects or other children. The mover can migrate it from root to Visual.
        var visual = transform.Find("Visual");
        var body = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
        if (body == null) body = GetComponent<SpriteRenderer>();
        var material = body != null ? body.sharedMaterial : null;
        if (body == null || material == null || !material.HasProperty(SpriteTintId))
        { RestoreVeilVisual(); return; }

        if (body != veilRenderer || material != veilMaterial)
        {
            RestoreVeilVisual();
            veilRenderer = body; veilMaterial = material;
            if (veilOriginalProperties == null) veilOriginalProperties = new MaterialPropertyBlock();
            if (veilProperties == null) veilProperties = new MaterialPropertyBlock();
            body.GetPropertyBlock(veilOriginalProperties);
            veilBaseTint = veilOriginalProperties.HasProperty(SpriteTintId)
                ? veilOriginalProperties.GetColor(SpriteTintId) : material.GetColor(SpriteTintId);
        }
        // A shader tint multiplier leaves SpriteRenderer.color to the existing
        // facing/walk/damage/healing code. Reapplying never compounds the opacity.
        body.GetPropertyBlock(veilProperties);
        Color tint = veilBaseTint;
        tint.a *= VeilOpacity;
        veilProperties.SetColor(SpriteTintId, tint);
        body.SetPropertyBlock(veilProperties);
    }

    private void RestoreVeilVisual()
    {
        if (veilRenderer != null)
        {
            // SpriteRenderer also puts its *current* texture/pose data in the block.
            // Restoring a whole old snapshot without merging these would resurrect
            // the facing texture captured when Veil began, even though sprite changed.
            veilRenderer.GetPropertyBlock(veilProperties);
            if (veilProperties.HasProperty(SpriteTextureId))
                veilOriginalProperties.SetTexture(SpriteTextureId, veilProperties.GetTexture(SpriteTextureId));
            if (veilProperties.HasProperty(SpriteAlphaTextureId))
                veilOriginalProperties.SetTexture(SpriteAlphaTextureId, veilProperties.GetTexture(SpriteAlphaTextureId));
            if (veilProperties.HasProperty(SpriteExternalAlphaId))
                veilOriginalProperties.SetFloat(SpriteExternalAlphaId, veilProperties.GetFloat(SpriteExternalAlphaId));
            if (veilProperties.HasProperty(SpriteRendererColorId))
                veilOriginalProperties.SetVector(SpriteRendererColorId, veilProperties.GetVector(SpriteRendererColorId));
            if (veilProperties.HasProperty(SpriteFlipId))
                veilOriginalProperties.SetVector(SpriteFlipId, veilProperties.GetVector(SpriteFlipId));
            veilRenderer.SetPropertyBlock(veilOriginalProperties);
        }
        veilRenderer = null; veilMaterial = null;
    }

    private void UpdateClamor()
    {
        if (!CharacterPotionBuffs.Has(character, AlchemyPotionKind.Clamor))
        {
            ReleaseNoise(); return;
        }
        if (Time.timeScale <= 0f) return;
        if (nextNoiseTime < 0f) nextNoiseTime = Time.time + 3f;
        if (retention == null)
        {
            retentionRoot = new GameObject("Potion Noise Simulation Region") { hideFlags = HideFlags.HideAndDontSave };
            retentionRoot.transform.SetParent(transform, false);
            retention = retentionRoot.AddComponent<CameraVisionStreamingRegion>();
        }
        if (Time.time >= nextRetentionRefresh)
        {
            // Keep already-streamed-out listeners within hearing range available.
            // This simulates noise only; it does not reveal them to the player.
            retention.Configure(NoiseRadius + .5f);
            nextRetentionRefresh = Time.time + .25f;
        }
        if (Time.time < nextNoiseTime) return;
        nextNoiseTime = Time.time + 3f;
        EmitClamor();
    }

    private void EmitClamor()
    {
        if (character == null) return;
        var scene = ZeldaRuntimeRegistry.GetGameplayScene(gameObject);
        Vector2 origin = transform.position;
        // Snapshot: investigation callbacks may register or disable other characters.
        var listeners = new List<ZeldaCharacterAiBase>(ZeldaRuntimeRegistry.AiCharacters);
        foreach (var ai in listeners)
        {
            if (ai == null || !ai.isActiveAndEnabled || ai.gameObject == gameObject ||
                ai is AutomatonCharacterAi || ai is SkeletonCharacterAi ||
                ai.CurrentState == ZeldaAiState.Hostile || ai.IsStunned ||
                ZeldaRuntimeRegistry.GetGameplayScene(ai.gameObject) != scene ||
                ((Vector2)ai.transform.position - origin).sqrMagnitude > NoiseRadius * NoiseRadius) continue;
            var mover = ai.GetComponent<ZeldaFourWayMover>();
            var data = ai.GetComponent<ZeldaCharacterData>();
            if (data == null || data.IsDead || data.IsGhostLike ||
                (mover != null && mover.isActiveAndEnabled)) continue;
            ai.InvestigatePosition(origin);
        }
        var pulse = new GameObject("Clamor Potion Pulse") { hideFlags = HideFlags.DontSave };
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(pulse, scene);
        pulse.transform.position = transform.position;
        pulse.AddComponent<ZeldaInvestigationPulse>().Configure(NoiseRadius, .65f,
            new Color(.48f, .8f, 1f, .32f), 0, 3);
    }

    private void RefreshTargets(float radius)
    {
        targets.Clear(); uniqueTargets.Clear(); overlaps.Clear();
        var scene = ZeldaRuntimeRegistry.GetGameplayScene(gameObject);
        Physics2D.SyncTransforms();
        Physics2D.OverlapCircle(transform.position, radius,
            new ContactFilter2D { useTriggers = true }, overlaps);
        foreach (var hit in overlaps)
        {
            if (hit == null || !hit.enabled || !hit.gameObject.activeInHierarchy ||
                ZeldaRuntimeRegistry.GetGameplayScene(hit.gameObject) != scene) continue;
            Component root = hit.GetComponentInParent<ZeldaCharacterData>();
            bool isCharacter = root != null;
            if (root == null) root = FindMechanism(hit);
            if (root == null || root.gameObject == gameObject || !uniqueTargets.Add(root)) continue;
            if (isCharacter && (((ZeldaCharacterData)root).IsDead || ((ZeldaCharacterData)root).IsGhostLike)) continue;
            var behaviour = root as Behaviour;
            if (behaviour != null && !behaviour.isActiveAndEnabled) continue;
            var sprite = root.GetComponent<SpriteRenderer>();
            var collider = root.GetComponent<Collider2D>();
            targets.Add(new Target { root = root, sprite = sprite,
                collider = collider != null ? collider : hit, character = isCharacter });
            if (targets.Count >= MaximumOutlines) break;
        }
    }

    private static Component FindMechanism(Collider2D hit)
    {
        Component result = hit.GetComponentInParent<LeverData>();
        if (result == null) result = hit.GetComponentInParent<WaterwayGateController>();
        if (result == null) result = hit.GetComponentInParent<BatterySocket>();
        if (result == null) result = hit.GetComponentInParent<ElectricGatePillar>();
        if (result == null) result = hit.GetComponentInParent<AlarmDevice>();
        if (result == null) result = hit.GetComponentInParent<FortressBallista>();
        if (result == null) result = hit.GetComponentInParent<LiquidInjector>();
        if (result == null) result = hit.GetComponentInParent<AlchemyCauldron>();
        if (result == null) result = hit.GetComponentInParent<SpecificItemSubmissionStation>();
        if (result == null) result = hit.GetComponentInParent<DoorHingeInteraction>();
        if (result == null) result = hit.GetComponentInParent<SignpostInteraction>();
        if (result == null) result = hit.GetComponentInParent<DocumentReader>();
        return result;
    }

    private void DrawOutlines(CameraCircularVision vision, float radius)
    {
        EnsureOutlineMesh();
        if (outlineMesh == null) return;
        vertices.Clear(); indices.Clear(); colors.Clear();
        Vector2 origin = transform.position;
        foreach (var target in targets)
        {
            if (target.root == null || !target.root.gameObject.activeInHierarchy) continue;
            var data = target.root as ZeldaCharacterData;
            if (data != null && (data.IsDead || data.IsGhostLike)) continue;
            Bounds bounds;
            if (target.sprite != null && target.sprite.enabled && target.sprite.sprite != null)
                bounds = target.sprite.bounds;
            else if (target.collider != null && target.collider.enabled) bounds = target.collider.bounds;
            else continue;
            if (((Vector2)bounds.center - origin).sqrMagnitude > radius * radius ||
                vision.IsWorldPositionVisible(bounds.center, target.root.transform)) continue;
            // Ethereal, unfilled contours identify occupants and mechanisms, never
            // a duplicated full-color sprite or a hole in the vision darkness.
            Color tint = target.character ? new Color(.58f,.92f,1f,.85f) : new Color(.91f,.67f,1f,.75f);
            Vector2 min = (Vector2)bounds.min - Vector2.one * .025f;
            Vector2 max = (Vector2)bounds.max + Vector2.one * .025f;
            float z = bounds.center.z - .02f;
            AddLine(new Vector3(min.x,min.y,z),new Vector3(max.x,min.y,z),tint);
            AddLine(new Vector3(max.x,min.y,z),new Vector3(max.x,max.y,z),tint);
            AddLine(new Vector3(max.x,max.y,z),new Vector3(min.x,max.y,z),tint);
            AddLine(new Vector3(min.x,max.y,z),new Vector3(min.x,min.y,z),tint);
        }
        outlineMesh.Clear(); outlineMesh.SetVertices(vertices); outlineMesh.SetTriangles(indices,0);
        outlineMesh.SetColors(colors); outlineMesh.RecalculateBounds();
        outlineRenderer.enabled = vertices.Count > 0;
    }

    private void EnsureOutlineMesh()
    {
        if (outlineRoot != null) return;
        Shader shader = Shader.Find("Sprites/Default");
        int uiLayer = LayerMask.NameToLayer("UI");
        if (shader == null || uiLayer < 0) return;
        outlineRoot = new GameObject("Potion Sight Contours", typeof(MeshFilter), typeof(MeshRenderer))
            { hideFlags = HideFlags.HideAndDontSave, layer = uiLayer };
        outlineRoot.transform.SetParent(transform,false);
        outlineMesh = new Mesh { name = "Potion Sight Contours", hideFlags = HideFlags.HideAndDontSave };
        outlineMesh.MarkDynamic(); outlineRoot.GetComponent<MeshFilter>().sharedMesh = outlineMesh;
        outlineMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        outlineRenderer = outlineRoot.GetComponent<MeshRenderer>();
        outlineRenderer.sharedMaterial = outlineMaterial;
        outlineRenderer.sortingOrder = 30010;
        outlineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        outlineRenderer.receiveShadows = false;
    }

    private void AddLine(Vector3 a, Vector3 b, Color color)
    {
        Vector2 delta = b - a;
        if (delta.sqrMagnitude < .000001f) return;
        Vector3 normal = new Vector3(-delta.y,delta.x,0).normalized * .018f;
        int start = vertices.Count;
        vertices.Add(transform.InverseTransformPoint(a-normal));
        vertices.Add(transform.InverseTransformPoint(a+normal));
        vertices.Add(transform.InverseTransformPoint(b+normal));
        vertices.Add(transform.InverseTransformPoint(b-normal));
        for (int i=0;i<4;i++) colors.Add(color);
        indices.Add(start); indices.Add(start+1); indices.Add(start+2);
        indices.Add(start); indices.Add(start+2); indices.Add(start+3);
    }

    private void ReleaseNoise()
    {
        nextNoiseTime = -1f; nextRetentionRefresh = 0f;
        if (retentionRoot != null) { retentionRoot.SetActive(false); Release(retentionRoot); }
        retentionRoot = null; retention = null;
    }
    private void ReleaseOutlines()
    {
        if (outlineRoot != null) { outlineRoot.SetActive(false); Release(outlineRoot); }
        Release(outlineMesh); Release(outlineMaterial);
        outlineRoot = null; outlineMesh = null; outlineRenderer = null; outlineMaterial = null;
        targets.Clear(); nextTargetRefresh = 0f;
    }
    private static void Release(Object value)
    {
        if (value == null) return;
        if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
    }
    private void OnDisable() { RestoreVeilVisual(); ReleaseOutlines(); ReleaseNoise(); }
    private void OnDestroy() { RestoreVeilVisual(); ReleaseOutlines(); ReleaseNoise(); }
}
