using UnityEngine;

/// <summary>Civilian bodyguard: broad shoulders, cloth wraps and a leather vest; no military armor.</summary>
public sealed class BodyguardZeldaCharacterData : StrongmanZeldaCharacterData
{
    [Header("Civilian Bodyguard / 民用保镖外观")]
    [SerializeField] private Color headclothColor = new Color(.86f, .77f, .57f, 1f);
    [SerializeField] private Color hairColor = new Color(.20f, .12f, .08f, 1f);
    [SerializeField] private Color tunicColor = new Color(.65f, .14f, .24f, 1f);
    [SerializeField] private Color leatherColor = new Color(.43f, .25f, .12f, 1f);
    [SerializeField] private Color buckleColor = new Color(.73f, .77f, .76f, 1f);
    [SerializeField] private Color skinColor = new Color(.79f, .54f, .35f, 1f);
    [SerializeField] private Color eyeColor = new Color(.055f, .055f, .05f, 1f);

    public override ZeldaAttackVisualShape AttackVisualShape => ZeldaAttackVisualShape.Sword;

    // The 20x20 canvas, feet and pivot match the existing Strongman walk rig.
    // W=headband/wrist wraps; H=exposed hair; C=tunic; V/T/U=leather vest.
    // F/S=skin; R=beard; B/G/K=leather belt, gloves, boots; A=buckle.
    private static readonly string[] Front = {
        ".......HHHHHH.......",
        "......HHHHHHHH......",
        "......WWWWWWWW......",
        "......FEFFFFEF......",
        "......SFFFFFFS......",
        "......SRRFFRRS......",
        "...FFFCCCCCCCCFFF...",
        "...FFFCVVCCVVCFFF...",
        "...SSSCVTCCVTCSSS...",
        "...WWWCVVCCVVCWWW...",
        "...GGGCVVBBVVCGGG...",
        "....DDDVCAACVDDD....",
        ".....DBBBBBBBBD.....",
        "......DCCCCCCD......",
        "......KKK..KKK......",
        "......KKK..KKK......",
        "....................",
        "....................",
        "....................",
        "...................."
    };
    private static readonly string[] Back = {
        ".......HHHHHH.......",
        "......HHHHHHHH......",
        "......WWWWWWWW......",
        "......HHHHHHHH......",
        "......HHHHHHHH......",
        "......SHHHHHHS......",
        "...FFFCCCCCCCCFFF...",
        "...FFFCVTTTTVCFFF...",
        "...SSSCVVVVVVCSSS...",
        "...WWWCVVUVVVCWWW...",
        "...GGGCVVUVVVCGGG...",
        "....DDDVVUVVVDDD....",
        ".....DBBBBBBBBD.....",
        "......DCCCCCCD......",
        "......KKK..KKK......",
        "......KKK..KKK......",
        "....................",
        "....................",
        "....................",
        "...................."
    };
    private static readonly string[] Left = {
        ".....HHHHHH.........",
        "....HHHHHHHH........",
        "....WWWWWWWW........",
        "....FEFFFHHH........",
        "....SFFFFHHS........",
        "....SRRFRRHS........",
        "......CCCCCVVD......",
        "......CCFFFCVD......",
        "......CCSSSCVD......",
        "......CCWWWCVD......",
        "......CCGGGCVD......",
        "......DCVTVCVD......",
        "......DBBBBBBD......",
        "......DCCCCCCD......",
        "......KKK..KKK......",
        "......KKK..KKK......",
        "....................",
        "....................",
        "....................",
        "...................."
    };

    private readonly Sprite[,] bodyguardFrames = new Sprite[4, 2];
    private bool visualsDirty = true;
    public override Color GhostFormEyeColor => eyeColor;

    public override void ApplyCharacterVisual(SpriteRenderer renderer, Vector2 facing, bool isMoving, bool isAttacking)
    {
        if (renderer == null) return;
        if (visualsDirty)
        {
            PixelCharacterWalkAnimator animator = GetComponent<PixelCharacterWalkAnimator>();
            if (animator != null) animator.InvalidateFrames();
            ReleaseBodyguardSprites();
            visualsDirty = false;
        }
        int direction = Mathf.Abs(facing.x) > Mathf.Abs(facing.y) ? (facing.x < 0f ? 2 : 3) : (facing.y > 0f ? 1 : 0);
        int pose = isAttacking ? 1 : 0;
        if (bodyguardFrames[direction, pose] == null)
            bodyguardFrames[direction, pose] = CreateBodyguardSprite(direction, isAttacking);
        renderer.sprite = bodyguardFrames[direction, pose];
        renderer.color = GetDamageFeedbackTint(GetMovementTint(isMoving));
    }

