$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
function Extract($source,$signature){
    $start=$source.IndexOf($signature);if($start -lt 0){throw "Missing $signature"}
    $end=$source.IndexOf('{',$start)+1;$depth=1
    while($depth){if($source[$end] -eq '{'){$depth++};if($source[$end] -eq '}'){$depth--};$end++}
    $source.Substring($start,$end-$start)
}
$inventory=Get-Content -Raw "$root/Assets/Scripts/Items/PersistentInventory.cs"
$item=Get-Content -Raw "$root/Assets/Scripts/Items/DisposableHandgunPickupItem.cs"
$code=@'
using System;
public static class Mathf {public static int Max(int a,int b)=>Math.Max(a,b);public static int Min(int a,int b)=>Math.Min(a,b);}
public static class BatteryPickupItem {public const string DefaultItemId="battery";}
public class Slot {
 public string ItemId,ItemInstanceId,ItemName,ItemDescription;public int Quantity;public bool IsEmpty=>Quantity==0;
 public void Set(string id,int n,string instance="",string name="",string description=""){ItemId=id;Quantity=n;ItemInstanceId=instance;ItemName=name;ItemDescription=description;}
 public void Clear(){Set("",0);}
}
public class PersistentInventory {
 public static PersistentInventory Instance;public Slot[] slots;public int notifications;
 public PersistentInventory(int size){slots=new Slot[size];for(int i=0;i<size;i++)slots[i]=new Slot();Instance=this;}
 void NotifyChanged(){notifications++;}void SelectSlot(int i){}
 bool Contains(string id,int n){int total=0;foreach(var s in slots)if(s.ItemId==id)total+=s.Quantity;return total>=n;}
'@
$code+=Extract $inventory '    public bool TryAddItem('
$code+=Extract $inventory '    public bool TryRemoveItem('
$code+=@'
}
public class PickupItemBase {
 public string ItemId="disposable_handgun";public bool blocked,destroyed;public int callbacks;public object gameObject=>this;
 public virtual bool TryPickUp()=>false;protected bool IsPickupBlockedForControlledCharacter()=>blocked;
 protected void OnPickedUp(PersistentInventory i){callbacks++;}protected void NotifyPickedUp(){callbacks++;}
 protected void Destroy(object o){destroyed=true;}
}
public class DisposableHandgunPickupItem:PickupItemBase {
'@
$code+=Extract $item '    public override bool TryPickUp()'
$code+=@'
}
public static class StackTests {
 static int checks;static void Check(bool b,string m){checks++;if(!b)throw new Exception(m);}
 public static int Run(){
  var bag=new PersistentInventory(2);
  for(int i=0;i<6;i++){var gun=new DisposableHandgunPickupItem();Check(gun.TryPickUp()&&gun.destroyed&&gun.callbacks==2,"Pickup succeeds with callbacks");}
  Check(bag.slots[0].Quantity==3&&bag.slots[1].Quantity==3,"Three per slot, fourth spills into next slot");
  var extra=new DisposableHandgunPickupItem();Check(!extra.TryPickUp()&&!extra.destroyed&&extra.callbacks==0,"Full inventory retains world item");
  Check(bag.TryRemoveItem("disposable_handgun",1)&&bag.slots[1].Quantity==2,"Use/drop removes only one");
  Check(new DisposableHandgunPickupItem().TryPickUp()&&bag.slots[1].Quantity==3,"Refill existing stack");
  bag=new PersistentInventory(1);bag.slots[0].Set("disposable_handgun",1,"legacy-id");
  Check(new DisposableHandgunPickupItem().TryPickUp()&&bag.slots[0].Quantity==2&&bag.slots[0].ItemInstanceId=="","Old unique handgun joins stack");
  Check(bag.TryRemoveItem("disposable_handgun",1)&&bag.slots[0].Quantity==1,"Merged legacy stack consumes one");
  bag=new PersistentInventory(1);Check(!bag.TryAddItem("disposable_handgun",4,99)&&bag.slots[0].IsEmpty,"Limit enforced even for oversized caller request, atomic failure");
  Check(bag.TryAddItem("disposable_handgun",3)&&bag.slots[0].Quantity==3,"Default add limit clamped to three");
  bag=new PersistentInventory(1);var blocked=new DisposableHandgunPickupItem{blocked=true};Check(!blocked.TryPickUp()&&bag.slots[0].IsEmpty,"Pickup lock preserved");
  bag.slots[0].Set("key",1,"unique-key");Check(!bag.TryAddItem("key",1,3)&&bag.slots[0].ItemInstanceId=="unique-key","Other unique items remain isolated");
  bag=new PersistentInventory(1);bag.slots[0].Set("stability_crystal",1,"old-crystal");Check(bag.TryAddItem("stability_crystal",9,1)&&bag.slots[0].Quantity==10,"Existing crystal migration preserved");
  bag=new PersistentInventory(1);Check(!bag.TryAddItem("battery",2,3),"Battery still cannot stack");
  return checks;
 }
}
'@
Add-Type -TypeDefinition $code
$count=[StackTests]::Run()
if(!$inventory.Contains('? TryRemoveItem(slot.ItemId, 1)')){throw 'Expected single-item consumption in inventory use path'}
Write-Output "PASS: $count production stacking/pickup/removal checks, including legacy unique handguns. Unity presentation is not exercised."
