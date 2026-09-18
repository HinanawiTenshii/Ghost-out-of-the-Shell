using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
public class SceneTransitionPoint : MonoBehaviour
{
    [SerializeField] private string targetSceneName = "NextScene";
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField, Tooltip("Exact local-space size shared by the trigger collider and interaction test.")]
    private Vector2 triggerSize = new Vector2(1.1f, 0.75f);
    [SerializeField, Tooltip("Exact local-space offset shared by the trigger collider and interaction test.")]
    private Vector2 triggerOffset = new Vector2(0f, 0.25f);
    [SerializeField] private Vector2 interactionPromptOffset = new Vector2(0f, 0.9f);

    private SpriteRenderer spriteRenderer;
    private GameObject interactionPromptObject;
    private TextMesh interactionPromptText;
    private Font interactionPromptFont;
    private Material interactionPromptMaterial;
    private BoxCollider2D triggerCollider;

    public string TargetSceneName => targetSceneName;
    public Vector2 TriggerSize => triggerSize;
    public Vector2 TriggerOffset => triggerOffset;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        SynchronizeCollider();

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
                controlledCharacter.transform.position,
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

        // Keep overlap-based interaction available without the HUD/font.
        ZeldaInteractionArbiter.OfferInteraction(
            this,
            controlledCharacter,
            interactKey,
            controlledCharacter.transform.position,
            SetInteractionPromptVisible);
        EnsureInteractionPrompt();
        if (interactionPromptObject == null)
            return;

        interactionPromptObject.transform.position =
            controlledCharacter.GetOverheadWorldPosition(interactionPromptOffset);
        interactionPromptObject.transform.rotation = Quaternion.identity;
        interactionPromptFont.RequestCharactersInTexture(
            "按[E]继续前进",
            72,
            FontStyle.Normal);
        interactionPromptMaterial.mainTexture = interactionPromptFont.material.mainTexture;
    }

    private ZeldaFourWayMover GetActiveControlledCharacterNearby()
    {
        ZeldaFourWayMover mover = ZeldaRuntimeRegistry.GetControlledMover();
        if (mover == null || !OverlapsInteractionArea(mover))
            return null;

        ZeldaCharacterData characterData = mover.GetComponent<ZeldaCharacterData>();
        return characterData == null || !characterData.IsDead ? mover : null;
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
            return triggerCollider.OverlapPoint(mover.transform.position);
        }

        ColliderDistance2D distance = triggerCollider.Distance(moverCollider);
        return distance.isValid &&
            (distance.isOverlapped || distance.distance <= 0.001f);
    }

    private void SynchronizeCollider()
    {
        if (triggerCollider == null)
        {
            triggerCollider = GetComponent<BoxCollider2D>();
        }

        if (triggerCollider == null)
        {
            return;
        }

        triggerCollider.isTrigger = true;
        triggerCollider.size = new Vector2(
            Mathf.Max(0.05f, triggerSize.x),
            Mathf.Max(0.05f, triggerSize.y));
        triggerCollider.offset = triggerOffset;
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
        ZeldaPossessionProgressBar.ConfigureOverlayRenderer(promptRenderer);
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

        RetroSceneLoadReveal.BeginTransition(targetSceneName.Trim(), () =>
        {
            SceneTravelStateManager.GetOrCreate().ResetAllSceneStates();
            SceneManager.LoadScene(targetSceneName.Trim());
        });
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
        // Keep this geometry in sync with
        // RuntimeMiniMapGraphic.AddTransitionIcon: a doorway outline with a
        // left-pointing transition arrow. SceneTransitionPoint represents the
        // non-persistent transition, so it uses the same Ghost-blue tint as
        // the corresponding minimap icon.
        const int width = 32;
        const int height = 40;
        const float pixelsPerUnit = 32f;
        const float lineWidth = 3f;
        Vector2 center = new Vector2(16f, 20f);
        Color iconColor = ZeldaUiPalette.Ghost;

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.name = "Scene Transition Map Icon";
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[width * height];
        DrawLine(pixels, width, height, center + new Vector2(-9f, -14f), center + new Vector2(-9f, 14f), lineWidth, iconColor);
        DrawLine(pixels, width, height, center + new Vector2(-9f, 14f), center + new Vector2(5f, 14f), lineWidth, iconColor);
        DrawLine(pixels, width, height, center + new Vector2(5f, 14f), center + new Vector2(5f, 5f), lineWidth, iconColor);
        DrawLine(pixels, width, height, center + new Vector2(5f, -5f), center + new Vector2(5f, -14f), lineWidth, iconColor);
        DrawLine(pixels, width, height, center + new Vector2(5f, -14f), center + new Vector2(-9f, -14f), lineWidth, iconColor);

        DrawLine(pixels, width, height, center + new Vector2(13f, 5f), center + new Vector2(-1f, 5f), lineWidth, iconColor);
        DrawLine(pixels, width, height, center + new Vector2(13f, -5f), center + new Vector2(-1f, -5f), lineWidth, iconColor);
        DrawLine(pixels, width, height, center + new Vector2(13f, 5f), center + new Vector2(13f, -5f), lineWidth, iconColor);
        DrawLine(pixels, width, height, center + new Vector2(-1f, 9f), center + new Vector2(-8f, 0f), lineWidth, iconColor);
        DrawLine(pixels, width, height, center + new Vector2(-8f, 0f), center + new Vector2(-1f, -9f), lineWidth, iconColor);
        DrawLine(pixels, width, height, center + new Vector2(-1f, 9f), center + new Vector2(-1f, 5f), lineWidth, iconColor);
        DrawLine(pixels, width, height, center + new Vector2(-1f, -9f), center + new Vector2(-1f, -5f), lineWidth, iconColor);

        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(
            texture,
            new Rect(0, 0, width, height),
            new Vector2(0.5f, 0.3f),
            pixelsPerUnit);
    }

    private static void DrawLine(
        Color[] pixels,
        int textureWidth,
        int textureHeight,
        Vector2 start,
        Vector2 end,
        float lineWidth,
        Color color)
    {
        float radius = lineWidth * 0.5f;
        int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(start.x, end.x) - radius));
        int maxX = Mathf.Min(textureWidth - 1, Mathf.CeilToInt(Mathf.Max(start.x, end.x) + radius));
        int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(start.y, end.y) - radius));
        int maxY = Mathf.Min(textureHeight - 1, Mathf.CeilToInt(Mathf.Max(start.y, end.y) + radius));
        Vector2 segment = end - start;
        float segmentLengthSquared = segment.sqrMagnitude;
        float radiusSquared = radius * radius;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector2 point = new Vector2(x + 0.5f, y + 0.5f);
                float t = segmentLengthSquared <= Mathf.Epsilon
                    ? 0f
                    : Mathf.Clamp01(Vector2.Dot(point - start, segment) / segmentLengthSquared);
                Vector2 closest = start + segment * t;
                if ((point - closest).sqrMagnitude <= radiusSquared)
                {
                    pixels[y * textureWidth + x] = color;
                }
            }
        }
    }

    private void OnValidate()
    {
        triggerSize.x = Mathf.Max(0.05f, triggerSize.x);
        triggerSize.y = Mathf.Max(0.05f, triggerSize.y);
        SynchronizeCollider();
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
