using System.Collections.Generic;
using UnityEngine;

/// <summary>Opt-in presentation for authored levels. No changes to combat, palettes or perception.</summary>
[DefaultExecutionOrder(-32000), DisallowMultipleComponent]
public sealed class LevelSceneVisualProfile : MonoBehaviour
{
    public Shader visionShader;
    public Material contactMaterial;
    public Mesh contactMesh;
    private static readonly Dictionary<int, LevelSceneVisualProfile> profiles = new Dictionary<int, LevelSceneVisualProfile>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetProfiles() => profiles.Clear();
    void Awake() => profiles[gameObject.scene.handle] = this;
    void OnDestroy() => profiles.Remove(gameObject.scene.handle);
    static LevelSceneVisualProfile For(GameObject obj)
    {
        if (!Application.isPlaying || !obj) return null;
        profiles.TryGetValue(ZeldaRuntimeRegistry.GetGameplayScene(obj).handle, out var profile);
        return profile;
    }
    void Start()
    {
        // Registrations can precede Awake on a transferred/persistent body. Registry traversal once, not a scene scan.
        foreach (var mover in ZeldaRuntimeRegistry.Movers) DecorateCharacter(mover);
        foreach (var ai in ZeldaRuntimeRegistry.AiCharacters) DecorateVision(ai);
    }
    public static void DecorateCharacter(ZeldaFourWayMover mover)
    {
        if (!mover) return;
        var profile = For(mover.gameObject);
        var body = mover.GetComponent<ZeldaCharacterData>();
        if (!profile || !body || body.IsGhostLike || body is BehemothZeldaCharacterData) return;
        profile.Shadow(body.gameObject, true);
    }
    public static void DecorateVision(ZeldaCharacterAiBase ai)
    {
        if (!ai) return;
        var profile = For(ai.gameObject);
        if (!profile || ai.GetComponent<VisualStyleLabVisionStyle>()) return;
        ai.gameObject.AddComponent<VisualStyleLabVisionStyle>().Configure(profile.visionShader);
    }
    public static void DecoratePickup(PickupItemBase item)
    {
        if (!item) return;
        var profile = For(item.gameObject);
        if (profile) profile.Shadow(item.gameObject, false);
    }
    void Shadow(GameObject obj, bool actor)
    {
        if (obj.GetComponent<LevelContactShadow>()) return;
        obj.AddComponent<LevelContactShadow>().Configure(contactMesh, contactMaterial, actor);
    }
}
