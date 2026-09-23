$ErrorActionPreference='Stop'
. "$PSScriptRoot/TestClockworkPuppetVisual.ps1"
$walkImages=@()
foreach($pose in 0..2){
    foreach($segments in 0..6){
        [ClockworkPuppetPickupItemVisual]::ApplyRuntimePose($texture,($segments/6.0),$pose)
        $pixels=$texture.pixels.Clone()
        $red=@($pixels|Where-Object {$_.a -gt 0 -and $_.r -gt $_.g*1.5}).Count
        Check ($red -eq 2*$segments) 'Walking never refills energy display'
        foreach($row in 0..13){foreach($x in 0..($w-1)){
            $i=($h-1-$row)*$w+$x
            Check ($pixels[$i].a -eq $full[$i].a) 'Head and upper neck stay still'
        }}
        $seen=[Collections.Generic.HashSet[int]]::new();$queue=[Collections.Generic.Queue[int]]::new()
        $seed=0;while($pixels[$seed].a -eq 0){$seed++};$queue.Enqueue($seed);[void]$seen.Add($seed)
        while($queue.Count){
            $i=$queue.Dequeue();$x=$i%$w;$y=[int][Math]::Floor($i/$w)
            foreach($n in @($(if($x -gt 0){$i-1}else{-1}),$(if($x -lt $w-1){$i+1}else{-1}),$(if($y -gt 0){$i-$w}else{-1}),$(if($y -lt $h-1){$i+$w}else{-1}))){
                if($n -ge 0 -and $pixels[$n].a -gt 0 -and $seen.Add($n)){$queue.Enqueue($n)}
            }
        }
        Check ($seen.Count -eq @($pixels|Where-Object a -gt 0).Count) 'No detached legs in any pose'
        $feet=@();foreach($x in @(1,6,16,21)){
            $lowest=0;while($pixels[$lowest*$w+$x].a -eq 0){$lowest++};$feet+=,$lowest
        }
        $expected=if($pose -eq 0){@(0,0,0,0)}elseif($pose -eq 1){@(2,0,2,0)}else{@(0,2,0,2)}
        Check (($feet -join ',') -eq ($expected -join ',')) 'Diagonal legs lift exactly two pixels'
        if($segments -eq 6){$walkImages+=@{pixels=$pixels;label=@('Rest','Step A','Step B')[$pose]}}
    }
}
[ClockworkPuppetPickupItemVisual]::ApplyRuntimePose($texture,.5,2)
$walkImages+=@{pixels=$texture.pixels.Clone();label='Step B / half charge'}
[ClockworkPuppetPickupItemVisual]::ApplyRuntimePose($texture,1,0)
foreach($i in 0..($full.Length-1)){foreach($c in @('r','g','b','a')){Check ($texture.pixels[$i].$c -eq $full[$i].$c) 'Rest restores every original pixel without trails'}}
$runtime=Get-Content -Raw "$root/Assets/Scripts/Items/ClockworkPuppetRuntime.cs"
function RuntimeMethod($sig){
    $start=$runtime.IndexOf($sig);$brace=$runtime.IndexOf('{',$start);$end=$brace+1;$depth=1
    while($depth -gt 0){if($runtime[$end] -eq '{'){$depth++};if($runtime[$end] -eq '}'){$depth--};$end++}
    return $runtime.Substring($start,$end-$start).Replace('Vector2','WalkVector').Replace('Mathf','WalkMath').Replace('Time.deltaTime','WalkTime.deltaTime')
}
$code=@'
using System;
public struct WalkVector {
    public float x,y;public WalkVector(float x,float y){this.x=x;this.y=y;}
    public float magnitude=>(float)Math.Sqrt(x*x+y*y);public float sqrMagnitude=>x*x+y*y;
    public static WalkVector operator -(WalkVector a,WalkVector b)=>new WalkVector(a.x-b.x,a.y-b.y);
}
public static class WalkMath {public static float Max(float a,float b)=>Math.Max(a,b);public static float Repeat(float a,float b)=>a-(float)Math.Floor(a/b)*b;}
public static class WalkTime {public static float deltaTime=.02f;}
public static class DocumentReader {public static bool IsInputBlocked;}
public class WalkBody {public WalkVector position;}
public class WalkLever {public bool isActiveAndEnabled=true;}
public class WalkProbe {
    object runtimeTexture=new object();WalkBody physicsBody=new WalkBody(),transform=new WalkBody();
    bool hasWalkPosition,broken,attached,manualControlMode=true,IsUnderRemoteControl=true;
    WalkVector previousWalkPosition,manualMoveDirection=new WalkVector(1,0);
    WalkLever targetLever=new WalkLever();float walkMotionRemaining,walkElapsed,moveSpeed=1.35f;int walkPose;
    void UpdateMagicVisual(){} // Pixel/energy upload behavior is exercised separately using the real visual class.
    void Move(){physicsBody.position=new WalkVector(physicsBody.position.x+.02f,0);LateUpdate();}
    static int checks;static void Check(bool b,string s){checks++;if(!b)throw new Exception(s);}
    public static int Run(){
        var p=new WalkProbe();p.LateUpdate();Check(p.walkPose==0,"No movement despite held input stays idle");
        p.Move();Check(p.walkPose==1,"Actual movement starts step A");
        for(int i=0;i<6;i++)p.Move();Check(p.walkPose==2,"Movement advances to B");
        p.LateUpdate();Check(p.walkPose!=0,"Bridge a render frame without physics motion");
        for(int i=0;i<5;i++)p.LateUpdate();Check(p.walkPose==0,"Blocked wall stops walking");
        p.Move();p.manualMoveDirection=new WalkVector();p.LateUpdate();Check(p.walkPose==0,"Input release restores rest immediately");
        p.manualMoveDirection=new WalkVector(1,0);p.Move();p.attached=true;p.Move();Check(p.walkPose==0,"Attached moving lever does not trigger walking");
        p.attached=false;p.Move();DocumentReader.IsInputBlocked=true;p.LateUpdate();Check(p.walkPose==0,"UI interruption restores rest");DocumentReader.IsInputBlocked=false;
        p.Move();p.IsUnderRemoteControl=false;p.Move();Check(p.walkPose==0,"Leaving remote mode stops manual gait");
        p.manualControlMode=false;p.Move();Check(p.walkPose==1,"Automatic pursuit also animates");
        p.targetLever=null;p.Move();Check(p.walkPose==0,"No automatic destination stops gait");
        p.targetLever=new WalkLever();p.Move();p.broken=true;p.Move();Check(p.walkPose==0,"Broken puppet cannot animate");
        p.broken=false;p.ResetWalkAnimation();p.physicsBody.position=new WalkVector(100,0);p.LateUpdate();Check(p.walkPose==0,"Re-enable/restore seeds position without phantom step");
        p.physicsBody.position=new WalkVector(200,0);p.LateUpdate();Check(p.walkPose==0,"Teleport not a walking step");
        p.Move();WalkTime.deltaTime=0;p.LateUpdate();Check(p.walkPose==0,"Paused game stops gait");WalkTime.deltaTime=.02f;
        return checks;
    }
'@
$code+=(RuntimeMethod '    private void LateUpdate()')+(RuntimeMethod '    private void ResetWalkAnimation()')+'}'
Add-Type -TypeDefinition $code
$motionChecks=[WalkProbe]::Run()
$bmp=[Drawing.Bitmap]::new(920,285);$g=[Drawing.Graphics]::FromImage($bmp);$g.Clear([Drawing.Color]::FromArgb(24,31,43));$font=[Drawing.Font]::new('Segoe UI',12)
foreach($j in 0..3){
    foreach($y in 0..27){foreach($x in 0..23){
        $c=$walkImages[$j].pixels[$y*24+$x];if($c.a -eq 0){continue}
        $brush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(255,[int]($c.r*255),[int]($c.g*255),[int]($c.b*255)))
        $g.FillRectangle($brush,($j*230+30+$x*7),(20+(27-$y)*7),7,7);$brush.Dispose()
    }}
    $g.DrawString($walkImages[$j].label,$font,[Drawing.Brushes]::White,$j*230+30,240)
}
$bmp.Save("$root/Docs/ClockworkPuppet-walk-preview.png",[Drawing.Imaging.ImageFormat]::Png)
$g.Dispose();$bmp.Dispose();$font.Dispose()
Write-Output "PASS: $motionChecks production movement-gate scenarios and $checks total pixel/energy assertions."
