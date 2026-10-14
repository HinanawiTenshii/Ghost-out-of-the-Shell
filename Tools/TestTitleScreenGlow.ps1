$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskSource = Get-Content -Raw "$taskRoot/Assets/Scripts/UI/TitleScreenGlow.cs"
# Execute the production mesh modifier against lightweight Unity data stubs.
$taskStubs = @'
namespace UnityEngine {
 public class DisallowMultipleComponent : System.Attribute {}
 public struct Vector2 {
  public float x,y; public Vector2(float x,float y){this.x=x;this.y=y;}
  public static Vector2 operator *(Vector2 v,float n){return new Vector2(v.x*n,v.y*n);}
 }
 public struct Vector3 {public float x,y,z;}
 public struct Color32 {public byte r,g,b,a;}
 public struct UIVertex {public Vector3 position;public Vector2 uv0;public Color32 color;}
 public static class Mathf {public static int RoundToInt(float n){return (int)System.Math.Round(n);}}
 public class GameObject {
  public int adds;public object component;
  public T AddComponent<T>() where T:new(){adds++;var value=new T();component=value;return value;}
 }
}
namespace UnityEngine.UI {
 public class Graphic {
  public UnityEngine.GameObject gameObject=new UnityEngine.GameObject();
  public T GetComponent<T>() where T:class{return gameObject.component as T;}
 }
 public abstract class BaseMeshEffect {
  public bool enabled=true; public bool IsActive(){return enabled;}
  public abstract void ModifyMesh(VertexHelper vertices);
 }
 public class VertexHelper {
  public System.Collections.Generic.List<UnityEngine.UIVertex> data=new System.Collections.Generic.List<UnityEngine.UIVertex>();
  public int currentVertCount {get{return data.Count;}}
  public void GetUIVertexStream(System.Collections.Generic.List<UnityEngine.UIVertex> target){target.AddRange(data);}
  public void Clear(){data.Clear();}
  public void AddUIVertexTriangleStream(System.Collections.Generic.List<UnityEngine.UIVertex> input){data.AddRange(input);}
 }
}
'@
Add-Type -TypeDefinition ($taskSource + $taskStubs)
$checks = 0
function Check($condition, $message) {
    if (!$condition) { throw $message }
    $script:checks++
}
$effect = [TitleScreenGlow]::new()
$mesh = [UnityEngine.UI.VertexHelper]::new()
$original = @()
foreach ($i in 0..5) {
    $v = [UnityEngine.UIVertex]::new()
    # Nested value types must be copied back; direct PowerShell field edits modify a boxed copy.
    $position=$v.position; $position.x = $i % 3; $position.y = [Math]::Floor($i / 3); $v.position=$position
    $uv=$v.uv0; $uv.x = $i / 6.0; $uv.y = 0.75; $v.uv0=$uv
    $color=$v.color; $color.r = 40; $color.g = 130; $color.b = 210
    $color.a = @(255,128,0)[$i % 3]; $v.color=$color
    $original += $v; $mesh.data.Add($v)
}
$effect.ModifyMesh($mesh)
Check ($mesh.currentVertCount -eq 150) 'Three eight-direction rings plus the core'
for ($i = 0; $i -lt 6; $i++) {
    $v = $mesh.data[144 + $i]; $s = $original[$i]
    Check ($v.position.Equals($s.position) -and $v.uv0.Equals($s.uv0) -and $v.color.a -eq $s.color.a) 'Crisp core geometry, alpha and UVs preserved on top'
    Check ($v.color.r -eq [Math]::Round(71.5) -and $v.color.g -eq 172 -and $v.color.b -eq 244) 'Blue core brightened without bleaching to white'
}
for ($copy = 0; $copy -lt 24; $copy++) {
    $radius = if ($copy -lt 8) { 6.0 } elseif ($copy -lt 16) { 3.0 } else { 1.25 }
    $opacity = if ($copy -lt 8) { 0.035 } elseif ($copy -lt 16) { 0.085 } else { 0.16 }
    for ($i = 0; $i -lt 6; $i++) {
        $v = $mesh.data[$copy * 6 + $i]; $s = $original[$i]
        $dx = $v.position.x - $s.position.x; $dy = $v.position.y - $s.position.y
        Check ([Math]::Abs([Math]::Sqrt($dx*$dx+$dy*$dy)-$radius) -lt 0.00001) 'Bounded halo radius'
        Check ($v.uv0.Equals($s.uv0)) 'Texture/font atlas coordinates unchanged'
        Check ($v.color.r -eq 31 -and $v.color.g -eq 142 -and $v.color.b -eq 244) 'Saturated fluorescent blue halo'
        Check ($v.color.a -eq [Math]::Round($s.color.a*$opacity)) 'Stronger halo still respects source transparency'
    }
}
$next = [UnityEngine.UI.VertexHelper]::new()
$next.data.AddRange([UnityEngine.UIVertex[]]$original)
$effect.ModifyMesh($next)
Check ($next.currentVertCount -eq 150) 'Buffers do not accumulate between rebuilds'
foreach ($rgb in @(@(46,71,89),@(255,255,255),@(220,80,30),@(9,30,51))) {
    $muted = [UnityEngine.UI.VertexHelper]::new()
    $originalMuted = [UnityEngine.UIVertex[]]::new(6)
    for ($i=0;$i -lt 6;$i++) {
        $v=$original[$i];$color=$v.color;$color.r=$rgb[0];$color.g=$rgb[1];$color.b=$rgb[2];$v.color=$color
        $originalMuted[$i]=$v;$muted.data.Add($v)
    }
    $effect.ModifyMesh($muted)
    for ($i=0;$i -lt 6;$i++) {
        Check ($muted.data[144+$i].Equals($originalMuted[$i])) 'Disabled, white, warm and dark background cores remain unchanged'
        Check ($muted.data[$i].color.a -eq 0) 'Nonblue accents receive no new outer glow'
        Check ($muted.data[48+$i].color.a -eq [Math]::Round($originalMuted[$i].color.a*.025)) 'Nonblue middle glow retains original opacity'
        Check ($muted.data[96+$i].color.a -eq [Math]::Round($originalMuted[$i].color.a*.055)) 'Nonblue inner glow retains original opacity'
    }
}
$empty = [UnityEngine.UI.VertexHelper]::new(); $effect.ModifyMesh($empty)
Check ($empty.currentVertCount -eq 0) 'Empty text supported'
$effect.enabled = $false
$inactive = [UnityEngine.UI.VertexHelper]::new(); $inactive.data.AddRange([UnityEngine.UIVertex[]]$original)
$effect.ModifyMesh($inactive)
Check ($inactive.currentVertCount -eq 6) 'Disabled effect leaves original mesh intact'
$effect.enabled = $true
$large = [UnityEngine.UI.VertexHelper]::new()
foreach ($i in 1..4000) { $large.data.Add($original[0]) }
$effect.ModifyMesh($large)
Check ($large.currentVertCount -eq 4000) 'Very long text stays within the uGUI vertex budget'
$graphic = [UnityEngine.UI.Graphic]::new()
[TitleScreenGlow]::Attach($graphic); [TitleScreenGlow]::Attach($graphic); [TitleScreenGlow]::Attach($null)
Check ($graphic.gameObject.adds -eq 1) 'Attaching is idempotent and null-safe'
$controller = Get-Content -Raw "$taskRoot/Assets/Scripts/UI/TitleScreenController.cs"
$slots = Get-Content -Raw "$taskRoot/Assets/Scripts/UI/SaveSlotPanel.cs"
Check (([regex]::Matches($controller, 'TitleScreenGlow.Attach\(text\)')).Count -eq 2) 'Title and menu labels are covered'
Check ($controller.Contains('TitleScreenGlow.Attach(image)')) 'Thin frame edges are covered'
Check ($slots.Contains('if (useTitleLayout) TitleScreenGlow.Attach(text);')) 'Legacy explicit title attachment retained; gameplay is covered by the Canvas scope'
Write-Output "PASS: $checks title-screen glow checks (production mesh modifier, Unity data stubs)."
