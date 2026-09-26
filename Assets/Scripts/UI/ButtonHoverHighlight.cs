using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Opt-in hover feedback for buttons without an existing visual transition.
/// An independent, non-raycasting layer preserves data-driven button colours.
/// </summary>
[DisallowMultipleComponent]
public sealed class ButtonHoverHighlight : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private const float HoverAlpha = 0.18f;
    private const float FadeSeconds = 0.12f;
    private Button target;
    private Image highlight;
    private bool pointerInside;

    public static void AttachIfMissing(Button button)
    {
        if (button == null || button.transition != Selectable.Transition.None) return;
        AttachOverlay(button);
    }

    // Explicit opt-in for controls whose native tint is too subtle. Keep their
    // pressed/selected transition intact and render hover independently of it.
    public static void AttachOverlay(Button button)
    {
        if (button == null || button.GetComponent<PauseMenuButtonHover>() != null ||
            button.GetComponent<ButtonHoverHighlight>() != null) return;

        var effect = button.gameObject.AddComponent<ButtonHoverHighlight>();
        effect.target = button;
        var layer = new GameObject("Hover Highlight", typeof(RectTransform), typeof(Image));
        var rect = layer.GetComponent<RectTransform>();
        rect.SetParent(button.transform, false);
        rect.SetAsFirstSibling();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        effect.highlight = layer.GetComponent<Image>();
        effect.highlight.raycastTarget = false;
        Color color = ZeldaUiPalette.Ghost;
        color.a = 0f;
        effect.highlight.color = color;
    }

    public void OnPointerEnter(PointerEventData eventData) { pointerInside = true; }
    public void OnPointerExit(PointerEventData eventData) { pointerInside = false; }

    private void LateUpdate()
    {
        if (target == null || highlight == null) return;
        Color color = highlight.color;
        float alpha = target.IsActive() && target.IsInteractable()
            ? Mathf.MoveTowards(color.a, pointerInside ? HoverAlpha : 0f,
                HoverAlpha * Time.unscaledDeltaTime / FadeSeconds)
            : 0f;
        if (Mathf.Approximately(color.a, alpha)) return;
        color.a = alpha;
        highlight.color = color;
    }

    private void OnDisable()
    {
        pointerInside = false;
        if (highlight == null) return;
        Color color = highlight.color;
        color.a = 0f;
        highlight.color = color;
    }
}
