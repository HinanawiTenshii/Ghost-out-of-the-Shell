using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Pixel selector and restrained colour feedback; no gameplay action.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class FakeTitleMenuButton : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler,
    ISelectHandler, IDeselectHandler, ISubmitHandler
{
    [SerializeField] private Text label;
    [SerializeField] private Image highlight;
    [SerializeField] private GameObject selector;
    [SerializeField] private Color normal = new Color32(117, 214, 242, 255);
    [SerializeField] private Color focused = new Color32(221, 250, 255, 255);
    [SerializeField] private Color disabled = new Color32(62, 98, 133, 255);
    private Button button;
    private FakeTitleScreenPresentation presentation;
    [Min(.04f)] [SerializeField] private float blinkInterval = .08f;
    [Range(1, 4)] [SerializeField] private int clickBlinks = 3;
    private bool hovered, selected;
    private float blinkStarted = -1f;
    private int revealLevel = 3;

    public void SetRevealLevel(int level)
    {
        revealLevel = Mathf.Clamp(level, 0, 3);
        if (revealLevel < 3) { hovered = selected = false; blinkStarted = -1f; }
        Refresh();
    }

    private void Awake()
    {
        button = GetComponent<Button>();
        presentation = GetComponentInParent<FakeTitleScreenPresentation>();
    }
    private void OnEnable() { if (button == null) button = GetComponent<Button>(); Refresh(); }
    private void OnDisable() { hovered = selected = false; blinkStarted = -1f; Refresh(); }
    private void Update() { Refresh(); }
    private void Refresh()
    {
        if (button == null) return;
        bool available = button != null && button.IsInteractable();
        bool active = available && (hovered || selected);
        float interval = Mathf.Max(.04f, blinkInterval);
        bool blinking = active && blinkStarted >= 0f &&
            Time.unscaledTime - blinkStarted < interval * Mathf.Clamp(clickBlinks, 1, 4) * 2f;
        bool visible = !blinking || Mathf.FloorToInt((Time.unscaledTime - blinkStarted) / interval) % 2 == 0;
        // During the intro the parent group blocks interaction, but enabled
        // options retain their normal colour while the menu itself stays hidden.
        if (label != null)
        {
            Color color = !button.interactable ? disabled : blinking ? Color.white : active ? focused : normal;
            if (revealLevel == 1)
                color = button.interactable ? new Color32(20, 52, 84, 255) : new Color32(10, 23, 40, 255);
            else if (revealLevel == 2)
                color = button.interactable ? new Color32(45, 122, 158, 255) : new Color32(26, 55, 79, 255);
            else if (revealLevel == 0) color.a = 0f;
            if (!visible) color.a = 0f;
            label.color = color;
        }
        // Retain the transparent raycast surface, never brighten the surroundings.
        if (highlight != null) highlight.color = Color.clear;
        bool showSelector = active && visible && revealLevel == 3;
        if (selector != null && selector.activeSelf != showSelector) selector.SetActive(showSelector);
    }
    public void OnPointerEnter(PointerEventData e)
    {
        if (!button.IsInteractable()) return;
        hovered = true;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(gameObject);
    }
    public void OnPointerExit(PointerEventData e) { hovered = false; }
    public void OnPointerClick(PointerEventData e)
    {
        if (e.button == PointerEventData.InputButton.Left && button.IsInteractable()) BeginBlink();
    }
    public void OnSelect(BaseEventData e) { selected = true; }
    public void OnDeselect(BaseEventData e) { selected = false; hovered = false; blinkStarted = -1f; }
    public void OnSubmit(BaseEventData e) { if (button.IsInteractable()) BeginBlink(); }
    private void BeginBlink()
    {
        blinkStarted = Time.unscaledTime;
        if (presentation != null) presentation.PlayOptionConfirmSound();
        Refresh();
    }
}
