using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Run only in the isolated validation project. Does not save scenes or player inventory.
[InitializeOnLoad]
public static class SkillRequirementsSmoke
{
    static IEnumerator test;
    static int checks, lastFrame = -1;
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static SkillRequirementsSmoke() { EditorApplication.update += Tick; }
    public static void Run()
    {
        UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
            UnityEditor.SceneManagement.NewSceneMode.Single);
        SessionState.SetBool("SkillRequirementsSmoke", true); EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;
        try
        {
            if (SessionState.GetBool("SkillRequirementsSmoke", false))
            { SessionState.SetBool("SkillRequirementsSmoke", false); test = Verify(); }
            if (test == null || test.MoveNext()) return;
            test = null; Debug.Log("SKILL_REQUIREMENTS_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { test = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void Check(bool value, string label) { checks++; if (!value) throw new Exception(label); }
    static FieldInfo Field(object o, string name)
    {
        for (var t = o.GetType(); t != null; t = t.BaseType)
        { var f = t.GetField(name, Flags); if (f != null) return f; }
        throw new Exception("Missing field " + name);
    }
    static void Set(object o, string name, object value) => Field(o,name).SetValue(o,value);
    static T Get<T>(object o, string name) => (T)Field(o,name).GetValue(o);
    static object Call(object o, string name, params object[] args) => o.GetType().GetMethod(name,Flags).Invoke(o,args);
    static void Skill(ZeldaCharacterData actor, int value) { Set(actor,"skillValue",value); }
    static PickupItemBase Item(string name) => Resources.Load<GameObject>("PickupItems/" + name).GetComponent<PickupItemBase>();
    static IEnumerator Verify()
    {
        Time.timeScale = 1;
        var root = new GameObject("Skill test actor");
        var data = root.AddComponent<ZeldaCharacterData>();
        var mover = root.AddComponent<ZeldaFourWayMover>();
        ZeldaRuntimeRegistry.ClaimControlledMover(mover);
        var invRoot = new GameObject("Test inventory"); invRoot.SetActive(false);
        var inventory = invRoot.AddComponent<PersistentInventory>(); Set(inventory,"saveToPlayerPrefs",false);
        invRoot.SetActive(true); inventory.ClearAll(); inventory.enabled = false;
        while (DocumentReader.IsInputBlocked) yield return null;
        Check(!data.IsDead && !data.IsGhostLike, "living test body");
        for (int required = 2; required <= 5; required++)
        {
            Skill(data,required-1); Check(!CharacterSkillRequirements.Has(data,required),"below threshold " + required);
            Skill(data,required); Check(CharacterSkillRequirements.Has(data,required),"at threshold " + required);
            Skill(data,required+1); Check(CharacterSkillRequirements.Has(data,required),"above threshold " + required);
        }
        Check(!CharacterSkillRequirements.Has(null,3),"null actor denied");
        Check(CharacterSkillRequirements.FailureMessage(3).Contains("能力值不足"),"notification text");
        int[] expected = {6,5,4,4,3,2};
        for(int i=0;i<expected.Length;i++) Check(CharacterSkillRequirements.PryEnergyCost(6,3+i)==expected[i],"round once at end, skill " + (3+i));
        Check(CharacterSkillRequirements.PryEnergyCost(6,100)==1,"positive energy never rounds to zero");
        Check(CharacterSkillRequirements.PryEnergyCost(0,3)==0,"zero base cost remains zero");
        Check(CharacterSkillRequirements.PryEnergyCost(25,5)==16,"floating point exact integer boundary");

        var box = Object.Instantiate(Resources.Load<GameObject>("PickupItems/CardboardBoxPickupItem"),Vector3.zero,Quaternion.identity)
            .GetComponent<CardboardBoxPickupItem>();
        foreach(string name in new[]{"Bombs/BombPickupItem","Bombs/PurpleBombPickupItem","Bombs/DecoyBombPickupItem","DisposableHandgunPickupItem","PuppetRemotePickupItem"})
        {
            var item = Item(name);
            Check(item.RequiredUseSkill==3,"required 3: " + name);
            Check(item.ItemDescription.EndsWith("使用需要技巧值：3。"),"description suffix " + name);
            Check(item.FormatDescriptionWithRequirements(item.ItemDescription)==item.ItemDescription,"idempotent suffix");
            Check(item.FormatDescriptionWithRequirements("old saved description").EndsWith("使用需要技巧值：3。"),"legacy saved description suffix");
            inventory.ClearAll(); inventory.SelectSlot(0); inventory.TryAddItem(item.ItemId,1,item.MaxStackSize);
            Skill(data,2); Call(mover,"EndItemAttack");
            Check(!inventory.TryUseSelectedItem(),"low skill inventory rejects " + name);
            Check(inventory.GetSlot(0).Quantity==1 && !box.HasStoredItem && !mover.IsAttacking,"no consumption, box insertion or gesture " + name);
            var instance = Object.Instantiate(item);
            Check(!instance.ExecuteUse(data),"direct ExecuteUse rejects " + name);
            Object.DestroyImmediate(instance.gameObject);
            Check(inventory.TryDropSelectedItem(),"Q still works without skill " + name);
            Call(mover,"EndItemAttack");
            foreach(var dropped in Object.FindObjectsOfType<PickupItemBase>()) if(dropped != box) Object.DestroyImmediate(dropped.gameObject);
            // Q next to a box may store the item; start the next case with an empty box.
            Object.DestroyImmediate(box.gameObject);
            box = Object.Instantiate(Resources.Load<GameObject>("PickupItems/CardboardBoxPickupItem"),Vector3.zero,Quaternion.identity).GetComponent<CardboardBoxPickupItem>();
        }
        Object.DestroyImmediate(box.gameObject);
        inventory.ClearAll(); inventory.SelectSlot(0);
        var bomb = Item("Bombs/BombPickupItem"); inventory.TryAddItem(bomb.ItemId,1,3); Skill(data,3);
        Check(inventory.TryUseSelectedItem() && inventory.GetSlot(0).IsEmpty,"bomb R accepted at threshold");
        foreach(var placed in Object.FindObjectsOfType<PlacedBomb>()) Object.DestroyImmediate(placed.gameObject);
        Call(mover,"EndItemAttack");
        var golem = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Level2NPCS/Automaton-level2.prefab"),new Vector3(40,40),Quaternion.identity);
        var automaton = golem.GetComponent<AutomatonCharacterAi>(); automaton.SetHostilityActive(false);
        ZeldaRuntimeRegistry.ClaimControlledMover(mover);
        var remote = (PuppetRemotePickupItem)Item("PuppetRemotePickupItem");
        Skill(data,2); Check(remote.ToggleSceneAutomatons(data)==0 && !automaton.HostilityActivated,"direct remote denied at 2");
        Skill(data,3); Check(remote.ToggleSceneAutomatons(data)>0 && automaton.HostilityActivated,"remote accepted at 3");
        Object.DestroyImmediate(golem);

        var gate = new GameObject("Water gate").AddComponent<WaterwayGate>();
        var controller = new GameObject("Water controller").AddComponent<WaterwayGateController>(); controller.BindGate(gate);
        bool before = gate.IsOpenRequested;
        Skill(data,2); Call(controller,"HandleInteraction"); Check(gate.IsOpenRequested==before,"water gate denied at 2");
        Skill(data,3); Call(controller,"HandleInteraction"); Check(gate.IsOpenRequested!=before,"water gate accepted at 3: blocked=" + DocumentReader.IsInputBlocked + " nearby=" + (Call(controller,"GetNearbyCharacter")!=null) + " attacking=" + mover.IsAttacking);
        Call(mover,"EndItemAttack"); gate.SetOpenImmediately(!before);
        Skill(data,0); controller.SetOpen(before); Check(gate.IsOpenRequested==before,"scripted gate unchanged");
        gate.SetOpenImmediately(before);
        var lever = new GameObject("Gate lever").AddComponent<LeverData>(); Set(lever,"linkedObject",gate.gameObject);
        Check(lever.Complexity==3,"linked lever effective requirement");
        Skill(data,2); Check(!(bool)Call(lever,"TryInteract",data),"gate lever rejects low skill");
        Skill(data,3); Check((bool)Call(lever,"TryInteract",data),"gate lever accepts 3"); Call(mover,"EndItemAttack");
        Set(lever,"linkedObject",null); Check(lever.Complexity==1,"ordinary lever unchanged after rebind");

        var door = new GameObject("Key door").AddComponent<DoorHingeInteraction>();
        Set(door,"requiredUnlockItemInstanceId","TEST-KEY"); door.SetLocked(true);
        var key = Item("KeyPickupItem"); inventory.ClearAll();
        Check(inventory.TryAddUniqueItem(key.ItemId,"TEST-KEY",1,1),"test key added");
        Skill(data,1); Check(!door.TryInteract() && door.IsLocked && !inventory.GetSlot(0).IsEmpty,"low skill preserves key and lock");
        Check(door.RequiredUnlockSkill==2,"locked key requirement exposed");
        var leaf = new GameObject("Door leaf"); leaf.transform.SetParent(door.transform);
        var doorData = leaf.AddComponent<DoorData>();
        var popup = leaf.GetComponentInChildren<ZeldaRequirementWindow>(true);
        Check(popup != null,"requirement popup created"); Call(popup,"Refresh",true);
        var skillRow = Get<RectTransform>(popup,"skillRow");
        Check(skillRow && skillRow.gameObject.activeSelf && skillRow.GetComponentsInChildren<Image>().Length==2,"two wrench icons plus original durability");
        Skill(data,2); Check(door.TryInteract() && !door.IsLocked && inventory.GetSlot(0).IsEmpty,"key unlock at 2 consumes exactly one key");
        Call(popup,"Refresh",false); Check(!skillRow.gameObject.activeSelf,"unlocked door hides skill row");
        Set(door,"passwordLockEnabled",true); door.SetLocked(true); Skill(data,1);
        Call(door,"OpenPasswordWindow"); Check(!DoorPasswordPanel.IsOpen && Time.timeScale==1,"password low skill no menu or pause");
        Skill(data,2); Call(door,"OpenPasswordWindow"); Check(DoorPasswordPanel.IsOpen && Time.timeScale==0,"password menu accepted at 2");
        Skill(data,1); Call(door,"HandlePasswordResult",true); Check(door.IsLocked,"password commit rechecks skill");
        Skill(data,2); Call(door,"HandlePasswordResult",true); Check(!door.IsLocked,"correct password accepted at 2");
        Object.DestroyImmediate(door.GetComponent<DoorPasswordPanel>()); Time.timeScale=1;

        var pillar = new GameObject("Electric pillar").AddComponent<ElectricGatePillar>();
        Skill(data,4); ElectricGatePanel.Open(pillar,data,null); Check(!ElectricGatePanel.IsOpen && Time.timeScale==1,"electric panel denied at 4");
        Skill(data,5); ElectricGatePanel.Open(pillar,data,null); Check(ElectricGatePanel.IsOpen && Time.timeScale==0,"electric panel accepted at 5");
        ElectricGatePanel.CloseFor(pillar); yield return null; Time.timeScale=1;
        Call(mover,"EndItemAttack");
        var cauldron = new GameObject("Cauldron").AddComponent<AlchemyCauldron>();
        inventory.ClearAll(); inventory.SelectSlot(0); inventory.TryAddItem(AlchemyRecipeCatalog.RedHerbId,3,3);
        Skill(data,3); Check(!cauldron.TryInteract(out string message) && message.Contains("能力值不足"),"cauldron denies low skill with prompt");
        Check(cauldron.IngredientCount==0 && inventory.GetSlot(0).Quantity==3,"herbs unchanged");
        Skill(data,4); Check(cauldron.TryInteract(out message) && cauldron.RedCount==1 && inventory.GetSlot(0).Quantity==2,"cauldron accepts at 4");
        Call(mover,"EndItemAttack"); cauldron.ApplyRuntimeState(new AlchemyCauldron.RuntimeState{red=3});
        inventory.ClearAll(); inventory.TryAddItem(GlassBottlePickupItem.DefaultItemId,1,3); Skill(data,3);
        Check(!cauldron.TryInteract(out message) && cauldron.IsReady && inventory.GetSlot(0).ItemId==GlassBottlePickupItem.DefaultItemId,"bottle and brew preserved below threshold");
        Skill(data,4); Check(cauldron.TryInteract(out message) && !cauldron.IsReady,"bottle exchange accepted at 4"); Call(mover,"EndItemAttack");

        var puppetItem = (ClockworkPuppetPickupItem)Item("ClockworkPuppetPickupItem");
        Check(puppetItem.RequiredUseSkill==0 && Item("ClockworkPuppetRemotePickupItem").RequiredUseSkill==0,"deployment and reconnect unrestricted");
        Check(puppetItem.ItemDescription.Contains(CharacterSkillRequirements.PryDescription) &&
            Item("ClockworkPuppetRemotePickupItem").ItemDescription.Contains(CharacterSkillRequirements.PryDescription) &&
            puppetItem.ItemDescription.EndsWith("使用需要技巧值：0。") &&
            Item("ClockworkPuppetRemotePickupItem").ItemDescription.EndsWith("使用需要技巧值：0。"),"both puppet descriptions retain pry requirement before the general zero-use footer");
        Skill(data,2); var puppet=puppetItem.DeployPuppet(new Vector3(20,20),data,null,12f);
        Check(puppet && puppet.IsUnderRemoteControl,"low skill can still deploy puppet");
        door.SetLocked(true); float energy=puppet.RemainingMagic;
        Check(!(bool)Call(puppet,"TryCompleteDoorPry",door) && door.IsLocked && puppet.RemainingMagic==energy,"low skill cannot pry or spend energy");
        int[] liveExpected = {10,8,7,6,5}; // Production prefab: maximum 20, original pry fraction .5.
        for(int skill=3;skill<=7;skill++)
        {
            Skill(data,skill); door.SetLocked(true); Set(puppet,"magic",12f);
            int cost=puppet.GetPryMagicCost(data);
            Check(cost==liveExpected[skill-3],"live owner cost " + skill + ": " + cost);
            Check((bool)Call(puppet,"TryCompleteDoorPry",door) && !door.IsLocked && puppet.RemainingMagic==12-cost,"pry unlock and exact energy deduction " + skill);
        }
        door.SetLocked(true); Set(puppet,"magic",.5f);
        Check(!(bool)Call(puppet,"TryCompleteDoorPry",door) && door.IsLocked && puppet.RemainingMagic==.5f,"insufficient energy does not unlock");
        Set(puppet,"magic",12f); Set(door,"canBeLockpicked",false);
        Check(!(bool)Call(puppet,"TryCompleteDoorPry",door) && door.IsLocked && puppet.RemainingMagic==12f,"unpickable lock unchanged");
        var growth = new GameObject("Growth test").AddComponent<PlayerGrowthAttributes>();
        Skill(data,2); growth.AddSkillValue(1);
        Check(data.FinalSkillValue==3 && CharacterSkillRequirements.Has(data,3),"growth skill counts for requirements");
        Check(puppet.GetPryMagicCost(data)==10,"growth counts for pry base threshold");
        growth.AddSkillValue(1); Check(puppet.GetPryMagicCost(data)==8,"growth improves pry energy cost");
        ClockworkPuppetRuntime.EndActiveRemoteControl();
        yield return null;
    }
}
