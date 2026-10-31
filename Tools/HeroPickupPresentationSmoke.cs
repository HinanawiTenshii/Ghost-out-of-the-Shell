using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class HeroPickupPresentationSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly Stack<IEnumerator> steps = new Stack<IEnumerator>();
    static int frame = -1, checks;
    static double deadline;
    static HeroPickupPresentationSmoke() { EditorApplication.update += Tick; }
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    static FieldInfo Field(object o, string name)
    {
        for (var t = o.GetType(); t != null; t = t.BaseType) { var f = t.GetField(name,F); if (f != null) return f; }
        throw new Exception("field " + name);
    }
    static T Get<T>(object o,string name) => (T)Field(o,name).GetValue(o);
    static void Set(object o,string name,object value) => Field(o,name).SetValue(o,value);
    static void Call(object o,string name,params object[] args) => typeof(HeroZeldaCharacterData).GetMethod(name,F).Invoke(o,args);
    public static void Run()
    {
        Check(Application.dataPath.Replace('\\','/').Contains("/.codex-temp/"),"isolated only");
        PlayerSettings.productName = "hero-pickup-smoke";
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        SessionState.SetBool(nameof(HeroPickupPresentationSmoke),true); EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(HeroPickupPresentationSmoke),false))
            { SessionState.SetBool(nameof(HeroPickupPresentationSmoke),false); deadline = EditorApplication.timeSinceStartup+100; steps.Push(Verify()); }
            if (steps.Count == 0) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("test timeout");
            while (steps.Count > 0)
            {
                var e = steps.Peek(); if (!e.MoveNext()) { steps.Pop(); continue; }
                if (e.Current is IEnumerator nested) { steps.Push(nested); continue; }
                return;
            }
            Debug.Log("HERO_PICKUP_PRESENTATION_PASS checks="+checks); EditorApplication.Exit(0);
        }
        catch(Exception e) { steps.Clear(); Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static IEnumerator Until(Func<bool> test,string message)
    {
        double end=EditorApplication.timeSinceStartup+4;
        while(!test()) { if(EditorApplication.timeSinceStartup>end) throw new Exception(message); yield return null; }
        Check(true,message);
    }
    static PickupItemBase Item(string path)
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/PickupItems/"+path+".prefab");
        Check(prefab!=null,"item exists "+path);
        return Object.Instantiate(prefab,new Vector3(30,30),Quaternion.identity).GetComponent<PickupItemBase>();
    }
    static IEnumerator Verify()
    {
        while(Time.frameCount<3 || RetroSceneLoadReveal.IsPreparingGameplay) yield return null;
        Time.timeScale=1;
        var inventory=PersistentInventory.Instance;
        if(inventory==null)
        {
            var invRoot=new GameObject("Pickup Inventory"); invRoot.SetActive(false);
            inventory=invRoot.AddComponent<PersistentInventory>(); Set(inventory,"saveToPlayerPrefs",false); invRoot.SetActive(true);
        }
        inventory.ClearAll(); ItemDescriptionWindow.ResetDiscoveryHistoryIfPresent();
        var heroes=new HeroZeldaCharacterData[3];
        string[] names={"Hero","Hero-Unarmed","Hero-ShieldOnly"};
        for(int i=0;i<3;i++)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/VitalNPCS/"+names[i]+".prefab");
            heroes[i]=Object.Instantiate(prefab,new Vector3(i*4,0),Quaternion.identity).GetComponent<HeroZeldaCharacterData>();
            heroes[i].GetComponent<ZeldaFourWayMover>().ReceiveControl(Vector2.left);
        }
        Render(heroes);
        for(int i=0;i<3;i++)
        {
            var hero=heroes[i]; var mover=hero.GetComponent<ZeldaFourWayMover>();
            foreach(var other in heroes) other.GetComponent<ZeldaFourWayMover>().enabled=false;
            mover.ReceiveControl(Vector2.left); ZeldaRuntimeRegistry.ClaimControlledMover(mover);
            ActiveZeldaCharacterDataSource.Instance?.Refresh();
            yield return null;
            inventory.ClearAll(); ItemDescriptionWindow.ResetDiscoveryHistoryIfPresent();
            var item=Item(i==0?"KeyPickupItem":i==1?"Bombs/BombPickupItem":"DisposableHandgunPickupItem");
            if(i==0) item.ItemVisual.SetDisplayColor(new Color(.65f,.13f,.8f));
            var originalIcon=item.ItemVisual.InventoryIcon;
            Vector3 before=hero.transform.position, scale=hero.transform.localScale;
            Vector2 colliderSize=hero.GetComponent<BoxCollider2D>().size;
            int hitboxes=Object.FindObjectsOfType<ZeldaAttackHitbox>().Length;
            string itemId=item.ItemId;
            float pickedAt=Time.unscaledTime;
            Check(item.TryPickUp(),names[i]+" successfully picks item");
            Check(hero.IsPresentingPickup && mover.IsUsingItemAttack,"pickup starts special pose and movement lock");
            Check(!ItemDescriptionWindow.IsOpen,"first description waits for presentation");
            Check(Get<object>(hero,"pickupHalo")==null,"no halo before reaching highest point");
            Check(Mathf.Abs(hero.PickupPresentationDuration-hero.PickupRaiseDuration-2f)<.0001f,"hold duration is two seconds in addition to raising");
            Check(!mover.TryPerformAttack(Vector2.left,null),"presentation cannot spawn a melee attack");
            var during=Item("GoldCoinPickupItem");
            Check(!during.TryPickUp() && during!=null,"rapid second pickup waits without consuming or overwriting first");
            Object.Destroy(during.gameObject);
            var held=Get<SpriteRenderer>(hero,"heldPickupRenderer");
            Check(held!=null && held.sprite!=originalIcon && held.sprite.texture!=originalIcon.texture,"owns independent icon and texture");
            Check(held.GetComponents<Collider2D>().Length==0 && held.GetComponent<PickupItemBase>()==null,"pure visual has no collectible or collider");
            yield return null; yield return null;
            Check(item==null && held.sprite!=null && held.sprite.texture!=null,"icon survives destruction of world item");
            Check(inventory.GetQuantity(itemId)==1,"exactly one item awarded");
            Check(hero.transform.position==before && hero.transform.localScale==scale && hero.GetComponent<BoxCollider2D>().size==colliderSize,"body and physics not moved or scaled");
            Check(mover.FacingDirection==Vector2.left && Object.FindObjectsOfType<ZeldaAttackHitbox>().Length==hitboxes,"no direction change or melee damage");
            yield return Until(()=>hero.IsPickupAtPeak && Get<object>(hero,"pickupHalo")!=null,"halo starts at highest point");
            var halo=Get<object>(hero,"pickupHalo");
            var haloRoot=Get<GameObject>(halo,"root");
            var haloMesh=Get<Mesh>(halo,"mesh");
            Vector3 peakPosition=held.transform.localPosition;
            Check(haloRoot.transform.localPosition==peakPosition,"halo centred on the held item");
            Check(haloMesh.vertexCount==456 && haloRoot.GetComponentsInChildren<Renderer>().Length==1 &&
                haloRoot.GetComponent<Collider2D>()==null,"single bounded visual mesh with no collision");
            Check(haloRoot.GetComponent<MeshRenderer>().sortingOrder<held.sortingOrder,"glow stays behind readable item");
            while(Time.unscaledTime<pickedAt+hero.PickupRaiseDuration+1.85f) yield return null;
            Check(hero.IsPresentingPickup && hero.IsPickupAtPeak && !ItemDescriptionWindow.IsOpen,"window stays shut throughout two-second peak hold");
            Check(held.transform.localPosition==peakPosition && held.color.a>.99f,"held item does not lower or fade during the hold");
            yield return Until(()=>!hero.IsPresentingPickup,"presentation completes");
            yield return Until(()=>ItemDescriptionWindow.IsOpen,"description opens after pose");
            Check(Time.unscaledTime-pickedAt>=hero.PickupRaiseDuration+1.98f,"window opens only after the full peak hold");
            Check(!mover.IsUsingItemAttack,"movement lock released even when window pauses game");
            Check(Get<SpriteRenderer>(hero,"heldPickupRenderer")==null,"held item removed after pose");
            yield return null;
            Check(Get<object>(hero,"pickupHalo")==null && haloRoot==null && haloMesh==null,"glow geometry and object released after display");
            ItemDescriptionWindow.GetOrCreate().CloseWindow(); Time.timeScale=1;
            yield return null;
        }
        var target=heroes[2]; var controller=target.GetComponent<ZeldaFourWayMover>();
        // Known items still perform the pose, but don't repeatedly open the modal.
        var repeated=Item("DisposableHandgunPickupItem");
        Check(repeated.TryPickUp() && target.IsPresentingPickup,"repeat pickup also celebrates");
        Time.timeScale=0;
        yield return Until(()=>!target.IsPresentingPickup,"unscaled animation cannot get stuck while paused");
        Check(!ItemDescriptionWindow.IsOpen && !controller.IsUsingItemAttack,"known pickup has no repeated modal or frozen lock"); Time.timeScale=1;
        inventory.ClearAll();
        for(int i=0;i<inventory.SlotCount;i++) inventory.TryAddItem("smoke_fill_"+i,1,1);
        var full=Item("KeyPickupItem"); Check(!full.TryPickUp() && !target.IsPresentingPickup,"full inventory does not show reward pose"); Object.Destroy(full.gameObject);
        inventory.ClearAll();
        var cancel=Item("GoldCoinPickupItem");
        Check(target.TryPresentPickedItem(cancel,controller),"start control-loss case");
        Set(target,"pickupElapsed",target.PickupRaiseDuration+.2f);
        target.ApplyCharacterVisual(target.transform.Find("Visual").GetComponent<SpriteRenderer>(),Vector2.right,false,true);
        Check(Get<object>(target,"pickupHalo")!=null,"cancellation case has active peak glow");
        controller.enabled=false; yield return null;
        Check(!target.IsPresentingPickup && Get<SpriteRenderer>(target,"heldPickupRenderer")==null,"control loss cleans presentation");
        Check(Get<object>(target,"pickupHalo")==null,"control loss also removes halo");
        controller.ReceiveControl(Vector2.right);
        Check(target.TryPresentPickedItem(cancel,controller),"start save-restore case");
        target.ApplyRuntimeState(target.CaptureRuntimeState());
        Check(!target.IsPresentingPickup && !controller.IsUsingItemAttack,"loading does not serialize cosmetic pose");
        Check(target.TryPresentPickedItem(cancel,controller),"start stun cancellation case"); controller.CancelAttackForStun(); yield return null;
        Check(!target.IsPresentingPickup,"interrupted pose cleans up");
        Object.Destroy(cancel.gameObject);
        // Ordinary characters retain the existing short single-hand gesture.
        controller.enabled=false;
        var civilian=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Level2NPCS/Idle/Civilian-level2-Idle.prefab"));
        var civilianMover=civilian.GetComponent<ZeldaFourWayMover>(); civilianMover.ReceiveControl(Vector2.down);
        ZeldaRuntimeRegistry.ClaimControlledMover(civilianMover); ActiveZeldaCharacterDataSource.Instance?.Refresh();
        var common=Item("GoldCoinPickupItem"); ItemDescriptionWindow.ApplyDiscoveryHistory(new[]{common.DescriptionDiscoveryId});
        Check(common.TryPickUp() && civilianMover.IsUsingItemAttack,"nonhero keeps normal pickup gesture");
        Check(civilian.transform.Find("Hero Held Pickup")==null,"no hero effect on other characters");
    }

    static void Render(HeroZeldaCharacterData[] heroes)
    {
        var cameraRoot=new GameObject("Hero Pickup Preview Camera"); var camera=cameraRoot.AddComponent<Camera>(); camera.enabled=false;
        camera.orthographic=true; camera.orthographicSize=1.4f; camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.055f,.12f,.18f);
        var rt=RenderTexture.GetTemporary(360,360,24,RenderTextureFormat.ARGB32); var previous=RenderTexture.active;
        var image=new Texture2D(1080,1080,TextureFormat.RGB24,false); camera.targetTexture=rt;
        for(int i=0;i<heroes.Length;i++)
        {
            var hero=heroes[i]; var item=Item(i==0?"KeyPickupItem":i==1?"Bombs/BombPickupItem":"TrialProofPickupItem");
            if(i==0) item.ItemVisual.SetDisplayColor(new Color(.65f,.13f,.8f));
            Check(hero.TryPresentPickedItem(item,hero.GetComponent<ZeldaFourWayMover>()),"preview variant supports pose");
            var body=hero.transform.Find("Visual").GetComponent<SpriteRenderer>();
            for(int phase=0;phase<3;phase++)
            {
                VerifyRectangularArms(hero, phase);
                Set(hero,"pickupElapsed",phase==0?0f:phase==1?.15f:.5f);
                hero.ApplyCharacterVisual(body,Vector2.left,false,true);
                cameraRoot.transform.position=hero.transform.position+new Vector3(0,.8f,-10);
                camera.Render(); RenderTexture.active=rt;
                image.ReadPixels(new Rect(0,0,360,360),i*360,(2-phase)*360);
                Check(body.sprite.pixelsPerUnit==16,"presentation keeps 16 PPU");
                Check(body.sprite.pivot.y==4,"ground pivot unchanged");
                var pixels=body.sprite.texture.GetPixels32();
                bool shortArms=true;
                for(int y=16;y<24;y++) for(int x=0;x<20;x++) shortArms &= pixels[y*20+x].a==0;
                Check(shortArms,"short arms do not grow past the original head silhouette");
                if(phase>0)
                {
                    foreach(int x in new[]{4,11}) Check(pixels[5*20+x+2].a==0,"no resting lower arm left below raised arm");
                    for(int y=5;y<=8;y++) foreach(int x in new[]{5,10})
                        Check(pixels[y*20+x+2].a==255,"continuous torso sides from shoulder to hem without one-pixel notches");
                    foreach(int x in new[]{4,5,10,11}) Check(pixels[8*20+x+2].a>0,"raised arm has edge-connected shoulder");
                }
            }
            Call(hero,"EndPickupPresentation"); Object.Destroy(item.gameObject);
        }
        image.Apply(); File.WriteAllBytes(Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Hero-Pickup-preview.png"),image.EncodeToPNG());
        camera.targetTexture=null; RenderTexture.active=previous; RenderTexture.ReleaseTemporary(rt); Object.Destroy(cameraRoot); Object.Destroy(image);
    }

    static void VerifyRectangularArms(HeroZeldaCharacterData hero,int pose)
    {
        for(int side=0;side<2;side++)
        {
            var pixels=new Color[20*24]; Call(hero,"DrawPickupArm",pixels,side,pose);
            int count=0,minX=20,maxX=-1,minY=24,maxY=-1;
            for(int y=0;y<24;y++) for(int x=0;x<20;x++)
                if(pixels[y*20+x].a>0) { count++; minX=Mathf.Min(minX,x); maxX=Mathf.Max(maxX,x); minY=Mathf.Min(minY,y); maxY=Mathf.Max(maxY,y); }
            Check(count==8,"all poses keep the complete eight-pixel arm");
            Check((maxX-minX+1)==(pose==1?4:2) && (maxY-minY+1)==(pose==1?2:4),"arm rotates as a rigid rectangle");
            for(int y=minY;y<=maxY;y++) for(int x=minX;x<=maxX;x++) Check(pixels[y*20+x].a==1,"no hole or offset between sleeve and hand");
        }
    }
}
