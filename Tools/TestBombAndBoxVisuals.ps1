$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root=Split-Path $PSScriptRoot -Parent
$base=Get-Content -Raw (Join-Path $PSScriptRoot 'TestPeopleVisuals.ps1')
$stubs=[regex]::Match($base,"(?s)\`$stubs=@'\r?\n(.*?)\r?\n'@").Groups[1].Value
$stubs=$stubs.Replace('public Rect(int x,int y,int w,int h)','public Rect(float x,float y,float w,float h)')
$stubs=$stubs.Replace('public Color[] pixels;', 'public Color[] pixels; int width; public void SetPixel(int x,int y,Color c){pixels[y*width+x]=c;}')
$stubs=$stubs.Replace('pixels=new Color[w*h];','width=w;pixels=new Color[w*h];')
$stubs=$stubs.Replace('public static int Clamp(', 'public static int Max(int a,int b){return Math.Max(a,b);} public static int Clamp(')
$code='using UnityEngine;'+$stubs+@'
public class SerializeField:Attribute {}
public class PickupItemBase {public string ItemId;}
public class PickupItemVisualBase:PeopleVisualBase {
    private PickupItemBase item=new PickupItemBase();
    public T GetComponent<T>() where T:class {return item as T;}
    protected virtual string RuntimeSpriteName {get{return "";}}
    protected virtual FilterMode RuntimeTextureFilterMode {get{return FilterMode.Point;}}
    protected virtual bool UsesEmbeddedColors {get{return false;}}
    protected virtual Color GetPixelColor(char c){return Color.clear;}
    protected virtual string[] GetPixelRows(){return null;}
    public int Width {get{return GetPixelRows()[0].Length;}}
    public int Height {get{return GetPixelRows().Length;}}
    public Color[] Render(string id){
        item.ItemId=id;var rows=GetPixelRows();
        int width=rows[0].Length,height=rows.Length;var p=new Color[width*height];
        for(int y=0;y<height;y++){
            if(rows[y].Length!=width)throw new Exception("Width");
            for(int x=0;x<width;x++)p[(height-1-y)*width+x]=GetPixelColor(rows[y][x]);
        }
        return p;
    }
    public void SetHighlights(bool b){GetType().GetField("useBodyHighlight",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(this,b);}
}
'@
foreach($name in @('BombPickupItemVisual','CardboardBoxPickupItemVisual','PurpleBombShellPickupItemVisual','FusePickupItemVisual')) {
    $code+=(Get-Content -Raw (Join-Path $root "Assets/Scripts/Items/$name.cs")).Replace('using UnityEngine;','')
}
$placed=Get-Content -Raw (Join-Path $root 'Assets/Scripts/Items/PlacedBomb.cs')
$s=$placed.IndexOf('    private static Texture2D CreateLayerTexture(')
$e=$placed.IndexOf('    private static Sprite CreateMaskSprite(', $s)
$method=$placed.Substring($s,$e-$s).Replace('private static','public static')
$code+='public static class PlacedLayerHarness {'+$method+'}'
$utility=Get-Content -Raw (Join-Path $root 'Assets/Scripts/Characters/PixelWalkFrameUtility.cs')
$code+=$utility.Replace('using UnityEngine;','')
Add-Type -TypeDefinition $code -CompilerOptions '/nowarn:0414,0649'
$images=@(); $count=0
foreach($id in @('bomb','purple_bomb')) {
    $prefab=Get-Content -Raw (Join-Path $root ('Assets/Resources/PickupItems/'+$(if($id -eq 'bomb'){'BombPickupItem'}else{'PurpleBombPickupItem'})+'.prefab'))
    $visual=New-Object BombPickupItemVisual
    $colors=@{}
    foreach($field in @('bodyColor','bodyHighlightColor','fuseColor')) {
        $m=[regex]::Match($prefab,$field+': \{r: ([\d.]+), g: ([\d.]+), b: ([\d.]+), a: ([\d.]+)\}')
        $rgb=@(1..4|ForEach-Object{[float]::Parse($m.Groups[$_].Value,[Globalization.CultureInfo]::InvariantCulture)})
        $visual.SetColor($field,$rgb[0],$rgb[1],$rgb[2]);$colors[$field]=[UnityEngine.Color]::new($rgb[0],$rgb[1],$rgb[2],1)
    }
    $visual.SetHighlights($true)
    $pixels=$visual.Render($id);[PeopleVisualBase]::CheckConnected($pixels)
    $images+=,@{pixels=$pixels;label=$(if($id -eq 'bomb'){'Bomb'}else{'Super Bomb'})}
    $rows=[BombPickupItemVisual]::GetBombRows($id -eq 'purple_bomb')
    $body=[PlacedLayerHarness]::CreateLayerTexture($rows,0,'Body')
    $highlight=[PlacedLayerHarness]::CreateLayerTexture($rows,1,'Highlight')
    $fuse=[PlacedLayerHarness]::CreateLayerTexture($rows,2,'Fuse')
    if($body.filterMode -ne 'Point' -or $fuse.filterMode -ne 'Point'){throw 'Filtering'}
    foreach($highlights in @($true,$false)) {
        $visual.SetHighlights($highlights);$pickup=$visual.Render($id)
        foreach($i in 0..255) {
            $c=$body.pixels[$i];$tint=$colors.bodyColor
            if($highlights -and $highlight.pixels[$i].a -gt 0){$c=$highlight.pixels[$i];$tint=$colors.bodyHighlightColor}
            $actual=[UnityEngine.Color]::new(($c.r*$tint.r),($c.g*$tint.g),($c.b*$tint.b),$c.a)
            if($fuse.pixels[$i].a -gt 0){$actual=$fuse.pixels[$i]}
            $expected=$pickup[$i]
            foreach($channel in @('r','g','b','a')) {if([Math]::Abs($actual.$channel-$expected.$channel) -gt .0001){throw "Pickup/placed mismatch: $id $i $channel"}}
            $count++
        }
    }
    $flash=New-Object 'UnityEngine.Color[]' 256
    foreach($i in 0..255) {
        $c=$body.pixels[$i];$flash[$i]=[UnityEngine.Color]::new($c.r,0,0,$c.a)
        if($highlight.pixels[$i].a -gt 0){$flash[$i]=[UnityEngine.Color]::new(1,.38,.38,1)}
        if($fuse.pixels[$i].a -gt 0){$flash[$i]=$fuse.pixels[$i]}
    }
    $images+=,@{pixels=$flash;label='Armed / flash'}
}
$box=New-Object CardboardBoxPickupItemVisual
$boxPixels=$box.Render('cardboard_box')
$w=$box.Width;$h=$box.Height
if($w -ne 24 -or $h -ne 20){throw 'Box must retain original 6:5 canvas proportions on a 24x20 grid'}
# Check connectivity on the non-square box canvas.
$seen=[Collections.Generic.HashSet[int]]::new();$queue=[Collections.Generic.Queue[int]]::new()
$seed=0;while($boxPixels[$seed].a -eq 0){$seed++};[void]$seen.Add($seed);$queue.Enqueue($seed)
while($queue.Count){
    $i=$queue.Dequeue();$x=$i%$w;$y=[int][Math]::Floor($i/$w)
    foreach($n in @($(if($x -gt 0){$i-1}else{-1}),$(if($x -lt $w-1){$i+1}else{-1}),$(if($y -gt 0){$i-$w}else{-1}),$(if($y -lt $h-1){$i+$w}else{-1}))){
        if($n -ge 0 -and $boxPixels[$n].a -gt 0 -and $seen.Add($n)){$queue.Enqueue($n)}
    }
}
if($seen.Count -ne @($boxPixels|Where-Object {$_.a -gt 0}).Count){throw 'Detached box pixels'}
$tex=$null;$sprite=$box.CreateStandaloneSprite([ref]$tex)
if($tex.filterMode -ne 'Point'){throw 'Worn box must remain pixel sharp'}
foreach($i in 0..($boxPixels.Length-1)) {
    foreach($channel in @('r','g','b','a')) {if($boxPixels[$i].$channel -ne $tex.pixels[$i].$channel){throw 'Worn box differs from pickup'}}
    $count++
}
$images+=,@{pixels=$boxPixels;label='Cardboard Box'}
$mirror=New-Object 'UnityEngine.Color[]' ($w*$h)
foreach($y in 0..($h-1)){foreach($x in 0..($w-1)){$mirror[$y*$w+$x]=$boxPixels[$y*$w+$w-1-$x]}}
$images+=,@{pixels=$mirror;label='Worn / reversed'}
$bitmap=[Drawing.Bitmap]::new(840,480);$g=[Drawing.Graphics]::FromImage($bitmap)
$g.Clear([Drawing.Color]::FromArgb(24,31,43));$font=[Drawing.Font]::new('Arial',13)
foreach($index in 0..5) {
    $col=[int][Math]::Floor($index/2);$row=$index%2
    $entry=$images[$index];$iw=if($index -ge 4){$w}else{16};$ih=if($index -ge 4){$h}else{16}
    $scale=if($index -ge 4){8}else{10}
    $ox=$col*280+(280-$iw*$scale)/2;$oy=$row*240+16
    foreach($y in 0..($ih-1)){foreach($x in 0..($iw-1)){
        $c=$entry.pixels[($ih-1-$y)*$iw+$x];if($c.a -eq 0){continue}
        $brush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(255,[int]($c.r*255),[int]($c.g*255),[int]($c.b*255)))
        $g.FillRectangle($brush,($ox+$x*$scale),($oy+$y*$scale),$scale,$scale);$brush.Dispose()
    }}
    $g.DrawString($entry.label,$font,[Drawing.Brushes]::White,($col*280+50),($oy+184))
}
$path=Join-Path $root 'Docs/BombAndBox-preview.png'
$bitmap.Save($path,[Drawing.Imaging.ImageFormat]::Png);$font.Dispose();$g.Dispose();$bitmap.Dispose()
Write-Output "PASS: $count pixel comparisons, silhouettes, Point filtering, pickup/placed and pickup/worn consistency. Preview: $path"

