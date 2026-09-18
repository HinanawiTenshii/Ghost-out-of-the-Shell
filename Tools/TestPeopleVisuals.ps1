# Execute production pixel generation and walking code with lightweight Unity data stubs.
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root=Split-Path $PSScriptRoot -Parent
$stubs=@'
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
        public static Color black {get {return new Color(0,0,0,1);}}
        public static Color white {get {return new Color(1,1,1,1);}}
        public static Color Lerp(Color a,Color b,float t) {return new Color(a.r+(b.r-a.r)*t,a.g+(b.g-a.g)*t,a.b+(b.b-a.b)*t,a.a+(b.a-a.a)*t);}
        public static Color clear {get {return new Color();}}
    }
    public struct Rect {public Rect(int x,int y,int w,int h) {}}
    public class Texture2D {
        public string name; public FilterMode filterMode; public TextureWrapMode wrapMode; public HideFlags hideFlags;
        public Color[] pixels;
        public Texture2D(int w,int h,TextureFormat f,bool mip) {pixels=new Color[w*h];}
        public void SetPixels(Color[] p) {pixels=(Color[])p.Clone();}
        public void Apply(bool mip, bool discard) {}
    }
    public class Sprite {
        public string name; public HideFlags hideFlags; public Texture2D texture;
        public static Sprite Create(Texture2D t,Rect r,Vector2 p,float ppu) {return new Sprite {texture=t};}
    }
}

