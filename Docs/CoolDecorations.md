# Material-readable decoration palette

The current palette supersedes the overly blue material colors while retaining
saturated foliage, water, cloth and accents. Wood is brown, stone/steel is gray,
and pale linen stays neutral. Geometry remains unchanged.

## Scope

20 prefabs: the three Bed variants, Chair-Normal, Chest, Table-Normal, Shelf,
WorkTable, Doll, the fountain, GeometricSignpost, GeometricTree, GeometricFlowerbed
and all seven standalone Geometric plant variants. The already-blue translucent
branch tree, legacy pixel decorations, lights, doors, characters and global CRT
or desert filter settings were not changed.

- Stone borders: light gray RGB (0.66, 0.68, 0.70); recesses (0.43, 0.46, 0.49).
- Soil: dark earth brown (0.27, 0.17, 0.12), preserving contrast under the plants.
- Foliage: emerald green (0.12, 0.70, 0.40), lighter facets (0.22, 0.82, 0.48),
  with turquoise succulent leaves and small saturated pink/gold flowers.
- Fountain: azure water (0.07, 0.55, 0.84); particle tint (0.32, 0.84, 0.98), alpha still 0.65.
- Wood: dark brown frames (0.38, 0.20, 0.11), main chest/workbench surfaces
  (0.67, 0.39, 0.20), lighter chair seat (0.74, 0.47, 0.25); related brown
  shades on cabinet doors/drawer, wooden bed boards and the training dummy.
- Tree trunk/signpost pole: (0.55, 0.29, 0.14); arrow boards (0.73, 0.44, 0.21).
- Table-Normal retains a teal painted surface (0.22, 0.57, 0.56), lighter panel
  (0.33, 0.67, 0.63) and brown wooden perimeter/front rail.
- Guard bed frames, iron chest bands, tongs, hammer head and fountain nozzle are
  gray metal. Hammer handle is brown. Luxury bed keeps rosewood, purple and gold.
- Blue/teal/purple bedding and saturated flower/leaf/water colors remain.
  Neutral gray material colors are intentional exceptions to the high-saturation
  palette. Adjacent parts of each material use related hues with ordered brightness,
  not additional gradients, textures, shapes or a global CRT adjustment.

Only RGB fields changed: alpha, meshes/sprites, IDs/GUIDs, object counts, transforms,
colliders, interaction and animation settings were compared against the start-of-task
contents and preserved. This revision changes 13 prefabs; the seven standalone
plant variants retain their saturated colors. No scenes were edited. Instances
without per-instance color overrides inherit the new colors.

## Verification and previews

`Tools/CoolDecorationPalette.json` records all target paths and original-to-new RGB
mappings; `previousColors` maps the preceding blue-dominant palette to this revision.
`perFileColors` records prior-to-new exceptions for the painted tabletop.
`Tools/TestCoolDecorationPalette.ps1` checks 144 color fields across the
20 prefabs and the fountain's particle tint, then draws the before/after comparison
from actual sprite geometry on the same navy background (blue-dominant palette
on the left, material-readable palette on the right). Saturation checks apply
to vegetation/water/colored cloth, with separate brown-wood and gray-stone/steel checks.

The plant, fountain, flowerbed, bed, furniture, chair and geometric-tree validation/
preview scripts also pass. The flowerbed test reflects its current three-sprite structure and
the existing user-added solid frame collider; neither embedded plants nor old
collision settings were restored to make the test pass.

`Docs/CoolDecorations-comparison.png` uses an illustrative layout without particles
or CRT, not a screenshot of the level. Updated individual previews also use a navy
background. In-game CRT rendering was not tested during this color-only change.
