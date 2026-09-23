using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
public class DoorData : MonoBehaviour
{
    [SerializeField] private int durability = 1;
    [SerializeField] private int fragmentCount = 18;
    [SerializeField] private float fragmentLifetime = 0.85f;
    [SerializeField] private float fragmentSpeed = 4.2f;
    [SerializeField] private float fragmentScale = 0.2f;
    [SerializeField] private float shakeDuration = 0.28f;
    [SerializeField] private float shakeAmount = 0.09f;
    [SerializeField] private float shakeSpeed = 95f;
    [SerializeField] private AudioClip breakSound;
    [SerializeField] private float breakSoundVolume = 5f;
    [Header("AI Destruction Investigation")]
    [SerializeField, Min(0f)] private float investigationRadius = 4f;
    [SerializeField, Min(0.05f)] private float investigationPulseDuration = 0.7f;
    [SerializeField] private Color investigationPulseColor = Color.white;
    [Header("Visual-only Door Hardware")]
    [SerializeField, Tooltip("Small child visuals that follow hit shake without enlarging interaction bounds.")]
    private Transform[] attachedVisuals = new Transform[0];
    private Vector3[] attachedVisualRestPositions;

    private SpriteRenderer spriteRenderer;
    private SpriteRenderer shakeVisual;
    private bool originalForceRenderingOff;
    private float shakeTimer;
    private float shakeElapsed;
    private bool isShaking;
    private bool isDestroyed;
    private ZeldaRequirementWindow requirementWindow;

