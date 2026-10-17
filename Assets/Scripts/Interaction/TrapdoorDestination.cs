using UnityEngine;

/// <summary>Shared asset identifying one scene-authored trapdoor across scene loads.</summary>
[CreateAssetMenu(fileName = "TrapdoorDestination", menuName = "Cogitans & Extensa/Trapdoor Destination (活板门定位)")]
public sealed class TrapdoorDestination : ScriptableObject
{
    [SerializeField, Tooltip("由目标活板门的编辑器同步按钮写入。场景必须加入 Build Settings。")] private string sceneName;
    [SerializeField, Tooltip("目标暂时缺失时使用的最后一次编辑器定位；正常传送总是读取目标活板门的实际位置。")]
    private Vector2 fallbackPosition;
    public string SceneName => sceneName;
    public Vector2 FallbackPosition => fallbackPosition;
}
