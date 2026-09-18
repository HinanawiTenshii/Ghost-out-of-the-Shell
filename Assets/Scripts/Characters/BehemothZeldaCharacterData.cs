using UnityEngine;

/// <summary>A fixed, one-second pulse field; it never refreshes an existing stun.</summary>
public sealed class FearRoarArea : MonoBehaviour
{
    private float radius = 3f;
    private float stunDuration = 3f;
    private const float Duration = 1f;
    private readonly LineRenderer[] rings = new LineRenderer[5];
    private readonly System.Collections.Generic.HashSet<ZeldaCharacterAiBase> affected = new System.Collections.Generic.HashSet<ZeldaCharacterAiBase>();
    private Material ringMaterial;
    private float elapsed;
    private static Sprite icon;
    private static readonly Sprite[] rankedIcons = new Sprite[3];
    public static Sprite GetRankedIcon(int rank)
    {
        rank = Mathf.Clamp(rank, 1, 3);
        if (rankedIcons[rank - 1] == null) rankedIcons[rank - 1] = CreateIcon(rank);
        return rankedIcons[rank - 1];
    }
    public static Sprite Icon
    {
        get
        {
            if (icon == null) icon = CreateIcon(0);
            return icon;
        }
    }
    private static Sprite CreateIcon(int rank)
    {
            // A rounded skull with hollow eyes, a nose and separated teeth.
            // Compose the waves separately so every texture row has the same width.
            string[] skull = {
                "..#####..", ".#######.", "#########",
                "##..#..##", "##..#..##", ".###.###.",
                "..#####..", "..#.#.#..", "..#.#.#.."
            };
            int size = rank > 0 ? 22 : 17;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "Fear Roar Icon";
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                bool head = x >= 4 && x <= 12 && y >= 4 && y <= 12 && skull[y - 4][x - 4] == '#';
                int edge = Mathf.Min(x, 16 - x);
                bool wave = (edge == 0 && y >= 5 && y <= 11) ||
                    (edge == 1 && (y == 4 || y == 12)) ||
                    (edge == 2 && y >= 6 && y <= 10) ||
                    (edge == 3 && (y == 5 || y == 11));
                bool numeral = rank > 0 && y >= 15 && y <= 21 && x >= 14 && x <= 21 &&
                    (y == 15 || y == 21 || (rank == 1 ? x == 18 : rank == 2 ? x == 16 || x == 19 : x == 15 || x == 18 || x == 20));
                texture.SetPixel(x, size - 1 - y, head || wave || numeral ? Color.white : Color.clear);
            }
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * 0.5f, size);
    }

    public void Initialize(ZeldaCharacterData user, int level)
    {
        radius = level >= 2 ? 3.5f : 3f;
        stunDuration = level >= 3 ? 5f : 3f;
        transform.position = user.transform.position;
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(gameObject, ZeldaRuntimeRegistry.GetGameplayScene(user.gameObject));
        var visual = user.GetComponent<SpriteRenderer>();
        ringMaterial = new Material(Shader.Find("Sprites/Default"));
        for (int i = 0; i < rings.Length; i++)
        {
            var ring = new GameObject("Roar Wave").AddComponent<LineRenderer>();
            ring.transform.SetParent(transform, false);
            ring.sharedMaterial = ringMaterial;
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = 96;
            ring.widthMultiplier = 0.055f;
            ring.sortingLayerID = visual != null ? visual.sortingLayerID : 0;
            ring.sortingOrder = visual != null ? visual.sortingOrder + 2 : 2;
            for (int j = 0; j < 96; j++)
            {
                float a = j * Mathf.PI * 2f / 96;
                ring.SetPosition(j, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0));
            }
            ring.enabled = false;
            rings[i] = ring;
        }
        AffectCharacters();
    }

    private void AffectCharacters()
    {
        foreach (var ai in ZeldaRuntimeRegistry.AiCharacters)
        {
            if (ai == null || !ai.isActiveAndEnabled || ZeldaRuntimeRegistry.GetGameplayScene(ai.gameObject) != gameObject.scene ||
                affected.Contains(ai) || ((Vector2)(ai.transform.position - transform.position)).sqrMagnitude > radius * radius) continue;
            var mover = ai.GetComponent<ZeldaFourWayMover>();
            var data = ai.GetComponent<ZeldaCharacterData>();
            if (data == null || data.IsDead || (mover != null && mover.isActiveAndEnabled)) continue;
            affected.Add(ai);
            if (!ai.IsStunned) ai.StunForDuration(stunDuration);
        }
    }

    private void Update()
    {
        if (elapsed >= Duration) { Destroy(gameObject); return; }
        AffectCharacters();
        elapsed = Mathf.Min(Duration, elapsed + Time.deltaTime);
        for (int i = 0; i < rings.Length; i++)
        {
            float age = elapsed - i * 0.16f;
            var ring = rings[i];
            ring.enabled = age >= 0 && age < 0.36f;
            if (!ring.enabled) continue;
            float t = age / 0.36f;
            ring.transform.localScale = Vector3.one * Mathf.Lerp(0.05f, radius, t);
            Color tint = new Color(1f, 0.3f, 0.23f, 0.72f * (1f - t));
            ring.startColor = ring.endColor = tint;
        }
    }

    private void OnDestroy()
    {
        if (ringMaterial != null) Destroy(ringMaterial);
    }
}

