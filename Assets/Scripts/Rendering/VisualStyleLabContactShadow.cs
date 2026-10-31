using UnityEngine;

/// <summary>Opt-in contact shadow for the visual lab. Never changes character art or control.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(1000)]
public sealed class VisualStyleLabContactShadow : MonoBehaviour
{
    [SerializeField] private ZeldaCharacterData character;
    [SerializeField] private SpriteRenderer body;
    [SerializeField] private MeshRenderer shadow;
    public MeshRenderer ShadowRenderer => shadow;
    public SpriteRenderer BodyRenderer => body;

    private void OnEnable() => ResolveBody();
    private void Start() => ResolveBody();
    private void ResolveBody()
    {
        // The mover transfers art from the root to Visual during Awake, even
        // for AI bodies. Never use the disabled root as their visibility source.
        var visual=transform.Find("Visual");
        if(visual!=null&&visual.TryGetComponent<SpriteRenderer>(out var renderer))body=renderer;
    }

    // Cached references only; parenting handles movement without a scene query,
    // mesh rebuild or sprite change. Sprint art can rise while this stays grounded.
    private void LateUpdate()
    {
        if (shadow == null) return;
        bool visible = character != null && !character.IsDead && !character.IsGhostLike &&
            body != null && body.enabled && body.gameObject.activeInHierarchy && body.color.a > .01f;
        if (shadow.enabled != visible) shadow.enabled = visible;
    }
    private void OnDisable() { if (shadow != null) shadow.enabled = false; }
}
