using UnityEngine;

public partial class GhostZeldaCharacterData
{
    private const int GhostFrameSize = 16;
    private const int GhostPoseCount = 4;
    private static Sprite[] ghostVisualFrames;
    private static readonly int[] GhostRowInsets = { 6, 4, 3, 2, 2, 2, 2, 2, 3, 4, 6 };
    private static readonly Color GhostBodyColor = new Color(0.82f, 0.98f, 1f, 0.88f);
    private static readonly Color GhostShadeColor = new Color(0.46f, 0.78f, 0.96f, 0.7f);
    private static readonly Color GhostEyeColor = new Color(0.07f, 0.16f, 0.24f, 0.95f);

    private static Sprite SelectGhostSprite(Vector2 facing, bool moving, float elapsed)
    {
        EnsureGhostVisualFrames();
        Vector2 cardinal = ToNearestCardinalDirection(facing);
        int direction = cardinal == Vector2.up ? 1 : cardinal == Vector2.left ? 2 : cardinal == Vector2.right ? 3 : 0;
        int pose = Mathf.FloorToInt(elapsed * (moving ? 6f : 3f)) % GhostPoseCount;
        float blinkTime = Mathf.Repeat(elapsed, 4.6f);
        bool blinking = blinkTime >= 4.1f && blinkTime < 4.26f;
        return ghostVisualFrames[GhostFrameIndex(direction, moving, pose, blinking)];
    }

    private static int GhostFrameIndex(int direction, bool moving, int pose, bool blinking)
    {
        return (((direction * 2 + (moving ? 1 : 0)) * GhostPoseCount + pose) * 2) + (blinking ? 1 : 0);
    }

    private static void EnsureGhostVisualFrames()
    {
        if (ghostVisualFrames != null && ghostVisualFrames[0] != null) return;

        // One shared, point-filtered atlas: no textures, arrays or sprite creation in
        // the animation loop. Frame bounds/pivot stay identical to the old art.
        const int columns = 8;
        var atlas = new Texture2D(GhostFrameSize * columns, GhostFrameSize * columns, TextureFormat.RGBA32, false)
        {
            name = "Ghost Directional Animation",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        ghostVisualFrames = new Sprite[64];
        for (int direction = 0; direction < 4; direction++)
        for (int movement = 0; movement < 2; movement++)
        for (int pose = 0; pose < GhostPoseCount; pose++)
        for (int blink = 0; blink < 2; blink++)
        {
            int index = GhostFrameIndex(direction, movement != 0, pose, blink != 0);
            int x = (index % columns) * GhostFrameSize;
            int y = (index / columns) * GhostFrameSize;
            atlas.SetPixels(x, y, GhostFrameSize, GhostFrameSize,
                DrawGhostFrame(direction, movement != 0, pose, blink != 0));
            Sprite sprite = Sprite.Create(atlas, new Rect(x, y, GhostFrameSize, GhostFrameSize),
                new Vector2(0.5f, 0.25f), 16f, 0, SpriteMeshType.FullRect);
            sprite.name = "Ghost " + direction + (movement != 0 ? " Glide " : " Float ") + pose + (blink != 0 ? " Blink" : "");
            sprite.hideFlags = HideFlags.HideAndDontSave;
            ghostVisualFrames[index] = sprite;
        }
        atlas.Apply(false, false);
    }

    private static Color[] DrawGhostFrame(int direction, bool moving, int pose, bool blinking)
    {
        var pixels = new Color[GhostFrameSize * GhostFrameSize];
        int bob = pose == 1 || pose == 2 ? 1 : 0;
        int ripple = pose == 1 ? 1 : pose == 3 ? -1 : 0;

        for (int y = 3; y <= 13; y++)
        {
            int inset = GhostRowInsets[y - 3];
            if (moving && direction == 0)
            {
                // Facing down: the lower body leads while the crown draws in.
                if (y == 4 || y == 5) inset--;
                if (y == 11 || y == 12) inset++;
            }
            else if (moving && direction == 1 && bob != 0 && y >= 9 && y <= 11)
            {
                // Facing up: gather the shoulders into an upward stretch.
                inset++;
            }
            int shift = GhostRowShift(direction, moving, y, ripple);
            for (int x = inset; x < GhostFrameSize - inset; x++)
            {
                Color color = (y == 3 && x < inset + 3) ||
                    (y == 4 && x < inset + 2) || (y >= 5 && y <= 11 && x == inset)
                    ? GhostShadeColor : GhostBodyColor;
                SetGhostPixel(pixels, x + shift, y + bob, color);
            }
        }

        if (moving && direction == 1)
        {
            // A short trailing wisp, not legs; a single-pixel crown lift.
            for (int x = 6; x <= 8; x++) SetGhostPixel(pixels, x + ripple, 2 + bob, GhostShadeColor);
            if (bob == 0)
                for (int x = 7; x <= 8; x++) SetGhostPixel(pixels, x, 14, GhostBodyColor);
        }
        else if (moving && direction == 0 && bob != 0)
        {
            for (int x = 7; x <= 9; x++) SetGhostPixel(pixels, x + ripple, 2 + bob, GhostShadeColor);
        }

        // Two-pixel eyes remain simple and dark, with a smaller far eye in profile.
        int eyeY = direction == 1 ? 10 : moving && direction == 0 ? 8 : 9;
        int height = blinking ? 1 : 2;
        if (direction == 2)
        {
            DrawGhostEye(pixels, 4, eyeY, 2, height, direction, moving, ripple, bob);
            DrawGhostEye(pixels, 8, eyeY, 1, height, direction, moving, ripple, bob);
        }
        else if (direction == 3)
        {
            DrawGhostEye(pixels, 10, eyeY, 2, height, direction, moving, ripple, bob);
            DrawGhostEye(pixels, 7, eyeY, 1, height, direction, moving, ripple, bob);
        }
        else
        {
            DrawGhostEye(pixels, 5, eyeY, 2, height, direction, moving, ripple, bob);
            DrawGhostEye(pixels, 9, eyeY, 2, height, direction, moving, ripple, bob);
        }
        return pixels;
    }

    private static int GhostRowShift(int direction, bool moving, int y, int ripple)
    {
        int side = direction == 2 ? -1 : direction == 3 ? 1 : 0;
        if (moving && side != 0)
        {
            // Lean into horizontal travel, leaving the lower edge behind.
            if (y >= 9) return side;
            if (y <= 5) return -side + side * ripple;
        }
        if (y <= 5) return ripple * (direction == 1 || direction == 2 ? -1 : 1);
        return 0;
    }

    private static void DrawGhostEye(Color[] pixels, int x, int y, int width, int height,
        int direction, bool moving, int ripple, int bob)
    {
        for (int row = y; row < y + height; row++)
        for (int column = x; column < x + width; column++)
            SetGhostPixel(pixels, column + GhostRowShift(direction, moving, row, ripple), row + bob, GhostEyeColor);
    }

    private static void SetGhostPixel(Color[] pixels, int x, int y, Color color)
    {
        if (x >= 0 && x < GhostFrameSize && y >= 0 && y < GhostFrameSize)
            pixels[y * GhostFrameSize + x] = color;
    }
}
