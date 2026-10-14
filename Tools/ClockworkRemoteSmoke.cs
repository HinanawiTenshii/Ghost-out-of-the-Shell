using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Isolated project only. Uses all production scripts/prefabs, real Unity lifecycle and serialization.
[InitializeOnLoad]
public static class ClockworkRemoteSmoke
{
    const string Pending = "ClockworkRemoteSmoke.Pending";
    static IEnumerator test;
    static int checks;
    static int lastTestFrame = -1;
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static ClockworkRemoteSmoke() { EditorApplication.update += Tick; }
    public static void Run() { SessionState.SetBool(Pending, true); EditorApplication.EnterPlaymode(); }
    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        if (Time.frameCount == lastTestFrame) return;
        lastTestFrame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(Pending, false))
            { SessionState.SetBool(Pending, false); test = Test(); }
            if (test != null && !test.MoveNext())
            { test = null; Debug.Log("CLOCKWORK_REMOTE_SMOKE_PASS checks=" + checks); EditorApplication.Exit(0); }
        }
        catch (Exception e) { test = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void Check(bool value, string label) { checks++; if (!value) throw new Exception(label); }
    static void Set(object obj, string name, object value) => obj.GetType().GetField(name, Private).SetValue(obj, value);
    static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, Private | BindingFlags.Public).Invoke(obj, args);
    static ClockworkPuppetRuntime Find(string id)
    {
        foreach (var p in ClockworkPuppetRuntime.ActivePuppets) if (p.PuppetInstanceId == id) return p;
        return null;
    }
    static void End() { ClockworkPuppetRuntime.EndActiveRemoteControl(); ClockworkPuppetBoxDriver.EndActiveRemoteControl(); }
    static IEnumerator Test()
    {
        while (DocumentReader.IsInputBlocked) yield return null;
        var a = SceneManager.CreateScene("PuppetSceneA"); SceneManager.SetActiveScene(a);
        var actor = new GameObject("Remote operator");
        var data = actor.AddComponent<ZeldaCharacterData>();
        var mover = actor.AddComponent<ZeldaFourWayMover>();
        UnityEngine.Object.DontDestroyOnLoad(actor);
        ZeldaRuntimeRegistry.ClaimControlledMover(mover);
        Check(!data.IsGhostLike && !data.IsDead, "Living player test body");
        var invObject = new GameObject("Test inventory"); invObject.SetActive(false);
        var inventory = invObject.AddComponent<PersistentInventory>();
        Set(inventory, "saveToPlayerPrefs", false); invObject.SetActive(true);
        inventory.ClearAll(); inventory.enabled = false; // Drive input methods explicitly.
        var source = Resources.Load<GameObject>("PickupItems/ClockworkPuppetPickupItem").GetComponent<ClockworkPuppetPickupItem>();
        var remote = Resources.Load<GameObject>("PickupItems/ClockworkPuppetRemotePickupItem").GetComponent<ClockworkPuppetRemotePickupItem>();
        Check(source != null && remote != null && remote.ItemVisual.InventoryIcon != null, "Production prefab/icon import");
        Check(inventory.TryAddUniqueItem(source.ItemId, "PAIR-1", 1, 1, "Puppet one", "", false, default, true, 8.25f), "Insert puppet");
        int slot = inventory.SelectedSlotIndex;
        for (int i=0;i<inventory.SlotCount;i++) if(i!=slot) inventory.SetSlot(i,"filled",1);
        Check(inventory.TryUseSelectedItem(), "R deploys through real inventory");
        var puppet = Find("PAIR-1");
        Check(puppet != null && puppet.IsUnderRemoteControl, "Deployed puppet enters control");
        Check(inventory.GetSlot(slot).ItemId == ClockworkPuppetRemotePickupItem.DefaultItemId, "Remote replaces original slot in full bag");
        Check(puppet.gameObject.scene == a && Mathf.Abs(puppet.RemainingMagic-8.25f)<.001f, "Gameplay scene and charge preserved");
        End(); Call(mover,"EndItemAttack"); yield return null;
        Check(inventory.TryUseSelectedItem() && puppet.IsUnderRemoteControl, "R re-enters bound puppet");
        End(); Call(mover,"EndItemAttack"); yield return null;
        Call(puppet,"Reclaim");
        Check(inventory.GetSlot(slot).ItemId == source.ItemId && inventory.GetSlot(slot).StoredCharge==8.25f, "Reclaim in full bag preserves charge");
        yield return null; Call(mover,"EndItemAttack");
        Check(Find("PAIR-1")==null, "Reclaim really destroys only world representation");
        Check(inventory.TryUseSelectedItem(), "Redeploy regenerates remote");
        puppet=Find("PAIR-1");
        Check(inventory.TryDropSelectedItem(), "Q during control discards projected remote");
        Check(inventory.GetSlot(slot).IsEmpty && puppet!=null && !puppet.IsUnderRemoteControl, "Q keeps puppet and releases control");
        yield return null; Call(mover,"EndItemAttack"); Call(puppet,"Reclaim");
        yield return null; Call(mover,"EndItemAttack");
        Check(inventory.TryUseSelectedItem(), "Recovery after discard regenerates on next use");
        puppet=Find("PAIR-1"); Call(puppet,"SpendMagic",2.5f); End(); Call(mover,"EndItemAttack"); yield return null;

        // Actual production scene snapshot, Unity JSON inventory, scene unload and recreated runtime.
        string json=JsonUtility.ToJson(inventory.CaptureRuntimeState());
        var manager=SceneTravelStateManager.GetOrCreate();
        Call(manager,"CaptureScene",a);
        var b=SceneManager.CreateScene("PuppetSceneB");SceneManager.SetActiveScene(b);
        Check(!remote.TryReconnect(data,"PAIR-1"), "Still-loaded other scene cannot be controlled");
        var unload=SceneManager.UnloadSceneAsync(a);while(!unload.isDone)yield return null;
        Check(inventory.GetSlot(slot).ItemInstanceId=="PAIR-1" && Find("PAIR-1")==null,"True scene unload retains remote but destroys runtime");
        Check(!remote.TryReconnect(data,"PAIR-1"),"Unloaded target cleanly refuses reconnect");
        inventory.ApplyRuntimeState(JsonUtility.FromJson<PersistentInventory.RuntimeState>(json));
        a=SceneManager.CreateScene("PuppetSceneA");SceneManager.SetActiveScene(a);
        Call(manager,"RestoreScene",a);
        var states=(IDictionary)manager.GetType().GetField("sceneStates",Private).GetValue(manager);
        Call(manager,"FinalizeRestoredDevices",a,states[a.name]);
        puppet=Find("PAIR-1");
        Check(puppet!=null && puppet.gameObject.scene==a,"Scene snapshot recreates correctly bound target in correct scene");
        Check(Mathf.Abs(puppet.RemainingMagic-5.75f)<.001f && !puppet.IsUnderRemoteControl,"Scene return preserves energy without auto-control");
        yield return null; Call(mover,"EndItemAttack");
        Check(inventory.TryUseSelectedItem()&&puppet.IsUnderRemoteControl,"Original remote reconnects recreated target after scene return");
        End(); Call(mover,"EndItemAttack"); yield return null;
        // Recreate with the exact disk-save scalar serializer as well.
        var saved=new Fields{values=SavedScalarFields.Capture(puppet,true)};
        string fieldsJson=JsonUtility.ToJson(saved);
        UnityEngine.Object.Destroy(puppet.gameObject);yield return null;
        puppet=source.DeployPuppet(Vector3.zero,null,null,-1);
        SavedScalarFields.Apply(puppet,JsonUtility.FromJson<Fields>(fieldsJson).values);
        puppet.RestoreSavedControl(null,false,false);
        Check(puppet.PuppetInstanceId=="PAIR-1"&&puppet.RemainingMagic==5.75f,"Unity JSON scalar restore preserves identity and charge");
        yield return null;
        Check(inventory.TryUseSelectedItem(),"Disk-restored target reconnects");
        Call(puppet,"SpendMagic",6f);
        Check(inventory.GetSlot(slot).IsEmpty,"Energy death removes matching remote");
        yield return null;Check(Find("PAIR-1")==null&&!ClockworkPuppetRuntime.IsRemoteControlActive,"Energy death destroys target and releases control");

        // Box control and idle restoration do not steal the active link from another box.
        Call(mover,"EndItemAttack");inventory.ClearAll();
        var boxPrefab=Resources.Load<GameObject>("PickupItems/CardboardBoxPickupItem");
        var box=UnityEngine.Object.Instantiate(boxPrefab,actor.transform.position,Quaternion.identity).GetComponent<CardboardBoxPickupItem>();
        Check(inventory.TryAddUniqueItem(source.ItemId,"BOX-1",1,1,"Box puppet"),"Insert second puppet");
        Check(inventory.TryUseSelectedItem()&&box.IsClockworkPuppetDriven,"R container deployment");
        Check(inventory.GetSlot(0).ItemId==ClockworkPuppetRemotePickupItem.DefaultItemId,"Box R also replaces slot with remote");
        End(); Call(mover,"EndItemAttack");yield return null;
        Check(inventory.TryUseSelectedItem()&&box.GetComponent<ClockworkPuppetBoxDriver>().IsUnderRemoteControl,"Box puppet reconnects");
        var idle=UnityEngine.Object.Instantiate(boxPrefab,new Vector3(20,20),Quaternion.identity).GetComponent<CardboardBoxPickupItem>();
        idle.gameObject.AddComponent<ClockworkPuppetBoxDriver>().Configure(idle,source,7,"BOX-2","","",false,Color.white,false);
        Check(box.GetComponent<ClockworkPuppetBoxDriver>().IsUnderRemoteControl,"Restoring idle box does not steal current control");
        Call(box.GetComponent<ClockworkPuppetBoxDriver>(),"SpendMagic",100f);
        Check(!box.HasStoredItem&&inventory.GetSlot(0).IsEmpty,"Box depletion destroys remote and empties container");
        foreach(var item in UnityEngine.Object.FindObjectsOfType<ClockworkPuppetRemotePickupItem>())
            Check(false,"No projected remote survives as a world pickup");
        UnityEngine.Object.Destroy(box.gameObject);UnityEngine.Object.Destroy(idle.gameObject);
        yield return null;
        var ballistaTests=ClockworkBallistaSmoke.Test(inventory,source,actor,mover,data,manager);
        while(ballistaTests.MoveNext()) yield return null;
    }
    [Serializable] public class Fields {public System.Collections.Generic.List<SavedScalarFields.Value> values;}
}
