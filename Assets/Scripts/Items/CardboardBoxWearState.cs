using System.Collections.Generic;
using UnityEngine;

/// <summary>Runtime disguise attached to the currently controlled character.</summary>
public sealed class CardboardBoxWearState : MonoBehaviour
{
    private const float TransitionAnimationDuration = 0.2f;
    private const float TransitionAnimationDistance = 0.22f;

    private ZeldaFourWayMover mover;
    private ZeldaCharacterData characterData;
    private string sourceItemId;
    private float staminaDrainPerSecond;
    private float movementSpeedMultiplier = 0.5f;
    private Vector2 wornVisualOffset;
    private int sourceMaximumStackSize = 3;
    private int fragmentCount = 14;
    private float fragmentLifetime = 0.7f;
    private float fragmentSpeed = 2.8f;
    private float fragmentScale = 0.13f;
    private GameObject visualObject;
    private SpriteRenderer visualRenderer;
    private Sprite runtimeSprite;
    private Texture2D runtimeTexture;
    private int remainingBlockedAttacks;
    private float damageFlashTimer;
    private bool exiting;
    private bool itemReturned;
    private bool suppressItemReturn;
    private bool visualFacesRight;
    private bool enterAnimationPlaying;
    private float enterAnimationElapsed;
    private float exitAnimationElapsed;
    private Vector3 exitAnimationStartPosition;
    private bool returnItemAfterExitAnimation = true;
    private readonly List<SpriteRenderer> hiddenCharacterRenderers =
        new List<SpriteRenderer>();
    private readonly List<bool> hiddenCharacterRendererStates =
        new List<bool>();

    public float MovementSpeedMultiplier => movementSpeedMultiplier;
    public bool IsStationary => mover == null || !mover.IsMoving;
    public ZeldaFourWayMover WearerMover => mover;
    public Vector2 WorldCenter => transform.position;
    public int RemainingBlockedAttacks => remainingBlockedAttacks;
    public string SaveSourceItemId => sourceItemId;

    public void Configure(
        CardboardBoxPickupItem source,
        ZeldaFourWayMover controlledMover)
    {
        mover = controlledMover;
        characterData = GetComponent<ZeldaCharacterData>();
        if (source != null)
        {
            sourceItemId = source.ItemId;
            staminaDrainPerSecond = source.StaminaDrainPerSecond;
            movementSpeedMultiplier = source.WornMovementSpeedMultiplier;
            wornVisualOffset = source.WornVisualOffset;
            sourceMaximumStackSize = source.MaxStackSize;
            remainingBlockedAttacks = source.MaximumBlockedAttacks;
            fragmentCount = source.FragmentCount;
            fragmentLifetime = source.FragmentLifetime;
            fragmentSpeed = source.FragmentSpeed;
            fragmentScale = source.FragmentScale;
        }
        else
        {
            remainingBlockedAttacks = 2;
        }
        HideCharacterVisuals();
        CreateVisual();
        mover?.RegisterCardboardBox(this);
    }

    private void Update()
    {
        if (mover == null || characterData == null ||
            characterData.IsDead || !mover.isActiveAndEnabled)
        {
            CompleteExit(true);
            return;
        }

        if (exiting)
        {
            UpdateExitAnimation();
            return;
        }

        float remainingStamina = mover.ConsumeStamina(
            Mathf.Max(0f, staminaDrainPerSecond) * Time.deltaTime);
        if (remainingStamina <= 0f)
        {
            BeginAnimatedExit(true);
            return;
        }

        UpdateEnterAnimation();
        UpdateVisualFacing();

        if (damageFlashTimer > 0f)
        {
            damageFlashTimer = Mathf.Max(0f, damageFlashTimer - Time.deltaTime);
            if (visualRenderer != null)
            {
                visualRenderer.color = Mathf.FloorToInt(
                    damageFlashTimer * 30f) % 2 == 0
                    ? Color.white
                    : new Color(1f, 0.35f, 0.25f, 1f);
            }
        }
        else if (visualRenderer != null)
        {
            visualRenderer.color = Color.white;
        }
    }

    public bool TryBlockAttack(ZeldaCharacterData attacker)
    {
        if (exiting || remainingBlockedAttacks <= 0)
        {
            return false;
        }

        remainingBlockedAttacks--;
        damageFlashTimer = 0.22f;
        if (remainingBlockedAttacks <= 0)
        {
            BreakWornBox();
        }

        return true;
    }

