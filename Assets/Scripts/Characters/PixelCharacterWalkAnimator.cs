using System.Collections.Generic;
using UnityEngine;

/// <summary>Runtime-owned cached walk frames. Does not move transforms or colliders.</summary>
[DisallowMultipleComponent]
public sealed class PixelCharacterWalkAnimator : MonoBehaviour
{
    private readonly Dictionary<Sprite, Sprite[]> cachedFrames = new Dictionary<Sprite, Sprite[]>();
    private readonly HashSet<Sprite> unreadableSprites = new HashSet<Sprite>();
    private RectInt leftFoot;
    private RectInt rightFoot;
    private float framesPerSecond;
    private float elapsed;
    private int lastFrame = -1;

    public void Initialize(ZeldaCharacterData character)
    {
        framesPerSecond = 7f;
        if (character is BehemothZeldaCharacterData)
        {
            leftFoot = new RectInt(8, 2, 6, 4);
            rightFoot = new RectInt(18, 2, 6, 4);
            framesPerSecond = 5f;
        }
        else if (character is StrongmanZeldaCharacterData)
        {
            leftFoot = new RectInt(6, 4, 3, 2);
            rightFoot = new RectInt(11, 4, 3, 2);
        }
        else if (character is SwordZeldaCharacterData ||
            character is CivilianZeldaCharacterData || character is PrisonerZeldaCharacterData)
        {
            // Short boots use one row; the raised frame occupies the trouser row above.
            leftFoot = new RectInt(5, 3, 2, 2);
            rightFoot = new RectInt(9, 3, 2, 2);
        }
        else if (character is NobleZeldaCharacterData)
        {
            leftFoot = new RectInt(4, 1, 5, 3);
            rightFoot = new RectInt(9, 1, 4, 3);
        }
        else
        {
            // Blacksmith: short legs under the apron, matching the new body maps.
            leftFoot = new RectInt(5, 3, 2, 2);
            rightFoot = new RectInt(9, 3, 2, 2);
        }
    }

    public static bool Supports(ZeldaCharacterData character)
    {
        return character is SwordZeldaCharacterData || character is StrongmanZeldaCharacterData ||
            character is CivilianZeldaCharacterData || character is PrisonerZeldaCharacterData ||
            character is BlacksmithZeldaCharacterData || character is NobleZeldaCharacterData ||
            character is BehemothZeldaCharacterData;
    }

    // The caller first reapplies the original directional idle/attack sprite.
    public void Apply(SpriteRenderer renderer, bool moving, bool attacking)
    {
        if (!moving || attacking)
        {
            elapsed = 0f;
            lastFrame = -1;
            return;
        }
        if (renderer == null || renderer.sprite == null) return;
        if (lastFrame != Time.frameCount)
        {
            elapsed = Mathf.Repeat(elapsed + Time.deltaTime, 2f / framesPerSecond);
            lastFrame = Time.frameCount;
        }
        Sprite original = renderer.sprite;
        if (!cachedFrames.TryGetValue(original, out Sprite[] frames))
        {
            if (unreadableSprites.Contains(original)) return;
            if (!original.texture.isReadable)
            {
                unreadableSprites.Add(original);
                return;
            }
            frames = CreateFrames(original);
            cachedFrames.Add(original, frames);
        }
        renderer.sprite = frames[Mathf.FloorToInt(elapsed * framesPerSecond) % 2];
    }

    private Sprite[] CreateFrames(Sprite original)
    {
        Rect rect = original.rect;
        int width = Mathf.RoundToInt(rect.width);
        int height = Mathf.RoundToInt(rect.height);
        Color[] region = original.texture.GetPixels(Mathf.RoundToInt(rect.x), Mathf.RoundToInt(rect.y), width, height);
        var pixels = new Color32[region.Length];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = region[i];
        var frames = new Sprite[2];
        for (int pose = 0; pose < 2; pose++)
        {
            Color32[] framePixels = PixelWalkFrameUtility.BuildStep(pixels, width, height, pose == 0 ? leftFoot : rightFoot);
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = original.name + " Walk " + pose,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels32(framePixels);
            // GhostFormRuntime reads these pixels to recolor the current pose.
            texture.Apply(false, false);
            frames[pose] = Sprite.Create(texture, new Rect(0, 0, width, height),
                new Vector2(original.pivot.x / width, original.pivot.y / height), original.pixelsPerUnit);
            frames[pose].name = texture.name;
            frames[pose].hideFlags = HideFlags.HideAndDontSave;
        }
        return frames;
    }

    private void OnDestroy() => InvalidateFrames();

    // Clothing edits invalidate both the base art and any derived walk frames.
    public void InvalidateFrames()
    {
        foreach (Sprite[] frames in cachedFrames.Values)
        foreach (Sprite sprite in frames)
        {
            if (sprite == null) continue;
            Texture2D texture = sprite.texture;
            if (Application.isPlaying) { Destroy(sprite); Destroy(texture); }
            else { DestroyImmediate(sprite); DestroyImmediate(texture); }
        }
        cachedFrames.Clear();
        unreadableSprites.Clear();
        elapsed = 0f;
        lastFrame = -1;
    }
}
