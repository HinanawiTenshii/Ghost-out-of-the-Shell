$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$source=Get-Content -Raw "$root/Assets/Scripts/Characters/ChefZeldaCharacterData.cs"
$maps=[regex]::Matches($source,'(?s)    private static readonly string\[\] (Front|Back|Left|Right) = \{.*?\};')
if($maps.Count -ne 4){throw 'Missing directional maps'}
foreach($m in $maps){
    $r=@([regex]::Matches($m.Value,'"([.A-Z]+)"')|ForEach-Object{$_.Groups[1].Value})
    if($r.Count -ne 20 -or @($r|Where-Object{$_.Length -ne 16}).Count){throw 'Incorrect canvas dimensions'}
}
$start=$source.IndexOf('    private static char GetPoseSymbol(')
$end=$source.IndexOf('    private Color PixelColor',$start)
$method=$source.Substring($start,$end-$start).Replace('private static char','public static char')
Add-Type -TypeDefinition ('public static class ChefPixels {'+($maps.Value -join [Environment]::NewLine)+$method+'}')
foreach($d in 0..3){foreach($p in 0..3){
    $occupied=[Collections.Generic.HashSet[int]]::new()
    foreach($row in 0..19){foreach($x in 0..15){
        $c=[ChefPixels]::GetPoseSymbol($d,$p,$x,$row)
        if($c -ne '.'){[void]$occupied.Add($row*16+$x)}
        if($row -gt 16 -and $c -ne '.'){throw 'Feet extend below civilian baseline'}
    }}
    $visited=[Collections.Generic.HashSet[int]]::new()
    $queue=[Collections.Generic.Queue[int]]::new();$queue.Enqueue(@($occupied)[0])
    while($queue.Count){
        $i=$queue.Dequeue();if(!$occupied.Contains($i) -or !$visited.Add($i)){continue}
        $x=$i%16
        if($x -gt 0){$queue.Enqueue($i-1)};if($x -lt 15){$queue.Enqueue($i+1)}
        $queue.Enqueue($i-16);$queue.Enqueue($i+16)
    }
    if($visited.Count -ne $occupied.Count){throw "Detached pixels: direction $d pose $p"}
    $handX=@(10,4,7,7)[$d]
    $hand=[ChefPixels]::GetPoseSymbol($d,$p,$handX,12)
    if($p -eq 3 -and $hand -eq 'F'){throw 'Duplicate attacking hand'}
    if($p -ne 3 -and $hand -ne 'F'){throw 'Missing resting hand'}
}}
$prefab=Get-Content -Raw "$root/Assets/Prefabs/Level2NPCS/Chef-level2.prefab"
foreach($part in @('7ff9e487d46f4c509f0b179541f6b578','a81494bb8bf44504a8f11adb1cb226c4','c7a3afec32db4e4d83d29bb18d2f601c','m_Size: {x: 0.34, y: 0.54}')){
    if(!$prefab.Contains($part)){throw "Missing prefab component/reference: $part"}
}
if($prefab -match 'wearHeadcloth|headclothColor|trousersColor'){throw 'Stale civilian-only fields'}
if(!$source.Contains('new Vector2(0.5f, 0.25f), 16f')){throw 'Unexpected civilian foot alignment'}

Add-Type -AssemblyName System.Drawing
$bmp=[Drawing.Bitmap]::new(800,970);$g=[Drawing.Graphics]::FromImage($bmp)
$g.Clear([Drawing.Color]::FromArgb(25,32,43));$font=[Drawing.Font]::new('Arial',11)
$palette=@{C=@(199,214,212);S=@(141,157,159);W=@(240,242,227);A=@(232,232,204);R=@(199,38,31);F=@(194,133,87);H=@(51,33,23);E=@(13,26,36);K=@(56,33,18)}
$dirs=@('Front','Back','Left','Right');$poses=@('Idle','Walk 1','Walk 2','Attack')
foreach($p in 0..3){foreach($d in 0..3){
    foreach($row in 0..19){foreach($x in 0..15){
        $c=[string][ChefPixels]::GetPoseSymbol($d,$p,$x,$row);if($c -eq '.'){continue}
        $rgb=$palette[$c];if(!$rgb){throw "Unknown pixel $c"}
        $brush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb($rgb[0],$rgb[1],$rgb[2]))
        $g.FillRectangle($brush,20+$d*200+$x*10,10+$p*240+$row*10,10,10);$brush.Dispose()
    }}
    $g.DrawString(($dirs[$d]+' / '+$poses[$p]),$font,[Drawing.Brushes]::LightSteelBlue,40+$d*200,213+$p*240)
}}
$path="$root/Docs/Chef-level2-preview.png"
$bmp.Save($path,[Drawing.Imaging.ImageFormat]::Png);$g.Dispose();$bmp.Dispose();$font.Dispose()
Write-Output 'PASS: 16 production poses; connected limbs; short legs; centered side hands; no duplicate attacking hand; prefab wiring.'
Write-Output $path
