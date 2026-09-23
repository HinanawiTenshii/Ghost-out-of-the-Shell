$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$source=Get-Content -Raw "$root/Assets/Scripts/Items/ClockworkPuppetPickupItemVisual.cs"
$bomb=Get-Content -Raw "$root/Assets/Scripts/Items/BombPickupItemVisual.cs"
$stub=@'
using System;
using UnityEngine;
namespace UnityEngine {
    public class SerializeField:Attribute {}
    public enum TextureFormat{RGBA32} public enum FilterMode{Point} public enum TextureWrapMode{Clamp}
    public struct Color {
        public float r,g,b,a;public Color(float r,float g,float b,float a){this.r=r;this.g=g;this.b=b;this.a=a;}
        public static Color clear=>new Color();public static Color black=>new Color(0,0,0,1);
        public static Color Lerp(Color a,Color b,float t)=>new Color(a.r+(b.r-a.r)*t,a.g+(b.g-a.g)*t,a.b+(b.b-a.b)*t,a.a+(b.a-a.a)*t);
    }
    public struct Vector2 {public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}}
    public struct Rect {public Rect(float x,float y,float w,float h){}}
    public class Texture2D {
        public string name;public int width,height;public Color[] pixels;public FilterMode filterMode;public TextureWrapMode wrapMode;
        public Texture2D(int w,int h,TextureFormat f,bool mip){width=w;height=h;pixels=new Color[w*h];}
        public void SetPixel(int x,int y,Color c){if(x<0||x>=width||y<0||y>=height)throw new Exception("Pixel out of bounds");pixels[y*width+x]=c;}
        public void Apply(bool mip,bool discard){if(discard)throw new Exception("Energy texture must stay readable");}
    }
    public class Sprite {
        public string name;public Texture2D texture;public Vector2 pivot;public float ppu;
        public static Sprite Create(Texture2D t,Rect r,Vector2 p,float ppu)=>new Sprite{texture=t,pivot=p,ppu=ppu};
    }
    public static class Mathf {
        public static int Abs(int v)=>Math.Abs(v);public static int CeilToInt(float v)=>(int)Math.Ceiling(v);
        public static float Clamp01(float v)=>Math.Max(0,Math.Min(1,v));public static int Clamp(int v,int a,int b)=>Math.Max(a,Math.Min(b,v));
    }
}
public class PickupItemBase{public string ItemId="bomb";}
public class PickupItemVisualBase {
    protected virtual string RuntimeSpriteName=>"";protected virtual bool UsesEmbeddedColors=>false;
    protected virtual string[] GetPixelRows()=>null;protected virtual Color GetPixelColor(char c)=>Color.clear;
    protected T GetComponent<T>() where T:class=>new PickupItemBase() as T;
    public Color[] Render(){var rows=GetPixelRows();int w=rows[0].Length,h=rows.Length;var p=new Color[w*h];
        for(int row=0;row<h;row++){if(rows[row].Length!=w)throw new Exception("Row width");for(int x=0;x<w;x++)p[(h-1-row)*w+x]=GetPixelColor(rows[row][x]);}return p;}
}
'@
Add-Type -TypeDefinition ($stub+$source.Replace('using UnityEngine;','')+$bomb.Replace('using UnityEngine;','')) -CompilerOptions '/nowarn:0649'
$checks=0
function Check($b,$label){if(!$b){throw $label};$script:checks++}
$fibres=[regex]::Matches($source,'DrawFibre\(pixels, (\d+), (\d+), (\d+), (\d+), (\d+), (\d+), (?:true|false), lift[AB]\)')
Check ($fibres.Count -eq 4) 'Four articulated legs'
foreach($f in $fibres){
    Check ([int]$f.Groups[4].Value -lt [int]$f.Groups[2].Value) 'Joint rises above base attachment'
    Check ([int]$f.Groups[6].Value -gt [int]$f.Groups[2].Value) 'Foot bends back below base attachment'
}
$texture=$null;$sprite=[ClockworkPuppetPickupItemVisual]::CreateRuntimeSprite([ref]$texture)
$w=$texture.width;$h=$texture.height
Check ($w -eq 24 -and $h -eq 28) 'Coarse 24x28 canvas'
Check ($sprite.ppu -eq 28 -and [Math]::Abs($sprite.pivot.y-.04) -lt .00001) 'Keep normalized world height and deployed ground pivot'
$pickup=[ClockworkPuppetPickupItemVisual]::new().Render();$full=$texture.pixels.Clone()
foreach($i in 0..($full.Length-1)){
    foreach($channel in @('r','g','b','a')){Check ($pickup[$i].$channel -eq $full[$i].$channel) 'Pickup and deployed palette agree'}
    $x=$i%$w;$y=[int][Math]::Floor($i/$w)
    Check ($full[$i].a -eq $full[$y*$w+$w-1-$x].a) 'Balanced phage silhouette'
}
# Four-connected head, collar, segmented neck, base plate, spike and all four feet.
$seen=[Collections.Generic.HashSet[int]]::new();$queue=[Collections.Generic.Queue[int]]::new()
$seed=0;while($full[$seed].a -eq 0){$seed++};$queue.Enqueue($seed);[void]$seen.Add($seed)
while($queue.Count){
    $i=$queue.Dequeue();$x=$i%$w;$y=[int][Math]::Floor($i/$w)
    foreach($n in @($(if($x -gt 0){$i-1}else{-1}),$(if($x -lt $w-1){$i+1}else{-1}),$(if($y -gt 0){$i-$w}else{-1}),$(if($y -lt $h-1){$i+$w}else{-1}))){
        if($n -ge 0 -and $full[$n].a -gt 0 -and $seen.Add($n)){$queue.Enqueue($n)}
    }
}
Check ($seen.Count -eq @($full|Where-Object a -gt 0).Count) 'All mechanical parts connected'
$feet=0;$last=$false
foreach($x in 0..($w-1)){$solid=$full[$x].a -gt 0;if($solid -and !$last){$feet++};$last=$solid}
Check ($feet -eq 4) 'Four separate gripping feet'
$images=@(@{pixels=$full;w=$w;h=$h;label='Puppet / full'})
foreach($segments in 0..6){
    [ClockworkPuppetPickupItemVisual]::ApplyRuntimeEnergy($texture,($segments/6.0))
    $red=0
    foreach($i in 0..($full.Length-1)){
        $c=$texture.pixels[$i];$old=$full[$i]
        $wasEnergy=$old.r -gt $old.g*1.5 -and $old.a -gt 0
        if($c.r -gt $c.g*1.5 -and $c.a -gt 0){$red++}
        Check ($c.a -eq $old.a) 'Energy preserves silhouette'
        if(!$wasEnergy){foreach($channel in @('r','g','b')){Check ($c.$channel -eq $old.$channel) 'Energy does not change head or legs'}}
    }
    Check ($red -eq $segments*2) 'Exactly six independently depleting two-pixel segments'
    if($segments -eq 0 -or $segments -eq 3){$images+=@{pixels=$texture.pixels.Clone();w=$w;h=$h;label="Puppet / $segments of 6"}}
}
# Check normalized energy clamping and restoration.
[ClockworkPuppetPickupItemVisual]::ApplyRuntimeEnergy($texture,2)
foreach($i in 0..($full.Length-1)){foreach($channel in @('r','g','b','a')){Check ($texture.pixels[$i].$channel -eq $full[$i].$channel) 'Full recharge restores original palette'}}
[ClockworkPuppetPickupItemVisual]::ApplyRuntimeEnergy($null,0)
$bombVisual=[BombPickupItemVisual]::new()
$bombPrefab=Get-Content -Raw "$root/Assets/Resources/PickupItems/BombPickupItem.prefab"
foreach($field in @('bodyColor','bodyHighlightColor','fuseColor')){
    $m=[regex]::Match($bombPrefab,$field+': \{r: ([\d.]+), g: ([\d.]+), b: ([\d.]+), a: ([\d.]+)\}')
    $c=[UnityEngine.Color]::new([float]$m.Groups[1].Value,[float]$m.Groups[2].Value,[float]$m.Groups[3].Value,[float]$m.Groups[4].Value)
    $bombVisual.GetType().GetField($field,[Reflection.BindingFlags]'NonPublic,Instance').SetValue($bombVisual,$c)
}
$bombVisual.GetType().GetField('useBodyHighlight',[Reflection.BindingFlags]'NonPublic,Instance').SetValue($bombVisual,($bombPrefab -match 'useBodyHighlight: 1'))
$bombPixels=$bombVisual.Render()
$images+=@{pixels=$bombPixels;w=16;h=16;label='Bomb / reference'}
Add-Type -AssemblyName System.Drawing
$bmp=[Drawing.Bitmap]::new(920,285);$g=[Drawing.Graphics]::FromImage($bmp);$g.Clear([Drawing.Color]::FromArgb(24,31,43))
$font=[Drawing.Font]::new('Segoe UI',12)
foreach($j in 0..($images.Count-1)){
    $im=$images[$j];$scale=196.0/$im.h;$left=$j*230+(230-$im.w*$scale)/2
    foreach($y in 0..($im.h-1)){foreach($x in 0..($im.w-1)){
        $c=$im.pixels[$y*$im.w+$x];if($c.a -eq 0){continue}
        $brush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(255,[int]($c.r*255),[int]($c.g*255),[int]($c.b*255)))
        $g.FillRectangle($brush,[single]($left+$x*$scale),[single](20+($im.h-1-$y)*$scale),[single]$scale,[single]$scale);$brush.Dispose()
    }}
    $g.DrawString($im.label,$font,[Drawing.Brushes]::White,$j*230+30,240)
}
$bmp.Save("$root/Docs/ClockworkPuppet-preview.png",[Drawing.Imaging.ImageFormat]::Png)
$g.Dispose();$bmp.Dispose();$font.Dispose()
Write-Output "PASS: $checks production pixel/energy assertions. Preview: Docs/ClockworkPuppet-preview.png"