    private Sprite CreateBodyguardSprite(int direction, bool attacking)
    {
        Texture2D texture = CreateBodyguardTexture(direction, attacking);
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 20, 20), new Vector2(0.5f, 0.3f), 16f);
        sprite.name = texture.name;
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    private Texture2D CreateBodyguardTexture(int direction, bool attacking)
    {
        string[] map = direction == 1 ? Back : direction >= 2 ? Left : Front;
        var pixels = new Color[400];
        for (int row = 0; row < 20; row++)
        for (int x = 0; x < 20; x++)
            pixels[(19 - row) * 20 + x] = BodyguardPixelColor(map[row][direction == 3 ? 19 - x : x]);
        if (attacking)
        {
            if (direction < 2)
            {
                int outerX = direction == 0 ? 3 : 14;
                PaintBodyguard(pixels, outerX, 9, 3, 5, '.');
                PaintBodyguard(pixels, direction == 0 ? 5 : 14, 9, 1, 4, 'D');
            }
            else
            {
                // Replace the centered near arm with clothing before extending it.
                PaintBodyguard(pixels, direction == 2 ? 8 : 9, 9, 3, 4, 'C');
            }
            int startX = direction == 2 ? 2 : direction == 3 ? 12 : 7;
            int startY = direction == 1 ? 12 : direction == 0 ? 5 : 9;
            PaintBodyguard(pixels, startX, startY, 6, 3, 'F');
            int handX = direction == 2 ? startX : direction == 3 ? startX + 3 : startX + 1;
            int handY = direction == 1 ? startY + 1 : startY;
            PaintBodyguard(pixels, handX, handY, 3, 2, 'G');
        }
        var texture = new Texture2D(20, 20, TextureFormat.RGBA32, false)
        {
            name = "Civilian Bodyguard " + direction + (attacking ? " Attack" : " Idle"),
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.SetPixels(pixels);
        // Keep readable for the shared walking animator and ghost-form recoloring.
        texture.Apply(false, false);
        return texture;
    }

    private void PaintBodyguard(Color[] pixels, int x, int y, int width, int height, char symbol)
    {
        for (int py = y; py < y + height; py++)
        for (int px = x; px < x + width; px++)
            pixels[py * 20 + px] = BodyguardPixelColor(symbol);
    }

    private Color BodyguardPixelColor(char symbol)
    {
        switch (symbol)
        {
            case 'W': return headclothColor;
            case 'H': return hairColor;
            case 'C': return tunicColor;
            case 'D': return Color.Lerp(tunicColor, Color.black, .25f);
            case 'L': return Color.Lerp(tunicColor, Color.white, .20f);
            case 'V': return leatherColor;
            case 'T': return Color.Lerp(leatherColor, new Color(.83f, .62f, .37f), .38f);
            case 'U': case 'B': return Color.Lerp(leatherColor, Color.black, .25f);
            case 'A': return buckleColor;
            case 'F': return skinColor;
            case 'S': return Color.Lerp(skinColor, new Color(.38f, .20f, .11f), .35f);
            case 'G': return leatherColor;
            case 'R': return Color.Lerp(hairColor, Color.black, .15f);
            case 'K': return Color.Lerp(leatherColor, Color.black, .45f);
            case 'E': return eyeColor;
            default: return Color.clear;
        }
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        visualsDirty = true;
    }

    private void OnDrawGizmos()
    {
        if (Application.isPlaying) return;
        Matrix4x4 oldMatrix = Gizmos.matrix;
        Color oldColor = Gizmos.color;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.35f, 0.9f, 1f, 0.8f);
        Gizmos.DrawWireCube(new Vector3(0f, 0.25f, 0f), new Vector3(1.25f, 1.25f, 0f));
        Gizmos.matrix = oldMatrix;
        Gizmos.color = oldColor;
    }

    private void OnDestroy() => ReleaseBodyguardSprites();
    private void ReleaseBodyguardSprites()
    {
        for (int d = 0; d < 4; d++)
        for (int p = 0; p < 2; p++)
        {
            Sprite sprite = bodyguardFrames[d, p];
            if (sprite == null) continue;
            Texture2D texture = sprite.texture;
            if (Application.isPlaying) { Destroy(sprite); Destroy(texture); }
            else { DestroyImmediate(sprite); DestroyImmediate(texture); }
            bodyguardFrames[d, p] = null;
        }
    }
}
