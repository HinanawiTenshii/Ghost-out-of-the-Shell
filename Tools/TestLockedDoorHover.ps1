$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$source=Get-Content -Raw "$root/Assets/Scripts/Map/RuntimeMiniMapGraphic.cs"
$methods=[regex]::Match($source,'(?s)    internal static void GetLockedDoorLineGeometry.*?(?=    public void RebuildFromScene)').Value
if(!$methods){throw 'Missing shared door rendering/hit geometry'}
$fixture=@'
using System;
public struct Vector2 {
 public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}
 public static Vector2 zero {get{return new Vector2();}}
 public float sqrMagnitude {get{return x*x+y*y;}}public float magnitude {get{return (float)Math.Sqrt(sqrMagnitude);}}
 public Vector2 normalized {get{return magnitude>0 ? this*(1/magnitude):zero;}}
 public static Vector2 operator +(Vector2 a,Vector2 b){return new Vector2(a.x+b.x,a.y+b.y);}
 public static Vector2 operator -(Vector2 a,Vector2 b){return new Vector2(a.x-b.x,a.y-b.y);}
 public static Vector2 operator *(Vector2 a,float b){return new Vector2(a.x*b,a.y*b);}
}
public static class Mathf {
 public static float Max(float a,float b){return Math.Max(a,b);}
 public static float Atan2(float y,float x){return (float)Math.Atan2(y,x);}
 public const float Rad2Deg=57.29578f;
}
public class MapPointOfInterestRecord {public Vector2 LineStart,LineEnd;}
public class DoorHoverFixture {
 public float iconScale=1,zoom=1;public bool hasBounds=true;public Vector2 pan;
 public bool TryWorldToLocal(Vector2 p,out Vector2 result){result=p*zoom+pan;return hasBounds;}
 // METHODS
 static int checks;
 static void Check(bool b,string s){checks++;if(!b)throw new Exception(s);}
 public static int Run(){
  foreach(float angle in new[]{0f,45f,90f,135f,180f,270f})
  foreach(float length in new[]{.1f,8f,100f})
  foreach(float zoom in new[]{.4f,1f,5f}){
   double rad=angle*Math.PI/180;var direction=new Vector2((float)Math.Cos(rad),(float)Math.Sin(rad));
   var door=new MapPointOfInterestRecord{LineStart=new Vector2(10,20),LineEnd=new Vector2(10,20)+direction*length};
   var f=new DoorHoverFixture{zoom=zoom,pan=new Vector2(-35,20)};
   Vector2 center,size;float degrees;
   Check(f.TryGetLockedDoorHoverRect(door,out center,out size,out degrees),"Door hit rect available");
   Vector2 expected=(door.LineStart+door.LineEnd)*(.5f*zoom)+f.pan;
   Check((center-expected).magnitude<.001f,"Hover follows pan and zoom");
   Check(size.y>=14 && size.x>=17.5f-.001f,"Thin and distant doors have usable hitboxes");
   Vector2 hitDirection=new Vector2((float)Math.Cos(degrees*Math.PI/180),(float)Math.Sin(degrees*Math.PI/180));
   Check((direction-hitDirection).magnitude<.001f,"Hover follows door rotation");
   Vector2 a,b;f.TryWorldToLocal(door.LineStart,out a);f.TryWorldToLocal(door.LineEnd,out b);
   Vector2 lineCenter,half;float width;GetLockedDoorLineGeometry(a,b,f.iconScale,out lineCenter,out half,out width);
   Check((center-lineCenter).magnitude<.001f && size.x>=half.magnitude*2 && size.y>width,"Hitbox contains shared line plus padding");
  }
  var empty=new DoorHoverFixture();Vector2 c,s;float r;
  Check(!empty.TryGetLockedDoorHoverRect(null,out c,out s,out r),"Null door has no hitbox");
  Check(!empty.TryGetLockedDoorHoverRect(new MapPointOfInterestRecord(),out c,out s,out r),"Zero-length line has no phantom hitbox");
  empty.hasBounds=false;Check(!empty.TryGetLockedDoorHoverRect(new MapPointOfInterestRecord{LineEnd=new Vector2(1,0)},out c,out s,out r),"Unbuilt map has no hitbox");
  return checks;
 }
}
'@
Add-Type -TypeDefinition $fixture.Replace('// METHODS',$methods)
Write-Output "PASS: $([DoorHoverFixture]::Run()) production locked-door hover geometry assertions."
