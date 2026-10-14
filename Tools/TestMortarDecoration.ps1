$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$source=Get-Content -Raw "$root/Assets/Scripts/Decorations/GeometricFortressDecoration.cs"
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
public struct Vector2{
 public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}
 public float sqrMagnitude=>x*x+y*y;
 public float magnitude=>(float)Math.Sqrt(sqrMagnitude);
 public Vector2 normalized{get{float l=(float)Math.Sqrt(x*x+y*y);return new Vector2(x/l,y/l);}}
 public static Vector2 operator *(Vector2 v,float f)=>new Vector2(v.x*f,v.y*f);
 public static Vector2 operator +(Vector2 a,Vector2 b)=>new Vector2(a.x+b.x,a.y+b.y);
 public static Vector2 operator -(Vector2 a,Vector2 b)=>new Vector2(a.x-b.x,a.y-b.y);
 public static float Dot(Vector2 a,Vector2 b)=>a.x*b.x+a.y*b.y;
}
public struct Vector3{public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}}
public struct Color32{
 public byte r,g,b,a;
 public static implicit operator Color32(Color c)=>new Color32{r=(byte)(c.r*255),g=(byte)(c.g*255),b=(byte)(c.b*255),a=(byte)(c.a*255)};
}
public struct Color{
 public float r,g,b,a;public Color(float r,float g,float b,float a){this.r=r;this.g=g;this.b=b;this.a=a;}
 public static Color white=>new Color(1,1,1,1);
 public static Color Lerp(Color a,Color b,float t)=>new Color(a.r+(b.r-a.r)*t,a.g+(b.g-a.g)*t,a.b+(b.b-a.b)*t,a.a+(b.a-a.a)*t);
}
public static class Mathf{
 public static float Clamp(float x,float a,float b)=>Math.Max(a,Math.Min(b,x));public static float Max(float a,float b)=>Math.Max(a,b);public static float Sqrt(float x)=>(float)Math.Sqrt(x);public const float PI=(float)Math.PI;public static float Sin(float x)=>(float)Math.Sin(x);public static float Cos(float x)=>(float)Math.Cos(x);
 public static float Floor(float v)=>(float)Math.Floor(v);
 public static float Min(float a,float b)=>Math.Min(a,b);
 public static float Repeat(float v,float length)=>v-Floor(v/length)*length;
 public static float SmoothStep(float a,float b,float t){t=Clamp(t,0,1);return a+(b-a)*t*t*(3-2*t);}
}
public class FortressProbe{
'@
$methods=@('    public enum FurnitureShape','    public struct Part','    public static Vector2 ReferenceSize(',
 '    public static List<Part> BuildRuneGlowParts(','    public static Vector2 ExpandGlowVertex(',
 '    public static float RuneBreathMultiplier(','    public static List<Part> BuildRuneGlyphParts(',
 '    public static List<Part> BuildParts(','    private static void Crate(','    private static void Beam(',
 '    private static Part Rect(','    private static Part Diamond(','    private static Part Disc(',
 '    public static void WriteFireSurface(')
$fireFields=([regex]::Matches($source,'(?s)private static readonly Vector2\[\] Fire(?:Centers|Sizes)\s*=.*?;') | ForEach-Object {$_.Value}) -join "`n"
Add-Type -TypeDefinition ($stub+$fireFields+(($methods|ForEach-Object {Extract $_}) -join "`n")+'}')
$checks=0
function Check($ok,$message){if(!$ok){throw $message};$script:checks++}
$wood=[Color]::new(.67,.39,.20,1);$metal=[Color]::new(.67,.71,.74,1);$red=[Color]::new(.86,.12,.09,1)
$kind=[FortressProbe+FurnitureShape]::Mortar
$parts=[FortressProbe]::BuildParts($kind,$wood,$metal,$red)
$size=[FortressProbe]::ReferenceSize($kind)
Add-Type -AssemblyName System.Drawing
$bmp=[Drawing.Bitmap]::new(600,660);$g=[Drawing.Graphics]::FromImage($bmp)
$g.Clear([Drawing.Color]::FromArgb(24,31,43))
$verts=0
foreach($part in $parts){
 $verts+=$part.Points.Length
 $pts=[Drawing.PointF[]]::new($part.Points.Length)
 for($v=0;$v -lt $part.Points.Length;$v++){
  $p=$part.Points[$v]
  Check ([Math]::Abs($p.x) -le $size.x/2+.00001 -and [Math]::Abs($p.y) -le $size.y/2+.00001) 'Centered geometry inside collider'
  $pts[$v]=[Drawing.PointF]::new(300+$p.x*200,290-$p.y*200)
 }
 for($v=1;$v -lt $part.Points.Length-1;$v++){
  $a=$part.Points[0];$b=$part.Points[$v];$c=$part.Points[$v+1]
  Check ((($b.x-$a.x)*($c.y-$a.y)-($b.y-$a.y)*($c.x-$a.x)) -gt .000001) 'Convex triangle fan'
 }
 $c=$part.Color;Check ($c.a -eq 1) 'Opaque geometric parts'
 $brush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(255,[int]($c.r*255),[int]($c.g*255),[int]($c.b*255)))
 $g.FillPolygon($brush,$pts);$brush.Dispose()
}
Check ($verts -lt 300) 'Compact mesh'
Check ($parts.Count -lt 40) 'Simple part count'
Check ([int]$kind -eq 6) 'Existing shape indices preserved'
$font=[Drawing.Font]::new('Arial',18)
$g.DrawString('FORTRESS MORTAR  /  TOP VIEW',$font,[Drawing.Brushes]::White,58,590)
$path=Join-Path $root 'Docs/Fortress-Mortar-preview.png'
$bmp.Save($path,[Drawing.Imaging.ImageFormat]::Png)
$g.Dispose();$bmp.Dispose();$font.Dispose()
Write-Output "PASS: $checks checks; $verts vertices; $($parts.Count) parts. Preview: $path"
