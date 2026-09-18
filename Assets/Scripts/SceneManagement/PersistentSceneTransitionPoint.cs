using UnityEngine;

/// <summary>
/// Invisible runtime transition trigger with an editor-only size outline.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public sealed class PersistentSceneTransitionPoint : MonoBehaviour
{
    [SerializeField] private string targetSceneName = "NextScene";
    [SerializeField, Tooltip("Name shown in the interaction prompt for the destination area.")]
    private string destinationAreaName = "目标区域";
    [SerializeField, Tooltip("World position assigned to the controlled character after the target scene and its spawn point finish initializing.")]
    private Vector2 targetArrivalPosition;
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField, Tooltip("Exact local-space size shared by the editor outline, trigger collider and interaction test.")]
    private Vector2 triggerSize = new Vector2(1.1f, 0.75f);
    [SerializeField, Tooltip("Exact local-space offset shared by the editor outline, trigger collider and interaction test.")]
    private Vector2 triggerOffset = new Vector2(0f, 0.25f);
    [SerializeField] private Vector2 interactionPromptOffset = new Vector2(0f, 0.9f);
    [SerializeField] private Color editorBorderColor =
        new Color(0.22f, 0.76f, 1f, 0.9f);

    private GameObject promptObject;
    private TextMesh promptText;
    private Font promptFont;
    private Material promptMaterial;
    private BoxCollider2D triggerCollider;
    private bool isTransitioning;

    public string TargetSceneName => targetSceneName;
    public string DestinationAreaName => destinationAreaName;
    public Vector2 TargetArrivalPosition => targetArrivalPosition;
    public Vector2 TriggerSize => triggerSize;
    public Vector2 TriggerOffset => triggerOffset;

    private void Awake()
    {
        // Create the state manager before any item can be collected so it can
        // assign stable scene identities while the authored hierarchy is intact.
        SceneTravelStateManager.GetOrCreate();
        SynchronizeCollider();
    }

    private void Update()
    {
        if (isTransitioning || DocumentReader.IsInputBlocked)
        {
            return;
        }

        ZeldaFourWayMover mover = GetNearbyControlledMover();
        if (mover != null)
        {
            // A transition point is an area interaction.  Using this object's
            // centre here made the global interaction arbiter reject the
            // offer after the player crossed the centre of a wide/scaled
            // trigger, even though both colliders were still overlapping.
            // Offer at the mover position so collider overlap is the single
            // source of truth for both the prompt and the input range.
            ZeldaInteractionArbiter.OfferInteraction(
                this,
                mover,
                interactKey,
                mover.transform.position,
                SetPromptFromArbiter);
        }
        else
        {
            SetPrompt(null);
        }
        if (mover != null && Input.GetKeyDown(interactKey))
        {
            ZeldaInteractionArbiter.Submit(
                this,
                mover,
                interactKey,
                mover.transform.position,
                BeginTransition);
        }
    }

    private void SetPromptFromArbiter(bool visible)
    {
        SetPrompt(visible ? GetNearbyControlledMover() : null);
    }

    private ZeldaFourWayMover GetNearbyControlledMover()
    {
        ZeldaFourWayMover mover = ZeldaRuntimeRegistry.GetControlledMover();
        if (mover == null || !OverlapsInteractionArea(mover))
        {
            return null;
        }

        ZeldaCharacterData data = mover.GetComponent<ZeldaCharacterData>();
        return data == null || !data.IsDead ? mover : null;
    }

    private bool OverlapsInteractionArea(ZeldaFourWayMover mover)
    {
        if (mover == null)
        {
            return false;
        }

        if (triggerCollider == null)
        {
            triggerCollider = GetComponent<BoxCollider2D>();
        }

        if (triggerCollider == null || !triggerCollider.enabled)
        {
            return false;
        }

        Collider2D moverCollider = mover.GetComponent<Collider2D>();
        if (moverCollider == null || !moverCollider.enabled ||
            !moverCollider.gameObject.activeInHierarchy)
        {
            // All normal Zelda movers have a body collider, but retain a
            // point fallback for a custom mover whose collider was removed.
            return triggerCollider.OverlapPoint(mover.transform.position);
        }

        ColliderDistance2D distance =
            triggerCollider.Distance(moverCollider);
        return distance.isValid &&
            (distance.isOverlapped || distance.distance <= 0.001f);
    }

    private void BeginTransition()
    {
        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            Debug.LogWarning(
                "PersistentSceneTransitionPoint needs a target scene name.",
                this);
            return;
        }

        isTransitioning = true;
        SetPrompt(null);
        SceneTravelStateManager.GetOrCreate().TravelToScene(
            targetSceneName,
            targetArrivalPosition);
    }

    private void SetPrompt(ZeldaFourWayMover mover)
    {
        if (mover == null)
        {
            if (promptObject != null && promptObject.activeSelf)
            {
                promptObject.SetActive(false);
            }
            return;
        }

        EnsurePrompt();
        if (promptObject == null)
        {
            return;
        }

        promptObject.transform.position =
            mover.GetOverheadWorldPosition(interactionPromptOffset);
        promptObject.transform.rotation = Quaternion.identity;
        if (!promptObject.activeSelf)
        {
            promptObject.SetActive(true);
        }

        string promptMessage = BuildPromptMessage();
        promptText.text = promptMessage;
        promptFont.RequestCharactersInTexture(
            promptMessage,
            72,
            FontStyle.Normal);
        promptMaterial.mainTexture = promptFont.material.mainTexture;
    }

    private void EnsurePrompt()
    {
        if (promptObject != null)
        {
            return;
        }

        ZeldaHealthHeartsUI ui = ZeldaHealthHeartsUI.Instance;
        promptFont = ui != null ? ui.PermissionLabelFont : null;
        if (promptFont == null)
        {
            return;
        }

        string promptMessage = BuildPromptMessage();
        promptFont.RequestCharactersInTexture(
            promptMessage,
            72,
            FontStyle.Normal);
        promptObject = new GameObject(name + " Persistent Transition Prompt");
        promptText = promptObject.AddComponent<TextMesh>();
        promptText.text = promptMessage;
        promptText.font = promptFont;
        promptText.fontSize = 72;
        promptText.characterSize = 0.035f;
        promptText.anchor = TextAnchor.MiddleCenter;
        promptText.alignment = TextAlignment.Center;
        promptText.color = ZeldaUiPalette.Primary;

        MeshRenderer renderer = promptObject.GetComponent<MeshRenderer>();
        int highestLayerId = 0;
        int highestLayerValue = int.MinValue;
        foreach (SortingLayer sortingLayer in SortingLayer.layers)
        {
            if (sortingLayer.value > highestLayerValue)
            {
                highestLayerValue = sortingLayer.value;
                highestLayerId = sortingLayer.id;
            }
        }

        renderer.sortingLayerID = highestLayerId;
        renderer.sortingOrder = short.MaxValue - 2;
        ZeldaPossessionProgressBar.ConfigureOverlayRenderer(renderer);
        promptMaterial = new Material(promptFont.material)
        {
            name = name + " Persistent Transition Font Material",
            hideFlags = HideFlags.HideAndDontSave
        };
        promptMaterial.mainTexture = promptFont.material.mainTexture;
        renderer.sharedMaterial = promptMaterial;
    }

    private string BuildPromptMessage()
    {
        string areaName = string.IsNullOrWhiteSpace(destinationAreaName)
            ? "目标区域"
            : destinationAreaName.Trim();
        return "按[E]前往 " + areaName;
    }

    private void SynchronizeCollider()
    {
        if (triggerCollider == null)
        {
            triggerCollider = GetComponent<BoxCollider2D>();
        }

        triggerCollider.isTrigger = true;
        triggerCollider.size = new Vector2(
            Mathf.Max(0.05f, triggerSize.x),
            Mathf.Max(0.05f, triggerSize.y));
        triggerCollider.offset = triggerOffset;
    }

    private void OnDisable()
    {
        SetPrompt(null);
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

    private void OnValidate()
    {
        destinationAreaName = string.IsNullOrWhiteSpace(destinationAreaName)
            ? "目标区域"
            : destinationAreaName.Trim();
        triggerSize.x = Mathf.Max(0.05f, triggerSize.x);
        triggerSize.y = Mathf.Max(0.05f, triggerSize.y);
        SynchronizeCollider();
    }

    private void OnDrawGizmos()
    {
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = editorBorderColor;
        Gizmos.DrawWireCube(triggerOffset, triggerSize);
        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }
}
