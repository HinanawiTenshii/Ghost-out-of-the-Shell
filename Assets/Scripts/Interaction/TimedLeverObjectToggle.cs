using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Temporarily reverses the active state of up to six objects whenever its
/// configured LeverData sources successfully emit an interaction signal.
/// </summary>
public sealed class TimedLeverObjectToggle : MonoBehaviour
{
    private const int MaximumBoundObjects = 6;
    private const int MaximumBoundLevers = 4;

    [Header("Source Levers (Maximum 4)")]
    [SerializeField, InspectorName("Source Lever 1")]
    private LeverData sourceLever;
    [SerializeField, InspectorName("Source Lever 2")]
    private LeverData sourceLever2;
    [SerializeField, InspectorName("Source Lever 3")]
    private LeverData sourceLever3;
    [SerializeField, InspectorName("Source Lever 4")]
    private LeverData sourceLever4;
    [SerializeField, Min(0.01f)] private float toggleDuration = 3f;
    [SerializeField] private GameObject[] boundObjects = new GameObject[0];

    private readonly List<GameObject> toggledObjects =
        new List<GameObject>(MaximumBoundObjects);
    private readonly List<bool> originalActiveStates =
        new List<bool>(MaximumBoundObjects);
    private float remainingDuration;
    private bool temporaryToggleActive;

    public LeverData SourceLever => sourceLever;
    public int MaximumSourceLevers => MaximumBoundLevers;
    public float ToggleDuration => toggleDuration;
    public bool IsTemporaryToggleActive => temporaryToggleActive;
    [System.Serializable] public sealed class SaveState
    {
        public float remaining;
        public int[] indices;
        public bool[] original;
    }
    public SaveState CaptureSaveState()
    {
        var indices = new List<int>();
        var states = new List<bool>();
        for (int i = 0; i < toggledObjects.Count; i++)
        {
            int index = System.Array.IndexOf(boundObjects, toggledObjects[i]);
            if (index < 0) continue;
            indices.Add(index); states.Add(originalActiveStates[i]);
        }
        return new SaveState { remaining = remainingDuration, indices = indices.ToArray(), original = states.ToArray() };
    }
    public void ApplySaveState(SaveState state)
    {
        if (state == null) return;
        toggledObjects.Clear(); originalActiveStates.Clear();
        remainingDuration = state.remaining;
        for (int i = 0; i < state.indices.Length; i++)
        {
            int index = state.indices[i];
            if (index < 0 || index >= boundObjects.Length || boundObjects[index] == null) continue;
            toggledObjects.Add(boundObjects[index]); originalActiveStates.Add(state.original[i]);
        }
        temporaryToggleActive = remainingDuration > 0f && toggledObjects.Count > 0;
    }

    public LeverData GetSourceLever(int index)
    {
        switch (index)
        {
            case 0: return sourceLever;
            case 1: return sourceLever2;
            case 2: return sourceLever3;
            case 3: return sourceLever4;
            default: return null;
        }
    }

    private void Awake()
    {
        if (sourceLever == null)
        {
            sourceLever = GetComponent<LeverData>();
        }
    }

    private void OnEnable()
    {
        if (sourceLever == null)
        {
            sourceLever = GetComponent<LeverData>();
        }

        SubscribeToConfiguredLevers();
    }

    private void Update()
    {
        if (!temporaryToggleActive)
        {
            return;
        }

        remainingDuration -= Time.deltaTime;
        if (remainingDuration <= 0f)
        {
            RestoreOriginalStates();
        }
    }

    private void OnDisable()
    {
        UnsubscribeFromConfiguredLevers();

        RestoreOriginalStates();
    }

    private void OnDestroy()
    {
        UnsubscribeFromConfiguredLevers();

        RestoreOriginalStates();
    }

    private void SubscribeToConfiguredLevers()
    {
        for (int i = 0; i < MaximumBoundLevers; i++)
        {
            LeverData lever = GetSourceLever(i);
            if (lever == null || IsDuplicateSourceLever(lever, i))
            {
                continue;
            }

            // Removing first also makes repeated enable calls idempotent.
            lever.SignalEmitted -= HandleLeverSignal;
            lever.SignalEmitted += HandleLeverSignal;
        }
    }

    private void UnsubscribeFromConfiguredLevers()
    {
        for (int i = 0; i < MaximumBoundLevers; i++)
        {
            LeverData lever = GetSourceLever(i);
            if (lever == null || IsDuplicateSourceLever(lever, i))
            {
                continue;
            }

            lever.SignalEmitted -= HandleLeverSignal;
        }
    }

    private bool IsDuplicateSourceLever(LeverData candidate, int index)
    {
        for (int i = 0; i < index; i++)
        {
            if (GetSourceLever(i) == candidate)
            {
                return true;
            }
        }

        return false;
    }

    private void HandleLeverSignal(LeverData lever, bool isOn)
    {
        TriggerTemporaryToggle();
    }

    public void TriggerTemporaryToggle()
    {
        if (temporaryToggleActive)
        {
            // Preserve the states captured by the first trigger and only extend
            // the active interval. Re-toggling here would invert them twice.
            remainingDuration = Mathf.Max(0.01f, toggleDuration);
            return;
        }

        toggledObjects.Clear();
        originalActiveStates.Clear();
        HashSet<GameObject> uniqueTargets = new HashSet<GameObject>();
        int count = Mathf.Min(
            boundObjects != null ? boundObjects.Length : 0,
            MaximumBoundObjects);
        for (int i = 0; i < count; i++)
        {
            GameObject target = boundObjects[i];
            // Disabling this controller would immediately cancel its timer.
            if (target == null || target == gameObject ||
                !uniqueTargets.Add(target))
            {
                continue;
            }

            bool originalState = target.activeSelf;
            toggledObjects.Add(target);
            originalActiveStates.Add(originalState);
            target.SetActive(!originalState);
        }

        if (toggledObjects.Count == 0)
        {
            return;
        }

        CameraCircularVision.NotifyBlockersChanged();
        remainingDuration = Mathf.Max(0.01f, toggleDuration);
        temporaryToggleActive = true;
    }

    public void RestoreOriginalStates()
    {
        if (!temporaryToggleActive && toggledObjects.Count == 0)
        {
            return;
        }

        for (int i = 0; i < toggledObjects.Count; i++)
        {
            GameObject target = toggledObjects[i];
            if (target != null)
            {
                target.SetActive(originalActiveStates[i]);
            }
        }

        toggledObjects.Clear();
        originalActiveStates.Clear();
        remainingDuration = 0f;
        temporaryToggleActive = false;
        CameraCircularVision.NotifyBlockersChanged();
    }

    private void OnValidate()
    {
        toggleDuration = Mathf.Max(0.01f, toggleDuration);
        if (boundObjects != null && boundObjects.Length > MaximumBoundObjects)
        {
            System.Array.Resize(ref boundObjects, MaximumBoundObjects);
        }
    }
}
