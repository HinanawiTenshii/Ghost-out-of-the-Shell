using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scene-only marker for an interaction that can add a quest journal entry.
/// It deliberately uses world SpriteRenderers, so it is never captured by the
/// full map or real-time mini-map UI.
/// </summary>
public sealed class QuestJournalInteractionMarker : MonoBehaviour
{
    private struct MarkerChange
    {
        public QuestJournalEventOperation operation;
        public string entryId;
        public string title;
        public string details;
    }

    private const int WhiteBorderOrder = 29997;
    private const int DarkCyanOrder = 29998;
    private const int SkyBlueOrder = 29999;

    private static Texture2D sharedTexture;
    private static Sprite sharedSprite;

    [SerializeField, Tooltip("World-space offset from the interaction object.")]
    private Vector2 markerOffset = new Vector2(0f, 0.72f);

    private readonly List<MarkerChange> markerChanges = new List<MarkerChange>();
    private GameObject markerRoot;
    private QuestJournalManager journal;
    private Transform positionAnchor;
    private bool useDoorChildPosition;

    public static QuestJournalInteractionMarker Configure(
        GameObject owner,
        bool addDirectEntry,
        string directEntryId,
        QuestJournalEventChange[] changes)
    {
        if (owner == null || !HasRelevantChange(addDirectEntry, directEntryId, changes))
            return null;

        QuestJournalInteractionMarker marker =
            owner.GetComponent<QuestJournalInteractionMarker>();
        if (marker == null)
        {
            marker = owner.AddComponent<QuestJournalInteractionMarker>();
        }

        marker.SetEntrySources(addDirectEntry, directEntryId, changes);
        return marker;
    }

    private static bool HasRelevantChange(
        bool addDirectEntry,
        string directEntryId,
        QuestJournalEventChange[] changes)
    {
        if (addDirectEntry && !string.IsNullOrWhiteSpace(directEntryId))
            return true;

        if (changes == null)
            return false;

        for (int index = 0; index < changes.Length; index++)
        {
            QuestJournalEventOperation operation = changes[index].operation;
            if ((operation == QuestJournalEventOperation.AddOrUpdate ||
                 operation == QuestJournalEventOperation.UpdateExisting ||
                 operation == QuestJournalEventOperation.AppendDetails) &&
                !string.IsNullOrWhiteSpace(changes[index].entryId))
            {
                return true;
            }
        }

        return false;
    }

    private void SetEntrySources(
        bool addDirectEntry,
        string directEntryId,
        QuestJournalEventChange[] changes)
    {
        ResolvePositionAnchor();
        markerChanges.Clear();
        if (addDirectEntry)
        {
            AddMarkerChange(
                QuestJournalEventOperation.AddOrUpdate,
                directEntryId,
                string.Empty,
                string.Empty);
        }

        if (changes != null)
        {
            for (int index = 0; index < changes.Length; index++)
            {
                QuestJournalEventOperation operation = changes[index].operation;
                if (operation == QuestJournalEventOperation.AddOrUpdate ||
                    operation == QuestJournalEventOperation.UpdateExisting ||
                    operation == QuestJournalEventOperation.AppendDetails)
                {
                    AddMarkerChange(
                        operation,
                        changes[index].entryId,
                        changes[index].title,
                        changes[index].details);
                }
            }
        }

        EnsureMarkerVisual();
        ConnectJournal();
        RefreshVisibility();
    }

    private void AddMarkerChange(
        QuestJournalEventOperation operation,
        string entryId,
        string title,
        string details)
    {
        if (string.IsNullOrWhiteSpace(entryId))
            return;

        markerChanges.Add(new MarkerChange
        {
            operation = operation,
            entryId = entryId.Trim(),
            title = title ?? string.Empty,
            details = details ?? string.Empty
        });
    }

    private void OnEnable()
    {
        ConnectJournal();
        RefreshVisibility();
    }

    private void OnDisable()
    {
        if (markerRoot != null)
        {
            markerRoot.SetActive(false);
        }
        DisconnectJournal();
    }

    private void OnDestroy()
    {
        DisconnectJournal();
        if (markerRoot != null)
        {
            Destroy(markerRoot);
        }
    }

    private void LateUpdate()
    {
        if (markerRoot != null)
        {
            markerRoot.transform.position = GetAnchorPosition();
        }
    }

    private void ResolvePositionAnchor()
    {
        positionAnchor = transform;
        useDoorChildPosition = false;
        if (GetComponent<DoorHingeInteraction>() == null)
            return;

        // DoorHingeInteraction is normally placed on the hinge root, while
        // the visible door panel is its child. Prefer the explicitly named
        // child, then fall back to the first direct child so prefab variants
        // require no additional scene configuration.
        Transform namedDoor = FindChildRecursively(transform, "Door");
        if (namedDoor != null)
        {
            positionAnchor = namedDoor;
            useDoorChildPosition = true;
        }
        else if (transform.childCount > 0)
        {
            positionAnchor = transform.GetChild(0);
            useDoorChildPosition = true;
        }
    }

