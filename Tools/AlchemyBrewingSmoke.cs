using System;
using System.Collections;
using System.Reflection;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Isolated project only: production scripts/resources plus Alchemy-Cauldron in TestPrefabs.
[InitializeOnLoad]
public static class AlchemyBrewingSmoke
{
    const string Pending="AlchemyBrewingSmoke.Pending";
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static IEnumerator run; static int checks,frame=-1;
    static AlchemyBrewingSmoke(){EditorApplication.update+=Tick;}
    public static void Run(){SessionState.SetBool(Pending,true);EditorApplication.EnterPlaymode();}
    static void Tick()
    {
        if(!EditorApplication.isPlaying || frame==Time.frameCount)return;frame=Time.frameCount;
        try{
            if(SessionState.GetBool(Pending,false)){SessionState.SetBool(Pending,false);run=Test();}
            if(run!=null && !run.MoveNext()){run=null;Debug.Log("ALCHEMY_BREWING_PASS checks="+checks);EditorApplication.Exit(0);}
        }catch(Exception e){run=null;Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static void Check(bool value,string name){checks++;if(!value)throw new Exception(name);}
    static void Set(object o,string name,object value)=>o.GetType().GetField(name,Flags).SetValue(o,value);
    static T Get<T>(object o,string name)=>(T)o.GetType().GetField(name,Flags).GetValue(o);
    static void Ready(ZeldaFourWayMover mover)=>mover.EndItemAttack();
    static void Empty(AlchemyCauldron pot,PersistentInventory bag){pot.ApplyRuntimeState(default);bag.ClearAll();bag.SelectSlot(0);}
    static bool Submit(AlchemyCauldron pot,PersistentInventory bag,ZeldaFourWayMover mover,string id)
    {Ready(mover);bag.SetSlot(0,id,1);bag.SelectSlot(0);return pot.TryInteract(out _);}
    static bool Near(Color a,Color b)=>Vector3.Distance(new Vector3(a.r,a.g,a.b),new Vector3(b.r,b.g,b.b))<.002f;
    static IEnumerator Test()
    {
        Time.timeScale=1;
        while(DocumentReader.IsInputBlocked)yield return null;
        var scene=SceneManager.CreateScene("Alchemy Brewing Test");SceneManager.SetActiveScene(scene);
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TestPrefabs/Alchemy-Cauldron.prefab");
        Check(prefab!=null,"Cauldron prefab imports");
        var root=UnityEngine.Object.Instantiate(prefab);var pot=root.GetComponent<AlchemyCauldron>();
        var visual=root.GetComponent<GeometricAlchemyDecoration>();
        Check(pot!=null && visual!=null,"Cauldron component and overhead visual");
        Check(root.GetComponent<CameraVisionStreamingExempt>()!=null,"Functional pot remains available across streaming");
        Check(Near(pot.DisplayedColor,AlchemyRecipeCatalog.UnbrewedColor),"Empty pot uses neutral unbrewed color");
        var inventoryObject=new GameObject("Test inventory");inventoryObject.SetActive(false);
        var bag=inventoryObject.AddComponent<PersistentInventory>();Set(bag,"saveToPlayerPrefs",false);Set(bag,"persistBetweenScenes",false);
        inventoryObject.SetActive(true);bag.ClearAll();
        var actor=new GameObject("Brewer");actor.transform.position=new Vector3(0,-1.1f,0);
        actor.AddComponent<BoxCollider2D>().size=Vector2.one*.25f;
        var data=actor.AddComponent<ZeldaCharacterData>();var mover=actor.AddComponent<ZeldaFourWayMover>();
        var health=data.CaptureRuntimeState();health.health=8;health.currentHealth=8;data.ApplyRuntimeState(health);
        ZeldaRuntimeRegistry.ClaimControlledMover(mover);mover.ReceiveControl(Vector2.up);
        yield return null; // Let the production decoration build its original mesh once.
        string[] ids={AlchemyRecipeCatalog.RedHerbId,AlchemyRecipeCatalog.GreenHerbId,AlchemyRecipeCatalog.BlueHerbId};
        for(int a=0;a<3;a++)for(int b=0;b<3;b++)for(int c=0;c<3;c++)
        {
            Empty(pot,bag);
            int[] n=new int[3];foreach(int i in new[]{a,b,c}){
                Check(Submit(pot,bag,mover,ids[i]),"Submit one selected ingredient");n[i]++;
                Check(pot.IngredientCount==n[0]+n[1]+n[2] && bag.GetQuantity(ids[i])==0,"Exactly one consumed per submission");
            }
            var kind=AlchemyRecipeCatalog.Resolve(n[0],n[1],n[2]);
            Check(pot.IsReady && pot.BrewedKind==kind,"Order-independent recipe resolution");
            Check(pot.StatusText().Contains(AlchemyRecipeCatalog.GetName(kind)),"Prompt identifies completed recipe");
            Ready(mover);bag.SetSlot(0,ids[0],1);bag.SelectSlot(0);
            Check(!pot.TryInteract(out _) && bag.GetQuantity(ids[0])==1,"Ready pot never consumes a fourth herb");
            Ready(mover);bag.SetSlot(0,GlassBottlePickupItem.DefaultItemId,1);
            Check(pot.TryInteract(out _) && !pot.IsReady && pot.IngredientCount==0,"Bottle extraction resets pot");
            Check(bag.GetQuantity(AlchemyRecipeCatalog.GetItemId(kind))==1 && bag.GetQuantity(GlassBottlePickupItem.DefaultItemId)==0,"One bottle becomes exactly one matching potion");
        }
        foreach(AlchemyPotionKind kind in Enum.GetValues(typeof(AlchemyPotionKind)))
        {
            var resolved=bag.ResolveItemPrefab(AlchemyRecipeCatalog.GetItemId(kind));
            var item=resolved as PotionPickupItem;
            Check(item!=null && item.Kind==kind && item.MaxStackSize==1,"Distinct prefab, identity and non-stackable potion: expected="+kind+
                ", resolved="+(resolved!=null ? resolved.GetType().Name+"/"+resolved.ItemId+"/"+resolved.ItemName+"/max="+resolved.MaxStackSize : "null")+
                ", actual kind="+(item!=null ? item.Kind.ToString() : "null"));
            Check(item.ItemVisual is PotionPickupItemVisual && item.ItemVisual.InventoryIcon!=null,"Filled bottle inventory icon");
            Check(item.ItemDescription.Contains(AlchemyRecipeCatalog.GetName(kind)),"Potion has full localized description");
            Check(Vector3.Distance((Vector3)(Vector4)AlchemyRecipeCatalog.GetColor(kind),(Vector3)(Vector4)AlchemyRecipeCatalog.UnbrewedColor)>.25f,"Every potion is distinct from unbrewed fluid");
        }
        Check(AlchemyRecipeCatalog.GetDuration(AlchemyPotionKind.Echo)==120,"Echo retains original duration");
        Check(AlchemyRecipeCatalog.GetDuration(AlchemyPotionKind.Berserk)==22.5f,"Berserk duration halved");
        Check(AlchemyRecipeCatalog.GetDuration(AlchemyPotionKind.Sight)==30,"Sight duration halved");
        bool invalid=false;try{AlchemyRecipeCatalog.Resolve(1,0,0);}catch(ArgumentException){invalid=true;}Check(invalid,"Incomplete recipe rejected");

        Empty(pot,bag);Submit(pot,bag,mover,ids[0]);Submit(pot,bag,mover,ids[0]);Submit(pot,bag,mover,ids[0]);Ready(mover);
        for(int i=0;i<bag.SlotCount;i++)bag.SetSlot(i,ids[2],3);
        bag.SetSlot(2,GlassBottlePickupItem.DefaultItemId,2);bag.SelectSlot(2);
        int events=0;bag.InventoryChanged+=()=>events++;
        string before=JsonUtility.ToJson(bag.CaptureRuntimeState());
        Check(!pot.TryInteract(out string full) && full.Contains("不足"),"Full bag rejects extraction if bottle stack would retain one");
        Check(pot.IsReady && before==JsonUtility.ToJson(bag.CaptureRuntimeState()) && events==0,"Failed extraction has zero loss or intermediate notifications");
        bag.SetSlot(2,GlassBottlePickupItem.DefaultItemId,1);bag.SelectSlot(2);events=0;bool snapshotConsistent=false;
        bag.InventoryChanged+=()=>{if(bag.GetQuantity(AlchemyRecipeCatalog.GetItemId(AlchemyPotionKind.Vitality))>0)snapshotConsistent=pot.IngredientCount==0;};
        Check(pot.TryInteract(out _) && events==1 && snapshotConsistent,"Last bottle is replaced atomically even in full inventory");

        // Custom bottle state is preserved; legacy individually identified supplies can merge.
        bag.ClearAll();bag.ApplyRuntimeState(new PersistentInventory.RuntimeState{selectedSlotIndex=0,slots=new[]{new PersistentInventory.RuntimeSlotState{
            itemId=GlassBottlePickupItem.DefaultItemId,quantity=1,itemInstanceId="old-bottle",itemState="{\"version\":1,\"speed\":6,\"radius\":5}"
        }}});
        Check(bag.TryAddUniqueItem(GlassBottlePickupItem.DefaultItemId,"new-bottle",1,3,itemState:"{\"version\":1,\"speed\":6.0,\"radius\":5.0}"),"Legacy bottle merges into stack");
        Check(bag.GetSlot(0).Quantity==2 && string.IsNullOrEmpty(bag.GetSlot(0).ItemInstanceId),"Merged supply uses fungible identity");
        Check(bag.TryAddUniqueItem(GlassBottlePickupItem.DefaultItemId,"custom-bottle",1,3,itemState:"{\"version\":1,\"speed\":9,\"radius\":12}"),"Custom bottle enters own slot");
        Check(bag.GetSlot(0).Quantity==2 && bag.GetSlot(1).Quantity==1,"Different bottle settings never merge");
        string customState=bag.GetSlot(1).ItemState;
        Check(bag.TryAddItem(GlassBottlePickupItem.DefaultItemId,1,99) && bag.GetSlot(0).Quantity==3 && bag.GetSlot(1).ItemState==customState,"Default grants fill default stack without erasing custom state");
        bag.SelectSlot(1);Check(bag.TryConsumeSelectedItem(GlassBottlePickupItem.DefaultItemId) && bag.GetSlot(1).IsEmpty && bag.GetSlot(0).Quantity==3,"Precise consumption uses selected stack");
        foreach(string id in new[]{ids[0],ids[1],ids[2],GlassBottlePickupItem.DefaultItemId}){
            bag.ClearAll();Check(bag.ResolveItemPrefab(id).MaxStackSize==3,"Supply prefab max stack three");
            Check(bag.TryAddItem(id,4,99) && bag.GetSlot(0).Quantity==3 && bag.GetSlot(1).Quantity==1,"Generic grant enforces three per slot");
        }
        var discoveryIds=new string[4];for(int i=0;i<4;i++)discoveryIds[i]=bag.ResolveItemPrefab(i<3 ? ids[i] : GlassBottlePickupItem.DefaultItemId).DescriptionDiscoveryId;
        ItemDescriptionWindow.ApplyDiscoveryHistory(discoveryIds);
        foreach(string id in new[]{ids[0],ids[1],ids[2],GlassBottlePickupItem.DefaultItemId})
        {
            Ready(mover);bag.ClearAll();
            var pickup=UnityEngine.Object.Instantiate(bag.ResolveItemPrefab(id).gameObject).GetComponent<PickupItemBase>();
            int notifications=0,pickupEvents=0,reentrySuccess=0;
            Action reenter=()=>{notifications++;if(pickup.TryPickUp())reentrySuccess++;};
            bag.InventoryChanged+=reenter;
            pickup.PickedUp+=same=>{pickupEvents++;if(same.TryPickUp())reentrySuccess++;};
            Check(pickup.TryPickUp(),"First physical pickup commits "+id);
            Check(!pickup.TryPickUp(),"Same-object same-frame duplicate rejected "+id);
            Check(bag.GetQuantity(id)==1 && notifications==1 && pickupEvents==1 && reentrySuccess==0,"Inventory/PickedUp callback reentry cannot duplicate "+id);
            bag.InventoryChanged-=reenter;
        }
        Ready(mover);bag.ClearAll();for(int i=0;i<bag.SlotCount;i++)bag.SetSlot(i,"full-test-"+i,1);
        var retryPickup=UnityEngine.Object.Instantiate(bag.ResolveItemPrefab(ids[0]).gameObject).GetComponent<PickupItemBase>();
        Check(!retryPickup.TryPickUp() && bag.GetQuantity(ids[0])==0,"Full-bag failed physical pickup has no insertion");
        bag.ClearSlot(0);Check(retryPickup.TryPickUp() && bag.GetQuantity(ids[0])==1,"Failed pickup releases transaction lock for a later retry");
        // Partial recipes and color transitions round-trip without consuming or replaying anything.
        Empty(pot,bag);Submit(pot,bag,mover,ids[2]);Submit(pot,bag,mover,ids[0]);
        var saved=JsonUtility.FromJson<AlchemyCauldron.RuntimeState>(JsonUtility.ToJson(pot.CaptureRuntimeState()));
        Empty(pot,bag);pot.ApplyRuntimeState(saved);
        Check(pot.RedCount==1 && pot.BlueCount==1 && !pot.IsReady,"Partial recipe save restore");
        Submit(pot,bag,mover,ids[0]);Ready(mover);
        Check(!Near(pot.DisplayedColor,AlchemyRecipeCatalog.GetColor(AlchemyPotionKind.Might)),"Brew begins a gradient rather than instant color jump");
        var mesh=Get<Mesh>(visual,"mesh");
        float deadline=Time.time+1.15f;while(Time.time<deadline)yield return null;
        Check(Near(pot.DisplayedColor,AlchemyRecipeCatalog.GetColor(AlchemyPotionKind.Might)),"Gradient reaches recipe color");
        Check(Get<Mesh>(visual,"mesh")==mesh && Near(visual.LiquidColor,pot.DisplayedColor),"Liquid recolors original mesh without rebuilding bubbles");
        saved=pot.CaptureRuntimeState();Empty(pot,bag);pot.ApplyRuntimeState(saved);
        Check(pot.IsReady && pot.BrewedKind==AlchemyPotionKind.Might && Near(pot.DisplayedColor,AlchemyRecipeCatalog.GetColor(AlchemyPotionKind.Might)),"Completed potion save restore");
        Empty(pot,bag);bag.SetSlot(0,ids[0],1);Time.timeScale=0;
        Check(!pot.TryInteract(out _) && pot.IngredientCount==0,"Paused interactions do not consume");Time.timeScale=1;
        actor.transform.position=Vector3.one*20;Check(!pot.TryInteract(out _) && bag.GetQuantity(ids[0])==1,"Out-of-range interaction does not consume");
        actor.transform.position=new Vector3(0,-1.1f,0);Ready(mover);
        bag.SetSlot(0,"not-a-herb",1);Check(!pot.TryInteract(out _) && pot.IngredientCount==0,"Unsupported item is rejected without loss");
        if(PlayerGrowthAttributes.Instance==null)new GameObject("Test player energy").AddComponent<PlayerGrowthAttributes>();
        foreach(AlchemyPotionKind kind in Enum.GetValues(typeof(AlchemyPotionKind)))
        {
            if(kind==AlchemyPotionKind.Death)continue;Ready(mover);bag.ClearAll();
            string id=AlchemyRecipeCatalog.GetItemId(kind);bag.SetSlot(0,id,1);bag.SelectSlot(0);
            Check(bag.TryUseSelectedItem() && bag.GetQuantity(id)==0 && !data.IsDead,"R consumes one "+kind+" potion");
            Check(bag.GetQuantity(GlassBottlePickupItem.DefaultItemId)==1,"R returns one empty bottle after "+kind);
            if(kind==AlchemyPotionKind.Veil)Check(CharacterPotionBuffs.Has(data,kind),"Veil remains after its own drinking gesture");
            if(kind==AlchemyPotionKind.Berserk)Check(!CharacterPotionBuffs.Has(data,AlchemyPotionKind.Veil),"Drinking a different potion cancels Veil");
        }
        Ready(mover);bag.ClearAll();bag.SetSlot(0,AlchemyRecipeCatalog.GetItemId(AlchemyPotionKind.Veil),1);bag.SelectSlot(0);
        Check(bag.TryUseSelectedItem() && CharacterPotionBuffs.Has(data,AlchemyPotionKind.Veil),"Fresh Veil potion succeeds");
        Ready(mover);bag.SetSlot(0,ids[0],1);
        Check(!bag.TryUseSelectedItem() && CharacterPotionBuffs.Has(data,AlchemyPotionKind.Veil),"Failed passive-supply R does not cancel Veil");
        Check(bag.TryDropSelectedItem() && CharacterPotionBuffs.Has(data,AlchemyPotionKind.Veil),"Ordinary Q drop does not cancel Veil");
        foreach(var droppedPowder in UnityEngine.Object.FindObjectsOfType<HerbPowderPickupItem>())
            droppedPowder.gameObject.SetActive(false); // Q test fixture is not part of the recipe preview.
        root.transform.position=Vector3.one*100;actor.transform.position=Vector3.one*200;
        // Render real production meshes and generated bottle sprites before CRT post-processing.
        Font font=Get<Font>(pot,"promptFont");
        for(int i=0;i<11;i++)
        {
            int col=i%6,row=i/6;Vector3 position=new Vector3(-8.5f+col*3.4f,2f-row*4f,0);
            var display=UnityEngine.Object.Instantiate(prefab,position,Quaternion.identity);
            var sample=display.GetComponent<AlchemyCauldron>();string label="未炼制";
            if(i>0)
            {
                var kind=(AlchemyPotionKind)(i-1);var ingredients=AlchemyRecipeCatalog.GetIngredients(kind);
                sample.ApplyRuntimeState(new AlchemyCauldron.RuntimeState{red=ingredients.x,green=ingredients.y,blue=ingredients.z});
                label=AlchemyRecipeCatalog.GetName(kind)+"\n红"+ingredients.x+" 绿"+ingredients.y+" 蓝"+ingredients.z;
                UnityEngine.Object.Instantiate(bag.ResolveItemPrefab(AlchemyRecipeCatalog.GetItemId(kind)).gameObject,position+new Vector3(1.05f,-.4f,0),Quaternion.identity);
            }
            var title=new GameObject("Preview Label",typeof(TextMesh));title.transform.position=position+new Vector3(0,-1.3f,0);
            var text=title.GetComponent<TextMesh>();text.font=font;text.fontSize=64;text.characterSize=.035f;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.text=label;
            title.GetComponent<MeshRenderer>().sharedMaterial=font.material;
            font.RequestCharactersInTexture(label,64,FontStyle.Normal);
        }
        yield return null;yield return null;
        var cameraObject=new GameObject("Alchemy Preview Camera",typeof(Camera));cameraObject.transform.position=new Vector3(0,0,-10);
        var camera=cameraObject.GetComponent<Camera>();camera.orthographic=true;camera.orthographicSize=4.7f;camera.clearFlags=CameraClearFlags.SolidColor;
        camera.backgroundColor=new Color(.025f,.05f,.10f);camera.cullingMask=~0;
        var texture=new RenderTexture(2600,1200,24);camera.targetTexture=texture;camera.Render();
        var previous=RenderTexture.active;RenderTexture.active=texture;
        var image=new Texture2D(texture.width,texture.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);image.Apply();
        File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(),"AlchemyBrewing-preview.png"),image.EncodeToPNG());
        RenderTexture.active=previous;camera.targetTexture=null;texture.Release();UnityEngine.Object.Destroy(texture);UnityEngine.Object.Destroy(image);
        yield return null;
    }
}
