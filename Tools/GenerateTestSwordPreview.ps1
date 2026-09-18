param([string]$ProjectRoot = (Split-Path $PSScriptRoot -Parent))
Add-Type -AssemblyName System.Drawing
$source = Get-Content -Raw -LiteralPath (Join-Path $ProjectRoot 'Assets/Prefabs/TestNPCS/TestSwordZeldaCharacterData.cs')
$maps = @{}
foreach ($name in @('Front','Back','Left')) {
    $block = [regex]::Match($source, "string\[\] $name = \{([\s\S]*?)\};").Groups[1].Value
    $maps[$name] = @([regex]::Matches($block, '"([.LASCRGBE]+)"') | ForEach-Object { $_.Groups[1].Value })
    if ($maps[$name].Count -ne 16 -or @($maps[$name] | Where-Object { $_.Length -ne 16 }).Count) { throw "Invalid map $name" }
}
$palette = @{L='181,211,216';A='109,137,150';S='57,77,91';C='34,54,67';R='177,58,58';G='210,168,90';B='71,53,44';E='13,26,36'}
foreach ($key in @($palette.Keys)) { $rgb=$palette[$key].Split(','); $palette[$key]=[Drawing.Color]::FromArgb(255,[int]$rgb[0],[int]$rgb[1],[int]$rgb[2]) }
function Pixel($bmp, [int]$x, [int]$y, $color) { if ($x -ge 0 -and $x -lt 16 -and $y -ge 0 -and $y -lt 20) { $bmp.SetPixel($x,19-$y,$color) } }
function Box($bmp,[int]$x,[int]$y,[int]$w,[int]$h,$color) { for($j=$y;$j -lt $y+$h;$j++){for($i=$x;$i -lt $x+$w;$i++){Pixel $bmp $i $j $color}} }
$sheet = [Drawing.Bitmap]::new(640,1080)
$g=[Drawing.Graphics]::FromImage($sheet); $g.Clear([Drawing.Color]::FromArgb(18,25,33))
$font=[Drawing.Font]::new('Arial',12)
for($d=0;$d -lt 4;$d++) { for($f=0;$f -lt 6;$f++) {
    $bmp=[Drawing.Bitmap]::new(16,20)
    $map = if($d -eq 0){$maps.Front}elseif($d -eq 1){$maps.Back}else{$maps.Left}
    $stride=if($f -eq 1){-1}elseif($f -eq 3){1}else{0}; $bob=if($f -eq 2 -or $f -eq 4){1}else{0}
    for($row=0;$row -lt 16;$row++){for($x=0;$x -lt 16;$x++){
        $key=[string]$map[$row][$x]; if($key -eq '.'){continue}
        $arm=0; if($row -ge 9 -and $row -le 11 -and ($x -eq 3 -or $x -eq 12)){$arm=if($x -lt 8){$stride}else{-$stride}}
        $px=if($d -eq 3){15-$x}else{$x}; Pixel $bmp $px (19-$row+$bob+$arm) $palette[$key]
    }}
    if($d -lt 2){
        Box $bmp 5 (2+[Math]::Max(0,$stride)) 2 (3+$bob) $palette.C; Box $bmp 9 (2+[Math]::Max(0,-$stride)) 2 (3+$bob) $palette.S
        Box $bmp 4 (1+[Math]::Max(0,$stride)) 3 1 $palette.S; Box $bmp 9 (1+[Math]::Max(0,-$stride)) 3 1 $palette.C
    }else{
        Box $bmp (6+$stride) 2 2 (3+$bob) $palette.S; Box $bmp (8-$stride) 2 2 (3+$bob) $palette.C
        Box $bmp (5+$stride) 1 3 1 $palette.S; Box $bmp (7-$stride) 1 3 1 $palette.C
    }
    if($f -eq 5){if($d -eq 0){Box $bmp 6 6 4 2 $palette.L;Box $bmp 7 5 2 1 $palette.G}elseif($d -eq 1){Box $bmp 6 13 4 2 $palette.L}else{$px=if($d -eq 2){1}else{10};Box $bmp $px 8 5 2 $palette.L}}
    if($d -eq 0 -and $f -eq 0){$bmp.Save((Join-Path $ProjectRoot 'Assets/Prefabs/TestNPCS/TestSwordGuy-Front.png'),[Drawing.Imaging.ImageFormat]::Png)}
    $g.DrawString((@('FRONT','BACK','LEFT','RIGHT')[$d] + ' / ' + $f),$font,[Drawing.Brushes]::LightGray,($d*160),($f*180))
    for($y=0;$y -lt 20;$y++){for($x=0;$x -lt 16;$x++){$c=$bmp.GetPixel($x,$y);if($c.A -gt 0){$b=[Drawing.SolidBrush]::new($c);$g.FillRectangle($b,($d*160+16+$x*8),($f*180+20+$y*8),8,8);$b.Dispose()}}}
    $bmp.Dispose()
}}
$sheet.Save((Join-Path $ProjectRoot 'Docs/TestSwordGuy-Preview.png'),[Drawing.Imaging.ImageFormat]::Png)
$font.Dispose();$g.Dispose();$sheet.Dispose()
