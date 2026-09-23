$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$source=Get-Content -Raw "$root/Assets/Scripts/Decorations/GeometricCurvedTable.cs"
function Method($signature) {
    $start=$source.IndexOf($signature);if($start -lt 0){throw "Missing $signature"}
    $brace=$source.IndexOf('{',$start);$end=$brace+1;$depth=1
    while($depth -gt 0){if($source[$end] -eq '{'){$depth++};if($source[$end] -eq '}'){$depth--};$end++}
    $source.Substring($start,$end-$start)
}
$stub=@'
using System;
using System.Collections.Generic;
public struct Vector2 {
    public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}
    public static Vector2 operator +(Vector2 a,Vector2 b){return new Vector2(a.x+b.x,a.y+b.y);}
    public static Vector2 operator *(Vector2 a,float b){return new Vector2(a.x*b,a.y*b);}
}
public struct Vector3 {public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}}
public struct Color {public float r,g,b,a;public Color(float r,float g,float b,float a){this.r=r;this.g=g;this.b=b;this.a=a;}}
public static class Mathf {
    public const float Deg2Rad=(float)Math.PI/180,Rad2Deg=180/(float)Math.PI;
    public static float Sin(float x){return (float)Math.Sin(x);}public static float Cos(float x){return (float)Math.Cos(x);}
    public static float Asin(float x){return (float)Math.Asin(x);}public static float Clamp01(float x){return Math.Max(0,Math.Min(1,x));}
    public static float Max(float a,float b){return Math.Max(a,b);}public static float Min(float a,float b){return Math.Min(a,b);}
}
public class TableGeometryProbe {
    int curveSegments;
    public List<Vector3> vertices=new List<Vector3>();public List<Color> colors=new List<Color>();public List<int> triangles=new List<int>();
    public static TableGeometryProbe Build(bool arc){
        var p=new TableGeometryProbe();p.curveSegments=arc?24:40;
        float radius=arc?1.6f:.9f,inner=arc?.95f:0;
        p.AddSurface(p.vertices,p.colors,p.triangles,arc,radius,inner,0,0,new Vector2(0,-.06f),.003f,new Color(.38f,.2f,.11f,1));
        p.AddSurface(p.vertices,p.colors,p.triangles,arc,radius,inner,0,0,new Vector2(0,0),.002f,new Color(.52f,.28f,.14f,1));
        p.AddSurface(p.vertices,p.colors,p.triangles,arc,radius-.08f,arc?inner+.08f:0,0,arc?.08f:0,new Vector2(0,0),.001f,new Color(.33f,.67f,.63f,1));
        p.AddSurface(p.vertices,p.colors,p.triangles,arc,radius-.105f,arc?inner+.105f:0,0,arc?.105f:0,new Vector2(0,0),0,new Color(.22f,.57f,.56f,1));
        return p;
    }
    public static bool Contains(Vector2[] poly,float x,float y){
        bool inside=false;
        for(int i=0,j=poly.Length-1;i<poly.Length;j=i++){
            var a=poly[i];var b=poly[j];
            if(((a.y>y)!=(b.y>y))&&x<(b.x-a.x)*(y-a.y)/(b.y-a.y)+a.x)inside=!inside;
        }
        return inside;
    }
'@
$methods=(Method '    public static Vector2[] BuildCollisionOutline(')+(Method '    private void AddSurface(')+(Method '    private static Vector2 Polar(')
Add-Type -TypeDefinition ($stub+$methods+'}')
$checks=0
function Check($ok,$label){if(!$ok){throw $label};$script:checks++}
$circle=[TableGeometryProbe]::BuildCollisionOutline($false,.9,0,40,0)
$arc=[TableGeometryProbe]::BuildCollisionOutline($true,1.6,.95,24,0)
Check ($circle.Length -eq 40) 'Round collider count'
Check ($arc.Length -eq 50) 'Quarter-arc collider count'
Check ([TableGeometryProbe]::Contains($circle,0,0)) 'Circle center must be solid'
Check (![TableGeometryProbe]::Contains($circle,1,0)) 'Outside circle must be free'
Check (![TableGeometryProbe]::Contains($arc,.4,.4)) 'Inner arc opening must be free'
Check ([TableGeometryProbe]::Contains($arc,1.2,.4)) 'Arc tabletop must be solid'
Check (![TableGeometryProbe]::Contains($arc,1.5,1.5)) 'Outside arc must be free'
Check (![TableGeometryProbe]::Contains($arc,-.1,1.2)) 'Quarter arc does not fill other quadrants'
$rotated=[TableGeometryProbe]::BuildCollisionOutline($true,1.6,.95,24,90)
Check ([TableGeometryProbe]::Contains($rotated,-.4,1.2)) 'Arc angle rotates collider'
foreach($point in $circle){Check ([Math]::Abs([Math]::Sqrt($point.x*$point.x+$point.y*$point.y)-.9) -lt .00001) 'Circle radius'}
$models=@([TableGeometryProbe]::Build($false),[TableGeometryProbe]::Build($true))
foreach($model in $models){
    Check ($model.vertices.Count -lt 1100 -and $model.colors.Count -eq $model.vertices.Count) 'Small colored mesh'
    for($i=0;$i -lt $model.triangles.Count;$i+=3){
        $a=$model.vertices[$model.triangles[$i]];$b=$model.vertices[$model.triangles[$i+1]];$c=$model.vertices[$model.triangles[$i+2]]
        $cross=($b.x-$a.x)*($c.y-$a.y)-($b.y-$a.y)*($c.x-$a.x)
        Check ($cross -lt -0.00000001) 'All visible triangles have consistent nonzero area'
    }
}
foreach($name in 'Table-Round','Table-QuarterArc'){
    $path="$root/Assets/Prefabs/Decorations/$name.prefab";$prefab=Get-Content -Raw $path
    Check ($prefab.Contains('guid: f15ea45f45b74dcc9bc01f2cbdc82811')) 'Valid component GUID'
    Check ($prefab.Contains('PolygonCollider2D:') -and $prefab.Contains('m_IsTrigger: 0')) 'Solid shaped collision'
    Check ($prefab.Contains('m_Layer: 0') -and $prefab.Contains('sortingOrder: -3')) 'Match table layer and sorting'
    Check (Test-Path "$path.meta") 'Prefab meta exists'
    $points=@([regex]::Matches($prefab,'- \{x: ([-\d.]+), y: ([-\d.]+)\}'))
    $expected=if($name -eq 'Table-Round'){$circle}else{$arc}
    Check ($points.Count -eq $expected.Length) 'Serialized collider point count matches generator'
    foreach($i in 0..($points.Count-1)){
        $x=[double]::Parse($points[$i].Groups[1].Value,[cultureinfo]::InvariantCulture);$y=[double]::Parse($points[$i].Groups[2].Value,[cultureinfo]::InvariantCulture)
        Check ([Math]::Abs($x-$expected[$i].x) -lt .00001 -and [Math]::Abs($y-$expected[$i].y) -lt .00001) 'Serialized collider matches generated visual profile'
    }
}
# Render the actual production AddSurface triangles rather than a separate illustration.
Add-Type -AssemblyName System.Drawing
$bmp=[Drawing.Bitmap]::new(1000,460);$g=[Drawing.Graphics]::FromImage($bmp)
$g.Clear([Drawing.Color]::FromArgb(25,32,43));$font=[Drawing.Font]::new('Arial',14)
foreach($j in 0..1){
    $model=$models[$j];$origin=if($j -eq 0){@(245,215)}else{@(610,340)}
    for($i=0;$i -lt $model.triangles.Count;$i+=3){
        $pts=[Drawing.PointF[]]::new(3)
        foreach($k in 0..2){$v=$model.vertices[$model.triangles[$i+$k]];$pts[$k]=[Drawing.PointF]::new($origin[0]+$v.x*175,$origin[1]-$v.y*175)}
        $c=$model.colors[$model.triangles[$i]];$brush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(255,[int]($c.r*255),[int]($c.g*255),[int]($c.b*255)))
        $g.FillPolygon($brush,$pts);$brush.Dispose()
    }
}
$g.DrawString('Round table',$font,[Drawing.Brushes]::White,175,402)
$g.DrawString('Quarter arc table / 90 degrees',$font,[Drawing.Brushes]::White,600,402)
$preview=Join-Path $root 'Docs/CurvedTables-preview.png';$bmp.Save($preview,[Drawing.Imaging.ImageFormat]::Png)
$font.Dispose();$g.Dispose();$bmp.Dispose()
Write-Output "PASS: $checks geometry/collision/prefab checks. Preview: $preview"
