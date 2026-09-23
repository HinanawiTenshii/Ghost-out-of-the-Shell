# Level2 Floor1 courtyard fence

Scene: `Assets/Scenes/Level2/Level2-Floor1.unity`.
Hierarchy root: **Courtyard Fence - Sight Transparent**.

- Matches the reference rectangle near the northwest courtyard: west x=-83.85, east x=19, south y=18.55. The northern edge is already occupied by white walls, so no duplicate fence is placed there.
- West/east fence ends meet the existing white walls at y=70.350003 / 55.349869.
- Openings follow the existing roads: west (-83.85, 38.996738), south (-24.7536, 18.55), east (19, 39.796738). All three have 3-unit clear width, without a door or collider across the opening.
- Pillars are 0.65-square, on an 8-unit spacing grid with additional terminal/gate pillars. Each fence run is a single centered 0.44-wide gray wall, replacing the former double rails and perpendicular bars. Uses the same engine Square sprite and Sprites/Default material as existing walls; no custom texture or runtime generation script.
- 37 sprite renderers, 31 pillars, 6 continuous fence colliders plus 31 pillar colliders. Individual objects remain editable in the scene. The full wall collision footprint is unchanged.
- All new objects are on **Default**, not Blocks/Hidden Blocks. The player camera's Blocks/Hidden Blocks ray mask therefore ignores them, while the player's solid collision mask still includes them. Existing NPC perception rules are unchanged.

Validation: `pwsh -NoProfile -File Tools/TestLevel2CourtyardFence.ps1` checks references, geometry, clear gate openings, no white-wall/river overlap, continuous collision and player layer masks. Its layout preview is a static scene-data rendering, not a Unity Play Mode capture.
