# Level1 tabletop instance colors

The geometric table refresh added a central Table-TopPanel sprite. Existing
Level1 scene instances still override only the original Square (1) surface;
the new panel inherited the default teal color, creating an unrelated teal
rectangle inside yellow, orange, purple, green or gray tabletops.

Only the new panel's RGB is now overridden for the 13 custom-colored tables:

- Level1-Floor1: 9 instances.
- Level1-Floor2: 1 instance.
- Level1-Floor3: 3 instances.

The panel uses the existing surface RGB blended 6% toward white. This preserves
its hue while giving the inset a subtle, consistent brightness difference.
Existing chair colors, tabletop base colors, alpha, brown frames, scene object
positions, transforms, collision and gameplay settings are unchanged.
The shared Table-Normal prefab and Level2 are unchanged.

The change consists solely of 39 new serialized RGB property overrides targeting
the existing Table-TopPanel SpriteRenderer (865400000000000002). No objects,
materials, scripts or draw calls were added. A before/after whole-scene hash
comparison excluding these 39 additions verifies all other saved data is retained.

Run Tools/TestLevel1TableColors.ps1 to validate the 13 instances and render
Docs/Level1TableColors-preview.png using the prefab rectangles and scene colors.
This is a static palette preview, not a scene/CRT screenshot. Play Mode was not tested.
For later manual recoloring of these instances, edit both Square (1) and
Table-TopPanel colors; this is a saved scene correction, not a runtime tint-sync script.
