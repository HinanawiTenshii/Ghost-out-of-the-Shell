using UnityEngine;

public partial class HeroZeldaCharacterData
{
    [Header("Item Presentation / 双手举起物品")]
    [SerializeField] private bool celebrateItemPickup = true;
    [SerializeField, Range(.1f, .8f), InspectorName("物品举起时长")]
    private float pickupRaiseDuration = .28f;
    private const float PickupPeakHoldSeconds = 2f;
    private Sprite[] pickupPoses;
    private Sprite pickupIcon;
    private Texture2D pickupIconTexture;
    private SpriteRenderer heldPickupRenderer;
    private ZeldaFourWayMover pickupMover;
    private bool presentingPickup;
    private bool pickupPosesDirty;
    private float pickupElapsed;
    private HeroPickupHalo pickupHalo;
    public bool IsPresentingPickup => presentingPickup;
    public float PickupRaiseDuration => Mathf.Clamp(pickupRaiseDuration, .1f, .8f);
    public float PickupPresentationDuration => PickupRaiseDuration + PickupPeakHoldSeconds;
    public bool IsPickupAtPeak => presentingPickup && pickupElapsed >= PickupRaiseDuration;

    /// <summary>Cosmetic only: the caller has already committed the inventory transaction.</summary>
    public bool TryPresentPickedItem(PickupItemBase item, ZeldaFourWayMover mover)
    {
        if (!celebrateItemPickup || item == null || mover == null || mover.gameObject != gameObject ||
            !mover.isActiveAndEnabled || IsDead || IsGhostLike || mover.IsWearingCardboardBox ||
            presentingPickup || mover.IsAttacking) return false;
        var itemVisual = item.ItemVisual;
        Sprite source = itemVisual != null ? itemVisual.InventoryIcon : null;
        if (source == null) return false;
        if (!mover.TryBeginItemAttack(PickupPresentationDuration + .1f)) return false;

        pickupMover = mover;
        pickupElapsed = 0f;
        presentingPickup = true;
        ResetSprintVisual();
        // World pickups own and destroy their generated textures on the same frame.
        // Clone the native texture (also works for non-readable icons), not the
        // pickup GameObject: no duplicate inventory item, physics or use script.
        pickupIconTexture = Instantiate(source.texture);
        pickupIconTexture.name = "Hero held item texture";
        pickupIconTexture.hideFlags = HideFlags.HideAndDontSave;
        pickupIcon = Sprite.Create(pickupIconTexture, source.rect, new Vector2(.5f, .5f), source.pixelsPerUnit);
        pickupIcon.name = "Hero held " + item.ItemName;
        pickupIcon.hideFlags = HideFlags.HideAndDontSave;
        var visual = new GameObject("Hero Held Pickup") { hideFlags = HideFlags.DontSave };
        visual.transform.SetParent(transform, false);
        heldPickupRenderer = visual.AddComponent<SpriteRenderer>();
        heldPickupRenderer.sprite = pickupIcon;
        heldPickupRenderer.color = itemVisual.InventoryTint;
        Transform body = transform.Find("Visual");
        var renderer = body != null ? body.GetComponent<SpriteRenderer>() : GetComponent<SpriteRenderer>();
        if (renderer != null) ApplyPickupPresentation(renderer);
        return true;
    }

    protected override void Update()
    {
        base.Update();
        if (!presentingPickup) return;
        if (IsDead || IsGhostLike || pickupMover == null || !pickupMover.isActiveAndEnabled ||
            !pickupMover.IsUsingItemAttack || GameSaveSystem.IsLoading ||
            (SceneTravelStateManager.Instance != null && SceneTravelStateManager.Instance.IsSceneTravelRestoreInProgress))
        { EndPickupPresentation(); return; }
        // A pause or first-pickup modal must never freeze the hands / input lock.
        pickupElapsed += Time.unscaledDeltaTime;
        if (pickupElapsed >= PickupPresentationDuration) EndPickupPresentation();
    }