    public int Durability => durability;
    public bool IsDestroyed => isDestroyed;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        attachedVisualRestPositions = new Vector3[attachedVisuals.Length];
        for (int i = 0; i < attachedVisuals.Length; i++)
            if (attachedVisuals[i] != null)
                attachedVisualRestPositions[i] = attachedVisuals[i].localPosition;
        EnsureRequirementWindow();
    }

    public bool IsAttachedVisual(Transform candidate)
    {
        for (int i = 0; i < attachedVisuals.Length; i++)
            if (attachedVisuals[i] == candidate)
                return true;
        return false;
    }

    private void OffsetAttachedVisuals(Vector3 offset)
    {
        if (attachedVisualRestPositions == null) return;
        for (int i = 0; i < attachedVisuals.Length; i++)
            if (attachedVisuals[i] != null)
                attachedVisuals[i].localPosition = attachedVisualRestPositions[i] + offset;
    }

    private void LateUpdate()
    {
        UpdateShake();
    }

    public void ReceiveAttack(int attackPower)
    {
        ReceiveAttack(attackPower, null, false);
    }

    public void ReceiveAttack(
        int attackPower,
        ZeldaCharacterData attackSource,
        bool isDirectCharacterAttack)
    {
        if (isDestroyed)
        {
            return;
        }

        if (attackPower >= durability)
        {
            BreakDoor(attackSource, isDirectCharacterAttack);
            return;
        }

        if (!isShaking)
        {
            if (spriteRenderer == null || spriteRenderer.sprite == null) return;
            GameObject visual = new GameObject("Door Hit Visual");
            visual.transform.SetParent(transform, false);
            shakeVisual = visual.AddComponent<SpriteRenderer>();
            originalForceRenderingOff = spriteRenderer.forceRenderingOff;
            spriteRenderer.forceRenderingOff = true;
        }
        shakeTimer = shakeDuration;
        shakeElapsed = 0f;
        isShaking = true;
    }

    private void UpdateShake()
    {
        if (!isShaking)
        {
            return;
        }

        shakeTimer = Mathf.Max(0f, shakeTimer - Time.deltaTime);
        shakeElapsed += Time.deltaTime;
        // Only offset a visual copy. Door transforms and colliders are also
        // used by hinge physics, quest markers and saved scene positions.
        if (shakeVisual != null)
        {
            shakeVisual.sprite = spriteRenderer.sprite;
            shakeVisual.color = spriteRenderer.color;
            shakeVisual.sharedMaterial = spriteRenderer.sharedMaterial;
            shakeVisual.sortingLayerID = spriteRenderer.sortingLayerID;
            shakeVisual.sortingOrder = spriteRenderer.sortingOrder;
            shakeVisual.flipX = spriteRenderer.flipX;
            shakeVisual.flipY = spriteRenderer.flipY;
            shakeVisual.drawMode = spriteRenderer.drawMode;
            shakeVisual.size = spriteRenderer.size;
            shakeVisual.maskInteraction = spriteRenderer.maskInteraction;
            shakeVisual.enabled = spriteRenderer.enabled;
            shakeVisual.gameObject.layer = gameObject.layer;
            Vector3 worldOffset = new Vector3(
                Mathf.Cos(shakeElapsed * shakeSpeed),
                Mathf.Sin(shakeElapsed * shakeSpeed * 0.83f), 0f) * shakeAmount;
            shakeVisual.transform.localPosition = transform.InverseTransformVector(worldOffset);
            OffsetAttachedVisuals(shakeVisual.transform.localPosition);
        }

        if (shakeTimer <= 0f)
        {
            StopShake();
        }
    }

    private void StopShake()
    {
        OffsetAttachedVisuals(Vector3.zero);
        if (isShaking && spriteRenderer != null)
            spriteRenderer.forceRenderingOff = originalForceRenderingOff;
        if (shakeVisual != null)
        {
            shakeVisual.gameObject.SetActive(false);
            Destroy(shakeVisual.gameObject);
            shakeVisual = null;
        }
        isShaking = false;
        shakeTimer = 0f;
    }

    private void OnDisable()
    {
        StopShake();
    }

    private void BreakDoor(
        ZeldaCharacterData attackSource,
        bool isDirectCharacterAttack)
    {
        StopShake();
        isDestroyed = true;
        Vector3 destructionPosition = spriteRenderer != null ? spriteRenderer.bounds.center : transform.position;
        // Destroy is deferred until the end of the frame. Disable the broken
        // object's colliders immediately so it cannot incorrectly block the
        // witness ray cast toward the attacking player.
        Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
        Bounds affectedBounds = default(Bounds);
        bool hasAffectedBounds = false;
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] == null)
            {
                continue;
            }
            if (!hasAffectedBounds)
            {
                affectedBounds = colliders[i].bounds;
                hasAffectedBounds = true;
            }
            else
            {
                affectedBounds.Encapsulate(colliders[i].bounds);
            }
            colliders[i].enabled = false;
        }
        if (hasAffectedBounds)
        {
            CameraCircularVision.NotifyBlockersChanged(affectedBounds);
        }
        else
        {
            CameraCircularVision.NotifyBlockersChanged();
        }

        NotifyNearbyAi(
            destructionPosition,
            isDirectCharacterAttack ? attackSource : null);
        SpawnInvestigationPulse(destructionPosition);
        PlayBreakSound();
        SpawnFragments();
        Destroy(gameObject);
    }

    private void PlayBreakSound()
    {
        if (breakSound == null)
        {
            return;
        }

        AudioSource.PlayClipAtPoint(breakSound, transform.position, breakSoundVolume);
    }

    private void SpawnFragments()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        Color fragmentColor = spriteRenderer.color;
        Vector3 origin = spriteRenderer.bounds.center;

        for (int i = 0; i < fragmentCount; i++)
        {
            float angle = (360f / fragmentCount) * i + Random.Range(-15f, 15f);
            Vector2 direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            float speed = fragmentSpeed * Random.Range(0.65f, 1.15f);

            GameObject fragmentObject = new GameObject("Door Fragment");
            fragmentObject.transform.position = origin;
            fragmentObject.transform.localScale = Vector3.one * fragmentScale * Random.Range(0.75f, 1.25f);

            SpriteRenderer fragmentRenderer = fragmentObject.AddComponent<SpriteRenderer>();
            fragmentRenderer.sprite = DoorFragmentVisual.FragmentSprite;
            fragmentRenderer.color = fragmentColor;
            fragmentRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
            fragmentRenderer.sortingOrder = spriteRenderer.sortingOrder + 1;

            DoorFragmentVisual fragment = fragmentObject.AddComponent<DoorFragmentVisual>();
            fragment.Configure(direction * speed, fragmentLifetime, Random.Range(-720f, 720f));
        }
    }

    private void OnValidate()
    {
        durability = Mathf.Max(0, durability);
        fragmentCount = Mathf.Max(1, fragmentCount);
        fragmentLifetime = Mathf.Max(0f, fragmentLifetime);
        fragmentSpeed = Mathf.Max(0f, fragmentSpeed);
        fragmentScale = Mathf.Max(0f, fragmentScale);
        shakeDuration = Mathf.Max(0f, shakeDuration);
        shakeAmount = Mathf.Max(0f, shakeAmount);
        shakeSpeed = Mathf.Max(0f, shakeSpeed);
        breakSoundVolume = Mathf.Max(0f, breakSoundVolume);
        investigationRadius = Mathf.Max(0f, investigationRadius);
        investigationPulseDuration = Mathf.Max(0.05f, investigationPulseDuration);
    }

    private void NotifyNearbyAi(
        Vector2 destructionPosition,
        ZeldaCharacterData directAttackSource)
    {
        float radiusSquared = investigationRadius * investigationRadius;
        foreach (ZeldaCharacterAiBase ai in ZeldaRuntimeRegistry.AiCharacters)
        {
            if (ai == null || !ai.isActiveAndEnabled)
            {
                continue;
            }

            if (directAttackSource != null &&
                ai.OnPlayerDestroyedDoor(directAttackSource))
            {
                continue;
            }

            if (investigationRadius <= 0f ||
                ((Vector2)ai.transform.position - destructionPosition).sqrMagnitude >
                radiusSquared)
            {
                continue;
            }

            ai.InvestigatePosition(destructionPosition);
        }
    }

    private void SpawnInvestigationPulse(Vector3 destructionPosition)
    {
        if (investigationRadius <= 0f)
        {
            return;
        }

        GameObject pulseObject = new GameObject("Door Destruction Investigation Pulse");
        pulseObject.transform.position = destructionPosition;
        ZeldaInvestigationPulse pulse = pulseObject.AddComponent<ZeldaInvestigationPulse>();
        int sortingLayerId = spriteRenderer != null ? spriteRenderer.sortingLayerID : 0;
        int sortingOrder = spriteRenderer != null ? spriteRenderer.sortingOrder + 10 : 10;
        pulse.Configure(
            investigationRadius,
            investigationPulseDuration,
            investigationPulseColor,
            sortingLayerId,
            sortingOrder);
    }

    private void EnsureRequirementWindow()
    {
        requirementWindow = GetComponentInChildren<ZeldaRequirementWindow>(true);
        if (requirementWindow == null)
        {
            GameObject windowObject = new GameObject("Durability Requirement Window");
            windowObject.transform.SetParent(transform, false);
            requirementWindow = windowObject.AddComponent<ZeldaRequirementWindow>();
        }

        requirementWindow.Configure(this);
    }
}
