using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
public class SceneTransitionPoint : MonoBehaviour
{
    [SerializeField] private string targetSceneName = "NextScene";
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private float interactionDistance = 1.2f;
    [SerializeField] private Color frameColor = new Color(0.22f, 0.76f, 1f, 1f);
    [SerializeField] private Color centerColor = new Color(0.08f, 0.22f, 0.46f, 0.85f);
    [SerializeField] private Vector2 interactionPromptOffset = new Vector2(0f, 0.9f);

    private SpriteRenderer spriteRenderer;
    private GameObject interactionPromptObject;
    private TextMesh interactionPromptText;
    private Font interactionPromptFont;
    private Material interactionPromptMaterial;

    public string TargetSceneName => targetSceneName;
    public float InteractionDistance => interactionDistance;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        BoxCollider2D boxCollider = GetComponent<BoxCollider2D>();
        boxCollider.isTrigger = true;
        boxCollider.size = new Vector2(1.1f, 0.75f);
        boxCollider.offset = new Vector2(0f, 0.25f);

        ApplyVisual();
    }

    private void Update()
    {
        if (DocumentReader.IsInputBlocked)
        {
            return;
        }

        ZeldaFourWayMover controlledCharacter =
            GetActiveControlledCharacterNearby();
        if (Input.GetKeyDown(interactKey) && controlledCharacter != null)
        {
            ZeldaInteractionArbiter.Submit(
                this,
                controlledCharacter,
                interactKey,
                transform.position,
                LoadTargetScene);
        }
    }

    private void LateUpdate()
    {
        ZeldaFourWayMover controlledCharacter = DocumentReader.IsInputBlocked
            ? null
            : GetActiveControlledCharacterNearby();
        if (controlledCharacter == null)
        {
            SetInteractionPromptVisible(false);
            return;
        }

        EnsureInteractionPrompt();
        if (interactionPromptObject == null)
            return;

        interactionPromptObject.transform.position =
            controlledCharacter.GetOverheadWorldPosition(interactionPromptOffset);
        interactionPromptObject.transform.rotation = Quaternion.identity;
        ZeldaInteractionArbiter.OfferInteraction(
            this,
            controlledCharacter,
            interactKey,
            transform.position,
            SetInteractionPromptVisible);

        interactionPromptFont.RequestCharactersInTexture(
            "按[E]继续前进",
            72,
            FontStyle.Normal);
        interactionPromptMaterial.mainTexture = interactionPromptFont.material.mainTexture;
    }

    private ZeldaFourWayMover GetActiveControlledCharacterNearby()
    {
        ZeldaFourWayMover mover = ZeldaRuntimeRegistry.GetControlledMover();
        if (mover == null ||
            Vector2.Distance(transform.position, mover.transform.position) > interactionDistance)
            return null;

        ZeldaCharacterData characterData = mover.GetComponent<ZeldaCharacterData>();
        return characterData == null || !characterData.IsDead ? mover : null;
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
            "按[E]继续前进",
            72,
            FontStyle.Normal);

        interactionPromptObject = new GameObject(name + " Scene Transition Prompt");
        interactionPromptText = interactionPromptObject.AddComponent<TextMesh>();
        interactionPromptText.text = "按[E]继续前进";
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
            name = name + " Scene Transition Prompt Font Material",
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

    private void LoadTargetScene()
    {
        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            Debug.LogWarning("SceneTransitionPoint needs a target scene name.", this);
            return;
        }

        SceneTravelStateManager.GetOrCreate().ResetAllSceneStates();
        SceneManager.LoadScene(targetSceneName.Trim());
    }

    private void ApplyVisual()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        spriteRenderer.sprite = CreateTransitionSprite();
        spriteRenderer.color = Color.white;
        spriteRenderer.sortingOrder = 1;
    }

    private Sprite CreateTransitionSprite()
    {
        const int width = 20;
        const int height = 16;
        Texture2D texture = new Texture2D(width, height);
        texture.filterMode = FilterMode.Point;

        Color clear = new Color(1f, 1f, 1f, 0f);
        FillRect(texture, 0, 0, width, height, clear);

        FillRect(texture, 4, 3, 12, 2, frameColor);
        FillRect(texture, 3, 5, 14, 2, frameColor);
        FillRect(texture, 2, 7, 16, 3, frameColor);
        FillRect(texture, 4, 8, 12, 1, centerColor);
        FillRect(texture, 6, 9, 8, 2, centerColor);
        FillRect(texture, 8, 11, 4, 2, frameColor);

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
        interactionDistance = Mathf.Max(0f, interactionDistance);
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
