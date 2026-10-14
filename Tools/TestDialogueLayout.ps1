$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$source=Get-Content -Raw -Encoding UTF8 "$root/Assets/Scripts/Interaction/CharacterDialogueWindow.cs"
$method=[regex]::Match($source,'(?s)    internal static float GetChoiceViewportHeight\(.*?\n    \}').Value
if(!$method){throw 'Missing production dock height calculation'}
$fixture=@'
using System;
public static class Mathf {
 public static float Max(float a,float b){return Math.Max(a,b);}
 public static float Clamp(float v,float min,float max){return Math.Max(min,Math.Min(max,v));}
}
public static class DialogueLayoutTests {
 // METHOD
 public static int Run(){
  int checks=0;
  foreach(float panel in new[]{360f,480f,664f,960f,1440f})
  foreach(int count in new[]{1,2,3,4})
  foreach(float option in new[]{49f,76f,240f,1500f}){
   float preferred=count*option+(count-1)*10;
   float actual=GetChoiceViewportHeight(preferred,panel);
   if(actual<=0 || actual>(panel-128)*.5f)throw new Exception("Choice viewport consumed transcript");checks++;
   if(preferred<=(panel-128)*.5f && actual!=preferred)throw new Exception("Short choices should fit exactly at bottom");checks++;
   if(actual+74>=panel-80)throw new Exception("Choices overlap header/transcript");checks++;
   if(preferred>(panel-128)*.5f && actual>=preferred)throw new Exception("Long choices must scroll in capped viewport");checks++;
  }
  if(GetChoiceViewportHeight(0,664)!=1)throw new Exception("Empty choices must not create invalid rect");
  return checks+1;
 }
}
'@
Add-Type -TypeDefinition $fixture.Replace('// METHOD',$method)
Write-Output "PASS: $([DialogueLayoutTests]::Run()) production dock-sizing assertions."
$guards=@(
 @(([regex]::Matches($source,'AddRule\("Frame (Left|Right|Top|Bottom)"').Count -eq 4),'Four-sided frame'),
 @($source.Contains('Rect("Choice " + (i+1), choiceContent)'),'Choices are outside transcript content'),
 @($source.Contains('choiceScroll.content = choiceContent') -and $source.Contains('scroll.content = content'),'Independent transcript/choice scrolling'),
 @($source.Contains('new Vector2(26,48), new Vector2(-24,49)'),'Bottom-anchored options above footer'),
 @($source.Contains('choiceDivider.offsetMin = new Vector2(26, 60 + height)') -and $source.Contains('dialogueViewport.offsetMin = new Vector2(26, 74 + height)'),'Divider with space on both sides'),
 @($source.Contains('image.raycastTarget = false'),'Borders do not consume pointer input'),
 @($source.Contains('Mathf.Approximately(lastPanelHeight, panel.rect.height)'),'Resize refresh')
)
foreach($guard in $guards){if(!$guard[0]){throw $guard[1]}}
Write-Output "PASS: $($guards.Count) layout wiring checks; actual Unity rendering still needs Play Mode verification."
