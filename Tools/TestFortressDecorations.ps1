$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$source=Get-Content -Raw "$root/Assets/Scripts/Decorations/GeometricFortressDecoration.cs"
function Extract($signature){
 $start=$source.IndexOf($signature);if($start -lt 0){throw "Missing $signature"}
 $b=$source.IndexOf('{',$start);$e=$b+1;$depth=1
 while($depth){if($source[$e] -eq '{'){$depth++};if($source[$e] -eq '}'){$depth--};$e++}
 # Expression-bodied polygon constructors end in a semicolon after their array initializer.
 if($source.Substring($start,$b-$start).Contains('=>')){$e=$source.IndexOf(';',$e)+1}
 $source.Substring($start,$e-$start)
}
$stub=@'
using System;
using System.Collections.Generic;
public struct Vector2{
 public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}
 public float sqrMagnitude=>x*x+y*y;
 public float magnitude=>(float)Math.Sqrt(sqrMagnitude);
 public Vector2 normalized{get{float l=(float)Math.Sqrt(x*x+y*y);return new Vector2(x/l,y/l);}}
 public static Vector2 operator *(Vector2 v,float f)=>new Vector2(v.x*f,v.y*f);
 public static Vector2 operator +(Vector2 a,Vector2 b)=>new Vector2(a.x+b.x,a.y+b.y);
 public static Vector2 operator -(Vector2 a,Vector2 b)=>new Vector2(a.x-b.x,a.y-b.y);
 public static float Dot(Vector2 a,Vector2 b)=>a.x*b.x+a.y*b.y;
}
public struct Vector3{public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}}
public struct Color32{
 public byte r,g,b,a;
 public static implicit operator Color32(Color c)=>new Color32{r=(byte)(c.r*255),g=(byte)(c.g*255),b=(byte)(c.b*255),a=(byte)(c.a*255)};
}
public struct Color{
 public float r,g,b,a;public Color(float r,float g,float b,float a){this.r=r;this.g=g;this.b=b;this.a=a;}
 public static Color white=>new Color(1,1,1,1);
 public static Color Lerp(Color a,Color b,float t)=>new Color(a.r+(b.r-a.r)*t,a.g+(b.g-a.g)*t,a.b+(b.b-a.b)*t,a.a+(b.a-a.a)*t);
}
public static class Mathf{
 public static float Clamp(float x,float a,float b)=>Math.Max(a,Math.Min(b,x));public static float Max(float a,float b)=>Math.Max(a,b);public static float Sqrt(float x)=>(float)Math.Sqrt(x);public const float PI=(float)Math.PI;public static float Sin(float x)=>(float)Math.Sin(x);public static float Cos(float x)=>(float)Math.Cos(x);
 public static float Floor(float v)=>(float)Math.Floor(v);
 public static float Min(float a,float b)=>Math.Min(a,b);
 public static float Repeat(float v,float length)=>v-Floor(v/length)*length;
 public static float SmoothStep(float a,float b,float t){t=Clamp(t,0,1);return a+(b-a)*t*t*(3-2*t);}
}
public class FortressProbe{
'@
$methods=@('    public enum FurnitureShape','    public struct Part','    public static Vector2 ReferenceSize(',
 '    public static List<Part> BuildRuneGlowParts(','    public static Vector2 ExpandGlowVertex(',
 '    public static float RuneBreathMultiplier(','    public static List<Part> BuildRuneGlyphParts(',
 '    public static List<Part> BuildParts(','    private static void Crate(','    private static void Beam(',
 '    private static Part Rect(','    private static Part Diamond(','    private static Part Disc(',
 '    public static void WriteFireSurface(')
$fireFields=([regex]::Matches($source,'(?s)private static readonly Vector2\[\] Fire(?:Centers|Sizes)\s*=.*?;') | ForEach-Object {$_.Value}) -join "`n"
Add-Type -TypeDefinition ($stub+$fireFields+(($methods|ForEach-Object {Extract $_}) -join "`n")+'}')
$checks=0
function Check($ok,$message){if(!$ok){throw $message};$script:checks++}
$names=@('Fortress-WarTable','Fortress-Ballista','Fortress-SupplyPallet','Fortress-Well','Fortress-Brazier','Fortress-RuneSeal')
$wood=[Color]::new(.67,.39,.20,1);$stone=[Color]::new(.67,.71,.74,1);$accent=[Color]::new(.18,.85,.49,1)
Add-Type -AssemblyName System.Drawing
$bmp=[Drawing.Bitmap]::new(1260,790);$g=[Drawing.Graphics]::FromImage($bmp)
$g.Clear([Drawing.Color]::FromArgb(24,31,43));$font=[Drawing.Font]::new('Arial',16)
$counts=@()
for($i=0;$i -lt 6;$i++){
 $accentValues=@(@(.14,.53,.68),@(.22,.57,.56),@(.22,.57,.56),@(.09,.49,.71),@(1,.56,.12),@(1,.14,.08))[$i]
 $accent=[Color]::new($accentValues[0],$accentValues[1],$accentValues[2],1)
 $kind=[FortressProbe+FurnitureShape]$i;$parts=[FortressProbe]::BuildParts($kind,$wood,$stone,$accent)
 $size=[FortressProbe]::ReferenceSize($kind);$verts=0
 $ox=210+420*($i%3);$oy=185+390*[Math]::Floor($i/3)
 foreach($part in $parts){
  $verts+=$part.Points.Length
  Check ($part.Points.Length -ge 3) 'Polygon needs vertices'
  $pts=[Drawing.PointF[]]::new($part.Points.Length)
  for($v=0;$v -lt $part.Points.Length;$v++){
   $p=$part.Points[$v]
   Check ([Math]::Abs($p.x) -le $size.x/2+.00001 -and [Math]::Abs($p.y) -le $size.y/2+.00001) "Geometry outside centered bounds: $kind"
   $pts[$v]=[Drawing.PointF]::new($ox+$p.x*140,$oy-$p.y*140)
  }
  for($v=1;$v -lt $part.Points.Length-1;$v++){
   $a=$part.Points[0];$b=$part.Points[$v];$c=$part.Points[$v+1]
   Check ((($b.x-$a.x)*($c.y-$a.y)-($b.y-$a.y)*($c.x-$a.x)) -gt 0.000001) "Nonconvex/degenerate polygon: $kind"
  }
  $c=$part.Color;Check ($c.a -eq 1) 'Solid vertex colors'
  $brush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(255,[int]($c.r*255),[int]($c.g*255),[int]($c.b*255)))
  $g.FillPolygon($brush,$pts);$brush.Dispose()
 }
 Check ($verts -lt 300) "Keep mesh compact: $kind ($verts vertices)"
 $counts+="$kind=$verts"
 if($i -eq 4){
  # Fire has moved out of the static body mesh; show its actual lifecycle pose here.
  $fire=[Vector3[]]::new(20);$fireColors=[Color32[]]::new(20)
  [FortressProbe]::WriteFireSurface(0,[Vector2]::new(1,1),$accent,$fire,$fireColors)
  for($f=0;$f -lt 5;$f++){
   $pts=[Drawing.PointF[]]::new(4)
   for($v=0;$v -lt 4;$v++){$p=$fire[$f*4+$v];$pts[$v]=[Drawing.PointF]::new($ox+$p.x*140,$oy-$p.y*140)}
   $c=$fireColors[$f*4];$brush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb($c.a,$c.r,$c.g,$c.b))
   $g.FillPolygon($brush,$pts);$brush.Dispose()
  }
 }
 $path="$root/Assets/Prefabs/Decorations/Fortress/$($names[$i]).prefab"
 $prefab=Get-Content -Raw $path
 Check ($prefab.Contains('guid: 8cfbc9f0ba524b4e92007596a37fba52') -and $prefab.Contains("shape: $i")) 'Correct visual component and kind'
 Check ($prefab.Contains('sortingOrder: -3') -and $prefab.Contains('m_Layer: 0')) 'Existing furniture render convention'
 Check ($prefab.Contains('BoxCollider2D:') -and $prefab.Contains('m_IsTrigger: 0')) 'Single simple collider'
 $solid=if($i -eq 5){0}else{1}
 Check ($prefab.Contains("solid: $solid")) 'Only floor rune seal defaults to non-solid'
 $colliderBlock=[regex]::Match($prefab,'(?s)BoxCollider2D:(.*?)---').Groups[1].Value
 Check ($colliderBlock.Contains("m_Enabled: $solid")) 'Serialized collider matches Solid setting'
 $prefabSize=[regex]::Match($prefab,'m_Size: \{x: ([\d.]+), y: ([\d.]+)\}')
 Check ([Math]::Abs([double]::Parse($prefabSize.Groups[1].Value,[cultureinfo]::InvariantCulture)-$size.x) -lt .00001 -and [Math]::Abs([double]::Parse($prefabSize.Groups[2].Value,[cultureinfo]::InvariantCulture)-$size.y) -lt .00001) 'Collider dimensions match rendered bounds'
 Check ($prefab.Contains('m_LocalPosition: {x: 0, y: 0, z: 0}')) 'Centered root'
 Check ((Get-Content -Raw "$path.meta") -match 'guid: [a-f0-9]{32}\s') 'Valid prefab GUID'
 $g.DrawString($names[$i],$font,[Drawing.Brushes]::White,$ox-120,$oy+130)
}
Check ($source.Contains('visual.AddComponent<MeshRenderer>()') -and !$source.Contains('AddComponent<SpriteRenderer>')) 'Single combined mesh, no per-detail renderers'
Check ($source.Contains('if (dirty || visual == null) Rebuild()')) 'No steady-state per-frame mesh regeneration'
$glow=[FortressProbe]::BuildRuneGlowParts([Color]::new(1,.14,.08,1))
Check ($glow.Count -eq 24) 'Glow isolates ring, diamond border and eight runic strokes'
foreach($part in $glow){
 for($i=0;$i -lt $part.Points.Length;$i++){
  $a=$part.Points[$i];$b=$part.Points[($i+1)%$part.Points.Length]
  $expanded=[FortressProbe]::ExpandGlowVertex($part.Points,$i,.075)
  $distance=(($b.x-$a.x)*($expanded.y-$a.y)-($b.y-$a.y)*($expanded.x-$a.x))/[Math]::Sqrt(($b.x-$a.x)*($b.x-$a.x)+($b.y-$a.y)*($b.y-$a.y))
  Check ([Math]::Abs($distance+.075) -lt .00001) 'Uniform outward glow band around each red segment'
 }
 Check ($part.Color.r -eq 1 -and $part.Color.g -lt .2) 'Magic glow remains warm red'
}
foreach($token in @('ParticleSystemShapeType.Circle','main.maxParticles = 32','main.useUnscaledTime = false','ParticleSystemRenderMode.Mesh','ParticleSystemSimulationSpace.Local','ParticleSystemStopBehavior.StopEmittingAndClear','ReleaseObject(effectMesh)','ReleaseObject(effectMaterial)','Resources.Load<Shader>("WarmLightGlow")')){
 Check ($source.Contains($token)) "Effects lifecycle/config: $token"
}
$runePrefab=Get-Content -Raw "$root/Assets/Prefabs/Decorations/Fortress/Fortress-RuneSeal.prefab"
Check ($runePrefab.Contains('accentColor: {r: 1, g: 0.14, b: 0.08, a: 1}')) 'Serialized red rune color'
Check ($runePrefab.Contains('runeGlowIntensity: 1.6')) 'Serialized local glow enabled'
for($i=0;$i -le 200;$i++){
 $t=$i*.032;$value=[FortressProbe]::RuneBreathMultiplier($t,3.2,.35)
 Check ($value -ge .64999 -and $value -le 1.35001) 'Breathing stays visible and within intensity bounds'
 Check ([Math]::Abs($value-[FortressProbe]::RuneBreathMultiplier($t+3.2,3.2,.35)) -lt .00001) 'Breathing repeats smoothly'
}
Check ([Math]::Abs([FortressProbe]::RuneBreathMultiplier(.8,3.2,.35)-1.35) -lt .00001) 'Breathing peak at quarter-cycle'
Check ([Math]::Abs([FortressProbe]::RuneBreathMultiplier(2.4,3.2,.35)-.65) -lt .00001) 'Breathing trough at three-quarter-cycle'
Check ([FortressProbe]::RuneBreathMultiplier(5,3.2,0) -eq 1) 'Zero depth preserves steady glow'
$glyphKeys=@()
foreach($variant in 0..2){
 $parts=[FortressProbe]::BuildRuneGlyphParts($variant,[Color]::new(1,.14,.08,1))
 Check ($parts.Count -eq 3) 'Coarse three-stroke rune glyph'
 $glyphKeys+=($parts|ForEach-Object {($_.Points|ForEach-Object {"$($_.x),$($_.y)"}) -join ';'}) -join '|'
 foreach($part in $parts){
  for($i=0;$i -lt $part.Points.Length;$i++){
   $p=$part.Points[$i];$expanded=[FortressProbe]::ExpandGlowVertex($part.Points,$i,.09)
   Check ([Math]::Abs($expanded.x) -lt .6 -and [Math]::Abs($expanded.y) -lt .6) 'Particle halo stays small and finite'
  }
 }
}
Check (@($glyphKeys|Select-Object -Unique).Count -eq 3) 'Three distinct rune shapes'
foreach($token in @('runeBreathTime += Time.deltaTime','main.maxParticles=16','area.radiusThickness=.12f','renderer.SetMeshes(runeGlyphMeshes)','renderer.sharedMaterial=effectMaterial','ReleaseObject(runeGlyphMeshes[i])')){
 Check ($source.Contains($token)) "Rune particle animation/lifecycle: $token"
}
$warBody=[regex]::Match($source,'(?s)case FurnitureShape.WarTable:\s*p.Add(.*?)case FurnitureShape.Ballista:').Groups[1].Value
Check (!$warBody.Contains('Diamond(') -and $warBody.Contains('Abstract ink strokes')) 'Map has abstract strokes without detailed unit markers'
$particleBody=Extract '    private void BuildRuneParticles('
foreach($token in @('main.startColor=Color.white','BuildRuneGlyphParts(i,accentColor)','renderer.sharedMaterial=effectMaterial','renderer.SetActiveVertexStreams','ParticleSystemVertexStream.Color','ParticleSystemVertexStream.UV')){
 Check ($particleBody.Contains($token)) "Explicit red particle color pipeline: $token"
}
$meshMethod=Extract '    private static Mesh CreateGlowMesh('
Check ($meshMethod.Contains('result.colors32=packedColors') -and !$meshMethod.Contains('SetColors(colors)')) 'Mesh particles use packed RGBA8 instead of misinterpreted float colors'
Check ($meshMethod.Contains('packedColors[i]=colors[i]')) 'RGB and feather alpha converted together'
$preview=Join-Path $root 'Docs/Fortress-preview.png'
$bmp.Save($preview,[Drawing.Imaging.ImageFormat]::Png);$g.Dispose();$font.Dispose();$bmp.Dispose()
Write-Output "PASS: $checks geometry/prefab checks. Mesh vertices: $counts. Preview: $preview"
