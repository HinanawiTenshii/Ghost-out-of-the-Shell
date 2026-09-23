$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$source=Get-Content -Raw "$root/Assets/Scripts/Decorations/GeometricStorageFurniture.cs"
function Method($signature) {
    $start=$source.IndexOf($signature);if($start -lt 0){throw "Missing $signature"}
    $brace=$source.IndexOf('{',$start);$end=$brace+1;$depth=1
    while($depth -gt 0){if($source[$end] -eq '{'){$depth++};if($source[$end] -eq '}'){$depth--};$end++}
    $source.Substring($start,$end-$start)
}
$stub=@'
using System;
using System.Collections.Generic;
public struct Vector2 {public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}}
public struct Color {
    public float r,g,b,a;public Color(float r,float g,float b,float a){this.r=r;this.g=g;this.b=b;this.a=a;}
    public static Color white {get{return new Color(1,1,1,1);}}
    public static Color Lerp(Color a,Color b,float t){return new Color(a.r+(b.r-a.r)*t,a.g+(b.g-a.g)*t,a.b+(b.b-a.b)*t,a.a+(b.a-a.a)*t);}
}
public class StorageProbe {
    public static List<Part> Build(bool barrel){return BuildParts(barrel,new Color(.67f,.39f,.2f,1),new Color(.38f,.2f,.11f,1),new Color(.73f,.45f,.24f,1),new Color(.67f,.71f,.74f,1));}
    public static Vector2[] Collision(bool barrel){return CollisionOutline(barrel);}
    public static bool Contains(Vector2[] poly,float x,float y){
        bool inside=false;for(int i=0,j=poly.Length-1;i<poly.Length;j=i++){
            var a=poly[i];var b=poly[j];if(((a.y>y)!=(b.y>y))&&x<(b.x-a.x)*(y-a.y)/(b.y-a.y)+a.x)inside=!inside;
        }return inside;
    }
'@
$part=[regex]::Match($source,'(?s)    private struct Part.*?(?=    private GameObject)').Value.Replace('private struct','public struct')
$methods=''
foreach($sig in '    private static List<Part> BuildParts(', '    private static void AddCrate(', '    private static Part Rect(', '    private static Vector2[] BarrelOutline()', '    private static Vector2[] CollisionOutline('){$methods+=Method $sig}
Add-Type -TypeDefinition ($stub+$part+$methods+'}')
$checks=0
function Check($ok,$label){if(!$ok){throw $label};$script:checks++}
$models=@([StorageProbe]::Build($false),[StorageProbe]::Build($true))
$names=@('StorageRack','Barrel-Horizontal-Large');$sizes=@(@(2.8,2.5),@(3.4,1.9))
foreach($j in 0..1){
    $parts=$models[$j];$w,$h=$sizes[$j];$vertices=0;$minX=100;$minY=100;$maxX=-100;$maxY=-100
    foreach($p in $parts){
        $vertices+=$p.Points.Count
        Check ($p.Color.a -eq 1) 'Opaque flat geometry'
        foreach($v in $p.Points){
            Check ([Math]::Abs($v.x) -le $w/2+.00001 -and [Math]::Abs($v.y) -le $h/2+.00001) 'Visual fits configured footprint'
            $minX=[Math]::Min($minX,$v.x);$maxX=[Math]::Max($maxX,$v.x);$minY=[Math]::Min($minY,$v.y);$maxY=[Math]::Max($maxY,$v.y)
        }
        for($i=0;$i -lt $p.Points.Count;$i++){
            $a=$p.Points[$i];$b=$p.Points[($i+1)%$p.Points.Count];$c=$p.Points[($i+2)%$p.Points.Count]
            Check ((($b.x-$a.x)*($c.y-$b.y)-($b.y-$a.y)*($c.x-$b.x)) -gt 0) 'Convex part permits fan triangulation'
        }
    }
    Check ($vertices -lt 200) 'Low vertex count'
    Check ([Math]::Abs($minX+$maxX) -lt .00001 -and [Math]::Abs($minY+$maxY) -lt .00001) 'Root at complete visual center'
    $file="$root/Assets/Prefabs/Decorations/$($names[$j]).prefab";$text=Get-Content -Raw $file
    Check ($text.Contains('guid: 732e38a15d454624ad1248c9440e18ba')) 'Correct visual component'
    Check ($text.Contains('m_LocalPosition: {x: 0, y: 0, z: 0}') -and $text.Contains('m_LocalScale: {x: 1, y: 1, z: 1}')) 'Clean prefab root'
    Check ($text.Contains('m_IsTrigger: 0') -and $text.Contains('m_Layer: 0')) 'Solid non-vision-blocking default layer'
    $outline=[StorageProbe]::Collision($j -eq 1)
    $serialized=@([regex]::Matches($text,'- \{x: ([-\d.]+), y: ([-\d.]+)\}'))
    Check ($serialized.Count -eq $outline.Count) 'Collider point count'
    foreach($i in 0..($outline.Count-1)){
        $x=[double]::Parse($serialized[$i].Groups[1].Value,[cultureinfo]::InvariantCulture);$y=[double]::Parse($serialized[$i].Groups[2].Value,[cultureinfo]::InvariantCulture)
        Check ([Math]::Abs($x-$outline[$i].x) -lt .00001 -and [Math]::Abs($y-$outline[$i].y) -lt .00001) 'Baked and generated collision agree'
    }
    Write-Output "$($names[$j]): $($parts.Count) polygons, $vertices vertices, one renderer."
}
$barrel=[StorageProbe]::Collision($true)
Check ([StorageProbe]::Contains($barrel,0,0)) 'Solid barrel body'
Check ([StorageProbe]::Contains($barrel,.92,-.9)) 'Solid overhead barrel footprint'
Check ([StorageProbe]::Contains($barrel,0,-.9)) 'Continuous barrel body, no elevation support gaps'
Check (![StorageProbe]::Contains($barrel,1.65,.9)) 'Tapered corner is not blocked'
foreach($v in $barrel){
    Check (@($barrel | Where-Object {[Math]::Abs($_.x-$v.x) -lt .00001 -and [Math]::Abs($_.y+$v.y) -lt .00001}).Count -eq 1) 'Barrel plan outline symmetric across its horizontal axis'
}
Check ($models[0][0].Points.Count -eq 4 -and [StorageProbe]::Contains($models[0][0].Points,0,0)) 'Rack has a continuous overhead platform, not open front shelves'
foreach($p in $models[1]){
    foreach($v in $p.Points){
        Check ([StorageProbe]::Contains($barrel,$v.x*.9999,$v.y*.9999)) 'Every barrel detail lies within top-down body, no exposed feet'
    }
}
Add-Type -AssemblyName System.Drawing
$bmp=[Drawing.Bitmap]::new(1120,500);$g=[Drawing.Graphics]::FromImage($bmp)
$g.Clear([Drawing.Color]::FromArgb(25,32,43));$font=[Drawing.Font]::new('Segoe UI',14)
# Draw the source Chest rectangles on the left for a direct palette/style reference.
$chest=Get-Content -Raw "$root/Assets/Prefabs/Decorations/Chest.prefab";$transforms=@{};$sprites=@()
foreach($b in [regex]::Split($chest,'(?m)(?=^--- !u!)')){
    $go=[regex]::Match($b,'m_GameObject: \{fileID: (\d+)').Groups[1].Value
    if($b -match '^--- !u!4 ' -and !$b.Contains('m_Father: {fileID: 0}')){
        $p=[regex]::Match($b,'m_LocalPosition: \{x: ([^,]+), y: ([^,]+)');$s=[regex]::Match($b,'m_LocalScale: \{x: ([^,]+), y: ([^,]+)')
        $transforms[$go]=@(([double]$p.Groups[1].Value-.020942688),([double]$p.Groups[2].Value-.037400007),[double]$s.Groups[1].Value,[double]$s.Groups[2].Value)
    }
    if($b -match '^--- !u!212 '){
        $c=[regex]::Match($b,'m_Color: \{r: ([^,]+), g: ([^,]+), b: ([^,]+)')
        $sprites+=@{go=$go;order=[int][regex]::Match($b,'m_SortingOrder: (-?\d+)').Groups[1].Value;color=@([double]$c.Groups[1].Value,[double]$c.Groups[2].Value,[double]$c.Groups[3].Value)}
    }
}
foreach($s in ($sprites|Sort-Object order)){
    $t=$transforms[$s.go];$c=$s.color;$brush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(255,[int]($c[0]*255),[int]($c[1]*255),[int]($c[2]*255)))
    $g.FillRectangle($brush,[single](150+($t[0]-$t[2]/2)*110),[single](220-($t[1]+$t[3]/2)*110),[single]($t[2]*110),[single]($t[3]*110));$brush.Dispose()
}
foreach($j in 0..1){
    $anchor=if($j -eq 0){@(475,220)}else{@(900,220)}
    foreach($p in $models[$j]){
        $points=[Drawing.PointF[]]@($p.Points|ForEach-Object{[Drawing.PointF]::new($anchor[0]+$_.x*110,$anchor[1]-$_.y*110)})
        $c=$p.Color;$brush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(255,[int]($c.r*255),[int]($c.g*255),[int]($c.b*255)))
        $g.FillPolygon($brush,$points);$brush.Dispose()
    }
}
$g.DrawString('Chest reference',$font,[Drawing.Brushes]::White,82,405)
$g.DrawString('StorageRack / Top view',$font,[Drawing.Brushes]::White,375,405)
$g.DrawString('Horizontal barrel / Top view',$font,[Drawing.Brushes]::White,770,405)
$out="$root/Docs/StorageFurniture-preview.png";$bmp.Save($out,[Drawing.Imaging.ImageFormat]::Png)
$font.Dispose();$g.Dispose();$bmp.Dispose()
Write-Output "PASS: $checks geometry/collision/prefab assertions. Preview: $out"
