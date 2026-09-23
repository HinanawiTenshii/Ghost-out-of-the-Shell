# Geometric sky-blue branch tree

Prefab: `Assets/Prefabs/Decorations/Plants/PixelSkyBlueBranchTree.prefab`

The existing asset is a minimal leafless geometric tree with an asymmetric lean,
a lower spreading left fork, a taller right fork and an offset central twig.
A vertical gradient transitions from deep blue (0.10, 0.35, 0.56) at the base to
light sky blue (0.53, 0.82, 0.92) at the top. Branch joints remain translucent.
The legacy prefab/class names, GUIDs and object IDs remain unchanged so existing
scene instances continue to use it. Placement, bottom-center pivot, nominal 3x3
footprint, default sorting order and no-collider behavior are retained.

## Inspector

- **Branch Color** multiplies the gradient (default white preserves its colors).
- **Opacity** controls all transparency (default 0.65; 0 invisible, 1 opaque).
- **Sorting Order** retains the existing renderer-order control.

The single SpriteRenderer draws one connected mesh (38 vertices, 36 triangles).
Faces do not overlap, so intersections do not become darker/more opaque than
the rest of the tree. A shared 64x64 gradient texture supplies the color; geometric
edges, not the texture resolution, define the silhouette. All instances share
the generated sprite/texture, released when the last enabled instance is removed.
Geometry is created once, not rebuilt each frame. Editor changes refresh in edit
mode. The existing cyan 3x3 editor outline is retained.

## Geometry override fix

Unity 2021.3 rejected the previous FullRect sprite's geometry override, reproduced
in the actual editor even after deferring creation to Update. Use SpriteMeshType.Tight
instead. Also convert local outline coordinates into Sprite.rect pixel coordinates
before OverrideGeometry: `(local + (1.5, 0)) * (64 / 3)`. The API applies pivot/PPU
itself; passing local coordinates directly caused a second out-of-rectangle error.
See [Unity 2021.3 API](https://docs.unity3d.com/2021.3/Documentation/ScriptReference/Sprite.OverrideGeometry.html).

Generation is deferred from OnEnable to Update, and the shared sprite is published
only after its geometry is prepared. The prefab and script GUIDs are unchanged;
Level1-Floor1 and other existing scene instances inherit the fix automatically.

## Validation

Run `Tools/TestGeometricSkyBlueBranchTree.ps1` to check triangle winding, bounds,
area, manifold edges, pairwise non-overlap, prefab identity, translucency and
single-renderer structure, then render the preview from production mesh data.
`Docs/GeometricSkyBlueBranchTree-preview.png` is a static mesh preview over two
background tones with a grid to show transparency, not a Unity/CRT screenshot.

C# compilation and static geometry checks passed. A temporary Unity editor harness
instantiated two copies of the actual prefab and verified deferred generation,
38 vertices / 36 triangles, world-scale bounds, alpha 0.65, resource sharing, and
disable/re-enable behavior with no geometry errors. The harness and its temporary
objects were removed after testing; no scene was saved or switched.

`Docs/GeometricSkyBlueBranchTree-Unity-preview.png` is an actual Unity camera render
of the prefab, without CRT. Level1-Floor1 gameplay/CRT composition has not been
tested in Play Mode; its saved placement/instance overrides were not edited.