namespace UnityEngine {
    public struct Color32 {
        public byte r,g,b,a;
        public Color32(byte r,byte g,byte b,byte a) {this.r=r;this.g=g;this.b=b;this.a=a;}
        public static implicit operator Color32(Color c) {return new Color32((byte)(c.r*255),(byte)(c.g*255),(byte)(c.b*255),(byte)(c.a*255));}
        public static implicit operator Color(Color32 c) {return new Color(c.r/255f,c.g/255f,c.b/255f,c.a/255f);}
    }
    public struct RectInt {
        public int xMin,yMin,xMax,yMax;
        public RectInt(int x,int y,int w,int h) {xMin=x;yMin=y;xMax=x+w;yMax=y+h;}
    }
    public static class Mathf {public static int Clamp(int v,int min,int max) {return Math.Max(min,Math.Min(max,v));}}
}
public class PeopleVisualBase {
    public void SetColor(string name,float r,float g,float b) {
        GetType().GetField(name,System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(this,new UnityEngine.Color(r,g,b,1));
    }
    public void SetCrown(bool value) {
        GetType().GetField("showCrown",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(this,value);
    }
    public static UnityEngine.Color[] Walk(UnityEngine.Color[] p,bool noble,int step) {
        var src=new UnityEngine.Color32[256];
        for(int i=0;i<256;i++) src[i]=p[i];
        var rect=noble ? new UnityEngine.RectInt(step==0?4:9,1,step==0?5:4,3) : new UnityEngine.RectInt(step==0?5:9,3,2,2);
        var result=PixelWalkFrameUtility.BuildStep(src,16,16,rect);
        var output=new UnityEngine.Color[256]; for(int i=0;i<256;i++) output[i]=result[i];
        return output;
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
}

'@
$code='using UnityEngine;'+$stubs
foreach($name in @('Civilian','Prisoner','Noble')) {
    $source=Get-Content -Raw (Join-Path $root "Assets/Scripts/Characters/$($name)ZeldaCharacterData.cs")
    $maps=@{}
    foreach($d in @('Front','Back','Left','Right')) {
        $block=[regex]::Match($source,'(?s)string\[\] '+$d+'Body = \{(.*?)\};').Groups[1].Value
        $rows=[string[]]@([regex]::Matches($block,'"([.A-Z]+)"') | ForEach-Object {$_.Groups[1].Value})
        if($rows.Count -ne 16 -or @($rows | Where-Object {$_.Length -ne 16}).Count) {throw "Invalid map $name $d"}
        foreach($x in 6..9) {if($rows[4][$x] -eq '.' -or $rows[5][$x] -eq '.') {throw "Neck/gap $name $d"}}
        if($rows[5].Contains('F') -or $rows[5].Trim('.').Length -lt 6) {throw "Neck $name $d"}
        $maps[$d]=$rows
        if($name -ne 'Noble' -and $rows[13] -ne '................') {throw "Legs not shortened $name $d"}
        if($name -ne 'Noble') {
            foreach($row in 10..12) {if($rows[$row] -match '[^K.]') {throw "Leg color must match boots $name $d"}}
        }
    }
    foreach($row in 0..15) {
        $reverse=$maps.Left[$row].ToCharArray(); [Array]::Reverse($reverse)
        if(($reverse -join '') -ne $maps.Right[$row]) {throw "Asymmetric profile $name"}
    }
    $fields=([regex]::Matches($source,'private Color \w+\s*=\s*new Color\([^;]+;') | ForEach-Object {$_.Value}) -join [Environment]::NewLine
    if($name -eq 'Noble') {$fields+=[Environment]::NewLine+'private bool showCrown;'}
    $eye=[regex]::Match($source,'public override Color GhostFormEyeColor => ([^;]+);').Groups[1].Value
    $start=$source.IndexOf('    private static readonly string[] FrontBody')
    $end=$source.IndexOf('    private static int DirectionIndex')
    $production=$source.Substring($start,$end-$start)
    $code+=@"
public class $($name)VisualHarness : PeopleVisualBase {
$fields
public Color GhostFormEyeColor {get {return $eye;}}
public Color[] Render(int direction,bool attack) {
    var dirs=new[]{Vector2.down,Vector2.up,Vector2.left,Vector2.right};
    return CreateTexture(dirs[direction],attack).pixels;
}
$production
}
"@
    if($name -ne 'Prisoner' -and (-not $source.Contains('visualsDirty = true;') -or -not $source.Contains('animator.InvalidateFrames();'))) {throw "Missing color refresh $name"}
}
$utility=Get-Content -Raw (Join-Path $root 'Assets/Scripts/Characters/PixelWalkFrameUtility.cs')
$code+=$utility.Replace('using UnityEngine;','')
Add-Type -TypeDefinition $code -CompilerOptions '/nowarn:0414,0649'
$people=@((New-Object CivilianVisualHarness),(New-Object PrisonerVisualHarness),(New-Object NobleVisualHarness),(New-Object NobleVisualHarness))
$king=Get-Content -Raw (Join-Path $root 'Assets/Prefabs/Level1NPCS/King.prefab')
foreach($field in @('robeColor','robeTrimColor')) {
    $rgb=@(foreach($channel in @('r','g','b')) {
        $value=[regex]::Match($king,'propertyPath: '+$field+'\.'+$channel+'\s+value: ([\d.]+)').Groups[1].Value
        if(-not $value) {throw "Missing King override $field.$channel"}
        [float]::Parse($value,[Globalization.CultureInfo]::InvariantCulture)
    })
    $people[3].SetColor($field,$rgb[0],$rgb[1],$rgb[2])
}
$people[3].SetCrown($true)
$labels=@('Civilian','Prisoner','Noble','King')
$font=[Drawing.Font]::new('Arial',11)
$count=0
foreach($pose in @('Idle','Attack','Walk0','Walk1')) {
    $bmp=[Drawing.Bitmap]::new(800,840); $g=[Drawing.Graphics]::FromImage($bmp)
    $g.Clear([Drawing.Color]::FromArgb(24,31,43))
    foreach($kind in 0..3) {foreach($d in 0..3) {
        $p=$people[$kind].Render($d,($pose -eq 'Attack'))
        if($pose.StartsWith('Walk')) {$p=[PeopleVisualBase]::Walk($p,($kind -ge 2),[int]::Parse($pose.Substring(4)))}
        [PeopleVisualBase]::CheckConnected($p); $count++
        if($pose -eq 'Attack') {
            $skin=$people[$kind].Render(0,$false)[(15-4)*16+7]
            $oldX=if($d -eq 0){4}elseif($d -eq 1){10}else{7}
            foreach($row in 8..9) {foreach($x in $oldX..($oldX+1)) {
                if($p[(15-$row)*16+$x].Equals($skin)) {throw "Duplicate attack hand $kind $d"}
            }}
        }
        $ox=20+$d*200; $oy=10+$kind*210
        foreach($row in 0..15) {foreach($x in 0..15) {
            $c=$p[(15-$row)*16+$x]; if($c.a -le 0) {continue}
            $brush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(255,[int]($c.r*255),[int]($c.g*255),[int]($c.b*255)))
            $g.FillRectangle($brush,($ox+$x*10),($oy+$row*10),10,10); $brush.Dispose()
        }}
        $g.DrawString(($labels[$kind]+' / '+@('Front','Back','Left','Right')[$d]),$font,[Drawing.Brushes]::White,($ox+10),($oy+173))
    }}
    $path=Join-Path $root "Docs/People-$pose-preview.png"
    $bmp.Save($path,[Drawing.Imaging.ImageFormat]::Png); $g.Dispose(); $bmp.Dispose()
    Write-Output "Preview: $path"
}
foreach($kind in @(0,2)) {
    $fields=if($kind -eq 0){@('clothingColor')}else{@('robeColor','robeTrimColor')}
    foreach($field in $fields) {
    $changed=if($kind -eq 0){New-Object CivilianVisualHarness}else{New-Object NobleVisualHarness}
    $changed.SetColor($field,.72,.20,.24)
    foreach($d in 0..3) {foreach($attack in @($false,$true)) {
        $a=$people[$kind].Render($d,$attack); $b=$changed.Render($d,$attack)
        $different=@(0..255 | Where-Object {-not $a[$_].Equals($b[$_])}).Count
        if($different -lt 5) {throw "Clothing not recolored $field $kind $d attack=$attack"}
        foreach($i in (14*16)..255) {if(-not $a[$i].Equals($b[$i])) {throw 'Clothing edit affected hair'}}
        if(-not $attack) {
            foreach($i in (11*16)..255) {if(-not $a[$i].Equals($b[$i])) {throw 'Clothing edit affected face'}}
        }
    }}
    }
}
$font.Dispose()
Write-Output "PASS: $count idle/attack/walk poses, connectivity, no neck, symmetric profiles, no duplicate hands, clothing recolor and cache invalidation."
