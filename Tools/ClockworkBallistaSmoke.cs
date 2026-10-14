using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Called by ClockworkRemoteSmoke in the isolated project with unmodified production components.
public static class ClockworkBallistaSmoke
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static int checks;
    static void Check(bool v,string m){checks++;if(!v)throw new Exception(m);}
    static object Call(object o,string m,params object[] args)=>o.GetType().GetMethod(m,Flags).Invoke(o,args);
    static void Set(object o,string f,object v)=>o.GetType().GetField(f,Flags).SetValue(o,v);
    static ClockworkPuppetRuntime Find(string id){foreach(var p in ClockworkPuppetRuntime.ActivePuppets)if(p.PuppetInstanceId==id)return p;return null;}
    static void Move(ClockworkPuppetRuntime p,Vector2 v){p.transform.position=v;p.GetComponent<Rigidbody2D>().position=v;Physics2D.SyncTransforms();}
    static void End(ZeldaFourWayMover mover){ClockworkPuppetRuntime.EndActiveRemoteControl();ClockworkPuppetBoxDriver.EndActiveRemoteControl();mover.EndItemAttack();}
    static void ClearBolts(){foreach(var b in UnityEngine.Object.FindObjectsOfType<FortressBallistaBolt>())UnityEngine.Object.Destroy(b.gameObject);}
    public static IEnumerator Test(PersistentInventory bag,ClockworkPuppetPickupItem source,GameObject actor,
        ZeldaFourWayMover mover,ZeldaCharacterData data,SceneTravelStateManager manager)
    {
        End(mover);bag.ClearAll();
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TestPrefabs/Fortress-Ballista.prefab");
        var weapon=UnityEngine.Object.Instantiate(prefab,new Vector3(10,0),Quaternion.identity).GetComponent<FortressBallista>();
        Check(weapon!=null,"Actual ballista prefab loads");
        Check(bag.TryAddUniqueItem(source.ItemId,"BALLISTA-PUPPET"),"Puppet in inventory");
        Check(bag.TryUseSelectedItem(),"Deploy for ballista");
        var p=Find("BALLISTA-PUPPET");
        Check(!weapon.TryFireFromPuppet(p,data),"Unattached puppet cannot bypass controls");
        Move(p,new Vector2(10,1.4f));Check(!p.TryAttachToBallista(weapon),"Front cannot attach");
        Move(p,new Vector2(11.5f,0));Check(!p.TryAttachToBallista(weapon),"Side cannot attach");
        Move(p,new Vector2(10,-5));Check(!p.TryAttachToBallista(weapon),"Far target rejected");
        Move(p,new Vector2(10,-1.4f));
        var obstacle=new GameObject("Mount obstruction");obstacle.transform.position=new Vector3(10,-1.1f);
        obstacle.AddComponent<BoxCollider2D>().size=new Vector2(1,.1f);Physics2D.SyncTransforms();
        Check(!p.TryAttachToBallista(weapon),"Cannot snap through intervening solid wall");
        UnityEngine.Object.Destroy(obstacle);yield return null;
        Check(p.TryAttachToBallista(weapon)&&p.IsAttachedToBallista(weapon),"E-equivalent tail attachment");
        Check(Vector2.Distance(p.GetComponent<Rigidbody2D>().position,weapon.RearWorldPosition)<.001f,"Tail anchor exact");
        Check(Physics2D.GetIgnoreCollision(p.GetComponent<Collider2D>(),weapon.GetComponent<Collider2D>()),"Mounted collider does not push puppet off tail");
        Check(!weapon.CanAcceptPuppet()&&weapon.CanAcceptPuppet(p),"One puppet per ballista");
        foreach(float angle in new[]{0f,90f,180f,270f})
        foreach(Vector3 scale in new[]{Vector3.one,new Vector3(1.5f,.7f,1),new Vector3(-1.3f,1.7f,1)})
        {
            weapon.transform.rotation=Quaternion.Euler(0,0,angle);weapon.transform.localScale=scale;
            Call(p,"UpdateAttachedPose");
            Check(Vector2.Distance(p.GetComponent<Rigidbody2D>().position,weapon.RearWorldPosition)<.001f,"Anchor follows rotation/nonuniform/mirrored scaling");
            var direction=Quaternion.Euler(0,0,p.GetComponent<Rigidbody2D>().rotation)*Vector3.up;
            Check(Vector2.Dot(direction,weapon.ShotDirection)>.999f,"Puppet points along barrel");
        }
        weapon.transform.rotation=Quaternion.identity;weapon.transform.localScale=Vector3.one;Call(p,"UpdateAttachedPose");
        float before=p.RemainingMagic;
        Check(p.TryFireMountedBallista(),"Attached active puppet fires");
        Check(Mathf.Approximately(p.RemainingMagic,before-source.OperationMagicCost),"Successful shot consumes one configured operation");
        var shot=UnityEngine.Object.FindObjectOfType<FortressBallistaBolt>();
        Check(shot!=null&&ReferenceEquals(shot.GetType().GetField("owner",Flags).GetValue(shot),data),"Real projectile retains controlling body's damage attribution");
        Check(weapon.IsCoolingDown&&!p.TryFireMountedBallista()&&Mathf.Approximately(p.RemainingMagic,before-source.OperationMagicCost),"Cooldown blocks fire without charging energy");
        Check((float)weapon.GetType().GetField("recoilRemaining",Flags).GetValue(weapon)>0,"Puppet shot has normal recoil");
        ClearBolts();End(mover);yield return null;
        Check(!p.TryFireMountedBallista(),"Idle attached puppet never fires autonomously");
        Check(bag.TryUseSelectedItem()&&p.IsAttachedToBallista(weapon),"Remote re-enters without losing mount");
        End(mover);yield return null;

        // Production scene snapshot restores the reference by stable object path, not instance ID.
        var scene=weapon.gameObject.scene;Call(manager,"CaptureScene",scene);
        UnityEngine.Object.Destroy(p.gameObject);yield return null;
        Call(manager,"RestoreScene",scene);
        var states=(IDictionary)manager.GetType().GetField("sceneStates",Flags).GetValue(manager);
        Call(manager,"FinalizeRestoredDevices",scene,states[scene.name]);
        p=Find("BALLISTA-PUPPET");
        Check(p!=null&&p.IsAttachedToBallista(weapon)&&!p.IsUnderRemoteControl,"Scene restore reattaches without seizing input");
        Check(Mathf.Approximately(p.RemainingMagic,before-source.OperationMagicCost),"Saved attached energy retained");
        yield return null;mover.EndItemAttack();
        Check(bag.TryUseSelectedItem(),"Restored attached puppet reconnects");
        Set(weapon,"nextFireTime",-1f);Set(p,"magic",source.OperationMagicCost);
        Check(p.TryFireMountedBallista(),"Last-energy shot succeeds");
        Check(p.IsBroken&&bag.GetSlot(0).IsEmpty&&weapon.CanAcceptPuppet(),"Exhaustion clears link and frees mount");
        ClearBolts();yield return null;mover.EndItemAttack();

        // Box exits with the same identity/energy and the existing remote remains usable.
        var boxPrefab=Resources.Load<GameObject>("PickupItems/CardboardBoxPickupItem");
        var box=UnityEngine.Object.Instantiate(boxPrefab,actor.transform.position,Quaternion.identity).GetComponent<CardboardBoxPickupItem>();
        Check(bag.TryAddUniqueItem(source.ItemId,"BOX-BALLISTA"),"Insert boxed candidate");
        Check(bag.TryUseSelectedItem()&&box.IsClockworkPuppetDriven,"R drives nearby box");
        box.transform.position=new Vector3(10,-1.4f);box.GetComponent<Rigidbody2D>().position=box.transform.position;Physics2D.SyncTransforms();
        Call(box.GetComponent<ClockworkPuppetBoxDriver>(),"DisembarkPuppetAtMount",null,weapon);
        p=Find("BOX-BALLISTA");
        Check(p!=null&&p.IsAttachedToBallista(weapon)&&!box.HasStoredItem,"Concealed puppet exits and mounts tail");
        Check(bag.GetSlot(0).ItemInstanceId=="BOX-BALLISTA"&&bag.GetSlot(0).ItemId==ClockworkPuppetRemotePickupItem.DefaultItemId,"Box transfer retains exact remote binding");
        weapon.enabled=false;Call(p,"UpdateAttachedPose");
        Check(!p.IsAttachedToBallista(weapon)&&!p.TryFireMountedBallista(),"Disabled weapon safely releases attachment");
        Check(!Physics2D.GetIgnoreCollision(p.GetComponent<Collider2D>(),weapon.GetComponent<Collider2D>()),"Detached collision restored");
        weapon.enabled=true;End(mover);yield return null;
        // Ordinary human firing remains available with exactly the same cooldown.
        actor.transform.position=new Vector3(10,-1.5f);actor.GetComponent<Rigidbody2D>().position=actor.transform.position;
        mover.RestoreSavedMovement(Vector2.up,mover.CurrentStamina);mover.EndItemAttack();Set(weapon,"nextFireTime",-1f);
        Check(weapon.TryFire()&&weapon.IsCoolingDown&&!weapon.TryFire(),"Human tail operation and cooldown preserved");
        ClearBolts();Debug.Log("CLOCKWORK_BALLISTA_SMOKE_PASS checks="+checks);
    }
}
