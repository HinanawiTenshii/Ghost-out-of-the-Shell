using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Inspection-only copies above the world dimmer; never edits gameplay renderers.</summary>
public sealed class AttributeSourceHighlight
{
    private readonly List<SpriteRenderer> sources = new List<SpriteRenderer>();
    private readonly List<AttributeHighlightGraphic> graphics = new List<AttributeHighlightGraphic>();
    private GameObject root;
    private Canvas canvas;
    private RectTransform canvasRect;
    private Transform owner;
    private bool visible;

    public void Show(Transform source, float alpha)
    {
        if (source == null || !source.gameObject.activeInHierarchy || alpha <= 0f)
        {
            Hide();
            return;
        }
        Camera camera = Camera.main;
        if (camera == null) { Hide(); return; }
        if (root == null) Build();
        if (!visible || owner != source) RefreshSources(source);
        visible = true;
        if (!root.activeSelf) root.SetActive(true);
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        for (int i = 0; i < sources.Count; i++)
        {
            SpriteRenderer renderer = sources[i];
            AttributeHighlightGraphic graphic = graphics[i];
            bool show = renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy &&
                renderer.sprite != null && renderer.color.a > 0f;
            graphic.gameObject.SetActive(show);
            if (!show) continue;
            Vector3 center = renderer.bounds.center;
            Vector2 size = renderer.drawMode == SpriteDrawMode.Simple ? (Vector2)renderer.sprite.bounds.size : renderer.size;
            Vector3 screen = camera.WorldToScreenPoint(center);
            Vector3 screenRight = camera.WorldToScreenPoint(center + renderer.transform.TransformVector(Vector3.right * size.x * .5f));
            Vector3 screenUp = camera.WorldToScreenPoint(center + renderer.transform.TransformVector(Vector3.up * size.y * .5f));
            if (screen.z <= 0f || !RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, uiCamera, out var local) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenRight, uiCamera, out var right) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenUp, uiCamera, out var up))
            {
                graphic.gameObject.SetActive(false);
                continue;
            }
            Vector2 x = right - local, y = up - local;
            RectTransform rect = graphic.rectTransform;
            rect.anchoredPosition = local;
            rect.sizeDelta = new Vector2(Mathf.Max(.1f, x.magnitude * 2), Mathf.Max(.1f, y.magnitude * 2));
            rect.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(x.y, x.x) * Mathf.Rad2Deg);
            // The transformed right vector already incorporates negative X scale.
            bool mirrored = x.x * y.y - x.y * y.x < 0;
            rect.localScale = new Vector3(renderer.flipX ? -1 : 1, renderer.flipY != mirrored ? -1 : 1, 1);
            Color tint = renderer.color;
            tint.a *= Mathf.Clamp01(alpha);
            graphic.SetSource(renderer.sprite, tint);
        }
    }

    public void Hide()
    {
        visible = false;
        if (root != null && root.activeSelf) root.SetActive(false);
    }

    public void Dispose()
    {
        Hide();
        if (root != null) Object.Destroy(root);
        root = null; owner = null;
        sources.Clear(); graphics.Clear();
    }

    private void Build()
    {
        root = new GameObject("Attribute Source White Highlight", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        root.layer = LayerMask.NameToLayer("UI");
        canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 30001; // Dimmer 30000, requirement/character windows 31000/31100.
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = .5f;
        canvasRect = (RectTransform)root.transform;
        CRTScreenEffect.RegisterCanvas(canvas); // No halo: preserve the exact source silhouette.
    }

    private void RefreshSources(Transform source)
    {
        owner = source;
        sources.Clear();
        source.GetComponentsInChildren(true, sources);
        sources.Sort(CompareRenderers);
        while (graphics.Count < sources.Count)
        {
            var go = new GameObject("Highlighted Source Sprite", typeof(RectTransform), typeof(CanvasRenderer), typeof(AttributeHighlightGraphic));
            go.layer = root.layer;
            go.transform.SetParent(canvasRect, false);
            var graphic = go.GetComponent<AttributeHighlightGraphic>();
            graphic.raycastTarget = false;
            graphic.material = AttributeHighlightGraphic.SharedMaterial;
            graphics.Add(graphic);
        }
        for (int i = sources.Count; i < graphics.Count; i++) graphics[i].gameObject.SetActive(false);
    }

    private static int CompareRenderers(SpriteRenderer a, SpriteRenderer b)
    {
        int layer = SortingLayer.GetLayerValueFromID(a.sortingLayerID).CompareTo(SortingLayer.GetLayerValueFromID(b.sortingLayerID));
        return layer != 0 ? layer : a.sortingOrder.CompareTo(b.sortingOrder);
    }
}

/// <summary>Original-size sprite with a white wash, without dilating its opaque pixels.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class AttributeHighlightGraphic : Image
{
    private static Material sharedMaterial;
    public static Material SharedMaterial
    {
        get
        {
            if (sharedMaterial == null)
            {
                var shader = Resources.Load<Shader>("UIAttributeSourceHighlight");
                if (shader != null) sharedMaterial = new Material(shader) { name = "Attribute Source White Highlight", hideFlags = HideFlags.HideAndDontSave };
            }
            return sharedMaterial;
        }
    }
    public void SetSource(Sprite sprite, Color tint)
    {
        this.sprite = sprite;
        color = tint;
    }
}
