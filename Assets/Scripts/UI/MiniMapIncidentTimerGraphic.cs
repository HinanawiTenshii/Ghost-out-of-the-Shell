using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// A separate inset countdown around the HUD mini-map. Reads the shared scene
/// clock, including its pause, visibility reset and save/load rules; never ticks it.
/// </summary>
[DisallowMultipleComponent, RequireComponent(typeof(CanvasRenderer))]
public sealed class MiniMapIncidentTimerGraphic : MaskableGraphic
{
    [SerializeField, Min(0.5f)] private float borderWidth = 2f;
    private Graphic miniMap;
    private SharedPlayerHostilityTimer.IncidentPhase displayedPhase;
    private float remaining01;
    public SharedPlayerHostilityTimer.IncidentPhase DisplayedPhase => displayedPhase;
    public float Remaining01 => remaining01;
    public bool HasIncident => displayedPhase != SharedPlayerHostilityTimer.IncidentPhase.Idle;

    public static readonly Color HostileColor = new Color32(255, 48, 60, 255);
    public static readonly Color SearchColor = new Color32(255, 219, 48, 255);
    private static readonly Color TrackColor = new Color32(7, 15, 38, 255);

    protected override void Awake() { base.Awake(); raycastTarget = false; }
    protected override void OnEnable() { base.OnEnable(); raycastTarget = false; RefreshFromScene(); RefreshQuestBorderInset(); }

    public void Bind(Graphic map)
    {
        if (miniMap is RuntimeMiniMapGraphic previous) previous.SetQuestBorderInset(0f);
        miniMap = map;
        RefreshFromScene();
        RefreshQuestBorderInset();
    }

    protected override void OnDisable()
    {
        if (miniMap is RuntimeMiniMapGraphic map) map.SetQuestBorderInset(0f);
        base.OnDisable();
    }

    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        RefreshQuestBorderInset();
    }

    private void RefreshQuestBorderInset()
    {
        if (!(miniMap is RuntimeMiniMapGraphic map) || map == null) return;
        if (!isActiveAndEnabled || !HasIncident) { map.SetQuestBorderInset(0f); return; }
        Rect bounds = rectTransform.rect;
        float size = Mathf.Min(bounds.width, bounds.height);
        float half = Mathf.Max(0f, size * .5f - Mathf.Clamp(borderWidth, .5f, size * .1f) - 1f);
        Matrix4x4 toMap = map.rectTransform.worldToLocalMatrix * rectTransform.localToWorldMatrix;
        Vector2 min = toMap.MultiplyPoint3x4(bounds.center - Vector2.one * half);
        Vector2 max = toMap.MultiplyPoint3x4(bounds.center + Vector2.one * half);
        Rect viewport = map.GetPixelAdjustedRect();
        float inset = Mathf.Max(Mathf.Max(min.x - viewport.xMin, viewport.xMax - max.x),
            Mathf.Max(min.y - viewport.yMin, viewport.yMax - max.y));
        map.SetQuestBorderInset(inset);
    }

    // Independent of ZeldaHealthHeartsUI.Update: that event-driven script sleeps
    // whenever its short animations finish, even while the player is being chased.
    private void LateUpdate() => RefreshFromScene();

    public void RefreshFromScene()
    {
        if (miniMap == null || !miniMap.isActiveAndEnabled || GameSaveSystem.IsLoading ||
            !SharedPlayerHostilityTimer.TryGetForScene(SceneManager.GetActiveScene(), out var timer) ||
            !timer.isActiveAndEnabled)
        {
            SetDisplay(SharedPlayerHostilityTimer.IncidentPhase.Idle, 0f);
            return;
        }
        float duration = timer.Phase == SharedPlayerHostilityTimer.IncidentPhase.Hostile
            ? timer.HostileDuration : timer.SearchDuration;
        SetDisplay(timer.Phase, timer.Remaining / Mathf.Max(.1f, duration));
    }

    public void SetDisplay(SharedPlayerHostilityTimer.IncidentPhase phase, float fraction)
    {
        fraction = float.IsNaN(fraction) ? 0f : Mathf.Clamp01(fraction);
        if (phase != SharedPlayerHostilityTimer.IncidentPhase.Hostile &&
            phase != SharedPlayerHostilityTimer.IncidentPhase.Search)
        {
            phase = SharedPlayerHostilityTimer.IncidentPhase.Idle;
            fraction = 0f;
        }
        if (displayedPhase == phase && remaining01 == fraction) return;
        bool borderChanged = displayedPhase != phase;
        displayedPhase = phase;
        remaining01 = fraction;
        color = phase == SharedPlayerHostilityTimer.IncidentPhase.Search ? SearchColor : HostileColor;
        if (borderChanged) RefreshQuestBorderInset();
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (!HasIncident) return;
        Rect bounds = rectTransform.rect;
        float size = Mathf.Min(bounds.width, bounds.height);
        if (size <= 2f) return;
        Rect square = new Rect(bounds.center - Vector2.one * size * .5f, Vector2.one * size);
        float width = Mathf.Clamp(borderWidth, .5f, size * .1f);
        const float backing = .5f;
        // A narrow navy backing keeps the ring legible over bright map walls.
        // Mitered corners partition the ring without overlapping alpha or gaps.
        for (int edge = 0; edge < 4; edge++) AddEdge(vh, square, width + backing * 2f, edge, 1f, TrackColor);
        Rect band = Inset(square, backing);
        for (int edge = 0; edge < 4; edge++)
        {
            float remaining = BuffDurationIconGraphic.GetEdgeRemaining(edge, remaining01);
            if (remaining > 0f) AddEdge(vh, band, width, edge, remaining, color);
        }
    }

    private static Rect Inset(Rect rect, float amount)
        => new Rect(rect.xMin + amount, rect.yMin + amount, rect.width - amount * 2f, rect.height - amount * 2f);

    private static Vector2 Corner(Rect rect, int index)
    {
        switch (index & 3)
        {
            case 0: return new Vector2(rect.xMin, rect.yMax);
            case 1: return new Vector2(rect.xMax, rect.yMax);
            case 2: return new Vector2(rect.xMax, rect.yMin);
            default: return new Vector2(rect.xMin, rect.yMin);
        }
    }

    private static void AddEdge(VertexHelper vh, Rect outer, float width, int edge, float remaining, Color tint)
    {
        Rect inner = Inset(outer, width);
        Vector2 outerEnd = Corner(outer, edge + 1), innerEnd = Corner(inner, edge + 1);
        int first = vh.currentVertCount;
        vh.AddVert(Vector2.Lerp(Corner(outer, edge), outerEnd, 1f - remaining), tint, Vector2.zero);
        vh.AddVert(outerEnd, tint, Vector2.zero);
        vh.AddVert(innerEnd, tint, Vector2.zero);
        vh.AddVert(Vector2.Lerp(Corner(inner, edge), innerEnd, 1f - remaining), tint, Vector2.zero);
        vh.AddTriangle(first, first + 1, first + 2);
        vh.AddTriangle(first, first + 2, first + 3);
    }
}
