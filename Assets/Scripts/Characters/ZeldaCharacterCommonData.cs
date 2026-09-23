using UnityEngine;

/// <summary>
/// Shared Inspector configuration inherited by every character data component.
/// Keep serialized names stable: prefab overrides and pre-refactor saves use them.
/// Concrete characters retain their own attack geometry and visual parameters.
/// </summary>
public abstract class ZeldaCharacterCommonData : MonoBehaviour
{
    public const int MinimumPermissionLevel = -2;
    public const int MaximumPermissionLevel = 9;

    [Header("Common Attributes / 通用属性")]
    [SerializeField] protected int health = 6;
    [SerializeField] protected int skillValue;
    [SerializeField, Range(MinimumPermissionLevel, MaximumPermissionLevel)]
    protected int permissionLevel;
    [SerializeField] protected int possessionEnergy = 3;
    [SerializeField] protected int possessionCost = 1;
    [SerializeField] protected float moveSpeed = 3.5f;
    [Header("Player Camera / 控制时的摄像机与视野")]
    [SerializeField, Min(0.01f), Tooltip("控制此角色时摄像机的正交半高度；附身聚焦仍在此数值基础上缩放。")]
    protected float cameraOrthographicSize = 7f;
    [SerializeField, Min(0.1f), Tooltip("控制此角色时的遮罩视野半径（世界单位），不影响 AI 侦测范围。")]
    protected float playerVisionRadius = 13f;
    [SerializeField, Tooltip("可选的场景镜头/视野例外。用于保留现有关卡画面；数值为 0 时使用上方基础值。")]
    private SceneCameraSettingsOverride[] sceneCameraOverrides = new SceneCameraSettingsOverride[0];

    [System.Serializable]
    public struct SceneCameraSettingsOverride
    {
        public string scenePath;
        [Min(0f)] public float orthographicSize;
        [Min(0f)] public float visionRadius;
    }

    public float CameraOrthographicSize => Mathf.Max(0.01f, cameraOrthographicSize);
    public float PlayerVisionRadius => Mathf.Max(0.1f, playerVisionRadius);

    public float GetCameraOrthographicSize(string scenePath)
    {
        if (sceneCameraOverrides != null && !string.IsNullOrEmpty(scenePath))
        {
            foreach (SceneCameraSettingsOverride setting in sceneCameraOverrides)
                if (setting.scenePath == scenePath && setting.orthographicSize > 0f)
                    return Mathf.Max(0.01f, setting.orthographicSize);
        }
        return CameraOrthographicSize;
    }

