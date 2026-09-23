# Geometric furniture refresh

Update: Chair-Normal and Table-Normal now use centered prefab roots. Their scene instances were compensated to preserve placement; see `CenteredFurniturePivots.md`. Earlier notes about retaining legacy root coordinates below describe the original visual-only refresh.

Updated five existing prefabs in `Assets/Prefabs/Decorations/`, matching the
low-saturation, flat rectangular style of the remade beds:

| Prefab | Visual | Square sprites |
| --- | --- | --- |
| Chest | Wood storage chest, muted metal reinforcing bands and side latch | 6 |
| Table-Normal | Plain wood tabletop with broad inset and front rail | 4 |
| Shelf | Existing double-door cabinet with lower drawer and restrained brass handles | 8 |
| WorkTable | Wood workbench, blue-gray workpiece, hammer and angular tongs | 8 |
| Doll | Wooden training dummy with square red/linen target, headband and stand | 10 |

Only existing built-in Square sprites and Sprites/Default material are used.
No runtime visual generators, custom shaders or textures were added. Existing
object names/IDs, prefab GUIDs, root transforms and local depth are retained.
All original solid BoxCollider2D components and their owning transforms were
compared byte-for-byte before/after and remain unchanged. Doll retains its original
DollAttackShake component and settings (duration 0.18, amount 0.09, speed 80);
its child SpriteRenderers, including the new pieces, are found by the existing
script's GetComponentsInChildren call. No collider was added to Doll.

This task targets Table-Normal, not the separate PixelTopDownTable prefab.
No scene files or gameplay scripts were edited. Individual child colors remain
editable in SpriteRenderer. The Shelf keeps its original cabinet appearance
rather than becoming a different kind of open bookshelf.

`Tools/TestGeometricFurniture.ps1` validates prefab identities, local references,
renderer counts, shapes, collision footprints and hit-shake settings, and generates
`Docs/GeometricFurniture-preview.png` from the actual serialized rectangles.
Preview panels fit each object individually and are not at equal world scale.
All checks passed and Unity logged successful import of the five updated prefabs.
The preview is static; Play Mode interactions and CRT appearance were not tested.

## Chair-Normal

The matching chair uses five built-in Square sprites: dark wood frame, lighter
wood seat, a right-side backrest and two simple arm rails. Its wood colors match
Table-Normal and the other remade furniture. Original right-side orientation,
0.4 x 0.4 footprint, root placement, local depth, sorting range (-4 to -2),
prefab GUID and existing component/object IDs remain unchanged. It originally
had no collider or behavior script; neither was added. Child SpriteRenderer
colors remain editable. No scene files were modified.

`Tools/TestGeometricChair.ps1` validates these structural properties and produces
`Docs/GeometricChair-preview.png` from the actual prefab geometry. Static checks
passed; this chair has not been tested in Play Mode or through the CRT effect.
