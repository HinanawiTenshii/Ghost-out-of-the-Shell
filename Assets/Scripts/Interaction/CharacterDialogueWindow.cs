using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Single paused modal. No dialogue state is shared between characters.</summary>
public sealed class CharacterDialogueWindow : MonoBehaviour
{
    private static CharacterDialogueWindow active;
    private static int closedFrame = -1;
    public static bool IsOpen => active != null;
    public static bool BlocksInput => IsOpen || Time.frameCount <= closedFrame;
    private CharacterDialogue speaker;
    private ZeldaFourWayMover player;
    private CharacterDialogue.Node current;
    private Canvas canvas;
    private Font font;
    private Text transcript, status;
    // A glow uses 25 copies of each glyph. Keep every visible Text mesh safely
    // below the uGUI vertex limit, without weakening glow as history grows.
    private const int TranscriptBlockCharacters = 320;
    private readonly List<Text> transcriptBlocks = new List<Text>();
    private readonly TextGenerator transcriptLayout = new TextGenerator();
    private float transcriptWidth = -1f, transcriptScale = -1f;
    private ScrollRect scroll;
    private RectTransform content;
    private RectTransform panel, dialogueViewport, choiceViewport, choiceContent, choiceDivider;
    private ScrollRect choiceScroll;
    private float lastPanelHeight = -1f;
    private readonly Button[] buttons = new Button[4];
    private readonly Text[] labels = new Text[4];
    private readonly CharacterDialogue.Choice[] visibleChoices = new CharacterDialogue.Choice[4];
    private float previousTimeScale;
    private bool previousCursor, restored, selecting;
    private CursorLockMode previousLock;
    private int inputFrame;
    private GameObject ownedEventSystem;
    private string history = "";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { active = null; closedFrame = -1; }

