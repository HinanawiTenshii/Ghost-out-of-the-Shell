$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$source=Get-Content -Raw "$root/Assets/Scripts/Characters/HeavyPaladinZeldaCharacterData.cs"
$maps=[regex]::Matches($source,'(?s)    private static readonly string\[\] (Front|Back|Left|Right) = \{.*?\};')
if($maps.Count -ne 4){throw 'Missing directional maps'}
$rows=@{}
foreach($m in $maps){
    $r=@([regex]::Matches($m.Value,'"([.A-Z]+)"')|ForEach-Object{$_.Groups[1].Value})
    if($r.Count -ne 20 -or @($r|Where-Object{$_.Length -ne 20}).Count){throw 'Incorrect canvas dimensions'}
    $rows[$m.Groups[1].Value]=$r
}
$start=$source.IndexOf('    private static char GetPoseSymbol(')
$end=$source.IndexOf('    private Color PixelColor',$start)
$method=$source.Substring($start,$end-$start).Replace('private static char','public static char')
Add-Type -TypeDefinition ('public static class HeavyPaladinPixels {'+($maps.Value -join [Environment]::NewLine)+$method+'}')
function AssertConnected($cells,[int]$width){
    $occupied=[Collections.Generic.HashSet[int]]::new()
    for($i=0;$i -lt $cells.Count;$i++){if($cells[$i] -ne '.'){[void]$occupied.Add($i)}}
    $visited=[Collections.Generic.HashSet[int]]::new()
    $queue=[Collections.Generic.Queue[int]]::new();$queue.Enqueue(@($occupied)[0])
    while($queue.Count){
        $i=$queue.Dequeue();if(!$occupied.Contains($i) -or !$visited.Add($i)){continue}
        $x=$i%$width
        if($x -gt 0){$queue.Enqueue($i-1)};if($x -lt $width-1){$queue.Enqueue($i+1)}
        $queue.Enqueue($i-$width);$queue.Enqueue($i+$width)
    }
    if($visited.Count -ne $occupied.Count){throw 'Detached head, limb or weapon pixels'}
}
$dirs=@('Front','Back','Left','Right')
foreach($d in 0..3){foreach($p in 0..3){
    $cells=@()
    foreach($row in 0..19){foreach($x in 0..19){
        $c=[HeavyPaladinPixels]::GetPoseSymbol($d,$p,$x,$row);$cells+=$c
        if($rows[$dirs[$d]][$row][$x] -in @('C','D') -and $c -ne $rows[$dirs[$d]][$row][$x]){throw 'Hand painted over cape'}
        if($row -ge 16 -and $c -ne '.'){throw 'Legs extend below short baseline'}
    }}
    AssertConnected $cells 20
    $handX=@(15,4,9,10)[$d];$handRow=@(10,10,12,10)[$d]
    if($p -ne 3 -and [HeavyPaladinPixels]::GetPoseSymbol($d,$p,$handX,$handRow) -ne 'G'){throw 'Missing resting gauntlet'}
    if($p -eq 3 -and [HeavyPaladinPixels]::GetPoseSymbol($d,$p,$handX,$handRow) -eq 'G'){throw 'Duplicate resting hand while attacking'}
}}
if($rows.Front[3].Substring(8,4) -ne 'VVVV' -or $rows.Front[4].Substring(9,2) -ne 'VV'){throw 'Missing T-shaped visor'}
$strong=Get-Content -Raw "$root/Assets/Scripts/Characters/DesertStrongmanZeldaCharacterData.cs"
$strongRows=@([regex]::Matches([regex]::Match($strong,'(?s)string\[\] Front = \{(.*?)\};').Groups[1].Value,'"([.A-Z]+)"')|ForEach-Object{$_.Groups[1].Value})
function SilhouetteSize($r){
    $xs=@();$ys=@()
    foreach($y in 0..($r.Count-1)){foreach($x in 0..($r[$y].Length-1)){if($r[$y][$x] -ne '.'){$xs+=$x;$ys+=$y}}}
    return @((($xs|Measure-Object -Maximum).Maximum-($xs|Measure-Object -Minimum).Minimum+1),
        (($ys|Measure-Object -Maximum).Maximum-($ys|Measure-Object -Minimum).Minimum+1))
}
$heavySize=SilhouetteSize $rows.Front;$strongSize=SilhouetteSize $strongRows
if(($heavySize -join ',') -ne ($strongSize -join ',')){throw 'Front silhouette must match Strongman footprint'}
$prefab=Get-Content -Raw "$root/Assets/Prefabs/Level2NPCS/HeavyPaladin-level2.prefab"
if(!$prefab.Contains('m_LocalScale: {x: 1.1, y: 1.1, z: 1}')){throw 'Incorrect Strongman-matched overall scale'}
$hitbox=Get-Content -Raw "$root/Assets/Scripts/Characters/ZeldaAttackHitbox.cs"
$axe=@([regex]::Matches([regex]::Match($hitbox,'(?s)string\[\] BattleAxePixels = \{(.*?)\};').Groups[1].Value,'"([.A-Z]+)"')|ForEach-Object{$_.Groups[1].Value})
if($axe.Count -ne 16 -or @($axe|Where-Object{$_.Length -ne 16}).Count){throw 'Incorrect axe dimensions'}
foreach($r in $axe){$reverse=$r.ToCharArray();[array]::Reverse($reverse);if($r -ne ($reverse -join '')){throw 'Axe heads are not symmetric'}}
AssertConnected ($axe -join '').ToCharArray() 16
if(!$source.Contains('ZeldaAttackVisualShape.BattleAxe')){throw 'Axe visual not wired'}

