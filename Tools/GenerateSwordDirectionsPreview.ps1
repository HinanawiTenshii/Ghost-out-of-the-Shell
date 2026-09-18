# Run the production sprite-generation methods against small in-memory Unity stubs.
# This verifies actual attack poses too; no separately reimplemented artwork.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path $PSScriptRoot -Parent
$source = Get-Content -Raw (Join-Path $root 'Assets/Scripts/Characters/SwordZeldaCharacterData.cs')
$names = @('Front','Back','Left','Right')
$maps = @{}
foreach ($name in $names) {
    $block = [regex]::Match($source, '(?s)string\[\] '+$name+'Body = \{(.*?)\};').Groups[1].Value
    $rows = @([regex]::Matches($block, '"([.A-Z]+)"') | ForEach-Object { $_.Groups[1].Value })
    if ($rows.Count -ne 16 -or @($rows | Where-Object { $_.Length -ne 16 }).Count -ne 0) { throw "Bad map: $name" }
    $maps[$name] = $rows
    # No neck: helmet/face sits immediately on a broad, fully opaque shoulder row.
    if ($rows[5].Contains('F') -or $rows[5].Trim('.').Length -lt 6) { throw "Neck in $name" }
    foreach ($x in 6..9) { if ($rows[4][$x] -eq '.' -or $rows[5][$x] -eq '.') { throw "Head/shoulder gap in $name" } }
    foreach ($row in 5..10) { if ($rows[$row].Trim('.').Contains('.')) { throw "Torso gap in $name" } }
}
foreach ($row in 0..15) {
    $reverse=$maps.Left[$row].ToCharArray(); [array]::Reverse($reverse)
    if (($reverse -join '') -ne $maps.Right[$row]) { throw 'Side mirroring mismatch' }
}
if (($maps.Front -join '') -eq ($maps.Back -join '')) { throw 'Front and back must differ' }
$stubs = @'
using System;
using System.Collections.Generic;
namespace UnityEngine {
    public enum TextureFormat { RGBA32 }
    public enum FilterMode { Point }
    public enum TextureWrapMode { Clamp }
    public enum HideFlags { HideAndDontSave }
    public struct Vector2 {
        public float x,y;
        public Vector2(float x,float y) {this.x=x;this.y=y;}
        public static readonly Vector2 up=new Vector2(0,1),down=new Vector2(0,-1),left=new Vector2(-1,0),right=new Vector2(1,0);
        public static bool operator ==(Vector2 a,Vector2 b) {return a.x==b.x&&a.y==b.y;}
        public static bool operator !=(Vector2 a,Vector2 b) {return !(a==b);}
        public override bool Equals(object obj) {return obj is Vector2 && this==(Vector2)obj;}
        public override int GetHashCode() {return x.GetHashCode()^y.GetHashCode();}
    }
    public struct Color {
        public float r,g,b,a;
        public Color(float r,float g,float b,float a) {this.r=r;this.g=g;this.b=b;this.a=a;}
        public static Color clear {get {return new Color();}}
    }
    public struct Rect {public Rect(int x,int y,int w,int h) {}}
    public class Texture2D {
        public string name; public FilterMode filterMode; public TextureWrapMode wrapMode; public HideFlags hideFlags;
        public Color[] pixels;
        public Texture2D(int w,int h,TextureFormat f,bool mip) {pixels=new Color[w*h];}
        public void SetPixels(Color[] p) {pixels=(Color[])p.Clone();}
        public void Apply() {}
    }
    public class Sprite {
        public string name; public HideFlags hideFlags; public Texture2D texture;
        public static Sprite Create(Texture2D t,Rect r,Vector2 p,float ppu) {return new Sprite {texture=t};}
    }
}
public class SwordVisualHarness {
    private bool useYellowArmorHighlights,useMagentaArmorHighlights;
    public UnityEngine.Color[] Render(int variant,int direction,bool attack) {
        useYellowArmorHighlights=variant==1; useMagentaArmorHighlights=variant==2;
        var dirs=new[]{UnityEngine.Vector2.down,UnityEngine.Vector2.up,UnityEngine.Vector2.left,UnityEngine.Vector2.right};
        return CreateCharacterSprite(dirs[direction],attack).texture.pixels;
    }
    public static void CheckConnected(UnityEngine.Color[] p) {
        var seen=new bool[256]; var queue=new Queue<int>(); int seed=8*16+8;
        if(p[seed].a==0) throw new Exception("Missing body centre");
        seen[seed]=true;queue.Enqueue(seed);
        while(queue.Count>0) {
            int i=queue.Dequeue(),x=i%16,y=i/16;
            int[] ns={x>0?i-1:-1,x<15?i+1:-1,y>0?i-16:-1,y<15?i+16:-1};
            foreach(int n in ns) if(n>=0&&!seen[n]&&p[n].a>0) {seen[n]=true;queue.Enqueue(n);}
        }
        for(int i=0;i<256;i++) if(p[i].a>0&&!seen[i]) throw new Exception("Detached pixel at "+i%16+","+i/16);
    }
'@
$start = $source.IndexOf('    private static readonly string[] FrontBody')
$end = $source.IndexOf('    private static Vector2 ToNearestCardinalDirection')
$production = $source.Substring($start,$end-$start)
Add-Type -TypeDefinition ("using UnityEngine;" + $stubs + $production + "}")
$harness = New-Object SwordVisualHarness
# The front breastplate, buckle and lower plates must stay neutral across ranks,
# including attack poses (test the unobscured upper breastplate while attacking).
foreach ($attack in @($false,$true)) {
    $normal=$harness.Render(0,0,$attack)
    foreach ($variant in 1..2) {
        $ranked=$harness.Render($variant,0,$attack)
        $lastRow=if($attack){8}else{10}
        foreach ($row in 6..$lastRow) { foreach ($x in 6..9) {
            $i=(15-$row)*16+$x
            if (-not $normal[$i].Equals($ranked[$i])) {throw 'Front breastplate changed with rank'}
        }}
    }
}
$font = [Drawing.Font]::new('Arial',11)
foreach ($attack in @($false,$true)) {
    $bmp=[Drawing.Bitmap]::new(800,660); $g=[Drawing.Graphics]::FromImage($bmp)
    $g.Clear([Drawing.Color]::FromArgb(24,31,43))
    foreach ($variant in 0..2) { foreach ($d in 0..3) {
        $pixels=$harness.Render($variant,$d,$attack)
        [SwordVisualHarness]::CheckConnected($pixels)
        # Idle/attack must have exactly the expected number of gauntlet pixels.
        $gauntlets=@($pixels | Where-Object { [Math]::Abs($_.r-.60) -lt .001 -and [Math]::Abs($_.g-.64) -lt .001 -and $_.a -gt 0 })
        $expected=if ($d -lt 2) {8} else {4}
        if ($gauntlets.Count -ne $expected) { throw "Duplicate or missing hand in direction $d attack=$attack" }
        $originX=20+$d*200; $originY=20+$variant*220
        foreach ($row in 0..15) { foreach ($x in 0..15) {
            $c=$pixels[(15-$row)*16+$x]
            if ($c.a -le 0) {continue}
            $brush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(255,[int]($c.r*255),[int]($c.g*255),[int]($c.b*255)))
            $g.FillRectangle($brush,($originX+$x*10),($originY+$row*10),10,10); $brush.Dispose()
        }}
        $label=@('SwordGuy','Yellow','Magenta')[$variant]+' / '+$names[$d]
        $g.DrawString($label,$font,[Drawing.Brushes]::White,($originX+10),($originY+175))
    }}
    $file=if($attack){'SwordDirections-attack-preview.png'}else{'SwordDirections-preview.png'}
    $path=Join-Path $root ('Docs/'+$file)
    $bmp.Save($path,[Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    Write-Output "Preview: $path"
}
$font.Dispose()
Write-Output 'PASS: 24 production-rendered poses, four-connected silhouettes, no neck, mirrored profiles, no duplicate attacking hands.'
