using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Captures the state at scene entry and restores it after holding T.
/// Created automatically, so existing scenes and both transition prefabs do
/// not require manual changes.
/// </summary>
[DefaultExecutionOrder(-450)]
public sealed class QuickLevelRestartController : MonoBehaviour
{
    [SerializeField] private KeyCode restartKey = KeyCode.T;
    [SerializeField, Min(0.1f)] private float holdDuration = 1.5f;
    [SerializeField] private Vector2 progressOffset = new Vector2(0f, 1.05f);
    [SerializeField, Min(0.1f)] private float progressScale = 0.8f;
    [SerializeField] private Color progressColor =
        new Color(0.95f, 0.08f, 0.08f, 1f);
    [SerializeField, Min(1)] private int entryCaptureDelayFrames = 6;

    private static QuickLevelRestartController instance;
    private float heldTime;
    private ZeldaPossessionProgressBar progressBar;
    private Transform progressTransform;
    private int captureGeneration;
    private bool restartRequested;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstance()
    {
        if (instance != null)
        {
            return;
        }

        GameObject controllerObject =
            new GameObject("Quick Level Restart Controller");
        controllerObject.AddComponent<QuickLevelRestartController>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        SceneTravelStateManager.GetOrCreate();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        ScheduleEntryCapture(SceneManager.GetActiveScene());
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        ResetHold();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        restartRequested = false;
        ResetHold();
        ScheduleEntryCapture(scene);
    }

    private void ScheduleEntryCapture(Scene scene)
    {
        captureGeneration++;
        SceneTravelStateManager stateManager =
            SceneTravelStateManager.GetOrCreate();
        if (!stateManager.IsQuickRestartPending)
        {
            // Never allow T to use the previous scene's checkpoint while the
            // newly loaded scene is still restoring and building its own.
            stateManager.ClearQuickRestartCheckpoint();
        }

        StartCoroutine(CaptureEntryAfterInitialization(
            scene,
            captureGeneration));
    }

    private IEnumerator CaptureEntryAfterInitialization(
        Scene scene,
        int generation)
    {
        for (int frame = 0; frame < entryCaptureDelayFrames; frame++)
        {
            yield return null;
        }

        if (generation != captureGeneration ||
            !scene.IsValid() ||
            scene != SceneManager.GetActiveScene())
        {
            yield break;
        }

        SceneTravelStateManager stateManager =
            SceneTravelStateManager.GetOrCreate();
        const int maximumReadyWaitFrames = 120;
        for (int frame = 0; frame < maximumReadyWaitFrames; frame++)
        {
            if (generation != captureGeneration ||
                !scene.IsValid() ||
                scene != SceneManager.GetActiveScene())
            {
                yield break;
            }

            if (!stateManager.IsQuickRestartPending &&
                !stateManager.IsSceneTravelRestoreInProgress &&
                ZeldaRuntimeRegistry.GetControlledMover() != null)
            {
                stateManager.CaptureQuickRestartCheckpoint(scene);
                yield break;
            }

            yield return null;
        }

        Debug.LogWarning(
            "Quick restart checkpoint was not captured because the scene " +
            "did not finish restoring a controlled character in time.",
            this);
    }

    private void Update()
    {
        SceneTravelStateManager stateManager =
            SceneTravelStateManager.GetOrCreate();
        ZeldaFourWayMover mover = ZeldaRuntimeRegistry.GetControlledMover();
        bool inputBlocked =
            DocumentReader.IsInputBlocked ||
            PauseMenuController.IsPaused ||
            restartRequested ||
            stateManager.IsQuickRestartPending ||
            mover == null ||
            !stateManager.HasQuickRestartCheckpoint;

        if (inputBlocked || !Input.GetKey(restartKey))
        {
            ResetHold();
            return;
        }

        heldTime += Time.unscaledDeltaTime;
        EnsureProgressBar(mover);
        if (progressBar != null)
        {
            progressBar.SetProgress(
                heldTime / Mathf.Max(0.1f, holdDuration),
                progressColor);
        }

        if (heldTime < holdDuration)
        {
            return;
        }

        restartRequested = true;
        ResetHold();
        if (!stateManager.RestartFromQuickCheckpoint())
        {
            restartRequested = false;
        }
    }

    private void EnsureProgressBar(ZeldaFourWayMover mover)
    {
        if (mover == null)
        {
            return;
        }

        if (progressBar == null)
        {
            GameObject progressObject =
                new GameObject("Quick Restart Progress");
            progressObject.AddComponent<SpriteRenderer>();
            progressBar =
                progressObject.AddComponent<ZeldaPossessionProgressBar>();
            progressTransform = progressObject.transform;
        }

        if (progressTransform.parent != mover.transform)
        {
            progressTransform.SetParent(mover.transform, false);
        }

        progressTransform.localPosition =
            mover.GetOverheadLocalPosition(progressOffset);
        progressTransform.localRotation = Quaternion.identity;
        progressTransform.localScale =
            Vector3.one * Mathf.Max(0.1f, progressScale);
    }

    private void ResetHold()
    {
        heldTime = 0f;
        if (progressBar != null)
        {
            progressBar.Hide();
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        holdDuration = Mathf.Max(0.1f, holdDuration);
        progressScale = Mathf.Max(0.1f, progressScale);
        entryCaptureDelayFrames = Mathf.Max(1, entryCaptureDelayFrames);
    }
#endif
}
