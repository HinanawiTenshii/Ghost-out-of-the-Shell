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

    private SpriteRenderer spriteRenderer;
    private Vector3 baseLocalPosition;
    private float shakeTimer;
    private bool isDestroyed;
    private ZeldaRequirementWindow requirementWindow;

    public int Durability => durability;
    public bool IsDestroyed => isDestroyed;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        baseLocalPosition = transform.localPosition;
        EnsureRequirementWindow();
    }

    private void Update()
    {
        UpdateShake();
    }

    public void ReceiveAttack(int attackPower)
    {
        if (isDestroyed)
        {
            return;
        }

        if (attackPower >= durability)
        {
            BreakDoor();
            return;
        }

        shakeTimer = shakeDuration;
    }

    private void UpdateShake()
    {
        if (shakeTimer <= 0f)
        {
            transform.localPosition = baseLocalPosition;
            return;
        }

        shakeTimer = Mathf.Max(0f, shakeTimer - Time.deltaTime);
        float shake = Mathf.Sin(Time.time * shakeSpeed) * shakeAmount;
        transform.localPosition = baseLocalPosition + new Vector3(shake, 0f, 0f);

        if (shakeTimer <= 0f)
        {
            transform.localPosition = baseLocalPosition;
        }
    }

    private void BreakDoor()
    {
        isDestroyed = true;
        Vector3 destructionPosition = spriteRenderer != null ? spriteRenderer.bounds.center : transform.position;
        NotifyNearbyAi(destructionPosition);
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

    private void NotifyNearbyAi(Vector2 destructionPosition)
    {
        if (investigationRadius <= 0f)
        {
            return;
        }

        float radiusSquared = investigationRadius * investigationRadius;
        foreach (ZeldaCharacterAiBase ai in ZeldaRuntimeRegistry.AiCharacters)
        {
            if (ai == null || !ai.isActiveAndEnabled ||
                ((Vector2)ai.transform.position - destructionPosition).sqrMagnitude > radiusSquared)
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
