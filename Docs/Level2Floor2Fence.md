# Level2 Floor2 perimeter fence

Scene: `Assets/Scenes/Level2/Level2-Floor2.unity`

Root object: `Level2 Floor2 Perimeter Fence`. Existing scene objects are unchanged.

Matches the current Floor1 courtyard fence: one 0.44-unit gray wall per run,
0.65-unit lighter-gray pillars spaced at most 8 units apart, engine Square
sprites and Sprites/Default material. No new textures, materials or scripts.
There are 5 walls and 14 pillars (19 renderers and 19 solid BoxCollider2D components).

## Placement (world coordinates)

* West of the northern river: x = 18, y = 56.36311285 to 83.95306285.
* East of the building: y = 55, x = 110.2080301 to 137.7973026.
* Three 3-unit road openings: y = 59.038573..62.038573 and
  72.283573..75.283573 on the west fence; x = 120.943868..123.943868
  on the east fence.

Endpoint pillars are inset by their half-width so their outside edges meet
the existing walls or the opening boundaries. The fence does not overlap
white walls or water. All fence geometry stays on Default layer: solid for
character movement but excluded from the player camera's vision blockers,
as in Floor1.

## Validation

Run `Tools/TestLevel2Floor2Fence.ps1` for unique IDs/references, object counts,
shape sizes, matching visual/collider bounds, continuous collision, clear
gate openings, wall/water overlap and collision/vision layer checks.
It also generates `Docs/Level2-Floor2-layout-preview.png` from scene geometry.

Static checks and layout preview passed; Unity Play Mode was not run.
