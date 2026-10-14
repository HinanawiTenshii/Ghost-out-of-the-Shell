using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

// Loaded by BallistaSmoke in its isolated project. Physics, projectile and damage hitbox are production code.
public static class BallistaProximitySmoke
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly Vector2 Origin=new Vector2(100,100);
    static int checks;
    static void Check(bool value,string label){checks++;if(!value)throw new Exception(label);}
    static T Get<T>(object o,string field)=>(T)o.GetType().GetField(field,Private).GetValue(o);
    static void Call(object o,string method)=>o.GetType().GetMethod(method,Private).Invoke(o,null);
    static FortressBallistaBolt Bolt()
    {
        foreach(var b in UnityEngine.Object.FindObjectsOfType<FortressBallistaBolt>())
            if(!Get<bool>(b,"finished"))return b;
        return null;
    }
    static void Clear()
    {
        foreach(var b in UnityEngine.Object.FindObjectsOfType<FortressBallistaBolt>())UnityEngine.Object.DestroyImmediate(b.gameObject);
        foreach(var b in UnityEngine.Object.FindObjectsOfType<ZeldaAttackHitbox>())UnityEngine.Object.DestroyImmediate(b.gameObject);
        foreach(var b in UnityEngine.Object.FindObjectsOfType<BombExplosionVisual>())UnityEngine.Object.DestroyImmediate(b.gameObject);
    }
    static GameObject Box(string name,Vector2 pos,Vector2 size,bool trigger=false)
    {
        var o=new GameObject(name);o.transform.position=pos;var c=o.AddComponent<BoxCollider2D>();c.size=size;c.isTrigger=trigger;return o;
    }
    static void DamageTick(){Physics2D.SyncTransforms();Physics2D.Simulate(.02f);Physics2D.Simulate(.02f);}
    static FortressBallistaBolt Fire(FortressBallista weapon,ZeldaCharacterData owner,Vector2 direction,float speed=12)
    {
        Check(FortressBallistaBolt.Fire(weapon,owner,Origin,direction,speed),"Shot accepted");
        var bolt=Bolt();if(bolt!=null)bolt.enabled=false;return bolt;
    }
    public static void Test(FortressBallista weapon,ZeldaCharacterData owner)
    {
        Clear();
        // A side-offset character never touches the narrow arrow, but still triggers and takes damage.
        foreach(Vector2 direction in new[]{Vector2.up,Vector2.down,Vector2.left,Vector2.right,new Vector2(.6f,.8f)})
        {
            Vector2 side=new Vector2(direction.y,-direction.x);
            var target=Box("Offset fuse target",Origin+direction*2+side*.55f,Vector2.one*.2f);
            var data=target.AddComponent<ZeldaCharacterData>();target.AddComponent<CircleCollider2D>().radius=.1f;
            var bolt=Fire(weapon,owner,direction,300);
            Check(bolt!=null,"Target outside initial blast area");Call(bolt,"FixedUpdate");
            Check(Get<bool>(bolt,"finished"),"Fast swept fuse catches side-offset character in every direction");
            Check(Vector2.Distance(bolt.transform.position,target.GetComponent<Collider2D>().ClosestPoint(bolt.transform.position))>.15f,"Proximity detonates before direct arrow contact");
            DamageTick();Check(data.Damage==8,"Fuse contact is inside real hitbox and compound body takes damage once");
            Check(data.LastOwner==owner,"Firing body remains damage owner");
            int count=UnityEngine.Object.FindObjectsOfType<ZeldaAttackHitbox>().Length;
            Call(bolt,"FixedUpdate");Call(bolt,"Explode");Check(count==1&&UnityEngine.Object.FindObjectsOfType<ZeldaAttackHitbox>().Length==1,"Fuse/contact callbacks cannot explode twice");
            Clear();UnityEngine.Object.DestroyImmediate(target);
        }

        // Diagonal corners are part of the square blast, not a radius-0.8 circle.
        var corner=Box("Square-corner target",Origin+new Vector2(.75f,.75f),Vector2.one*.05f);
        var cornerData=corner.AddComponent<ZeldaCharacterData>();
        Check(Fire(weapon,owner,Vector2.up)==null,"Muzzle proximity includes square blast corners");
        DamageTick();Check(cornerData.Damage==8,"Corner target receives normal explosion damage");
        Clear();UnityEngine.Object.DestroyImmediate(corner);
        var outside=Box("Outside blast width",Origin+new Vector2(.95f,0),Vector2.one*.1f);outside.AddComponent<ZeldaCharacterData>();
        var live=Fire(weapon,owner,Vector2.up);Check(live!=null,"No fuse outside authored explosion extent");Call(live,"FixedUpdate");
        Check(!Get<bool>(live,"finished"),"Near-but-outside character remains outside during flight");Clear();UnityEngine.Object.DestroyImmediate(outside);

        // Trigger body on a child still resolves its character, matching damage lookup.
        var parent=new GameObject("Character parent");parent.transform.position=Origin+new Vector2(.6f,0);
        var parentData=parent.AddComponent<ZeldaCharacterData>();
        var child=new GameObject("Child trigger body");child.transform.SetParent(parent.transform,false);child.AddComponent<BoxCollider2D>().size=Vector2.one*.1f;child.GetComponent<Collider2D>().isTrigger=true;
        Check(Fire(weapon,owner,Vector2.up)==null,"Child character trigger activates proximity");
        DamageTick();Check(parentData.Damage==8,"Child character trigger receives damage");Clear();UnityEngine.Object.DestroyImmediate(parent);

        var corpse=Box("Corpse",Origin+new Vector2(.6f,0),Vector2.one*.1f);corpse.AddComponent<ZeldaCharacterData>().IsDead=true;
        var ghost=Box("Ghost",Origin+new Vector2(-.6f,0),Vector2.one*.1f);var ghostData=ghost.AddComponent<ZeldaCharacterData>();ghostData.IsGhostLike=true;ghostData.CanTakeAttackDamage=false;
        live=Fire(weapon,owner,Vector2.up);Check(live!=null,"Dead/ghost targets do not trigger fuse");Call(live,"FixedUpdate");Check(!Get<bool>(live,"finished"),"Dead/ghost targets ignored on sweep too");
        Clear();UnityEngine.Object.DestroyImmediate(corpse);UnityEngine.Object.DestroyImmediate(ghost);

        // Props inside blast extent but away from the narrow arrow must never cause proximity.
        var props=new GameObject[7];Type[] types={typeof(DoorData),typeof(BatteryPickupItem),typeof(BatterySocket),typeof(CardboardBoxPickupItem),typeof(BombPickupItem),typeof(DollAttackShake),null};
        for(int i=0;i<props.Length;i++)
        {
            props[i]=Box("Non-character fuse exclusion",Origin+new Vector2(i%2==0?.6f:-.6f,(i/2-1)*.3f),Vector2.one*.1f,i==6);
            if(types[i]!=null)props[i].AddComponent(types[i]);
        }
        live=Fire(weapon,owner,Vector2.up);Check(live!=null,"Nearby props and interaction zones do not trigger muzzle fuse");
        for(int i=0;i<8;i++)Call(live,"FixedUpdate");Check(!Get<bool>(live,"finished"),"Nearby props ignored throughout swept fuse");
        Clear();foreach(var prop in props)UnityEngine.Object.DestroyImmediate(prop);

        // Actual contact remains required for a wall even while it is already inside the blast area.
        var wall=Box("Contact-only wall",Origin+new Vector2(0,2),new Vector2(2,.1f));
        live=Fire(weapon,owner,Vector2.up);for(int i=0;i<5;i++)Call(live,"FixedUpdate");
        Check(!Get<bool>(live,"finished")&&live.transform.position.y>101,"Wall inside blast width does not cause proximity");
        for(int i=0;i<5&&!Get<bool>(live,"finished");i++)Call(live,"FixedUpdate");
        Check(Get<bool>(live,"finished")&&live.transform.position.y>101.8f,"Original direct wall contact still detonates");
        Clear();UnityEngine.Object.DestroyImmediate(wall);

        wall=Box("Earlier thin wall",Origin+new Vector2(0,1),new Vector2(2,.02f));
        var farther=Box("Character beyond earlier impact",Origin+new Vector2(0,3),Vector2.one*.1f);farther.AddComponent<ZeldaCharacterData>();
        live=Fire(weapon,owner,Vector2.up,300);Call(live,"FixedUpdate");
        Check(Get<bool>(live,"finished")&&live.transform.position.y<101,"Earlier wall impact wins over later character proximity");
        Clear();UnityEngine.Object.DestroyImmediate(wall);UnityEngine.Object.DestroyImmediate(farther);

        // Targets walking into the current area trigger even before the arrow's next displacement.
        var moving=Box("Incoming character",Origin+new Vector2(3,0),Vector2.one*.1f);moving.AddComponent<ZeldaCharacterData>();
        live=Fire(weapon,owner,Vector2.up,1);Check(live!=null,"Initially distant character");
        moving.transform.position=Origin+new Vector2(.5f,0);Call(live,"FixedUpdate");
        Check(Get<bool>(live,"finished")&&Vector2.Distance(live.transform.position,Origin)<.001f,"Character entering current area causes immediate explosion");
        Clear();UnityEngine.Object.DestroyImmediate(moving);

        var otherScene=SceneManager.CreateScene("Proximity Other Scene");
        var foreign=Box("Other-scene character",Origin+new Vector2(.5f,0),Vector2.one*.1f);foreign.AddComponent<ZeldaCharacterData>();SceneManager.MoveGameObjectToScene(foreign,otherScene);
        live=Fire(weapon,owner,Vector2.up);Check(live!=null,"Additively loaded foreign character does not trigger");Call(live,"FixedUpdate");Check(!Get<bool>(live,"finished"),"Sweep stays scene-scoped");
        Clear();UnityEngine.Object.DestroyImmediate(foreign);SceneManager.UnloadSceneAsync(otherScene);

        var suspended=Box("Streamed-out nearby character",Origin+new Vector2(.6f,0),Vector2.one*.1f);var suspendedData=suspended.AddComponent<ZeldaCharacterData>();suspended.SetActive(false);CameraVisionObjectStreaming.Suspended.Add(suspended);
        Check(Fire(weapon,owner,Vector2.up)==null&&suspended.activeSelf,"Retention wakes off-axis target before muzzle fuse query");
        DamageTick();Check(suspendedData.Damage==8,"Woken character gets blast damage");Clear();CameraVisionObjectStreaming.Suspended.Remove(suspended);UnityEngine.Object.DestroyImmediate(suspended);

        Vector3 oldOwnerPosition=owner.transform.position;int ownerDamage=owner.Damage;
        owner.transform.position=Origin+new Vector2(.6f,0);
        Check(Fire(weapon,owner,Vector2.up)==null,"Shooter is not exempt from proximity");
        DamageTick();Check(owner.Damage==ownerDamage+8,"Shooter remains susceptible to own blast");
        Clear();owner.transform.position=oldOwnerPosition;
        Debug.Log("BALLISTA_PROXIMITY_SMOKE_PASS checks="+checks);
    }
}
