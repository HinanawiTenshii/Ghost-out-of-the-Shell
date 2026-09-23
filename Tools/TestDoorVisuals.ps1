$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$baselines=@{
 'DoorHinge'='B6F2C2E556FF04E7EBE4FF0C59D51923B0D2964AC2DDD1EB00E4FE781B34AFB8'
 'DoubleDoorHinge'='484D1905C62F4F21EE43647ADF75E9B9CB6A744FF0563A89FC17522C89E3238C'
}
foreach($name in $baselines.Keys){
 $text=(Get-Content -Raw "$root/Assets/Prefabs/Decorations/$name.prefab").Replace("`r`n","`n").TrimEnd()
 if(!$text.Contains('easeDoorRotation: 1')){throw 'Door easing not enabled'}
 $canonical=[regex]::Replace($text,'(?m)^  m_Color: .*$','  m_Color: RGB')
 $canonical=[regex]::Replace($canonical,'m_Materials:\n  - \{[^\n]+\}','m_Materials: MATERIAL')
 $canonical=$canonical.Replace("  easeDoorRotation: 1`n",'')
 # Strip only the explicitly added visual hardware before comparing the original structure.
 $canonical=[regex]::Replace($canonical,'(?ms)^--- !u!(?:1|4|212) &866100\d+\n.*?(?=^---|\z)','').TrimEnd()
 $canonical=[regex]::Replace($canonical,'  m_Children:\n(?:  - \{fileID: 866100\d+\}\n)+',"  m_Children: []`n")
 $canonical=[regex]::Replace($canonical,'  attachedVisuals:\n(?:  - \{fileID: 866100\d+\}\n?)+','').TrimEnd()
 if($name -eq 'DoorHinge'){
  # The user's additional pivot-alignment request explicitly changes this one position.
  $canonical=$canonical.Replace('m_LocalPosition: {x: 5.428338, y: 0, z: 0}','m_LocalPosition: {x: 5.281, y: 0.059999228, z: 0}')
  if(!$text.Contains('m_LocalPosition: {x: 5.428338, y: 0, z: 0}')){throw 'Single leaf endpoint not aligned'}
 }
 $hash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($canonical)))
 if($hash -ne $baselines[$name]){throw "Unexpected nonvisual prefab change: $name"}
}
$source=Get-Content -Raw "$root/Assets/Scripts/Interaction/DoorHingeInteraction.cs"
if($source -notmatch 'private bool easeDoorRotation;' -or !$source.Contains(': Quaternion.RotateTowards(previous, target, rotationSpeed * Time.deltaTime)')){throw 'Original opt-out motion must remain'}
$bridge=Get-Content -Raw "$root/Assets/Scenes/Level1/Level1-Floor2.unity"
$block=[regex]::Match($bridge,'(?ms)^--- !u!114 &607445789\r?\n.*?(?=^---|\z)').Value
if(!$block.Contains('rotationSpeed: 45') -or $block.Contains('easeDoorRotation: 1')){throw 'Bridge motion changed'}

# Run the production easing state machine. These stubs supply only planar
# quaternion interpolation/math, equivalent for the Z-only door trajectories.
$motion=[regex]::Match($source,'(?s)    private struct HingeMotion.*?(?=    private void UpdateHingeTargets)').Value
if(!$motion){throw 'Production motion state not found'}
$harness=@'
using System;
using UnityEngine;
namespace UnityEngine {
 public struct Quaternion {
  public float angle;
  public Quaternion(float a){angle=a;}
  public static float Angle(Quaternion a,Quaternion b){return Math.Abs(a.angle-b.angle);}
  public static Quaternion Slerp(Quaternion a,Quaternion b,float t){return new Quaternion(a.angle+(b.angle-a.angle)*t);}
 }
 public static class Mathf {public static float Clamp01(float v){return Math.Max(0,Math.Min(1,v));}}
}
public static class DoorMotionRegression {
'@
$tests=@'
 static int checks;
 static void Check(bool value,string label){checks++;if(!value)throw new Exception(label);}
 public static int Run(){
  foreach(int frames in new[]{8,15,30,60})foreach(float end in new[]{90f,-90f}){
   var motion=new HingeMotion();var current=new Quaternion(0);var target=new Quaternion(end);float previous=0;
   for(int i=1;i<=frames;i++){
    current=motion.Step(current,target,360f,.25f/frames);float angle=Math.Abs(current.angle);
    Check(angle+.001f>=previous && angle<=90.001f,"monotonic sweep/no overshoot");
    float t=(float)i/frames;Check(Math.Abs(angle-90*t*t*(3-2*t))<.001f,"smooth profile");previous=angle;
   }
   Check(Math.Abs(current.angle-end)<.001f,"same .25 second duration and endpoint");
  }
  var m=new HingeMotion();var shut=new Quaternion(0);var open=new Quaternion(90);
  var half=m.Step(shut,open,360,.125f);Check(Math.Abs(half.angle-45)<.001f,"halfway");
  Check(m.Step(half,open,360,0).angle==half.angle,"pause");
  Check(m.Step(half,open,0,1).angle==half.angle,"zero speed");
  var reverse=m.Step(half,shut,360,0);Check(reverse.angle==half.angle,"reverse without snap");
  Check(Math.Abs(m.Step(reverse,shut,360,.125f).angle)<.001f,"reverse finishes");
  Check(m.Step(open,open,360,.016f).angle==90,"restored endpoint unchanged");
  var speed=new HingeMotion();var pose=speed.Step(shut,open,180,.25f);
  Check(Math.Abs(pose.angle-45)<.001f,"custom speed");
  Check(Math.Abs(speed.Step(pose,open,360,.125f).angle-90)<.001f,"speed change without restart");
  var stalled=new HingeMotion();Check(stalled.Step(shut,open,360,0).angle==0,"initial pause");
  Check(stalled.Step(shut,open,360,1).angle==90,"large delta clamps exactly");
  return checks;
 }
}
'@
Add-Type -TypeDefinition ($harness+"`n"+$motion+"`n"+$tests)
Write-Output ("PASS: {0} production easing assertions; prefab structure/physics/ranges retained except requested hinge-end alignment; bridge stays linear." -f [DoorMotionRegression]::Run())
