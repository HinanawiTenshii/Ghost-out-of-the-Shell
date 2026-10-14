$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$source=Get-Content -Raw "$root/Assets/Scripts/Decorations/GeometricShopDecoration.cs"
function Extract($signature){
 $start=$source.IndexOf($signature);if($start -lt 0){throw "Missing $signature"}
 $b=$source.IndexOf('{',$start);$e=$b+1;$depth=1
 while($depth){if($source[$e] -eq '{'){$depth++};if($source[$e] -eq '}'){$depth--};$e++}
 # Expression-bodied polygon constructors end in a semicolon after their array initializer.
 if($source.Substring($start,$b-$start).Contains('=>')){$e=$source.IndexOf(';',$e)+1}
 $source.Substring($start,$e-$start)
}
$stub=@'
using System;
using System.Collections.Generic;
public struct Vector2{public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}}
public struct Color{
 public float r,g,b,a;public Color(float r,float g,float b,float a){this.r=r;this.g=g;this.b=b;this.a=a;}
 public static Color white=>new Color(1,1,1,1);
 public static Color Lerp(Color a,Color b,float t)=>new Color(a.r+(b.r-a.r)*t,a.g+(b.g-a.g)*t,a.b+(b.b-a.b)*t,a.a+(b.a-a.a)*t);
}
public static class Mathf{public const float PI=(float)Math.PI;public static float Sin(float x)=>(float)Math.Sin(x);public static float Cos(float x)=>(float)Math.Cos(x);}
public class ShopProbe{
'@
$methods=@('    public enum FurnitureShape','    public struct Part','    public static Vector2 ReferenceSize(',
 '    public static List<Part> BuildParts(','    private static void Sword(','    private static void Helmet(',
 '    private static Part Rect(','    private static Part Diamond(','    private static Part Disc(')
Add-Type -TypeDefinition ($stub+(($methods|ForEach-Object {Extract $_}) -join "`n")+'}')
$checks=0
function Check($ok,$message){if(!$ok){throw $message};$script:checks++}
$names=@('WeaponShop-SwordDisplay','WeaponShop-PolearmDisplay','ToolShop-Workbench','ToolShop-SupplyTray','ArmorShop-ShieldDisplay','ArmorShop-HelmetDisplay')
$wood=[Color]::new(.67,.39,.20,1);$stone=[Color]::new(.67,.71,.74,1);$accent=[Color]::new(.18,.85,.49,1)
Add-Type -AssemblyName System.Drawing
$bmp=[Drawing.Bitmap]::new(1260,790);$g=[Drawing.Graphics]::FromImage($bmp)
$g.Clear([Drawing.Color]::FromArgb(24,31,43));$font=[Drawing.Font]::new('Arial',16)
$counts=@()
for($i=0;$i -lt 6;$i++){
 $accent=if($i -lt 2){[Color]::new(.52,.21,.27,1)}elseif($i -lt 4){[Color]::new(.22,.57,.56,1)}else{[Color]::new(.37,.28,.62,1)}
 $kind=[ShopProbe+FurnitureShape]$i;$parts=[ShopProbe]::BuildParts($kind,$wood,$stone,$accent)
 $size=[ShopProbe]::ReferenceSize($kind);$verts=0
 $ox=210+420*($i%3);$oy=185+390*[Math]::Floor($i/3)
 foreach($part in $parts){
  $verts+=$part.Points.Length
  Check ($part.Points.Length -ge 3) 'Polygon needs vertices'
  $pts=[Drawing.PointF[]]::new($part.Points.Length)
  for($v=0;$v -lt $part.Points.Length;$v++){
   $p=$part.Points[$v]
   Check ([Math]::Abs($p.x) -le $size.x/2+.00001 -and [Math]::Abs($p.y) -le $size.y/2+.00001) "Geometry outside centered bounds: $kind"
   $pts[$v]=[Drawing.PointF]::new($ox+$p.x*140,$oy-$p.y*140)
  }
  for($v=1;$v -lt $part.Points.Length-1;$v++){
   $a=$part.Points[0];$b=$part.Points[$v];$c=$part.Points[$v+1]
   Check ((($b.x-$a.x)*($c.y-$a.y)-($b.y-$a.y)*($c.x-$a.x)) -gt 0.000001) "Nonconvex/degenerate polygon: $kind"
  }
  $c=$part.Color;Check ($c.a -eq 1) 'Solid vertex colors'
  $brush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(255,[int]($c.r*255),[int]($c.g*255),[int]($c.b*255)))
  $g.FillPolygon($brush,$pts);$brush.Dispose()
 }
 Check ($verts -lt 300) "Keep mesh compact: $kind ($verts vertices)"
 $counts+="$kind=$verts"
 $path="$root/Assets/Prefabs/Decorations/ShopDisplays/$($names[$i]).prefab"
 $prefab=Get-Content -Raw $path
 Check ($prefab.Contains('guid: e7c759e891c04404b1b8a8a3c11bbf25') -and $prefab.Contains("shape: $i")) 'Correct visual component and kind'
 Check ($prefab.Contains('sortingOrder: -3') -and $prefab.Contains('m_Layer: 0')) 'Existing furniture render convention'
 Check ($prefab.Contains('BoxCollider2D:') -and $prefab.Contains('m_IsTrigger: 0')) 'Single simple collider'
 $solid=if($i -eq 3){0}else{1}
 Check ($prefab.Contains("solid: $solid")) 'Only supply tray defaults to non-solid'
 $colliderBlock=[regex]::Match($prefab,'(?s)BoxCollider2D:(.*?)---').Groups[1].Value
 Check ($colliderBlock.Contains("m_Enabled: $solid")) 'Serialized collider matches Solid setting'
 $prefabSize=[regex]::Match($prefab,'m_Size: \{x: ([\d.]+), y: ([\d.]+)\}')
 Check ([Math]::Abs([double]::Parse($prefabSize.Groups[1].Value,[cultureinfo]::InvariantCulture)-$size.x) -lt .00001 -and [Math]::Abs([double]::Parse($prefabSize.Groups[2].Value,[cultureinfo]::InvariantCulture)-$size.y) -lt .00001) 'Collider dimensions match rendered bounds'
 Check ($prefab.Contains('m_LocalPosition: {x: 0, y: 0, z: 0}')) 'Centered root'
 Check ((Get-Content -Raw "$path.meta") -match 'guid: [a-f0-9]{32}\s') 'Valid prefab GUID'
 $g.DrawString($names[$i],$font,[Drawing.Brushes]::White,$ox-120,$oy+130)
}
Check ($source.Contains('visual.AddComponent<MeshRenderer>()') -and !$source.Contains('AddComponent<SpriteRenderer>')) 'Single combined mesh, no per-detail renderers'
Check ($source.Contains('if (dirty || visual == null) Rebuild()')) 'No steady-state per-frame mesh regeneration'
$preview=Join-Path $root 'Docs/ShopDisplays-preview.png'
$bmp.Save($preview,[Drawing.Imaging.ImageFormat]::Png);$g.Dispose();$font.Dispose();$bmp.Dispose()
Write-Output "PASS: $checks geometry/prefab checks. Mesh vertices: $counts. Preview: $preview"
