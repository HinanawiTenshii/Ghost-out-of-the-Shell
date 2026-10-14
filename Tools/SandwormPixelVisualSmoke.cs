using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

// Isolated-project, editor-only GPU verification of the shipped 3x pixel-art prefab.
// Run with -executeMethod SandwormPixelVisualSmoke.RunGpu; do not use -nographics.
// No production scene is loaded, saved, or edited. Boss instances retain authored 3x scale.
public static class SandwormPixelVisualSmoke
{
    const string PrefabPath = "Assets/Prefabs/Level2NPCS/Sandworm-BossPrototype.prefab";
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly List<Object> Owned = new List<Object>();
    static readonly Color Background = new Color(.06f, .075f, .17f);
    static readonly Color Ink = new Color(.88f, .78f, .57f);
    static readonly Color Muted = new Color(.54f, .59f, .63f);
    static int checks;

    static T Own<T>(T item) where T : Object { Owned.Add(item); return item; }
    static void Check(bool value, string label) { checks++; if (!value) throw new Exception("Sandworm pixel: " + label); }
    static float Field(SandwormBossPrototype worm, string name) => (float)typeof(SandwormBossPrototype).GetField(name, Fields).GetValue(worm);

    static void Cleanup()
    {
        RenderTexture.active = null;
        for (int i = Owned.Count - 1; i >= 0; i--)
            if (Owned[i] != null) Object.DestroyImmediate(Owned[i]);
        Owned.Clear();
    }

