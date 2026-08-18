using System;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
public class LeverData : MonoBehaviour
{
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
    [SerializeField] private Color metalColor = new Color(0.72f, 0.72f, 0.68f, 1f);
    [SerializeField] private Vector2 interactionPromptOffset = new Vector2(0f, 0.9f);

    private SpriteRenderer spriteRenderer;
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
        BoxCollider2D boxCollider = GetComponent<BoxCollider2D>();
        boxCollider.isTrigger = false;
        boxCollider.size = new Vector2(0.8f, 0.65f);
        boxCollider.offset = new Vector2(0f, 0.28f);

        offSprite = CreateLeverSprite(false);
        onSprite = CreateLeverSprite(true);
        ApplyVisual();
        EnsureRequirementWindow();
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

        EnsureInteractionPrompt();
        if (interactionPromptObject == null)
            return;

        interactionPromptText.text = promptMessage;
        interactionPromptObject.transform.position =
            characterData.transform.position + (Vector3)interactionPromptOffset;
        interactionPromptObject.transform.rotation = Quaternion.identity;
        ZeldaInteractionArbiter.OfferInteraction(
            this,
            characterData.GetComponent<ZeldaFourWayMover>(),
            interactKey,
            transform.position,
            SetInteractionPromptVisible);

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

        DoorHingeInteraction doorHinge = target.GetComponent<DoorHingeInteraction>();
        if (doorHinge != null)
        {
            doorHinge.ToggleFromExternal();
            return;
        }

        target.SetActive(!target.activeSelf);
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
        const int width = 16;
        const int height = 16;
        Texture2D texture = new Texture2D(width, height);
        texture.filterMode = FilterMode.Point;

        Color clear = new Color(1f, 1f, 1f, 0f);
        Color darkBase = Color.Lerp(baseColor, Color.black, 0.45f);

        FillRect(texture, 0, 0, width, height, clear);
        FillRect(texture, 3, 2, 10, 3, darkBase);
        FillRect(texture, 4, 4, 8, 2, baseColor);
        FillRect(texture, 6, 6, 4, 2, metalColor);

        if (pulled)
        {
            FillRect(texture, 6, 8, 2, 4, metalColor);
            FillRect(texture, 5, 11, 2, 2, leverColor);
        }
        else
        {
            FillRect(texture, 8, 8, 2, 4, metalColor);
            FillRect(texture, 9, 11, 2, 2, leverColor);
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.25f), 16f);
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
        SetInteractionPromptVisible(false);
    }

    private void OnDestroy()
    {
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
