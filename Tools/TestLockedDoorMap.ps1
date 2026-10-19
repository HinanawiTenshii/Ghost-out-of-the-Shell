$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$source = Get-Content -Raw "$root/Assets/Scripts/Map/MapPointOfInterest.cs"
$source = $source.Substring(0, $source.IndexOf('public sealed class MapPointOfInterestUiInteraction'))
$source = $source.Replace('using UnityEngine.EventSystems;', '').Replace('using UnityEngine.UI;', '')
# Reuse the minimal Unity fixture; compile the actual production POI records/manager.
$leverTest = Get-Content -Raw "$PSScriptRoot/TestLeverMapDiscovery.ps1"
$stubs = [regex]::Match($leverTest, "(?s)\`$stubs = @'\r?\n(.*?)\r?\n'@").Groups[1].Value
if (!$stubs) { throw 'Missing shared discovery fixture' }
$tests = @'
public static class LockedDoorMapTests {
 static int checks;
 static void Check(bool b,string s){checks++;if(!b)throw new Exception(s);}
 public static int Run(){
  var m=MapPointOfInterestManager.GetOrCreate();m.ResetAllPoints();
  var a=new UnityEngine.Vector2{x=2,y=3};var b=new UnityEngine.Vector2{x=2,y=7};var color=new UnityEngine.Color();
  var visible=new List<MapPointOfInterestRecord>();int events=0;m.PointsChanged+=()=>events++;
  m.UpdateLockedDoor("door","Level1",a,b,color,true,false);
  Check(m.CaptureSaveState().Count==0,"Unseen locked door must not leak into save/map");
  m.UpdateLockedDoor("door","Level1",a,b,color,false,true);
  Check(m.CaptureSaveState().Count==0,"Unlocked visible door must not discover");
  m.UpdateLockedDoor("door","Level1",a,b,color,true,true);
  Check(m.IsDiscovered("door") && events==1,"First visible locked door discovers once");
  m.CopyVisibleLockedDoors("Level1",visible);Check(visible.Count==1,"Visible in current floor");
  var r=visible[0];Check(r.LineStart==a && r.LineEnd==b,"Actual endpoints preserved");
  Check(r.TooltipText=="锁住的门（钥匙）\n需要：对应钥匙","Safe fallback for missing key name");
  Check(r.IconShape==MapPointIconShape.KeyLockedDoor && !r.IsLineMarker,"Key badge replaces old door line");
  Check(r.WorldPosition.x==2 && r.WorldPosition.y==5,"Leaf center not hinge center");
  Check(!r.CanBeMarked && !m.ToggleMarked("door"),"Door badge is hover-only");
  for(int i=0;i<20;i++)m.UpdateLockedDoor("door","Level1",a,b,color,true,false);
  Check(events==1,"No redundant changes per frame");
  m.UpdateLockedDoor("door","Level1",a,b,color,true,false,false,"哨所钥匙");
  Check(events==2 && r.TooltipText=="锁住的门（钥匙）\n需要：哨所钥匙","Key name refreshes tooltip once");
  for(int i=0;i<20;i++)m.UpdateLockedDoor("door","Level1",a,b,color,true,false,false,"哨所钥匙");
  Check(events==2,"Unchanged key name causes no menu rebuilds");
  m.CopyVisibleLockedDoors("Level1",visible);Check(visible.Count==1,"Retained after leaving sight");
  m.CopyVisibleLockedDoors("Level2",visible);Check(visible.Count==0,"Not on unrelated floor");
  m.UpdateLockedDoor("door","Level1",a,b,color,false,false);
  m.CopyVisibleLockedDoors("Level1",visible);Check(visible.Count==0 && m.IsDiscovered("door"),"Unlock hides but retains discovery");
  m.UpdateLockedDoor("door","Level1",a,b,color,true,false);
  m.CopyVisibleLockedDoors("Level1",visible);Check(visible.Count==1,"Relock seen door restores icon");
  m.UpdateLockedDoor("door","Level1",a,b,color,false,false);
  var saved=m.CaptureSaveState();m.ResetAllPoints();m.ApplySaveState(saved);
  m.CopyVisibleLockedDoors("Level1",visible);Check(visible.Count==0 && m.IsDiscovered("door"),"Broken/unlocked state survives save restore");
  m.UpdateLockedDoor("door2","Level1",b,a,color,true,true);
  saved=m.CaptureSaveState();m.ResetAllPoints();m.ApplySaveState(saved);
  m.CopyVisibleLockedDoors("Level1",visible);Check(visible.Count==1 && visible[0].Id=="door2","Save restores only intact lock");
  Check(visible[0].LineStart==b && visible[0].LineEnd==a,"Save retains orientation and geometry");
  m.UpdateLockedDoor("door2","Level1",a,b,color,true,false);
  m.CopyVisibleLockedDoors("Level1",visible);Check(visible[0].LineStart==a,"Live transform updates endpoints");
  m.UpdateLockedDoor("door2","Level1",a,b,color,true,false,true,"SECRET-KEY");
  Check(visible[0].IconShape==MapPointIconShape.PasswordLockedDoor && visible[0].TooltipText=="锁住的门（密码）","Password mode distinct and no key leak");
  m.UpdateLockedDoor("door3","Level1",a,b,color,true,true,false,"骑士长的钥匙");
  saved=m.CaptureSaveState();m.ResetAllPoints();m.ApplySaveState(saved);
  MapPointOfInterestRecord key;Check(m.TryGetPoint("door3",out key)&&key.TooltipText.EndsWith("骑士长的钥匙"),"Key name preserved by record save restore");
  m.CopyVisibleLockedDoors("Level1",visible);Check(visible.Count==2,"Both lock types included in door queries");
  m.ApplySaveState(null);m.CopyVisibleLockedDoors("Level1",visible);
  Check(visible.Count==0 && !m.IsDiscovered("door2"),"Other save/new game does not inherit discovery");
  Check((int)MapPointIconShape.Lever==5 && (int)MapPointIconShape.LockedDoor==6,"Old save enum compatibility");
  return checks;
 }
}
'@
Add-Type -TypeDefinition ("#pragma warning disable 0649`n"+$source+"`n"+$stubs+"`n"+$tests)
Write-Output ("PASS: {0} production lock record/discovery/save assertions." -f [LockedDoorMapTests]::Run())
$marker=Get-Content -Raw "$root/Assets/Scripts/Map/LockedDoorMapMarker.cs"
$hinge=Get-Content -Raw "$root/Assets/Scripts/Interaction/DoorHingeInteraction.cs"
$data=Get-Content -Raw "$root/Assets/Scripts/Interaction/DoorData.cs"
$graphic=Get-Content -Raw "$root/Assets/Scripts/Map/RuntimeMiniMapGraphic.cs"
$journal=Get-Content -Raw "$root/Assets/Scripts/UI/TabJournalMenuController.cs"
$checks=@(
 @($marker.Contains('vision.IsWorldBoundsVisible(bounds, transform)'), 'Actual vision, not camera-only discovery'),
 @($marker.Contains('GameSaveSystem.IsLoading'), 'Loading cannot discover'),
 @($marker.Contains('GetMapObjectId(transform)'), 'Stable scene identity'),
 @($marker.Contains('leaves[i].IsDestroyed') -and $marker.Contains('leaves[i] == null'), 'Destroyed or missing leaf hides'),
 @($marker.Contains('renderer.transform.TransformVector') -and $marker.Contains('renderer.transform.TransformPoint'), 'Scaled/rotated leaf geometry'),
 @($hinge.Contains('gameObject.AddComponent<LockedDoorMapMarker>()'), 'Automatic attachment'),
 @(([regex]::Matches($hinge,'RefreshLockState\(\)').Count -eq 4), 'All four lock state mutation paths notify'),
 @($data.Contains('NotifyLeafDestroyed()'), 'Immediate break notification'),
 @($graphic.Contains('CopyVisiblePoints(geometrySceneName, lockedDoorMarkers)'), 'Shared HUD/full map drawing'),
 @($marker.Contains('hinge.RequiredUnlockItemDisplayName'), 'Cached actual key name supplied to map'),
 @($journal.Contains('bool keyRequirement = point.IconShape == MapPointIconShape.KeyLockedDoor'), 'Key tooltip has room for requirement')
)
foreach($check in $checks){if(!$check[0]){throw $check[1]}}
Write-Output "PASS: $($checks.Count) wiring/geometry source guards. Not a Unity Play Mode test."
