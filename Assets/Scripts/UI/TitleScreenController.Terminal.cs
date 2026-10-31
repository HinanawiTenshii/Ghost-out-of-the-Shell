using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed partial class TitleScreenController
{
    private CanvasGroup terminalFrame;
    private CanvasGroup terminalInput;
    private CanvasGroup[] terminalTitleLines;
    private CanvasGroup[] terminalRows;
    private Image bootCursor;
    private Image readyLamp;
    private bool terminalReady;

    private void BuildTerminalInterface()
    {
        var background = CreateUiObject("Title Background", transform, typeof(Image)).GetComponent<Image>();
        Stretch(background.rectTransform);
        background.color = BackgroundBlue;

        // Fixed reference geometry inside an expanding canvas keeps all submenus aligned.
        var frame = TerminalRect("Outer Frame", background.transform, Vector2.zero, new Vector2(1180f, 628f));
        terminalInput = frame.gameObject.AddComponent<CanvasGroup>();
        terminalInput.interactable = false;
        terminalInput.blocksRaycasts = false;

        var chrome = TerminalRect("Terminal Frame", frame, Vector2.zero, frame.sizeDelta);
        terminalFrame = chrome.gameObject.AddComponent<CanvasGroup>();
        terminalFrame.blocksRaycasts = false;
        var fill = chrome.gameObject.AddComponent<Image>();
        fill.color = new Color32(3, 13, 28, 255);
        fill.raycastTarget = false;
        CreateBorder(chrome, GhostBlue, 2f);
        var inset = TerminalRect("Inner Frame", chrome, Vector2.zero, Vector2.zero);
        Stretch(inset);
        inset.offsetMin = Vector2.one * 6f;
        inset.offsetMax = Vector2.one * -6f;
        terminalInnerFrame = inset.gameObject;
        CreateBorder(inset, new Color32(27, 97, 135, 255), 1f);
        // Keep the window structure, without a redundant header or keyboard-help caption.
        terminalHeader = TerminalRule(chrome, "Header Rule", new Vector2(0f, -32f), new Vector2(-16f, 1f));
        terminalHeader.rectTransform.anchorMin = new Vector2(0f, 1f);
        terminalHeader.rectTransform.anchorMax = Vector2.one;
        terminalFooter = TerminalRule(chrome, "Footer Rule", new Vector2(0f, 32f), new Vector2(-16f, 1f));
        terminalFooter.rectTransform.anchorMin = Vector2.zero;
        terminalFooter.rectTransform.anchorMax = new Vector2(1f, 0f);
        terminalDivider = TerminalRule(chrome, "Menu Divider", new Vector2(58f, -50f), new Vector2(1f, 528f));
        terminalDivider.rectTransform.anchorMin = terminalDivider.rectTransform.anchorMax = new Vector2(.5f, 1f);
        terminalDivider.rectTransform.pivot = new Vector2(.5f, 1f);
        readyLamp = TerminalRule(chrome, "Ready Indicator", new Vector2(565f, 296f), new Vector2(6f, 6f));
        readyLamp.color = new Color32(43, 99, 126, 255);
        readyLamp.rectTransform.anchorMin = readyLamp.rectTransform.anchorMax = Vector2.one;
        readyLamp.rectTransform.anchoredPosition = new Vector2(-25f, -18f);

        var title = TerminalRect("Title Geometry", frame, new Vector2(-266f, 0f), new Vector2(560f, 420f));
        terminalTitleLines = new[]
        {
            CreateTitleText(title, "GHOST", new Vector2(0f, 102f), 168).gameObject.AddComponent<CanvasGroup>(),
            CreateTitleText(title, "out of the", Vector2.zero, 36).gameObject.AddComponent<CanvasGroup>(),
            CreateTitleText(title, "SHELL", new Vector2(0f, -102f), 168).gameObject.AddComponent<CanvasGroup>()
        };
        BuildTitleRevealMasks(title);

        mainOptionsRoot = TerminalRect("Title Options", frame, Vector2.zero, Vector2.zero);
        ConfigureOptionsRoot(mainOptionsRoot);
        var first = CreateOption(mainOptionsRoot, "开始新游戏", 144f, true, StartGame);
        continueButton = CreateOption(mainOptionsRoot, "继续游戏", 72f, true, () => GameSaveSystem.Instance?.ContinueLatest());
        var progress = CreateOption(mainOptionsRoot, "游戏进度", 0f, true, ShowProfiles);
        var settings = CreateOption(mainOptionsRoot, "游戏设置（暂未开放）", -72f, false, null);
        var quit = CreateOption(mainOptionsRoot, "结束游戏", -144f, true, QuitGame);
        terminalRows = new[]
        {
            first.gameObject.AddComponent<CanvasGroup>(), continueButton.gameObject.AddComponent<CanvasGroup>(),
            progress.gameObject.AddComponent<CanvasGroup>(), settings.gameObject.AddComponent<CanvasGroup>(),
            quit.gameObject.AddComponent<CanvasGroup>()
        };
        terminalWriters = new[]
        {
            first.GetComponent<TitleTerminalOption>(), continueButton.GetComponent<TitleTerminalOption>(),
            progress.GetComponent<TitleTerminalOption>(), settings.GetComponent<TitleTerminalOption>(),
            quit.GetComponent<TitleTerminalOption>()
        };
        mainStatus = CreateMenuLabel(mainOptionsRoot, "", -238f, 18);
        mainStatus.rectTransform.sizeDelta = new Vector2(440f, 64f);

        bootCursor = TerminalRule(frame, "Boot Cursor", new Vector2(22f, -20f), new Vector2(8f, 12f));
        bootLine = TerminalRule(frame, "Boot Line", Vector2.zero, new Vector2(36f, 2f));
        RefreshContinueButton();
        ExhibitionEdition.AddMenuCaption(transform, titleFont);
        BuildTerminalAudio();
        RenderTerminalBoot(0f);
    }

    private static RectTransform TerminalRect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var rect = CreateUiObject(name, parent).GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static Image TerminalRule(Transform parent, string name, Vector2 position, Vector2 size)
    {
        var image = TerminalRect(name, parent, position, size).gameObject.AddComponent<Image>();
        image.color = GhostBlue;
        image.raycastTarget = false;
        TitleScreenGlow.Attach(image);
        EnhanceTerminalGlow(image);
        return image;
    }

    private IEnumerator BootTerminal()
    {
        // The loading curtain must finish first, otherwise it hides the boot sequence.
        while (RetroSceneLoadReveal.IsBlockingInput) yield return null;
        float started = Time.unscaledTime;
        bool sounded = false;
        while (Time.unscaledTime - started < TerminalBootDuration)
        {
            if (!sounded && Time.unscaledTime - started >= .30f)
            {
                sounded = true;
                PlayTerminalCue(terminalBootSound);
            }
            RenderTerminalBoot(Time.unscaledTime - started);
            yield return null;
        }
        RenderTerminalBoot(TerminalBootDuration);
        // Do not interpret a held Enter used on the outgoing screen as a new command.
        while (Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter) || Input.GetMouseButton(0))
            yield return null;
        terminalReady = true;
        terminalInput.interactable = true;
        terminalInput.blocksRaycasts = true;
        suppressSelectionSound = true;
        RestoreTerminalSelection();
        suppressSelectionSound = false;
    }

    private void RestoreTerminalSelection()
    {
        if (!terminalReady || EventSystem.current == null || SaveSlotPanel.IsOpen || RetroSceneLoadReveal.IsBlockingInput) return;
        RectTransform root = mainOptionsRoot.gameObject.activeSelf ? mainOptionsRoot : progressOptionsRoot;
        if (root == null || !root.gameObject.activeInHierarchy) return;
        var current = EventSystem.current.currentSelectedGameObject;
        if (current != null && current.transform.IsChildOf(root))
        {
            var button = current.GetComponent<Button>();
            if (button != null && button.IsActive() && button.IsInteractable()) return;
        }
        // Only queried on a screen change or when a selected save becomes unavailable.
        foreach (var button in root.GetComponentsInChildren<Button>())
            if (button.IsActive() && button.IsInteractable())
            {
                bool wasSuppressed = suppressSelectionSound;
                suppressSelectionSound = true;
                button.Select();
                suppressSelectionSound = wasSuppressed;
                break;
            }
    }
}
