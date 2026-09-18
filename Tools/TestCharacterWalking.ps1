$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$utility = Get-Content -Raw (Join-Path $root 'Assets/Scripts/Characters/PixelWalkFrameUtility.cs')
# Execute the actual production pixel transformation code with value-type stubs.
$stubs = @'
namespace UnityEngine {
    public struct Color32 { public byte r,g,b,a; public Color32(byte r,byte g,byte b,byte a) {this.r=r;this.g=g;this.b=b;this.a=a;} }
    public struct RectInt {
        public int xMin,yMin,xMax,yMax;
        public RectInt(int x,int y,int w,int h) {xMin=x;yMin=y;xMax=x+w;yMax=y+h;}
    }
    public static class Mathf { public static int Clamp(int v,int min,int max) {return System.Math.Max(min,System.Math.Min(max,v));} }
}
public static class WalkPixelTests {
    static bool Same(UnityEngine.Color32 a,UnityEngine.Color32 b) {return a.r==b.r&&a.g==b.g&&a.b==b.b&&a.a==b.a;}
    public static int CheckSwordPose(string[] rows) {
        for(int x=5;x<=10;x++)
            if("ATDS".IndexOf(rows[10][x])<0) throw new System.Exception("Waist must retain its armor colors");
        for(int row=11;row<=12;row++) for(int x=0;x<16;x++)
            if(rows[row][x]!='.'&&rows[row][x]!='K') throw new System.Exception("All leg pixels must use the boot color");
        var src=new UnityEngine.Color32[256];
        for(int row=0;row<16;row++) for(int x=0;x<16;x++)
            if(rows[row][x]!='.') src[(15-row)*16+x]=new UnityEngine.Color32(100,100,100,255);
        int[] bootColumns={5,6,9,10};
        for(int pose=-1;pose<2;pose++) {
            var result=pose<0?src:PixelWalkFrameUtility.BuildStep(src,16,16,new UnityEngine.RectInt(pose==0?5:9,3,2,2));
            foreach(int x in bootColumns) if(result[4*16+x].a==0) throw new System.Exception("Boot lost its connection to the leg");
            for(int i=0;i<3*16;i++) if(result[i].a!=0) throw new System.Exception("Legs extend below the shortened baseline");
            // Check actual four-connected geometry of each full directional sprite.
            // Every opaque pixel (including both boots) must remain connected to the torso.
            var visited=new bool[256]; var pending=new System.Collections.Generic.Queue<int>();
            int seed=8*16+8;
            if(result[seed].a==0) throw new System.Exception("Missing torso seed");
            visited[seed]=true; pending.Enqueue(seed);
            while(pending.Count>0) {
                int i=pending.Dequeue(),x=i%16,y=i/16;
                int[] neighbours={x>0?i-1:-1,x<15?i+1:-1,y>0?i-16:-1,y<15?i+16:-1};
                foreach(int n in neighbours) if(n>=0&&!visited[n]&&result[n].a!=0) { visited[n]=true; pending.Enqueue(n); }
            }
            for(int i=0;i<256;i++) if(result[i].a!=0&&!visited[i]) throw new System.Exception("Detached pixel, pose "+pose+", at "+i%16+","+i/16);
        }
        return 3;
    }
    public static int Run() {
        int[][] cases={new[]{16,16,5,3,2,2,9,3,2,2},new[]{20,20,6,4,3,2,11,4,3,2},
            new[]{16,16,5,3,2,2,9,3,2,2},new[]{16,16,4,1,5,3,9,1,4,3},new[]{32,28,8,2,6,4,18,2,6,4}};
        int count=0;
        foreach(var p in cases) {
            int w=p[0],h=p[1]; var src=new UnityEngine.Color32[w*h];
            for(int i=0;i<src.Length;i++) src[i]=new UnityEngine.Color32((byte)(i%255),(byte)(i/w),0,255);
            var original=(UnityEngine.Color32[])src.Clone();
            for(int pose=0;pose<2;pose++) {
                int j=2+pose*4; var rect=new UnityEngine.RectInt(p[j],p[j+1],p[j+2],p[j+3]);
                var result=PixelWalkFrameUtility.BuildStep(src,w,h,rect);
                for(int y=0;y<h;y++) for(int x=0;x<w;x++) {
                    bool inside=x>=rect.xMin&&x<rect.xMax&&y>=rect.yMin&&y<rect.yMax;
                    var expected=inside?(y==rect.yMin?default(UnityEngine.Color32):src[(y-1)*w+x]):src[y*w+x];
                    if(!Same(expected,result[y*w+x])) throw new System.Exception("Wrong pixel at "+x+","+y);
                    if(!Same(original[y*w+x],src[y*w+x])) throw new System.Exception("Idle frame mutated");
                }
                count++;
            }
            PixelWalkFrameUtility.BuildStep(src,w,h,new UnityEngine.RectInt(-5,-5,w+10,h+10));
        }
        return count;
    }
}
'@
Add-Type -TypeDefinition ($utility + "`n" + $stubs)
$count = [WalkPixelTests]::Run()
$sword = Get-Content -Raw (Join-Path $root 'Assets/Scripts/Characters/SwordZeldaCharacterData.cs')
foreach ($side in @('Front','Back','Left','Right')) {
    $block = [regex]::Match($sword, '(?s)string\[\] '+$side+'Body = \{(.*?)\};').Groups[1].Value
    $rows = [string[]]@([regex]::Matches($block, '"([.A-Z]+)"') | ForEach-Object { $_.Groups[1].Value })
    $count += [WalkPixelTests]::CheckSwordPose($rows)
}
foreach ($file in @('Assets/Scripts/Characters/ZeldaFourWayMover.cs','Assets/Scripts/AI/ZeldaCharacterAiBase.cs')) {
    if (-not (Get-Content -Raw (Join-Path $root $file)).Contains('ApplyCharacterVisualWithMovement(')) { throw "Missing visual entry: $file" }
}
foreach ($name in @('Civilian','Blacksmith','Noble','Prisoner','Behemoth')) {
    if (-not (Get-Content -Raw (Join-Path $root "Assets/Scripts/Characters/${name}ZeldaCharacterData.cs")).Contains('texture.Apply(false, attacking);')) { throw "Idle texture must remain readable: $name" }
}
$animator = Get-Content -Raw (Join-Path $root 'Assets/Scripts/Characters/PixelCharacterWalkAnimator.cs')
if ($animator.Contains('character is GhostZeldaCharacterData') -or $animator.Contains('BuildFloat') -or $utility.Contains('BuildFloat')) { throw 'Ghost must retain its original translation-only movement.' }
if ($animator.Contains('transform.localPosition =') -or $animator.Contains('transform.position =')) { throw 'Animation must not move the character transform.' }
if (-not $animator.Contains('!moving || attacking') -or -not $animator.Contains('lastFrame != Time.frameCount')) { throw 'Animation state guards missing.' }
Write-Output "PASS: $count pixel animation cases, source immutability, both visual entry points, readable idle textures and movement/attack guards."
