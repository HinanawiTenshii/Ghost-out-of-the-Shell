# Minimal hinged door visual refresh

Targets: DoorHinge and DoubleDoorHinge. Sliding doors and the rotating bridge
are not visually redesigned by this change.

## Surface

The existing unit-square leaf and circular hinge sprites are retained. Following
the request for handles on both sides, each leaf also has exactly two small
built-in Square child sprites: four renderers total for the single door, eight
for the double door. No new colliders, meshes, textures or runtime generators.

MinimalDoorSurface shades the existing rectangular leaf with a brown face,
dark edge and broad light edge. The previous painted handle has been removed.
Two gray rectangular handles straddle the long sides near the free end of each
leaf; the right leaf's pair is mirrored toward the central seam. Hinge circles
are gray. Surface marks stay inside the existing quad; shader vertices and
alpha silhouette are unchanged. Orientation shading varies only from 0.92 to 1.0.
Existing scene SpriteRenderer tints still drive the wood face color.

The existing two surface material assets retain their identities (their handle
direction settings are no longer needed); handles use Sprites/Default. No
per-frame material creation. Object-space
surface coordinates require DisableBatching, so door faces do not use dynamic
batching; each leaf face is still one quad/render submission, plus two simple
handle rectangles. No glow, particles, trails or further ornamental geometry.

Handle local centers are (0.39, +/-0.65) in the unit-square leaf coordinate system
(-0.39 on the right-hand leaf), with size (0.055, 0.5). They overlap the edge
slightly, preventing a detached look. At default door scale each is approximately
0.119 x 0.042 world units. DoorData.attachedVisuals makes them follow and reset
the existing visual-only hit shake; destroying the leaf removes its handles.
DoorHingeInteraction excludes these explicitly registered decorations from its
renderer proximity fallback, so their small protrusions do not increase the
interaction range even when AI temporarily disables the solid colliders.

## Motion

DoorHingeInteraction.easeDoorRotation defaults to false; only the two door
prefabs opt in. A normalized angular progress uses SmoothStep, preserving the
configured angle/speed duration (90 degrees at 360 degrees/s = 0.25 s).
Speed now controls average angular rate for opted-in doors, not peak rate.
The real hinge rotates the sprite and collider together, with no visual lag,
spring overshoot or collider/visual separation. Reversing begins at the current
pose; pause and zero speed hold the pose; state restoration still snaps directly.

The opt-out Quaternion.RotateTowards path remains unchanged. The bridge's
scene component does not opt in, preserving its original 45-degree/s rotation.
Interaction, key/password locks, destruction, AI passage, save-state and map
endpoint logic are unchanged.

## Requested hinge alignment

The single leaf's local center is now (5.428338, 0, 0), exactly half its original
10.856676 length from the pivot, instead of (5.281, 0.059999228, 0).
Its near short-edge midpoint therefore meets the real hinge circle center.
Double-door leaves were already at +/-5.428338 and remain unchanged.

The collider follows this explicitly requested alignment with its leaf. Its
size/offset, Rigidbody2D settings and interaction distances are unchanged.
This is not a claim that the old misaligned world-space collider position is
preserved: at the default 0.2 root scale the single leaf shifts about +0.02947 X,
-0.012 Y in the closed hinge frame. All other transforms are unchanged.

## Validation

- Runtime and editor C# projects compile with zero warnings/errors.
- TestDoorVisuals.ps1: 470 production easing-state checks with planar math stubs;
  frame rates, timing, both directions, pause, zero speed, reversal, speed changes,
  large frame deltas and restored endpoints.
- Prefab structural hashes match the pre-change assets after excluding colors,
  materials, the easing opt-in, requested single-leaf alignment and the two
  explicitly registered visual-only handle children per leaf.
- TestDoorHandles.ps1: count, mirror/size, built-in geometry, local references,
  no extra colliders, proximity exclusion, shake/reset and removal of painted latch.
- TestDoubleDoorHinge.ps1: references, seam, opposite rotation, state transitions.
- TestBridgeDoorAnimation.ps1: 19 existing sliding/interlock regression assertions.

Run the motion and bridge scripts in separate PowerShell processes because their
standalone Unity math stubs define overlapping type names. No gameplay scenes
were saved or edited for this task. In-game/CRT visuals and live physics have not
been play-tested. A temporary isolated editor preview harness was removed when
its result was unavailable, so no Unity-rendered preview is claimed.
