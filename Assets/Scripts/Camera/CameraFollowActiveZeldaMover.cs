using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Camera))]
[DefaultExecutionOrder(-200)]
public class CameraFollowActiveZeldaMover : MonoBehaviour
{
    [SerializeField] private Vector3 followOffset = new Vector3(0f, 0f, -10f);
    [SerializeField] private bool smoothFollow = true;
    [SerializeField] private float followSmoothTime = 0.12f;
    [Header("Camera Size Transition / 角色大小由角色数据提供")]
    [SerializeField, Min(0.01f)] private float puppetOrthographicSize = 3f;
    [SerializeField, Min(0.01f)] private float cameraSizeSmoothTime = 0.25f;
    [SerializeField, Range(0.25f, 1f)] private float possessionFocusSizeMultiplier = 0.72f;

    private ZeldaFourWayMover currentTarget;
    private Camera followCamera;
    private Vector3 followVelocity;
    private float cameraSizeVelocity;
    private bool soulTransferFollow;
    private Vector3 shakeOffset;
    private float shakeRemaining, shakeDuration, shakeStrength, shakeFrequency;

    /// <summary>Visual-only offset, kept out of the smooth-follow velocity.</summary>
    public void PlayShake(float duration, float strength, float frequency = 22f)
    {
        if (!isActiveAndEnabled || duration <= 0f || strength <= 0f) return;
        shakeDuration = shakeRemaining = duration;
        shakeStrength = strength;
        shakeFrequency = Mathf.Max(1f, frequency);
    }

    private void ClearShake()
    {
        transform.position -= shakeOffset;
        shakeOffset = Vector3.zero;
        shakeRemaining = 0f;
    }

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
        ClearShake();
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
        // Scene travel restores gameplay objects after sceneLoaded. Wait one
        // frame before rebinding; controlled character data supplies size/range,
        // while the loaded camera retains its rendering and transition settings.
        yield return null;

        followCamera = GetComponent<Camera>();
        currentTarget = null;
        ClearShake();
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
        // Remove last frame's visual displacement before computing the follow position.
        transform.position -= shakeOffset;
        shakeOffset = Vector3.zero;
        FollowActiveTarget();
        if (shakeRemaining > 0f)
        {
            shakeRemaining = Mathf.Max(0f, shakeRemaining - Time.deltaTime);
            float age = shakeDuration - shakeRemaining;
            float fade = shakeRemaining / shakeDuration;
            float phase = age * shakeFrequency * Mathf.PI * 2f;
            shakeOffset = new Vector3(Mathf.Sin(phase), Mathf.Sin(phase * 1.23f + 1.1f) * 0.65f, 0f)
                * (shakeStrength * fade);
            transform.position += shakeOffset;
        }
    }

    public void BeginSoulTransferFollow(ZeldaFourWayMover target)
    {
        currentTarget = target;
        followVelocity = Vector3.zero;
        soulTransferFollow = true;
    }

    private void FollowActiveTarget()
    {
        ZeldaFourWayMover target = FindActiveControlledCharacter();
        Transform remoteControlTarget =
            ClockworkPuppetRuntime.RemoteControlTargetTransform;
        if (target == null && remoteControlTarget == null)
        {
            return;
        }

        if (target != null)
        {
            currentTarget = target;
        }

        if (remoteControlTarget != null)
        {
            UpdateCameraSize(puppetOrthographicSize);
        }
        else if (currentTarget != null)
        {
            UpdateCameraSize(currentTarget);
        }

        Vector3 followPosition = remoteControlTarget != null
            ? remoteControlTarget.position
            : currentTarget.transform.position;
        Vector3 targetPosition = followPosition + followOffset;
        if (smoothFollow || soulTransferFollow)
        {
            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref followVelocity,
                soulTransferFollow ? Mathf.Max(0.12f, followSmoothTime) : followSmoothTime);
            if (soulTransferFollow && Vector3.Distance(transform.position, targetPosition) < 0.02f)
                soulTransferFollow = false;
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

        ZeldaCharacterData data = target.CharacterData;
        float baseTargetSize = data != null
            ? data.GetCameraOrthographicSize(gameObject.scene.path) : 7f;
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

    private void UpdateCameraSize(float targetSize)
    {
        if (followCamera == null)
        {
            followCamera = GetComponent<Camera>();
        }

        followCamera.orthographicSize = Mathf.SmoothDamp(
            followCamera.orthographicSize,
            Mathf.Max(0.01f, targetSize),
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
        puppetOrthographicSize = Mathf.Max(0.01f, puppetOrthographicSize);
        cameraSizeSmoothTime = Mathf.Max(0.01f, cameraSizeSmoothTime);
        possessionFocusSizeMultiplier = Mathf.Clamp(possessionFocusSizeMultiplier, 0.25f, 1f);
    }
}