    private bool ApplyPickupPresentation(SpriteRenderer renderer)
    {
        if (!presentingPickup || renderer == null) return false;
        if (pickupPosesDirty) { ReleasePickupPoses(); pickupPosesDirty = false; }
        if (pickupPoses == null) pickupPoses = new Sprite[3];
        // Two full seconds at the peak, counted AFTER raising the item. Do not
        // start lowering/fading the item early or hide the effect behind the modal.
        float lift = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(pickupElapsed / PickupRaiseDuration));
        int pose = lift < .3f ? 0 : lift < .8f ? 1 : 2;
        if (pickupPoses[pose] == null) pickupPoses[pose] = CreatePickupPose(pose);
        // Face the viewer for the presentation, without modifying gameplay facing,
        // collision, position or normal directional movement frames.
        renderer.sprite = pickupPoses[pose];
        if (heldPickupRenderer != null)
        {
            if (heldPickupRenderer.transform.parent != renderer.transform)
                heldPickupRenderer.transform.SetParent(renderer.transform, false);
            float scale = .52f / Mathf.Max(.01f, Mathf.Max(pickupIcon.bounds.size.x, pickupIcon.bounds.size.y));
            heldPickupRenderer.transform.localScale = Vector3.one * scale;
            heldPickupRenderer.transform.localPosition = new Vector3(0,
                Mathf.Lerp(.25f, .88f, lift) + pickupIcon.bounds.extents.y * scale, 0);
            heldPickupRenderer.sortingLayerID = renderer.sortingLayerID;
            heldPickupRenderer.sortingOrder = renderer.sortingOrder + 1;
            Color tint = heldPickupRenderer.color;
            tint.a = renderer.color.a;
            heldPickupRenderer.color = tint;
            if (IsPickupAtPeak)
            {
                if (pickupHalo == null) pickupHalo = new HeroPickupHalo(renderer.transform);
                pickupHalo.Draw(heldPickupRenderer.transform.localPosition, renderer.sortingLayerID,
                    heldPickupRenderer.sortingOrder - 1, pickupElapsed - PickupRaiseDuration,
                    PickupPeakHoldSeconds, renderer.color.a);
            }
        }
        return true;
    }

    private Sprite CreatePickupPose(int pose)
    {
        const int w = 20, h = 24;
        var pixels = new Color[w * h];
        // Same 16 PPU body, boots and ground pivot, with transparent headroom.
        // Remove the ENTIRE resting arm, including the blue lower-end pixels at
        // y=5. Keep the inner shoulder at y=8 as the body attachment. Equipment
        // is briefly stowed rather than painted over the face or held object.
        for (int row = 0; row < 16; row++)
        for (int x = 0; x < 16; x++)
        {
            char c = Front[row][x];
            if (c == '.' || ((row >= 8 && row <= 10) && (x <= 5 || x >= 10)) ||
                (row == 7 && (x == 4 || x == 11))) continue;
            int y = 15 - row;
            if (pose == 2 && c == 'E') c = 'F';
            pixels[y * w + x + 2] = PixelColor(c);
        }
        // The resting arms used to cover these two torso edge columns. Restore
        // the shirt/belt beneath them, so lifting the arms leaves no one-pixel
        // notches between the inner shoulders and the hem on either side.
        for (int y = 5; y <= 7; y++)
        {
            Color cloth = PixelColor(y == 6 ? 'B' : 'A');
            pixels[y * w + 7] = cloth;  // body x=5 (+2 canvas margin)
            pixels[y * w + 12] = cloth; // body x=10
        }
        if (pose == 2)
        {
            // A one-pixel upward glance, without reshaping or shifting the head.
            pixels[11 * w + 8] = PixelColor('E');
            pixels[11 * w + 11] = PixelColor('E');
        }
        for (int side = 0; side < 2; side++) DrawPickupArm(pixels, side, pose);
        var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) {
            name = "Hero two-handed pickup " + pose, filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave
        };
        texture.SetPixels(pixels); texture.Apply();
        var sprite = Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(.5f, 4f / h), 16);
        sprite.name = texture.name; sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    private void DrawPickupArm(Color[] pixels, int side, int pose)
    {
        // One rigid 2 x 4 rectangle: the full original y=5..8 extent, not an
        // extended limb. Rotate sleeve and hand together in three simple steps.
        // The final sleeve spans x=3..4 at y=8 and shares an edge with the inner
        // shoulder x=5; fingers stay on the same two columns (no bent/L-shaped gap).
        for (int along = 0; along < 4; along++)
        for (int across = 0; across < 2; across++)
        {
            int x = pose == 1 ? 4 - along : (pose == 2 ? 3 : 4) + across;
            int y = pose == 1 ? 8 + across : pose == 2 ? 8 + along : 8 - along;
            char ink = along == 0 ? 'H' : along == 1 ? 'A' : 'F';
            pixels[y * 20 + (side == 0 ? x : 15 - x) + 2] = PixelColor(ink);
        }
    }

    private void EndPickupPresentation()
    {
        presentingPickup = false;
        pickupHalo?.Dispose(); pickupHalo = null;
        if (heldPickupRenderer != null) { heldPickupRenderer.gameObject.SetActive(false); ReleasePickupObject(heldPickupRenderer.gameObject); }
        ReleasePickupObject(pickupIcon);
        ReleasePickupObject(pickupIconTexture);
        heldPickupRenderer = null; pickupIcon = null; pickupIconTexture = null;
        var mover = pickupMover; pickupMover = null;
        if (mover != null) mover.EndItemAttack();
    }

    private static void ReleasePickupObject(Object value)
    {
        if (value == null) return;
        if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
    }

    private void ReleasePickupPoses()
    {
        if (pickupPoses == null) return;
        foreach (var sprite in pickupPoses)
        {
            if (sprite == null) continue;
            var texture = sprite.texture;
            if (Application.isPlaying) { Destroy(sprite); Destroy(texture); }
            else { DestroyImmediate(sprite); DestroyImmediate(texture); }
        }
        pickupPoses = null;
    }
}