# Assembly materials must read as parts of the current super bomb.
$shell=New-Object PurpleBombShellPickupItemVisual
$fuseMaterial=New-Object FusePickupItemVisual
$shellPixels=$shell.Render('purple_bomb_shell');$fusePixels=$fuseMaterial.Render('fuse')
foreach($material in @($shell,$fuseMaterial)) {if($material.Width -ne 16 -or $material.Height -ne 16){throw 'Material grid must be 16x16'}}
[PeopleVisualBase]::CheckConnected($shellPixels);[PeopleVisualBase]::CheckConnected($fusePixels)
foreach($row in 3..12){foreach($x in 0..15){
    $a=$shellPixels[(15-$row)*16+$x];$b=$images[2].pixels[(13-$row)*16+$x]
    foreach($channel in @('r','g','b','a')) {if([Math]::Abs($a.$channel-$b.$channel) -gt .0001){throw 'Shell must retain super-bomb casing and belt'}}
}}
foreach($i in 0..31){if($shellPixels[(15-[int][Math]::Floor($i/16))*16+$i%16].a -ne 0){throw 'Shell must not include installed fuse'}}
$materials=@(@{pixels=$shellPixels;label='Super Bomb Shell'},@{pixels=$fusePixels;label='Fuse'},@{pixels=$images[2].pixels;label='Assembled Super Bomb'})
$bitmap=[Drawing.Bitmap]::new(840,250);$g=[Drawing.Graphics]::FromImage($bitmap)
$g.Clear([Drawing.Color]::FromArgb(24,31,43));$font=[Drawing.Font]::new('Arial',12)
foreach($index in 0..2){
    $entry=$materials[$index];$ox=$index*280+60
    foreach($y in 0..15){foreach($x in 0..15){
        $c=$entry.pixels[(15-$y)*16+$x];if($c.a -eq 0){continue}
        $brush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(255,[int]($c.r*255),[int]($c.g*255),[int]($c.b*255)))
        $g.FillRectangle($brush,($ox+$x*10),(20+$y*10),10,10);$brush.Dispose()
    }}
    $g.DrawString($entry.label,$font,[Drawing.Brushes]::White,($index*280+45),210)
}
$path=Join-Path $root 'Docs/BombMaterials-preview.png'
$bitmap.Save($path,[Drawing.Imaging.ImageFormat]::Png);$font.Dispose();$g.Dispose();$bitmap.Dispose()
Write-Output "PASS: connected 16x16 shell/fuse, shell matches super-bomb casing pixel-for-pixel. Preview: $path"
