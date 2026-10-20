using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class GhostVisualSmoke
{
    const BindingFlags Flags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static readonly Vector2[] Directions = { Vector2.down, Vector2.up, Vector2.left, Vector2.right };
    static int checks, lastFrame = -1;
    static IEnumerator suite;
    static double deadline;
    static GhostVisualSmoke() { EditorApplication.update += Tick; }
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    static Sprite Frame(Vector2 direction, bool moving, float time) =>
        (Sprite)typeof(GhostZeldaCharacterData).GetMethod("SelectGhostSprite", Flags).Invoke(null, new object[] { direction, moving, time });
    static Color[] Pixels(Sprite sprite) => sprite.texture.GetPixels((int)sprite.rect.x, (int)sprite.rect.y, 16, 16);
    static bool Eye(Color p) => p.a > .9f && p.r < .1f;
    public static void Run()
    {
        try
        {
            Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "isolated test project only");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            VerifyFrames();
            PlayerSettings.productName = "ghost-visual-smoke";
            SessionState.SetBool(nameof(GhostVisualSmoke), true);
            EditorApplication.EnterPlaymode();
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void VerifyFrames()
    {
        var textures = new HashSet<Texture>();
        foreach (Vector2 direction in Directions)
        foreach (bool moving in new[] { false, true })
        {
            var shapes = new HashSet<string>();
            float fps = moving ? 6f : 3f;
            for (int pose = 0; pose < 4; pose++)
            {
                Sprite sprite = Frame(direction, moving, (pose + .1f) / fps);
                textures.Add(sprite.texture);
                Check(sprite.rect.size == Vector2.one * 16 && sprite.pixelsPerUnit == 16f && sprite.pivot == new Vector2(8, 4), "original size and pivot");
                Check(sprite.texture.filterMode == FilterMode.Point, "crisp pixel filtering");
                Color[] pixels = Pixels(sprite);
                shapes.Add(string.Concat(pixels.Select(p => p.a == 0 ? " " : Eye(p) ? "E" : ".")));
                int eyes = pixels.Count(Eye);
                Check(eyes == (direction.x != 0 ? 6 : 8), "enlarged eyes in all normal frames");
                for (int i = 16; i < 240; i++)
                    if (Eye(pixels[i]))
                        Check(pixels[i - 16].a > 0 && pixels[i + 16].a > 0 &&
                            pixels[i - 1].a > 0 && pixels[i + 1].a > 0, "eyes stay inside the body silhouette");
                Check(Connected(pixels), "body and eyes form a connected silhouette");
                Check(eyes < pixels.Count(p => p.a > 0) / 10f, "eyes do not dominate body");
                for (int i = 0; i < 16; i++)
                    Check(pixels[i].a == 0 && pixels[15 * 16 + i].a == 0 &&
                        pixels[i * 16].a == 0 && pixels[i * 16 + 15].a == 0, "no clipped pixels at canvas edge");
                Check(Frame(direction, moving, (pose + .1f) / fps) == sprite, "cached sprite reused");
            }
            Check(shapes.Count >= 3, "each facing has a multi-pose idle/glide animation");
            Check(Pixels(Frame(direction, moving, 4.15f)).Count(Eye) < Pixels(Frame(direction, moving, 4f)).Count(Eye), "brief blink");
        }
        Check(textures.Count == 1, "one shared atlas for all animation variants");
        Check(Frame(Vector2.zero, false, 0) == Frame(Vector2.down, false, 0), "zero facing defaults down");
        Check(Frame(new Vector2(-.9f, .1f), false, 0) == Frame(Vector2.left, false, 0), "diagonal facing cardinalized");
        foreach (bool moving in new[] { false, true })
        for (int pose = 0; pose < 4; pose++)
        {
            float time = (pose + .1f) / (moving ? 6f : 3f);
            Color[] left = Pixels(Frame(Vector2.left, moving, time)), right = Pixels(Frame(Vector2.right, moving, time));
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
                Check((left[y * 16 + x].a > 0) == (right[y * 16 + 15 - x].a > 0) &&
                    Eye(left[y * 16 + x]) == Eye(right[y * 16 + 15 - x]), "mirrored side poses and eyes");
        }
        RenderPreview();
        Debug.Log("GHOST_VISUAL_FRAMES_PASS checks=" + checks);
    }
    static bool Connected(Color[] pixels)
    {
        int first = Array.FindIndex(pixels, p => p.a > 0);
        var reached = new HashSet<int> { first };
        var queue = new Queue<int>(); queue.Enqueue(first);
        while (queue.Count > 0)
        {
            int i = queue.Dequeue();
            foreach (int n in new[] { i - 16, i + 16, i % 16 > 0 ? i - 1 : -1, i % 16 < 15 ? i + 1 : -1 })
                if (n >= 0 && n < 256 && pixels[n].a > 0 && reached.Add(n)) queue.Enqueue(n);
        }
        return reached.Count == pixels.Count(p => p.a > 0);
    }
    static void RenderPreview()
    {
        // Rows: down/up/left/right. Columns: four idle, four glide, blink.
        const int scale = 6, cell = 20 * scale;
        var sheet = new Texture2D(cell * 9, cell * 4, TextureFormat.RGB24, false);
        var background = new Color(.055f, .08f, .13f);
        var pixels = Enumerable.Repeat(background, sheet.width * sheet.height).ToArray();
        for (int direction = 0; direction < 4; direction++)
        for (int column = 0; column < 9; column++)
        {
            bool moving = column >= 4;
            float time = column == 8 ? 4.15f : ((column % 4) + .1f) / (moving ? 6f : 3f);
            Color[] frame = Pixels(Frame(Directions[direction], moving, time));
            for (int y = 0; y < 16 * scale; y++)
            for (int x = 0; x < 16 * scale; x++)
            {
                Color p = frame[(y / scale) * 16 + x / scale];
                int targetX = column * cell + 2 * scale + x;
                int targetY = (3 - direction) * cell + 2 * scale + y;
                pixels[targetY * sheet.width + targetX] = Color.Lerp(background, p, p.a);
            }
        }
        sheet.SetPixels(pixels); sheet.Apply();
        File.WriteAllBytes("Ghost-directional-preview.png", sheet.EncodeToPNG());
        Object.DestroyImmediate(sheet);
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(GhostVisualSmoke), false))
            { SessionState.SetBool(nameof(GhostVisualSmoke), false); suite = VerifyRuntime(); deadline = EditorApplication.timeSinceStartup + 60; }
            if (suite == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("runtime test timeout");
            if (suite.MoveNext()) return;
            suite = null; Debug.Log("GHOST_VISUAL_RUNTIME_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Time.timeScale = 1; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static IEnumerator VerifyRuntime()
    {
        while (RetroSceneLoadReveal.IsPreparingGameplay || DocumentReader.IsInputBlocked) yield return null;
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Ghost.prefab");
        var go = Object.Instantiate(prefab);
        var ghost = go.GetComponent<GhostZeldaCharacterData>();
        var mover = go.GetComponent<ZeldaFourWayMover>(); mover.enabled = false;
        var renderer = go.transform.Find("Visual").GetComponent<SpriteRenderer>();
        var collider = go.GetComponent<BoxCollider2D>();
        Vector2 size = collider.size, offset = collider.offset;
        Vector3 position = go.transform.position, localPosition = renderer.transform.localPosition, scale = renderer.transform.localScale;
        Check(!PixelCharacterWalkAnimator.Supports(ghost), "generic leg animator does not override ghost poses");
        Check(!ghost.CanAttack && !ghost.CanTakeAttackDamage && !ghost.CanBeDetectedByAi && !ghost.ParticipatesInCharacterCollision, "ghost gameplay abilities unchanged");
        var seen = new HashSet<Sprite>();
        float until = Time.time + .8f;
        while (Time.time < until)
        {
            ghost.ApplyCharacterVisualWithMovement(renderer, Vector2.right, true, false);
            seen.Add(renderer.sprite);
            yield return null;
        }
        Check(seen.Count >= 3, "actual scaled runtime clock advances animation");
        Time.timeScale = 0;
        yield return null;
        ghost.ApplyCharacterVisual(renderer, Vector2.right, true, false);
        Sprite paused = renderer.sprite;
        for (int i = 0; i < 5; i++)
        {
            yield return null;
            ghost.ApplyCharacterVisual(renderer, Vector2.right, true, false);
            Check(renderer.sprite == paused, "pause freezes ghost animation");
        }
        Time.timeScale = 1;
        foreach (Vector2 direction in Directions)
        {
            ghost.ApplyCharacterVisualWithMovement(renderer, direction, false, false);
            Check(renderer.sprite == Frame(direction, false, Time.time), "facing changes use current direction immediately");
        }
        Check(collider.size == size && collider.offset == offset, "collider unchanged");
        Check(go.transform.position == position && renderer.transform.localPosition == localPosition && renderer.transform.localScale == scale, "no physical or visual transform drift");
        Check(renderer.color.a < 1 && renderer.color.b > renderer.color.r, "translucent blue-white palette retained");
        Texture atlas = renderer.sprite.texture;
        var second = Object.Instantiate(prefab);
        var secondGhost = second.GetComponent<GhostZeldaCharacterData>();
        var secondRenderer = second.transform.Find("Visual").GetComponent<SpriteRenderer>();
        secondGhost.ApplyCharacterVisual(secondRenderer, Vector2.up, true, false);
        Check(secondRenderer.sprite.texture == atlas, "ghost instances share atlas");
        Object.Destroy(go); Object.Destroy(second);
    }
}
