using System;
using UnityEngine;

/// <summary>
/// Accepts three specific persistent pickup identities over multiple
/// interactions, then creates one configured pickup reward at this object.
/// </summary>
public sealed class SpecificItemSubmissionStation : MonoBehaviour
{
    [Serializable]
    public struct RuntimeState
    {
        public bool firstItemSubmitted;
        public bool secondItemSubmitted;
        public bool thirdItemSubmitted;
        public bool rewardAvailable;
    }

    private const int RequiredItemCount = 3;

    [Header("Required Specific Items")]
    [SerializeField] private PickupItemPersistentIdentity requiredItemIdentity1;
    [SerializeField] private PickupItemPersistentIdentity requiredItemIdentity2;
    [SerializeField] private PickupItemPersistentIdentity requiredItemIdentity3;

    [Header("Generated Reward")]
    [SerializeField] private PickupItemBase rewardPrefab;

    [Header("Interaction")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField, Min(0.1f)] private float interactionDistance = 1.2f;
    [SerializeField] private Vector2 interactionPromptOffset = new Vector2(0f, 0.9f);

    private readonly bool[] submittedItems = new bool[RequiredItemCount];
    private bool rewardAvailable;
    private PickupItemBase generatedReward;
    private GameObject interactionPromptObject;
    private TextMesh interactionPromptText;
    private Font interactionPromptFont;
    private Material interactionPromptMaterial;

