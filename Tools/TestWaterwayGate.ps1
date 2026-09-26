$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$source=Get-Content -Raw "$taskRoot/Assets/Scripts/Interaction/WaterwayGate.cs"
function Extract($signature) {
    $start=$source.IndexOf($signature); if($start -lt 0){throw "Missing $signature"}
    $begin=$source.IndexOf('{',$start); $end=$begin+1; $depth=1
    while($depth){if($source[$end] -eq '{'){$depth++};if($source[$end] -eq '}'){$depth--};$end++}
    return $source.Substring($start,$end-$start)
}
$code=@'
using System;
using System.Collections.Generic;
public struct Color {public float r,g,b,a;public Color(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;}}
public struct Vector2 {public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}public static Vector2 zero=>new Vector2();}
public struct Vector3 {public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}}
public class Transform {public Vector3 localScale,localPosition;}
public class BoxCollider2D {public bool enabled=true,isTrigger;public Vector2 size,offset;public Transform transform=new Transform();}
public static class Application {public static bool isPlaying=true;}
public static class Time {public static float deltaTime;}
public static class Physics2D {public static int sync;public static void SyncTransforms(){sync++;}}
public static class CameraCircularVision {public static int changes;public static void NotifyBlockersChanged(){changes++;}}
public static class Mathf {
 public static int Clamp(int n,int min,int max)=>Math.Max(min,Math.Min(max,n));
 public static float Max(float a,float b)=>Math.Max(a,b);
 public static float Min(float a,float b)=>Math.Min(a,b);
 public static float Lerp(float a,float b,float t)=>a+(b-a)*t;
 public static float SmoothStep(float a,float b,float t)=>Lerp(a,b,t*t*(3-2*t));
 public static bool Approximately(float a,float b)=>Math.Abs(a-b)<.000001;
 public static float MoveTowards(float a,float b,float d)=>Math.Abs(b-a)<=d?b:a+Math.Sign(b-a)*d;
}
public class Event {public int count;public void Invoke(){count++;}}
public class GateHarness {
 public enum Letter {A,B,C,D,E,F,G,H,I,J,K,L,M,N,O,P,Q,R,S,T,U,V,W,X,Y,Z}
 private const float Depth=.72f,PostWidth=.42f,PostDepth=1.12f;
 public bool open,initialized,dirty;public float progress,moveDuration=.65f;
 public Transform shutter=new Transform(),sign=new Transform();
 public BoxCollider2D barrier=new BoxCollider2D();
 public Event onOpened=new Event(),onClosed=new Event();
 public object generated=new object();
 private T GetComponent<T>() where T:class=>barrier as T;
 private void Rebuild(){dirty=false;generated=new object();}
 public float lastLift;
 private void UpdateShutterClip(float lift){lastLift=lift;}
 public Array VisiblePanels(float width,float lift){var a=BuildShutter(width);for(int i=0;i<a.Count;i++)a[i]=ClipShutterPanel(a[i],lift);return a.ToArray();}
 public void Tick(float dt){Time.deltaTime=dt;Update();}
 public void Enable(){OnEnable();}
 public Array Frame(float width)=>BuildFrame(width).ToArray();
 public Array GetShutterPanels(float width)=>BuildShutter(width).ToArray();
 public void Post(BoxCollider2D p,float x)=>ConfigurePost(p,x);
'@
foreach($signature in @('    public static string FormatIdentifier(', '    public void SetOpen(', '    public void SetOpenImmediately(', '    private void OnEnable(', '    private void Update(', '    private void ApplyPose(', '    private void ApplyCollision(', '    private static Panel ClipShutterPanel(', '    private static void ConfigurePost(', '    private struct Panel', '    private static List<Panel> BuildFrame(', '    private static List<Panel> BuildShutter(')){$code+=Extract $signature}
$code+='}'
Add-Type -TypeDefinition $code
$checks=0
function Check($value,$label){if(!$value){throw $label};$script:checks++}
foreach($digit in 0..9){foreach($suffix in 0..25){Check ([GateHarness]::FormatIdentifier($digit,[GateHarness+Letter]$suffix) -cmatch '^[0-9][A-Z]$') 'Identifier has exactly one digit and uppercase letter'}}
Check ([GateHarness]::FormatIdentifier(-1,[Enum]::ToObject([GateHarness+Letter],-2)) -eq '0A') 'Clamp invalid low identifier'
Check ([GateHarness]::FormatIdentifier(20,[Enum]::ToObject([GateHarness+Letter],99)) -eq '9Z') 'Clamp invalid high identifier'
$gate=[GateHarness]::new();$gate.Enable()
Check ($gate.barrier.enabled -and $gate.progress -eq 0) 'Closed initial gate is solid'
$gate.SetOpen($true);Check $gate.barrier.enabled 'Opening request retains collision'
$gate.Tick(.325);Check ($gate.progress -gt .49 -and $gate.progress -lt .51) 'Halfway animation'
Check $gate.barrier.enabled 'Collision retained mid-opening'
Check ($gate.shutter.localScale.y -eq 1 -and [Math]::Abs($gate.shutter.localPosition.y-.37) -lt .001) 'Rigid shutter translates halfway without scaling'
$gate.Tick(.325);Check (!$gate.barrier.enabled -and $gate.progress -eq 1) 'Fully open releases passage'
Check ($gate.onOpened.count -eq 1) 'Open completion invoked once'
$gate.SetOpen($true);$gate.Tick(1);Check ($gate.onOpened.count -eq 1) 'Repeated command does not duplicate event'
$gate.SetOpen($false);Check $gate.barrier.enabled 'Closing collision is immediate'
$gate.Tick(.325);$gate.SetOpen($true);$gate.Tick(.325)
Check ($gate.onClosed.count -eq 0 -and $gate.onOpened.count -eq 2) 'Mid-animation reversal does not complete wrong state'
$gate.SetOpen($false);$gate.Tick(.65)
Check ($gate.onClosed.count -eq 1 -and $gate.progress -eq 0) 'Close endpoint event'
$gate.SetOpenImmediately($true);Check (!$gate.barrier.enabled -and $gate.onOpened.count -eq 2) 'Immediate restore avoids gameplay events'
$gate.SetOpen($false);$gate.Tick(.1);$progress=$gate.progress;$gate.Enable()
Check ([Math]::Abs($progress-$gate.progress) -lt .00001) 'Streaming re-enable preserves progress'
Check ($gate.sign.localScale.y -eq 0) 'Pose never scales identifier plate (default stub scale untouched)'
$post=[BoxCollider2D]::new();$gate.Post($post,1.91)
Check ($post.size.x -gt .419 -and $post.size.y -gt 1.119 -and !$post.isTrigger) 'Permanent solid side posts'
[Application]::isPlaying=$false;$gate.SetOpen($true)
Check (!$gate.barrier.enabled -and $gate.progress -eq 1) 'Editor state preview is immediate'
$frame=$gate.Frame(3.4);$leaf=$gate.GetShutterPanels(3.4)
Check ($frame.Count -eq 16 -and $leaf.Count -eq 2) 'Plain shutter has only a face and narrow rim'
Check ([Math]::Abs($gate.sign.localPosition.y-.44) -lt .001) 'Number plate stays on fixed housing'
foreach($step in 0..20){
 $t=$step/20.0;$lift=.74*$t*$t*(3-2*$t)
 $visible=$gate.VisiblePanels(3.4,$lift)
 for($i=0;$i -lt $visible.Count;$i++){
  $part=$visible[$i];$original=$leaf[$i]
  Check ([Math]::Abs($part.Width-$original.Width) -lt .0001 -and $part.Height -ge 0 -and $part.Height -le $original.Height+.0001) 'Clip preserves width and never stretches geometry'
  if($part.Height -gt .00001){
   Check ($part.Y+$lift+$part.Height/2 -le .3601 -and $part.Y+$lift-$part.Height/2 -ge -.3601) 'Visible translated panel stays inside housing aperture'
   if([Math]::Abs($part.Height-$original.Height) -lt .00001){Check ([Math]::Abs($part.Y-$original.Y) -lt .0001) 'Unoccluded details translate rigidly without distortion'}
  }
  if($step -eq 20){Check ($part.Height -lt .0001) 'Fully open leaf is concealed inside housing'}
 }
}
Check ($source.Contains('shutterMesh.MarkDynamic();') -and $source.Contains('Mathf.Approximately(lastLift, lift)')) 'Cached mesh buffers update only when pose changes'
foreach($part in @($frame)+@($leaf)){Check ($part.Width -gt 0 -and $part.Height -gt 0) 'Nondegenerate rectangles'}
Check ($source.Contains('text.font = numberFont;') -and $source.Contains('renderer.sharedMaterial = numberFont.material;')) 'Uses project font and matching atlas material'
$prefab=Get-Content -Raw "$taskRoot/Assets/Prefabs/Decorations/WaterwayGate.prefab"
Check ($prefab.Contains('292af55397c443f6a5d7774297dac481')) 'Project pixel font assigned in prefab'
Check (([regex]::Matches($prefab,'(?m)^BoxCollider2D:')).Count -eq 3) 'Prefab has gate and two permanent post colliders'
Check ($prefab.Contains('leftPost: {fileID: 202}') -and $prefab.Contains('rightPost: {fileID: 302}')) 'Post references wired'
Write-Output "PASS: $checks waterway gate checks (production state/geometry methods with Unity stubs)."

# Three poses from production clipping/rectangle data; not a Unity screenshot.
Add-Type -AssemblyName System.Drawing
$bitmap=[System.Drawing.Bitmap]::new(960,810)
$g=[System.Drawing.Graphics]::FromImage($bitmap);$g.Clear([System.Drawing.Color]::FromArgb(10,22,42))
$scale=190.0;$cx=480.0
function Paint($x,$y,$w,$h,$r,$green,$b){
 if($h -le 0){return}
 $brush=[System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb([int]($r*255),[int]($green*255),[int]($b*255)))
 $g.FillRectangle($brush,[single]($cx+($x-$w/2)*$scale),[single]($cy-($y+$h/2)*$scale),[single]($w*$scale),[single]($h*$scale));$brush.Dispose()
}
$fonts=[System.Drawing.Text.PrivateFontCollection]::new();$fonts.AddFontFile("$taskRoot/Assets/Fonts/ZLabsPixel_12px_M_CN.ttf")
$font=[System.Drawing.Font]::new($fonts.Families[0],28,[System.Drawing.FontStyle]::Regular,[System.Drawing.GraphicsUnit]::Pixel)
$caption=[System.Drawing.Font]::new('Consolas',15)
$format=[System.Drawing.StringFormat]::new();$format.Alignment=[System.Drawing.StringAlignment]::Center;$format.LineAlignment=[System.Drawing.StringAlignment]::Center
$brush=[System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(224,237,230))
$g.TextRenderingHint=[System.Drawing.Text.TextRenderingHint]::SingleBitPerPixelGridFit
for($state=0;$state -lt 3;$state++){
 $cy=160+260*$state;$lift=@(0,.37,.74)[$state]
 foreach($part in $gate.VisiblePanels(3.4,$lift)){Paint $part.X ($part.Y+$lift) $part.Width $part.Height $part.Color.r $part.Color.g $part.Color.b}
 foreach($part in $frame){Paint $part.X $part.Y $part.Width $part.Height $part.Color.r $part.Color.g $part.Color.b}
 Paint 0 .44 .84 .24 .24 .27 .28;Paint 0 .44 .70 .18 .11 .15 .18
 $g.DrawString('1A',$font,$brush,[System.Drawing.RectangleF]::new(400,($cy-.44*$scale-24),160,48),$format)
 $g.DrawString(@('CLOSED','HALFWAY','OPEN')[$state],$caption,$brush,30,($cy-150))
}
$bitmap.Save("$taskRoot/Docs/WaterwayGate-preview.png",[System.Drawing.Imaging.ImageFormat]::Png)
$brush.Dispose();$format.Dispose();$caption.Dispose();$font.Dispose();$fonts.Dispose();$g.Dispose();$bitmap.Dispose()
