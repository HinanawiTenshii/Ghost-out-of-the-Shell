param([string]$PreviewPath = '')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$source=Get-Content -Raw "$root/Assets/Scripts/Interaction/WaterwayGateController.cs"
function Extract($signature){
 $start=$source.IndexOf($signature);if($start -lt 0){throw "Missing $signature"}
 $begin=$source.IndexOf('{',$start);$end=$begin+1;$depth=1
 while($depth){if($source[$end] -eq '{'){$depth++};if($source[$end] -eq '}'){$depth--};$end++}
 $source.Substring($start,$end-$start)
}
$code=@'
using System;
using System.Collections.Generic;
public struct Color {public float r,g,b,a;public Color(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;}}
public struct Vector2 {public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}public static float Distance(Vector2 a,Vector2 b)=>(float)Math.Sqrt((a.x-b.x)*(a.x-b.x)+(a.y-b.y)*(a.y-b.y));}
public struct Quaternion {public float z;public static Quaternion identity=>new Quaternion();public static Quaternion Euler(float x,float y,float z)=>new Quaternion{z=z};}
public struct Vector3 {public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}}
public class Transform {public Quaternion localRotation;public Vector2 position;public Vector3 localPosition;}
public class TextMesh {public string text;public Color color;}
public class Scene {public int handle=1;}
public class GameObject {public Scene scene=new Scene();}
public class WaterwayGate {public string Identifier="1A";public bool IsOpenRequested;public int calls;public void SetOpen(bool value){calls++;IsOpenRequested=value;}}
public static class WaterwayGateTargetFade {public static WaterwayGate busyGate;public static bool IsGateTransitioning(WaterwayGate gate)=>gate!=null && gate==busyGate;}
public static class BatterySocket {public static object unpowered;public static bool IsPowered(object controller)=>controller!=unpowered;}
public class ZeldaCharacterData {public bool IsDead,IsGhostForm;}
public class ZeldaFourWayMover {public bool isActiveAndEnabled=true;public GameObject gameObject=new GameObject();public Transform transform=new Transform();public ZeldaCharacterData data=new ZeldaCharacterData();public T GetComponent<T>() where T:class=>data as T;}
public static class ZeldaRuntimeRegistry {public static ZeldaFourWayMover mover;public static ZeldaFourWayMover GetControlledMover()=>mover;public static Scene GetGameplayScene(GameObject g)=>g.scene;}
public static class DocumentReader {public static bool IsInputBlocked;}
public static class ClockworkPuppetRuntime {public static bool BlocksCharacterInput;}
public static class Time {public static float timeScale=1,deltaTime=.08f;}
public static class Application {public static bool isPlaying=true;}
public static class Mathf {
 public const float Deg2Rad=(float)(Math.PI/180.0);
 public static float Sin(float a)=>(float)Math.Sin(a);
 public static float Cos(float a)=>(float)Math.Cos(a);
 public static float Abs(float a)=>Math.Abs(a);
 public static float Max(float a,float b)=>Math.Max(a,b);
 public static float Lerp(float a,float b,float t)=>a+(b-a)*t;
 public static float SmoothStep(float a,float b,float t)=>Lerp(a,b,t*t*(3-2*t));
 public static float MoveTowards(float a,float b,float d)=>Math.Abs(b-a)<=d?b:a+Math.Sign(b-a)*d;
}
public class ControllerHarness {
 public WaterwayGate linkedGate;public float pose,interactionDistance=1.4f,switchDuration=.16f;
 public Transform handle=new Transform(),transform=new Transform();public TextMesh identifierText=new TextMesh();public GameObject gameObject=new GameObject();public bool isActiveAndEnabled=true;
 public void Tick()=>RefreshState();public void Interact()=>HandleInteraction();public ZeldaFourWayMover Nearby()=>GetNearbyCharacter();
 public Array CasePanels()=>BuildCase().ToArray();public Array HandlePanels()=>BuildHandle().ToArray();
 private void UpdateHandleGeometry(float amount){}
 public Vector3 Tip(float amount)=>GetHandleTip(amount);
 public Array ProjectedPanels(float amount){var parts=new Panel[6];FillHandlePanels(parts,amount);return parts;}
'@
foreach($property in @('DisplayedIdentifier','IsOpenRequested','IsTransitionLocked','HasBatteryPower','InputBlocked')){
 $match=[regex]::Match($source,'(?s)(?:public|private)\s+(?:string|bool)\s+'+$property+'\s*=>.*?;')
 if(!$match.Success){throw "Missing production property $property"};$code+=$match.Value
}
foreach($method in @('public void BindGate(', 'public void SetOpen(', 'public void ToggleFromExternal(', 'private void RefreshState(', 'private ZeldaFourWayMover GetNearbyCharacter(', 'private void HandleInteraction(', 'private struct Panel', 'private static List<Panel> BuildCase(', 'private static List<Panel> BuildHandle(', 'private static Vector3 GetHandleTip(', 'private static void FillHandlePanels(')){$code+=Extract $method}
$code+='}'
Add-Type -TypeDefinition $code
$count=0
function Check($ok,$label){if(!$ok){throw $label};$script:count++}
$c=[ControllerHarness]::new()
$c.Tick();$c.ToggleFromExternal();$c.SetOpen($true)
Check ($c.DisplayedIdentifier -eq '--' -and !$c.IsOpenRequested) 'Unbound is safe and shows dashes'
$g=[WaterwayGate]::new();$g.Identifier='2B';$c.BindGate($g)
Check ($g.calls -eq 0 -and $c.identifierText.text -eq '2B') 'Binding never changes gate state'
$c.ToggleFromExternal();Check ($g.IsOpenRequested -and $g.calls -eq 1) 'Opens linked gate once'
$c.ToggleFromExternal();Check (!$g.IsOpenRequested -and $g.calls -eq 2) 'Closes linked gate once'
$g.SetOpen($true);$c.Tick();Check $c.IsOpenRequested 'External gate changes are authoritative'
$g.Identifier='9Z';$c.Tick();Check ($c.identifierText.text -eq '9Z') 'Number follows gate edits'
$other=[ControllerHarness]::new();$other.BindGate($g);$other.ToggleFromExternal();$c.Tick()
Check (!$c.IsOpenRequested -and !$other.IsOpenRequested) 'Multiple controls remain synchronized'
$c.pose=0;$g.SetOpen($true);$c.Tick()
Check ($c.pose -gt 0 -and $c.pose -lt 1 -and [Math]::Abs($c.handle.localPosition.y+.14) -lt .001) 'Switch axle remains fixed at midpoint'
$c.Tick();Check ($c.pose -eq 1 -and [Math]::Abs($c.handle.localPosition.y+.14) -lt .001 -and $c.Tip(1).y -gt 0) 'Upper throw keeps assembly axle fixed'
[Application]::isPlaying=$false;$g.SetOpen($false);$c.Tick()
Check ($c.pose -eq 0 -and [Math]::Abs($c.handle.localPosition.y+.14) -lt .001 -and $c.Tip(0).y -lt 0) 'Lower throw keeps assembly axle fixed'
foreach($step in 0..20){
 $amount=$step/20.0;$tip=$c.Tip($amount);$panels=$c.ProjectedPanels($amount)
 Check ([Math]::Abs($tip.y*$tip.y+$tip.z*$tip.z-.22*.22) -lt .00001 -and $tip.x -eq 0) 'Grip follows constant-radius arc, not straight translation'
 Check ([Math]::Abs($panels[0].Y-$tip.y/2) -lt .00001 -and $panels[0].Height -ge [Math]::Abs($tip.y)) 'Rod connects fixed axle to moving grip'
 Check ([Math]::Abs($panels[2].Y-$tip.y) -lt .00001) 'Grip follows projected arm tip'
}
Check ([Math]::Abs($c.Tip(.5).y) -lt .00001 -and $c.Tip(.5).z -lt $c.Tip(0).z) 'Mid-throw faces viewer instead of moving axle'
Check ($c.ProjectedPanels(.5)[0].Height -lt $c.ProjectedPanels(0)[0].Height -and $c.ProjectedPanels(.5)[2].Height -gt $c.ProjectedPanels(0)[2].Height) 'Rod foreshortens and grip shows more depth at midpoint'
[Application]::isPlaying=$true
Check ($null -eq $c.Nearby()) 'Missing controlled mover is safe'
$m=[ZeldaFourWayMover]::new();[ZeldaRuntimeRegistry]::mover=$m
Check ($null -ne $c.Nearby()) 'Living nearby character can interact'
$before=$g.calls;$c.Interact();Check ($g.calls -eq $before+1) 'Interaction sends a single gate command'
[BatterySocket]::unpowered=$c;$unpoweredCalls=$g.calls;$c.Interact();$c.ToggleFromExternal();$c.SetOpen($true)
Check ($g.calls -eq $unpoweredCalls -and !$c.HasBatteryPower) 'Empty socket blocks player and scripted controller use'
[BatterySocket]::unpowered=$null
[WaterwayGateTargetFade]::busyGate=$g
$lockedCalls=$g.calls;$c.Interact();$c.ToggleFromExternal();$c.SetOpen(!$g.IsOpenRequested);$other.ToggleFromExternal()
Check ($g.calls -eq $lockedCalls -and $c.IsTransitionLocked -and $other.IsTransitionLocked) 'Busy gate rejects player, external controller and second controller requests'
$independent=[ControllerHarness]::new();$independentGate=[WaterwayGate]::new();$independent.BindGate($independentGate);$independent.ToggleFromExternal()
Check ($independentGate.calls -eq 1) 'Other gate controller remains usable'
[WaterwayGateTargetFade]::busyGate=$null;$c.Interact()
Check ($g.calls -eq $lockedCalls+1 -and !$c.IsTransitionLocked) 'Controller can interact again immediately after full completion'
$before=$g.calls-1
[DocumentReader]::IsInputBlocked=$true;$c.Interact();Check ($g.calls -eq $before+1) 'Menus block interaction'
[DocumentReader]::IsInputBlocked=$false
[ClockworkPuppetRuntime]::BlocksCharacterInput=$true;$c.Interact();Check ($g.calls -eq $before+1) 'Puppet input takeover blocks interaction'
[ClockworkPuppetRuntime]::BlocksCharacterInput=$false
[Time]::timeScale=0;$c.Interact();Check ($g.calls -eq $before+1) 'Paused game cannot toggle'
[Time]::timeScale=1
$m.data.IsDead=$true;Check ($null -eq $c.Nearby()) 'Dead character excluded'
$m.data.IsDead=$false;$m.data.IsGhostForm=$true;Check ($null -eq $c.Nearby()) 'Ghost interaction follows shared rule'
$m.data.IsGhostForm=$false;$m.transform.position=[Vector2]::new(5,0);Check ($null -eq $c.Nearby()) 'Out of range excluded'
$m.transform.position=[Vector2]::new(0,0);$m.gameObject.scene.handle=2;Check ($null -eq $c.Nearby()) 'Other gameplay scene excluded'
$m.gameObject.scene.handle=1;$m.isActiveAndEnabled=$false;Check ($null -eq $c.Nearby()) 'Disabled character excluded'
$c.BindGate($null);Check ($c.identifierText.text -eq '--') 'Unbinding clears old number'
Check ($c.CasePanels().Length -eq 14 -and $c.HandlePanels().Length -eq 6) 'Fixed axle collar and small batched rectangle geometry'
Check ($source.Contains('ZeldaInteractionArbiter.OfferInteraction(') -and $source.Contains('ZeldaInteractionArbiter.Submit(')) 'Uses shared arbitration'
Check ($source.Contains('linkedGate == null || IsTransitionLocked || !HasBatteryPower || mover == null')) 'Busy or unpowered controller does not offer interaction'
Check ($source.Contains('!InputBlocked && !IsTransitionLocked && HasBatteryPower ? GetNearbyCharacter() : null')) 'Late arbitration cannot show stale use prompt while locked'
$prefab=Get-Content -Raw "$root/Assets/Prefabs/Decorations/WaterwayGateController.prefab"
Check ($prefab.Contains('m_Layer: 0') -and $prefab.Contains('m_IsTrigger: 0')) 'Solid controller is masked world geometry and does not occlude sight'
Check ($source.Contains('if (gameObject.layer == LayerMask.NameToLayer("Visible Non Blocking")) gameObject.layer = 0;')) 'Existing controller scene overrides migrate before visual generation'
Check ($prefab.Contains('linkedGate: {fileID: 0}') -and $prefab.Contains('292af55397c443f6a5d7774297dac481')) 'Prefab has binding slot and project pixel font'
Write-Output "PASS: $count controller production-method and asset checks (Unity stubs, not Play Mode)."

if($PreviewPath){
 Add-Type -AssemblyName System.Drawing
 $bitmap=[Drawing.Bitmap]::new(840,450);$graphics=[Drawing.Graphics]::FromImage($bitmap)
 $graphics.Clear([Drawing.Color]::FromArgb(24,32,45))
 $scale=230.0
 for($state=0;$state -lt 3;$state++){
  $cx=140+280*$state;$cy=210
  foreach($p in $c.CasePanels()){
   $brush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(255,[int]($p.Color.r*255),[int]($p.Color.g*255),[int]($p.Color.b*255)))
   $graphics.FillRectangle($brush,[single]($cx+($p.X-$p.Width/2)*$scale),[single]($cy-($p.Y+$p.Height/2)*$scale),[single]($p.Width*$scale),[single]($p.Height*$scale));$brush.Dispose()
  }
  $saved=$graphics.Save();$graphics.TranslateTransform($cx,($cy+.14*$scale))
  foreach($p in $c.ProjectedPanels($state*.5)){
   $brush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(255,[int]($p.Color.r*255),[int]($p.Color.g*255),[int]($p.Color.b*255)))
   $graphics.FillRectangle($brush,[single](($p.X-$p.Width/2)*$scale),[single](-($p.Y+$p.Height/2)*$scale),[single]($p.Width*$scale),[single]($p.Height*$scale));$brush.Dispose()
  }
  $graphics.Restore($saved)
  $font=[Drawing.Font]::new('Consolas',27,[Drawing.FontStyle]::Bold)
  $text='2B';$color=@([Drawing.Color]::Orange,[Drawing.Color]::Aquamarine,[Drawing.Color]::Aquamarine)[$state]
  $brush=[Drawing.SolidBrush]::new($color);$size=$graphics.MeasureString($text,$font)
  $graphics.DrawString($text,$font,$brush,[single]($cx-$size.Width/2),[single]($cy-.34*$scale-$size.Height/2))
  $font.Dispose();$brush.Dispose()
  $font=[Drawing.Font]::new('Consolas',14);$label=@('CLOSED','MID THROW','OPEN')[$state];$size=$graphics.MeasureString($label,$font)
  $graphics.DrawString($label,$font,[Drawing.Brushes]::White,[single]($cx-$size.Width/2),380);$font.Dispose()
 }
 $bitmap.Save($PreviewPath,[Drawing.Imaging.ImageFormat]::Png);$graphics.Dispose();$bitmap.Dispose()
 Write-Output 'Preview uses production geometry; labels use a surrogate font (not a Unity screenshot).'
}
