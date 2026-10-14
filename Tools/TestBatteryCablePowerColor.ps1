$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$cable=Get-Content -Raw "$root/Assets/Scripts/Interaction/BatterySocketCable.cs"
$socket=Get-Content -Raw "$root/Assets/Scripts/Interaction/BatterySocket.cs"
function Extract([string]$text,[string]$signature){
 $start=$text.IndexOf($signature);if($start -lt 0){throw "Missing $signature"}
 $begin=$text.IndexOf('{',$start);$end=$begin+1;$depth=1
 while($depth){if($text[$end] -eq '{'){$depth++};if($text[$end] -eq '}'){$depth--};$end++}
 return $text.Substring($start,$end-$start)
}
$refresh=(Extract $cable 'private void RefreshCoreColor()').Replace('private void','public void')
$colors=([regex]::Matches($cable,'private static readonly Color (?:Powered|Unpowered)CoreColor = [^;]+;')|ForEach-Object{$_.Value.Replace('private static','public static')}) -join "`n"
$power=([regex]::Matches($socket,'public (?:bool|int) (?:HasBattery|RemainingCharge|HasPower) => [^;]+;')|ForEach-Object{$_.Value}) -join "`n"
$stub=@'
using System;
using System.Collections.Generic;
public struct Color {
 public float r,g,b,a;public Color(float r,float g,float b){this.r=r;this.g=g;this.b=b;this.a=1;}
}
public class Mesh {public int uploads;public Color[] uploaded;public void SetColors(List<Color> c){uploads++;uploaded=c.ToArray();}}
public static class BatteryPickupItem {public const int DefaultCapacity=5;}
public class BatterySocket {
 public bool initialized,batteryInstalled,startsWithBattery;public Charge charge=new Charge();public class Charge{public int remaining;}
 // POWER_PROPERTIES
}
public class CableColorHarness {
 public Mesh mesh=new Mesh();public List<Color> vertexColors=new List<Color>();
 public int coreVertexStart=8;public bool lastHasPower=true;public BatterySocket socket=new BatterySocket();
 // CORE_COLORS
 // REFRESH_METHOD
}
public static class CableColorTests {
 static int count;static void Check(bool b,string m){count++;if(!b)throw new Exception(m);}
 static bool Same(Color a,Color b)=>a.r==b.r&&a.g==b.g&&a.b==b.b&&a.a==b.a;
 static void CheckBands(CableColorHarness h,Color core){
  for(int i=0;i<h.vertexColors.Count;i++)Check(Same(h.vertexColors[i],i<8?new Color(.6f,.65f,.66f):core),"Only central band may recolor");
 }
 public static int Run(){
  var h=new CableColorHarness();
  for(int i=0;i<12;i++)h.vertexColors.Add(i<8?new Color(.6f,.65f,.66f):CableColorHarness.PoweredCoreColor);
  h.RefreshCoreColor();CheckBands(h,CableColorHarness.UnpoweredCoreColor);Check(h.mesh.uploads==1,"Empty socket must gray existing mesh");
  h.socket.startsWithBattery=true;h.RefreshCoreColor();CheckBands(h,CableColorHarness.PoweredCoreColor);Check(h.mesh.uploads==2,"Editor starts-with-battery preview");
  h.socket.initialized=true;h.socket.batteryInstalled=true;h.socket.charge.remaining=1;h.RefreshCoreColor();
  Check(h.mesh.uploads==2,"Remaining charge changes with power need no upload");
  h.socket.charge.remaining=0;h.RefreshCoreColor();CheckBands(h,CableColorHarness.UnpoweredCoreColor);Check(h.mesh.uploads==3,"Last charge depletion");
  for(int i=0;i<100;i++)h.RefreshCoreColor();Check(h.mesh.uploads==3,"Stable state must not upload per frame");
  h.socket.charge.remaining=5;h.RefreshCoreColor();CheckBands(h,CableColorHarness.PoweredCoreColor);
  h.socket.batteryInstalled=false;h.RefreshCoreColor();CheckBands(h,CableColorHarness.UnpoweredCoreColor);
  h.socket.batteryInstalled=true;h.RefreshCoreColor();CheckBands(h,CableColorHarness.PoweredCoreColor);
  h.socket=null;h.RefreshCoreColor();CheckBands(h,CableColorHarness.UnpoweredCoreColor);
  h.mesh=null;h.RefreshCoreColor();h.mesh=new Mesh();h.vertexColors.Clear();h.RefreshCoreColor();Check(h.mesh.uploads==0,"Absent/empty mesh safe");
  Check(Same(CableColorHarness.PoweredCoreColor,new Color(.84f,.09f,.055f)),"Preserve original powered red");
  var gray=CableColorHarness.UnpoweredCoreColor;Check(gray.r>=.7f&&gray.b-gray.r<=.05f,"Unpowered core should be light gray");
  return count;
 }
}
'@
Add-Type -TypeDefinition ($stub.Replace('// POWER_PROPERTIES',$power).Replace('// CORE_COLORS',$colors).Replace('// REFRESH_METHOD',$refresh))
$checks=[CableColorTests]::Run()
$late=Extract $cable 'private void LateUpdate()'
if(!$late.Contains('RefreshCoreColor();')){throw 'No immediate power-color refresh'}
if($refresh.Contains('Refresh()') -or $refresh.Contains('DrawRoute()') -or $refresh.Contains('TryRoute')){throw 'Power changes must not reroute geometry'}
$draw=Extract $cable 'private void DrawRoute()'
if(!$draw.Contains('coreVertexStart = vertexColors.Count;') -or !$draw.Contains('lastHasPower ? PoweredCoreColor : UnpoweredCoreColor')){throw 'New routes must use current charge state'}
Write-Output "PASS: $($checks+3) actual power-property/recolor checks; editor defaults, depletion, removal, restore, unchanged casing and no routing work."
