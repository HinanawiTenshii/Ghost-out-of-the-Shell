$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$source=Get-Content -Raw "$root/Assets/Scripts/Characters/ZeldaAttackHitbox.cs"
$map=[regex]::Match($source,'(?s)    private static readonly string\[\] SwordPixels = \{.*?\};').Value
$methods=[regex]::Match($source,'(?s)    private static Sprite CreateSwordSprite\(\).*?(?=    private static void CreateSprites)').Value
if(!$map -or !$methods){throw 'Sword production methods missing'}
$stubs=@'
using System;
using UnityEngine;
namespace UnityEngine {
    public enum FilterMode{Point} public enum TextureWrapMode{Clamp}
    public enum HideFlags{HideAndDontSave} public enum TextureFormat{RGBA32}
    public struct Color {
        public float r,g,b,a;
        public Color(float r,float g,float b,float a){this.r=r;this.g=g;this.b=b;this.a=a;}
        public static Color clear {get{return new Color(0,0,0,0);}}
    }
    public struct Vector2 {public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}}
    public struct Rect {public float width,height;public Rect(float x,float y,float w,float h){width=w;height=h;}}
    public class Texture2D {
        public int width,height; public bool mipmaps;public string name;public FilterMode filterMode;public TextureWrapMode wrapMode;public HideFlags hideFlags;
        Color[] pixels;
        public Texture2D(int w,int h,TextureFormat format,bool mip){width=w;height=h;mipmaps=mip;pixels=new Color[w*h];}
        public void SetPixel(int x,int y,Color c){if(x<0||x>=width||y<0||y>=height)throw new Exception("Out-of-bounds pixel");pixels[y*width+x]=c;}
        public Color GetPixel(int x,int y){return pixels[y*width+x];}public void Apply(){}
    }
    public class Sprite {
        public Texture2D texture;public Rect rect;public Vector2 pivot;public float pixelsPerUnit;public string name;public HideFlags hideFlags;
        public static Sprite Create(Texture2D t,Rect r,Vector2 p,float ppu){return new Sprite{texture=t,rect=r,pivot=p,pixelsPerUnit=ppu};}
    }
}
public static class SwordVisualRegression {
    public static Sprite Build(){return CreateSwordSprite();}
    public static int Run(){
        Sprite s=Build();int checks=0;
        if(s.texture.width!=8||s.texture.height!=16||s.texture.mipmaps||s.texture.filterMode!=FilterMode.Point)throw new Exception("Wrong texture dimensions/filtering");checks++;
        if(s.rect.width/s.pixelsPerUnit!=0.5f||s.rect.height/s.pixelsPerUnit!=1f||s.pivot.x!=0.5f||s.pivot.y!=0.18f)throw new Exception("Original weapon size and grip pivot changed");checks++;
        for(int row=0;row<16;row++){
            if(SwordPixels[row].Length!=8)throw new Exception("Malformed row");checks++;
            for(int x=0;x<8;x++){
                char c=SwordPixels[row][x];var p=s.texture.GetPixel(x,15-row);
                if(".HMGD".IndexOf(c)<0)throw new Exception("Fine-detail palette reintroduced");
                if(c!=SwordPixels[row][7-x])throw new Exception("Asymmetric sword construction");
                if(p.a!=(c=='.'?0f:1f))throw new Exception("Missing/extra opaque pixel");
                if(p.r!=p.g||p.g!=p.b)throw new Exception("Palette must preserve owner tint");checks++;
            }
        }
        if(SwordPixelColor('H').r<=SwordPixelColor('M').r||SwordPixelColor('M').r<=SwordPixelColor('G').r||SwordPixelColor('G').r<=SwordPixelColor('D').r)throw new Exception("Missing blade/guard/grip contrast");checks++;
        return checks;
    }
'@
Add-Type -TypeDefinition ($stubs+"`n"+$map+"`n"+$methods+"`n}")
Write-Output ("Production sword visual: {0} assertions passed." -f [SwordVisualRegression]::Run())
foreach($name in 'SwordZeldaCharacterData','ArabSoldierZeldaCharacterData','PaladinZeldaCharacterData'){
    $data=Get-Content -Raw "$root/Assets/Scripts/Characters/$name.cs"
    if($data -notmatch 'AttackVisualShape => ZeldaAttackVisualShape.Sword;'){throw "Sword family bypasses shared visual: $name"}
}
Add-Type -AssemblyName System.Drawing
$sprite=[SwordVisualRegression]::Build()
$bmp=[Drawing.Bitmap]::new(880,330);$g=[Drawing.Graphics]::FromImage($bmp)
$g.Clear([Drawing.Color]::FromArgb(25,32,43))
$font=[Drawing.Font]::new('Arial',11)
$tints=@(@(1,1,1,1),@(1,0.85,0.2,0.65),@(0.5,0.85,1,0.65),@(0.85,0.64,0.28,0.65))
$labels=@('Neutral geometry','SwordGuy / Arab soldier','Paladin','Advanced Paladin')
foreach($i in 0..3){foreach($row in 0..15){foreach($x in 0..7){
    $c=$sprite.texture.GetPixel($x,15-$row);if($c.a -eq 0){continue};$t=$tints[$i]
    $brush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb([int](255*$c.a*$t[3]),[int](255*$c.r*$t[0]),[int](255*$c.g*$t[1]),[int](255*$c.b*$t[2])))
    $g.FillRectangle($brush,$i*220+46+$x*16,18+$row*16,16,16);$brush.Dispose()
}};$g.DrawString($labels[$i],$font,[Drawing.Brushes]::White,$i*220+15,292)}
$path=Join-Path $root 'Docs/Sword-attack-preview.png';$bmp.Save($path,[Drawing.Imaging.ImageFormat]::Png)
$font.Dispose();$g.Dispose();$bmp.Dispose()
Write-Output "Shared SwordGuy/Arab/Paladin routing verified. Preview: $path"
