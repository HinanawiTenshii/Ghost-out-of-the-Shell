$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$source=Get-Content -Raw "$root/Assets/Scripts/Decorations/GeometricAlchemyDecoration.cs"
function Extract($signature){
 $start=$source.IndexOf($signature);if($start -lt 0){throw "Missing $signature"}
 $b=$source.IndexOf('{',$start);$e=$b+1;$depth=1
 while($depth){if($source[$e] -eq '{'){$depth++};if($source[$e] -eq '}'){$depth--};$e++}
 $source.Substring($start,$e-$start)
}
$stub=@'
using System;
using System.Collections.Generic;
public struct Vector2 {
 public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}
 public static Vector2 zero=>new Vector2();public float magnitude=>(float)Math.Sqrt(x*x+y*y);
 public static Vector2 operator -(Vector2 a,Vector2 b)=>new Vector2(a.x-b.x,a.y-b.y);
 public static Vector2 operator *(Vector2 a,float b)=>new Vector2(a.x*b,a.y*b);
 public static implicit operator Vector2(Vector3 a)=>new Vector2(a.x,a.y);
}
public struct Vector3 {
 public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
 public static Vector3 operator +(Vector3 a,Vector3 b)=>new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);
 public static Vector3 operator *(Vector3 a,float b)=>new Vector3(a.x*b,a.y*b,a.z*b);
 public static float Distance(Vector3 a,Vector3 b)=>(float)Math.Sqrt((a.x-b.x)*(a.x-b.x)+(a.y-b.y)*(a.y-b.y)+(a.z-b.z)*(a.z-b.z));
}
public struct Color{
 public float r,g,b,a;public Color(float r,float g,float b,float a){this.r=r;this.g=g;this.b=b;this.a=a;}
 public static Color white=>new Color(1,1,1,1);
 public static Color Lerp(Color a,Color b,float t)=>new Color(a.r+(b.r-a.r)*t,a.g+(b.g-a.g)*t,a.b+(b.b-a.b)*t,a.a+(b.a-a.a)*t);
}
public static class Mathf{
 public const float PI=(float)Math.PI;public static float Cos(float a)=>(float)Math.Cos(a);public static float Sin(float a)=>(float)Math.Sin(a);
 public static float Abs(float a)=>Math.Abs(a);public static float Clamp01(float a)=>Math.Max(0,Math.Min(1,a));public static float Lerp(float a,float b,float t)=>a+(b-a)*t;
}
public static class Random{
 static System.Random rng=new System.Random(42);public static float value=>(float)rng.NextDouble();
 public static float Range(float a,float b)=>a+(b-a)*value;
 public static Vector2 insideUnitCircle {get{float a=value*Mathf.PI*2;float r=(float)Math.Sqrt(value);return new Vector2(Mathf.Cos(a)*r,Mathf.Sin(a)*r);}}
}
public class Mesh{
 public void Clear(){}public void SetVertices(List<Vector3> a){}public void SetColors(List<Color> a){}
 public void SetUVs(int i,List<Vector2> a){}public void SetTriangles(List<int> a,int i){}public void RecalculateBounds(){}
}
// General affine XY transform includes rotations, nonuniform/mirrored scale and nested-parent shear.
public class Transform{
 public float a=1,b=0,c=0,d=1,tx=4,ty=-2;
 public Vector3 TransformPoint(Vector3 p)=>new Vector3(a*p.x+b*p.y+tx,c*p.x+d*p.y+ty,p.z);
 public Vector3 InverseTransformPoint(Vector3 p){float det=a*d-b*c;float x=p.x-tx,y=p.y-ty;return new Vector3((d*x-b*y)/det,(-c*x+a*y)/det,p.z);}
}
public class Probe{
 Vector2 size=new Vector2(1.6f,1.4f),bubbleRadiusRange=new Vector2(.06875f,.10625f),bubbleGrowthTime=new Vector2(.8f,1.35f);
 float bubbleInterval=.3f,bubblePopDuration=.22f,nextBubble;
 Color accent=new Color(.18f,.85f,.49f,1);
 Transform transform=new Transform(); Mesh bubbleMesh=new Mesh();
 List<Bubble> bubbles=new List<Bubble>();List<Vector3> bubbleVertices=new List<Vector3>();
 List<Color> bubbleColors=new List<Color>();List<int> bubbleTriangles=new List<int>();List<Vector2> bubbleUvs=new List<Vector2>();
 Vector2[] liquidPolygon=new Vector2[12];
 static int checks;static void Check(bool b,string s){checks++;if(!b)throw new Exception(s);}
 public static int Run(){
  float previous=0;for(int i=0;i<=100;i++){float r=GrowingBubbleRadius(i*.01f,1,.08f);Check(r>=previous&&r<=.080001f,"Smooth monotonic growth");previous=r;}
  Check(Math.Abs(previous-.08f)<.000001f,"Reaches maximum before popping");
  var p=new Probe();
  Check(Math.Abs(p.bubbleRadiusRange.x-.055f*1.25f)<.000001f&&Math.Abs(p.bubbleRadiusRange.y-.085f*1.25f)<.000001f,"Size increases exactly 25 percent from original");
  foreach(float[] matrix in new[]{new[]{1f,0f,0f,1f},new[]{3f,0f,0f,3f},new[]{0f,-2f,1f,0f},new[]{-2f,.5f,.3f,1f}}){
   p.transform.a=matrix[0];p.transform.b=matrix[1];p.transform.c=matrix[2];p.transform.d=matrix[3];
   p.bubbleVertices.Clear();p.BubbleDisc(new Vector3(4,-2,-.006f),.08f,Color.white);
   foreach(var v in p.bubbleVertices)Check(Math.Abs(Vector3.Distance(p.transform.TransformPoint(v),new Vector3(4,-2,-.006f))-.08f)<.00001f,"World radius invariant under affine transforms");
   p.bubbles.Clear();p.nextBubble=0;
   for(int frame=0;frame<300;frame++){
    p.TickBubbles(.025f);Check(p.bubbles.Count<=8,"Bounded live bubbles");
    foreach(var b in p.bubbles)Check(HasBubbleRoom(p.BubbleWorldCenter(b.anchor),b.radius*1.9f,p.liquidPolygon),"Pop envelope inside transformed liquid");
   }
   Check(p.bubbles.Count>0,"Still emits under each transform");
  }
  p.bubbles.Clear();p.nextBubble=100;
  p.bubbles.Add(new Bubble{anchor=Vector2.zero,radius=.06f,growTime=1,age=0});
  p.TickBubbles(.5f);Check(p.bubbleVertices.Count==20,"Growing ring uses two discs");
  float age=p.bubbles[0].age;p.TickBubbles(0);Check(p.bubbles[0].age==age,"Pause freezes life cycle");
  p.TickBubbles(.51f);Check(p.bubbleVertices.Count==50,"Maximum transitions to five pop droplets");
  p.TickBubbles(.23f);Check(p.bubbles.Count==0&&p.bubbleVertices.Count==0,"Pop disappears and geometry cleared");
  p.transform.a=.05f;p.transform.b=0;p.transform.c=0;p.transform.d=.05f;p.nextBubble=0;
  for(int frame=0;frame<100;frame++)p.TickBubbles(.03f);
  Check(p.bubbles.Count==0,"Tiny liquid skips emission rather than scaling bubbles");
  p.transform.a=1;p.transform.d=1;p.size=new Vector2(3.2f,2.8f);p.TickBubbles(.5f);
  Check(Math.Abs(p.liquidPolygon[0].x-(4+.4092f*2))<.00001f,"Inspector Size changes spawn region");
  return checks;
 }
'@
$methods=@('    private struct Bubble','    public enum PropKind','    public static Vector2 ReferenceSize(',
 '    private Vector3 BubbleWorldCenter(','    public static bool HasBubbleRoom(',
 '    public static float GrowingBubbleRadius(','    private void TickBubbles(','    private void BubbleDisc(')
Add-Type -TypeDefinition ($stub+(($methods|ForEach-Object {Extract $_}) -join "`n")+'}')
$count=[Probe]::Run()
if($source.Contains('Disc(-.15f, .14f, .085f')){throw 'Old static bubbles still present'}
foreach($guard in @('Application.isPlaying && shape == PropKind.Cauldron && animateBubbles','ReleaseObject(bubbleMesh)','bubbleMesh.MarkDynamic()')){
 if(!$source.Contains($guard)){throw "Missing lifecycle guard: $guard"}
}
Write-Output "PASS: $count production bubble math/lifecycle checks (Unity mesh services substituted)."
