using UnityEngine;

/// <summary>Sky-blue travelling hero; reuses the common sword combat and cached 16 PPU animation rig.</summary>
public partial class HeroZeldaCharacterData : PaladinZeldaCharacterData
{
    // Separate character types keep equipment variants stable when a renamed
    // possessed body is recreated from the save catalog in another scene.
    public virtual bool HasSword => true;
    public virtual bool HasShield => true;
    public override ZeldaAttackVisualShape AttackVisualShape => HasSword
        ? base.AttackVisualShape : ZeldaAttackVisualShape.Rock;
    // Render the silver/gold/blue weapon as solid pixels, like the hero body.
    // The inherited translucent combat tint was making it appear much darker.
    public override Color AttackVisualTint => HasSword
        ? Color.white : base.AttackVisualTint;

    [Header("Hero / 勇者外观")]
    [Tooltip("短袍与软帽使用 Armor Color / Shadow / Highlight；盾面使用 Cape Color。")]
    [SerializeField] private Color hairColor = new Color(0.88f, 0.66f, 0.25f, 1f);
    [SerializeField] private Color faceColor = new Color(0.96f, 0.76f, 0.54f, 1f);
    [SerializeField] private Color collarColor = new Color(0.88f, 0.93f, 0.89f, 1f);
    [SerializeField] private Color leatherColor = new Color(0.43f, 0.26f, 0.14f, 1f);
    [SerializeField] private Color bootsColor = new Color(0.25f, 0.17f, 0.12f, 1f);
    [SerializeField] private Color metalColor = new Color(0.69f, 0.8f, 0.85f, 1f);
    [SerializeField] private Color goldColor = new Color(0.94f, 0.75f, 0.32f, 1f);

    // A/S/H = sky-blue cloth, Y/L = golden hair, F/N = skin, E = eyes.
    // Keep the same two-pixel boots, pivot and body size as the other characters.
    private static readonly string[] Front = {
        "........HH......",
        "......HHAAAA....",
        ".....HAAAAAS....",
        ".....YYYYYY.....",
        "...FYYFFFFYYF...",
        "....YFEFFEFY....",
        ".....NFFFFN.....",
        "....HAOOOAAH....",
        "....FFAABAFF....",
        "....FFBBTBFF....",
        "....SAAOAAAS....",
        ".....HAAAAH.....",
        ".....KK..KK.....",
        ".....KK..KK.....",
        "................",
        "................"
    };

    private static readonly string[] Back = {
        "......HH........",
        "....AAAAHH......",
        "....SAAAAAH.....",
        ".....AAAYYY.....",
        ".....SAAYYY.....",
        "......SAYYY.....",
        ".....NYYYYN.....",
        "....HAABAAAH....",
        "....FFAABAFF....",
        "....FFBBBBFF....",
        "....SAAOAAAS....",
        ".....HAAAAH.....",
        ".....KK..KK.....",
        ".....KK..KK.....",
        "................",
        "................"
    };

    private static readonly string[] Left = {
        ".......HH.......",
        ".....HHAAAA.....",
        "....HAAAAAAS....",
        "....LYYYYYAS....",
        "...FFFYYYYY.....",
        "....FEFYYFY.....",
        "....NFFYYN......",
        ".....OOAAAA.....",
        ".....AAFFAS.....",
        ".....BBFFBB.....",
        "....SAAOAAAS....",
        ".....HAAAAH.....",
        ".....KK..KK.....",
        ".....KK..KK.....",
        "................",
        "................"
    };

    private static readonly string[] Right = {
        ".......HH.......",
        ".....AAAAHH.....",
        "....SAAAAAAH....",
        "....SAYYYYYL....",
        ".....YYYYYFFF...",
        ".....YFYYFEF....",
        "......NYYFFN....",
        ".....AAAAOO.....",
        ".....SAFFAA.....",
        ".....BBFFBB.....",
        "....SAAOAAAS....",
        ".....HAAAAH.....",
        ".....KK..KK.....",
        ".....KK..KK.....",
        "................",
        "................"
    };

    private static readonly string[] ShieldFront = { "QQQQQ", "QCCCQ", "QCTCQ", "QCDCQ", ".QDQ.", "..Q.." };
    private static readonly string[] ShieldBack = { "QQQQQ", "QBBBQ", "QBTBQ", "QBBBQ", ".QBQ.", "..Q.." };
    private static readonly string[] ShieldNearEdge = { "QQQ", "QCQ", "QCQ", "QDQ", "QDQ", ".Q." };
    private static readonly string[] ShieldFarEdge = { "QQQ", "QBQ", "QTQ", "QBQ", "QBQ", ".Q." };

