using UnityEngine;

/// <summary>Floating, hooded visitor. Uses the normal possession, damage and Idle AI systems.</summary>
public sealed class OuterRealmVisitorZeldaCharacterData : ZeldaCharacterData
{
    [Header("Outer Realm Visitor / 域外来客")]
    [SerializeField, Min(0)] private int attackPower = 10;
    [SerializeField] private GameObject attackPrefab;
    [SerializeField, Min(.1f)] private float attackDuration = .6f;
    [SerializeField, Min(.5f)] private float attackReach = 3f;
    [SerializeField, Min(.1f)] private float attackWidth = 1.25f;
    [SerializeField] private Color robeColor = new Color(.34f, .16f, .49f, 1);
    [SerializeField] private Color trimColor = new Color(.57f, .35f, .72f, 1);
    [SerializeField] private Color eyeColor = new Color(.8f, .32f, 1f, 1);
    private const int CanvasWidth = 24, CanvasHeight = 28, Padding = 4;
    private readonly Sprite[,] frames = new Sprite[4, 4];
    private bool visualsDirty = true;
    private OuterRealmVisitorVisual effects;
    private Transform visualTransform;
    private SpriteRenderer visualRenderer;
    private int castDirection, castPose;
    private bool casting;
    private Object castSource;
    public Color EyeColor => eyeColor;
    public override bool CanAttack => !IsGhostForm;
    public override int AttackPower => attackPower;
    public override GameObject AttackPrefab => attackPrefab;
    public override float AttackDuration => attackDuration;
    public override Vector2 AttackSize => new Vector2(attackWidth, attackReach);
    public override Vector2 AttackSpawnOffset => new Vector2(0, .3f + attackReach * .5f);
    public override ZeldaAttackVisualShape AttackVisualShape => ZeldaAttackVisualShape.DirectionalWave;
    public override Color AttackVisualTint => new Color(.65f, .2f, 1f, .9f);
    public override Color GhostFormEyeColor => eyeColor;
    // The dedicated visual rig combines bobbing and damage shake without moving the collider.
    protected override bool UsesSingleRendererDamageFeedback => false;

    private static readonly string[] Front = {
        "......DDDD......",
        ".....DHHHHD.....",
        "....DHLLLLHD....",
        "....HLNNNNLH....",
        "....HNENNENH....",
        "....HNNNNNNH....",
        "....DHNNNNHD....",
        "....DTHTTHTD....",
        "....LTHHHLTL....",
        "....HTHHLHTH....",
        "....DTHHLHTD....",
        "....DTHHLHTD....",
        "....DTHHLHTD....",
        "....DTHHLHTD....",
        "...DDTHHLHTDD...",
        "...DHTHHLHTHD...",
        "...DLTHTTHTLD...",
        "...DTTTTTTTTD...",
        "....DDDDDDDD....",
        "................"
    };
    private static readonly string[] Back = {
        "......DDDD......",
        ".....DHHHHD.....",
        "....DHLLLLHD....",
        "....HHLHHLHH....",
        "....HHLHHLHH....",
        "....HHHDDHHH....",
        "....DHHHHHHD....",
        "....DTTTTTTD....",
        "....LHHLLHHL....",
        "....HHDLLDHH....",
        "....DHDLLDHD....",
        "....DHDLLDHD....",
        "....DHDLLDHD....",
        "....DHDLLDHD....",
        "...DDHDLLDHDD...",
        "...DHHDLLDHHD...",
        "...DLTTTTTTLD...",
        "...DTTTTTTTTD...",
        "....DDDDDDDD....",
        "................"
    };
    private static readonly string[] Left = {
        "......DDDD......",
        ".....DHHHHD.....",
        "....DHLLLHHD....",
        "....HNNLHHLHD...",
        "....NELHHHLHD...",
        "....NNLHHHHHD...",
        ".....NTTHHHD....",
        "....DTHHTTHD....",
        "....LTHLHHHHD...",
        "....HTHLHHHHD...",
        "....HTHLDHHHD...",
        "....HTHLDHHHD...",
        "....HTHLDHHHD...",
        "....HTHLDHHHD...",
        "...DHTHLDHHHD...",
        "...LHTHLHHHHD...",
        "...DTTTTTTLHD...",
        "...DTTTTTTTHD...",
        "....DDDDDDDD....",
        "................"
    };
    public static int DirectionIndex(Vector2 facing) => Mathf.Abs(facing.x) > Mathf.Abs(facing.y)
        ? (facing.x < 0 ? 2 : 3) : (facing.y > 0 ? 1 : 0);

