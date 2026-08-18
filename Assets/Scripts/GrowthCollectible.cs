using UnityEngine;

/// <summary>
/// A world collectible that adds directly to the player's persistent growth currency.
/// It deliberately bypasses PersistentInventory.
/// </summary>
[RequireComponent(typeof(CircleCollider2D))]
public sealed class GrowthCollectible : MonoBehaviour
{
    [Header("Collection")]
    [SerializeField, Min(1)] private int collectibleAmount = 1;
    [SerializeField, Min(0.1f)] private float pickupDistance = 1.35f;
    [SerializeField] private KeyCode pickupKey = KeyCode.E;
    [SerializeField] private Vector2 promptOffset = new Vector2(0f, 1.25f);

    [Header("Appearance")]
    [SerializeField, Range(4, 24)] private int orbitPointCount = 16;
    [SerializeField, Min(0.1f)] private float orbitRadius = 0.9f;
    [SerializeField, Min(0.02f)] private float orbitPointSize = 0.14f;
    [SerializeField] private float orbitRotationSpeed = 12f;
    [SerializeField, Min(0.01f)] private float tetrahedronLineWidth = 0.045f;
    [SerializeField, Min(0.1f)] private float glowSize = 1.8f;
    [SerializeField, Range(0f, 1f)] private float glowOpacity = 0.42f;
    [SerializeField, Min(0.01f)] private float glowPulseSpeed = 1.35f;
    [SerializeField, Range(0f, 0.5f)] private float glowPulseAmount = 0.12f;
    [SerializeField] private int baseSortingOrder = 4;

    [Header("Pickup Burst")]
    [SerializeField, Range(8, 64)] private int pickupBurstParticleCount = 24;
    [SerializeField, Min(0.1f)] private float pickupBurstLifetime = 0.65f;
    [SerializeField, Min(0.1f)] private float pickupBurstSpeed = 1.25f;

    private static Sprite circleSprite;
    private static Sprite glowSprite;
    private static Material lineMaterial;
    private static Material particleMaterial;

    private Transform orbitRoot;
    private Transform glowTransform;
    private GameObject promptObject;
    private TextMesh promptText;
    private Font promptFont;
    private Material promptMaterial;

    private void Awake()
    {
        CircleCollider2D trigger = GetComponent<CircleCollider2D>();
        trigger.isTrigger = true;
        trigger.radius = Mathf.Max(orbitRadius, pickupDistance * 0.65f);
        BuildVisual();
    }

    private void Update()
    {
        float deltaTime = Time.unscaledDeltaTime;
        if (orbitRoot != null)
        {
            orbitRoot.Rotate(0f, 0f, orbitRotationSpeed * deltaTime);
        }

        if (glowTransform != null)
        {
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * glowPulseSpeed) * glowPulseAmount;
            glowTransform.localScale = Vector3.one * (glowSize * pulse);
        }

        ZeldaFourWayMover controlledMover = FindControlledMover();
        bool canCollect = controlledMover != null &&
            !PickupItemBase.IsGhostControlledMover(controlledMover) &&
            Vector2.Distance(transform.position, controlledMover.transform.position) <= pickupDistance;
        if (canCollect)
        {
            ZeldaInteractionArbiter.OfferInteraction(
                this,
                controlledMover,
                pickupKey,
                transform.position,
                SetPromptFromArbiter);
        }
        else
        {
            SetPromptVisible(false, null);
        }

