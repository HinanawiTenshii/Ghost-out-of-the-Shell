using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
public class LeverData : MonoBehaviour
{
    private static readonly HashSet<LeverData> ActiveLevers = new HashSet<LeverData>();
    public event Action<LeverData, bool> SignalEmitted;

    [SerializeField] private int complexity = 1;
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private float interactionDistance = 1.1f;
    [Header("Linked Mechanisms (Maximum 3)")]
    [SerializeField, InspectorName("Linked Mechanism 1")] private GameObject linkedObject;
    [SerializeField, InspectorName("Linked Mechanism 2")] private GameObject linkedObject2;
    [SerializeField, InspectorName("Linked Mechanism 3")] private GameObject linkedObject3;
    [SerializeField] private Color baseColor = new Color(0.42f, 0.28f, 0.16f, 1f);
    [SerializeField] private Color leverColor = new Color(0.72f, 0.12f, 0.08f, 1f);
    [SerializeField] private Vector2 interactionPromptOffset = new Vector2(0f, 0.9f);

    private SpriteRenderer spriteRenderer;
    private BoxCollider2D interactionCollider;
    private Sprite offSprite;
    private Sprite onSprite;
    private bool isOn;
    private ZeldaRequirementWindow requirementWindow;
    private GameObject interactionPromptObject;
    private TextMesh interactionPromptText;
    private Font interactionPromptFont;
    private Material interactionPromptMaterial;
    private bool insufficientSkillAcknowledged;

    public int Complexity => complexity;
    public bool IsOn => isOn;
    public static IReadOnlyCollection<LeverData> WorldLevers => ActiveLevers;
    public SpriteRenderer VisualRenderer => spriteRenderer;
    public Vector2 PuppetMountPoint
    {
        get
        {
            float headX = isOn ? 7f : 17f;
            return transform.TransformPoint(new Vector2(
                (headX - 12f) / 26.6667f,
                (20f - 5.28f) / 26.6667f));
        }
    }
    public Vector2 StemDirection
    {
        get
        {
            Vector2 basePoint = transform.TransformPoint(
                new Vector2(0f, (8f - 5.28f) / 26.6667f));
            Vector2 direction = PuppetMountPoint - basePoint;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.up;
        }
    }

    public float GetSurfaceDistanceTo(
        Collider2D sourceCollider,
        Vector2 sourcePosition)
    {
        float fallback = Vector2.Distance(sourcePosition, transform.position);
        if (sourceCollider == null || interactionCollider == null ||
            !interactionCollider.enabled)
        {
            return fallback;
        }

        ColliderDistance2D distance =
            sourceCollider.Distance(interactionCollider);
        if (!distance.isValid)
        {
            return fallback;
        }
        return distance.isOverlapped ? 0f : distance.distance;
    }

    public void ApplyPersistentState(bool value)
    {
        isOn = value;
        ApplyVisual();
    }
    public GameObject LinkedObject => linkedObject;

    public GameObject GetLinkedObject(int index)
    {
        switch (index)
        {
            case 0: return linkedObject;
            case 1: return linkedObject2;
            case 2: return linkedObject3;
            default: return null;
        }
    }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        interactionCollider = GetComponent<BoxCollider2D>();
        interactionCollider.isTrigger = false;
        interactionCollider.size = new Vector2(0.66f, 0.495f);
        interactionCollider.offset = new Vector2(0f, 0.21f);

