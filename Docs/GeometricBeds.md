# Minimal geometric beds

Updated in place under `Assets/Prefabs/Decorations/`:

- `Bed-Normal.prefab`: warm wooden frame, gray-blue blanket, one linen pillow; 8 Square sprites.
- `Bed-Guard.prefab`: gray frame, muted green blanket and pillow; 8 Square sprites.
- `Bed-Luxury.prefab`: wider dark frame, purple blanket, restrained gold fold and headboard inlay, two pillows; 10 Square sprites.

All are top-down, opaque flat rectangles using Unity's existing Square sprite and
Sprites/Default material. A headboard, footboard, recessed mattress and single
blanket fold distinguish the silhouette without pixel texture, patterns or gradients.
Existing child names, object/component IDs and prefab GUIDs are retained. Original
root placement, scale, local depth and outer footprint are unchanged: single beds
0.6708261 x 1.1950932; luxury bed 1.3586911 x 1.1950932 before scene scaling.

The prefabs had no colliders or runtime scripts and still have none. No scene was
edited. Sorting stays below characters (-5 through -1). Individual colors remain
editable on each child's SpriteRenderer. Legacy `Bed-inner1` now represents the
mattress, `Bed-inner2` the blanket's darker edge, and `Bed-blanket` its main color.

Run `Tools/TestGeometricBeds.ps1` for YAML reference, identity, footprint, color,
renderer-count and bed-part checks. It also renders `Docs/GeometricBeds-preview.png`
directly from the prefab rectangles at equal world scale. Static validation passed
and Unity's editor log confirmed import of all three prefabs. The preview is not
a Play Mode/CRT screenshot; in-game composition has not been tested.
