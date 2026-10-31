using UnityEngine;
using UnityEngine.UI;

public sealed partial class TitleScreenController
{
    private const float TerminalBootDuration = 2.3f;
    private Image bootLine, terminalHeader, terminalFooter, terminalDivider;
    private GameObject terminalInnerFrame;
    private RectMask2D[] titleRevealMasks;
    private Text[] terminalTitleLabels;
    private TitleTerminalOption[] terminalWriters;
    private int lastBootTick = -1;

    private static void EnhanceTerminalGlow(Graphic graphic)
    {
        // Opt-in per graphic. Gameplay UI and the fake title keep their existing halos.
        var glow = graphic.GetComponent<TitleScreenGlow>();
        if (glow == null) return;
        // Chinese menu glyphs are denser than the large logo. Give them a tighter
        // halo so enhanced phosphor does not bridge adjacent strokes/characters.
        if (graphic is Text text && text.fontSize < 60) glow.ConfigureHalo(1.1f, 1.45f);
        else glow.ConfigureHalo(1.6f, 1.75f);
    }

    private void BuildTitleRevealMasks(RectTransform parent)
    {
        titleRevealMasks = new RectMask2D[terminalTitleLines.Length];
        terminalTitleLabels = new Text[terminalTitleLines.Length];
        for (int i = 0; i < terminalTitleLines.Length; i++)
        {
            var text = terminalTitleLines[i].GetComponent<Text>();
            var rect = text.rectTransform;
            // Text never scales or reflows while revealing; only the clipping edge moves.
            var viewport = TerminalRect("Title Reveal " + i, parent, rect.anchoredPosition, rect.sizeDelta + Vector2.one * 32f);
            rect.SetParent(viewport, false);
            rect.anchoredPosition = Vector2.zero;
            titleRevealMasks[i] = viewport.gameObject.AddComponent<RectMask2D>();
            terminalTitleLabels[i] = text;
        }
    }

    private static float BootSteps(float elapsed, float start, float duration, int steps)
    {
        return Mathf.Floor(Mathf.Clamp01((elapsed - start) / duration) * steps + .0001f) / steps;
    }

    private void RenderTerminalBoot(float elapsed)
    {
        // Low-frame terminal motions, not smooth tweens. Stop all rebuild work when ready.
        int tick = Mathf.FloorToInt(elapsed * 30f + .001f);
        if (tick == lastBootTick) return;
        lastBootTick = tick;
        bool complete = elapsed >= TerminalBootDuration;
        bootLine.enabled = elapsed < .30f;
        bootLine.rectTransform.sizeDelta = new Vector2(Mathf.Lerp(36f, 1180f, BootSteps(elapsed, 0f, .30f, 8)), 2f);
        bootCursor.enabled = elapsed < .30f && (int)(elapsed / .1f) % 2 == 0;

        float unfold = BootSteps(elapsed, .30f, .50f, 10);
        var chrome = (RectTransform)terminalFrame.transform;
        chrome.sizeDelta = new Vector2(1180f, Mathf.Lerp(8f, 628f, unfold));
        terminalFrame.alpha = elapsed >= .30f ? 1f : 0f;
        terminalInnerFrame.SetActive(unfold >= .1f);
        terminalHeader.enabled = terminalFooter.enabled = unfold >= .2f;
        float divider = BootSteps(elapsed, .65f, .25f, 6);
        terminalDivider.enabled = divider > 0f;
        terminalDivider.rectTransform.sizeDelta = new Vector2(1f, 528f * divider);

        for (int i = 0; i < terminalTitleLines.Length; i++)
        {
            float start = i == 0 ? .72f : i == 1 ? .94f : 1.02f;
            float progress = BootSteps(elapsed, start, i == 1 ? .24f : .48f, i == 1 ? 3 : 8);
            var mask = titleRevealMasks[i];
            mask.enabled = progress < 1f;
            float bottom = ((RectTransform)mask.transform).rect.height * (1f - progress);
            mask.padding = new Vector4(0f, bottom, 0f, 0f);
            terminalTitleLines[i].alpha = progress > 0f ? 1f : 0f;
            // Three distinct phosphor levels; never flash to white or distort the glyphs.
            float brightness = progress < .34f ? .45f : progress < .75f ? .72f : 1f;
            Color color = GhostBlue * brightness;
            color.a = 1f;
            terminalTitleLabels[i].color = color;
        }

        int writingRow = Mathf.Clamp(Mathf.FloorToInt((elapsed - 1.30f) / .15f), 0, terminalRows.Length - 1);
        for (int i = 0; i < terminalRows.Length; i++)
        {
            float start = 1.30f + i * .15f;
            terminalRows[i].alpha = elapsed >= start ? 1f : 0f;
            if (complete) terminalWriters[i].EndBootWriting();
            else terminalWriters[i].SetBootWriting(BootSteps(elapsed, start, .20f, 4),
                i == writingRow && elapsed >= start && elapsed < 2.10f);
        }
        readyLamp.enabled = unfold >= .9f;
        bool readyPulse = complete || elapsed >= 2.10f && (int)((elapsed - 2.10f) / .1f) % 2 == 0;
        readyLamp.color = readyPulse ? GhostBlue : new Color32(43, 99, 126, 255);
    }
}