    public int SubmittedItemCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < submittedItems.Length; i++)
            {
                if (submittedItems[i])
                {
                    count++;
                }
            }

            return count;
        }
    }

    public bool IsComplete => SubmittedItemCount >= RequiredItemCount;

    private void Update()
    {
        if (DocumentReader.IsInputBlocked || IsComplete)
        {
            return;
        }

        ZeldaFourWayMover controlledMover = FindNearbyControlledMover();
        if (controlledMover != null && Input.GetKeyDown(interactKey))
        {
            ZeldaInteractionArbiter.Submit(
                this,
                controlledMover,
                interactKey,
                transform.position,
                SubmitFromInteraction);
        }
    }

    private void SubmitFromInteraction()
    {
        TrySubmitOneAvailableItem();
    }

    private void LateUpdate()
    {
        UpdateInteractionPrompt();
    }

    public bool TrySubmitOneAvailableItem()
    {
        if (IsComplete)
        {
            return false;
        }

        PersistentInventory inventory = PersistentInventory.Instance;
        if (inventory == null)
        {
            return false;
        }

        for (int i = 0; i < RequiredItemCount; i++)
        {
            if (submittedItems[i])
            {
                continue;
            }

            PickupItemPersistentIdentity identity = GetRequiredIdentity(i);
            if (identity == null)
            {
                continue;
            }

            string instanceId = identity.StableInstanceId;
            if (!inventory.ContainsItemInstance(instanceId) ||
                !inventory.TryRemoveItemInstance(instanceId))
            {
                continue;
            }

            submittedItems[i] = true;
            if (IsComplete)
            {
                rewardAvailable = true;
                SpawnRewardIfNeeded();
                SetInteractionPromptVisible(false);
                QuestJournalManager.GetOrCreate().EvaluateKnownCompletionConditions();
            }

            return true;
        }

        return false;
    }

    public RuntimeState CaptureRuntimeState()
    {
        return new RuntimeState
        {
            firstItemSubmitted = submittedItems[0],
            secondItemSubmitted = submittedItems[1],
            thirdItemSubmitted = submittedItems[2],
            rewardAvailable = rewardAvailable
        };
    }

    public void ApplyRuntimeState(RuntimeState state)
    {
        submittedItems[0] = state.firstItemSubmitted;
        submittedItems[1] = state.secondItemSubmitted;
        submittedItems[2] = state.thirdItemSubmitted;
        rewardAvailable = state.rewardAvailable;

        if (IsComplete && rewardAvailable)
        {
            SpawnRewardIfNeeded();
            QuestJournalManager.GetOrCreate().EvaluateKnownCompletionConditions();
        }
        else
        {
            RemoveGeneratedReward();
        }
    }

    private void SpawnRewardIfNeeded()
    {
        if (!rewardAvailable || rewardPrefab == null || generatedReward != null)
        {
            return;
        }

        GameObject rewardObject = Instantiate(
            rewardPrefab.gameObject,
            transform.position,
            Quaternion.identity);
        if (!rewardObject.activeSelf)
        {
            rewardObject.SetActive(true);
        }

        generatedReward = rewardObject.GetComponent<PickupItemBase>();
        if (generatedReward != null)
        {
            generatedReward.PickedUp += OnGeneratedRewardPickedUp;
        }
    }

    private void OnGeneratedRewardPickedUp(PickupItemBase pickedUpItem)
    {
        if (pickedUpItem != null)
        {
            pickedUpItem.PickedUp -= OnGeneratedRewardPickedUp;
        }

        if (generatedReward == pickedUpItem)
        {
            generatedReward = null;
        }

        rewardAvailable = false;
    }

    private void RemoveGeneratedReward()
    {
        if (generatedReward == null)
        {
            return;
        }

        generatedReward.PickedUp -= OnGeneratedRewardPickedUp;
        Destroy(generatedReward.gameObject);
        generatedReward = null;
    }

    private PickupItemPersistentIdentity GetRequiredIdentity(int index)
    {
        switch (index)
        {
            case 0: return requiredItemIdentity1;
            case 1: return requiredItemIdentity2;
            case 2: return requiredItemIdentity3;
            default: return null;
        }
    }

    private bool HasAvailableRequiredItem()
    {
        PersistentInventory inventory = PersistentInventory.Instance;
        if (inventory == null)
        {
            return false;
        }

        for (int i = 0; i < RequiredItemCount; i++)
        {
            PickupItemPersistentIdentity identity = GetRequiredIdentity(i);
            if (!submittedItems[i] && identity != null &&
                inventory.ContainsItemInstance(identity.StableInstanceId))
            {
                return true;
            }
        }

        return false;
    }

    private ZeldaFourWayMover FindNearbyControlledMover()
    {
        ZeldaFourWayMover mover = ZeldaRuntimeRegistry.GetControlledMover();
        if (mover == null || PickupItemBase.IsGhostControlledMover(mover) ||
            Vector2.Distance(transform.position, mover.transform.position) >
            interactionDistance)
        {
            return null;
        }

        ZeldaCharacterData characterData = mover.GetComponent<ZeldaCharacterData>();
        return characterData == null || !characterData.IsDead ? mover : null;
    }

    private void UpdateInteractionPrompt()
    {
        ZeldaFourWayMover mover = DocumentReader.IsInputBlocked || IsComplete
            ? null
            : FindNearbyControlledMover();
        if (mover == null)
        {
            SetInteractionPromptVisible(false);
            return;
        }

        string promptMessage = HasAvailableRequiredItem()
            ? $"按[E]提交物品  {SubmittedItemCount}/{RequiredItemCount}"
            : $"缺少指定物品  {SubmittedItemCount}/{RequiredItemCount}";
        // A missing prompt font must not remove this gameplay candidate.
        ZeldaInteractionArbiter.OfferInteraction(
            this,
            mover,
            interactKey,
            transform.position,
            SetInteractionPromptVisible);
        EnsureInteractionPrompt();
        if (interactionPromptObject == null)
        {
            return;
        }

        interactionPromptText.text = promptMessage;
        interactionPromptObject.transform.position =
            mover.GetOverheadWorldPosition(interactionPromptOffset);
        interactionPromptObject.transform.rotation = Quaternion.identity;
        interactionPromptFont.RequestCharactersInTexture(
            promptMessage,
            72,
            FontStyle.Normal);
        interactionPromptMaterial.mainTexture =
            interactionPromptFont.material.mainTexture;
    }

    private void EnsureInteractionPrompt()
    {
        if (interactionPromptObject != null)
        {
            return;
        }

        ZeldaHealthHeartsUI ui = ZeldaHealthHeartsUI.Instance;
        interactionPromptFont = ui != null ? ui.PermissionLabelFont : null;
        if (interactionPromptFont == null)
        {
            return;
        }

        const string promptCharacters =
            "按[E]提交物品缺少指定物品0123/ ";
        interactionPromptFont.RequestCharactersInTexture(
            promptCharacters,
            72,
            FontStyle.Normal);

        interactionPromptObject = new GameObject(name + " Submission Prompt");
        interactionPromptText = interactionPromptObject.AddComponent<TextMesh>();
        interactionPromptText.font = interactionPromptFont;
        interactionPromptText.fontSize = 72;
        interactionPromptText.characterSize = 0.035f;
        interactionPromptText.anchor = TextAnchor.MiddleCenter;
        interactionPromptText.alignment = TextAlignment.Center;
        interactionPromptText.color = ZeldaUiPalette.Primary;

        MeshRenderer promptRenderer =
            interactionPromptObject.GetComponent<MeshRenderer>();
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
            name = name + " Submission Prompt Font Material",
            hideFlags = HideFlags.HideAndDontSave
        };
        interactionPromptMaterial.mainTexture =
            interactionPromptFont.material.mainTexture;
        promptRenderer.sharedMaterial = interactionPromptMaterial;
        interactionPromptObject.SetActive(false);
    }

    private void SetInteractionPromptVisible(bool visible)
    {
        if (interactionPromptObject != null &&
            interactionPromptObject.activeSelf != visible)
        {
            interactionPromptObject.SetActive(visible);
        }
    }

    private void OnDisable()
    {
        SetInteractionPromptVisible(false);
    }

    private void OnDestroy()
    {
        if (generatedReward != null)
        {
            generatedReward.PickedUp -= OnGeneratedRewardPickedUp;
        }

        if (interactionPromptObject != null)
        {
            Destroy(interactionPromptObject);
        }

        if (interactionPromptMaterial != null)
        {
            Destroy(interactionPromptMaterial);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        interactionDistance = Mathf.Max(0.1f, interactionDistance);
    }
#endif
}