        if (!DocumentReader.IsInputBlocked &&
            canCollect &&
            Input.GetKeyDown(pickupKey))
        {
            ZeldaInteractionArbiter.Submit(
                this,
                controlledMover,
                pickupKey,
                transform.position,
                CollectFromInteraction);
        }
    }

    private void CollectFromInteraction()
    {
        TryCollect();
    }

    private void SetPromptFromArbiter(bool visible)
    {
        SetPromptVisible(visible, visible ? FindControlledMover() : null);
    }

    public bool TryCollect()
    {
        ZeldaFourWayMover controlledMover = FindControlledMover();
        if (PickupItemBase.IsGhostControlledMover(controlledMover))
        {
            return false;
        }

        PlayerGrowthAttributes growth = PlayerGrowthAttributes.Instance;
        if (growth == null)
        {
            Debug.LogWarning(
                "GrowthCollectible requires one PlayerGrowthAttributes singleton in the scene.",
                this);
            return false;
        }

        growth.AddCollectibles(Mathf.Max(1, collectibleAmount));
        PlayPickupBurst();
        Destroy(gameObject);
        return true;
    }

    private void PlayPickupBurst()
    {
        EnsureSharedVisualAssets();
        GameObject burstObject = new GameObject(name + " Pickup Light Burst");
        burstObject.transform.position = transform.position;
        burstObject.SetActive(false);

        ParticleSystem particles = burstObject.AddComponent<ParticleSystem>();
        particles.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = particles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 0.1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(
            pickupBurstLifetime * 0.75f,
            pickupBurstLifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(
            pickupBurstSpeed * 0.7f,
            pickupBurstSpeed * 1.25f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
        main.startColor = Color.white;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0f;
        main.maxParticles = Mathf.Max(8, pickupBurstParticleCount);
        main.stopAction = ParticleSystemStopAction.Destroy;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(
                0f,
                (short)Mathf.Clamp(pickupBurstParticleCount, 8, 64))
        });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.18f;
        shape.radiusThickness = 1f;
        shape.arc = 360f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime =
            particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient fadeGradient = new Gradient();
        fadeGradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.85f, 0.35f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = fadeGradient;

        ParticleSystemRenderer particleRenderer =
            burstObject.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.sortingOrder = baseSortingOrder + 2;
        particleRenderer.sharedMaterial = particleMaterial;

        burstObject.SetActive(true);
        particles.Play();
    }

    private void BuildVisual()
    {
        EnsureSharedVisualAssets();
        CreateGlow();
        CreateTetrahedron();
        CreateOrbitPoints();
    }

    private void CreateGlow()
    {
        GameObject glowObject = new GameObject("Breathing Glow");
        glowObject.transform.SetParent(transform, false);
        glowTransform = glowObject.transform;
        SpriteRenderer renderer = glowObject.AddComponent<SpriteRenderer>();
        renderer.sprite = glowSprite;
        renderer.color = new Color(1f, 1f, 1f, glowOpacity);
        renderer.sortingOrder = baseSortingOrder - 2;
        glowTransform.localScale = Vector3.one * glowSize;
    }

    private void CreateTetrahedron()
    {
        Transform edgeRoot = new GameObject("White Tetrahedron").transform;
        edgeRoot.SetParent(transform, false);

        Vector3[] vertices =
        {
            new Vector3(-0.12f, 0.62f, 0f),
            new Vector3(-0.55f, -0.34f, 0f),
            new Vector3(0.58f, -0.08f, 0f),
            new Vector3(0.12f, -0.55f, 0f)
        };
        int[,] edges =
        {
            { 0, 1 }, { 0, 2 }, { 0, 3 },
            { 1, 2 }, { 1, 3 }, { 2, 3 }
        };

        for (int i = 0; i < edges.GetLength(0); i++)
        {
            GameObject edgeObject = new GameObject("Edge " + (i + 1));
            edgeObject.transform.SetParent(edgeRoot, false);
            LineRenderer line = edgeObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = 2;
            line.SetPosition(0, vertices[edges[i, 0]]);
            line.SetPosition(1, vertices[edges[i, 1]]);
            line.startWidth = tetrahedronLineWidth;
            line.endWidth = tetrahedronLineWidth;
            line.startColor = Color.white;
            line.endColor = Color.white;
            line.numCapVertices = 4;
            line.numCornerVertices = 4;
            line.sharedMaterial = lineMaterial;
            line.sortingOrder = baseSortingOrder;
        }
    }

    private void CreateOrbitPoints()
    {
        orbitRoot = new GameObject("Rotating Orbit Points").transform;
        orbitRoot.SetParent(transform, false);
        int count = Mathf.Clamp(orbitPointCount, 4, 24);
        for (int i = 0; i < count; i++)
        {
            float angle = i * Mathf.PI * 2f / count;
            GameObject pointObject = new GameObject("Orbit Point " + (i + 1));
            pointObject.transform.SetParent(orbitRoot, false);
            pointObject.transform.localPosition = new Vector3(
                Mathf.Cos(angle) * orbitRadius,
                Mathf.Sin(angle) * orbitRadius,
                0f);
            pointObject.transform.localScale = Vector3.one * orbitPointSize;
            SpriteRenderer renderer = pointObject.AddComponent<SpriteRenderer>();
            renderer.sprite = circleSprite;
            renderer.color = Color.white;
            renderer.sortingOrder = baseSortingOrder + 1;
        }
    }

    private void SetPromptVisible(bool visible, ZeldaFourWayMover mover)
    {
        if (visible)
        {
            EnsurePrompt();
        }

        if (promptObject == null)
            return;

        if (visible && mover != null)
        {
            promptObject.transform.position = mover.transform.position + (Vector3)promptOffset;
            promptObject.transform.rotation = Quaternion.identity;
        }

        if (promptObject.activeSelf != visible)
        {
            promptObject.SetActive(visible);
        }
    }

    private void EnsurePrompt()
    {
        if (promptObject != null)
            return;

        ZeldaHealthHeartsUI ui = ZeldaHealthHeartsUI.Instance;
        promptFont = ui != null ? ui.PermissionLabelFont : null;
        if (promptFont == null)
            return;

        const string message = "按[E]拾取";
        promptFont.RequestCharactersInTexture(message, 72, FontStyle.Normal);
        promptObject = new GameObject(name + " Growth Collectible Prompt");
        promptText = promptObject.AddComponent<TextMesh>();
        promptText.text = message;
        promptText.font = promptFont;
        promptText.fontSize = 72;
        promptText.characterSize = 0.035f;
        promptText.anchor = TextAnchor.MiddleCenter;
        promptText.alignment = TextAlignment.Center;
        promptText.color = ZeldaUiPalette.Primary;

        MeshRenderer renderer = promptObject.GetComponent<MeshRenderer>();
        renderer.sortingOrder = short.MaxValue - 2;
        promptMaterial = new Material(promptFont.material)
        {
            name = name + " Growth Collectible Prompt Material",
            hideFlags = HideFlags.HideAndDontSave
        };
        promptMaterial.mainTexture = promptFont.material.mainTexture;
        renderer.sharedMaterial = promptMaterial;
        promptObject.SetActive(false);
    }

    private ZeldaFourWayMover FindControlledMover()
    {
        return ZeldaRuntimeRegistry.GetControlledMover();
    }

    private static void EnsureSharedVisualAssets()
    {
        if (circleSprite == null)
        {
            circleSprite = CreateCircleSprite(64, false);
            circleSprite.name = "Growth Collectible Orbit Circle";
        }

        if (glowSprite == null)
        {
            glowSprite = CreateCircleSprite(128, true);
            glowSprite.name = "Growth Collectible Soft Glow";
        }

        if (lineMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            lineMaterial = new Material(shader)
            {
                name = "Growth Collectible White Line Material",
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        if (particleMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            particleMaterial = new Material(shader)
            {
                name = "Growth Collectible Pickup Particle Material",
                hideFlags = HideFlags.HideAndDontSave,
                mainTexture = circleSprite.texture
            };
        }
    }

    private static Sprite CreateCircleSprite(int size, bool softEdge)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        float radius = size * 0.5f;
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float normalizedDistance =
                Vector2.Distance(new Vector2(x, y), center) / radius;
            float alpha = softEdge
                ? Mathf.Clamp01(1f - normalizedDistance)
                : Mathf.Clamp01((1f - normalizedDistance) * size * 0.45f);
            texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }

        texture.Apply(false, true);
        return Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            size);
    }

    private void OnDestroy()
    {
        if (promptObject != null)
        {
            Destroy(promptObject);
        }
        if (promptMaterial != null)
        {
            Destroy(promptMaterial);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        collectibleAmount = Mathf.Max(1, collectibleAmount);
        pickupDistance = Mathf.Max(0.1f, pickupDistance);
        orbitPointCount = Mathf.Clamp(orbitPointCount, 4, 24);
        orbitRadius = Mathf.Max(0.1f, orbitRadius);
        orbitPointSize = Mathf.Max(0.02f, orbitPointSize);
        tetrahedronLineWidth = Mathf.Max(0.01f, tetrahedronLineWidth);
        glowSize = Mathf.Max(0.1f, glowSize);
        glowPulseSpeed = Mathf.Max(0.01f, glowPulseSpeed);
        pickupBurstParticleCount = Mathf.Clamp(pickupBurstParticleCount, 8, 64);
        pickupBurstLifetime = Mathf.Max(0.1f, pickupBurstLifetime);
        pickupBurstSpeed = Mathf.Max(0.1f, pickupBurstSpeed);
    }
#endif
}