    private Vector3 GetAnchorPosition()
    {
        if (positionAnchor == null)
        {
            ResolvePositionAnchor();
        }

        Vector3 anchorPosition = positionAnchor != null
            ? positionAnchor.position
            : transform.position;
        return useDoorChildPosition
            ? anchorPosition
            : anchorPosition + (Vector3)markerOffset;
    }

    private static Transform FindChildRecursively(Transform root, string childName)
    {
        for (int index = 0; index < root.childCount; index++)
        {
            Transform child = root.GetChild(index);
            if (string.Equals(child.name, childName, System.StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }

            Transform nested = FindChildRecursively(child, childName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private void ConnectJournal()
    {
        QuestJournalManager nextJournal = QuestJournalManager.GetOrCreate();
        if (journal == nextJournal)
            return;

        DisconnectJournal();
        journal = nextJournal;
        journal.JournalChanged += RefreshVisibility;
        journal.TrackedQuestChanged += RefreshVisibility;
    }

    private void DisconnectJournal()
    {
        if (journal != null)
        {
            journal.JournalChanged -= RefreshVisibility;
            journal.TrackedQuestChanged -= RefreshVisibility;
            journal = null;
        }
    }

    private void RefreshVisibility()
    {
        if (markerRoot == null)
            return;

        bool shouldShow = false;
        if (journal != null)
        {
            for (int index = 0; index < markerChanges.Count; index++)
            {
                MarkerChange change = markerChanges[index];
                bool entryExists = journal.TryGetEntry(change.entryId, out QuestJournalManager.Entry entry);
                if (!entryExists)
                {
                    // An AddOrUpdate on a missing entry creates a new journal
                    // item and therefore remains visible independently of the
                    // currently tracked quest.
                    if (change.operation == QuestJournalEventOperation.AddOrUpdate)
                    {
                        shouldShow = true;
                        break;
                    }
                    continue;
                }

                // Changes to an existing task's explanation are only relevant
                // while that exact task is being tracked.
                if (!string.Equals(
                        journal.TrackedEntryId,
                        change.entryId,
                        System.StringComparison.Ordinal) ||
                    IsChangeAlreadyApplied(change, entry))
                {
                    continue;
                }

                shouldShow = true;
                break;
            }
        }

        markerRoot.SetActive(shouldShow);
    }

    private static bool IsChangeAlreadyApplied(
        MarkerChange change,
        QuestJournalManager.Entry entry)
    {
        string expectedDetails = (change.details ?? string.Empty).Trim();
        string currentDetails = (entry.Details ?? string.Empty).Trim();
        if (change.operation == QuestJournalEventOperation.AppendDetails)
        {
            return string.IsNullOrEmpty(expectedDetails) ||
                currentDetails.Contains(expectedDetails);
        }

        string expectedTitle = (change.title ?? string.Empty).Trim();
        bool titleMatches = string.IsNullOrEmpty(expectedTitle) ||
            string.Equals(entry.Title, expectedTitle, System.StringComparison.Ordinal);
        bool detailsMatch = string.IsNullOrEmpty(expectedDetails) ||
            string.Equals(currentDetails, expectedDetails, System.StringComparison.Ordinal);
        return titleMatches && detailsMatch;
    }

    private void EnsureMarkerVisual()
    {
        if (markerRoot != null)
            return;

        EnsureSharedSprite();
        markerRoot = new GameObject("Quest Journal Interaction Marker");
        markerRoot.transform.position = GetAnchorPosition();
        markerRoot.transform.rotation = Quaternion.Euler(0f, 0f, 45f);

        CreateLayer("White Border", 0.46f, Color.white, WhiteBorderOrder);
        CreateLayer("Dark Cyan Outer", 0.36f, new Color32(18, 92, 108, 255), DarkCyanOrder);
        CreateLayer("Sky Blue Inner", 0.18f, new Color32(86, 190, 245, 255), SkyBlueOrder);
    }

    private void CreateLayer(string layerName, float size, Color color, int order)
    {
        GameObject layer = new GameObject(layerName, typeof(SpriteRenderer));
        layer.transform.SetParent(markerRoot.transform, false);
        layer.transform.localScale = new Vector3(size, size, 1f);

        SpriteRenderer renderer = layer.GetComponent<SpriteRenderer>();
        renderer.sprite = sharedSprite;
        renderer.color = color;
        renderer.sortingLayerID = 0;
        // The vision mask is order 30000, while UI is rendered later by its
        // composite camera. This keeps the marker below both of them.
        renderer.sortingOrder = order;
    }

    private static void EnsureSharedSprite()
    {
        if (sharedSprite != null)
            return;

        sharedTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            name = "Quest Interaction Marker Texture",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        sharedTexture.SetPixel(0, 0, Color.white);
        sharedTexture.Apply(false, false);
        sharedSprite = Sprite.Create(
            sharedTexture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            1f);
        sharedSprite.name = "Quest Interaction Marker Sprite";
        sharedSprite.hideFlags = HideFlags.HideAndDontSave;
    }
}