/// <summary>
/// Oversized SwordGuy-derived mechanical brute with a radial shockwave attack.
/// </summary>
public sealed class BehemothZeldaCharacterData : ZeldaCharacterData
{
    [Header("Behemoth Combat")]
    [SerializeField, Min(0)] private int behemothAttackPower = 4;
    [SerializeField] private GameObject behemothAttackPrefab;
    [SerializeField, Min(0.05f)] private float behemothAttackDuration = 0.58f;
    [SerializeField] private Vector2 shockwaveSize = new Vector2(5.1f, 5.1f);
    [SerializeField] private Color shockwaveColor =
        new Color(0.95f, 0.12f, 0.08f, 0.78f);

    [Header("Behemoth Appearance")]
    [SerializeField] private Color armorColor =
        new Color(0.46f, 0.5f, 0.5f, 1f);
    [SerializeField] private Color armorDarkColor =
        new Color(0.18f, 0.21f, 0.22f, 1f);
    [SerializeField] private Color energyColor =
        new Color(0.82f, 0.035f, 0.025f, 1f);
    [SerializeField] private Color eyeColor =
        new Color(1f, 0.75f, 0.12f, 1f);

    private bool visualsDirty = true;

    private readonly Sprite[] idleSprites = new Sprite[4];
    private readonly Sprite[] attackSprites = new Sprite[4];
    private readonly Texture2D[] idleTextures = new Texture2D[4];
    private readonly Texture2D[] attackTextures = new Texture2D[4];

    public override int AttackPower => behemothAttackPower;
    public override bool CanAttack => !IsGhostForm;
    [Header("Fear Roar Camera Shake")]
    [SerializeField, Min(0f)] private float roarShakeStrength = 0.12f;
    [SerializeField, Min(1f)] private float roarShakeFrequency = 22f;
    public override string CharacterSkillName => "恐惧咆哮";
    public override string NativeCharacterSkillId => "fear_roar";
    public override Sprite CharacterSkillIcon => FearRoarArea.Icon;
    public override bool TryUseCharacterSkill()
    {
        return TryUseFearRoar(roarShakeStrength, roarShakeFrequency);
    }
    public override Color GhostFormEyeColor => eyeColor;
    public override GameObject AttackPrefab => behemothAttackPrefab;
    public override float AttackDuration => behemothAttackDuration;
    public override Vector2 AttackSpawnOffset => Vector2.zero;
    public override Vector2 AttackSize => shockwaveSize;
    public override ZeldaAttackVisualShape AttackVisualShape =>
        ZeldaAttackVisualShape.Shockwave;
    public override Color AttackVisualTint => shockwaveColor;

    public override void ApplyCharacterVisual(
        SpriteRenderer spriteRenderer,
        Vector2 facingDirection,
        bool isMoving,
        bool isAttacking)
    {
        if (spriteRenderer == null)
        {
            return;
        }

        EnsureSprites();
        int index = DirectionIndex(facingDirection);
        spriteRenderer.sprite = isAttacking
            ? attackSprites[index]
            : idleSprites[index];
        Color baseTint = isMoving
            ? Color.white
            : new Color(0.92f, 0.96f, 1f, 1f);
        spriteRenderer.color = GetDamageFeedbackTint(baseTint);
    }

    private void EnsureSprites()
    {
        if (visualsDirty)
        {
            var animator = GetComponent<PixelCharacterWalkAnimator>();
            if (animator != null) animator.InvalidateFrames();
            ReleaseSprites();
            visualsDirty = false;
        }
        if (idleSprites[0] != null)
        {
            return;
        }

        Vector2[] directions =
        {
            Vector2.down,
            Vector2.up,
            Vector2.left,
            Vector2.right
        };
        for (int i = 0; i < directions.Length; i++)
        {
            idleTextures[i] = CreateTexture(directions[i], false);
            idleSprites[i] = CreateSprite(
                idleTextures[i],
                "Behemoth " + i);
            attackTextures[i] = CreateTexture(directions[i], true);
            attackSprites[i] = CreateSprite(
                attackTextures[i],
                "Behemoth Attack " + i);
        }
    }

