$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root=Split-Path $PSScriptRoot -Parent
$base=Get-Content -Raw (Join-Path $PSScriptRoot 'TestPeopleVisuals.ps1')
$stubs=[regex]::Match($base,"(?s)\`$stubs=@'\r?\n(.*?)\r?\n'@").Groups[1].Value
$source=Get-Content -Raw (Join-Path $root 'Assets/Scripts/Characters/BehemothZeldaCharacterData.cs')
$source=$source.Substring($source.IndexOf('public sealed class BehemothZeldaCharacterData'))
$maps=@{}
foreach($direction in @('Front','Back','Left','Right')) {
    $block=[regex]::Match($source,'(?s)string\[\] '+$direction+'Body = \{(.*?)\};').Groups[1].Value
    $rows=[string[]]@([regex]::Matches($block,'"([.A-Z]+)"') | ForEach-Object {$_.Groups[1].Value})
    if($rows.Count -ne 28 -or @($rows | Where-Object {$_.Length -ne 32}).Count) {throw "Invalid dimensions $direction"}
    foreach($x in 13..18) {if($rows[7][$x] -eq '.' -or $rows[8][$x] -eq '.') {throw "Neck/gap $direction"}}
    foreach($row in 22..25) {
        foreach($x in @(8,9,10,11,12,13,18,19,20,21,22,23)) {
            if($rows[$row][$x] -eq '.') {throw "Missing short leg $direction"}
        }
    }
    foreach($row in 26..27) {if($rows[$row].Trim('.').Length) {throw 'Legs extend too far'}}
    $maps[$direction]=$rows
}
foreach($row in 0..27) {
    $reverse=$maps.Left[$row].ToCharArray(); [Array]::Reverse($reverse)
    if(($reverse -join '') -ne $maps.Right[$row]) {throw 'Profiles not mirrored'}
}
if(($maps.Back -join '').Contains('Y')) {throw 'Eyes on back of helmet'}
foreach($x in 10..21) {if($maps.Front[7][$x] -ne 'O') {throw 'Missing forehead separation'}}
foreach($row in 9..14) {
    if($maps.Front[$row][9] -ne 'O' -or $maps.Front[$row][22] -ne 'O') {throw 'Missing cheek outline'}
}
foreach($row in 9..15) {if($maps.Left[$row][13] -ne 'O') {throw 'Missing profile head/back separation'}}
$prefab=Get-Content -Raw (Join-Path $root 'Assets/Prefabs/Level1NPCS/Behemoth.prefab')
if($prefab -notmatch 'm_LocalScale: \{x: 1\.25, y: 1\.25, z: 1\}') {throw 'Expected 25% larger body and collider'}
if(($maps.Front[0..10] -join '').Contains('Y') -or -not $maps.Front[11].Contains('Y')) {throw 'Beast face should be recessed below the raised back armor'}
foreach($direction in @('Front','Back','Left','Right')) {
    $all=$maps[$direction] -join ''
    $red=[regex]::Matches($all,'[CRPE]').Count
    $opaque=[regex]::Matches($all,'[A-Z]').Count
    if($red -gt $opaque*.2) {throw "Red coverage too high $direction"}
    if([regex]::Matches($all,'C').Count -lt 8) {throw "Missing rectangular red inserts $direction"}
    if(-not ($maps[$direction] -join '').Contains('P')) {throw "Missing arm pistons $direction"}
}
$fields=([regex]::Matches($source,'private Color \w+\s*=\s*new Color\([^;]+;') | ForEach-Object {$_.Value}) -join [Environment]::NewLine
$start=$source.IndexOf('    private static readonly string[] FrontBody')
$end=$source.IndexOf('    private static int DirectionIndex')
$production=$source.Substring($start,$end-$start)
$code='using UnityEngine;'+$stubs+@"
public class BehemothVisualHarness : PeopleVisualBase {
$fields
public Color[] Render(int direction,bool attack) {
    var dirs=new[]{Vector2.down,Vector2.up,Vector2.left,Vector2.right};
    return CreateTexture(dirs[direction],attack).pixels;
}
$production
}
"@
$code+=@'
public static class BehemothVisualChecks {
    public static Color[] Walk(Color[] p,int step) {
        var src=new Color32[p.Length]; for(int i=0;i<p.Length;i++) src[i]=p[i];
        var result=PixelWalkFrameUtility.BuildStep(src,32,28,new RectInt(step==0?8:18,2,6,4));
        var output=new Color[p.Length]; for(int i=0;i<p.Length;i++) output[i]=result[i];
        return output;
    }
    public static void Connected(Color[] p) {
        var seen=new bool[p.Length]; var pending=new Queue<int>(); int seed=14*32+16;
        if(p[seed].a==0) throw new Exception("Missing torso");
        seen[seed]=true;pending.Enqueue(seed);
        while(pending.Count>0) {
            int i=pending.Dequeue(),x=i%32,y=i/32;
            int[] ns={x>0?i-1:-1,x<31?i+1:-1,y>0?i-32:-1,y<27?i+32:-1};
            foreach(int n in ns) if(n>=0&&!seen[n]&&p[n].a>0) {seen[n]=true;pending.Enqueue(n);}
        }
        for(int i=0;i<p.Length;i++) if(p[i].a>0&&!seen[i]) throw new Exception("Detached pixel "+i%32+","+i/32);
    }
}
'@
$utility=Get-Content -Raw (Join-Path $root 'Assets/Scripts/Characters/PixelWalkFrameUtility.cs')
$code+=$utility.Replace('using UnityEngine;','')
Add-Type -TypeDefinition $code -CompilerOptions '/nowarn:0414,0649'
$character=New-Object BehemothVisualHarness
$font=[Drawing.Font]::new('Arial',11)
$count=0
foreach($pose in @('Idle','Attack','Walk0','Walk1')) {
    $bmp=[Drawing.Bitmap]::new(1152,288); $g=[Drawing.Graphics]::FromImage($bmp)
    $g.Clear([Drawing.Color]::FromArgb(24,31,43))
    foreach($d in 0..3) {
        $pixels=$character.Render($d,($pose -eq 'Attack'))
        if($pose.StartsWith('Walk')) {$pixels=[BehemothVisualChecks]::Walk($pixels,[int]::Parse($pose.Substring(4)))}
        [BehemothVisualChecks]::Connected($pixels); $count++
        if($pose -eq 'Attack') {
            $feet=if($d -lt 2){@(2,29)}else{@(13,18)}
            foreach($x in $feet) {if($pixels[2*32+$x].a -le 0) {throw 'Slam fists not at ground level'}}
        }
        $ox=16+$d*288
        foreach($row in 0..27) {foreach($x in 0..31) {
            $c=$pixels[(27-$row)*32+$x]; if($c.a -le 0) {continue}
            $brush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(255,[int]($c.r*255),[int]($c.g*255),[int]($c.b*255)))
            $g.FillRectangle($brush,($ox+$x*8),(10+$row*8),8,8); $brush.Dispose()
        }}
        $g.DrawString(('Behemoth / '+@('Front','Back','Left','Right')[$d]),$font,[Drawing.Brushes]::White,($ox+20),248)
    }
    $path=Join-Path $root "Docs/Behemoth-$pose-preview.png"
    $bmp.Save($path,[Drawing.Imaging.ImageFormat]::Png); $g.Dispose(); $bmp.Dispose()
    Write-Output "Preview: $path"
}
$font.Dispose()
Write-Output "PASS: $count production poses, connected silhouettes, no neck, mirrored profiles, short legs and ground-contact slam."