    // Animation and energy share the same authored palm positions. Padding adds
    // room to reach beyond the robe without scaling the body or moving physics.
    public Vector3 GetAttackHandPosition(Vector2 facing)
    {
        int direction = DirectionIndex(facing);
        int pose = casting && direction == castDirection ? castPose : 0;
        Vector2 palm = PalmPixel(direction, pose);
        Vector2 forward = Facing(direction);
        // Start within the simple palm block so the wave visibly touches the
        // hand even under CRT filtering.
        Vector3 local = (palm + new Vector2(.5f - 8, .5f - 4) + forward * .5f) / 16;
        Transform rig = visualTransform != null ? visualTransform : transform.Find("Visual");
        return (rig != null ? rig : transform).TransformPoint(local);
    }

    public void SetCastProgress(Vector2 facing, float progress, Object source = null)
    {
        casting = true; castSource = source; castDirection = DirectionIndex(facing);
        castPose = progress < .12f ? 1 : progress < .24f ? 2 : progress < .85f ? 3 : progress < .94f ? 2 : 1;
        if (visualRenderer != null && !IsGhostForm && !IsDead)
            ApplyCharacterVisual(visualRenderer, facing, false, true);
    }

    public void EndCastVisual(Object source = null)
    {
        if (!casting) return;
        if (source != null && !ReferenceEquals(castSource, source)) return;
        casting = false; castPose = 0; castSource = null;
        if (visualRenderer != null && !IsGhostForm && !IsDead)
            ApplyCharacterVisual(visualRenderer, Facing(castDirection), false, false);
    }

    private static Vector2Int Facing(int direction) => direction == 0 ? Vector2Int.down :
        direction == 1 ? Vector2Int.up : direction == 2 ? Vector2Int.left : Vector2Int.right;

    private static Vector2Int PalmPixel(int direction, int pose)
    {
        Vector2Int palm;
        if (direction == 0) palm = pose == 0 ? new Vector2Int(3, 7) :
            new Vector2Int(4, pose == 1 ? 10 : pose == 2 ? 8 : 6);
        else if (direction == 1) palm = pose == 0 ? new Vector2Int(3, 7) :
            new Vector2Int(3, pose == 1 ? 10 : pose == 2 ? 12 : 14);
        else palm = pose == 0 ? new Vector2Int(8, 7) :
            new Vector2Int(pose == 1 ? 6 : pose == 2 ? 4 : 2, 10);
        if (direction == 3) palm.x = 15 - palm.x;
        return palm;
    }

    public override void ApplyCharacterVisual(SpriteRenderer renderer, Vector2 facing, bool moving, bool attacking)
    {
        if (renderer == null) return;
        visualTransform = renderer.transform;
        visualRenderer = renderer;
        if (visualsDirty) { ReleaseSprites(); visualsDirty = false; }
        int direction = DirectionIndex(facing);
        int pose = casting && direction == castDirection ? castPose : attacking ? 1 : 0;
        if (frames[direction, pose] == null) frames[direction, pose] = CreateSprite(direction, pose);
        renderer.sprite = frames[direction, pose];
        renderer.color = GetDamageFeedbackTint(Color.white);
        if (!Application.isPlaying) return;
        if (effects == null) effects = GetComponent<OuterRealmVisitorVisual>();
        if (effects != null) effects.Bind(renderer, direction);
    }
    private Sprite CreateSprite(int direction, int pose)
    {
        var pixels = new Color[CanvasWidth * CanvasHeight];
        var rows = direction == 0 ? Front : direction == 1 ? Back : Left;
        for (int row = 0; row < 20; row++)
        for (int x = 0; x < 16; x++)
            Put(pixels, x, 19 - row, rows[row][x]);
        int canonical = direction == 3 ? 2 : direction;
        if (canonical < 2)
            DrawArm(pixels, new Vector2Int(12, 11), new Vector2Int(12, 7), Vector2Int.down);
        var shoulder = canonical < 2 ? new Vector2Int(3, 11) : new Vector2Int(8, 10);
        DrawArm(pixels, shoulder, PalmPixel(canonical, pose), pose == 0 ? Vector2Int.down : Facing(canonical));
        if (direction == 3)
            for (int y = 0; y < CanvasHeight; y++) for (int x = 0; x < CanvasWidth / 2; x++)
            {
                int a = y * CanvasWidth + x, b = y * CanvasWidth + CanvasWidth - 1 - x;
                Color c = pixels[a]; pixels[a] = pixels[b]; pixels[b] = c;
            }
        var texture = new Texture2D(CanvasWidth, CanvasHeight, TextureFormat.RGBA32, false) {
            name = "Outer Realm Visitor " + direction + " Pose " + pose,
            filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave
        };
        texture.SetPixels(pixels); texture.Apply();
        var sprite = Sprite.Create(texture, new Rect(0, 0, CanvasWidth, CanvasHeight),
            new Vector2(12f / CanvasWidth, 8f / CanvasHeight), 16);
        sprite.name = texture.name; sprite.hideFlags = HideFlags.HideAndDontSave; return sprite;
    }

