$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$candle=Get-Content -Raw "$root/Assets/Scripts/Decorations/PixelWallCandleVisual.cs"
$lamp=Get-Content -Raw "$root/Assets/Scripts/Rendering/KeroseneStreetLamp.cs"
function Extract($source,$signature){
 $start=$source.IndexOf($signature);if($start -lt 0){throw "Missing $signature"}
 $begin=$source.IndexOf('{',$start);$end=$begin+1;$depth=1
 while($depth){if($source[$end] -eq '{'){$depth++};if($source[$end] -eq '}'){$depth--};$end++}
 $source.Substring($start,$end-$start).Replace('private static','public static')
}
$stubs=@'
using System;
public enum TextureFormat {RGBA32} public enum FilterMode {Point,Bilinear} public enum TextureWrapMode {Clamp} public enum SpriteMeshType {FullRect} public enum HideFlags {HideAndDontSave}
public struct Color {public float r,g,b,a;public Color(float r,float g,float b,float a){this.r=r;this.g=g;this.b=b;this.a=a;}public static Color white=>new Color(1,1,1,1);}
public struct Vector2 {public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}public static float Distance(Vector2 a,Vector2 b)=>(float)Math.Sqrt((a.x-b.x)*(a.x-b.x)+(a.y-b.y)*(a.y-b.y));}
public struct Vector3 {public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}public static Vector3 operator *(Vector3 p,float f)=>new Vector3(p.x*f,p.y*f,p.z*f);}
public struct Rect {public Rect(float x,float y,float w,float h){}}
public static class Mathf {public const float PI=(float)Math.PI;public static float Sin(float x)=>(float)Math.Sin(x);public static float Cos(float x)=>(float)Math.Cos(x);public static float Clamp01(float x)=>Math.Max(0,Math.Min(1,x));}
public class Texture2D {public int width,height;public string name;public FilterMode filterMode;public TextureWrapMode wrapMode;public Color[] pixels;public Texture2D(int w,int h,TextureFormat format,bool mip){width=w;height=h;pixels=new Color[w*h];}public void SetPixel(int x,int y,Color c){pixels[y*width+x]=c;}public void Apply(bool mip,bool unreadable){}}
public class Sprite {public string name;public Texture2D texture;public float ppu;public static Sprite Create(Texture2D t,Rect r,Vector2 p,float ppu,uint extrude,SpriteMeshType mesh)=>new Sprite{texture=t,ppu=ppu};}
public class Mesh {public string name;public HideFlags hideFlags;public Vector3[] vertices;public Vector2[] uv;public Color[] colors;public int[] triangles;public void RecalculateBounds(){}}
public static class GlowHarness {
'@
$code=$stubs+(Extract $lamp 'private static Mesh CreateDisc(')+'}'
Add-Type -TypeDefinition $code
$count=0
function Check($ok,$label){if(!$ok){throw $label};$script:count++}
$mesh=[GlowHarness]::CreateDisc()
Check ($mesh.vertices.Length -eq 129 -and $mesh.uv.Length -eq 129) 'Lamp glow keeps original geometry count and adds complete UVs'
for($i=0;$i -lt $mesh.vertices.Length;$i++){
 Check ([Math]::Abs($mesh.uv[$i].x-(.5+.5*$mesh.vertices[$i].x)) -lt .00001 -and [Math]::Abs($mesh.uv[$i].y-(.5+.5*$mesh.vertices[$i].y)) -lt .00001) 'UV radial falloff stays centered on lamp mesh'
}
foreach($i in $mesh.triangles){Check ($i -ge 0 -and $i -lt $mesh.vertices.Length) 'Mesh indices valid'}
$shader=Get-Content -Raw "$root/Assets/Resources/WarmLightGlow.shader"
Check ($shader.Contains('Blend One One') -and $shader.Contains('color.rgb * color.a * _Intensity')) 'Correct premultiplied additive glow'
Check ($shader.Contains('ZTest LEqual') -and $shader.Contains('"Queue"="Transparent"') -and !$shader.Contains('ZTest Always')) 'Glow uses normal world depth and queue, not overlay'
Check ($lamp.Contains('sortingOrder - 2, glowMaterial') -and $lamp.Contains('sortingOrder - 1, glowMaterial')) 'Lamp retains two halos below its body'
Check ($lamp.Contains('Dispose(glowMaterial)') -and $candle.Contains('Dispose(glowMaterial)')) 'Owned glow materials cleaned up'
Check ($lamp.Contains('renderer.enabled = lightOn;') -and $lamp.Contains('baseCollider.size = new Vector2(0.42f, 0.22f);')) 'Lamp switch and base collision unchanged'
Check ($candle.Contains('SampleFlame(') -and $candle.Contains('flameMesh.colors32 = flameColors;')) 'Wall candle animates a compact stepped contour mesh'
Check (!$candle.Contains('CreateGlowSprite(') -and !$candle.Contains('glowRadiusMultiplier')) 'Old large wall-candle light pools removed'
Check ($candle.Contains('new Color(1, .36f, .05f, opacity)') -and $candle.Contains('new Color(1, .96f, .58f, opacity)')) 'Original warm flame palette retained'
foreach($source in @($lamp,$candle)){Check ($source.Contains('Resources.Load<Shader>("WarmLightGlow")')) 'Both props load build-retained warm glow shader'}
foreach($file in @('KeroseneStreetLamp','PixelWallCandle','PixelGoldWallCandle')){
 $prefab=Get-Content -Raw "$root/Assets/Prefabs/Decorations/$file.prefab"
 Check ($prefab.Contains('glowIntensity:')) 'Each prefab explicitly configures glow intensity'
 if($file -eq 'KeroseneStreetLamp'){Check ($prefab.Contains('glowRadiusMultiplier:')) 'Street lamp large-radius light unchanged'}
 else {Check ($prefab.Contains('flameGlowWidth:') -and !$prefab.Contains('glowRadiusMultiplier:')) 'Wall candle configures only local flame bloom'}
}
Write-Output "PASS: $count decorative glow mesh/integration checks; wall-candle GPU animation coverage is in WallCandleSmoke.cs."
