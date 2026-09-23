$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$src=Get-Content -Raw "$root/Assets/Scripts/Characters/AutomatonZeldaCharacterData.cs"
$maps=[regex]::Matches($src,'(?s)    private static readonly string\[\] (Front|Back|Left|Right) = \{.*?\};')
if($maps.Count -ne 4){throw 'Missing directional maps'}
foreach($m in $maps){
    $rows=@([regex]::Matches($m.Value,'"([.A-Z]+)"')|ForEach-Object{$_.Groups[1].Value})
    if($rows.Count -ne 20 -or @($rows|Where-Object{$_.Length -ne 16}).Count){throw 'Incorrect sprite dimensions'}
}
$start=$src.IndexOf('    private static char GetPoseSymbol(')
$end=$src.IndexOf('    private Color PixelColor', $start)
$method=$src.Substring($start,$end-$start).Replace('private static char','public static char')
Add-Type -TypeDefinition ('public static class AutomatonPixels {'+($maps.Value -join [Environment]::NewLine)+$method+'}')
$prefab=Get-Content -Raw "$root/Assets/Prefabs/Level2NPCS/Automaton-level2.prefab"
$paladin=Get-Content -Raw "$root/Assets/Prefabs/Level2NPCS/Paladin.prefab"
$automatonScale=[double][regex]::Match($prefab,'m_LocalScale: \{x: ([^,]+)').Groups[1].Value
$paladinScale=[double][regex]::Match($paladin,'m_LocalScale: \{x: ([^,]+)').Groups[1].Value
if([Math]::Abs($automatonScale-.85) -gt .0001){throw 'Unexpected automaton scale'}
$paladinSource=Get-Content -Raw "$root/Assets/Scripts/Characters/PaladinZeldaCharacterData.cs"
$paladinFront=[regex]::Match($paladinSource,'(?s)string\[\] Front = \{(.*?)\};').Groups[1].Value
$paladinRows=@([regex]::Matches($paladinFront,'"([.A-Z]+)"')|ForEach-Object{$_.Groups[1].Value})
$paladinHeight=@($paladinRows|Where-Object{$_.Trim('.').Length -gt 0}).Count/16.0*$paladinScale
$automatonHeight=16.0/16.0*$automatonScale
if($automatonHeight -ge $paladinHeight){throw 'Automaton must be shorter than Paladin'}
foreach($d in 0..3){foreach($p in 0..2){
    foreach($row in 11..15){
        $bladePixels=@(0..15|Where-Object{[AutomatonPixels]::GetPoseSymbol($d,$p,$_,$row) -in @('B','C')}).Count
        $expected=if($d -ge 2){2}else{4}
        if($bladePixels -ne $expected){throw "Incorrect visible blade count: direction $d pose $p row $row"}
    }
}}
$palette=@{'.'=@(24,32,43);D=@(46,54,56);M=@(117,128,128);H=@(173,184,186);R=@(209,9,6);S=@(209,9,6);Y=@(255,191,31);B=@(219,230,232);C=@(117,128,128)}
Add-Type -AssemblyName System.Drawing
$bmp=[Drawing.Bitmap]::new(680,840);$g=[Drawing.Graphics]::FromImage($bmp)
$g.Clear([Drawing.Color]::FromArgb(24,32,43))
$font=[Drawing.Font]::new('Arial',11)
foreach($d in 0..3){foreach($p in 0..3){
    $occupied=[Collections.Generic.HashSet[int]]::new()
    foreach($row in 0..19){foreach($x in 0..15){
        $symbol=[AutomatonPixels]::GetPoseSymbol($d,$p,$x,$row)
        if(!$palette.ContainsKey([string]$symbol)){throw "Unknown pixel $symbol"}
        if($symbol -ne '.'){[void]$occupied.Add($row*16+$x)}
        if($p -eq 3 -and $symbol -in @('B','C','S')){throw 'Duplicate idle blade during attack'}
        $rgb=$palette[[string]$symbol];$b=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb($rgb[0],$rgb[1],$rgb[2]))
        $g.FillRectangle($b,10+$d*170+$x*9,10+$p*205+$row*9,9,9);$b.Dispose()
    }}
    $visited=[Collections.Generic.HashSet[int]]::new()
    $queue=[Collections.Generic.Queue[int]]::new();$queue.Enqueue(@($occupied)[0])
    while($queue.Count){
        $i=$queue.Dequeue();if(!$occupied.Contains($i) -or !$visited.Add($i)){continue}
        $x=$i%16
        if($x -gt 0){$queue.Enqueue($i-1)};if($x -lt 15){$queue.Enqueue($i+1)}
        $queue.Enqueue($i-16);$queue.Enqueue($i+16)
    }
    if($visited.Count -ne $occupied.Count){throw "Detached pixels in direction $d pose $p"}
    $g.DrawString((@('Front','Back','Left','Right')[$d]+' / '+@('Idle','Walk 1','Walk 2','Attack')[$p]),$font,[Drawing.Brushes]::LightSteelBlue,10+$d*170,193+$p*205)
}}
$path="$root/Docs/Automaton-level2-preview.png"
$bmp.Save($path,[Drawing.Imaging.ImageFormat]::Png);$g.Dispose();$bmp.Dispose();$font.Dispose()
Write-Output 'PASS: 4 directions x 4 poses, connected limbs/head, single profile blade, paired front/back blades, no duplicate idle blades during attacks; root scale 0.85 and visible height below Paladin.'
Write-Output $path
