# Render production sprite/step methods with Unity value-type stubs, not Play Mode.
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$source=Get-Content -Raw "$root/Assets/Scripts/Characters/DesertStrongmanZeldaCharacterData.cs"
$harness=Get-Content -Raw "$PSScriptRoot/TestPeopleVisuals.ps1"
$stubs=[regex]::Match($harness,"(?s)\`$stubs=@'\r?\n(.*?)\r?\n'@").Groups[1].Value
if(!$stubs){throw 'Missing Unity stubs'}
$maps=[regex]::Match($source,'(?s)    private static readonly string\[\] Front.*?(?=    private readonly Sprite\[,)').Value
$production=[regex]::Match($source,'(?s)    private Texture2D CreateDesertTexture.*?(?=    protected override void OnValidate)').Value
$fields=([regex]::Matches($source,'private Color \w+\s*=\s*new Color\([^;]+;')|ForEach-Object{$_.Value}) -join "`n"
foreach($direction in 'Front','Back','Left'){
    $block=[regex]::Match($maps,'(?s)string\[\] '+$direction+' = \{(.*?)\};').Groups[1].Value
    $rows=@([regex]::Matches($block,'"([^"]+)"')|ForEach-Object{$_.Groups[1].Value})
    if($rows.Count -ne 20){throw "Incorrect row count: $direction"}
    foreach($row in $rows){if($row.Length -ne 20 -or $row -match '[^.WHQCDLBAFSGRKE]'){throw "Invalid pixel row: $direction $row"}}
    foreach($r in 14..15){if($rows[$r] -ne '......KKK..KKK......'){throw 'Strongman feet no longer match existing animation rig'}}
    foreach($r in 16..19){if($rows[$r].Trim('.').Length){throw 'Legs are too long'}}
}
$utility=(Get-Content -Raw "$root/Assets/Scripts/Characters/PixelWalkFrameUtility.cs").Replace('using UnityEngine;','')
$code='using UnityEngine;'+$stubs+$utility+@"
public class DesertStrongmanHarness : PeopleVisualBase {
$fields
$maps
$production
public Color[] Render(int direction,bool attacking){return CreateDesertTexture(direction,attacking).pixels;}
public Color Glove {get{return gloveColor;}}
}
"@
$code+=@'
public static class DesertStrongmanChecks {
    public static Color[] Walk(Color[] src,int step){
        var p=new Color32[src.Length];for(int i=0;i<p.Length;i++)p[i]=src[i];
        var result=PixelWalkFrameUtility.BuildStep(p,20,20,new RectInt(step==0?6:11,4,3,2));
        var output=new Color[result.Length];for(int i=0;i<output.Length;i++)output[i]=result[i];return output;
    }
    public static void Connected(Color[] p){
        var seen=new bool[p.Length];var pending=new Queue<int>();int seed=10*20+10;
        if(p[seed].a==0)throw new Exception("Missing torso center");
        pending.Enqueue(seed);seen[seed]=true;
        while(pending.Count>0){
            int i=pending.Dequeue(),x=i%20,y=i/20;
            int[] next={x>0?i-1:-1,x<19?i+1:-1,y>0?i-20:-1,y<19?i+20:-1};
            foreach(int n in next)if(n>=0&&!seen[n]&&p[n].a>0){seen[n]=true;pending.Enqueue(n);}
        }
        for(int i=0;i<p.Length;i++)if(p[i].a>0&&!seen[i])throw new Exception("Detached pixel at "+i%20+","+i/20);
    }
}
'@
Add-Type -TypeDefinition $code
$actor=[DesertStrongmanHarness]::new()
Add-Type -AssemblyName System.Drawing
$font=[Drawing.Font]::new('Arial',12)
$poses=@('Idle','Walk0','Walk1','Attack');$count=0
foreach($pose in $poses){
    $bmp=[Drawing.Bitmap]::new(960,245);$g=[Drawing.Graphics]::FromImage($bmp);$g.Clear([Drawing.Color]::FromArgb(24,31,43))
    foreach($d in 0..3){
        $pixels=$actor.Render($d,$pose -eq 'Attack')
        if($pose.StartsWith('Walk')){$pixels=[DesertStrongmanChecks]::Walk($pixels,[int]::Parse($pose.Substring(4)))}
        [DesertStrongmanChecks]::Connected($pixels);$count++
        if($pose -eq 'Attack'){
            $oldX=@(3,14,8,9)[$d]
            foreach($x in $oldX..($oldX+2)) {if($pixels[9*20+$x].Equals($actor.Glove)){throw "Idle hand remains during attack: $d"}}
        }
        foreach($row in 0..19){foreach($x in 0..19){
            $c=$pixels[(19-$row)*20+$x];if($c.a -le 0){continue}
            $brush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(255,[int](255*$c.r),[int](255*$c.g),[int](255*$c.b)))
            $g.FillRectangle($brush,20+$d*240+$x*10,10+$row*10,10,10);$brush.Dispose()
        }}
        $g.DrawString((@('Front','Back','Left','Right')[$d]+' / '+$pose),$font,[Drawing.Brushes]::White,50+$d*240,218)
    }
    $path=Join-Path $root "Docs/DesertStrongman-$pose-preview.png"
    $bmp.Save($path,[Drawing.Imaging.ImageFormat]::Png);$g.Dispose();$bmp.Dispose();Write-Output "Preview: $path"
}
$font.Dispose()
$left=$actor.Render(2,$false);$right=$actor.Render(3,$false)
foreach($y in 0..19){foreach($x in 0..19){if(!$left[$y*20+$x].Equals($right[$y*20+19-$x])){throw 'Side silhouettes are not mirrored'}}}
$prefab=Get-Content -Raw "$root/Assets/Prefabs/Level2NPCS/Strongman-level2.prefab"
$template=Get-Content -Raw "$root/Assets/Prefabs/Level1NPCS/Strongman-Level1.prefab"
foreach($field in 'health','skillValue','permissionLevel','possessionEnergy','possessionCost','moveSpeed','cameraOrthographicSize','playerVisionRadius','attackPower','attackPrefab','attackDuration','attackSpawnOffset','attackSize'){
    $pattern="(?m)^  ${field}: (.*)$"
    if([regex]::Match($prefab,$pattern).Groups[1].Value.Trim() -ne [regex]::Match($template,$pattern).Groups[1].Value.Trim()){throw "Inherited gameplay value changed: $field"}
}
$definitions=@([regex]::Matches($prefab,'(?m)^--- !u!\d+ &(\d+)')|ForEach-Object{$_.Groups[1].Value})
foreach($ref in [regex]::Matches($prefab,'\{fileID: (\d+)\}')){
    $id=$ref.Groups[1].Value;if($id -ne '0' -and $definitions -notcontains $id){throw "Broken prefab local reference: $id"}
}
if(!$prefab.Contains('guid: 94e99f5562c84ae5b76ac29154ca6cee') -or !$source.Contains(': StrongmanZeldaCharacterData')){throw 'Incorrect inherited role or visual script'}
if(!$source.Contains('new Vector2(0.5f, 0.3f), 16f') -or !$source.Contains('texture.Apply(false, false)')){throw 'Pivot/scale or walking readability regression'}
Write-Output "PASS: $count connected directional poses, short feet, side mirrors, no duplicated attack hand, prefab links and unchanged gameplay values."
