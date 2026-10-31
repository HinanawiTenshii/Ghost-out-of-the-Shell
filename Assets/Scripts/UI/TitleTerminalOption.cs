using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Text-mode reverse-video focus shared by pointer and keyboard selection.</summary>
[RequireComponent(typeof(Button)), DisallowMultipleComponent]
public sealed class TitleTerminalOption : MonoBehaviour, IPointerEnterHandler, ISelectHandler,
    IDeselectHandler, IPointerDownHandler, IPointerUpHandler
{
    private Button button;
    private Image background;
    private Text label;
    private Text prompt;
    private Image cursor;
    private TitleScreenGlow labelGlow;
    private Color normal, disabled;
    private bool pressed;
    private bool focused;
    private bool selected;
    private bool available;
    private bool compact;
    private float nextBlink;
    private TitleScreenController owner;
    private string[] bootTextFrames;
    private bool bootWriting;
    private bool writingCursor;
    private int writtenCharacters = -1;

    public void Configure(Text text, Color foreground, Color muted, TitleScreenController controller)
    {
        owner = controller;
        button = GetComponent<Button>();
        background = GetComponent<Image>();
        label = text;
        bootTextFrames = new string[text.text.Length + 1];
        for (int i = 0; i < bootTextFrames.Length; i++) bootTextFrames[i] = text.text.Substring(0, i);
        normal = foreground;
        disabled = muted;
        labelGlow = label.GetComponent<TitleScreenGlow>();
        var arrow = new GameObject("Command Prompt", typeof(RectTransform), typeof(Text));
        arrow.transform.SetParent(transform, false);
        prompt = arrow.GetComponent<Text>();
        prompt.font = text.font; prompt.fontSize = 24; prompt.text = ">";
        prompt.alignment = TextAnchor.MiddleCenter; prompt.raycastTarget = false;
        prompt.rectTransform.anchorMin = prompt.rectTransform.anchorMax = new Vector2(0f, .5f);
        prompt.rectTransform.anchoredPosition = new Vector2(18f, 0f);
        prompt.rectTransform.sizeDelta = new Vector2(24f, 32f);
        var block = new GameObject("Command Cursor", typeof(RectTransform), typeof(Image));
        block.transform.SetParent(transform, false);
        cursor = block.GetComponent<Image>(); cursor.raycastTarget = false;
        cursor.rectTransform.anchorMin = cursor.rectTransform.anchorMax = new Vector2(1f, .5f);
        cursor.rectTransform.anchoredPosition = new Vector2(-18f, 0f);
        cursor.rectTransform.sizeDelta = new Vector2(9f, 18f);
        Refresh();
    }

    public void Refresh()
    {
        if (label == null) return;
        if (bootWriting) { RefreshBootWriting(); return; }
        available = button.IsInteractable();
        focused = available && selected;
        compact = ((RectTransform)transform).rect.width < 200f;
        label.rectTransform.offsetMin = new Vector2(compact ? 8f : 36f, 2f);
        label.rectTransform.offsetMax = new Vector2(compact ? -8f : -32f, -2f);
        // Button.OnEnable can start its default disabled tint before Transition.None
        // is configured, because the boot CanvasGroup is initially non-interactable.
        // Cancel that inherited renderer tint so the first menu matches later pages.
        background.CrossFadeColor(Color.white, 0f, true, true);
        background.color = focused ? (pressed ? new Color32(184, 250, 255, 255) : new Color32(94, 216, 238, 255)) : Color.clear;
        label.color = !available ? disabled : focused ? new Color32(3, 17, 34, 255) : normal;
        // Keep inverted glyphs dark instead of leaving blue halos over the focus bar.
        if (labelGlow != null) labelGlow.enabled = !focused && available;
        prompt.enabled = focused && !compact;
        prompt.color = cursor.color = new Color32(3, 17, 34, 255);
        cursor.enabled = focused && !compact;
        cursor.rectTransform.anchorMin = cursor.rectTransform.anchorMax = new Vector2(1f, .5f);
        cursor.rectTransform.anchoredPosition = new Vector2(-18f, 0f);
        nextBlink = Time.unscaledTime + .55f;
    }

    private void Update()
    {
        if (button == null || bootWriting) return;
        if (available != button.IsInteractable()) Refresh();
        if (!focused || compact || Time.unscaledTime < nextBlink) return;
        cursor.enabled = !cursor.enabled;
        nextBlink = Time.unscaledTime + .55f;
    }

    public void SetBootWriting(float progress, bool showCursor)
    {
        int count = Mathf.Clamp(Mathf.CeilToInt((bootTextFrames.Length - 1) * progress), 0, bootTextFrames.Length - 1);
        if (bootWriting && count == writtenCharacters && writingCursor == showCursor) return;
        bootWriting = true;
        writtenCharacters = count;
        writingCursor = showCursor;
        label.text = bootTextFrames[count];
        RefreshBootWriting();
    }

    public void EndBootWriting()
    {
        if (!bootWriting) return;
        bootWriting = false;
        label.text = bootTextFrames[bootTextFrames.Length - 1];
        Refresh();
    }

    private void RefreshBootWriting()
    {
        // The parent still blocks input. Draw the actual availability, not its temporary boot lock.
        background.CrossFadeColor(Color.white, 0f, true, true);
        background.color = Color.clear;
        label.color = button.interactable ? normal : disabled;
        if (labelGlow != null) labelGlow.enabled = button.interactable;
        prompt.enabled = false;
        cursor.enabled = writingCursor;
        cursor.color = normal;
        cursor.rectTransform.anchorMin = cursor.rectTransform.anchorMax = new Vector2(0f, .5f);
        cursor.rectTransform.anchoredPosition = new Vector2(Mathf.Min(36f + label.preferredWidth + 6f,
            ((RectTransform)transform).rect.width - 18f), 0f);
    }
    private void OnRectTransformDimensionsChange() { Refresh(); }
    private void OnEnable() { pressed = false; selected = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject; Refresh(); }
    private void OnDisable() { pressed = false; selected = false; }
    public void OnPointerEnter(PointerEventData data)
    {
        if (button.IsInteractable() && !RetroSceneLoadReveal.IsBlockingInput) button.Select();
    }
    public void OnSelect(BaseEventData data) { selected = true; Refresh(); if (focused && owner != null) owner.PlayTerminalNavigation(); }
    // EventSystem sends OnDeselect BEFORE it replaces currentSelectedGameObject.
    public void OnDeselect(BaseEventData data) { selected = false; pressed = false; Refresh(); }
    public void OnPointerDown(PointerEventData data) { if (data.button == PointerEventData.InputButton.Left) { pressed = true; Refresh(); } }
    public void OnPointerUp(PointerEventData data) { pressed = false; Refresh(); }
}
