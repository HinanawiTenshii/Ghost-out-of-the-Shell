# Render actual Blacksmith/Strongman production methods with the existing Unity data stubs.
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root=Split-Path $PSScriptRoot -Parent
$harness=Get-Content -Raw (Join-Path $PSScriptRoot 'TestPeopleVisuals.ps1')
$stubs=[regex]::Match($harness,"(?s)\`$stubs=@'\r?\n(.*?)\r?\n'@").Groups[1].Value
if(-not $stubs) {throw 'Missing Unity stubs'}
$code='using UnityEngine;'+$stubs
$names=@('Blacksmith','Strongman')
foreach($name in $names) {
    $source=Get-Content -Raw (Join-Path $root "Assets/Scripts/Characters/$($name)ZeldaCharacterData.cs")
    $size=if($name -eq 'Blacksmith'){16}else{20}
    $maps=@{}
    foreach($direction in @('Front','Back','Left','Right')) {
        $block=[regex]::Match($source,'(?s)string\[\] '+$direction+'Body = \{(.*?)\};').Groups[1].Value
        $rows=[string[]]@([regex]::Matches($block,'"([.A-Z]+)"') | ForEach-Object {$_.Groups[1].Value})
        if($rows.Count -ne $size -or @($rows | Where-Object {$_.Length -ne $size}).Count) {throw "Bad dimensions $name $direction"}
        $headEnd=if($size -eq 16){4}else{5}
        foreach($x in ($size/2-2)..($size/2+1)) {
            if($rows[$headEnd][$x] -eq '.' -or $rows[$headEnd+1][$x] -eq '.') {throw "Head/shoulder gap $name $direction"}
        }
        $maps[$direction]=$rows
        $feetStart=if($size -eq 16){11}else{14}
        foreach($row in $feetStart..($feetStart+1)) {
            if($rows[$row] -notmatch '^[.]+[PK]{2,3}[.]{2}[PK]{2,3}[.]+$') {throw "Long or malformed legs $name"}
        }
        foreach($row in ($feetStart+2)..($size-1)) {if($rows[$row].Trim('.').Length) {throw "Legs too long $name"}}
    }
    foreach($row in 0..($size-1)) {
        $reverse=$maps.Left[$row].ToCharArray(); [Array]::Reverse($reverse)
        if(($reverse -join '') -ne $maps.Right[$row]) {throw "Asymmetric profile $name"}
    }
    if($name -eq 'Blacksmith') {
        if($maps.Left[8].Substring(7,2) -ne 'GG' -or $maps.Right[8].Substring(7,2) -ne 'GG') {throw 'Blacksmith profile hands not centered'}
    } else {
        if($maps.Left[9].Substring(9,3) -ne 'GGG' -or $maps.Right[9].Substring(8,3) -ne 'GGG') {throw 'Strongman profile hands not centered'}
    }
    $fields=([regex]::Matches($source,'private Color \w+\s*=\s*new Color\([^;]+;') | ForEach-Object {$_.Value}) -join [Environment]::NewLine
    $eye=[regex]::Match($source,'public override Color GhostFormEyeColor => ([^;]+);').Groups[1].Value
    $start=$source.IndexOf('    private static readonly string[] FrontBody')
    $end=if($size -eq 16){$source.IndexOf('    private static int DirectionIndex')}else{$source.IndexOf('    private static Vector2 ToNearestCardinalDirection')}
    $production=$source.Substring($start,$end-$start)
    $code+=@"
public class $($name)VisualHarness : PeopleVisualBase {
$fields
public Color GhostFormEyeColor {get {return $eye;}}
public Color HandColor {get {return PixelColor('G');}}
public Color[] Render(int direction,bool attack) {
    var dirs=new[]{Vector2.down,Vector2.up,Vector2.left,Vector2.right};
    return CreateTexture(dirs[direction],attack).pixels;
}
$production
}
"@
}
$code+=@'
public static class WorkmenChecks {
    public static Color[] Walk(Color[] p,int size,int step) {
        var src=new Color32[p.Length]; for(int i=0;i<p.Length;i++) src[i]=p[i];
        var rect=size==16?new RectInt(step==0?5:9,3,2,2):new RectInt(step==0?6:11,4,3,2);
        var result=PixelWalkFrameUtility.BuildStep(src,size,size,rect);
        var output=new Color[p.Length]; for(int i=0;i<p.Length;i++) output[i]=result[i];
        return output;
    }
    public static void Connected(Color[] p,int size) {
        var seen=new bool[p.Length]; var pending=new Queue<int>(); int seed=(size/2)*size+size/2;
        if(p[seed].a==0) throw new Exception("No body centre");
        seen[seed]=true;pending.Enqueue(seed);
        while(pending.Count>0) {
            int i=pending.Dequeue(),x=i%size,y=i/size;
            int[] ns={x>0?i-1:-1,x<size-1?i+1:-1,y>0?i-size:-1,y<size-1?i+size:-1};
            foreach(int n in ns) if(n>=0&&!seen[n]&&p[n].a>0) {seen[n]=true;pending.Enqueue(n);}
        }
        for(int i=0;i<p.Length;i++) if(p[i].a>0&&!seen[i]) throw new Exception("Detached pixel "+i%size+","+i/size);
    }
}
'@
$utility=Get-Content -Raw (Join-Path $root 'Assets/Scripts/Characters/PixelWalkFrameUtility.cs')
$code+=$utility.Replace('using UnityEngine;','')
Add-Type -TypeDefinition $code -CompilerOptions '/nowarn:0414,0649'
$people=@((New-Object BlacksmithVisualHarness),(New-Object StrongmanVisualHarness))
$font=[Drawing.Font]::new('Arial',11)
$count=0
foreach($pose in @('Idle','Attack','Walk0','Walk1')) {
    $bmp=[Drawing.Bitmap]::new(960,470); $g=[Drawing.Graphics]::FromImage($bmp)
    $g.Clear([Drawing.Color]::FromArgb(24,31,43))
    foreach($kind in 0..1) {foreach($d in 0..3) {
        $size=if($kind -eq 0){16}else{20}
        $p=$people[$kind].Render($d,($pose -eq 'Attack'))
        if($pose.StartsWith('Walk')) {$p=[WorkmenChecks]::Walk($p,$size,[int]::Parse($pose.Substring(4)))}
        [WorkmenChecks]::Connected($p,$size); $count++
        if($pose -eq 'Attack') {
            $oldX=if($kind -eq 0){@(10,4,7,7)[$d]}else{@(3,14,9,8)[$d]}
            $oldY=if($kind -eq 0){6}else{9}
            $handWidth=if($kind -eq 0){2}else{3}
            foreach($y in $oldY..($oldY+1)) {foreach($x in $oldX..($oldX+$handWidth-1)) {
                if($p[$y*$size+$x].Equals($people[$kind].HandColor)) {throw "Old idle hand remains during attack $kind $d"}
            }}
        }
        if($kind -eq 0) {
            $metal=@($p | Where-Object {[Math]::Abs($_.r-.48) -lt .01 -and [Math]::Abs($_.g-.55) -lt .01 -and $_.a -gt 0}).Count
            if($metal -lt 3) {throw "Hammer missing $pose $d"}
        }
        $ox=20+$d*240; $oy=10+$kind*230
        foreach($row in 0..($size-1)) {foreach($x in 0..($size-1)) {
            $c=$p[($size-1-$row)*$size+$x]; if($c.a -le 0) {continue}
            $brush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(255,[int]($c.r*255),[int]($c.g*255),[int]($c.b*255)))
            $g.FillRectangle($brush,($ox+$x*10),($oy+$row*10),10,10); $brush.Dispose()
        }}
        $g.DrawString(($names[$kind]+' / '+@('Front','Back','Left','Right')[$d]),$font,[Drawing.Brushes]::White,($ox+10),($oy+205))
    }}
    $path=Join-Path $root "Docs/Workmen-$pose-preview.png"
    $bmp.Save($path,[Drawing.Imaging.ImageFormat]::Png); $g.Dispose(); $bmp.Dispose()
    Write-Output "Preview: $path"
}
$font.Dispose()
Write-Output "PASS: $count production poses, short legs, centered profile hands, connected bodies/hammers, mirrored profiles."