    public float GetPlayerVisionRadius(string scenePath)
    {
        if (sceneCameraOverrides != null && !string.IsNullOrEmpty(scenePath))
        {
            foreach (SceneCameraSettingsOverride setting in sceneCameraOverrides)
                if (setting.scenePath == scenePath && setting.visionRadius > 0f)
                    return Mathf.Max(0.1f, setting.visionRadius);
        }
        return PlayerVisionRadius;
    }
    [Header("Death Feedback / 死亡表现")]
    [SerializeField] protected float deathExplosionDuration = 0.35f;
    [Tooltip("死亡爆散相对死者身体最大世界尺寸的倍率，不使用当前控制角色或摄像机的尺寸。")]
    [SerializeField] protected float deathExplosionScale = 1.9f;
    [SerializeField] protected Color deathExplosionTint = new Color(1f, 0.65f, 0.12f, 1f);
    [Header("Damage Feedback / 受击表现")]
    [SerializeField] protected float damageFeedbackDuration = 0.18f;
    [SerializeField] protected Color damageTint = new Color(1f, 0.2f, 0.2f, 1f);
    [SerializeField] protected float damageShakeAmount = 0.04f;
    [SerializeField] protected float damageShakeSpeed = 80f;
    [Header("Attack Audio")]
    [SerializeField] protected AudioClip attackSound;
    [SerializeField, Range(0f, 1f)] protected float attackSoundVolume = 1f;
    [SerializeField, Range(0.1f, 3f)] protected float attackSoundPitch = 1f;
    [SerializeField, Range(0f, 1f)] protected float attackSoundSpatialBlend = 0.75f;
    [SerializeField, Min(0.01f)] protected float attackSoundMaxDistance = 16f;
    [Header("Damage Audio")]
    [Tooltip("为空时使用项目默认 8-bit 受击音效；音量设为 0 可静音。")]
    [SerializeField] protected AudioClip damageSound;
    [SerializeField, Range(0f, 1f)] protected float damageSoundVolume = 1f;
    [SerializeField, Range(0.1f, 3f)] protected float damageSoundPitch = 1f;
    [SerializeField, Range(0f, 1f)] protected float damageSoundSpatialBlend = 0.75f;
    [SerializeField, Min(0.01f)] protected float damageSoundMaxDistance = 16f;
    [Header("Death Audio")]
    [Tooltip("为空时使用项目默认 8-bit 死亡音效；音量设为 0 可静音。")]
    [SerializeField] protected AudioClip deathSound;
    [SerializeField, Range(0f, 1f)] protected float deathSoundVolume = 1f;
    [SerializeField, Range(0.1f, 3f)] protected float deathSoundPitch = 1f;
    [SerializeField, Range(0f, 1f)] protected float deathSoundSpatialBlend = 0.75f;
    [SerializeField, Min(0.01f)] protected float deathSoundMaxDistance = 16f;
    [Header("World Attribute Window")]
    [SerializeField] protected Vector2 attributeWindowOffset = Vector2.zero;
    [SerializeField, Min(0.1f)] protected float attributeWindowScale = 2.2f;


    protected void ValidateCommonData()
    {
        health = Mathf.Max(1, health);
        skillValue = Mathf.Max(0, skillValue);
        permissionLevel = Mathf.Clamp(
            permissionLevel,
            MinimumPermissionLevel,
            MaximumPermissionLevel);
        possessionEnergy = Mathf.Max(0, possessionEnergy);
        possessionCost = Mathf.Max(0, possessionCost);
        moveSpeed = Mathf.Max(0f, moveSpeed);
        cameraOrthographicSize = Mathf.Max(0.01f, cameraOrthographicSize);
        playerVisionRadius = Mathf.Max(0.1f, playerVisionRadius);
        deathExplosionDuration = Mathf.Max(0f, deathExplosionDuration);
        deathExplosionScale = Mathf.Max(0f, deathExplosionScale);
        damageFeedbackDuration = Mathf.Max(0f, damageFeedbackDuration);
        damageShakeAmount = Mathf.Max(0f, damageShakeAmount);
        damageShakeSpeed = Mathf.Max(0f, damageShakeSpeed);
        attackSoundVolume = Mathf.Clamp01(attackSoundVolume);
        attackSoundPitch = Mathf.Clamp(attackSoundPitch, 0.1f, 3f);
        attackSoundSpatialBlend = Mathf.Clamp01(attackSoundSpatialBlend);
        attackSoundMaxDistance = Mathf.Max(0.01f, attackSoundMaxDistance);
        damageSoundVolume = Mathf.Clamp01(damageSoundVolume);
        damageSoundPitch = Mathf.Clamp(damageSoundPitch, 0.1f, 3f);
        damageSoundSpatialBlend = Mathf.Clamp01(damageSoundSpatialBlend);
        damageSoundMaxDistance = Mathf.Max(0.01f, damageSoundMaxDistance);
        deathSoundVolume = Mathf.Clamp01(deathSoundVolume);
        deathSoundPitch = Mathf.Clamp(deathSoundPitch, 0.1f, 3f);
        deathSoundSpatialBlend = Mathf.Clamp01(deathSoundSpatialBlend);
        deathSoundMaxDistance = Mathf.Max(0.01f, deathSoundMaxDistance);
        attributeWindowScale = Mathf.Max(0.1f, attributeWindowScale);

    }
}