    private void DrawArm(Color[] pixels, Vector2Int shoulder, Vector2Int palm, Vector2Int forward)
    {
        // Match the other NPCs: a short rectangular sleeve, one cuff and a
        // solid palm. No joint outlines, individual fingers or knuckle pixels.
        Vector2Int wrist = palm - forward;
        Vector2Int side = new Vector2Int(-forward.y, forward.x);
        int left = Mathf.Min(shoulder.x, wrist.x) - Mathf.Abs(side.x);
        int right = Mathf.Max(shoulder.x, wrist.x) + Mathf.Abs(side.x);
        int bottom = Mathf.Min(shoulder.y, wrist.y) - Mathf.Abs(side.y);
        int top = Mathf.Max(shoulder.y, wrist.y) + Mathf.Abs(side.y);
        for (int y = bottom; y <= top; y++) for (int x = left; x <= right; x++)
            Put(pixels, x, y, y == top ? 'L' : 'H');
        for (int i = -1; i <= 1; i++) Put(pixels, wrist.x + side.x * i, wrist.y + side.y * i, 'T');
        // Three pixels across, two along the reach: slightly larger than a
        // civilian's hand but using the same flat, readable block silhouette.
        for (int depth = 0; depth < 2; depth++) for (int across = -1; across <= 1; across++)
        {
            Vector2Int p = palm + side * across + forward * depth;
            Put(pixels, p.x, p.y, 'P');
        }
    }

    private void Put(Color[] pixels, int x, int y, char color)
    {
        x += Padding; y += Padding;
        if (x >= 0 && x < CanvasWidth && y >= 0 && y < CanvasHeight)
            pixels[y * CanvasWidth + x] = PixelColor(color);
    }
    private Color PixelColor(char symbol)
    {
        switch (symbol) {
            case 'H': return robeColor;
            case 'L': return Color.Lerp(robeColor, Color.white, .18f);
            case 'D': return Color.Lerp(robeColor, Color.black, .42f);
            case 'T': return trimColor;
            case 'N': return Color.black;
            case 'P': return new Color(.45f, .38f, .52f, 1);
            case 'E': return eyeColor;
            default: return Color.clear;
        }
    }
    protected override void OnValidate()
    {
        base.OnValidate(); visualsDirty = true;
        attackPower = Mathf.Max(0, attackPower); attackReach = Mathf.Max(.5f, attackReach);
        attackWidth = Mathf.Max(.1f, attackWidth); attackDuration = Mathf.Max(.1f, attackDuration);
    }
    private void OnDestroy() => ReleaseSprites();
    private void ReleaseSprites()
    {
        for (int d = 0; d < 4; d++) for (int p = 0; p < 4; p++)
        {
            var sprite = frames[d, p]; if (sprite == null) continue;
            CrystalMesh.Release(sprite.texture); CrystalMesh.Release(sprite); frames[d, p] = null;
        }
    }
}