Add-Type -AssemblyName System.Drawing
$bmp=[Drawing.Bitmap]::new(1100,285);$g=[Drawing.Graphics]::FromImage($bmp)
$g.Clear([Drawing.Color]::FromArgb(25,32,43));$font=[Drawing.Font]::new('Arial',12)
$palette=@{A=@(59,138,176);S=@(33,84,122);H=@(122,161,176);C=@(26,66,148);D=@(18,41,89);V=@(5,15,26);G=@(138,172,185)}
foreach($d in 0..3){
    foreach($row in 0..19){foreach($x in 0..19){
        $c=[string][HeavyPaladinPixels]::GetPoseSymbol($d,0,$x,$row);if($c -eq '.'){continue}
        $rgb=$palette[$c];$brush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb($rgb[0],$rgb[1],$rgb[2]))
        $g.FillRectangle($brush,10+$d*210+$x*10,20+$row*10,10,10);$brush.Dispose()
    }}
    $g.DrawString($dirs[$d],$font,[Drawing.Brushes]::LightSteelBlue,75+$d*210,243)
}
$axePalette=@{H=@(245,245,245);M=@(204,204,204);G=@(163,163,163);W=@(89,56,33)}
foreach($row in 0..15){foreach($x in 0..15){
    $c=[string]$axe[$row][$x];if($c -eq '.'){continue};$rgb=$axePalette[$c]
    $brush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb($rgb[0],$rgb[1],$rgb[2]))
    $g.FillRectangle($brush,890+$x*10,30+$row*10,10,10);$brush.Dispose()
}}
$g.DrawString('Double-Headed Axe',$font,[Drawing.Brushes]::LightSteelBlue,890,243)
$path="$root/Docs/HeavyPaladin-level2-preview.png"
$bmp.Save($path,[Drawing.Imaging.ImageFormat]::Png);$g.Dispose();$bmp.Dispose();$font.Dispose()
Write-Output 'PASS: 16 production poses; connected limbs; cape occlusion; no duplicate attacking hand; Strongman footprint; symmetric connected battle axe.'
Write-Output $path
