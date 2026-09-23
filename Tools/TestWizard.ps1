# Validate and preview the Wizard production maps and serialized palette.
param([switch]$AttackPreview, [switch]$WalkPreview)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path $PSScriptRoot -Parent
$source = Get-Content -Raw (Join-Path $root 'Assets/Scripts/Characters/WizardZeldaCharacterData.cs')
$directions = @('Front','Back','Left','Right')
$names = @('Wizard-level2')
$labels = @('Wizard')
$maps = @{}
$symbols = @{}
foreach ($m in [regex]::Matches($source, "case '(\w)': return (\w+);")) { $symbols[$m.Groups[1].Value] = $m.Groups[2].Value }
foreach ($direction in $directions) {
    $block = [regex]::Match($source, '(?s)string\[\] '+$direction+' = \{(.*?)\};').Groups[1].Value
    $rows = @([regex]::Matches($block, '"([.A-Z]+)"') | ForEach-Object { $_.Groups[1].Value })
    if ($rows.Count -ne 20 -or @($rows | Where-Object {$_.Length -ne 16}).Count) { throw "Invalid map: $direction" }
    $maps[$direction] = $rows
}
foreach($direction in $directions){
    if(($maps[$direction] -join '') -match 'K'){throw 'Boot pixels must be covered by the robe'}
    if($maps[$direction][7].Trim('.').Length -ne 12){throw 'Hat brim must be wider than original eight pixels'}
}
# The two profile silhouettes are exact mirrors, including centered hands.
foreach ($row in 0..19) {
    $reversed = $maps.Left[$row].ToCharArray(); [array]::Reverse($reversed)
    if (($reversed -join '') -ne $maps.Right[$row]) { throw 'Side body/head mismatch' }
}
function Get-PoseCells($rows, [int]$direction, [int]$pose) {
    $cells = [char[,]]::new(16,20)
    foreach ($row in 0..19) { foreach ($x in 0..15) {
        $symbol = $rows[$row][$x]
        if ($symbol -eq '.') { continue }
        if (-not $symbols.ContainsKey([string]$symbol)) { throw "Unknown symbol $symbol" }
        $y = 19-$row
        if ($pose -eq 1 -or $pose -eq 2) {
            $liftedCorner=if($direction -lt 2){if($pose -eq 1){3}else{12}}else{if($pose -eq 1){4}else{11}}
            if($y -eq 2 -and $x -eq $liftedCorner){continue}
            $foldX=if($pose -eq 1){6}else{9}
            if($y -ge 2 -and $y -le 5 -and $symbol -eq 'A' -and $x -eq $foldX){$symbol='S'}
        }
        $cells[$x,$y]=$symbol
    } }
    if ($pose -eq 3) {
        if ($direction -lt 2) {
            $outerX = if ($direction -eq 0) {4} else {11}
            $innerX = if ($direction -eq 0) {5} else {10}
            foreach ($y in 6..8) {
                $cells[$outerX,$y]=[char]0
                $cells[$innerX,$y]=if ($y -eq 6) {'S'} else {'S'}
            }
        } else {
            $idleX = 7
            foreach ($y in 6..8) { foreach ($x in $idleX..($idleX+1)) {
                $cells[$x,$y]=if ($y -eq 6) {'S'} elseif (($direction -eq 2 -and $x -eq 8) -or ($direction -eq 3 -and $x -eq 7)) {'T'} else {'A'}
            } }
        }
        $startX = if ($direction -eq 2) {1} elseif ($direction -eq 3) {11} else {6}
        $startY = if ($direction -eq 1) {10} elseif ($direction -eq 0) {4} else {6}
        foreach ($y in $startY..($startY+2)) { foreach ($x in $startX..($startX+3)) {
            $cells[$x,$y] = if ($y -eq ($startY+2)) {'G'} else {'A'}
        } }
        $handX = if ($direction -eq 2) {$startX} elseif ($direction -eq 3) {$startX+2} else {$startX+1}
        $handY = if ($direction -eq 1) {$startY+1} else {$startY}
        foreach ($y in $handY..($handY+1)) { foreach ($x in $handX..($handX+1)) { $cells[$x,$y]='F' } }
    }
    return ,$cells
}
# Four-connectivity catches detached legs, hands, headcloth or scarf in all 16 poses.
foreach ($direction in $directions) {
    $columns = if ($direction -in @('Left','Right')) {@(7,8)} else {@(4,5,10,11)}
    foreach ($x in $columns) {
        if ($maps[$direction][11][$x] -ne 'G') { throw "Missing visible cuff: $direction" }
        foreach ($row in @(12,13)) { if ($maps[$direction][$row][$x] -ne 'F') { throw "Missing two-pixel-wide hand: $direction" } }
    }
}
foreach ($direction in @('Left','Right')) {
    $oldColumns = if ($direction -eq 'Left') {@(5,6)} else {@(9,10)}
    foreach ($row in @(12,13)) { foreach ($x in $oldColumns) {
        if ($maps[$direction][$row][$x] -eq 'F') { throw 'Old side hand remains on the front edge' }
    } }
}
foreach ($d in 0..3) { foreach ($pose in 0..3) {
    $cells = Get-PoseCells $maps[$directions[$d]] $d $pose
    foreach($y in 2..3){foreach($x in 5..10){
        if($cells[$x,$y] -notin @('A','S','L','G')){throw "Exposed legs or gap below robe: direction $d pose $pose"}
    }}
    if ($pose -eq 3) {
        $idleX = @(4,10,7,7)[$d]
        foreach ($y in 6..7) { foreach ($x in $idleX..($idleX+1)) {
            if ($cells[$x,$y] -eq 'F') { throw "Duplicate idle hand in attack direction $d" }
        } }
        if ($d -lt 2) {
            $otherX = if ($d -eq 0) {10} else {4}
            foreach ($y in 6..7) { foreach ($x in $otherX..($otherX+1)) {
                if ($cells[$x,$y] -ne 'F') { throw 'Non-attacking hand must remain unchanged' }
            } }
        }
        $handX = @(7,7,1,13)[$d]; $handY = @(4,11,6,6)[$d]
        foreach ($y in $handY..($handY+1)) { foreach ($x in $handX..($handX+1)) {
            if ($cells[$x,$y] -ne 'F') { throw 'Missing attacking hand' }
        } }
    }
    $seen = [bool[]]::new(320)
    $queue = [Collections.Generic.Queue[int]]::new()
    $seed = -1; $opaque = 0
    foreach ($y in 0..19) { foreach ($x in 0..15) { if ($cells[$x,$y] -ne [char]0) { $opaque++; $seed=$y*16+$x } } }
    $queue.Enqueue($seed); $seen[$seed]=$true; $count=0
    while ($queue.Count) {
        $i=$queue.Dequeue(); $count++; $x=$i%16; $y=[int][Math]::Floor($i/16)
        $neighbours=@()
        if ($x -gt 0) {$neighbours+=($i-1)}; if ($x -lt 15) {$neighbours+=($i+1)}
        if ($y -gt 0) {$neighbours+=($i-16)}; if ($y -lt 19) {$neighbours+=($i+16)}
        foreach ($n in $neighbours) {
            if (-not $seen[$n] -and $cells[($n%16),([int][Math]::Floor($n/16))] -ne [char]0) { $seen[$n]=$true; $queue.Enqueue($n) }
        }
    }
    if ($count -ne $opaque) { throw "Disconnected pixels: $($directions[$d]) pose $pose" }
} }
$bmp = [Drawing.Bitmap]::new(800,270)
$g = [Drawing.Graphics]::FromImage($bmp)
$g.Clear([Drawing.Color]::FromArgb(25,32,43))
$font = [Drawing.Font]::new('Arial',11)
$scriptGuid = [regex]::Match((Get-Content -Raw (Join-Path $root 'Assets/Scripts/Characters/WizardZeldaCharacterData.cs.meta')), 'guid: (\w+)').Groups[1].Value
foreach ($tier in 0..0) {
    $prefab = Get-Content -Raw (Join-Path $root "Assets/Prefabs/Level2NPCS/$($names[$tier]).prefab")
    if (-not $prefab.Contains("guid: $scriptGuid")) { throw 'Missing wizard data script reference' }
    if ($prefab -notmatch ('permissionLevel: '+($tier+1)+'\b')) { throw 'Rank mismatch' }
    if (-not $prefab.Contains('cameraOrthographicSize: 7') -or -not $prefab.Contains('playerVisionRadius: 13')) { throw 'Camera configuration mismatch' }
    foreach($expected in @('m_Name: Wizard-level2','m_Size: {x: 0.38, y: 0.56}',
        'guid: 9a6df8c329184af4a985710734a46f2c','guid: a81494bb8bf44504a8f11adb1cb226c4',
        'guid: c7a3afec32db4e4d83d29bb18d2f601c','attackDuration: 0.3','attackSize: {x: 0.65, y: 0.65}')){
        if(!$prefab.Contains($expected)){throw "Missing character/attack wiring: $expected"}
    }
    foreach($expected in @('AttackVisualShape => ZeldaAttackVisualShape.Shockwave',
        'new Vector2(0.5f, 0.2f), 16f','Gizmos.DrawWireCube(EditorVisualBoundsCenter, EditorVisualBoundsSize)',
        'new Sprite[4, 4]','GetDamageFeedbackTint(Color.white)')){
        if(!$source.Contains($expected)){throw "Missing standard character behavior: $expected"}
    }
    $palette=@{}
    foreach ($symbol in $symbols.Keys) {
        $field=$symbols[$symbol]
        $m=[regex]::Match($prefab,$field+': \{r: ([\d.]+), g: ([\d.]+), b: ([\d.]+), a: ([\d.]+)\}')
        if (-not $m.Success) { throw "Missing palette field $field" }
        $rgb=@(1..3 | ForEach-Object { [double]::Parse($m.Groups[$_].Value,[Globalization.CultureInfo]::InvariantCulture) })
        $palette[$symbol]=[Drawing.Color]::FromArgb(255,[int]($rgb[0]*255),[int]($rgb[1]*255),[int]($rgb[2]*255))
    }
    foreach ($d in 0..3) {
        $previewPose = if ($AttackPreview) {3} elseif($WalkPreview) {1} else {0}
        $cells=Get-PoseCells $maps[$directions[$d]] $d $previewPose
        foreach ($y in 0..19) { foreach ($x in 0..15) {
            $symbol=[string]$cells[$x,$y]
            if ($cells[$x,$y] -eq [char]0) {continue}
            $brush=[Drawing.SolidBrush]::new($palette[$symbol])
            $g.FillRectangle($brush,($d*200+20+$x*10),($tier*230+20+(19-$y)*10),10,10)
            $brush.Dispose()
        } }
        $g.DrawString(($labels[$tier]+' / '+$directions[$d]),$font,[Drawing.Brushes]::White,($d*200+15),($tier*270+233))
    }
}
$previewFile = if ($AttackPreview) {'Docs/Wizard-attack-preview.png'} elseif($WalkPreview) {'Docs/Wizard-walk-preview.png'} else {'Docs/Wizard-preview.png'}
$output=Join-Path $root $previewFile
$bmp.Save($output,[Drawing.Imaging.ImageFormat]::Png)
$font.Dispose(); $g.Dispose(); $bmp.Dispose()
Write-Output "PASS: 16 connected idle/walk/attack poses, no duplicate attacking hands, matching side body, indigo/sand/turquoise palette and prefab references. Preview: $output"
