using UnityEngine;

/// <summary>Runtime-only armed bomb created when a bomb inventory item is used.</summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class PlacedBomb : MonoBehaviour
{
    public string SaveSourceItemId { get; set; }
    public ZeldaCharacterData SaveOwner => owner;
    public void RestoreSavedOwner(ZeldaCharacterData savedOwner)
    {
        owner = savedOwner;
        // Saved scalar configuration is applied after the prefab's Configure.
        if (configured && !exploded) RefreshStreamingRetention();
    }
    private SpriteRenderer bombRenderer;
    private SpriteRenderer highlightRenderer;
    private SpriteRenderer fuseRenderer;
    private Sprite bombSprite;
    private Sprite highlightSprite;
    private Sprite fuseSprite;
    private Texture2D bombTexture;
    private Texture2D highlightTexture;
    private Texture2D fuseTexture;
    private float fuseDuration;
    private float flashInterval;
    private float elapsed;
    private Color bombColor;
    private Color highlightColor;
    private Color flashColor;
    private int attackPower;
    private Vector2 explosionSize;
    private float hitboxDuration;
    private Color hitboxColor;
    private AudioClip explosionSound;
    private static AudioClip defaultExplosionSound;
    private float explosionSoundVolume;
    private float explosionSoundPitch;
    private float explosionSoundSpatialBlend;
    private float explosionSoundMaxDistance;
    private float investigationRadius;
    private float pulseDuration;
    private Color pulseColor;
    private int effectSortingOrder;
    private ZeldaCharacterData owner;
    private bool configured;
    private bool exploded;
    private CameraVisionStreamingRegion streamingRegion;

    // Cover both sound attraction and the corners of the rectangular damage box.
    // This is in world units, independent of the bomb sprite's visual scale.
    private float StreamingRetentionRadius => Mathf.Max(investigationRadius, explosionSize.magnitude * 0.5f);

    private void Awake()
    {
        bombRenderer = GetComponent<SpriteRenderer>();
        BoxCollider2D attackReceiver = GetComponent<BoxCollider2D>();
        attackReceiver.isTrigger = true;
        attackReceiver.size = Vector2.one;
        GameObject highlightObject = new GameObject("Bomb Highlight");
        highlightObject.transform.SetParent(transform, false);
        highlightRenderer = highlightObject.AddComponent<SpriteRenderer>();
        highlightRenderer.enabled = false;
        GameObject fuseObject = new GameObject("Fuse");
        fuseObject.transform.SetParent(transform, false);
        fuseRenderer = fuseObject.AddComponent<SpriteRenderer>();
        fuseRenderer.color = Color.white;
    }

    public void Configure(
        float configuredFuseDuration,
        float configuredFlashInterval,
        Color configuredBombColor,
        bool useHighlight,
        Color configuredHighlightColor,
        Color configuredFlashColor,
        float visualSize,
        int configuredAttackPower,
        Vector2 configuredExplosionSize,
        float configuredHitboxDuration,
        Color configuredHitboxColor,
        AudioClip configuredExplosionSound,
        float configuredExplosionSoundVolume,
        float configuredExplosionSoundPitch,
        float configuredExplosionSoundSpatialBlend,
        float configuredExplosionSoundMaxDistance,
        float configuredInvestigationRadius,
        float configuredPulseDuration,
        Color configuredPulseColor,
        int configuredSortingOrder,
        ZeldaCharacterData configuredOwner,
        bool emitAuraParticles,
        Color auraParticleColor,
        float auraParticlesPerSecond,
        float auraParticleLifetime,
        float auraParticleRadius,
        float auraParticleSpeed,
        float auraParticleSize)
    {
        fuseDuration = Mathf.Max(0.05f, configuredFuseDuration);
        flashInterval = Mathf.Max(0.02f, configuredFlashInterval);
        bombColor = configuredBombColor;
        highlightColor = configuredHighlightColor;
        flashColor = configuredFlashColor;
        transform.localScale = Vector3.one * Mathf.Max(0.05f, visualSize);
        attackPower = Mathf.Max(0, configuredAttackPower);
        explosionSize = new Vector2(
            Mathf.Max(0.05f, configuredExplosionSize.x),
            Mathf.Max(0.05f, configuredExplosionSize.y));
        hitboxDuration = Mathf.Max(0.02f, configuredHitboxDuration);
        hitboxColor = configuredHitboxColor;
        // Resolve/preload at placement, not on the first frame of detonation.
        explosionSound = ResolveExplosionSound(configuredExplosionSound);
        explosionSoundVolume = Mathf.Clamp01(configuredExplosionSoundVolume);
        explosionSoundPitch = Mathf.Clamp(configuredExplosionSoundPitch, 0.1f, 3f);
        explosionSoundSpatialBlend = Mathf.Clamp01(configuredExplosionSoundSpatialBlend);
        explosionSoundMaxDistance = Mathf.Max(0.01f, configuredExplosionSoundMaxDistance);
        investigationRadius = Mathf.Max(0f, configuredInvestigationRadius);
        pulseDuration = Mathf.Max(0.05f, configuredPulseDuration);
        pulseColor = configuredPulseColor;
        effectSortingOrder = configuredSortingOrder;
        owner = configuredOwner;
        CreateBombSprite();
        bombRenderer.color = bombColor;
        bombRenderer.sortingOrder = effectSortingOrder;
        highlightRenderer.color = highlightColor;
        highlightRenderer.sortingLayerID = bombRenderer.sortingLayerID;
        highlightRenderer.sortingOrder = effectSortingOrder + 1;
        highlightRenderer.enabled = useHighlight;
        fuseRenderer.sortingLayerID = bombRenderer.sortingLayerID;
        fuseRenderer.sortingOrder = effectSortingOrder + 2;
        if (emitAuraParticles)
        {
            RadialFadingParticleEmitter auraEmitter =
                gameObject.AddComponent<RadialFadingParticleEmitter>();
            auraEmitter.Configure(
                auraParticleColor,
                auraParticlesPerSecond,
                auraParticleLifetime,
                auraParticleRadius,
                auraParticleSpeed,
                auraParticleSize,
                effectSortingOrder - 1);
        }
        configured = true;
        RefreshStreamingRetention();
    }

    private void RefreshStreamingRetention()
    {
        if (streamingRegion == null)
            streamingRegion = GetComponent<CameraVisionStreamingRegion>();
        if (streamingRegion == null)
            streamingRegion = gameObject.AddComponent<CameraVisionStreamingRegion>();
        streamingRegion.Configure(StreamingRetentionRadius);
    }

    private void Update()
    {
        if (!configured || exploded)
        {
            return;
        }

        elapsed += Time.deltaTime;
        int flashPhase = Mathf.FloorToInt(elapsed / flashInterval);
        bool normalPhase = (flashPhase & 1) == 0;
        bombRenderer.color = normalPhase ? bombColor : flashColor;
        if (highlightRenderer != null && highlightRenderer.enabled)
        {
            highlightRenderer.color = normalPhase
                ? highlightColor
                : Color.Lerp(flashColor, Color.white, 0.38f);
        }
        if (elapsed >= fuseDuration)
        {
            Explode();
        }
    }

    private void Explode()
    {
        if (exploded)
        {
            return;
        }
        exploded = true;
        Vector3 explosionPosition = transform.position;
        // Re-evaluate the region (including soul-mark radius upgrades) before
        // notifying AI or creating damage, including immediate/chain detonation.
        RefreshStreamingRetention();
        CreateAttackHitbox(explosionPosition);
        CreateExplosionVisual(explosionPosition);
        PlayExplosionSound(explosionPosition);
        NotifyNearbyAi(explosionPosition);
        CreateInvestigationPulse(explosionPosition);
        Destroy(gameObject);
    }

    /// <summary>Skips the remaining fuse time, while preserving configured effects.</summary>
    public void DetonateImmediately()
    {
        if (!configured || exploded)
        {
            return;
        }

        Explode();
    }

    public void DetonateSoulMark(int skillLevel = 1)
    {
        attackPower = skillLevel >= 2 ? 5 : 3;
        if (skillLevel >= 2) explosionSize *= 1.5f;
        hitboxColor = ZeldaUiPalette.Primary;
        pulseColor = Color.white;
        DetonateImmediately();
    }

    private static AudioClip ResolveExplosionSound(AudioClip configuredSound)
    {
        if (configuredSound != null) return configuredSound;
        if (defaultExplosionSound == null)
            defaultExplosionSound = Resources.Load<AudioClip>("Audio/BombExplosion");
        return defaultExplosionSound;
    }

    private void PlayExplosionSound(Vector3 explosionPosition)
    {
        if (explosionSound == null || explosionSoundVolume <= 0f)
        {
            return;
        }

        GameObject soundObject = new GameObject("Bomb Explosion Sound");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(soundObject, gameObject.scene);
        soundObject.AddComponent<CameraVisionStreamingExempt>();
        soundObject.transform.position = explosionPosition;
        AudioSource source = soundObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.dopplerLevel = 0f;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.minDistance = 1f;
        source.maxDistance = explosionSoundMaxDistance;
        source.spatialBlend = explosionSoundSpatialBlend;
        source.pitch = explosionSoundPitch;
        source.clip = explosionSound;
        source.volume = explosionSoundVolume;
        source.Play();

        float playbackDuration = explosionSound.length /
            Mathf.Max(0.1f, Mathf.Abs(explosionSoundPitch));
        Destroy(soundObject, playbackDuration + 0.1f);
    }

    private void CreateAttackHitbox(Vector3 explosionPosition)
    {
        GameObject hitboxObject = new GameObject(
            "Bomb Explosion Hitbox",
            typeof(SpriteRenderer),
            typeof(BoxCollider2D));
        hitboxObject.transform.position = explosionPosition;
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(hitboxObject, gameObject.scene);
        // Hand off the lease until the short damage trigger expires. Otherwise
        // LateUpdate could unload colliders before the next physics step.
        hitboxObject.AddComponent<CameraVisionStreamingRegion>().Configure(StreamingRetentionRadius);
        ZeldaAttackHitbox hitbox = hitboxObject.AddComponent<ZeldaAttackHitbox>();
        hitbox.Configure(
            hitboxDuration,
            explosionSize,
            hitboxColor,
            attackPower,
            owner,
            ZeldaAttackVisualShape.Box,
            true);

        // The explosion visual communicates the blast area; keep the damage
        // trigger active while hiding the generic attack hitbox outline.
        SpriteRenderer hitboxRenderer = hitboxObject.GetComponent<SpriteRenderer>();
        if (hitboxRenderer != null)
        {
            hitboxRenderer.enabled = false;
        }
    }

    private void NotifyNearbyAi(Vector2 explosionPosition)
    {
        if (investigationRadius <= 0f)
        {
            return;
        }

        float radiusSquared = investigationRadius * investigationRadius;
        foreach (ZeldaCharacterAiBase ai in ZeldaRuntimeRegistry.AiCharacters)
        {
            if (ai == null || !ai.isActiveAndEnabled ||
                ZeldaRuntimeRegistry.GetGameplayScene(ai.gameObject) != gameObject.scene ||
                ((Vector2)ai.transform.position - explosionPosition).sqrMagnitude >
                radiusSquared)
            {
                continue;
            }
            ai.InvestigatePosition(explosionPosition);
        }
    }

    private void CreateExplosionVisual(Vector3 explosionPosition)
    {
        GameObject visualObject = new GameObject("Bomb Explosion");
        visualObject.transform.position = explosionPosition;
        BombExplosionVisual visual = visualObject.AddComponent<BombExplosionVisual>();
        visual.Configure(
            explosionSize,
            Mathf.Max(0.3f, hitboxDuration * 2.5f),
            hitboxColor,
            bombRenderer.sortingLayerID,
            effectSortingOrder + 2);
    }

    private void CreateInvestigationPulse(Vector3 explosionPosition)
    {
        if (investigationRadius <= 0f)
        {
            return;
        }

        GameObject pulseObject = new GameObject("Bomb Investigation Pulse");
        pulseObject.transform.position = explosionPosition;
        ZeldaInvestigationPulse pulse =
            pulseObject.AddComponent<ZeldaInvestigationPulse>();
        pulse.Configure(
            investigationRadius,
            pulseDuration,
            pulseColor,
            bombRenderer.sortingLayerID,
            effectSortingOrder + 1);
    }

    private void CreateBombSprite()
    {
        ReleaseVisualResources();
        string[] rows = BombPickupItemVisual.GetBombRows(SaveSourceItemId == "purple_bomb");
        int height = rows.Length;
        int width = rows[0].Length;
        bombTexture = CreateLayerTexture(rows, 0, "Placed Bomb Body");
        highlightTexture = CreateLayerTexture(rows, 1, "Placed Bomb Highlight");
        fuseTexture = CreateLayerTexture(rows, 2, "Placed Bomb Fuse");
        bombSprite = CreateMaskSprite(bombTexture, width, height, "Placed Pixel Bomb");
        highlightSprite = CreateMaskSprite(highlightTexture, width, height, "Placed Bomb Highlight");
        fuseSprite = CreateMaskSprite(fuseTexture, width, height, "Placed Bomb Fuse");
        bombRenderer.sprite = bombSprite;
        highlightRenderer.sprite = highlightSprite;
        fuseRenderer.sprite = fuseSprite;
    }

    // Body and highlight remain independently tintable for the existing fuse
    // flash. The neutral collar and tan fuse do not flash with the casing.
    private static Texture2D CreateLayerTexture(string[] rows, int layer, string textureName)
    {
        int height = rows.Length;
        int width = rows[0].Length;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = textureName,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        for (int row = 0; row < height; row++)
        for (int x = 0; x < width; x++)
        {
            char symbol = rows[row][x];
            Color color = Color.clear;
            if (layer == 0 && (symbol == 'B' || symbol == 'D' || symbol == 'H'))
                color = BombPickupItemVisual.SamplePixel(symbol, Color.white, false, Color.white, Color.clear);
            else if (layer == 1 && symbol == 'H') color = Color.white;
            else if (layer == 2 && (symbol == 'K' || symbol == 'F'))
                color = BombPickupItemVisual.SamplePixel(symbol, Color.clear, false, Color.clear,
                    new Color(0.78f, 0.64f, 0.43f, 1f));
            texture.SetPixel(x, height - 1 - row, color);
        }
        texture.Apply(false, true);
        return texture;
    }

    private static Sprite CreateMaskSprite(
        Texture2D texture,
        int width,
        int height,
        string spriteName)
    {
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, width, height),
            new Vector2(0.5f, 0.5f),
            Mathf.Max(width, height));
        sprite.name = spriteName;
        return sprite;
    }

    private void OnDestroy() => ReleaseVisualResources();

    private void ReleaseVisualResources()
    {
        if (bombSprite != null)
        {
            Destroy(bombSprite);
        }
        if (bombTexture != null)
        {
            Destroy(bombTexture);
        }
        if (fuseSprite != null)
        {
            Destroy(fuseSprite);
        }
        if (highlightSprite != null)
        {
            Destroy(highlightSprite);
        }
        if (fuseTexture != null)
        {
            Destroy(fuseTexture);
        }
        if (highlightTexture != null)
        {
            Destroy(highlightTexture);
        }
    }
}