    private void BreakWornBox()
    {
        SpawnBreakFragments();
        CompleteExit(false);
    }

    public void RequestExit()
    {
        BeginAnimatedExit(true);
    }
    public void ExitForGhostForm() => CompleteExit(true);

    private void BeginAnimatedExit(bool returnToInventory)
    {
        if (exiting)
        {
            return;
        }

        exiting = true;
        enterAnimationPlaying = false;
        returnItemAfterExitAnimation = returnToInventory;
        exitAnimationElapsed = 0f;
        exitAnimationStartPosition = visualObject != null
            ? visualObject.transform.localPosition
            : wornVisualOffset;
    }

    private void UpdateEnterAnimation()
    {
        if (!enterAnimationPlaying || visualObject == null)
        {
            return;
        }

        enterAnimationElapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(
            enterAnimationElapsed / TransitionAnimationDuration);
        float easedProgress = Mathf.SmoothStep(0f, 1f, progress);
        Vector3 start = wornVisualOffset +
            Vector2.up * TransitionAnimationDistance;
        visualObject.transform.localPosition = Vector3.Lerp(
            start,
            wornVisualOffset,
            easedProgress);
        if (progress >= 1f)
        {
            enterAnimationPlaying = false;
            visualObject.transform.localPosition = wornVisualOffset;
        }
    }

    private void UpdateExitAnimation()
    {
        if (visualObject == null)
        {
            CompleteExit(returnItemAfterExitAnimation);
            return;
        }

        exitAnimationElapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(
            exitAnimationElapsed / TransitionAnimationDuration);
        float easedProgress = Mathf.SmoothStep(0f, 1f, progress);
        Vector3 end = wornVisualOffset +
            Vector2.up * TransitionAnimationDistance;
        visualObject.transform.localPosition = Vector3.Lerp(
            exitAnimationStartPosition,
            end,
            easedProgress);
        if (progress >= 1f)
        {
            CompleteExit(returnItemAfterExitAnimation);
        }
    }

    private void CompleteExit(bool returnToInventory)
    {
        if (suppressItemReturn || itemReturned)
        {
            return;
        }

        exiting = true;
        suppressItemReturn = !returnToInventory;
        mover?.UnregisterCardboardBox(this);
        RestoreCharacterVisuals();
        if (returnToInventory)
        {
            ReturnBoxToInventory();
        }

        Destroy(this);
    }

    private void ReturnBoxToInventory()
    {
        if (suppressItemReturn || itemReturned ||
            string.IsNullOrWhiteSpace(sourceItemId))
        {
            return;
        }

        PersistentInventory inventory = PersistentInventory.Instance;
        if (inventory != null && inventory.TryAddItem(
            sourceItemId,
            1,
            Mathf.Max(1, sourceMaximumStackSize)))
        {
            itemReturned = true;
            return;
        }

        // A completely full inventory must not delete the box. This fallback
        // is only used when no inventory slot can accept the returned item.
        CardboardBoxPickupItem groundBox =
            CardboardBoxPickupItem.SpawnGroundBox(
                sourceItemId,
                transform.position,
                Mathf.Max(1, remainingBlockedAttacks));
        itemReturned = groundBox != null;
    }

    private void HideCharacterVisuals()
    {
        hiddenCharacterRenderers.Clear();
        hiddenCharacterRendererStates.Clear();

        Transform characterVisualRoot = transform.Find("Visual");
        SpriteRenderer rootRenderer = GetComponent<SpriteRenderer>();
        SpriteRenderer[] renderers =
            GetComponentsInChildren<SpriteRenderer>(true);
        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer == null)
            {
                continue;
            }

            bool isCharacterVisual = renderer == rootRenderer ||
                (characterVisualRoot != null &&
                 (renderer.transform == characterVisualRoot ||
                  renderer.transform.IsChildOf(characterVisualRoot)));
            if (!isCharacterVisual)
            {
                continue;
            }

