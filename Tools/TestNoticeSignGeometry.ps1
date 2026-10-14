# Execute the authored rectangle mesh builder and render a static geometry preview (not Unity).
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$source=Get-Content -Raw "$root/Assets/Scripts/Decorations/GeometricSignpostDecoration.cs"
$prefab=Get-Content -Raw "$root/Assets/Prefabs/Decorations/ReadableNoticeSign.prefab"
function Extract($sig){
 $start=$source.IndexOf($sig);if($start -lt 0){throw "Missing $sig"};$end=$source.IndexOf('{',$start)+1;$depth=1
 while($depth){if($source[$end] -eq '{'){$depth++};if($source[$end] -eq '}'){$depth--};$end++};$source.Substring($start,$end-$start)
}
$code=@'
using System;using System.Collections.Generic;
public struct Vector2 {public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}}
public struct Vector3 {public float x,y,z;public static implicit operator Vector3(Vector2 p)=>new Vector3{x=p.x,y=p.y};}
public struct Color {public float r,g,b;public Color(float r,float g,float b){this.r=r;this.g=g;this.b=b;}public static Color black=>new Color();public static Color Lerp(Color a,Color b,float t)=>new Color(a.r+(b.r-a.r)*t,a.g+(b.g-a.g)*t,a.b+(b.b-a.b)*t);}
public static class Mathf {public static float Min(float a,float b)=>Math.Min(a,b);}
public class NoticeGeometry {
 public float poleWidth,poleHeight,boardWidth,boardHeight;
 public Color poleColor=new Color(.55f,.29f,.14f),boardColor=new Color(.73f,.44f,.21f);
 public List<Vector3> vertices=new List<Vector3>();public List<Color> colors=new List<Color>();public List<int> triangles=new List<int>();
 public void Build(){AddRectangle(vertices,colors,triangles,-poleWidth*.5f,0,poleWidth*.5f,poleHeight,poleColor);AddNoticeBoard(vertices,colors,triangles);}
'@
foreach($sig in @('    private void AddNoticeBoard(', '    private static void AddRectangle(', '    private static void AddPolygon(')){$code+=Extract $sig}
$code+='}'
Add-Type -TypeDefinition $code
$mesh=[NoticeGeometry]::new()
foreach($field in @('poleWidth','poleHeight','boardWidth','boardHeight')){
 $mesh.$field=[float]::Parse([regex]::Match($prefab,"(?m)^  ${field}: ([\d.]+)").Groups[1].Value,[Globalization.CultureInfo]::InvariantCulture)
}
$mesh.Build()
if($mesh.vertices.Count -ne 20 -or $mesh.triangles.Count -ne 30){throw 'Expected five rectangles in a single ten-triangle mesh'}
$stem=$mesh.poleHeight-$mesh.boardHeight
if($stem -le 0 -or $stem -gt $mesh.boardHeight*.4){throw 'Expected a short exposed stem, not the direction sign long pole'}
for($i=0;$i -lt $mesh.vertices.Count;$i+=4){
 $a=$mesh.vertices[$i];$b=$mesh.vertices[$i+1];$c=$mesh.vertices[$i+2];$d=$mesh.vertices[$i+3]
 if($a.x -ge $b.x -or $a.y -ge $c.y -or $a.y -ne $b.y -or $c.y -ne $d.y){throw 'Invalid rectangle winding or size'}
 foreach($p in @($a,$b,$c,$d)){if([Math]::Abs($p.x) -gt $mesh.boardWidth/2+.001 -or $p.y -lt 0 -or $p.y -gt $mesh.poleHeight+.001){throw 'Geometry out of bounds'}}
}
Add-Type -AssemblyName System.Drawing
$bitmap=[Drawing.Bitmap]::new(480,400);$graphics=[Drawing.Graphics]::FromImage($bitmap)
$graphics.Clear([Drawing.Color]::FromArgb(19,30,47));$scale=240;$baseY=344
for($i=0;$i -lt $mesh.vertices.Count;$i+=4){
 $a=$mesh.vertices[$i];$c=$mesh.vertices[$i+2];$t=$mesh.colors[$i]
 $brush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(255,[int]($t.r*255),[int]($t.g*255),[int]($t.b*255)))
 $graphics.FillRectangle($brush,[float](240+$a.x*$scale),[float]($baseY-$c.y*$scale),[float](($c.x-$a.x)*$scale),[float](($c.y-$a.y)*$scale));$brush.Dispose()
}
$preview=Join-Path $root 'Docs/ReadableNoticeSign-preview.png'
$bitmap.Save($preview,[Drawing.Imaging.ImageFormat]::Png);$graphics.Dispose();$bitmap.Dispose()
Write-Output "PASS: five rectangles / ten triangles, bounded geometry, short stem $stem. Static preview: $preview"