        offSprite = CreateLeverSprite(false);
        onSprite = CreateLeverSprite(true);
        ApplyVisual();
        EnsureRequirementWindow();
        // Runtime attachment covers all existing prefab/scene levers without
        // changing their interaction data or duplicating a shared prefab ID.
        if (GetComponent<MapPointOfInterest>() == null)
            gameObject.AddComponent<MapPointOfInterest>();
    }

    private void OnEnable()
    {
        ActiveLevers.Add(this);
    }

    public void ActivateFromClockworkPuppet()
    {
        isOn = !isOn;
        ApplyVisual();
        SignalEmitted?.Invoke(this, isOn);
        ApplyLinkedObjectChange();
    }

    private void Update()
    {
        if (DocumentReader.IsInputBlocked)
        {
            return;
        }

        if (Input.GetKeyDown(interactKey))
        {
            ZeldaCharacterData nearbyCharacter = FindNearbyActiveCharacter();
            if (nearbyCharacter != null)
            {
                ZeldaInteractionArbiter.Submit(
                    this,
                    nearbyCharacter.GetComponent<ZeldaFourWayMover>(),
                    interactKey,
                    transform.position,
                    HandlePlayerInteraction);
            }
        }
    }

    private void HandlePlayerInteraction()
    {
        ZeldaCharacterData nearbyCharacter = FindNearbyActiveCharacter();
        if (nearbyCharacter == null)
        {
            return;
        }

        bool interactionSucceeded = TryInteract(nearbyCharacter);
        if (!interactionSucceeded && nearbyCharacter.FinalSkillValue < complexity)
        {
            insufficientSkillAcknowledged = true;
        }
    }

    private void LateUpdate()
    {
        UpdateInteractionPrompt();
    }

    private bool TryInteract(ZeldaCharacterData characterData)
    {
        if (characterData == null || characterData.FinalSkillValue < complexity)
        {
            return false;
        }

        isOn = !isOn;
        ApplyVisual();
        SignalEmitted?.Invoke(this, isOn);
        ApplyLinkedObjectChange();
        return true;
    }

    private ZeldaCharacterData FindNearbyActiveCharacter()
    {
        ZeldaFourWayMover mover = ZeldaRuntimeRegistry.GetControlledMover();
        if (mover == null ||
            Vector2.Distance(transform.position, mover.transform.position) > interactionDistance)
            return null;

        ZeldaCharacterData characterData = mover.GetComponent<ZeldaCharacterData>();
        return characterData != null && !characterData.IsDead ? characterData : null;
    }

    private void UpdateInteractionPrompt()
    {
        ZeldaCharacterData characterData = DocumentReader.IsInputBlocked
            ? null
            : FindNearbyActiveCharacter();
        if (characterData == null)
        {
            SetInteractionPromptVisible(false);
            return;
        }

        bool hasEnoughSkill = characterData.FinalSkillValue >= complexity;
        if (hasEnoughSkill)
        {
            insufficientSkillAcknowledged = false;
        }
        string promptMessage = insufficientSkillAcknowledged && !hasEnoughSkill
            ? "能力不足"
            : "按[E]进行互动";

        // Register even in a minimal test scene with no HUD or prompt font.
        ZeldaFourWayMover controlledMover =
            characterData.GetComponent<ZeldaFourWayMover>();
        ZeldaInteractionArbiter.OfferInteraction(
            this,
            controlledMover,
            interactKey,
            transform.position,
            SetInteractionPromptVisible);
        EnsureInteractionPrompt();
        if (interactionPromptObject == null)
            return;

        interactionPromptText.text = promptMessage;
        interactionPromptObject.transform.position =
            controlledMover.GetOverheadWorldPosition(interactionPromptOffset);
        interactionPromptObject.transform.rotation = Quaternion.identity;
        interactionPromptFont.RequestCharactersInTexture(
            promptMessage,
            72,
            FontStyle.Normal);
        interactionPromptMaterial.mainTexture = interactionPromptFont.material.mainTexture;
    }

    private void EnsureInteractionPrompt()
    {
        if (interactionPromptObject != null)
            return;

        ZeldaHealthHeartsUI ui = ZeldaHealthHeartsUI.Instance;
        interactionPromptFont = ui != null ? ui.PermissionLabelFont : null;
        if (interactionPromptFont == null)
            return;

        interactionPromptFont.RequestCharactersInTexture(
            "按[E]进行互动能力不足",
            72,
            FontStyle.Normal);

        interactionPromptObject = new GameObject(name + " Lever Interaction Prompt");
        interactionPromptText = interactionPromptObject.AddComponent<TextMesh>();
        interactionPromptText.text = "按[E]进行互动";
        interactionPromptText.font = interactionPromptFont;
        interactionPromptText.fontSize = 72;
        interactionPromptText.characterSize = 0.035f;
        interactionPromptText.anchor = TextAnchor.MiddleCenter;
        interactionPromptText.alignment = TextAlignment.Center;
        interactionPromptText.color = ZeldaUiPalette.Primary;

        MeshRenderer promptRenderer = interactionPromptObject.GetComponent<MeshRenderer>();
        int highestSortingLayerId = 0;
        int highestSortingLayerValue = int.MinValue;
        foreach (SortingLayer sortingLayer in SortingLayer.layers)
        {
            if (sortingLayer.value > highestSortingLayerValue)
            {
                highestSortingLayerValue = sortingLayer.value;
                highestSortingLayerId = sortingLayer.id;
            }
        }

        promptRenderer.sortingLayerID = highestSortingLayerId;
        promptRenderer.sortingOrder = short.MaxValue - 2;
        ZeldaPossessionProgressBar.ConfigureOverlayRenderer(promptRenderer);
        ZeldaPossessionProgressBar.ConfigureOverlayRenderer(promptRenderer);
        interactionPromptMaterial = new Material(interactionPromptFont.material)
        {
            name = name + " Lever Interaction Prompt Font Material",
            hideFlags = HideFlags.HideAndDontSave
        };
        interactionPromptMaterial.mainTexture = interactionPromptFont.material.mainTexture;
        promptRenderer.sharedMaterial = interactionPromptMaterial;
        interactionPromptObject.SetActive(false);
    }

    private void SetInteractionPromptVisible(bool visible)
    {
        if (interactionPromptObject != null
            && interactionPromptObject.activeSelf != visible)
        {
            interactionPromptObject.SetActive(visible);
        }
    }

    private void ApplyLinkedObjectChange()
    {
        ApplyLinkedObjectChange(linkedObject);
        if (linkedObject2 != linkedObject)
        {
            ApplyLinkedObjectChange(linkedObject2);
        }

        if (linkedObject3 != linkedObject && linkedObject3 != linkedObject2)
        {
            ApplyLinkedObjectChange(linkedObject3);
        }
    }

    private static void ApplyLinkedObjectChange(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        // A bridge interlock owns the hinge on the same object. Do not also
        // toggle that hinge directly or it would rotate twice / bypass safety.
        RotatingBridgeMechanism bridgeMechanism = target.GetComponent<RotatingBridgeMechanism>();
        if (bridgeMechanism != null)
        {
            bridgeMechanism.ToggleFromExternal();
            return;
        }

        DoubleSlidingDoor slidingDoor = target.GetComponent<DoubleSlidingDoor>();
        if (slidingDoor != null)
        {
            slidingDoor.ToggleFromExternal();
            return;
        }

        DoorHingeInteraction doorHinge = target.GetComponent<DoorHingeInteraction>();
        if (doorHinge != null)
        {
            doorHinge.ToggleFromExternal();
            return;
        }

        target.SetActive(!target.activeSelf);
        CameraCircularVision.NotifyBlockersChanged();
    }

    private void ApplyVisual()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        spriteRenderer.sprite = isOn ? onSprite : offSprite;
        spriteRenderer.color = Color.white;
        spriteRenderer.sortingOrder = 1;
    }

    private Sprite CreateLeverSprite(bool pulled)
    {
        const int width = 24;
        const int height = 24;
        Texture2D texture = new Texture2D(width, height);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color clear = new Color(1f, 1f, 1f, 0f);
        Color darkBase = Color.Lerp(baseColor, Color.black, 0.45f);

        FillRect(texture, 0, 0, width, height, clear);

        Vector2 pivot = new Vector2(12f, 8f);
        Vector2 end = pulled
            ? new Vector2(7f, 20f)
            : new Vector2(17f, 20f);
        Vector2 leverDirection = (end - pivot).normalized;
        Vector2 leverCenter = (pivot + end) * 0.5f;
        float leverLength = Vector2.Distance(pivot, end) + 2f;

        // Draw the rectangular lever behind the base so its lower end looks
        // mechanically seated inside the semicircular housing.
        FillOrientedRectangle(
            texture,
            leverCenter,
            leverDirection,
            leverLength,
            4f,
            darkBase);
        FillOrientedRectangle(
            texture,
            leverCenter,
            leverDirection,
            leverLength - 1f,
            2f,
            Color.white);

        // A compact square head replaces the previous wide handle.
        FillOrientedRectangle(
            texture,
            end,
            Vector2.up,
            5f,
            5f,
            darkBase);
        FillOrientedRectangle(
            texture,
            end,
            Vector2.up,
            3f,
            3f,
            leverColor);

        FillUpperSemicircle(texture, new Vector2(12f, 4f), 8f, darkBase);
        FillUpperSemicircle(texture, new Vector2(12f, 4f), 6.5f, baseColor);
        // Even pixel counts around x=12 keep both sides of the base as exact
        // mirrors; the previous odd-width strips left one sharp extra pixel
        // on the right edge.
        FillRect(texture, 4, 3, 16, 2, darkBase);
        FillRect(texture, 6, 5, 12, 1, baseColor);

        texture.Apply();
        return Sprite.Create(
            texture,
            new Rect(0, 0, width, height),
            new Vector2(0.5f, 0.22f),
            26.666667f);
    }

    private static void FillUpperSemicircle(
        Texture2D texture,
        Vector2 center,
        float radius,
        Color color)
    {
        float radiusSquared = radius * radius;
        for (int y = Mathf.Max(0, Mathf.FloorToInt(center.y));
             y < texture.height;
             y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                Vector2 offset =
                    new Vector2(x + 0.5f, y + 0.5f) - center;
                if (offset.sqrMagnitude <= radiusSquared)
                {
                    texture.SetPixel(x, y, color);
                }
            }
        }
    }

    private static void FillOrientedRectangle(
        Texture2D texture,
        Vector2 center,
        Vector2 lengthDirection,
        float length,
        float rectangleWidth,
        Color color)
    {
        Vector2 normalizedLength = lengthDirection.sqrMagnitude > 0.0001f
            ? lengthDirection.normalized
            : Vector2.up;
        Vector2 widthDirection = new Vector2(
            -normalizedLength.y,
            normalizedLength.x);
        float halfLength = Mathf.Max(0f, length) * 0.5f;
        float halfWidth = Mathf.Max(0f, rectangleWidth) * 0.5f;

        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                Vector2 offset =
                    new Vector2(x + 0.5f, y + 0.5f) - center;
                if (Mathf.Abs(Vector2.Dot(offset, normalizedLength)) <=
                        halfLength &&
                    Mathf.Abs(Vector2.Dot(offset, widthDirection)) <=
                        halfWidth)
                {
                    texture.SetPixel(x, y, color);
                }
            }
        }
    }

    private static void FillRect(Texture2D texture, int startX, int startY, int width, int height, Color color)
    {
        for (int y = startY; y < startY + height; y++)
        {
            for (int x = startX; x < startX + width; x++)
            {
                texture.SetPixel(x, y, color);
            }
        }
    }

    private void OnValidate()
    {
        complexity = Mathf.Max(0, complexity);
        interactionDistance = Mathf.Max(0f, interactionDistance);
    }

    private void EnsureRequirementWindow()
    {
        requirementWindow = GetComponentInChildren<ZeldaRequirementWindow>(true);
        if (requirementWindow == null)
        {
            GameObject windowObject = new GameObject("Complexity Requirement Window");
            windowObject.transform.SetParent(transform, false);
            requirementWindow = windowObject.AddComponent<ZeldaRequirementWindow>();
        }

        requirementWindow.Configure(this);
    }

    private void OnDisable()
    {
        ActiveLevers.Remove(this);
        SetInteractionPromptVisible(false);
    }

    private void OnDestroy()
    {
        ActiveLevers.Remove(this);
        if (interactionPromptObject != null)
        {
            Destroy(interactionPromptObject);
        }

        if (interactionPromptMaterial != null)
        {
            Destroy(interactionPromptMaterial);
        }
    }
}
