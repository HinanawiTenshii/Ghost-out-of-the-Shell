using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Uses uGUI's registry: no per-frame scene scans, palette writes, or forced map rebuilds.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(Canvas))]
public sealed class VisualStyleLabTerminalScope : MonoBehaviour
{
    private Canvas owner;
    private RuntimeUiGlowScope originalScope;
    private bool originalScopeEnabled, restored;
    private readonly HashSet<Graphic> visited = new HashSet<Graphic>();
    private readonly List<Component> additions = new List<Component>();
    private readonly Dictionary<TitleScreenGlow, bool> oldGlows = new Dictionary<TitleScreenGlow, bool>();
    private readonly List<TitleScreenGlow> deadGlows = new List<TitleScreenGlow>();
    private int lastFrame = -1, nextCleanup;

    private void Awake()
    {
        owner = GetComponent<Canvas>();
        CaptureGlowScope();
    }
    public void CaptureGlowScope()
    {
        if (!originalScope)
        {
            originalScope = GetComponent<RuntimeUiGlowScope>();
            originalScopeEnabled = originalScope != null && originalScope.enabled;
        }
        if (originalScope) originalScope.enabled = false;
    }
    private void OnEnable() { lastFrame = -1; Canvas.preWillRenderCanvases += Refresh; }
    private void OnDisable() { Canvas.preWillRenderCanvases -= Refresh; }
    private void OnDestroy() { Restore(); }

    public void Restore()
    {
        if (restored) return;
        restored = true; enabled = false;
        foreach (var c in additions)
        {
            if (!c) continue;
            if (c is VisualStyleLabTerminalChrome) Destroy(c.gameObject);
            else { if (c is Behaviour b) b.enabled = false; Destroy(c); }
        }
        foreach (var glow in oldGlows) if (glow.Key) glow.Key.enabled = glow.Value;
        if (originalScope) originalScope.enabled = originalScopeEnabled;
        additions.Clear(); oldGlows.Clear(); visited.Clear();
    }

    private void Refresh()
    {
        if (restored || !owner || !owner.isActiveAndEnabled || lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;
        var graphics = GraphicRegistry.GetGraphicsForCanvas(owner);
        // Adding chrome extends this registry. It is explicitly excluded below.
        for (int i = 0; i < graphics.Count; i++)
        {
            Graphic g = graphics[i];
            if (!g || !g.isActiveAndEnabled || g is VisualStyleLabTerminalChrome || visited.Contains(g)) continue;
            if (g.rectTransform.rect.width < .1f || g.rectTransform.rect.height < .1f) continue;
            visited.Add(g); Style(g);
        }
        if (Time.frameCount >= nextCleanup)
        {
            visited.RemoveWhere(g => !g);
            additions.RemoveAll(c => !c);
            deadGlows.Clear();
            foreach (var pair in oldGlows) if (!pair.Key) deadGlows.Add(pair.Key);
            foreach (var dead in deadGlows) oldGlows.Remove(dead);
            nextCleanup = Time.frameCount + 120;
        }
    }

    private void Style(Graphic g)
    {
        var glow = g.GetComponent<TitleScreenGlow>();
        if (glow) { oldGlows[glow] = glow.enabled; glow.enabled = false; }
        if (g is Text text)
        {
            // Only headings get halos. Body text, counters and options retain sharp original glyphs.
            if (text.fontSize >= 26 || g.name == "Quest Name")
            {
                if (glow) glow.enabled = true;
                else { TitleScreenGlow.Attach(g); glow = g.GetComponent<TitleScreenGlow>(); additions.Add(glow); glow.ConfigureHalo(.55f, .45f); }
            }
            else Ink(g, VisualStyleLabTerminalInk.Treatment.Text);
            return;
        }
        if (g is RuntimeMiniMapLayerGraphic map)
        {
            if (map.DrawWalls) Ink(g, VisualStyleLabTerminalInk.Treatment.MapWalls);
            else if (map.DrawSurfaces) Ink(g, VisualStyleLabTerminalInk.Treatment.MapSurfaces);
            return; // Markers and POI geometry remain unchanged.
        }
        if (!(g is Image image) || image.GetComponent<Mask>() || image.GetComponent<RectMask2D>()) return;
        string n = g.name;
        bool window = IsWindow(n);
        bool hudPanel = n == "Inventory Placeholder";
        if (window || hudPanel)
        {
            var ink = Ink(g, image.sprite == null ? VisualStyleLabTerminalInk.Treatment.Panel : VisualStyleLabTerminalInk.Treatment.Border);
            ink.SolidWindow = window;
            var corners = Chrome(g.rectTransform, false);
            if (window)
            {
                var animation = g.gameObject.AddComponent<VisualStyleLabTerminalWindow>();
                additions.Add(animation); animation.Configure(ink, corners);
            }
            return;
        }
        bool spriteFrame = n == "Task Indicator Slot" || n == "Task Pointer Label Frame" ||
            n == "Requirement Panel" || n == "Permission Frame" || n.StartsWith("Inventory Slot ") || n.Contains("Popup Border");
        bool rule = image.sprite == null && Mathf.Min(g.rectTransform.rect.width, g.rectTransform.rect.height) <= 8;
        if (spriteFrame || rule)
        {
            Ink(g, VisualStyleLabTerminalInk.Treatment.Border);
            if (n == "Task Indicator Slot" || n == "Requirement Panel" || n == "Permission Frame") Chrome(g.rectTransform, true);
        }
    }
    private static bool IsWindow(string n) => n == "Item Description Panel" || n == "Document Panel" ||
        n == "Pause Menu Panel" || n == "Empty Tab Content Panel" || n == "Right Dialogue Panel" ||
        n == "Character Attribute Panel" || n == "Slot Panel";
    private VisualStyleLabTerminalInk Ink(Graphic g, VisualStyleLabTerminalInk.Treatment mode)
    {
        // Restored/cloned popup graphics may already carry the treatment even
        // though this new canvas scope has not visited them. Reuse it rather
        // than violating DisallowMultipleComponent during the scene handoff.
        var ink = g.GetComponent<VisualStyleLabTerminalInk>();
        if (ink == null)
        {
            ink = g.gameObject.AddComponent<VisualStyleLabTerminalInk>();
            additions.Add(ink);
        }
        ink.Mode = mode;
        return ink;
    }
    private VisualStyleLabTerminalChrome Chrome(RectTransform rect, bool compact)
    {
        var chrome = VisualStyleLabTerminalChrome.Create(rect, compact); additions.Add(chrome); return chrome;
    }
}
