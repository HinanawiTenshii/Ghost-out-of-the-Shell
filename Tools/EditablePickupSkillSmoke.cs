using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Execute only in an isolated Unity project; never saves changed prefab test values.
[InitializeOnLoad]
public static class EditablePickupSkillSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static int frame = -1, checks;
    static IEnumerator suite;
    static double deadline;
    static EditablePickupSkillSmoke() { EditorApplication.update += Tick; }
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var field = typeof(PickupItemBase).GetField("requiredUseSkill", BindingFlags.Instance | BindingFlags.Public);
        Check(field != null && field.FieldType == typeof(int), "public editable integer on shared pickup base");
        foreach (var prefab in Resources.LoadAll<GameObject>("PickupItems"))
        {
            var item = prefab.GetComponent<PickupItemBase>(); if (!item) continue;
            int expected = item is BombPickupItem || item is DisposableHandgunPickupItem || item is PuppetRemotePickupItem ? 3 : 0;
            Check(item.RequiredUseSkill == expected && item.requiredUseSkill == expected, "preserved default: " + prefab.name);
            var serialized = new SerializedObject(item);
            Check(serialized.FindProperty("requiredUseSkill").intValue == expected, "inspector scalar: " + prefab.name);
            Check(serialized.FindProperty("useSkillRequirementInitialized").boolValue, "migrated prefab or variant: " + prefab.name);
            Check(item.ItemDescription.EndsWith("使用需要技巧值：" + expected + "。"), "footer for all items including zero: " + prefab.name);
            Check(item.FormatDescriptionWithRequirements(item.ItemDescription) == item.ItemDescription, "no duplicate footer: " + prefab.name);
        }
        SessionState.SetBool(nameof(EditablePickupSkillSmoke), true); EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return; frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(EditablePickupSkillSmoke), false))
            { SessionState.SetBool(nameof(EditablePickupSkillSmoke), false); Time.timeScale = 1; deadline = EditorApplication.timeSinceStartup + 45; suite = Verify(); }
            if (suite == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("test timed out");
            if (suite.MoveNext()) return;
            suite = null; Debug.Log("EDITABLE_PICKUP_SKILL_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void Check(bool value, string label) { checks++; if (!value) throw new Exception(label); }
    static FieldInfo Field(object o, string name)
    {
        for (var t = o.GetType(); t != null; t = t.BaseType)
        { var f = t.GetField(name, F | BindingFlags.DeclaredOnly); if (f != null) return f; }
        throw new Exception("field " + name);
    }
    static void Set(object o, string name, object value) => Field(o, name).SetValue(o, value);
    static PickupItemBase Item(string name) => Resources.Load<GameObject>("PickupItems/" + name).GetComponent<PickupItemBase>();
    static int Occurrences(string text, string token) => text.Split(new[] { token }, StringSplitOptions.None).Length - 1;
    static IEnumerator Verify()
    {
        var actor = new GameObject("Skill actor").AddComponent<ZeldaCharacterData>();
        var mover = actor.gameObject.AddComponent<ZeldaFourWayMover>(); ZeldaRuntimeRegistry.ClaimControlledMover(mover);
        Set(actor, "skillValue", 2);
        var invRoot = new GameObject("Inventory"); invRoot.SetActive(false);
        var inventory = invRoot.AddComponent<PersistentInventory>(); Set(inventory, "saveToPlayerPrefs", false);
        invRoot.SetActive(true); inventory.ClearAll(); inventory.enabled = false;
        // Below-threshold direct use cannot bypass the common field on ANY type.
        foreach (var prefab in Resources.LoadAll<GameObject>("PickupItems"))
        {
            if (!prefab.GetComponent<PickupItemBase>()) continue;
            var item = Object.Instantiate(prefab).GetComponent<PickupItemBase>();
            item.requiredUseSkill = 5;
            Check(item.RequiredUseSkill == 5 && !item.ExecuteUse(actor), "direct use honors edited value: " + prefab.name);
            Check(item.ItemDescription.EndsWith("使用需要技巧值：5。"), "edited footer: " + prefab.name);
            Object.DestroyImmediate(item.gameObject);
        }
        ZeldaRuntimeRegistry.ClaimControlledMover(mover);
        var bomb = (BombPickupItem)Item("Bombs/BombPickupItem");
        var copy = Object.Instantiate(bomb);
        EditorJsonUtility.FromJsonOverwrite("{\"requiredUseSkill\":0,\"useSkillRequirementInitialized\":false}", copy);
        copy.OnAfterDeserialize(); Check(copy.RequiredUseSkill == 3, "old serialized bomb migrates to previous requirement");
        copy.requiredUseSkill = 0;
        string saved = EditorJsonUtility.ToJson(copy);
        copy.requiredUseSkill = 7; EditorJsonUtility.FromJsonOverwrite(saved, copy);
        Check(copy.requiredUseSkill == 0 && copy.RequiredUseSkill == 0, "authored zero survives serialization instead of reverting to three");
        copy.requiredUseSkill = -3; Check(copy.RequiredUseSkill == 0, "negative runtime input clamps to zero");
        Object.DestroyImmediate(copy.gameObject);

        string custom = "自定义说明：这是某个任务用的炸弹。";
        bomb.requiredUseSkill = 5;
        string corrected = bomb.FormatDescriptionWithRequirements(custom + "\n使用需要技巧值：3。\n使用需要技巧值：4。\n");
        Check(corrected == custom + "\n使用需要技巧值：5。", "old save footer replaced, prose preserved");
        Check(bomb.FormatDescriptionWithRequirements(corrected) == corrected, "reopening never duplicates requirement");
        Check(bomb.FormatDescriptionWithRequirements(null) == "使用需要技巧值：5。", "blank description still shows requirement");
        var puppet = Item("ClockworkPuppetPickupItem"); puppet.requiredUseSkill = 2;
        string puppetText = puppet.FormatDescriptionWithRequirements("人偶说明\n" + CharacterSkillRequirements.PryDescription + "\n使用需要技巧值：0。");
        Check(puppetText.EndsWith("使用需要技巧值：2。") && Occurrences(puppetText, CharacterSkillRequirements.PryDescription) == 1,
            "special pry requirement preserved once; general editable requirement is last");
        puppet.requiredUseSkill = 0;

        inventory.TryAddItem(bomb.ItemId, 1, bomb.MaxStackSize);
        Check(!inventory.TryUseSelectedItem() && inventory.GetSlot(0).Quantity == 1 && Object.FindObjectsOfType<PlacedBomb>().Length == 0,
            "updated prefab threshold denies inventory use without consuming or spawning");
        Set(actor, "skillValue", 5);
        Check(inventory.TryUseSelectedItem() && inventory.GetSlot(0).IsEmpty, "updated prefab threshold permits use at equality");
        foreach (var placed in Object.FindObjectsOfType<PlacedBomb>()) Object.DestroyImmediate(placed.gameObject);
        mover.EndItemAttack();
        // An old slot snapshot does not lock in its former requirement.
        inventory.ApplyRuntimeState(new PersistentInventory.RuntimeState { slots = new[] {
            new PersistentInventory.RuntimeSlotState { itemId = bomb.ItemId, quantity = 1,
                itemDescription = custom + "\n使用需要技巧值：3。" } } });
        bomb.requiredUseSkill = 6; Set(actor, "skillValue", 5);
        Check(!inventory.TryUseSelectedItem() && inventory.GetSlot(0).Quantity == 1, "old inventory uses current prefab value");
        var window = ItemDescriptionWindow.GetOrCreate();
        Check(window.OpenForSlot(inventory.GetSlot(0)), "inventory description window opens");
        string displayed = ((Text)Field(window, "descriptionText").GetValue(window)).text;
        Check(displayed == custom + "\n使用需要技巧值：6。", "actual UI replaces stale footer using prefab configuration");
        window.CloseWindow(); Time.timeScale = 1;
        bomb.requiredUseSkill = 0; Set(actor, "skillValue", 0); mover.EndItemAttack();
        Check(inventory.TryUseSelectedItem(), "setting formerly gated bomb to zero removes requirement");
        foreach (var placed in Object.FindObjectsOfType<PlacedBomb>()) Object.DestroyImmediate(placed.gameObject);
        bomb.requiredUseSkill = 3; mover.EndItemAttack();
        inventory.ClearAll(); inventory.TryAddItem(bomb.ItemId, 1, bomb.MaxStackSize);
        Check(inventory.TryDropSelectedItem(), "Q drop remains unrestricted by use skill");
        inventory.ClearAll();

        var golem = Item("ArmedGolemPickupItem"); golem.requiredUseSkill = 5;
        inventory.TryAddUniqueItem(golem.ItemId, "skill-golem", 1, 1);
        Check(!inventory.TryUseSelectedItem() && !inventory.GetSlot(0).IsEmpty, "R placement shortcut also checks edited skill");
        golem.requiredUseSkill = 0; inventory.ClearAll();
        var remote = (ClockworkPuppetRemotePickupItem)Item("ClockworkPuppetRemotePickupItem"); remote.requiredUseSkill = 4;
        Check(!remote.TryReconnect(actor, "absent-link"), "direct reconnect honors general requirement");
        remote.requiredUseSkill = 0;
        yield return null;
    }
}
