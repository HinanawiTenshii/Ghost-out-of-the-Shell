# Validate the production sprite mesh and draw a static preview from its actual data.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$source = Get-Content -Raw "$root/Assets/Scripts/Decorations/PixelSkyBlueBranchTreeVisual.cs"
$prefabPath = "$root/Assets/Prefabs/Decorations/Plants/PixelSkyBlueBranchTree.prefab"
$prefab = Get-Content -Raw $prefabPath
$vertexBlock = [regex]::Match($source, '(?s)Vector2\[\] Outline =\s*\{(.*?)\};').Groups[1].Value
$vertices = @([regex]::Matches($vertexBlock, 'new Vector2\(([-\d.]+)f, ([-\d.]+)f\)') | ForEach-Object {
    ,@([double]::Parse($_.Groups[1].Value, [cultureinfo]::InvariantCulture), [double]::Parse($_.Groups[2].Value, [cultureinfo]::InvariantCulture))
})
$indexBlock = [regex]::Match($source, '(?s)ushort\[\] Triangles =\s*\{(.*?)\};').Groups[1].Value
$indices = @([regex]::Matches($indexBlock, '\d+') | ForEach-Object { [int]$_.Value })
function Cross($a, $b, $c) { return ($b[0]-$a[0])*($c[1]-$a[1])-($b[1]-$a[1])*($c[0]-$a[0]) }
if ($vertices.Count -lt 3 -or $indices.Count -ne ($vertices.Count-2)*3) { throw 'Incomplete polygon triangulation' }
$polygonArea = 0.0
for ($i=0; $i -lt $vertices.Count; $i++) {
    $a=$vertices[$i]; $b=$vertices[($i+1)%$vertices.Count]
    if ($a[0] -lt -1.5 -or $a[0] -gt 1.5 -or $a[1] -lt 0 -or $a[1] -gt 3) { throw 'Mesh outside original 3x3 footprint' }
    $polygonArea += $a[0]*$b[1]-$b[0]*$a[1]
}
$meshArea=0.0; $edges=@{}; $triangles=@()
for ($i=0; $i -lt $indices.Count; $i+=3) {
    $ids=@($indices[$i],$indices[$i+1],$indices[$i+2])
    foreach ($id in $ids) { if ($id -lt 0 -or $id -ge $vertices.Count) { throw 'Invalid triangle index' } }
    $area=Cross $vertices[$ids[0]] $vertices[$ids[1]] $vertices[$ids[2]]
    if ($area -le 0.00000001) { throw 'Degenerate or reversed triangle' }
    $meshArea+=$area
    $triangles+=,@($vertices[$ids[0]],$vertices[$ids[1]],$vertices[$ids[2]])
    for ($j=0; $j -lt 3; $j++) {
        $pair=@($ids[$j],$ids[($j+1)%3]) | Sort-Object
        $key="$($pair[0]),$($pair[1])"
        if (!$edges.ContainsKey($key)) { $edges[$key]=0 }
        $edges[$key]++
    }
}
if ([Math]::Abs($meshArea-$polygonArea) -gt 0.000001) { throw 'Triangle area differs from silhouette' }
foreach ($entry in $edges.GetEnumerator()) {
    if ($entry.Value -gt 2) { throw 'Non-manifold shared edge' }
    if ($entry.Value -eq 1) {
        $pair=$entry.Key.Split(','); $delta=[Math]::Abs([int]$pair[0]-[int]$pair[1])
        if ($delta -ne 1 -and $delta -ne $vertices.Count-1) { throw 'Internal mesh crack' }
    }
}
# Convex triangle separating-axis test: interiors must never overlap,
# otherwise a transparent Sprite would accumulate opacity at branch junctions.
function OverlapInterior($left, $right) {
    foreach ($t in @($left,$right)) {
        for ($k=0; $k -lt 3; $k++) {
            $a=$t[$k]; $b=$t[($k+1)%3]; $nx=-($b[1]-$a[1]); $ny=$b[0]-$a[0]
            $p=@($left | ForEach-Object { $_[0]*$nx+$_[1]*$ny })
            $q=@($right | ForEach-Object { $_[0]*$nx+$_[1]*$ny })
            $pMin=($p|Measure-Object -Minimum).Minimum; $pMax=($p|Measure-Object -Maximum).Maximum
            $qMin=($q|Measure-Object -Minimum).Minimum; $qMax=($q|Measure-Object -Maximum).Maximum
            if ($pMax -le $qMin+1e-9 -or $qMax -le $pMin+1e-9) { return $false }
        }
    }
    return $true
}
for ($i=0; $i -lt $triangles.Count; $i++) {
    for ($j=$i+1; $j -lt $triangles.Count; $j++) {
        if (OverlapInterior $triangles[$i] $triangles[$j]) { throw "Overlapping triangles: $i, $j" }
    }
}
if (!(Get-Content -Raw ($prefabPath+'.meta')).Contains('guid: 4f348cc82e064a9694acb65278ffafc6')) { throw 'Prefab GUID changed' }
foreach ($id in 1..4) { if (!$prefab.Contains("&84763000000000000$id")) { throw 'Original object/component ID lost' } }
if ($prefab -match 'Collider2D:|m_Layer: [1-9]') { throw 'Decoration collision/layer changed' }
if ([regex]::Matches($prefab,'(?m)^SpriteRenderer:').Count -ne 1) { throw 'Expected one renderer' }
$color=[regex]::Match($prefab,'branchColor: \{r: ([\d.]+), g: ([\d.]+), b: ([\d.]+), a: ([\d.]+)\}')
$alpha=[double][regex]::Match($prefab,'opacity: ([\d.]+)').Groups[1].Value
if ($alpha -le 0 -or $alpha -ge 1) { throw 'Tree must remain translucent' }
if (!$source.Contains('tint.a = opacity;') -or !$source.Contains('OverrideGeometry(rectVertices, Triangles)')) { throw 'Preview does not match actual renderer' }
if ($source.Contains('SpriteMeshType.FullRect') -or !$source.Contains('SpriteMeshType.Tight')) { throw 'FullRect sprites reject geometry overrides' }
if (!$source.Contains('(Outline[i] + new Vector2(1.5f, 0f)) * (GradientSize / 3f)')) { throw 'OverrideGeometry must receive sprite-rect pixel coordinates' }
$enableBody=[regex]::Match($source,'(?s)private void OnEnable\(\)(.*?)private void OnValidate').Groups[1].Value
if ($enableBody.Contains('ApplyVisual();') -or $enableBody.Contains('EnsureSprite();')) { throw 'Geometry creation must not run during OnEnable / scene loading' }
$leftHeight=($vertices|Where-Object { $_[0] -lt -.3 }|ForEach-Object { $_[1] }|Measure-Object -Maximum).Maximum
$rightHeight=($vertices|Where-Object { $_[0] -gt .3 }|ForEach-Object { $_[1] }|Measure-Object -Maximum).Maximum
if ([Math]::Abs($leftHeight-$rightHeight) -lt .3) { throw 'Crown should have visibly asymmetric heights' }
function GradientColor($name) {
    $m=[regex]::Match($source,$name+' = new Color\(([\d.]+)f, ([\d.]+)f, ([\d.]+)f, 1f\)')
    if (!$m.Success) { throw "Missing gradient color $name" }
    return [Drawing.Color]::FromArgb([int](255*$alpha),[int](255*[double]$m.Groups[1].Value*[double]$color.Groups[1].Value),[int](255*[double]$m.Groups[2].Value*[double]$color.Groups[2].Value),[int](255*[double]$m.Groups[3].Value*[double]$color.Groups[3].Value))
}
Add-Type -AssemblyName System.Drawing
$bmp=[Drawing.Bitmap]::new(740,780); $g=[Drawing.Graphics]::FromImage($bmp)
$g.Clear([Drawing.Color]::FromArgb(27,36,45))
# Broad geometric blocks behind the tree make its transparency visible.
$background=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(48,60,69))
$g.FillRectangle($background,370,38,330,680); $background.Dispose()
$grid=[Drawing.Pen]::new([Drawing.Color]::FromArgb(58,255,255,255),1)
for ($i=0; $i -le 6; $i++) { $x=70+$i*100; $y=80+$i*100; $g.DrawLine($grid,$x,60,$x,700); $g.DrawLine($grid,50,$y,690,$y) }
$grid.Dispose()
$g.SmoothingMode=[Drawing.Drawing2D.SmoothingMode]::AntiAlias
$points=[Drawing.PointF[]]@($vertices|ForEach-Object { [Drawing.PointF]::new([float](370+$_[0]*210),[float](695-$_[1]*210)) })
$brush=[Drawing.Drawing2D.LinearGradientBrush]::new([Drawing.PointF]::new(370,695),[Drawing.PointF]::new(370,65),(GradientColor 'GradientBottom'),(GradientColor 'GradientTop'))
$g.FillPolygon($brush,$points); $brush.Dispose()
$font=[Drawing.Font]::new('Segoe UI',12)
$label=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(192,208,213))
$g.DrawString('Asymmetric blue gradient | opacity 0.65 | static preview',$font,$label,60,738)
$font.Dispose(); $label.Dispose()
$preview="$root/Docs/GeometricSkyBlueBranchTree-preview.png"
$bmp.Save($preview,[Drawing.Imaging.ImageFormat]::Png); $g.Dispose(); $bmp.Dispose()
Write-Output "PASS: $($vertices.Count) vertices, $($triangles.Count) non-overlapping triangles; original prefab IDs and 3x3 footprint; one translucent renderer."
Write-Output "Preview: $preview"