    protected override Sprite CreateSprite(int direction, int pose)
    {
        string[] rows = direction == 1 ? Back : direction == 2 ? Left : direction == 3 ? Right : Front;
        var pixels = new Color[16 * 16];
        for (int row = 0; row < 16; row++)
        for (int x = 0; x < 16; x++)
        {
            if (rows[row][x] == '.') continue;
            int y = 15 - row;
            if (row >= 12 && (pose == 1 || pose == 2) && (x < 8) == (pose == 1)) y++;
            pixels[y * 16 + x] = PixelColor(rows[row][x]);
        }

        if (HasSword) DrawScabbard(pixels, direction, pose != 3);
        if (HasShield) pixels = CreateRaisedShieldPose(pixels, direction, pose == 3);
        else if (pose == 3) DrawReachingArm(pixels, direction);

        var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false) {
            name = "Hero " + direction + " " + pose,
            filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.SetPixels(pixels);
        texture.Apply(); // Keep readable for ghost-form recoloring and item poses.
        var sprite = Sprite.Create(texture, new Rect(0, 0, 16, 16), new Vector2(.5f, .25f), 16f);
        sprite.name = texture.name;
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    private Color[] CreateRaisedShieldPose(Color[] body, int direction, bool attacking)
    {
        // Always raise the shield arm. Keep the other hand at rest unless
        // attacking, so idle/walking frames do not become frozen attack poses.
        if (direction < 2)
        {
            for (int y = 6; y <= 7; y++)
            {
                if (attacking || direction == 0)
                {
                    body[y * 16 + 4] = Color.clear;
                    body[y * 16 + 5] = PixelColor(y == 6 ? 'B' : 'A');
                }
                if (attacking || direction == 1)
                {
                    body[y * 16 + 11] = Color.clear;
                    body[y * 16 + 10] = PixelColor(y == 6 ? 'B' : 'S');
                }
            }
        }
        else if (attacking || direction == 2)
        {
            // The near arm carries the shield when facing left, but the sword
            // when facing right. Preserve the latter's resting hand.
            for (int y = 6; y <= 7; y++)
            for (int x = 7; x <= 8; x++) body[y * 16 + x] = PixelColor(y == 6 ? 'B' : 'A');
        }

        var result = new Color[16 * 16];
        // Up/right: the left (shield) arm is beyond the body. Left-facing:
        // the sword arm is the far arm. The head must not be painted over.
        bool farShield = direction == 1 || direction == 3;
        bool farAttackArm = direction == 1 || direction == 2;
        if (farShield) DrawRaisedShield(result, direction);
        if (attacking && farAttackArm) PaintAttackingArm(result, direction);
        for (int i = 0; i < body.Length; i++) if (body[i].a > 0f) result[i] = body[i];
        if (attacking && !farAttackArm) PaintAttackingArm(result, direction);
        if (!farShield) DrawRaisedShield(result, direction);
        // A near shield can tuck one pixel into the cheek during its recoil.
        // Keep the face/hair in front rather than flashing a body pixel white.
        if (buildingShieldFeedback)
            for (int y = 10; y < 16; y++)
            for (int x = 0; x < 16; x++)
                if (body[y * 16 + x].a > 0f) result[y * 16 + x] = body[y * 16 + x];
        return result;
    }

    private void DrawRaisedShield(Color[] pixels, int direction)
    {
        switch (direction)
        {
            case 0:
                // Face down: the raised shield covers the left forearm/chest,
                // while the attacking fist remains visible below its right edge.
                PaintBlock(pixels, 4, 7, 3, 2, 'H');
                PaintBlock(pixels, 6, 7, 2, 2, 'F');
                PaintShieldRows(pixels, ShieldFront, 3, 9, direction);
                break;
            case 1:
                // Face up: show the wood/strap on the back of the shield;
                // hair, cap and torso will subsequently cover its near portion.
                PaintBlock(pixels, 10, 8, 2, 3, 'H');
                PaintBlock(pixels, 11, 10, 2, 2, 'F');
                PaintShieldRows(pixels, ShieldBack, 9, 12, direction);
                break;
            case 2:
                PaintBlock(pixels, 5, 7, 2, 2, 'H');
                PaintBlock(pixels, 3, 7, 2, 2, 'F');
                PaintShieldRows(pixels, ShieldNearEdge, 1, 10, direction);
                break;
            default:
                PaintBlock(pixels, 9, 8, 3, 2, 'A');
                PaintBlock(pixels, 12, 8, 2, 2, 'F');
                // Keep a continuous outer rim beyond the near sword arm,
                // below the face rather than a tiny patch beside the eyes.
                PaintShieldRows(pixels, ShieldFarEdge, 13, 9, direction);
                break;
        }
    }

    private void PaintBlock(Color[] pixels, int left, int bottom, int width, int height, char color)
    {
        Color ink = PixelColor(color);
        for (int y = bottom; y < bottom + height; y++)
        for (int x = left; x < left + width; x++) pixels[y * 16 + x] = ink;
    }

    private void PaintShieldRows(Color[] pixels, string[] rows, int left, int top, int direction)
    {
        // One-pixel recoil affects only the shield, never the body or its collider.
        if (buildingShieldFeedback && shieldFeedbackPhase == 0)
        {
            // Front/back shields tuck toward the chest without rising into the eyes.
            if (direction == 0) left++;
            else if (direction == 1) left--;
            else if (direction == 2) left++;
            else left--;
        }
        for (int row = 0; row < rows.Length; row++)
        for (int x = 0; x < rows[row].Length; x++)
        {
            if (rows[row][x] == '.') continue;
            Color ink = PixelColor(rows[row][x]);
            if (buildingShieldFeedback) ink = Color.Lerp(ink, Color.white, shieldFeedbackPhase == 0 ? 1f : .3f);
            pixels[(top - row) * 16 + left + x] = ink;
        }
    }

    private void DrawScabbard(Color[] pixels, int direction, bool sheathed)
    {
        int x = direction == 0 ? 12 : direction == 1 ? 3 : direction == 2 ? 11 : 3;
        for (int y = 3; y <= 7; y++) pixels[y * 16 + x] = base.PixelColor('D');
        pixels[2 * 16 + x] = metalColor;
        if (!sheathed) return;
        for (int y = 9; y <= 10; y++) pixels[y * 16 + x] = leatherColor;
        for (int side = -1; side <= 1; side++) pixels[8 * 16 + x + side] = metalColor;
    }

    private void DrawReachingArm(Color[] pixels, int direction)
    {
        // Replace the attacking arm, preserving the other arm (and its shield).
        // Swordless variants reach to the civilian's shorter rock-attack grip.
        if (direction < 2)
        {
            int outer = direction == 0 ? 11 : 4;
            int inner = direction == 0 ? 10 : 5;
            for (int y = 6; y <= 8; y++)
            {
                pixels[y * 16 + outer] = Color.clear;
                pixels[y * 16 + inner] = PixelColor(y == 6 ? 'B' : 'S');
            }
        }
        else
        {
            for (int y = 6; y <= 7; y++)
            for (int x = 7; x <= 8; x++) pixels[y * 16 + x] = PixelColor(y == 6 ? 'B' : 'A');
        }
        PaintAttackingArm(pixels, direction);
    }

    private void PaintAttackingArm(Color[] pixels, int direction)
    {
        int startX = direction == 2 ? (HasSword ? 1 : 2) : direction == 3 ? (HasSword ? 11 : 10) : 6;
        int startY = direction == 1 ? 10 : direction == 0 ? 4 : 6;
        for (int y = startY; y < startY + 3; y++)
        for (int x = startX; x < startX + 4; x++) pixels[y * 16 + x] = PixelColor(y == startY + 2 ? 'H' : 'A');
        int handX = direction == 2 ? startX : direction == 3 ? startX + 2 : startX + 1;
        int handY = direction == 1 ? startY + 1 : startY;
        for (int y = handY; y < handY + 2; y++)
        for (int x = handX; x < handX + 2; x++) pixels[y * 16 + x] = faceColor;
    }

    protected override Color PixelColor(char symbol)
    {
        switch (symbol)
        {
            case 'Y': return hairColor;
            case 'L': return Color.Lerp(hairColor, Color.white, .25f);
            case 'F': return faceColor;
            case 'N': return Color.Lerp(faceColor, Color.black, .18f);
            case 'E': return GhostFormEyeColor;
            case 'O': return collarColor;
            case 'B': return leatherColor;
            case 'K': return bootsColor;
            case 'Q': return metalColor;
            case 'T': return goldColor;
            default: return base.PixelColor(symbol);
        }
    }
}
