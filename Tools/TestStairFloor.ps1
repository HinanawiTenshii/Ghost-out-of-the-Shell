$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$shader=Get-Content -Raw "$root/Assets/Shaders/StairFloor.shader"
$prefab=Get-Content -Raw "$root/Assets/Prefabs/Decorations/StairFloor.prefab"
$checks=0
function Check($ok,$label){if(!$ok){throw $label};$script:checks++}
Check ($shader.Contains('[Enum(Up,0,Down,1,Left,2,Right,3)]')) 'Four inspector directions'
Check ($shader.Contains('if (direction > 2.5) along = p.x;') -and $shader.Contains('else if (direction > 1.5) along = -p.x;') -and $shader.Contains('else if (direction > 0.5) along = -p.y;')) 'World direction mapping'
Check ($shader.Contains('_StairDirectionOverride >= 0.0 ? _StairDirectionOverride : _Direction')) 'Per-object override with material fallback'
Check ($shader.Contains('_StairDirectionOverride ("Per Object Ascent Direction", Float) = -1')) 'Existing stairs keep their authored direction'
Check ($shader.Contains('unity_ObjectToWorld') -and $shader.Contains('max(_StepDepth, 0.01)')) 'World spacing and safe zero depth'
Check ($shader.Contains('result.a *= tex2D(_MainTex, input.uv).a;')) 'Preserve sprite alpha'
Check ($shader.Contains('fwidth(grid)') -and $shader.Contains('smoothstep(0.3, 0.8, aa)')) 'Minification filter'
Check ([regex]::Matches($prefab,'(?m)^SpriteRenderer:').Count -eq 1) 'Only one floor renderer'
Check ($prefab -notmatch 'Collider|MonoBehaviour') 'Pure decoration without collision or per-frame script'
Check ($prefab.Contains('m_SortingOrder: -10')) 'Draw above existing floor and below props'
Check ($prefab.Contains('guid: c2cbd4b7aefe449586e1de91d630c9cc')) 'Prefab material link'
foreach($entry in @(@('StairFloor',0),@('StairFloor-Horizontal',3))){
    $mat=Get-Content -Raw "$root/Assets/Materials/$($entry[0]).mat"
    Check ($mat.Contains('guid: 8d88c060b75743c7a3b483d07e59c667')) 'Shader linked'
    Check ($mat -match '(?m)^\s*- _Direction: [0-3]\s*$') 'Material may use any user-authored ascent direction'
    Check ($mat.Contains('- _RiserWidth: 0.12') -and $mat.Contains('- _EdgeWidth: 0.045')) 'Subtle narrow stone edges'
    foreach($property in @('_TreadColor','_RiserColor','_EdgeColor')){
        $m=[regex]::Match($mat,"- ${property}: \{r: ([\d.]+), g: ([\d.]+), b: ([\d.]+), a: 1\}")
        Check ($m.Success) 'Opaque stone palette'
        $r,$green,$b=@(1..3|ForEach-Object{[double]$m.Groups[$_].Value})
        Check ($b -gt $green -and $green -gt $r) 'Indigo rather than neutral grey palette'
        $default=[regex]::Match($shader,"${property}\s*\([^\r\n]*?=\s*\(([\d.]+),\s*([\d.]+),\s*([\d.]+),\s*1\)")
        Check ($default.Success) 'Shader palette default found'
        foreach($channel in 1..3){
            Check ([Math]::Abs([double]$default.Groups[$channel].Value-[double]$m.Groups[$channel].Value) -lt .000001) 'Material and shader defaults agree numerically'
        }
    }
}
. "$PSScriptRoot/TestSpriteWaterFlowShader.ps1"
$program=[regex]::Match($shader,'(?s)CGPROGRAM(.*?)ENDCG').Groups[1].Value
$program=[regex]::Replace($program,'(?m)^\s*#pragma.*$','')
$program=$program.Replace('#include "UnityCG.cginc"','float4x4 unity_ObjectToWorld; float4 UnityObjectToClipPos(float4 v) { return v; }').Replace('fixed4','float4')
[SpriteWaterFlowCompileTest]::Check($program,'vert','vs_4_0')
[SpriteWaterFlowCompileTest]::Check($program,'frag','ps_4_0')
# Same phase/color construction at near distance, without CRT or Unity post-processing.
function Phase($direction,$x,$y){
    $along=switch($direction){0{$y} 1{-$y} 2{-$x} 3{$x}}
    $grid=$along/.6
    return $grid-[Math]::Floor($grid)
}
foreach($x in @(-3.17,-.22,.14,2.45)){
    foreach($y in @(-1.28,.21,3.13)){
        Check ([Math]::Abs((Phase 0 $x $y)-(Phase 1 $x (-$y))) -lt .000001) 'Up/down mirror'
        Check ([Math]::Abs((Phase 2 $x $y)-(Phase 3 (-$x) $y)) -lt .000001) 'Left/right mirror'
        Check ([Math]::Abs((Phase 0 $x $y)-(Phase 3 $y $x)) -lt .000001) 'Axes swap'
        Check ([Math]::Abs((Phase 0 $x $y)-(Phase 0 $x ($y+.6))) -lt .000001) 'World-space repeat'
    }
}
Add-Type -AssemblyName System.Drawing
$bmp=[Drawing.Bitmap]::new(960,700);$g=[Drawing.Graphics]::FromImage($bmp)
$g.Clear([Drawing.Color]::FromArgb(25,32,43));$font=[Drawing.Font]::new('Segoe UI',13)
$mat=Get-Content -Raw "$root/Assets/Materials/StairFloor.mat"
function PaletteBrush($property){
    $m=[regex]::Match($mat,"- ${property}: \{r: ([\d.]+), g: ([\d.]+), b: ([\d.]+)")
    $rgb=@(1..3|ForEach-Object{[int]([double]$m.Groups[$_].Value*255)})
    return [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb($rgb[0],$rgb[1],$rgb[2]))
}
$tread=PaletteBrush '_TreadColor';$riser=PaletteBrush '_RiserColor';$edge=PaletteBrush '_EdgeColor'
$labels=@('Up','Down','Left','Right')
foreach($d in 0..3){
    $left=25+235*$d
    for($i=0;$i -lt 300;$i++){
        $x=($i+.5)/75;$y=(300-$i-.5)/75
        $phase=Phase $d $x $y
        $brush=if($phase -lt .12){$riser}elseif($phase -lt .165){$edge}else{$tread}
        if($d -lt 2){$g.FillRectangle($brush,$left,35+$i,200,1)}
        elseif($i -lt 200){$g.FillRectangle($brush,$left+$i,35,1,300)}
    }
    $g.DrawString($labels[$d],$font,[Drawing.Brushes]::White,$left+70,360)
}
# Context swatches matching the supplied checker, blue and indigo floors.
$floorColors=@([Drawing.Color]::FromArgb(27,31,60),[Drawing.Color]::FromArgb(8,45,120),[Drawing.Color]::FromArgb(43,53,115))
$tileBrush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(52,56,79))
foreach($context in 0..2){
    $left=25+315*$context
    $floorBrush=[Drawing.SolidBrush]::new($floorColors[$context])
    $g.FillRectangle($floorBrush,$left,440,280,190);$floorBrush.Dispose()
    if($context -eq 0){
        foreach($x in 0..6){foreach($y in 0..3){if(($x+$y)%2 -eq 0){$g.FillRectangle($tileBrush,$left+40*$x,440+40*$y,40,40)}}}
    }
    foreach($i in 0..109){
        $phase=Phase 0 0 ((110-$i-.5)/60)
        $brush=if($phase -lt .12){$riser}elseif($phase -lt .165){$edge}else{$tread}
        $g.FillRectangle($brush,$left,520+$i,100,1)
    }
    $g.DrawString(@('Checker floor','Blue floor','Indigo floor')[$context],$font,[Drawing.Brushes]::White,$left+60,645)
}
$tileBrush.Dispose()
$bmp.Save("$root/Docs/StairFloor-preview.png",[Drawing.Imaging.ImageFormat]::Png)
$g.Dispose();$bmp.Dispose();$font.Dispose();$tread.Dispose();$riser.Dispose();$edge.Dispose()
Write-Output "PASS: $checks stair asset/direction checks and Direct3D vertex/fragment compilation."
