using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Camera))]
public class CameraFollowActiveZeldaMover : MonoBehaviour
{
    [SerializeField] private Vector3 followOffset = new Vector3(0f, 0f, -10f);
    [SerializeField] private bool smoothFollow = true;
    [SerializeField] private float followSmoothTime = 0.12f;
    [Header("Target Camera Size")]
    [SerializeField, Min(0.01f)] private float ghostOrthographicSize = 4.5f;
    [SerializeField, Min(0.01f)] private float characterOrthographicSize = 6f;
    [SerializeField, Min(0.01f)] private float cameraSizeSmoothTime = 0.25f;
    [SerializeField, Range(0.25f, 1f)] private float possessionFocusSizeMultiplier = 0.72f;

    private ZeldaFourWayMover currentTarget;
    private Camera followCamera;
    private Vector3 followVelocity;
    private float cameraSizeVelocity;

    private void Awake()
    {
        followCamera = GetComponent<Camera>();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (gameObject.scene != scene)
        {
            return;
        }

        StartCoroutine(ApplyLoadedSceneCameraSettings());
    }

    private IEnumerator ApplyLoadedSceneCameraSettings()
    {
        // Scene travel restores gameplay objects after sceneLoaded. Waiting one
        // frame makes the loaded scene's camera the final authority and lets
        // all generated Blocks/UI cameras copy its serialized configuration.
        yield return null;

        followCamera = GetComponent<Camera>();
        currentTarget = null;
        followVelocity = Vector3.zero;
        cameraSizeVelocity = 0f;

        CameraCircularVision circularVision =
            GetComponent<CameraCircularVision>();
        if (circularVision != null)
        {
            circularVision.RefreshSceneCameraSettings();
        }

        CameraVisionObjectStreaming streaming =
            GetComponent<CameraVisionObjectStreaming>();
        if (streaming != null)
        {
            streaming.RefreshSceneCameraSettings();
        }

        CRTScreenEffect crt = GetComponent<CRTScreenEffect>();
        if (crt != null)
        {
            crt.RefreshSceneCameraSettings();
        }

        if (ZeldaHealthHeartsUI.Instance != null)
        {
            ZeldaHealthHeartsUI.Instance.RefreshSceneCameraBinding();
        }
    }

    private void LateUpdate()
    {
        ZeldaFourWayMover target = FindActiveControlledCharacter();
        if (target == null)
        {
            return;
        }

        currentTarget = target;
        UpdateCameraSize(currentTarget);

        Vector3 targetPosition = currentTarget.transform.position + followOffset;
        if (smoothFollow)
        {
            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref followVelocity, followSmoothTime);
        }
        else
        {
            transform.position = targetPosition;
        }
    }

    private void UpdateCameraSize(ZeldaFourWayMover target)
    {
        if (followCamera == null)
        {
            followCamera = GetComponent<Camera>();
        }

        bool followsGhost = target.GetComponent<GhostZeldaCharacterData>() != null;
        float baseTargetSize = followsGhost ? ghostOrthographicSize : characterOrthographicSize;
        float focusMultiplier = Mathf.Lerp(1f, possessionFocusSizeMultiplier, target.PossessionProgress);
        float targetSize = baseTargetSize * focusMultiplier;
        followCamera.orthographicSize = Mathf.SmoothDamp(
            followCamera.orthographicSize,
            targetSize,
            ref cameraSizeVelocity,
            cameraSizeSmoothTime,
            Mathf.Infinity,
            Time.deltaTime);
    }

    private ZeldaFourWayMover FindActiveControlledCharacter()
    {
        return ZeldaRuntimeRegistry.GetControlledMover();
    }

    private void OnValidate()
    {
        followSmoothTime = Mathf.Max(0f, followSmoothTime);
        ghostOrthographicSize = Mathf.Max(0.01f, ghostOrthographicSize);
        characterOrthographicSize = Mathf.Max(0.01f, characterOrthographicSize);
        cameraSizeSmoothTime = Mathf.Max(0.01f, cameraSizeSmoothTime);
        possessionFocusSizeMultiplier = Mathf.Clamp(possessionFocusSizeMultiplier, 0.25f, 1f);
    }
}
