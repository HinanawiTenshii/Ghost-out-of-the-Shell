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
    [SerializeField, Min(0.01f), Tooltip("沙虫出土等临时广角效果的缓动时间；镜头与视野遮罩使用同一倍率同步过渡。")]
    private float temporaryFramingSmoothTime = 0.4f;
    [SerializeField, Range(0.25f, 1f)] private float possessionFocusSizeMultiplier = 0.72f;

    private ZeldaFourWayMover currentTarget;
    private Camera followCamera;
    private CameraCircularVision shakeVision;
    private Vector3 followVelocity;
    private float cameraSizeVelocity;
    private float temporaryFramingMultiplier = 1f;
    private float temporaryFramingVelocity;
    private bool soulTransferFollow;
    private Vector3 shakeOffset;
    private float shakeRemaining, shakeDuration, shakeStrength, shakeFrequency;

    public float TemporaryFramingMultiplier => temporaryFramingMultiplier;

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
        ResetZoomVelocity();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        ClearShake();
        ResetZoomVelocity();
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
        ResetZoomVelocity();

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
        }
        if (shakeVision == null) shakeVision = GetComponent<CameraCircularVision>();
        shakeOffset += (Vector3)CameraEnvironmentalShakeSource.SampleOffset(followCamera,shakeVision,Time.deltaTime);
        transform.position += shakeOffset;
    }

    public void BeginSoulTransferFollow(ZeldaFourWayMover target)
    {
        currentTarget = target;
        followVelocity = Vector3.zero;
        soulTransferFollow = true;
    }

    /// <summary>Teleport while the screen is covered; do not pan through the intervening level.</summary>
    public void SnapToControlledCharacter()
    {
        var target = ZeldaRuntimeRegistry.GetControlledMover();
        if (target == null) return;
        ClearShake();
        currentTarget = target;
        followVelocity = Vector3.zero;
        soulTransferFollow = false;
        transform.position = target.transform.position + followOffset;
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
        UpdateCameraSize(baseTargetSize * focusMultiplier);
    }

    private void UpdateCameraSize(float targetSize)
    {
        AdvanceCameraSize(targetSize, Time.deltaTime);
    }

    private void AdvanceCameraSize(float targetSize, float deltaTime)
    {
        if (followCamera == null)
        {
            followCamera = GetComponent<Camera>();
        }

        // Smooth the character/focus size separately, then apply the encounter
        // multiplier exactly once. The mask reads this same displayed multiplier
        // later in LateUpdate, so its screen-space edge never jumps out and back.
        float baseSize = followCamera.orthographicSize / temporaryFramingMultiplier;
        baseSize = SmoothZoomValue(baseSize, Mathf.Max(0.01f, targetSize),
            ref cameraSizeVelocity, cameraSizeSmoothTime, deltaTime);
        temporaryFramingMultiplier = SmoothZoomValue(temporaryFramingMultiplier,
            CameraTemporaryFraming.GetMultiplier(gameObject.scene),
            ref temporaryFramingVelocity, temporaryFramingSmoothTime, deltaTime);
        followCamera.orthographicSize = baseSize * temporaryFramingMultiplier;
    }

    private void ResetZoomVelocity()
    {
        cameraSizeVelocity = 0f;
        temporaryFramingVelocity = 0f;
    }

    internal static float SmoothZoomValue(float current, float target, ref float velocity,
        float smoothTime, float deltaTime)
    {
        // SmoothDamp can retain motion away from a newly reversed target. Drop
        // only that stale velocity; ordinary transitions retain their soft start/end.
        if (deltaTime <= 0f) return current;
        float difference = target - current;
        if (Mathf.Abs(difference) < 0.00001f)
        { velocity = 0f; return target; }
        if (velocity * difference < 0f) velocity = 0f;
        float next = Mathf.SmoothDamp(current, target, ref velocity,
            Mathf.Max(0.01f, smoothTime), Mathf.Infinity, deltaTime);
        float bounded = Mathf.Clamp(next, Mathf.Min(current, target), Mathf.Max(current, target));
        if (bounded != next || bounded == target) velocity = 0f;
        return bounded;
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
        temporaryFramingSmoothTime = Mathf.Max(0.01f, temporaryFramingSmoothTime);
        possessionFocusSizeMultiplier = Mathf.Clamp(possessionFocusSizeMultiplier, 0.25f, 1f);
    }
}
