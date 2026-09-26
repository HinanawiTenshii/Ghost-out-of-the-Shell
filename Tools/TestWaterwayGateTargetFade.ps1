$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$source=Get-Content -Raw "$root/Assets/Scripts/Interaction/WaterwayGateTargetFade.cs"
function Extract($signature){
 $start=$source.IndexOf($signature);if($start -lt 0){throw "Missing $signature"}
 $begin=$source.IndexOf('{',$start);$end=$begin+1;$depth=1
 while($depth){if($source[$end] -eq '{'){$depth++};if($source[$end] -eq '}'){$depth--};$end++}
 $source.Substring($start,$end-$start)
}
$code=@'
using System;
using System.Collections.Generic;
public struct Color {public float r,g,b,a;public Color(float r,float g,float b,float a){this.r=r;this.g=g;this.b=b;this.a=a;}}
public class SpriteRenderer {public Color color;public SpriteRenderer(float alpha){color=new Color(.1f,.4f,.9f,alpha);}}
public class GameObject {public bool activeSelf=true;public int changes;public void SetActive(bool v){activeSelf=v;changes++;}}
public class WaterwayGate {public bool IsOpenRequested,IsMoving;}
public static class Physics2D {public static int syncs;public static void SyncTransforms(){syncs++;}}
public static class CameraCircularVision {public static int updates;public static void NotifyBlockersChanged(){updates++;}}
public static class Time {public static float deltaTime;}
public static class Mathf {
 public static float Max(float a,float b)=>Math.Max(a,b);
 public static bool Approximately(float a,float b)=>Math.Abs(a-b)<.000001f;
 public static float SmoothStep(float a,float b,float t){t=Math.Max(0,Math.Min(1,t));return a+(b-a)*t*t*(3-2*t);}
 public static float MoveTowards(float a,float b,float d)=>Math.Abs(b-a)<=d?b:a+Math.Sign(b-a)*d;
}
public class FadeHarness {
 private static readonly HashSet<FadeHarness> ActiveFaders = new HashSet<FadeHarness>();
 public void Enable()=>ActiveFaders.Add(this);public void Disable()=>ActiveFaders.Remove(this);
 public WaterwayGate sourceGate;public float fadeDuration=.6f;
 private SpriteRenderer[] sprites;private Color[] originalColors;private float visibility=1f;private bool visibleRequested=true,initialized;
 public GameObject gameObject=new GameObject();public SpriteRenderer[] children={new SpriteRenderer(1),new SpriteRenderer(.4f)};
 public float Alpha=>visibility;public bool Target=>visibleRequested;
 public T[] GetComponentsInChildren<T>(bool inactive)=>children as T[];
 public void Tick(float dt){Time.deltaTime=dt;if(gameObject.activeSelf)Update();}
 public void Initialize()=>EnsureInitialized();public void InitializeScene()=>Start();
'@
$code += [regex]::Match($source,'public bool IsFading =>[^;]+;').Value
foreach($sig in @('public static bool IsGateTransitioning(', 'private void EnsureInitialized(', 'private void Start(', 'public void FadeIn(', 'public void FadeOut(', 'public void SetVisibleImmediately(', 'private void Update(', 'private void ApplyOpacity(', 'private void SetObjectActive(')){$code+=Extract $sig}
$code+='}'
Add-Type -TypeDefinition $code
$count=0
function Check($ok,$label){if(!$ok){throw $label};$script:count++}
$f=[FadeHarness]::new();$f.Initialize();$f.FadeOut()
Check ($f.gameObject.activeSelf -and $f.Alpha -eq 1) 'Close does not prematurely disable or snap opacity'
$f.Tick(.3)
Check ($f.gameObject.activeSelf -and [Math]::Abs($f.children[0].color.a-.5) -lt .0001) 'Halfway fade stays active'
Check ([Math]::Abs($f.children[1].color.a-.2) -lt .0001) 'Preserve originally translucent child alpha'
Check ([Math]::Abs($f.children[0].color.b-.9) -lt .0001) 'RGB remains unchanged'
$f.FadeOut();Check ([Math]::Abs($f.Alpha-.5) -lt .0001) 'Repeated close does not restart transition'
$f.Tick(.3);Check (!$f.gameObject.activeSelf -and $f.Alpha -eq 0 -and $f.children[0].color.a -eq 0) 'Disable only after fully transparent'
Check ($f.gameObject.changes -eq 1 -and [Physics2D]::syncs -eq 1) 'One deactivation and collider refresh'
$f.FadeIn()
Check ($f.gameObject.activeSelf -and $f.children[0].color.a -eq 0) 'Reactivation begins transparent'
$f.Initialize();$f.Tick(.3)
Check ([Math]::Abs($f.children[0].color.a-.5) -lt .0001) 'Activation does not recache zero alpha'
$f.FadeIn();$f.Tick(.3)
Check ($f.Alpha -eq 1 -and [Math]::Abs($f.children[1].color.a-.4) -lt .0001) 'Restore original color alpha at full visibility'
$f.FadeOut();$f.Tick(.2);$before=$f.Alpha;$f.FadeIn()
Check ([Math]::Abs($before-$f.Alpha) -lt .0001) 'Reverse fading without discontinuity'
$f.Tick(.1);Check ($f.Alpha -gt $before -and $f.gameObject.activeSelf) 'Fade reverses toward visible, no stale disable'
$f.FadeOut();$before=$f.Alpha;$f.Tick(0);Check ($f.Alpha -eq $before) 'Pause freezes fade'
$f.Tick(4);Check (!$f.gameObject.activeSelf) 'Large frame completes fade safely'
$f.SetVisibleImmediately($true)
Check ($f.gameObject.activeSelf -and $f.Alpha -eq 1) 'Immediate restore hook'
$f.children[0]=$null;$f.FadeOut();$f.Tick(.6);Check (!$f.gameObject.activeSelf) 'Destroyed child renderer is safe'
$closed=[FadeHarness]::new();$closed.sourceGate=[WaterwayGate]::new();$closed.InitializeScene()
Check (!$closed.gameObject.activeSelf -and $closed.Alpha -eq 0) 'Initially closed source is hidden before rendering'
$open=[FadeHarness]::new();$open.sourceGate=[WaterwayGate]::new();$open.sourceGate.IsOpenRequested=$true;$open.InitializeScene()
Check ($open.gameObject.activeSelf -and $open.Alpha -eq 1) 'Initially open source preserves visible scene'
$empty=[FadeHarness]::new();$empty.children=@();$empty.FadeOut();$empty.Tick(1);Check (!$empty.gameObject.activeSelf) 'No renderer target still toggles'
Check ($source.Contains('RequireComponent(typeof(CameraVisionStreamingExempt))')) 'Fade targets opt out of conflicting camera activation'
Check ($source.Contains('OnEnable() => ActiveFaders.Add(this)') -and $source.Contains('OnDisable() => ActiveFaders.Remove(this)')) 'Lifecycle registers and removes active faders'
$gate=[WaterwayGate]::new();$unrelated=[WaterwayGate]::new()
$a=[FadeHarness]::new();$a.sourceGate=$gate;$a.Enable();$a.Initialize()
$b=[FadeHarness]::new();$b.sourceGate=$gate;$b.fadeDuration=1.2;$b.Enable();$b.Initialize()
Check (![FadeHarness]::IsGateTransitioning($null)) 'Null gate is safe'
Check (![FadeHarness]::IsGateTransitioning($gate)) 'Settled objects do not lock gate'
$gate.IsMoving=$true;Check ([FadeHarness]::IsGateTransitioning($gate)) 'Gate motion locks before completion events start fading'
$gate.IsMoving=$false;$a.FadeOut();$b.FadeOut()
Check ([FadeHarness]::IsGateTransitioning($gate)) 'Fade-out locks immediately before first fade frame'
Check (![FadeHarness]::IsGateTransitioning($unrelated)) 'Other gate remains usable'
$a.Tick(.6);$a.Disable();$b.Tick(.6)
Check ([FadeHarness]::IsGateTransitioning($gate)) 'Waits for slowest linked target'
$b.Tick(.6);$b.Disable()
Check (![FadeHarness]::IsGateTransitioning($gate)) 'Unlocks after all targets fade out and deactivate'
$a.FadeIn();$a.Enable();$b.FadeIn();$b.Enable()
Check ([FadeHarness]::IsGateTransitioning($gate)) 'Reactivated fade-in locks immediately'
$a.Tick(.6);$b.Tick(.6)
Check ([FadeHarness]::IsGateTransitioning($gate)) 'Shorter fade-in does not prematurely unlock'
$b.Tick(.6)
Check (![FadeHarness]::IsGateTransitioning($gate)) 'Full fade-in completion unlocks'
$a.FadeOut();$a.Tick(.5999997)
Check ([FadeHarness]::IsGateTransitioning($gate)) 'Near-complete fade remains locked until exact endpoint'
$a.Tick(.01)
Check (!$a.IsFading -and !$a.gameObject.activeSelf) 'Tiny remaining fade reaches zero and disables without stuck lock'
$b.FadeOut();$b.Disable()
Check (![FadeHarness]::IsGateTransitioning($gate)) 'Disabled or unloaded target removes stale lock'
$a.Disable()

$scene=Get-Content -Raw "$root/Assets/Scenes/Level2/Level2-Floor-1.unity"
$blocks=[regex]::Split($scene,'(?m)(?=^--- !u!)')
$map=@{}
foreach($block in $blocks){$match=[regex]::Match($block,'^--- !u!\d+ &(\d+)');if($match.Success){$id=$match.Groups[1].Value;if($map.ContainsKey($id)){throw "Duplicate scene ID $id"};$map[$id]=$block}}
$expected=@{
 '1A'=@('LeftWingWater1','LeftWingWater2')
 '2A'=@('RightWingWater1','RightWingWater2')
 '3A'=@('DownWingWater1','DownWingWater2')
 '3B'=@('3BWater1','3BWater2','3BWater3')
}
$gates=@($blocks | Where-Object {$_ -match 'm_SourcePrefab:.*548ba4bc59f14d20abc60c22517aa1ae'})
Check ($gates.Count -eq 4) 'Exactly four existing gates'
foreach($gate in $gates){
 $name=[regex]::Match($gate,'propertyPath: m_Name\r?\n\s+value: (.+)').Groups[1].Value.Trim()
 $instance=[regex]::Match($gate,'^--- !u!1001 &(\d+)').Groups[1].Value
 foreach($event in @('onOpened','onClosed')){
  $found=@()
  $matches=[regex]::Matches($gate,'propertyPath: ('+$event+'\.m_PersistentCalls\.m_Calls\.Array\.data\[\d+\])\.m_Target\r?\n\s+value: *\r?\n\s+objectReference: \{fileID: (\d+)')
  foreach($match in $matches){
   $prefix=$match.Groups[1].Value;$faderId=$match.Groups[2].Value;$block=$map[$faderId]
   Check ($block -match 'f29c4c5b39154611b40d6d6d887ab5ee') 'Event target is the fade component, not bare GameObject'
   $method=if($event -eq 'onOpened'){'FadeIn'}else{'FadeOut'}
   Check ($gate -match ('propertyPath: '+[regex]::Escape($prefix)+'.m_MethodName\r?\n\s+value: '+$method)) 'Correct open/close callback'
   $targetId=[regex]::Match($block,'m_GameObject: \{fileID: (\d+)').Groups[1].Value
   $target=$map[$targetId];$targetName=[regex]::Match($target,'m_Name: (.+)').Groups[1].Value.Trim();$found+=$targetName
   $gateId=[regex]::Match($block,'sourceGate: \{fileID: (\d+)').Groups[1].Value
   Check ($map[$gateId].Contains("m_PrefabInstance: {fileID: $instance}")) 'Initial-state source matches event source'
   Check ($target.Contains("component: {fileID: $faderId}")) 'Fader attached to target'
   $exempt=@([regex]::Matches($target,'component: \{fileID: (\d+)') | ForEach-Object {$map[$_.Groups[1].Value]} | Where-Object {$_ -match '076bd33f4dd04d1188f3cf494d301c92'})
   Check ($exempt.Count -eq 1) 'Authored streaming exemption prevents offscreen reactivation'
  }
  Check (@(Compare-Object ($found | Sort-Object) ($expected[$name] | Sort-Object)).Count -eq 0) 'Original gate-to-water mapping preserved'
 }
}
Check (@($blocks | Where-Object {$_ -match 'm_Script:.*f29c4c5b39154611b40d6d6d887ab5ee'}).Count -eq 9) 'Only nine water objects receive faders'
Write-Output "PASS: $count fade production-method and scene-binding checks (Unity stubs, not Play Mode)."
