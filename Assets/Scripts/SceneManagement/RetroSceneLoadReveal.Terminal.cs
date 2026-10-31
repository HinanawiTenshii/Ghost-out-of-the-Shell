using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class RetroSceneLoadReveal
{
    private enum LoadingPhase { None, Covering, Reading, Restoring, Preparing, Revealing }
    private static readonly Color TerminalCyan = new Color32(76, 204, 230, 255);
    private static readonly Color DimBand = new Color(0.005f, .017f, .035f, .78f);
    private const float StatusDelay = .6f;
    private TerminalSceneLoadStyle terminalStyle;
    private Image[] bandImages;
    private RectTransform scanRoot;
    private GameObject statusRoot;
    private Text areaLabel, statusLabel;
    private Image statusCursor;
    private AudioSource transitionAudio;
    private LoadingPhase loadingPhase;
    private float waitingStarted;
    private bool localPresentation;
    private string destinationLabel;
    private int lastScanStep = -1, lastCursorTick = -1;

    private void BuildTerminalPresentation()
    {
        terminalStyle = Resources.Load<TerminalSceneLoadStyle>("TerminalSceneLoadStyle");
        bandImages = new Image[blackBands.Length];
        for (int i = 0; i < blackBands.Length; i++) bandImages[i] = blackBands[i].GetComponent<Image>();

        scanRoot = TerminalRect("Refresh Scan Edge", canvasObject.transform, Vector2.zero, Vector2.zero);
        scanRoot.anchorMin = new Vector2(0f, 1f);
        scanRoot.anchorMax = Vector2.one;
        scanRoot.sizeDelta = Vector2.zero;
        // Local phosphor halos also work on this critical CRT-bypass overlay.
        // No bloom camera, extra render texture, full-screen white flash or shader readback.
        BuildScanLine("Diffuse Residue", 15f, new Color(.18f, .64f, .85f, .045f));
        BuildScanLine("Near Residue", 6f, new Color(.20f, .73f, .90f, .13f));
        BuildScanLine("Crisp Scan Core", 1.5f, new Color(.30f, .80f, .90f, .72f));
        SetScanVisible(false);

        var status = TerminalRect("Area Connection Status", canvasObject.transform, Vector2.zero, new Vector2(420f, 112f));
        statusRoot = status.gameObject;
        Font font = terminalStyle != null ? terminalStyle.font : null;
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        areaLabel = BuildLoadingLabel("Destination", status, font, 36, new Vector2(0f, 26f), TerminalCyan);
        statusLabel = BuildLoadingLabel("Actual Loading Phase", status, font, 24, new Vector2(0f, -24f), new Color32(82, 153, 177, 255));
        var cursor = TerminalRect("Activity Cursor", status, new Vector2(-196f, -24f), new Vector2(9f, 18f));
        statusCursor = cursor.gameObject.AddComponent<Image>();
        statusCursor.color = TerminalCyan;
        statusCursor.raycastTarget = false;
        TitleScreenGlow.Attach(statusCursor);
        statusRoot.SetActive(false);

        var soundObject = new GameObject("Scene Refresh Audio");
        soundObject.transform.SetParent(transform, false);
        transitionAudio = soundObject.AddComponent<AudioSource>();
        transitionAudio.playOnAwake = false;
        transitionAudio.loop = false;
        transitionAudio.spatialBlend = 0f;
        transitionAudio.ignoreListenerPause = true;
        transitionAudio.volume = terminalStyle != null ? terminalStyle.volume : .25f;
    }

    private static RectTransform TerminalRect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private void BuildScanLine(string name, float height, Color color)
    {
        var rect = TerminalRect(name, scanRoot, Vector2.zero, new Vector2(0f, height));
        rect.anchorMin = new Vector2(0f, .5f);
        rect.anchorMax = new Vector2(1f, .5f);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    private static Text BuildLoadingLabel(string name, Transform parent, Font font, int size, Vector2 position, Color color)
    {
        var rect = TerminalRect(name, parent, position, new Vector2(420f, 48f));
        var text = rect.gameObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.fontStyle = FontStyle.Normal;
        text.alignment = TextAnchor.MiddleLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.supportRichText = false;
        text.raycastTarget = false;
        text.color = color;
        TitleScreenGlow.Attach(text);
        text.GetComponent<TitleScreenGlow>().ConfigureHalo(.65f, .7f);
        return text;
    }

    private void ConfigurePresentation(string destination, bool localOnly)
    {
        localPresentation = localOnly;
        SetPresentationDestination(destination);
        loadingPhase = LoadingPhase.None;
        statusRoot.SetActive(false);
        SetScanVisible(false);
        lastCursorTick = -1;
    }

    private void SetPresentationDestination(string scene)
    {
        destinationLabel = terminalStyle != null ? terminalStyle.AreaName(scene) : string.Empty;
        areaLabel.text = destinationLabel;
        LayoutStatusText();
    }

    private void BeginLoadingWait()
    {
        waitingStarted = Time.unscaledTime;
        SetLoadingPhase(LoadingPhase.Reading);
    }

    private void SetLoadingPhase(LoadingPhase phase)
    {
        loadingPhase = phase;
        string label = phase == LoadingPhase.Reading ? "正在读取区域" :
            phase == LoadingPhase.Restoring ? "正在恢复状态" :
            phase == LoadingPhase.Preparing ? "正在准备视野" : string.Empty;
        if (statusLabel.text != label) statusLabel.text = label;
        LayoutStatusText();
        // No artificial percentages or minimum hold for a message. Fast transitions
        // and local trapdoor/arena/settlement transitions skip this UI altogether.
        if (label.Length == 0) statusRoot.SetActive(false);
        lastCursorTick = -1;
    }

    private void UpdateLoadingStatus()
    {
        bool waiting = loadingPhase == LoadingPhase.Reading || loadingPhase == LoadingPhase.Restoring || loadingPhase == LoadingPhase.Preparing;
        bool visible = waiting && !localPresentation && !string.IsNullOrEmpty(destinationLabel) &&
            Time.unscaledTime - waitingStarted >= StatusDelay;
        if (statusRoot.activeSelf != visible) statusRoot.SetActive(visible);
        if (!visible) return;
        int tick = Mathf.FloorToInt(Time.unscaledTime * 4f);
        if (lastCursorTick == tick) return;
        lastCursorTick = tick;
        statusCursor.enabled = (tick & 1) == 0;
    }

    private void LayoutStatusText()
    {
        // Centre the two-line GROUP, while keeping terminal text left-aligned.
        // Fixed-width text boxes would leave a short area name noticeably off-centre.
        float width = Mathf.Clamp(Mathf.Ceil(Mathf.Max(areaLabel.preferredWidth, statusLabel.preferredWidth + 28f)), 160f, 420f);
        areaLabel.rectTransform.sizeDelta = statusLabel.rectTransform.sizeDelta = new Vector2(width, 48f);
        statusCursor.rectTransform.anchoredPosition = new Vector2(-width * .5f + statusLabel.preferredWidth + 14f, -24f);
    }

    private void SetScanVisible(bool visible) => scanRoot.gameObject.SetActive(visible);

    private IEnumerator ScanScreen(bool reveal, float duration)
    {
        SetLoadingPhase(reveal ? LoadingPhase.Revealing : LoadingPhase.Covering);
        lastScanStep = -1;
        if (transitionPending && terminalStyle != null)
            PlayTransitionSound(reveal ? terminalStyle.scanSound : terminalStyle.beginSound);
        float start = Time.unscaledTime;
        // Advance by absolute unscaled time, not a separate Wait per band: lower
        // frame rates may skip a step but cannot multiply the transition duration.
        do
        {
            RenderScanStep(reveal, Mathf.FloorToInt(Mathf.Clamp01((Time.unscaledTime - start) / Mathf.Max(.01f, duration)) * (blackBands.Length + 2)));
            yield return null;
        } while (Time.unscaledTime - start < duration);
        RenderScanStep(reveal, blackBands.Length + 2);
        SetScanVisible(false);
    }

    private void RenderScanStep(bool reveal, int step)
    {
        if (step == lastScanStep) return;
        lastScanStep = step;
        int count = blackBands.Length;
        for (int row = 0; row < count; row++)
        {
            int age = step - row;
            Image image = bandImages[count - 1 - row];
            // Two discrete phosphor levels trail the scan edge. For covering,
            // these darken the OLD frame; for revealing, only the ready NEW frame.
            float alpha = reveal ? (age < 0 ? 1f : age == 0 ? .78f : age == 1 ? .42f : 0f)
                                 : (age < 0 ? 0f : age == 0 ? .42f : age == 1 ? .78f : 1f);
            bool active = alpha > 0f;
            if (image.gameObject.activeSelf != active) image.gameObject.SetActive(active);
            if (!active) continue;
            Color color = alpha == 1f ? Color.black : new Color(DimBand.r, DimBand.g, DimBand.b, alpha);
            if (image.color != color) image.color = color;
        }
        SetScanVisible(step < count);
        float height = 1f - Mathf.Clamp01((step + 1f) / count);
        scanRoot.anchorMin = new Vector2(0f, height);
        scanRoot.anchorMax = new Vector2(1f, height);
    }

    private void PlayTransitionSound(AudioClip clip)
    {
        if (transitionAudio == null || clip == null) return;
        transitionAudio.Stop();
        transitionAudio.clip = clip;
        transitionAudio.Play();
    }

    private void FinishPresentation()
    {
        SetLoadingPhase(LoadingPhase.None);
        SetScanVisible(false);
        if (transitionPending && terminalStyle != null) PlayTransitionSound(terminalStyle.readySound);
    }
}
