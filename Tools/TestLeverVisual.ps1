$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$source=Get-Content -Raw "$root/Assets/Scripts/Interaction/LeverData.cs"
function Extract($signature){
    $start=$source.IndexOf($signature);if($start -lt 0){throw "Missing $signature"}
    $b=$source.IndexOf('{',$start);$e=$b+1;$depth=1
    while($depth){if($source[$e] -eq '{'){$depth++};if($source[$e] -eq '}'){$depth--};$e++}
    $source.Substring($start,$e-$start)
}
$code=@'
using System;
public struct Color {
 public float r,g,b,a;public Color(float r,float g,float b,float a){this.r=r;this.g=g;this.b=b;this.a=a;}
 public static Color clear=>new Color();public static Color black=>new Color(0,0,0,1);
 public static Color Lerp(Color x,Color y,float t)=>new Color(x.r+(y.r-x.r)*t,x.g+(y.g-x.g)*t,x.b+(y.b-x.b)*t,x.a+(y.a-x.a)*t);
}
public struct Vector2 {
 public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}
 public float sqrMagnitude=>x*x+y*y;public Vector2 normalized=>this*(1f/(float)Math.Sqrt(sqrMagnitude));
 public static Vector2 up=>new Vector2(0,1);
 public static Vector2 operator -(Vector2 a,Vector2 b)=>new Vector2(a.x-b.x,a.y-b.y);
 public static Vector2 operator +(Vector2 a,Vector2 b)=>new Vector2(a.x+b.x,a.y+b.y);
 public static Vector2 operator *(Vector2 a,float b)=>new Vector2(a.x*b,a.y*b);
 public static float Distance(Vector2 a,Vector2 b)=>(float)Math.Sqrt((a-b).sqrMagnitude);
 public static float Dot(Vector2 a,Vector2 b)=>a.x*b.x+a.y*b.y;
}
public static class Mathf {public static float Max(float a,float b)=>Math.Max(a,b);public static float Abs(float a)=>Math.Abs(a);public static int RoundToInt(float x)=>(int)Math.Round(x);}
public enum TextureFormat {RGBA32} public enum FilterMode {Point} public enum TextureWrapMode {Clamp}
public class Texture2D {
 public int width,height;public Color[] pixels;public string name;public bool mipmaps,discarded;public FilterMode filterMode;public TextureWrapMode wrapMode;
 public Texture2D(int w,int h,TextureFormat f,bool mip){width=w;height=h;pixels=new Color[w*h];mipmaps=mip;}
 public void SetPixel(int x,int y,Color c){if(x<0||x>=width||y<0||y>=height)throw new Exception("Out of bounds");pixels[y*width+x]=c;}
 public void Apply(bool mip,bool discard){discarded=discard;}
}
public struct Rect {public Rect(float x,float y,float w,float h){}}
public class Sprite {
 public string name;public Texture2D texture;public Vector2 pivot;public float ppu;
 public static Sprite Create(Texture2D t,Rect r,Vector2 p,float ppu)=>new Sprite{texture=t,pivot=p,ppu=ppu};
}
public class LeverVisualHarness {
 public Color baseColor=new Color(.42f,.28f,.16f,1),leverColor=new Color(.72f,.12f,.08f,1),metalColor=new Color(.46f,.5f,.5f,1);
 public Sprite Render(bool on)=>CreateLeverSprite(on);
'@
$signatures=@('    private Sprite CreateLeverSprite(', '    private static void FillOrientedRectangle(', '    private static void FillRect(')
foreach($sig in $signatures){$code+=Extract $sig}
$code+='}'
Add-Type -TypeDefinition $code
$checks=0
function Check($ok,$label){if(!$ok){throw $label};$script:checks++}
$renderer=[LeverVisualHarness]::new();$off=$renderer.Render($false);$on=$renderer.Render($true)
foreach($sprite in @($off,$on)){
    $t=$sprite.texture
    Check ($t.width -eq 24 -and $t.height -eq 24) 'Same coarse canvas'
    Check ([Math]::Abs($sprite.ppu-26.666667) -lt .00001 -and [Math]::Abs($sprite.pivot.y-.22) -lt .00001 -and $sprite.pivot.x -eq .5) 'World size and ground pivot unchanged'
    Check (!$t.mipmaps -and $t.discarded -and $t.filterMode.ToString() -eq 'Point') 'Point sampled, no mipmaps, discard CPU copy'
    foreach($c in $t.pixels){Check ($c.a -eq 0 -or $c.a -eq 1) 'Crisp opaque/transparent pixels'}
    # Flood-fill to catch a detached grip or shaft.
    $solid=@(0..575|Where-Object{$t.pixels[$_].a -gt 0})
    $visited=[Collections.Generic.HashSet[int]]::new();$queue=[Collections.Generic.Queue[int]]::new()
    $queue.Enqueue($solid[0]);[void]$visited.Add($solid[0])
    while($queue.Count){
        $i=$queue.Dequeue();$x=$i%24;$y=[int][Math]::Floor($i/24)
        foreach($d in @(@(-1,0),@(1,0),@(0,-1),@(0,1))){
            $nx=$x+$d[0];$ny=$y+$d[1];$n=$ny*24+$nx
            if($nx -ge 0 -and $nx -lt 24 -and $ny -ge 0 -and $ny -lt 24 -and $t.pixels[$n].a -gt 0 -and $visited.Add($n)){$queue.Enqueue($n)}
        }
    }
    Check ($visited.Count -eq $solid.Count) 'Grip, shaft and housing are connected'
}
foreach($y in 0..23){foreach($x in 0..23){
    $a=$off.texture.pixels[$y*24+$x];$b=$on.texture.pixels[$y*24+23-$x]
    Check ($a.Equals($b)) 'On/off are exact balanced mirrors'
}}
foreach($entry in @(@($off,17),@($on,7))){
    $c=$entry[0].texture.pixels[20*24+$entry[1]]
    Check ($c.Equals($renderer.leverColor)) 'Existing puppet head attachment stays inside red grip'
    $pixels=$entry[0].texture.pixels;$headX=[int]$entry[1]
    foreach($corner in @(@(-3,17),@(2,17),@(-3,22),@(2,22))){
        $cornerColor=$pixels[$corner[1]*24+$headX+$corner[0]]
        Check ($cornerColor.a -eq 0 -or $cornerColor.r -le $cornerColor.g) 'Rounded grip corners are cut away (shaft may pass behind lower corner)'
    }
    foreach($y in 13..16){
        $rowCount=@(0..23|Where-Object{$pixels[$y*24+$_].a -gt 0}).Count
        Check ($rowCount -ge 2 -and $rowCount -le 4) 'Narrow shaft remains visible and stick-like'
    }
}
Check ($source.Contains('interactionCollider.size = new Vector2(0.66f, 0.495f);') -and $source.Contains('interactionCollider.offset = new Vector2(0f, 0.21f);')) 'Collider unchanged'
Check ($source.Contains('(headX - 12f) / 26.6667f') -and $source.Contains('(20f - 5.28f) / 26.6667f')) 'Mount position unchanged'
Check ($source.Contains('Destroy(offSprite.texture);') -and $source.Contains('Destroy(onSprite.texture);')) 'Owned texture cleanup'

Add-Type -AssemblyName System.Drawing
$bmp=[Drawing.Bitmap]::new(740,380);$g=[Drawing.Graphics]::FromImage($bmp)
$g.Clear([Drawing.Color]::FromArgb(25,32,43));$font=[Drawing.Font]::new('Segoe UI',13)
foreach($pose in 0..1){
    $sprite=@($off,$on)[$pose];$left=40+360*$pose
    for($y=0;$y -lt 24;$y++){for($x=0;$x -lt 24;$x++){
        $c=$sprite.texture.pixels[(23-$y)*24+$x];if($c.a -le 0){continue}
        $brush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb([int]($c.r*255),[int]($c.g*255),[int]($c.b*255)))
        $g.FillRectangle($brush,$left+$x*10,35+$y*10,10,10)
        $g.FillRectangle($brush,$left+255+$x*2,150+$y*2,2,2);$brush.Dispose()
    }}
    $g.DrawString(@('Off / right','On / left')[$pose],$font,[Drawing.Brushes]::White,$left+65,305)
}
$bmp.Save("$root/Docs/Lever-preview.png",[Drawing.Imaging.ImageFormat]::Png)
$g.Dispose();$bmp.Dispose();$font.Dispose()
Write-Output "PASS: $checks production lever raster/layout guards; preview generated."
