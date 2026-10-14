using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Isolated Unity editor PlayMode integration. Uses shipped pickup prefabs and no authored scene writes.
[InitializeOnLoad]
public static class PotionBottleReturnSmoke
{
    const string Pending = "PotionBottleReturnSmoke.Pending";
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static IEnumerator test;
    static int checks, lastFrame = -1;
    static PotionBottleReturnSmoke() { EditorApplication.update += Tick; }
    public static void Run() { SessionState.SetBool(Pending, true); EditorApplication.EnterPlaymode(); }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(Pending, false)) { SessionState.SetBool(Pending, false); test = Test(); }
            if (test != null && !test.MoveNext())
            { test = null; Debug.Log("POTION_BOTTLE_RETURN_PASS checks=" + checks); EditorApplication.Exit(0); }
        }
        catch (Exception error) { test = null; Debug.LogException(error); EditorApplication.Exit(1); }
    }
    static void Check(bool value, string label) { checks++; if (!value) throw new Exception(label); }
    static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Flags).SetValue(owner, value);
    static string Json(PersistentInventory bag) => JsonUtility.ToJson(bag.CaptureRuntimeState());
    static ZeldaCharacterData Actor(string name, bool ghost = false)
    {
        var go = new GameObject(name);
        go.AddComponent<BoxCollider2D>().size = Vector2.one * .4f;
        ZeldaCharacterData data = ghost ? (ZeldaCharacterData)go.AddComponent<GhostZeldaCharacterData>() : go.AddComponent<SwordZeldaCharacterData>();
        var mover = go.AddComponent<ZeldaFourWayMover>();
        ZeldaRuntimeRegistry.ClaimControlledMover(mover);
        mover.ReceiveControl(Vector2.up);
        if (!ghost) Reset(data);
        return data;
    }
    static void Reset(ZeldaCharacterData data)
    {
        data.GetComponent<ZeldaFourWayMover>().CancelAttackForStun();
        var state = data.CaptureRuntimeState();
        state.health = 8; state.currentHealth = 8; state.possessionEnergy = 6;
        state.spentBasePossessionEnergy = 0; state.spentGrowthPossessionEnergy = 0;
        state.crystalEnergyThirds = 0; state.combatExpertiseRemaining = 0; state.potionBuffs = default;
        data.ApplyRuntimeState(state);
        if (PlayerGrowthAttributes.Instance != null) PlayerGrowthAttributes.Instance.ApplyRuntimeState(default);
    }
    static void RemoveGrowth()
    {
        if (PlayerGrowthAttributes.Instance != null) UnityEngine.Object.DestroyImmediate(PlayerGrowthAttributes.Instance.gameObject);
        Check(PlayerGrowthAttributes.Instance == null, "Fixture has no scene-provided player-growth singleton");
    }
    static void Potion(PersistentInventory bag, ZeldaCharacterData actor, AlchemyPotionKind kind, int slot = 0, bool clear = true)
    {
        actor.GetComponent<ZeldaFourWayMover>().CancelAttackForStun();
        if (clear) bag.ClearAll();
        bag.SetSlot(slot, AlchemyRecipeCatalog.GetItemId(kind), 1); bag.SelectSlot(slot);
    }
    static void Fill(PersistentInventory bag)
    {
        bag.ClearAll();
        for (int i = 0; i < bag.SlotCount; i++) bag.SetSlot(i, AlchemyRecipeCatalog.RedHerbId, 3);
    }
    static IEnumerator Test()
    {
        while (DocumentReader.IsInputBlocked) yield return null;
        var scene = SceneManager.CreateScene("Potion Bottle Return Integration"); SceneManager.SetActiveScene(scene);
        Time.timeScale = 1f;
        var root = new GameObject("Potion test inventory"); root.SetActive(false);
        var bag = root.AddComponent<PersistentInventory>();
        Set(bag, "saveToPlayerPrefs", false); Set(bag, "persistBetweenScenes", false);
        root.SetActive(true); bag.enabled = false; bag.ClearAll();
        var actor = Actor("Direct-scene controlled body");
        var mover = actor.GetComponent<ZeldaFourWayMover>();
        string spirit = AlchemyRecipeCatalog.GetItemId(AlchemyPotionKind.Spirit);
        string bottle = GlassBottlePickupItem.DefaultItemId;
        var bottlePrefab = bag.ResolveItemPrefab(bottle);
        Check(bottlePrefab is GlassBottlePickupItem && bottlePrefab.MaxStackSize == 3, "Returned-bottle resource is the normal three-stack glass bottle");

        // Direct Play in SampleScene has no player-growth object. The potion must bootstrap it.
        RemoveGrowth();
        Check(actor.TrySpendPossessionEnergy(5) && actor.FinalPossessionEnergy == 1, "Prepare five missing base energy without a growth singleton");
        Potion(bag, actor, AlchemyPotionKind.Spirit);
        Check(bag.TryUseSelectedItem(), "Spirit potion works when entering a scene directly without PlayerGrowthAttributes");
        Check(actor.FinalPossessionEnergy == 5 && actor.MaxPossessionEnergy == 6 && actor.CurrentHealth == 8, "Missing-energy branch restores exactly four without changing health or capacity");
        Check(bag.GetQuantity(spirit) == 0 && bag.GetQuantity(bottle) == 1, "Successful Spirit returns exactly one bottle");

        Reset(actor); RemoveGrowth(); Potion(bag, actor, AlchemyPotionKind.Spirit);
        Check(bag.TryUseSelectedItem(), "Full-energy Spirit also works without scene bootstrap");
        var growth = PlayerGrowthAttributes.Instance;
        Check(growth != null && actor.MaxPossessionEnergy == 8 && actor.FinalPossessionEnergy == 6, "Full energy creates persistent player-owned unfilled capacity of two");
        Check(growth.CaptureRuntimeState().potionEnergyRemaining > 29.9f && growth.CaptureRuntimeState().potionEnergyRemaining <= 30f, "Spirit temporary capacity lasts thirty seconds");
        Check(bag.GetQuantity(bottle) == 1 && !bag.Contains(spirit), "Full-energy branch also returns one bottle");
        Potion(bag, actor, AlchemyPotionKind.Spirit, 1, false);
        Check(bag.TryUseSelectedItem() && actor.FinalPossessionEnergy == 8 && actor.MaxPossessionEnergy == 8, "Following Spirit fills temporary reserve without stacking capacity");
        Check(bag.GetQuantity(bottle) == 2 && bag.GetSlot(0).Quantity == 2 && bag.GetSlot(1).IsEmpty, "Returned bottles merge into compatible existing stack");
        Potion(bag, actor, AlchemyPotionKind.Spirit, 1, false);
        Check(bag.TryUseSelectedItem() && actor.MaxPossessionEnergy == 8 && actor.FinalPossessionEnergy == 8, "Full temporary pool refreshes rather than overflows");
        Check(bag.GetSlot(0).Quantity == 3 && bag.GetSlot(1).IsEmpty, "Third returned bottle reaches stack cap three");

        // Every nonlethal recipe takes the shared successful-use transaction path.
        foreach (AlchemyPotionKind kind in Enum.GetValues(typeof(AlchemyPotionKind)))
        {
            if (kind == AlchemyPotionKind.Death) continue;
            Reset(actor); Potion(bag, actor, kind);
            int notifications = 0; bool observerSawAtomicState = false;
            Action observe = () => { notifications++; observerSawAtomicState = !bag.Contains(AlchemyRecipeCatalog.GetItemId(kind)) && bag.GetQuantity(bottle) == 1; };
            bag.InventoryChanged += observe;
            bool used = bag.TryUseSelectedItem(); bag.InventoryChanged -= observe;
            Check(used && !actor.IsDead, "Successful use of " + kind);
            Check(bag.GetSlot(0).ItemId == bottle && bag.GetSlot(0).Quantity == 1, "Consumed " + kind + " slot contains one empty bottle");
            Check(notifications == 1 && observerSawAtomicState, kind + " emits one complete inventory notification, never a consume-only intermediate state");
            if (kind == AlchemyPotionKind.Veil) Check(CharacterPotionBuffs.Has(actor, kind), "Drinking Veil does not cancel its newly granted invisibility");
        }

        Reset(actor); Fill(bag); Potion(bag, actor, AlchemyPotionKind.Might, 2, false);
        int herbCount = bag.GetQuantity(AlchemyRecipeCatalog.RedHerbId);
        Check(bag.TryUseSelectedItem() && bag.GetSlot(2).ItemId == bottle && bag.GetSlot(2).Quantity == 1, "Full inventory replaces consumed potion with returned bottle in the same slot");
        Check(bag.SelectedSlotIndex == 2 && bag.GetQuantity(AlchemyRecipeCatalog.RedHerbId) == herbCount, "Full-inventory drink preserves selection and unrelated items");

        Reset(actor); Fill(bag); bag.SetSlot(0, bottle, 2); Potion(bag, actor, AlchemyPotionKind.Might, 2, false);
        Check(bag.TryUseSelectedItem() && bag.GetSlot(0).Quantity == 3 && bag.GetSlot(2).IsEmpty, "Existing two-bottle stack is preferred and potion slot becomes empty");
        Reset(actor); Fill(bag); bag.SetSlot(0, bottle, 3); Potion(bag, actor, AlchemyPotionKind.Might, 2, false);
        Check(bag.TryUseSelectedItem() && bag.GetSlot(0).Quantity == 3 && bag.GetSlot(2).ItemId == bottle && bag.GetSlot(2).Quantity == 1, "Full bottle stack is not exceeded; another slot holds the return");

        Reset(actor); bag.ClearAll();
        const string custom = "{\"version\":1,\"speed\":12,\"radius\":18}";
        Check(bag.TryAddUniqueItem(bottle, "custom-throw-bottle-a", 1, 3, itemState: custom) &&
            bag.TryAddUniqueItem(bottle, "custom-throw-bottle-b", 1, 3, itemState: custom), "Custom throw-configuration bottle fixture");
        string savedCustom = bag.GetSlot(0).ItemState;
        Potion(bag, actor, AlchemyPotionKind.Might, 1, false);
        Check(bag.TryUseSelectedItem() && bag.GetSlot(0).Quantity == 2 && bag.GetSlot(0).ItemState == savedCustom, "Returning a default bottle does not merge into or overwrite custom throw parameters");
        Check(bag.GetSlot(1).ItemId == bottle && bag.GetSlot(1).Quantity == 1 && bag.GetSlot(1).ItemState == bottlePrefab.InventoryState, "Returned bottle receives standard projectile configuration, not potion/custom metadata");
        Check(string.IsNullOrEmpty(bag.GetSlot(1).ItemInstanceId) && bag.GetSlot(1).ItemName == bottlePrefab.ItemName, "Returned bottle has normal identity and localized name");

        string savedBag = Json(bag);
        bag.ClearAll();
        for (int i = 0; i < 3; i++)
        {
            bag.ApplyRuntimeState(JsonUtility.FromJson<PersistentInventory.RuntimeState>(savedBag));
            Check(Json(bag) == savedBag && bag.GetQuantity(bottle) == 3, "Repeated JSON restore preserves returned bottles and custom state without duplication");
        }

        Reset(actor); Fill(bag); Potion(bag, actor, AlchemyPotionKind.Might, 2, false);
        // Authoring/debug slots can contain an otherwise invalid stacked potion; never lose the bottle.
        Set(bag.GetSlot(2), "quantity", 2);
        string before = Json(bag); int events = 0; Action countEvent = () => events++; bag.InventoryChanged += countEvent;
        Check(!bag.TryUseSelectedItem(), "Malformed stacked potion with no room for a bottle is rejected before effect");
        Check(before == Json(bag) && events == 0 && !CharacterPotionBuffs.Has(actor, AlchemyPotionKind.Might), "No-space failure leaves both inventory and character unchanged");
        bag.InventoryChanged -= countEvent;

        Reset(actor); Potion(bag, actor, AlchemyPotionKind.Might);
        Check(mover.TryBeginItemAttack(.5f), "Prepare an actor whose hand animation is busy");
        before = Json(bag); events = 0; bag.InventoryChanged += countEvent;
        Check(!bag.TryUseSelectedItem() && before == Json(bag) && events == 0 && !CharacterPotionBuffs.Has(actor, AlchemyPotionKind.Might), "Attack-locked use neither drinks nor creates a bottle");
        bag.InventoryChanged -= countEvent; mover.EndItemAttack();

        Reset(actor); Potion(bag, actor, AlchemyPotionKind.Might); mover.enabled = false;
        var ghost = Actor("Controlled ghost cannot drink", true);
        before = Json(bag); events = 0; bag.InventoryChanged += countEvent;
        Check(!bag.TryUseSelectedItem() && before == Json(bag) && events == 0, "Ghost use does not consume potion or return a free bottle");
        bag.InventoryChanged -= countEvent;
        ghost.GetComponent<ZeldaFourWayMover>().enabled = false; mover.enabled = true; ZeldaRuntimeRegistry.ClaimControlledMover(mover);

        Reset(actor); Potion(bag, actor, AlchemyPotionKind.Might); bag.SetSlot(1, AlchemyRecipeCatalog.GetItemId(AlchemyPotionKind.Berserk), 1);
        bool nestedSucceeded = false; int callbacks = 0;
        Action recursiveUse = () => { callbacks++; bag.SelectSlot(1); nestedSucceeded |= bag.TryUseSelectedItem(); };
        bag.InventoryChanged += recursiveUse;
        Check(bag.TryUseSelectedItem(), "Outer potion transaction succeeds with a recursive inventory subscriber");
        bag.InventoryChanged -= recursiveUse;
        Check(!nestedSucceeded && callbacks == 1 && bag.GetQuantity(bottle) == 1 && bag.GetQuantity(AlchemyRecipeCatalog.GetItemId(AlchemyPotionKind.Berserk)) == 1, "InventoryChanged recursive R cannot consume another potion or return extra bottles");
        Check(!CharacterPotionBuffs.Has(actor, AlchemyPotionKind.Berserk), "Recursive use does not apply another effect before being rejected");

        // Poison must complete the inventory exchange even though Die changes control / destroys the body.
        Reset(actor); CharacterPotionBuffs.TryApply(actor, AlchemyPotionKind.Echo);
        Potion(bag, actor, AlchemyPotionKind.Death);
        int poisonEvents = 0; Action poisonEvent = () => poisonEvents++; bag.InventoryChanged += poisonEvent;
        Check(bag.TryUseSelectedItem() && actor.IsDead, "Triple-green potion kills the controlled body even with Echo armed");
        bag.InventoryChanged -= poisonEvent;
        Check(!bag.Contains(AlchemyRecipeCatalog.GetItemId(AlchemyPotionKind.Death)) && bag.GetQuantity(bottle) == 1 && poisonEvents == 1, "Poison also returns one bottle atomically after death");
        var poisonState = JsonUtility.FromJson<PersistentInventory.RuntimeState>(Json(bag)); bag.ClearAll(); bag.ApplyRuntimeState(poisonState);
        Check(bag.GetQuantity(bottle) == 1 && !bag.Contains(AlchemyRecipeCatalog.GetItemId(AlchemyPotionKind.Death)), "Poison death does not lose the returned bottle when saved and restored");
        yield return null;
    }
}