    private static Sprite CreateSprite(Texture2D texture, string spriteName)
    {
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, 32f, 28f),
            new Vector2(0.5f, 0.18f),
            16f);
        sprite.name = spriteName;
        return sprite;
    }

    // 32x28 at the existing 16 PPU. Low-set beast mask sits beneath a steel carapace with rectangular red inserts.
    // U/V/F/G/P belong exclusively to the near arms and are replaced for the slam.
    private static readonly string[] FrontBody = {
        "................................",
        "................................",
        "..........AAAAAAAAAAAA..........",
        "...ALLLLA.DDCCCCCCCCDD.ALLLLA...",
        "...ALLLLAAAACCCCCCCCLLLALLLLA...",
        "...AAAAAAAAACCCCCCCCAAAAAAAAA...",
        "...ASSSSASSSSSSSSSSSSSSASSSSA...",
        "...ASRRSASOOOOOOOOOOOOSASRRSA...",
        "...ASRRSAOLLLLLLLLLLLLOASRRSA...",
        "...ASSSSAOAAAAARRAAAAAOASSSSA...",
        "..ALLLLLLODDDDARRADDDDOLLLLLLA..",
        "..AAAAAAAODYYYARRAYYYDOAAAAAAA..",
        "..AAAAAAAOLDDDADDADDDLOAAAAAAA..",
        "..UUPPUUDOLSAAAAAAAASLODUUPPUU..",
        "..UUPPUUDOLSLLDLLDLLSLODUUPPUU..",
        "..UUPPUUDSOALLAAAALLAOSDUUPPUU..",
        "..UUPPUUDSSOOOOOOOOOOSSDUUPPUU..",
        ".FVVVVVVFSSAAASSSSAAASSFVVVVVVF.",
        ".FGGFFGGFSSSSSSSSSSSSSSFGGFFGGF.",
        ".FFFFFFFFAAAAAAAAAAAAAAFFFFFFFF.",
        ".FFFFFFFFDSSSSSSSSSSSSDFFFFFFFF.",
        ".GGGGGGGGSSSSSSSSSSSSSSGGGGGGGG.",
        "........DSDSSD....DSDSSD........",
        "........DSDSSD....DSDSSD........",
        "........DAAAAD....DAAAAD........",
        "........LLDDLL....LLDDLL........",
        "................................",
        "................................",
    };

    private static readonly string[] BackBody = {
        "................................",
        "................................",
        "..........AAAAAAAAAAAA..........",
        "...ALLLLA.DDDDDDDDDDDD.ALLLLA...",
        "...ALLLLAAAAAAAAAAAALLLALLLLA...",
        "...AAAAAAAAACCCCCCCCAAAAAAAAA...",
        "...ASSSSASAACCCCCCCCAASASSSSA...",
        "...ASRRSASADCCCCCCCCDASASRRSA...",
        "...ASRRSASAAAAAAAAAAAASASRRSA...",
        "...ASSSSASAAAADDDDAAAASASSSSA...",
        "..ALLLLLLAAAAAAAAAAAAAALLLLLLA..",
        "..AAAAAAAAAAAADRRDAAAAAAAAAAAA..",
        "..AAAAAAAAAAAADDDDAAAAAAAAAAAA..",
        "..UUPPUUDSAAAAAAAAAAAASDUUPPUU..",
        "..UUPPUUDSAAAADRRDAAAASDUUPPUU..",
        "..UUPPUUDSAAAADDDDAAAASDUUPPUU..",
        "..UUPPUUDSAAAAAAAAAAAASDUUPPUU..",
        ".FVVVVVVFSAAAADRRDAAAASFVVVVVVF.",
        ".FGGFFGGFSSSSSSSSSSSSSSFGGFFGGF.",
        ".FFFFFFFFAAAAAAAAAAAAAAFFFFFFFF.",
        ".FFFFFFFFDSSSSSSSSSSSSDFFFFFFFF.",
        ".GGGGGGGGSSSSSSSSSSSSSSGGGGGGGG.",
        "........DSDSSD....DSDSSD........",
        "........DSDSSD....DSDSSD........",
        "........DAAAAD....DAAAAD........",
        "........LLDDLL....LLDDLL........",
        "................................",
        "................................",
    };

    private static readonly string[] LeftBody = {
        "................................",
        "................................",
        "..........AAAAAAAAAAAA..........",
        "..........DDDDDDDDAAAAAA........",
        "........AAAAAAAAAAAADDDS........",
        "........AAAAAAAALLLLLCCC........",
        ".......SSSSSSSSALLLLLCCCS.......",
        ".......OOOOOOSSAAAAAACCCS.......",
        "......OLLLLLLOAASSSSSCCCS.......",
        "......OAAAARROAASSRRSASSS.......",
        "......ODDDDRROAASSRRSAAAS.......",
        "......OYYYDAAOAAAAAAAAAAS.......",
        "......ODDDDALOUUUPPUUCCC........",
        ".....OAAAAAALOUUUPPUUCCC........",
        ".....ODLLDLLLOUUUPPUUCCC........",
        ".....OAAAAAAAOUUUPPUUCCC........",
        "......OLLLLLLOUUUPPUUADA........",
        ".......OOOOOOFVVVVVVFAAA........",
        "........DDDDDFGGFFGGFSSS........",
        "........DASSSFFFFFFFFSSS........",
        "........DDSSSFFFFFFFFSDD........",
        "........SSSSSGGGGGGGGSSS........",
        "........DSDSSD....DSDSSD........",
        "........DSDSSD....DSDSSD........",
        "........DAAAAD....DAAAAD........",
        "........LLDDLL....LLDDLL........",
        "................................",
        "................................",
    };

    private static readonly string[] RightBody = {
        "................................",
        "................................",
        "..........AAAAAAAAAAAA..........",
        "........AAAAAADDDDDDDD..........",
        "........SDDDAAAAAAAAAAAA........",
        "........CCCLLLLLAAAAAAAA........",
        ".......SCCCLLLLLASSSSSSSS.......",
        ".......SCCCAAAAAASSOOOOOO.......",
        ".......SCCCSSSSSAAOLLLLLLO......",
        ".......SSSASRRSSAAORRAAAAO......",
        ".......SAAASRRSSAAORRDDDDO......",
        ".......SAAAAAAAAAAOAADYYYO......",
        "........CCCUUPPUUUOLADDDDO......",
        "........CCCUUPPUUUOLAAAAAAO.....",
        "........CCCUUPPUUUOLLLDLLDO.....",
        "........CCCUUPPUUUOAAAAAAAO.....",
        "........ADAUUPPUUUOLLLLLLO......",
        "........AAAFVVVVVVFOOOOOO.......",
        "........SSSFGGFFGGFDDDDD........",
        "........SSSFFFFFFFFSSSAD........",
        "........DDSFFFFFFFFSSSDD........",
        "........SSSGGGGGGGGSSSSS........",
        "........DSSDSD....DSSDSD........",
        "........DSSDSD....DSSDSD........",
        "........DAAAAD....DAAAAD........",
        "........LLDDLL....LLDDLL........",
        "................................",
        "................................",
    };

    private Texture2D CreateTexture(Vector2 facing, bool attacking)
    {
        string[] rows = facing == Vector2.up ? BackBody :
            facing == Vector2.left ? LeftBody : facing == Vector2.right ? RightBody : FrontBody;
        bool profile = facing == Vector2.left || facing == Vector2.right;
        var pixels = new Color[32 * 28];
        for (int row = 0; row < 28; row++)
        for (int x = 0; x < 32; x++)
        {
            char symbol = rows[row][x];
            bool arm = symbol == 'U' || symbol == 'V' || symbol == 'F' || symbol == 'G' || symbol == 'P';
            if (attacking && arm) symbol = profile ? 'S' : '.';
            pixels[(27 - row) * 32 + x] = PixelColor(symbol);
        }

        if (attacking)
        {
            // Both fists hit the ground in front/back views; the near arm covers
            // the far arm in profile. No duplicate idle fists are left behind.
            if (profile)
            {
                int armX = facing == Vector2.left ? 14 : 12;
                Paint(pixels, armX, 6, 6, 12, 'U');
                Paint(pixels, armX + 2, 8, 2, 7, 'P');
                Paint(pixels, armX + 1, 9, 4, 1, 'V');
                Paint(pixels, 12, 2, 8, 5, 'F');
                Paint(pixels, 13, 5, 6, 1, 'V');
                Paint(pixels, 12, 2, 8, 1, 'G');
                Paint(pixels, facing == Vector2.left ? 7 : 22, 16, 3, 1, 'Z');
            }
            else
            {
                Paint(pixels, 3, 6, 4, 12, 'U');
                Paint(pixels, 25, 6, 4, 12, 'U');
                Paint(pixels, 4, 8, 2, 7, 'P');
                Paint(pixels, 3, 9, 4, 1, 'V');
                Paint(pixels, 26, 8, 2, 7, 'P');
                Paint(pixels, 25, 9, 4, 1, 'V');
                Paint(pixels, 1, 2, 7, 5, 'F');
                Paint(pixels, 24, 2, 7, 5, 'F');
                Paint(pixels, 2, 5, 5, 1, 'V');
                Paint(pixels, 25, 5, 5, 1, 'V');
                Paint(pixels, 1, 2, 7, 1, 'G');
                Paint(pixels, 24, 2, 7, 1, 'G');
                if (facing == Vector2.down)
                {
                    Paint(pixels, 11, 16, 3, 1, 'Z');
                    Paint(pixels, 18, 16, 3, 1, 'Z');
                }
                else
                {
                    Paint(pixels, 14, 15, 4, 1, 'E');
                    Paint(pixels, 14, 13, 4, 1, 'E');
                    Paint(pixels, 14, 11, 4, 1, 'E');
                }
            }
        }

        var texture = new Texture2D(32, 28, TextureFormat.RGBA32, false)
        {
            name = "Behemoth " + facing + (attacking ? " Slam" : " Idle"),
            filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.SetPixels(pixels);
        // Keep idle pixels available to walking and ghost-form recoloring.
        texture.Apply(false, attacking);
        return texture;
    }

    private void Paint(Color[] pixels, int x, int y, int width, int height, char symbol)
    {
        for (int py = y; py < y + height; py++)
        for (int px = x; px < x + width; px++)
            pixels[py * 32 + px] = PixelColor(symbol);
    }

    private Color PixelColor(char symbol)
    {
        switch (symbol)
        {
            case 'A': case 'F': return armorColor;
            case 'D': case 'G': return armorDarkColor;
            case 'S': case 'U': return Color.Lerp(armorColor, Color.black, 0.3f);
            case 'L': case 'V': return Color.Lerp(armorColor, Color.white, 0.28f);
            case 'C': return Color.Lerp(energyColor, armorDarkColor, 0.30f);
            case 'T': return Color.Lerp(energyColor, armorDarkColor, 0.58f);
            case 'R': case 'P': return energyColor;
            case 'E': return Color.Lerp(energyColor, Color.white, 0.18f);
            case 'O': return Color.Lerp(armorDarkColor, Color.black, 0.42f);
            case 'Y': return eyeColor;
            case 'Z': return Color.Lerp(eyeColor, Color.white, 0.4f);
            default: return Color.clear;
        }
    }

    private static int DirectionIndex(Vector2 direction)
    {
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
        {
            return direction.x < 0f ? 2 : 3;
        }
        return direction.y > 0f ? 1 : 0;
    }

    private void OnDestroy() => ReleaseSprites();

    private void ReleaseSprites()
    {
        for (int i = 0; i < 4; i++)
        {
            ReleaseGenerated(idleSprites[i]); ReleaseGenerated(idleTextures[i]);
            ReleaseGenerated(attackSprites[i]); ReleaseGenerated(attackTextures[i]);
            idleSprites[i] = null; idleTextures[i] = null;
            attackSprites[i] = null; attackTextures[i] = null;
        }
    }

    private void ReleaseGenerated(Object generated)
    {
        if (generated == null) return;
        if (Application.isPlaying) Destroy(generated);
        else DestroyImmediate(generated);
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        visualsDirty = true;
        behemothAttackPower = Mathf.Max(0, behemothAttackPower);
        behemothAttackDuration =
            Mathf.Max(0.05f, behemothAttackDuration);
        shockwaveSize.x = Mathf.Max(0.1f, shockwaveSize.x);
        shockwaveSize.y = Mathf.Max(0.1f, shockwaveSize.y);
    }

    private void OnDrawGizmos()
    {
        if (Application.isPlaying)
        {
            return;
        }

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(1f, 0.12f, 0.08f, 0.9f);
        Gizmos.DrawWireCube(
            new Vector3(0f, 0.72f, 0f),
            new Vector3(2f, 1.75f, 0f));
        Gizmos.color = new Color(1f, 0.35f, 0.2f, 0.45f);
        // Shockwave size is world-space and must not grow with the body scale.
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
        Gizmos.DrawWireSphere(Vector3.zero, shockwaveSize.x * 0.5f);
        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }
}
