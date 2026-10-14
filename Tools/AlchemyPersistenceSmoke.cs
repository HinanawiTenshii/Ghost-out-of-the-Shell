using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Full production scene snapshot + disk JSON integration, in the isolated test project.
[InitializeOnLoad]
public static class AlchemyPersistenceSmoke
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    const string Pending="AlchemyPersistenceSmoke.Pending";
    static IEnumerator test;static int checks,lastFrame=-1;
    static AlchemyPersistenceSmoke(){EditorApplication.update+=Tick;}
    public static void Run(){SessionState.SetBool(Pending,true);EditorApplication.EnterPlaymode();}
    static void Tick()
    {
        if(!EditorApplication.isPlaying||lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
        try{
            if(SessionState.GetBool(Pending,false)){SessionState.SetBool(Pending,false);test=Test();}
            if(test!=null&&!test.MoveNext()){test=null;Debug.Log("ALCHEMY_PERSISTENCE_PASS checks="+checks);EditorApplication.Exit(0);}
        }catch(Exception error){test=null;Debug.LogException(error);EditorApplication.Exit(1);}
    }
    static void Check(bool ok,string label){checks++;if(!ok)throw new Exception(label);}
    static T Get<T>(object o,string n)=>(T)o.GetType().GetField(n,Flags).GetValue(o);
    static void Set(object o,string n,object value)=>o.GetType().GetField(n,Flags).SetValue(o,value);
    static object Call(object o,string n,params object[] args)=>o.GetType().GetMethod(n,Flags).Invoke(o,args);
    static object SnapshotFromDisk(string json,string sceneName)
    {
        var type=typeof(SceneTravelStateManager);
        var disk=JsonUtility.FromJson(json,type.GetNestedType("DiskWorld",BindingFlags.NonPublic));
        object saved=null;foreach(var entry in Get<IList>(disk,"scenes"))if(Get<string>(entry,"name")==sceneName)saved=entry;
        Check(saved!=null,"Saved scene present after JSON parse");
        var state=Activator.CreateInstance(type.GetNestedType("SceneState",BindingFlags.NonPublic),true);Set(state,"sceneName",sceneName);
        var objects=Get<IDictionary>(state,"objects");
        foreach(var entry in Get<IList>(saved,"objects"))objects.Add(Get<string>(entry,"path"),entry);
        return state;
    }
    static IEnumerator Test()
    {
        while(DocumentReader.IsInputBlocked)yield return null;
        var scene=SceneManager.CreateScene("Alchemy Save Integration");SceneManager.SetActiveScene(scene);Time.timeScale=1f;
        var root=new GameObject("Potion player");root.transform.position=new Vector3(0,-1.2f,0);
        root.AddComponent<BoxCollider2D>().size=Vector2.one*.4f;
        var data=root.AddComponent<SwordZeldaCharacterData>();var mover=root.AddComponent<ZeldaFourWayMover>();ZeldaRuntimeRegistry.ClaimControlledMover(mover);
        var baseline=data.CaptureRuntimeState();baseline.health=8;baseline.currentHealth=8;baseline.possessionEnergy=4;data.ApplyRuntimeState(baseline);
        var growth=PlayerGrowthAttributes.Instance;if(growth==null)growth=new GameObject("Potion growth").AddComponent<PlayerGrowthAttributes>();growth.ApplyRuntimeState(default);
        var invRoot=new GameObject("Alchemy inventory");invRoot.SetActive(false);var inv=invRoot.AddComponent<PersistentInventory>();
        Set(inv,"saveToPlayerPrefs",false);invRoot.SetActive(true);inv.enabled=false;inv.ClearAll();
        var template=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TestPrefabs/Alchemy-Cauldron.prefab");
        var cauldron=UnityEngine.Object.Instantiate(template).GetComponent<AlchemyCauldron>();
        Check(cauldron!=null,"Shipped cauldron includes interaction script");
        string message;
        Check(inv.TryAddItem(AlchemyRecipeCatalog.RedHerbId,2,3),"Add recipe ingredients");
        Check(cauldron.TryInteract(out message),"Submit first herb");mover.EndItemAttack();
        Check(cauldron.TryInteract(out message),"Submit second herb");mover.EndItemAttack();
        CharacterPotionBuffs.TryApply(data,AlchemyPotionKind.Might);CharacterPotionBuffs.TryApply(data,AlchemyPotionKind.Weakness);
        var manager=SceneTravelStateManager.Instance;string json=manager.CaptureDiskWorld();
        Check(json.Contains("\"hasAlchemyCauldron\":true")&&json.Contains("\"potionBuffs\":"),"World format contains cauldron and independent body buffs");
        var snapshot=SnapshotFromDisk(json,scene.name);
        cauldron.ApplyRuntimeState(default);data.ApplyRuntimeState(baseline);
        for(int i=0;i<3;i++)
        {
            Call(manager,"RestoreSceneSnapshot",scene,snapshot,false);
            Check(cauldron.RedCount==2&&!cauldron.IsReady,"Partial recipe restored without finishing or consuming items");
            Check(data.Health==5&&data.CurrentHealth==5&&data.FinalAttackPower==1,"Real scene restore applies Weakness after base HP and final attack override");
            Check(data.CaptureRuntimeState().health==8&&CharacterPotionBuffs.Has(data,AlchemyPotionKind.Might),"Repeated loading never bakes or doubles modifiers");
        }
        Check(inv.TryAddItem(AlchemyRecipeCatalog.BlueHerbId,1,3),"Third ingredient available");mover.EndItemAttack();
        Check(cauldron.TryInteract(out message)&&cauldron.BrewedKind==AlchemyPotionKind.Might,"Restored recipe can complete normally");mover.EndItemAttack();
        var stage=cauldron.CaptureRuntimeState();Check(stage.transitionRemaining>0,"Completion starts a color transition");
        stage.transitionRemaining=.5f;stage.displayedColor=Color.Lerp(stage.transitionFrom,stage.transitionTarget,.5f);cauldron.ApplyRuntimeState(stage);
        json=manager.CaptureDiskWorld();snapshot=SnapshotFromDisk(json,scene.name);
        cauldron.ApplyRuntimeState(default);Call(manager,"RestoreSceneSnapshot",scene,snapshot,false);
        var restored=cauldron.CaptureRuntimeState();
        Check(cauldron.IsReady&&cauldron.BrewedKind==AlchemyPotionKind.Might,"Completed recipe restored as unclaimed potion");
        Check(Mathf.Abs(restored.transitionRemaining-.5f)<.001f&&restored.displayedColor==stage.displayedColor,"Load preserves in-progress liquid color transition");
        Check(!inv.Contains(AlchemyRecipeCatalog.GetItemId(AlchemyPotionKind.Might)),"Loading full pot does not duplicate a reward in inventory");
        Check(inv.TryAddItem(GlassBottlePickupItem.DefaultItemId,1,3),"Provide receiving bottle");
        Check(cauldron.TryInteract(out message)&&!cauldron.IsReady,"Bottle after load claims one reward");mover.EndItemAttack();
        Check(inv.GetQuantity(AlchemyRecipeCatalog.GetItemId(AlchemyPotionKind.Might))==1&&!inv.Contains(GlassBottlePickupItem.DefaultItemId),"Single bottle converted to a single potion");
        json=manager.CaptureDiskWorld();snapshot=SnapshotFromDisk(json,scene.name);
        cauldron.ApplyRuntimeState(stage);Call(manager,"RestoreSceneSnapshot",scene,snapshot,false);
        Check(!cauldron.IsReady&&cauldron.IngredientCount==0,"Claimed pot stays empty on load");
        Check(!cauldron.TryInteract(out message)&&inv.GetQuantity(AlchemyRecipeCatalog.GetItemId(AlchemyPotionKind.Might))==1,"Repeated claim cannot duplicate items");
        // Legacy scene states predating this feature leave authored cauldron defaults intact.
        foreach(DictionaryEntry entry in Get<IDictionary>(snapshot,"objects"))Set(entry.Value,"hasAlchemyCauldron",false);
        var oldDefault=new AlchemyCauldron.RuntimeState{red=1};cauldron.ApplyRuntimeState(oldDefault);
        Call(manager,"RestoreSceneSnapshot",scene,snapshot,false);Check(cauldron.RedCount==1,"Missing cauldron flags do not reset unrelated defaults");
        // Disk player data is the separate path used for bodies that persist across travel.
        data.ApplyRuntimeState(baseline);CharacterPotionBuffs.TryApply(data,AlchemyPotionKind.Echo);CharacterPotionBuffs.TryApply(data,AlchemyPotionKind.Sight);
        CharacterPotionBuffs.TryApply(data,AlchemyPotionKind.Spirit);data.RestorePossessionEnergy(2);
        Check(inv.TryAddItem(GlassBottlePickupItem.DefaultItemId,3,3),"Add a full bottle stack for disk player roundtrip");
        int savedSelection=inv.SelectedSlotIndex;
        var player=JsonUtility.FromJson<SceneTravelStateManager.DiskPlayer>(JsonUtility.ToJson(manager.CaptureDiskPlayer()));
        Check(player.character.potionBuffs.remaining[(int)AlchemyPotionKind.Echo]>119f,"Disk player preserves full 120 second Echo");
        Check(player.growth.potionEnergyRemaining>29f&&player.growth.potionEnergyCurrent==2,"Player-owned energy stored separately from body buffs");
        data.ApplyRuntimeState(baseline);growth.ApplyRuntimeState(default);growth.ApplyRuntimeState(player.growth);data.ApplyRuntimeState(player.character);
        Check(CharacterPotionBuffs.Has(data,AlchemyPotionKind.Echo)&&CharacterPotionBuffs.Has(data,AlchemyPotionKind.Sight),"Disk player restores active body effects");
        Check(data.FinalPossessionEnergy==6&&data.MaxPossessionEnergy==6,"Temporary energy restored without permanent growth modification");
        Check(growth.CaptureRuntimeState().possessionEnergy==0,"Temporary energy is not saved as permanent growth");
        inv.ClearAll();
        for(int i=0;i<3;i++)
        {
            inv.ApplyRuntimeState(player.inventory);
            Check(inv.GetQuantity(AlchemyRecipeCatalog.GetItemId(AlchemyPotionKind.Might))==1 &&
                inv.GetQuantity(GlassBottlePickupItem.DefaultItemId)==3 && inv.SelectedSlotIndex==savedSelection,
                "Disk player inventory repeatedly restores potion identity, bottle stack and selected slot without duplication");
        }
        CharacterPotionBuffs.InvalidateEchoForTravel(data);Call(manager,"CaptureTravelingCharacter");
        var traveler=Get<ZeldaCharacterData.RuntimeState>(manager,"travelingCharacterState");
        Check(traveler.potionBuffs.remaining[(int)AlchemyPotionKind.Echo]==0&&traveler.potionBuffs.remaining[(int)AlchemyPotionKind.Sight]>0,"Travel invalidates only Echo before capturing the body");
        json=manager.CaptureDiskWorld();Check(!string.IsNullOrEmpty(json),"World capture still works with traveling-body exclusion");
        // Real item use consumes exactly the chosen prefab and does not drop the buff on transfer.
        data.ApplyRuntimeState(baseline);growth.ApplyRuntimeState(default);inv.ClearAll();
        var potion=Resources.Load<GameObject>("PickupItems/Potion-Veil").GetComponent<PotionPickupItem>();
        ItemDescriptionWindow.ApplyDiscoveryHistory(new[]{potion.DescriptionDiscoveryId});
        Check(inv.TryAddUniqueItem(potion.ItemId,"integration-veil",1,1,potion.ItemName,potion.ItemDescription),"Add a real potion instance");
        Check(inv.TryUseSelectedItem()&&!inv.Contains(potion.ItemId)&&CharacterPotionBuffs.Has(data,AlchemyPotionKind.Veil),"R consumes real potion and retains newly granted Veil");
        Check(inv.GetQuantity(GlassBottlePickupItem.DefaultItemId)==1,"Drinking a persisted potion returns one empty bottle");
        mover.EndItemAttack();var next=new GameObject("Next body");next.AddComponent<BoxCollider2D>();var nextData=next.AddComponent<SwordZeldaCharacterData>();var nextMover=next.AddComponent<ZeldaFourWayMover>();
        mover.enabled=false;ZeldaRuntimeRegistry.ClaimControlledMover(nextMover);
        Check(CharacterPotionBuffs.Has(data,AlchemyPotionKind.Veil)&&!CharacterPotionBuffs.Has(nextData,AlchemyPotionKind.Veil),"Body buff does not transfer to the next possessed character");
        inv.ClearAll();
        var poison=Resources.Load<GameObject>("PickupItems/Potion-Death").GetComponent<PotionPickupItem>();
        ItemDescriptionWindow.ApplyDiscoveryHistory(new[]{poison.DescriptionDiscoveryId});
        Check(inv.TryAddUniqueItem(poison.ItemId,"integration-poison",1,1,poison.ItemName,poison.ItemDescription),"Add a real poison instance");
        CharacterPotionBuffs.TryApply(nextData,AlchemyPotionKind.Echo);
        Check(inv.TryUseSelectedItem() && nextData.IsDead && !inv.Contains(poison.ItemId),
            "Real R poison consumes one item and kills controlled body despite Echo");
        Check(inv.GetQuantity(GlassBottlePickupItem.DefaultItemId)==1,"Poison also returns one empty bottle despite control transfer on death");
        yield return null;
    }
}