    public static void Open(CharacterDialogue owner, ZeldaFourWayMover actor)
    {
        if (BlocksInput || owner == null || !owner.CanConverse(actor) || !owner.HasDialoguePermission(actor)) return;
        var go = new GameObject("Character Dialogue Window", typeof(RectTransform));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, owner.gameObject.scene);
        var window = go.AddComponent<CharacterDialogueWindow>();
        window.speaker = owner; window.player = actor;
        window.previousTimeScale = Time.timeScale;
        window.previousCursor = Cursor.visible; window.previousLock = Cursor.lockState;
        window.inputFrame = Time.frameCount;
        active = window;
        Time.timeScale = 0; Cursor.visible = true; Cursor.lockState = CursorLockMode.None;
        try
        {
            // Compare before recording this opening. Only a successfully opened
            // window changes memory; rejected E presses and node transitions do not.
            bool differentActor = owner.HasDifferentPreviousActor(actor);
            window.Build(); window.EnterNode(owner.GetStartNode(actor), differentActor);
            if (!window.restored) owner.RememberDialogueActor(actor);
        }
        catch (Exception exception) { Debug.LogException(exception, owner); window.Close(); }
    }

    public static void CloseFor(CharacterDialogue owner)
    { if (active != null && active.speaker == owner) active.Close(); }

    private void Update()
    {
        if (speaker == null || player == null || ZeldaRuntimeRegistry.GetControlledMover() != player ||
            GameSaveSystem.IsLoading || !speaker.CanConverse(player)) { Close(); return; }
        if (Time.frameCount <= inputFrame) return;
        if (Input.GetKeyDown(KeyCode.Escape)) { Close(); return; }
        for (int i = 0; i < 4; i++)
            if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i)) || Input.GetKeyDown((KeyCode)((int)KeyCode.Keypad1 + i)))
            { Select(i); break; }
    }

    private void Enter(CharacterDialogue.Node node) => EnterNode(node, false);

    private void EnterNode(CharacterDialogue.Node node, bool differentActorOpening)
    {
        if (node == null) { Close(); return; }
        current = node;
        string text = differentActorOpening && !string.IsNullOrWhiteSpace(node.differentActorOpeningText)
            ? node.differentActorOpeningText : node.text;
        history += speaker.SpeakerName + "\n" + text + "\n\n";
        // Keep long looping conversations bounded; authoring text itself is not truncated.
        if (history.Length > 24000) history = history.Substring(history.Length - 24000);
        status.text = "";
        RefreshChoices();
        RefreshTranscript();
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        scroll.verticalNormalizedPosition = 0;
        choiceScroll.verticalNormalizedPosition = 1;
    }

    private void RefreshChoices()
    {
        Array.Clear(visibleChoices, 0, visibleChoices.Length);
        int count = 0;
        if (current.choices != null)
            foreach (var choice in current.choices)
            {
                if (choice == null || (choice.effect != null && !choice.effect.IsVisible(speaker, player))) continue;
                visibleChoices[count++] = choice;
                if (count == visibleChoices.Length) break;
            }
        for (int i = 0; i < 4; i++)
        {
            bool fallbackExit = count == 0 && i == 0;
            bool visible = fallbackExit || i < count;
            buttons[i].gameObject.SetActive(visible);
            if (!visible) continue;
            var choice = fallbackExit ? null : visibleChoices[i];
            string reason = null;
            bool allowed = choice == null || IsChoiceAvailable(choice, out reason);
            labels[i].text = (i + 1) + ". " + (choice == null ? "结束对话" : GetChoiceText(choice)) +
                            (!allowed && !string.IsNullOrEmpty(reason) ? "\n" + reason : "");
            labels[i].color = allowed ? ZeldaUiPalette.Primary : new Color(.42f, .47f, .53f, 1f);
            buttons[i].interactable = allowed;
        }
        RefreshDockedLayout();
    }

    private bool IsChoiceAvailable(CharacterDialogue.Choice choice, out string reason)
    {
        reason = null;
        if (choice.action == CharacterDialogue.ChoiceAction.Continue && speaker.FindNode(choice.nextNodeId) == null)
        { reason = "目标段落未配置"; return false; }
        return choice.effect == null || (choice.effect.IsVisible(speaker, player) && choice.effect.CanSelect(speaker, player, out reason));
    }

    private string GetChoiceText(CharacterDialogue.Choice choice)
    {
        return choice.effect == null ? choice.text : choice.effect.GetChoiceText(speaker, player, choice.text);
    }

    private void Select(int index)
    {
        if (restored || selecting || Time.frameCount <= inputFrame || index < 0 || index >= 4 ||
            !buttons[index].gameObject.activeSelf) return;
        var choice = visibleChoices[index];
        if (choice == null) { Close(); return; }
        if (choice == null || !IsChoiceAvailable(choice, out string reason)) { RefreshChoices(); return; }
        selecting = true; inputFrame = Time.frameCount;
        try
        {
            string selectedText = GetChoiceText(choice);
            if (choice.effect != null && !choice.effect.TryExecute(speaker, player))
            { status.text = "当前无法执行此选项。"; RefreshChoices(); return; }
            // An effect may remove the speaker or close the window. Never continue after that.
            if (restored || speaker == null || !speaker.isActiveAndEnabled) return;
            choice.onSelected?.Invoke();
            if (restored || speaker == null || !speaker.isActiveAndEnabled) return;
            if (choice.action == CharacterDialogue.ChoiceAction.Exit) { Close(); return; }
            history += "你\n" + selectedText + "\n\n";
            Enter(speaker.FindNode(choice.nextNodeId));
        }
        catch (Exception exception)
        { Debug.LogException(exception, speaker); Close(); }
        finally { selecting = false; }
    }

    private void Build()
    {
        font = speaker.DisplayFont != null ? speaker.DisplayFont : ZeldaHealthHeartsUI.Instance?.PermissionLabelFont;
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true; canvas.sortingOrder = short.MaxValue;
        int highest = int.MinValue;
        foreach (var layer in SortingLayer.layers)
            if (layer.value > highest) { highest = layer.value; canvas.sortingLayerID = layer.id; }
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        gameObject.AddComponent<GraphicRaycaster>();
        if (EventSystem.current == null)
        {
            ownedEventSystem = new GameObject("Dialogue Event System", typeof(EventSystem), typeof(StandaloneInputModule));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(ownedEventSystem, gameObject.scene);
        }

        var dim = Rect("Background Dimmer", transform);
        Stretch(dim, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        dim.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, speaker.DimOpacity);
        panel = Rect("Right Dialogue Panel", transform);
        panel.anchorMin = new Vector2(1, 0); panel.anchorMax = Vector2.one;
        panel.offsetMin = new Vector2(-520, 28); panel.offsetMax = new Vector2(-28, -28);
        panel.gameObject.AddComponent<Image>().color = new Color(.018f,.036f,.105f,1);
        AddRule("Frame Left", panel, Vector2.zero, new Vector2(0,1), Vector2.zero, new Vector2(2,0));
        AddRule("Frame Right", panel, new Vector2(1,0), Vector2.one, new Vector2(-2,0), Vector2.zero);
        AddRule("Frame Top", panel, new Vector2(0,1), Vector2.one, new Vector2(0,-2), Vector2.zero);
        AddRule("Frame Bottom", panel, Vector2.zero, new Vector2(1,0), Vector2.zero, new Vector2(0,2));
        var title = Label("Speaker", panel, 27); title.text = speaker.SpeakerName;
        Stretch(title.rectTransform, new Vector2(0,1), Vector2.one, new Vector2(26,-67), new Vector2(-24,-20));
        var hint = Label("Controls", panel, 16); hint.text = "[1–4] / 鼠标选择    [Esc] 结束对话";
        Stretch(hint.rectTransform, Vector2.zero, new Vector2(1,0), new Vector2(26,12), new Vector2(-20,38));

        var viewport = Rect("Conversation Scroll", panel);
        dialogueViewport = viewport;
        Stretch(viewport, Vector2.zero, Vector2.one, new Vector2(26,48), new Vector2(-24,-80));
        viewport.gameObject.AddComponent<Image>().color = new Color(0,0,0,.01f);
        viewport.gameObject.AddComponent<RectMask2D>();
        scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 32; scroll.viewport = viewport;
        content = Rect("Conversation", viewport); content.anchorMin = new Vector2(0,1); content.anchorMax = Vector2.one;
        content.pivot = new Vector2(.5f,1); content.sizeDelta = Vector2.zero;
        scroll.content = content;
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 0; layout.padding = new RectOffset(0,4,4,8);
        layout.childControlHeight = layout.childControlWidth = true;
        layout.childForceExpandHeight = false; layout.childForceExpandWidth = true;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        transcript = Label("Transcript", content, 23); transcript.color = new Color(.75f,.9f,1);
        transcriptBlocks.Add(transcript);
        TitleScreenGlow.Attach(transcript);

        // Choices live outside the transcript's ScrollRect, anchored above the bottom hint.
        // A separate viewport handles unusually long options without displacing the history.
        choiceViewport = Rect("Bottom Choices Scroll", panel);
        Stretch(choiceViewport, Vector2.zero, new Vector2(1,0), new Vector2(26,48), new Vector2(-24,49));
        choiceViewport.gameObject.AddComponent<Image>().color = new Color(0,0,0,.01f);
        choiceViewport.gameObject.AddComponent<RectMask2D>();
        choiceScroll = choiceViewport.gameObject.AddComponent<ScrollRect>();
        choiceScroll.horizontal = false; choiceScroll.movementType = ScrollRect.MovementType.Clamped;
        choiceScroll.scrollSensitivity = 32; choiceScroll.viewport = choiceViewport;
        choiceContent = Rect("Choices", choiceViewport);
        choiceContent.anchorMin = new Vector2(0,1); choiceContent.anchorMax = Vector2.one;
        choiceContent.pivot = new Vector2(.5f,1); choiceContent.sizeDelta = Vector2.zero;
        choiceScroll.content = choiceContent;
        var choicesLayout = choiceContent.gameObject.AddComponent<VerticalLayoutGroup>();
        choicesLayout.spacing = 10;
        choicesLayout.childControlHeight = choicesLayout.childControlWidth = true;
        choicesLayout.childForceExpandHeight = false; choicesLayout.childForceExpandWidth = true;
        choiceContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        choiceDivider = AddRule("Choices Divider", panel, Vector2.zero, new Vector2(1,0),
            new Vector2(26,60), new Vector2(-24,62));
        for (int i = 0; i < 4; i++)
        {
            int index = i;
            var rect = Rect("Choice " + (i+1), choiceContent);
            var image = rect.gameObject.AddComponent<Image>(); image.color = Color.white;
            var button = rect.gameObject.AddComponent<Button>(); buttons[i] = button; button.targetGraphic = image;
            // Dialogue uses its own 1-4 shortcuts. Do not latch uGUI's Selected
            // state on click: it takes priority over hover and uses the normal
            // tint, hiding feedback when a purchase reuses this same button.
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = button.colors;
            colors.normalColor = new Color(.035f,.12f,.25f); colors.highlightedColor = new Color(.05f,.3f,.45f);
            colors.pressedColor = new Color(.07f,.4f,.55f); colors.selectedColor = colors.normalColor;
            colors.disabledColor = new Color(.035f,.055f,.09f); colors.fadeDuration = .1f; button.colors = colors;
            var group = rect.gameObject.AddComponent<VerticalLayoutGroup>(); group.padding = new RectOffset(14,14,12,12);
            group.childControlHeight = group.childControlWidth = true;
            group.childForceExpandHeight = false;
            labels[i] = Label("Choice Text", rect, 21);
            button.onClick.AddListener(() => Select(index));
        }
        status = Label("Feedback", choiceContent, 18);
        status.gameObject.SetActive(false);
        CRTScreenEffect.RegisterCanvas(canvas, enableGlow: true);
    }

    private void LateUpdate()
    {
        if (panel != null && !Mathf.Approximately(lastPanelHeight, panel.rect.height)) RefreshDockedLayout();
        if (transcript != null && (!Mathf.Approximately(transcriptWidth, transcript.GetPixelAdjustedRect().width) ||
            !Mathf.Approximately(transcriptScale, transcript.pixelsPerUnit)))
        {
            RefreshTranscript();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }
    }

    private void RefreshTranscript()
    {
        float width = transcript.GetPixelAdjustedRect().width;
        if (width <= 0f) return;
        transcriptWidth = width; transcriptScale = transcript.pixelsPerUnit;
        var settings = transcript.GetGenerationSettings(new Vector2(width, 0));
        settings.verticalOverflow = VerticalWrapMode.Overflow;
        transcriptLayout.Populate(history, settings);
        var lines = transcriptLayout.lines;
        float totalHeight = transcript.cachedTextGeneratorForLayout.GetPreferredHeight(history, settings) / transcriptScale;
        int used = 0, start = 0, startLine = 0;
        float consumedHeight = 0;
        while (start < history.Length)
        {
            // Split at actual wrapped line starts: no new line breaks, no changes
            // to paragraph spacing, and no artificial breaks in long Chinese text.
            int nextLine = startLine + 1;
            while (nextLine < lines.Count && lines[nextLine].startCharIdx - start <= TranscriptBlockCharacters)
                nextLine++;
            if (nextLine < lines.Count || history.Length - start > TranscriptBlockCharacters)
                nextLine = Mathf.Max(startLine + 1, nextLine - 1);
            int end = nextLine < lines.Count ? lines[nextLine].startCharIdx : history.Length;
            // The dialogue panel is narrower than a 320-character line, even at
            // the smallest font size. Retain a safety bound for unusual fonts.
            end = Mathf.Min(end, start + TranscriptBlockCharacters);
            if (end <= start) break;
            if (end < history.Length && char.IsHighSurrogate(history[end - 1])) end--;
            float height = end < history.Length && nextLine < lines.Count
                ? (lines[startLine].topY - lines[nextLine].topY) / transcriptScale
                : Mathf.Max(0, totalHeight - consumedHeight);
            Text block;
            if (used < transcriptBlocks.Count) block = transcriptBlocks[used];
            else
            {
                block = Label("Transcript " + (used + 1), content, transcript.fontSize);
                block.color = transcript.color; block.lineSpacing = transcript.lineSpacing;
                TitleScreenGlow.Attach(block); transcriptBlocks.Add(block);
            }
            block.gameObject.SetActive(true);
            block.verticalOverflow = VerticalWrapMode.Overflow;
            block.text = history.Substring(start, end - start);
            var sizing = block.GetComponent<LayoutElement>();
            if (sizing == null) sizing = block.gameObject.AddComponent<LayoutElement>();
            sizing.minHeight = sizing.preferredHeight = height;
            consumedHeight += height; start = end; startLine = nextLine; used++;
        }
        for (int i = used; i < transcriptBlocks.Count; i++)
            transcriptBlocks[i].gameObject.SetActive(false);
    }

    internal static float GetChoiceViewportHeight(float preferredHeight, float panelHeight)
    {
        // Reserve header/footer space and at least half the remaining area for dialogue.
        return Mathf.Clamp(preferredHeight, 1f, Mathf.Max(1f, (panelHeight - 128f) * .5f));
    }

    private void RefreshDockedLayout()
    {
        if (choiceContent == null) return;
        status.gameObject.SetActive(!string.IsNullOrEmpty(status.text));
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(choiceContent);
        float height = GetChoiceViewportHeight(LayoutUtility.GetPreferredHeight(choiceContent), panel.rect.height);
        choiceViewport.offsetMax = new Vector2(-24, 48 + height);
        choiceDivider.offsetMin = new Vector2(26, 60 + height);
        choiceDivider.offsetMax = new Vector2(-24, 62 + height);
        dialogueViewport.offsetMin = new Vector2(26, 74 + height);
        lastPanelHeight = panel.rect.height;
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
    }

    private static RectTransform AddRule(string name, Transform parent, Vector2 min, Vector2 max,
        Vector2 offsetMin, Vector2 offsetMax)
    {
        var rect = Rect(name, parent);
        Stretch(rect, min, max, offsetMin, offsetMax);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = ZeldaUiPalette.Primary; image.raycastTarget = false;
        return rect;
    }

    private static RectTransform Rect(string objectName, Transform parent)
    {
        var go = new GameObject(objectName, typeof(RectTransform)); go.layer = LayerMask.NameToLayer("UI");
        var rect = go.GetComponent<RectTransform>(); rect.SetParent(parent, false); return rect;
    }
    private static void Stretch(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = offsetMin; rect.offsetMax = offsetMax; }
    private Text Label(string objectName, Transform parent, int size)
    {
        var text = Rect(objectName, parent).gameObject.AddComponent<Text>();
        text.font = font; text.fontSize = size; text.color = ZeldaUiPalette.Primary;
        text.supportRichText = false; text.raycastTarget = false; text.alignment = TextAnchor.UpperLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }
    private void Close() { Restore(); gameObject.SetActive(false); Destroy(gameObject); }
    private void Restore()
    {
        if (restored) return; restored = true;
        if (active == this)
        {
            active = null; closedFrame = Time.frameCount;
            Time.timeScale = previousTimeScale; Cursor.visible = previousCursor; Cursor.lockState = previousLock;
        }
        if (ownedEventSystem != null) Destroy(ownedEventSystem);
    }
    private void OnDisable() => Restore();
    private void OnDestroy() { Restore(); ((IDisposable)transcriptLayout).Dispose(); }
}