            hiddenCharacterRenderers.Add(renderer);
            hiddenCharacterRendererStates.Add(renderer.enabled);
            renderer.enabled = false;
        }
    }

    private void RestoreCharacterVisuals()
    {
        for (int i = 0; i < hiddenCharacterRenderers.Count; i++)
        {
            SpriteRenderer renderer = hiddenCharacterRenderers[i];
            if (renderer != null)
            {
                renderer.enabled = hiddenCharacterRendererStates[i];
            }
        }
        hiddenCharacterRenderers.Clear();
        hiddenCharacterRendererStates.Clear();
    }

    private void CreateVisual()
    {
        CardboardBoxPickupItem source =
            PersistentInventory.Instance != null
                ? PersistentInventory.Instance.ResolveItemPrefab(sourceItemId)
                    as CardboardBoxPickupItem
                : null;
        CardboardBoxPickupItemVisual boxVisual = source != null
            ? source.GetComponent<CardboardBoxPickupItemVisual>()
            : null;
        if (boxVisual == null)
        {
            return;
        }

        visualObject = new GameObject("Worn Cardboard Box Visual");
        visualObject.transform.SetParent(transform, false);
        visualObject.transform.localPosition = wornVisualOffset +
            Vector2.up * TransitionAnimationDistance;
        Vector3 parentScale = transform.lossyScale;
        float desiredWorldScale = Mathf.Max(0.01f, boxVisual.WorldScale);
        visualObject.transform.localScale = new Vector3(
            desiredWorldScale / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)),
            desiredWorldScale / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)),
            1f);
        visualRenderer = visualObject.AddComponent<SpriteRenderer>();
        runtimeSprite = boxVisual.CreateStandaloneSprite(out runtimeTexture);
        visualRenderer.sprite = runtimeSprite;
        visualRenderer.flipX = false;

        SpriteRenderer characterRenderer =
            GetComponentInChildren<SpriteRenderer>();
        if (characterRenderer != null)
        {
            visualRenderer.sortingLayerID = characterRenderer.sortingLayerID;
            visualRenderer.sortingOrder = characterRenderer.sortingOrder + 8;
        }

        UpdateVisualFacing();
        enterAnimationElapsed = 0f;
        enterAnimationPlaying = true;
    }

    private void UpdateVisualFacing()
    {
        if (mover == null || visualRenderer == null)
        {
            return;
        }

        float horizontalFacing = mover.FacingDirection.x;
        if (horizontalFacing > 0.01f)
        {
            visualFacesRight = true;
        }
        else if (horizontalFacing < -0.01f)
        {
            visualFacesRight = false;
        }

        // The authored sprite is the default left-facing version.
        visualRenderer.flipX = visualFacesRight;
    }

    private void SpawnBreakFragments()
    {
        int count = Mathf.Max(1, fragmentCount);
        for (int i = 0; i < count; i++)
        {
            float angle = 360f * i / count + Random.Range(-12f, 12f);
            Vector2 direction = new Vector2(
                Mathf.Cos(angle * Mathf.Deg2Rad),
                Mathf.Sin(angle * Mathf.Deg2Rad));
            GameObject fragmentObject = new GameObject("Cardboard Fragment");
            fragmentObject.transform.position = transform.position;
            fragmentObject.transform.localScale = Vector3.one *
                fragmentScale * Random.Range(0.7f, 1.25f);
            SpriteRenderer fragmentRenderer =
                fragmentObject.AddComponent<SpriteRenderer>();
            fragmentRenderer.sprite = DoorFragmentVisual.FragmentSprite;
            fragmentRenderer.color = new Color(0.72f, 0.55f, 0.32f, 1f);
            if (visualRenderer != null)
            {
                fragmentRenderer.sortingLayerID = visualRenderer.sortingLayerID;
                fragmentRenderer.sortingOrder = visualRenderer.sortingOrder + 1;
            }
            DoorFragmentVisual fragment =
                fragmentObject.AddComponent<DoorFragmentVisual>();
            fragment.Configure(
                direction * fragmentSpeed * Random.Range(0.7f, 1.2f),
                fragmentLifetime,
                Random.Range(-540f, 540f));
        }
    }

    private void OnDestroy()
    {
        mover?.UnregisterCardboardBox(this);
        RestoreCharacterVisuals();
        ReturnBoxToInventory();
        if (visualObject != null)
        {
            Destroy(visualObject);
        }
        if (runtimeSprite != null)
        {
            Destroy(runtimeSprite);
        }
        if (runtimeTexture != null)
        {
            Destroy(runtimeTexture);
        }
    }
}