    static SandwormBossPrototype Worm()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Check(prefab != null, "production prefab imports");
        Check(Vector3.Distance(prefab.transform.localScale, Vector3.one * 3) < .0001f, "authored prefab root is exactly 3x in all axes");
        var root = Own(Object.Instantiate(prefab));
        root.name = "Actual-size sandworm pixel fixture";
        root.transform.position = Vector3.zero;
        var worm = root.GetComponent<SandwormBossPrototype>();
        Check(worm != null, "production motion controller exists");
        root.GetComponent<SandwormStateController>().EnterAboveGround();
        worm.ResetPose();
        worm.SetMovementEnabled(true);
        return worm;
    }

    static Transform[] Parts(SandwormBossPrototype worm)
    {
        var parts = new List<Transform> { worm.Head };
        parts.AddRange(worm.BodySegments);
        parts.Add(worm.Tail);
        return parts.ToArray();
    }

    static Vector2[] Positions(SandwormBossPrototype worm)
    {
        var parts = Parts(worm);
        var points = new Vector2[parts.Length];
        for (int i = 0; i < parts.Length; i++) points[i] = parts[i].position;
        return points;
    }

    static Color32[] ReadTexture(Texture2D source)
    {
        // Generated production textures intentionally release their CPU copy. Read them
        // through Unity's graphics device; no external image manipulation is involved.
        var target = Own(new RenderTexture(source.width, source.height, 0, RenderTextureFormat.ARGB32));
        target.filterMode = FilterMode.Point;
        target.antiAliasing = 1;
        var readable = Own(new Texture2D(source.width, source.height, TextureFormat.RGBA32, false));
        Graphics.Blit(source, target);
        RenderTexture.active = target;
        readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
        readable.Apply();
        RenderTexture.active = null;
        return readable.GetPixels32();
    }

    static void TestPixelsAndCache(SandwormBossPrototype first, SandwormBossPrototype second)
    {
        Transform[] firstParts = Parts(first), secondParts = Parts(second);
        Check(firstParts.Length == 8 && secondParts.Length == 8, "head, six body segments, and tail remain separate");
        var references = new HashSet<Transform>();
        var sprites = new HashSet<Sprite>();
        var textures = new HashSet<Texture2D>();
        var visited = new HashSet<Texture2D>();
        for (int i = 0; i < firstParts.Length; i++)
        {
            var part = firstParts[i];
            Check(part != null && references.Add(part), "distinct editable transform " + i);
            Check(Vector3.Distance(part.localScale, Vector3.one) < .0001f, "part does not cancel or multiply the authored 3x scale " + i);
            var visual = part.GetComponent<SandwormSegmentVisual>();
            var renderer = part.GetComponent<SpriteRenderer>();
            var other = secondParts[i].GetComponent<SpriteRenderer>();
            Check(visual != null && renderer != null && renderer.sprite != null, "pixel renderer bound " + i);
            Sprite sprite = renderer.sprite;
            Check(sprite == other.sprite && sprite.texture == other.sprite.texture, "separate bosses share sprite/texture cache " + i);
            Check(Mathf.Abs(sprite.pixelsPerUnit - 16) < .001f, "16 source pixels per local unit " + i);
            Vector2 expectedSize = visual.Kind == SandwormSegmentKind.Head ? new Vector2(1.625f, 1.875f) :
                visual.Kind == SandwormSegmentKind.Tail ? new Vector2(1.875f, 1.5f) : new Vector2(1.125f, 1.75f);
            Check(Vector2.Distance(sprite.bounds.size, expectedSize) < .0001f &&
                Vector2.Distance(visual.NominalSize, expectedSize) < .0001f, "lower PPU preserves original world-space part dimensions " + i);
            Check(sprite.texture.filterMode == FilterMode.Point, "nearest-neighbor sampling " + i);
            Check(sprite.texture.width <= 32 && sprite.texture.height <= 32, "native coarse pixel-art source " + i);
            Check(sprite.texture.mipmapCount == 1, "no blurred mipmap pyramid " + i);
            Check(sprite.rect.width == sprite.texture.width && sprite.rect.height == sprite.texture.height, "whole low-resolution sprite source");
            sprites.Add(sprite); textures.Add(sprite.texture);
            if (!visited.Add(sprite.texture)) continue;
            Color32[] pixels = ReadTexture(sprite.texture);
            int opaque = 0, oldAmber = 0, greenAnnotation = 0;
            int minX = sprite.texture.width, maxX = -1, minY = sprite.texture.height, maxY = -1;
            var palette = new HashSet<Color32>();
            for (int p = 0; p < pixels.Length; p++)
            {
                Color32 color = pixels[p];
                Check(color.a == 0 || color.a == 255, "pixel alpha is binary, without anti-aliased edge samples");
                if (color.a == 0) continue;
                opaque++; palette.Add(color);
                int x = p % sprite.texture.width, y = p / sprite.texture.width;
                minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                if (Math.Abs(color.r - 248) <= 5 && Math.Abs(color.g - 177) <= 5 && Math.Abs(color.b - 53) <= 5) oldAmber++;
                if (color.g > color.r && color.g > color.b * 1.1f) greenAnnotation++;
            }
            Check(opaque > pixels.Length / 4 && opaque < pixels.Length, "compact opaque shape with transparent silhouette");
            Check(palette.Count >= 3 && palette.Count <= 6, "chibi art uses at most six flat colors including mouth and teeth");
            Check(oldAmber == 0, "former amber eye pixels are completely absent");
            Check(greenAnnotation == 0, "reference green mouth annotation is not part of any sprite palette");
            Check(ConnectedSilhouette(pixels, sprite.texture.width), "side limbs, armor and teeth remain connected, without floating pixels");
            int upperTipWidth = 0, lowerTipWidth = 0;
            for (int x = 0; x < sprite.texture.width; x++)
            {
                if (pixels[maxY * sprite.texture.width + x].a != 0) upperTipWidth++;
                if (pixels[minY * sprite.texture.width + x].a != 0) lowerTipWidth++;
            }
            Check(upperTipWidth >= 3 && lowerTipWidth >= 3, "outer lobes and paddles have broad tips rather than one-pixel spikes");
            if (visual.Kind != SandwormSegmentKind.Tail)
                Check(opaque > pixels.Length * .55f, "head and body use plump filled silhouettes instead of thin appendages");
            if (visual.Kind == SandwormSegmentKind.Body)
            {
                Check(sprite.texture.width == 18 && sprite.texture.height == 28, "16 PPU wide body canvas is 18x28 pixels");
                Check((maxY - minY + 1) > (maxX - minX + 1) * 1.2f, "body is visibly broader across its travel axis");
                Check(palette.Count <= 4, "body keeps just outline, shadow, midtone and broad highlight");
            }
            if (visual.Kind == SandwormSegmentKind.Head)
            {
                Check(sprite.texture.width == 26 && sprite.texture.height == 30, "16 PPU head canvas is 26x30 pixels");
                int frontTeeth = 0, darkMaw = 0;
                for (int y = 0; y < sprite.texture.height; y++)
                    for (int x = 14; x < sprite.texture.width; x++)
                    {
                        Color32 p = pixels[y * sprite.texture.width + x];
                        if (p.a == 0) continue;
                        if (p.r > 250 && p.g > 215 && p.b > 145) frontTeeth++;
                        if (p.r < 55 && p.g < 35 && p.b < 35) darkMaw++;
                    }
                Check(frontTeeth >= 18, "larger pale cutting edges make the mandibles prominent");
                Check(darkMaw >= 28, "an open dark mouth separates the paired jaws");
            }
            if (visual.Kind == SandwormSegmentKind.Tail)
                Check(sprite.texture.width == 30 && sprite.texture.height == 24, "16 PPU tail canvas is 30x24 pixels");
            int fineBlocks = 0;
            int width = sprite.texture.width;
            for (int y = 0; y < sprite.texture.height - 1; y += 2)
                for (int x = 0; x < width - 1; x += 2)
                {
                    Color32 p = pixels[y * width + x];
                    if (!p.Equals(pixels[y * width + x + 1]) || !p.Equals(pixels[(y + 1) * width + x]) ||
                        !p.Equals(pixels[(y + 1) * width + x + 1])) fineBlocks++;
                }
            Check(fineBlocks > 10, "native 16 PPU art uses its full pixel grid, not duplicated 2x2 blocks");
        }
        Check(sprites.Count <= 5 && textures.Count <= 5, "at most five shared sprite variants for all eight parts");
        Check(typeof(SandwormSegmentVisual).GetField("Amber", BindingFlags.NonPublic | BindingFlags.Static) == null, "eye-specific amber palette removed from visual implementation");
        foreach (var transform in first.GetComponentsInChildren<Transform>(true))
            Check(transform.name.IndexOf("eye", StringComparison.OrdinalIgnoreCase) < 0, "no separate eye object remains");
        foreach (var worm in new[] { first, second })
        {
            Check(worm.GetComponentsInChildren<Collider2D>(true).Length == 8 && worm.GetComponentsInChildren<Collider>(true).Length == 0, "eight authored 2D segment colliders, without 3D physics");
            Check(worm.GetComponentsInChildren<Rigidbody2D>(true).Length == 1 && worm.GetComponentsInChildren<Rigidbody>(true).Length == 0, "one shared 2D physics body preserves articulated movement");
            Check(worm.GetComponent<Rigidbody2D>().bodyType == RigidbodyType2D.Kinematic, "kinematic physics cannot topple or detach the segments");
        }
        var before = first.Head.GetComponent<SpriteRenderer>().sprite;
        first.gameObject.SetActive(false); first.gameObject.SetActive(true);
        Check(first.Head.GetComponent<SpriteRenderer>().sprite == before, "enable cycle reuses cached pixel art");
    }

    static void TestScaledMovement(SandwormBossPrototype worm)
    {
        float configuredSpeed = Field(worm, "moveSpeed") * 3;
        Check(configuredSpeed > 0f, "authored roaming speed is positive");
        // Exercise six world units of motion at the current Inspector speed;
        // do not require the historical speed or overwrite user-authored settings.
        float stepTime = .06f / configuredSpeed;
        Check(Mathf.Abs(Field(worm, "obstacleClearance") - .95f) < .001f, "navigation clearance matches broad head");
        Vector2[] initial = Positions(worm);
        float spacing = Field(worm, "segmentSpacing") * 3;
        for (int i = 1; i < initial.Length; i++)
            Check(Mathf.Abs(Vector2.Distance(initial[i - 1], initial[i]) - spacing) < .001f, "3x spacing preserved on initial part " + i);
        Check(initial[7].x < -17, "actual 3x body is not normalized to a small preview");
        var randomState = UnityEngine.Random.state;
        UnityEngine.Random.InitState(1387);
        try
        {
            for (int i = 0; i < 40; i++)
            {
                Check(worm.TryPickDestination(), "3x boss can choose a legal destination " + i);
                Vector2 point = worm.CurrentDestination;
                Check(Mathf.Abs(point.x) < 30 && Mathf.Abs(point.y) < 22.5f && worm.IsDestinationAllowed(point), "destination remains in the 60x45 area enlarged for varied long-distance roaming");
            }
        }
        finally { UnityEngine.Random.state = randomState; }
        Check(!worm.TrySetDestination(new Vector2(31, 0)) && !worm.TrySetDestination(new Vector2(0, 23)), "enlarged roaming area still rejects outside goals");
        Check(worm.TrySetDestination(new Vector2(8, 0)), "scaled straight target accepted");
        for (int step = 0; step < 100; step++) Step(worm, spacing, stepTime);
        Check(worm.HeadPosition.x > 5.8f && worm.HeadPosition.x < 6.1f, "travel distance agrees with the configured world speed");
        Check(worm.Tail.position.x > initial[7].x + 5.8f, "large tail advances along head's history");
        // Leave room for the larger turning radius at the current authored speed.
        Check(worm.TrySetDestination(new Vector2(12, 9)), "scaled curved target within original region");
        for (int step = 0; step < 60; step++) Step(worm, spacing, stepTime);
        Check(worm.HeadPosition.y > .5f, "large head turns toward curved route");
        Check(Mathf.Abs(worm.Tail.position.y) < .025f, "large tail still follows the earlier straight route, with true lag");
        Check(worm.BodySegments[0].position.y < worm.HeadPosition.y - .2f, "body bend trails the head rather than snapping to it");
        Check(worm.TrailPointCount > 2 && worm.TrailPointCount < 2048, "3x articulation keeps bounded trail history");
        worm.SetMovementEnabled(false);
        Vector2[] paused = Positions(worm);
        worm.Advance(.1f);
        Vector2[] after = Positions(worm);
        for (int i = 0; i < paused.Length; i++) Check(Vector2.Distance(paused[i], after[i]) < .0001f, "pause preserves large articulated part " + i);
    }

    static void Step(SandwormBossPrototype worm, float spacing, float deltaTime)
    {
        var before = Positions(worm);
        worm.Advance(deltaTime);
        var after = Positions(worm);
        for (int i = 0; i < after.Length; i++)
        {
            Check(Vector2.Distance(before[i], after[i]) < .095f, "3x part moves continuously without snap " + i);
            if (i == 0) continue;
            float gap = Vector2.Distance(after[i - 1], after[i]);
            Check(gap > spacing * .58f && gap < spacing * 1.07f, "scaled follower keeps controlled spacing " + i);
        }
    }

    static void Label(string text, Vector2 position, float size, Color color)
    {
        var go = Own(new GameObject("Pixel preview label"));
        go.transform.position = new Vector3(position.x, position.y, -1);
        var label = go.AddComponent<TextMesh>();
#if UNITY_2022_2_OR_NEWER
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
        label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
        label.text = text; label.fontSize = 60; label.characterSize = size;
        label.anchor = TextAnchor.UpperLeft; label.color = color;
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = label.font.material; renderer.sortingOrder = 1000;
    }

    static void Line(Vector2 from, Vector2 to, Color color, float width = .025f)
    {
        var line = Own(new GameObject("Pixel preview ruler")).AddComponent<LineRenderer>();
        line.sharedMaterial = Own(new Material(Shader.Find("Sprites/Default")));
        line.startColor = color; line.endColor = color;
        line.startWidth = width; line.endWidth = width; line.positionCount = 2;
        line.SetPosition(0, new Vector3(from.x, from.y, 1));
        line.SetPosition(1, new Vector3(to.x, to.y, 1));
        line.sortingOrder = -100;
    }

    static Bounds BoundsOf(SandwormBossPrototype worm)
    {
        var renderers = worm.GetComponentsInChildren<SpriteRenderer>();
        Bounds bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }

    static void Soldier(Vector2 position)
    {
        // Same native 16x16 Front pixels, palette, pivot and 16 PPU as
        // ArabSoldierZeldaCharacterData, without unrelated combat dependencies.
        string[] rows = {
            "......HHHH......", ".....WHHWW......", "....HWWWWWWH....", ".....WFEFEW.....",
            ".....DFFFFD.....", "....RCCCCCCR....", "....ARCCCLLA....", "....LLRCASLL....",
            "....FFRCAAFF....", "....FFBCBBFF....", ".....TSTTST.....", ".....TSTTST.....",
            ".....KK..KK.....", ".....KK..KK.....", "................", "................"
        };
        var palette = new Dictionary<char, Color> {
            ['A'] = new Color(.40f,.45f,.47f), ['S'] = new Color(.24f,.29f,.31f), ['L'] = new Color(.57f,.60f,.59f),
            ['W'] = new Color(.68f,.65f,.56f), ['H'] = new Color(.85f,.81f,.69f), ['D'] = new Color(.43f,.41f,.35f),
            ['F'] = new Color(.69f,.46f,.29f), ['E'] = new Color(.06f,.05f,.04f), ['T'] = new Color(.52f,.47f,.34f),
            ['B'] = new Color(.28f,.19f,.10f), ['K'] = new Color(.22f,.14f,.08f), ['C'] = new Color(.57f,.59f,.60f),
            ['R'] = new Color(.34f,.37f,.39f), ['.'] = Color.clear
        };
        var texture = Own(new Texture2D(16, 16, TextureFormat.RGBA32, false));
        texture.filterMode = FilterMode.Point;
        var pixels = new Color[256];
        for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++) pixels[(15 - y) * 16 + x] = palette[rows[y][x]];
        texture.SetPixels(pixels); texture.Apply();
        var sprite = Own(Sprite.Create(texture, new Rect(0, 0, 16, 16), new Vector2(.5f, .25f), 16));
        var root = Own(new GameObject("Level 2 soldier at original 1x scale"));
        root.transform.position = position;
        root.AddComponent<SpriteRenderer>().sprite = sprite;
        Check(root.transform.localScale == Vector3.one, "NPC reference retains original one-unit scale");
    }

    static void Detail(SandwormSegmentKind kind, Vector2 position)
    {
        var root = Own(new GameObject("Original-size 3x " + kind + " detail"));
        root.transform.position = position;
        root.transform.localScale = Vector3.one * 3;
        root.AddComponent<SandwormSegmentVisual>().Configure(kind);
        root.GetComponent<SpriteRenderer>().sortingOrder = 5;
    }

    static void Preview(SandwormBossPrototype hero)
    {
        // Straight pose makes every armor pixel easy to inspect. Only translation is used;
        // both complete worm and component samples keep the prefab's actual 3x scale.
        hero.ResetPose();
        hero.SetMovementEnabled(false);
        Bounds bounds = BoundsOf(hero);
        hero.transform.position += new Vector3(-bounds.center.x, 4.5f - bounds.center.y, 0);
        bounds = BoundsOf(hero);
        Check(Vector3.Distance(hero.transform.localScale, Vector3.one * 3) < .0001f, "complete preview worm keeps exact authored 3x scale");
        Check(bounds.size.x > 19 && bounds.size.x < 34, "complete enlarged body fits camera without scale normalization");

        Label("SANDWORM  /  CHIBI CARAPACE", new Vector2(-20.5f, 13.0f), .19f, Ink);
        Label("TRUE 3x SCALE    |    16 PPU    |    PLUMP SHELL + HEAVY JAWS    |    NO EYES / NO GREEN GLOW", new Vector2(-20.5f, 11.2f), .105f, Muted);
        Label("01   COMPLETE BODY / ACTUAL PREFAB SCALE", new Vector2(-20.5f, 9.3f), .11f, Muted);
        Line(new Vector2(-20.5f, -.6f), new Vector2(20.5f, -.6f), new Color(.19f, .22f, .26f));
        // Same scene/camera/world scale. The reference is deliberately close to the head.
        Vector2 npc = new Vector2(bounds.max.x + 1.8f, bounds.center.y - 1.0f);
        Soldier(npc);
        Label("LEVEL 2 SOLDIER\nORIGINAL 1x", npc + new Vector2(-1, -1.15f), .09f, Muted);
        Line(new Vector2(-18.5f, 1), new Vector2(-15.5f, 1), Ink, .045f);
        Line(new Vector2(-18.5f, .85f), new Vector2(-18.5f, 1.15f), Ink, .035f);
        Line(new Vector2(-15.5f, .85f), new Vector2(-15.5f, 1.15f), Ink, .035f);
        Label("3 WORLD UNITS", new Vector2(-18.5f, .6f), .085f, Muted);

        Label("02   INDIVIDUAL SECTIONS / SAME WORLD SCALE AS ABOVE", new Vector2(-20.5f, -1.5f), .11f, Muted);
        Detail(SandwormSegmentKind.Head, new Vector2(-12, -6));
        Detail(SandwormSegmentKind.Body, new Vector2(0, -6));
        Detail(SandwormSegmentKind.Tail, new Vector2(12, -6));
        Label("HEAD / 26 x 30 PX", new Vector2(-15.0f, -9.6f), .10f, Ink);
        Label("BODY / 18 x 28 PX", new Vector2(-3.0f, -9.6f), .10f, Ink);
        Label("TAIL / 30 x 24 PX", new Vector2(9.0f, -9.6f), .10f, Ink);
        Label("8 INDEPENDENT PARTS    /    POINT FILTERING    /    8 COLLIDERS    /    SHARED HEALTH", new Vector2(-20.5f, -12.1f), .10f, Muted);

        var camera = Own(new GameObject("Sandworm pixel orthographic preview")).AddComponent<Camera>();
        camera.transform.position = new Vector3(0, 0, -50);
        camera.orthographic = true; camera.orthographicSize = 14;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Background;
        camera.allowMSAA = false; camera.allowHDR = false;
        var target = Own(new RenderTexture(1600, 1000, 24));
        target.antiAliasing = 1; target.filterMode = FilterMode.Point;
        camera.targetTexture = target; camera.aspect = 1.6f;
        var image = Own(new Texture2D(1600, 1000, TextureFormat.RGBA32, false));
        camera.Render(); RenderTexture.active = target;
        image.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0); image.Apply();
        int warmPixels = 0;
        foreach (Color32 pixel in image.GetPixels32()) if (pixel.r > 100 && pixel.r > pixel.b * 1.4f) warmPixels++;
        Check(warmPixels > 40000, "native GPU render clearly shows enlarged warm pixel geometry");
        string output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Sandworm-pixel-preview.png"));
        File.WriteAllBytes(output, image.EncodeToPNG());
        camera.targetTexture = null; RenderTexture.active = null;
        Debug.Log("SANDWORM_PIXEL_PREVIEW " + output);
    }

    public static void RunGpu()
    {
        try
        {
            checks = 0;
            var first = Worm();
            var second = Worm();
            TestPixelsAndCache(first, second);
            TestScaledMovement(first);
            TestUnderground(first, second);
            first.gameObject.SetActive(false);
            Preview(second);
            Cleanup();
            PreviewCurve();
            Cleanup();
            PreviewStates();
            Cleanup();
            TestUndergroundOcclusion();
            Cleanup();
            Debug.Log("SANDWORM_PIXEL_VISUAL_PASS checks=" + checks);
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Cleanup(); Debug.LogException(error); EditorApplication.Exit(1); }
    }

    static void TestUnderground(SandwormBossPrototype first, SandwormBossPrototype second)
    {
        Transform[] a = Parts(first), b = Parts(second);
        var surface = new Sprite[a.Length];
        var positions = Positions(first);
        var orders = new int[a.Length];
        for (int i = 0; i < a.Length; i++) orders[i] = a[i].GetComponent<SpriteRenderer>().sortingOrder;
        for (int i = 0; i < a.Length; i++) surface[i] = a[i].GetComponent<SpriteRenderer>().sprite;
        var state = first.GetComponent<SandwormStateController>();
        var other = second.GetComponent<SandwormStateController>();
        state.EnterUnderground(); other.EnterUnderground();
        var visited = new HashSet<Texture2D>();
        var masked = new Sprite[a.Length];
        for (int i = 0; i < a.Length; i++)
        {
            var renderer = a[i].GetComponent<SpriteRenderer>();
            masked[i] = renderer.sprite;
            Check(masked[i] == b[i].GetComponent<SpriteRenderer>().sprite, "underground sprite cache is shared between worms");
            Check(masked[i] != surface[i] && masked[i].pixelsPerUnit == 16, "underground uses native 16-PPU variant");
            Check(masked[i].bounds == surface[i].bounds, "mask does not change part size or pivot");
            Check(renderer.color == Color.white && renderer.sharedMaterial.shader.name == "Sprites/Default", "no multiplicative tint or material replacement");
            Check(!a[i].GetComponent<Collider2D>().enabled, "underground collision off");
            Check(renderer.sortingOrder == orders[i], "group preserves per-part draw order while underground");
            Check(Vector2.Distance(a[i].position, positions[i]) < .0001f, "state change never moves segments");
            if (!visited.Add(masked[i].texture)) continue;
            var original = ReadTexture(surface[i].texture);
            var tinted = ReadTexture(masked[i].texture);
            Check(original.Length == tinted.Length, "mask preserves native dimensions");
            for (int p = 0; p < original.Length; p++)
            {
                Check(original[p].a == tinted[p].a, "mask retains transparent silhouette without a colored rectangle");
                if (original[p].a == 0) continue;
                Color32 expected = Color32.Lerp(original[p], new Color32(15, 33, 60, 255), .5f);
                Check(Math.Abs(tinted[p].r - expected.r) <= 1 && Math.Abs(tinted[p].g - expected.g) <= 1 &&
                    Math.Abs(tinted[p].b - expected.b) <= 1, "pixel matches exact RGB(15,33,60) alpha-over at 50 percent");
            }
        }
        Check(first.GetComponentsInChildren<SpriteRenderer>().Length == 8, "mask adds no sprite renderers or extra passes");
        state.EnterAboveGround(); other.EnterAboveGround();
        for (int i = 0; i < a.Length; i++)
        {
            Check(a[i].GetComponent<SpriteRenderer>().sprite == surface[i], "surfacing restores original cached sprite exactly");
            Check(a[i].GetComponent<Collider2D>().enabled, "surfacing restores collision");
            Check(a[i].GetComponent<SpriteRenderer>().sortingOrder == orders[i], "surface draw order is unchanged");
        }
        state.EnterUnderground();
        for (int i = 0; i < a.Length; i++) Check(a[i].GetComponent<SpriteRenderer>().sprite == masked[i], "repeated toggles reuse masks");
        state.EnterAboveGround();
        var serialized = new SerializedObject(state);
        serialized.FindProperty("state").enumValueIndex = (int)SandwormState.Underground;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        // Invoke only this component's edit-mode refresh. SendMessage also dispatches
        // Update to play-only character behaviours sharing the root in this fixture.
        typeof(SandwormStateController).GetMethod("Update", Fields).Invoke(state, null);
        Check(state.IsUnderground && !a[0].GetComponent<Collider2D>().enabled, "Inspector change applies without Play Mode");
        state.EnterAboveGround();
    }

    static void PreviewStates()
    {
        var above = Worm();
        var below = Worm();
        above.SetMovementEnabled(false); below.SetMovementEnabled(false);
        below.GetComponent<SandwormStateController>().EnterUnderground();
        var bounds = BoundsOf(above);
        above.transform.position += new Vector3(-bounds.center.x, 4.2f - bounds.center.y, 0);
        bounds = BoundsOf(below);
        below.transform.position += new Vector3(-bounds.center.x, -4.2f - bounds.center.y, 0);
        Label("ABOVE GROUND / ORIGINAL COLORS / COLLISION ON", new Vector2(-15, 8.8f), .13f, Ink);
        Label("UNDERGROUND / RGB 15, 33, 60 AT 50% / COLLISION OFF", new Vector2(-15, .4f), .13f, Ink);
        var camera = Own(new GameObject("Sandworm state comparison")).AddComponent<Camera>();
        camera.transform.position = new Vector3(0, 0, -50);
        camera.orthographic = true; camera.orthographicSize = 10;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Background;
        camera.allowMSAA = false; camera.allowHDR = false;
        var target = Own(new RenderTexture(1440, 900, 24));
        target.antiAliasing = 1; target.filterMode = FilterMode.Point;
        camera.targetTexture = target; camera.aspect = 1.6f;
        var preview = Own(new Texture2D(1440, 900, TextureFormat.RGBA32, false));
        camera.Render(); RenderTexture.active = target;
        preview.ReadPixels(new Rect(0, 0, 1440, 900), 0, 0); preview.Apply();
        string output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Sandworm-states-preview.png"));
        File.WriteAllBytes(output, preview.EncodeToPNG());
        camera.targetTexture = null; RenderTexture.active = null;
        Debug.Log("SANDWORM_STATES_PREVIEW " + output);
    }

    static void TestUndergroundOcclusion()
    {
        var worm = Worm();
        worm.SetMovementEnabled(false);
        var state = worm.GetComponent<SandwormStateController>();
        var group = worm.GetComponent<SortingGroup>();
        Check(group != null && !group.enabled, "surface has no external sorting override");
        var white = Own(new Texture2D(1, 1, TextureFormat.RGBA32, false));
        white.SetPixel(0, 0, Color.white); white.Apply();
        white.filterMode = FilterMode.Point;
        var square = Own(Sprite.Create(white, new Rect(0, 0, 1, 1), Vector2.one * .5f, 1));
        Color32 floorColor = new Color32(20, 40, 120, 255);
        Color32 decorColor = new Color32(175, 185, 195, 255);
        Color32 characterColor = new Color32(255, 30, 40, 255);
        var floor = Own(new GameObject("Floor at project order -10")).AddComponent<SpriteRenderer>();
        floor.sprite = square; floor.color = floorColor; floor.sortingOrder = -10;
        floor.transform.localScale = new Vector3(20, 10, 1);
        var decor = Own(new GameObject("Lowest decoration at project order -7")).AddComponent<SpriteRenderer>();
        decor.sprite = square; decor.color = decorColor; decor.sortingOrder = -7;
        decor.transform.position = worm.Head.position;
        decor.transform.localScale = Vector3.one * .8f;
        var character = Own(new GameObject("Character at project order 1")).AddComponent<SpriteRenderer>();
        character.sprite = square; character.color = characterColor; character.sortingOrder = 1;
        character.transform.position = worm.BodySegments[0].position;
        character.transform.localScale = Vector3.one * .8f;
        var camera = Own(new GameObject("World sorting regression camera")).AddComponent<Camera>();
        camera.transform.position = new Vector3(0, 0, -50);
        camera.orthographic = true; camera.orthographicSize = 5; camera.aspect = 2;
        camera.allowMSAA = false; camera.allowHDR = false;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
        var target = Own(new RenderTexture(800, 400, 24));
        target.antiAliasing = 1; camera.targetTexture = target;
        var rendered = Own(new Texture2D(800, 400, TextureFormat.RGBA32, false));
        Action capture = () => {
            // Camera.Render runs synchronously in Edit Mode, before Unity's normal
            // per-frame sorting-group update after enabling/disabling a group.
            SortingGroup.UpdateAllSortingGroups();
            camera.Render(); RenderTexture.active = target;
            rendered.ReadPixels(new Rect(0, 0, 800, 400), 0, 0); rendered.Apply();
            RenderTexture.active = null;
        };
        Func<Vector3, Color32> sample = point => {
            Vector3 p = camera.WorldToScreenPoint(point);
            return rendered.GetPixel(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y));
        };
        Func<Color32, Color32, bool> matches = (a, b) => Math.Abs(a.r - b.r) < 3 &&
            Math.Abs(a.g - b.g) < 3 && Math.Abs(a.b - b.b) < 3;
        capture();
        Check(!matches(sample(decor.transform.position), decorColor), "surface retains authored foreground appearance over decoration");
        Check(!matches(sample(character.transform.position), characterColor), "surface retains authored foreground appearance over character");
        state.EnterUnderground();
        capture();
        Check(group.enabled && group.sortingLayerID == 0 && group.sortingOrder == -9, "underground grouped between ground and scenery");
        Debug.Log("SANDWORM_SORTING_SAMPLES decor=" + sample(decor.transform.position) +
            " character=" + sample(character.transform.position) + " floor=" + sample(new Vector3(8, 0, 0)));
        Check(matches(sample(decor.transform.position), decorColor), "GPU: scenery draws in front of underground head");
        Check(matches(sample(character.transform.position), characterColor), "GPU: character draws in front of underground body");
        Check(!matches(sample(worm.Head.position + Vector3.up), floorColor), "GPU: ground does not hide underground worm");
        Check(matches(sample(new Vector3(8, 0, 0)), floorColor), "GPU: ordinary floor remains intact");
        state.EnterAboveGround();
        capture();
        Check(!group.enabled && !matches(sample(decor.transform.position), decorColor), "GPU: surfacing restores original order");
        state.EnterUnderground(); state.enabled = false;
        Check(!group.enabled, "disabling state component cannot leave sorting override behind");
        state.enabled = true;
        capture();
        Check(matches(sample(character.transform.position), characterColor), "GPU: re-enable restores underground ordering");
        camera.targetTexture = null;
    }

    static bool ConnectedSilhouette(Color32[] pixels, int width)
    {
        int origin = Array.FindIndex(pixels, p => p.a != 0);
        if (origin < 0) return false;
        var visited = new bool[pixels.Length];
        var queue = new Queue<int>();
        queue.Enqueue(origin); visited[origin] = true;
        int reached = 0, opaque = 0, height = pixels.Length / width;
        foreach (Color32 pixel in pixels) if (pixel.a != 0) opaque++;
        while (queue.Count > 0)
        {
            int pixel = queue.Dequeue(), x = pixel % width, y = pixel / width;
            reached++;
            for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
            {
                int xx = x + dx, yy = y + dy;
                if (xx < 0 || xx >= width || yy < 0 || yy >= height) continue;
                int next = yy * width + xx;
                if (visited[next] || pixels[next].a == 0) continue;
                visited[next] = true; queue.Enqueue(next);
            }
        }
        return reached == opaque;
    }

    static void PreviewCurve()
    {
        var worm = Worm();
        UnityEngine.Random.InitState(1387);
        for (int i = 0; i < 600; i++) worm.Advance(.04f);
        var bounds = BoundsOf(worm);
        var camera = Own(new GameObject("Sandworm curved pose preview")).AddComponent<Camera>();
        camera.transform.position = new Vector3(bounds.center.x, bounds.center.y, -50);
        camera.orthographic = true;
        camera.orthographicSize = Mathf.Max(bounds.extents.y + 2, (bounds.extents.x + 2) / 1.5f);
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Background;
        camera.allowMSAA = false; camera.allowHDR = false;
        var target = Own(new RenderTexture(1200, 800, 24));
        target.antiAliasing = 1; target.filterMode = FilterMode.Point;
        camera.targetTexture = target; camera.aspect = 1.5f;
        var preview = Own(new Texture2D(1200, 800, TextureFormat.RGBA32, false));
        camera.Render(); RenderTexture.active = target;
        preview.ReadPixels(new Rect(0, 0, 1200, 800), 0, 0); preview.Apply();
        string output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Sandworm-curved-preview.png"));
        File.WriteAllBytes(output, preview.EncodeToPNG());
        camera.targetTexture = null; RenderTexture.active = null;
        Debug.Log("SANDWORM_CURVED_PREVIEW " + output);
    }
}
