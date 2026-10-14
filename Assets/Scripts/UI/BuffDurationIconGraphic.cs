using UnityEngine;
using UnityEngine.UI;

public enum BuffDurationIcon
{
    Vitality = 0,
    Spirit = 1,
    Death = 2,
    Might = 3,
    Veil = 4,
    Berserk = 5,
    Sight = 6,
    Weakness = 7,
    Clamor = 8,
    Echo = 9,
    EchoImmunity = 10,
    CombatExpertise = 11,
    GhostForm = 12,
    RoyalCommand = 13,
    SoulMark = 14,
    Domino = 15,
    HolyShield = 16
}

/// <summary>
/// A dark square, a clockwise-erasing inner timer and a monochrome potion glyph.
/// Skill effects reuse their original sprite through a lazily-created child image.
/// Untimed effects retain the thin frame without drawing a countdown band.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class BuffDurationIconGraphic : MaskableGraphic
{
    [SerializeField] private BuffDurationIcon icon;
    [SerializeField, Range(0f, 1f)] private float remaining01 = 1f;

    private bool skillDisplay;
    private bool isTimed = true;
    private Sprite skillSprite;
    private Image skillImage;

    public BuffDurationIcon Icon => icon;
    public float Remaining01 => remaining01;
    public Sprite SkillSprite => skillSprite;
    public bool IsTimed => isTimed;

    private static readonly Color Background = new Color(.035f, .065f, .18f, 1f);
    private static readonly Color TimerTrack = new Color(.055f, .13f, .27f, 1f);

    // Twelve-pixel silhouettes use broad strokes and deliberate negative space.
    // One row run becomes one quad, rather than one quad for every filled pixel.
    private static readonly string[][] Glyphs =
    {
        new[] // Vitality: life-restoring cross.
        {
            "............", "....####....", "....####....", "....####....",
            ".##########.", ".##########.", ".##########.", ".##########.",
            "....####....", "....####....", "....####....", "............"
        },
        new[] // Spirit: a faceted energy crystal.
        {
            ".....##.....", "....####....", "...##.###...", "..##..####..",
            ".##...#####.", "##....######", "######....##", ".#####...##.",
            "..####..##..", "...###.##...", "....####....", ".....##....."
        },
        new[] // Death: hollow-eyed skull (instant effects normally have no tile).
        {
            "...######...", "..########..", ".##########.", ".##..##..##.",
            ".##..##..##.", ".##########.", "..###..###..", "..########..",
            "...######...", "...##..##...", "...##..##...", "............"
        },
        new[] // Might: curled fist above a broad wrist.
        {
            "....##......", "..####.##...", ".##########.", ".##.##.##.##",
            ".###########", "##..########", "##...#######", "###..######.",
            ".#########..", "..#######...", "...######...", "...######..."
        },
        new[] // Veil: fragmented humanoid outline.
        {
            "....####....", "....####....", "............", "...######...",
            "..##....##..", "..##....##..", "............", "..##....##..",
            "..##....##..", "...##..##...", "............", "...##..##..."
        },
        new[] // Berserk: crossed blades and their short guards.
        {
            "##........##", "###......###", ".###....###.", "..###..###..",
            "...######...", "....####....", "...######...", "..###..###..",
            ".###....###.", "#####..#####", "..##....##..", ".##......##."
        },
        new[] // Sight: an open eye, with a bright central pupil.
        {
            "............", "............", "....####....", "..########..",
            ".###....###.", "##...##...##", "##...##...##", ".###....###.",
            "..########..", "....####....", "............", "............"
        },
        new[] // Weakness: a heavy downward arrow.
        {
            "....####....", "....####....", "............", "....####....",
            "....####....", "....####....", "############", ".##########.",
            "..########..", "...######...", "....####....", ".....##....."
        },
        new[] // Clamor: two circular wavefronts expanding around a central source.
        {
            "....####....", "..##....##..", ".#........#.", ".#..####..#.",
            "#..#....#..#", "#..#.##.#..#", "#..#.##.#..#", "#..#....#..#",
            ".#..####..#.", ".#........#.", "..##....##..", "....####...."
        },
        new[] // Echo: a returning arc with a broad, downward triangular arrowhead.
        {
            "....#####...", "..########..", ".###....###.", "###......##.",
            "##.....#####", "##...##.###.", "##...##..#..", "###.........",
            ".###........", "..###...##..", "...#######..", "....#####..."
        },
        new[] // Echo's brief immunity: a solid protective shield with a cross.
        {
            "..########..", ".##########.", ".####..####.", ".####..####.",
            ".##......##.", ".##......##.", ".####..####.", "..###..###..",
            "..########..", "...######...", "....####....", ".....##....."
        }
    };

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        raycastTarget = false;
        RefreshSkillImage();
    }

    protected override void OnDisable()
    {
        // Disabling the Graphic alone does not disable its child graphics.
        if (skillImage != null) skillImage.enabled = false;
        base.OnDisable();
    }

    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        ResizeSkillImage();
    }

    public void SetDisplay(BuffDurationIcon newIcon, float newRemaining01)
    {
        newRemaining01 = SanitizeRemaining(newRemaining01);
        if (!skillDisplay && isTimed && icon == newIcon && remaining01 == newRemaining01) return;
        icon = newIcon;
        remaining01 = newRemaining01;
        skillDisplay = false;
        isTimed = true;
        skillSprite = null;
        if (skillImage != null)
        {
            skillImage.enabled = false;
            skillImage.sprite = null;
        }
        SetVerticesDirty();
    }

    public void SetSkillDisplay(BuffDurationIcon kind, Sprite sprite, float remaining01, bool timed = true)
    {
        remaining01 = SanitizeRemaining(remaining01);
        bool meshChanged = !skillDisplay || icon != kind || this.remaining01 != remaining01 || isTimed != timed;
        bool spriteChanged = !skillDisplay || skillSprite != sprite;
        icon = kind;
        this.remaining01 = remaining01;
        skillDisplay = true;
        isTimed = timed;
        skillSprite = sprite;
        if (spriteChanged) RefreshSkillImage();
        if (meshChanged) SetVerticesDirty();
    }

    private void RefreshSkillImage()
    {
        bool show = isActiveAndEnabled && skillDisplay && skillSprite != null;
        if (show && skillImage == null)
        {
            // This child belongs to the pooled tile and is reused on every refresh.
            var child = new GameObject("Skill Sprite", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            child.layer = gameObject.layer;
            child.transform.SetParent(transform, false);
            skillImage = child.GetComponent<Image>();
            skillImage.raycastTarget = false;
            skillImage.preserveAspect = true;
            skillImage.type = Image.Type.Simple;
            skillImage.color = ZeldaUiPalette.Primary;
            RectTransform imageRect = skillImage.rectTransform;
            imageRect.anchorMin = imageRect.anchorMax = new Vector2(.5f, .5f);
            imageRect.pivot = new Vector2(.5f, .5f);
            imageRect.anchoredPosition = Vector2.zero;
            ResizeSkillImage();
        }

        if (skillImage == null) return;
        skillImage.sprite = skillSprite;
        skillImage.enabled = show;
    }

    private void ResizeSkillImage()
    {
        if (skillImage == null) return;
        Rect bounds = rectTransform.rect;
        float size = Mathf.Max(0f, Mathf.Min(bounds.width, bounds.height) * .6f);
        skillImage.rectTransform.sizeDelta = new Vector2(size, size);
    }

    /// <summary>
    /// The fraction still illuminated on one clockwise edge: 0 top, 1 right,
    /// 2 bottom, 3 left. At 75% remaining the top edge has just disappeared.
    /// </summary>
    public static float GetEdgeRemaining(int clockwiseEdge, float remaining)
    {
        if (clockwiseEdge < 0 || clockwiseEdge > 3) return 0f;
        return Mathf.Clamp01(SanitizeRemaining(remaining) * 4f - (3 - clockwiseEdge));
    }

    private static float SanitizeRemaining(float remaining)
    {
        return float.IsNaN(remaining) ? 0f : Mathf.Clamp01(remaining);
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect bounds = rectTransform.rect;
        float scale = Mathf.Min(bounds.width, bounds.height) / 48f;
        if (scale <= 0f) return;

        Vector2 center = bounds.center;
        Color primary = ZeldaUiPalette.Primary;
        AddRect(vh, center, scale, -24f, -24f, 48f, 48f, Background);

        // Thin, permanent outer frame. The countdown is a separate inset band.
        AddRect(vh, center, scale, -24f, 23f, 48f, 1f, primary);
        AddRect(vh, center, scale, -24f, -24f, 48f, 1f, primary);
        AddRect(vh, center, scale, -24f, -23f, 1f, 46f, primary);
        AddRect(vh, center, scale, 23f, -23f, 1f, 46f, primary);

        if (isTimed)
        {
            for (int edge = 0; edge < 4; edge++)
            {
                AddTimerEdge(vh, center, scale, edge, 1f, TimerTrack);
                float remaining = GetEdgeRemaining(edge, remaining01);
                if (remaining > 0f) AddTimerEdge(vh, center, scale, edge, remaining, primary);
            }
        }

        if (skillDisplay) return;

        int index = Mathf.Clamp((int)icon, 0, Glyphs.Length - 1);
        string[] rows = Glyphs[index];
        const float unit = 1.75f;
        for (int y = 0; y < rows.Length; y++)
        {
            string row = rows[y];
            int x = 0;
            while (x < row.Length)
            {
                if (row[x] != '#') { x++; continue; }
                int start = x;
                while (x < row.Length && row[x] == '#') x++;
                AddRect(vh, center, scale, (start - row.Length * .5f) * unit,
                    (rows.Length * .5f - y - 1f) * unit, (x - start) * unit, unit, primary);
            }
        }
    }

    private static void AddTimerEdge(VertexHelper vh, Vector2 center, float scale,
        int edge, float remaining, Color tint)
    {
        // Mitered corners partition the ring exactly: neighboring segments never
        // overlap or leave little corner remnants after an edge has emptied.
        float erased = 1f - remaining;
        Vector2 outerStart = new Vector2(Mathf.Lerp(-20f, 20f, erased), 20f);
        Vector2 innerStart = new Vector2(Mathf.Lerp(-16f, 16f, erased), 16f);
        AddQuad(vh, center + RotateEdge(outerStart, edge) * scale,
            center + RotateEdge(new Vector2(20f, 20f), edge) * scale,
            center + RotateEdge(new Vector2(16f, 16f), edge) * scale,
            center + RotateEdge(innerStart, edge) * scale, tint);
    }

    private static Vector2 RotateEdge(Vector2 point, int edge)
    {
        switch (edge)
        {
            case 1: return new Vector2(point.y, -point.x);
            case 2: return -point;
            case 3: return new Vector2(-point.y, point.x);
            default: return point;
        }
    }

    private static void AddRect(VertexHelper vh, Vector2 center, float scale,
        float x, float y, float width, float height, Color tint)
    {
        AddQuad(vh, center + new Vector2(x, y) * scale,
            center + new Vector2(x, y + height) * scale,
            center + new Vector2(x + width, y + height) * scale,
            center + new Vector2(x + width, y) * scale, tint);
    }

    private static void AddQuad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c,
        Vector2 d, Color tint)
    {
        int first = vh.currentVertCount;
        vh.AddVert(a, tint, Vector2.zero);
        vh.AddVert(b, tint, Vector2.zero);
        vh.AddVert(c, tint, Vector2.zero);
        vh.AddVert(d, tint, Vector2.zero);
        vh.AddTriangle(first, first + 1, first + 2);
        vh.AddTriangle(first, first + 2, first + 3);
    }
}
