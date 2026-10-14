using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Executed only in the isolated test project; never writes the user's scenes/saves.
[InitializeOnLoad]
public static class DialogueShopSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static IEnumerator suite;
    static int frame = -1, checks;
    static double deadline;
    static DialogueShopSmoke() { EditorApplication.update += Tick; }
    static void Check(bool value, string label) { checks++; if (!value) throw new Exception(label); }
    static void Set(object o, string name, object value) => o.GetType().GetField(name, F).SetValue(o, value);
    static object Get(object o, string name) => o.GetType().GetField(name, F).GetValue(o);
    static object Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, F).Invoke(o, args);
    static PickupItemBase Template(string name) => Resources.Load<GameObject>("PickupItems/" + name).GetComponent<PickupItemBase>();
    static PickupItemBase Stock(Transform parent, PickupItemBase prefab, string name)
    {
        var item = Object.Instantiate(prefab, parent); item.name = name; return item;
    }
    static DialogueShopPurchaseEffect Shop(GameObject owner, Transform stock, PickupItemBase item, int price = 3)
    {
        var result = owner.AddComponent<DialogueShopPurchaseEffect>();
        Set(result, "stockRoot", stock); Set(result, "merchandise", item); Set(result, "coinCost", price); return result;
    }
    public static void Run()
    {
        Check(Application.dataPath.Replace('\\','/').Contains("/.codex-temp/"), "isolated project only");
        // Verify production scene serialization without starting the large level.
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Level2/Level2-Floor1.unity");
        var owner = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CharacterDialogue>(true))
            .Single(c => c.name == "弓箭铺店主");
        var effect = owner.GetComponent<DialogueShopPurchaseEffect>();
        Check(effect != null && effect.ReadStockCount() == 6, "authored owner resolves six real guns");
        Check(((Transform)Get(effect, "stockRoot")).name == "枪械店", "bound to gun-shop root only");
        Check(owner.StartNode.text.Contains("招牌上是弓箭，柜台上是手铳"), "authored gun-shop greeting");
        Check(owner.StartNode.choices[2].effect == effect && owner.StartNode.choices[2].action == CharacterDialogue.ChoiceAction.Continue &&
            owner.StartNode.choices[2].nextNodeId == "shop", "first purchase continues into repeatable shop");
        Check(owner.FindNode("node_d52f1832").choices[1].effect == effect, "second purchase connected");
        Check(effect.GetChoiceText(owner, null, "购买") == "购买一次性手铳X1（消耗3金币，剩余6）", "exact label format");
        var toolOwner = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CharacterDialogue>(true))
            .Single(c => c.name == "工具铺店主");
        var toolEffect = toolOwner.GetComponent<DialogueShopPurchaseEffect>();
        Check(toolEffect != null && toolEffect.ReadStockCount() == 3, "tool shop resolves three real puppets");
        Check(((Transform)Get(toolEffect, "stockRoot")).name == "工具店" &&
            Get(toolEffect, "merchandise") is ClockworkPuppetPickupItem, "tool shop binds exact stock group and puppet prefab");
        Check(toolOwner.StartNode.text.Contains("机关魔像") && toolOwner.StartNode.text.Contains("九枚金币"), "authored tool-shop greeting");
        Check(toolOwner.StartNode.choices[1].effect == toolEffect &&
            toolOwner.StartNode.choices[1].action == CharacterDialogue.ChoiceAction.Continue &&
            toolOwner.StartNode.choices[1].nextNodeId == "shop", "tool shop purchase continues into repeatable shop");
        Check(toolEffect.GetChoiceText(toolOwner, null, "购买") == "购买机关魔像X1（消耗9金币，剩余3）", "tool shop nine-coin label");
        var weaponOwner = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CharacterDialogue>(true))
            .Single(c => c.name == "武器铺店主");
        var weaponEffects = weaponOwner.GetComponents<DialogueShopPurchaseEffect>();
        Check(weaponEffects.Length == 2, "weapon shop has independent offers");
        var proximityOffer = weaponEffects.Single(e => ((PickupItemBase)Get(e, "merchandise")).ItemId == "proximity_bomb");
        var bombOffer = weaponEffects.Single(e => ((PickupItemBase)Get(e, "merchandise")).ItemId == "bomb");
        Check(proximityOffer.ReadStockCount() == 2 && bombOffer.ReadStockCount() == 7, "weapon shop stock split by exact item ID");
        Check(((Transform)Get(proximityOffer, "stockRoot")).name == "武器店" &&
            Get(proximityOffer, "stockRoot") == Get(bombOffer, "stockRoot"), "weapon offers share only the weapon-shop stock group");
        Check(weaponOwner.StartNode.text.Contains("普通炸弹") && weaponOwner.StartNode.text.Contains("感应炸弹"), "authored weapon-shop greeting");
        Check(weaponOwner.StartNode.choices[1].effect == proximityOffer && weaponOwner.StartNode.choices[2].effect == bombOffer,
            "first purchase is proximity bomb; second is normal bomb");
        Check(proximityOffer.GetChoiceText(weaponOwner, null, "购买") == "购买感应炸弹X1（消耗5金币，剩余2）" &&
            bombOffer.GetChoiceText(weaponOwner, null, "购买") == "购买炸弹X1（消耗3金币，剩余7）", "weapon labels and prices");
        var armorOwner = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CharacterDialogue>(true))
            .Single(c => c.name == "盔甲铺店主");
        var armorOffer = armorOwner.GetComponent<DialogueShopPurchaseEffect>();
        Check(armorOffer != null && armorOffer.ReadStockCount() == 6, "armor shop resolves six real boxes");
        Check(((Transform)Get(armorOffer, "stockRoot")).name == "盔甲店" &&
            Get(armorOffer, "merchandise") is CardboardBoxPickupItem, "armor shop binds exact box inventory group");
        Check(armorOwner.StartNode.text.Contains("纸箱") &&
            armorOwner.FindNode("node_9ba762bd").text.Contains("咒语"), "authored armor-shop dialogue explains the disguise");
        Check(armorOwner.StartNode.choices[1].effect == armorOffer &&
            armorOwner.FindNode("node_9ba762bd").choices[1].effect == armorOffer, "both armor purchase branches share stock");
        Check(armorOffer.GetChoiceText(armorOwner, null, "购买") == "购买纸箱X1（消耗3金币，剩余6）", "armor shop three-coin label");
        PlayerSettings.productName = "dialogue-shop-smoke";
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(nameof(DialogueShopSmoke), true); EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return; frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(DialogueShopSmoke), false))
            { SessionState.SetBool(nameof(DialogueShopSmoke), false); deadline = EditorApplication.timeSinceStartup + 120; suite = Verify(); }
            if (suite == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("shop test timed out");
            if (suite.MoveNext()) return;
            suite = null; Debug.Log("DIALOGUE_SHOP_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static IEnumerator Verify()
    {
        while (RetroSceneLoadReveal.IsBlockingInput || GameSaveSystem.IsPreparingSceneSave) yield return null;
        const string coins = GoldCoinPickupItem.DefaultItemId, guns = DisposableHandgunPickupItem.DefaultItemId;
        var invGo = new GameObject("Inventory"); invGo.SetActive(false);
        var inv = invGo.AddComponent<PersistentInventory>(); Set(inv, "saveToPlayerPrefs", false);
        invGo.SetActive(true); inv.ClearAll(); inv.enabled = false;
        var npc = new GameObject("Shopkeeper"); npc.AddComponent<SwordZeldaCharacterData>(); npc.AddComponent<ZeldaFourWayMover>();
        var dialogue = npc.AddComponent<CharacterDialogue>();
        var actor = new GameObject("Buyer"); actor.transform.position = Vector3.right;
        actor.AddComponent<SwordZeldaCharacterData>(); var player = actor.AddComponent<ZeldaFourWayMover>();
        player.ReceiveControl(Vector2.left); ZeldaRuntimeRegistry.ClaimControlledMover(player);
        var root = new GameObject("Stock").transform;
        var gun = Template("DisposableHandgunPickupItem");
        for (int i = 0; i < 6; i++) Stock(root, gun, "Gun " + i);
        var shop = Shop(npc, root, gun);
        var otherRoot = new GameObject("Other Shop Stock").transform;
        Stock(otherRoot, gun, "Unrelated Gun");
        Stock(root, Template("RedHerbPowderPickupItem"), "Mixed Stock Herb");
        Check(shop.ReadStockCount() == 6, "ignores other groups and other item IDs");
        inv.SetSlot(0, coins, 2);
        Check(!shop.CanSelect(dialogue, player, out var reason) && reason.Contains("金币"), "insufficient gold disables option");
        Check(!shop.TryExecute(dialogue, player) && inv.GetQuantity(coins) == 2 && shop.ReadStockCount() == 6, "failed purchase changes nothing");
        FullBag(inv, 4);
        Check(!shop.CanSelect(dialogue, player, out reason) && reason.Contains("空间"), "full bag rejects purchase");
        Check(!shop.TryExecute(dialogue, player) && inv.GetQuantity(coins) == 4, "full bag never charges");
        inv.SetSlot(0, coins, 3);
        Check(shop.CanSelect(dialogue, player, out reason), "payment frees last coin slot");
        int changed = 0; bool reentered = false; int observedStock = -1;
        Action observer = () => { changed++; observedStock = shop.ReadStockCount(); reentered |= shop.TryExecute(dialogue, player); };
        inv.InventoryChanged += observer;
        Check(shop.TryExecute(dialogue, player), "purchase succeeds");
        inv.InventoryChanged -= observer;
        Check(inv.GetQuantity(coins) == 0 && inv.GetQuantity(guns) == 1 && shop.ReadStockCount() == 5, "exact 3 coins, 1 gun, 1 removed stock");
        Check(changed == 1 && !reentered && observedStock == 5, "notification sees complete transaction and cannot reenter");
        Check(inv.GetSlot(0).ItemId == guns, "gun occupies freed coin slot");
        FullBag(inv, 4); inv.SetSlot(1, guns, 2);
        Check(shop.TryExecute(dialogue, player) && inv.GetSlot(1).Quantity == 3 && inv.GetQuantity(coins) == 1, "full bag can complete gun stack");
        FullBag(inv, 4); inv.SetSlot(1, guns, 3);
        Check(!shop.TryExecute(dialogue, player) && inv.GetSlot(1).Quantity == 3, "gun stack cap remains three");
        FullBag(inv, 1); inv.SetSlot(4, coins, 2);
        Check(shop.TryExecute(dialogue, player) && inv.GetQuantity(coins) == 0, "payment spans coin stacks and frees space");
        inv.ClearAll(); inv.SetSlot(0, coins, 20);
        Check(shop.ReadStockCount() == 3, "stock count updates immediately without waiting a frame");
        Check(shop.TryExecute(dialogue, player) && shop.TryExecute(dialogue, player) && shop.TryExecute(dialogue, player), "remaining stock can all be bought");
        Check(!shop.TryExecute(dialogue, player) && shop.ReadStockCount() == 0 && inv.GetQuantity(coins) == 11, "sold-out stock cannot be sold twice");
        Check(otherRoot.GetComponentsInChildren<PickupItemBase>().Length == 1, "other shop stock untouched");
        yield return null;

        // Shared configurable component reused for another item and another price.
        var herb = Template("RedHerbPowderPickupItem"); var herbShop = Shop(npc, root, herb, 2);
        Check(herbShop.ReadStockCount() == 1 && herbShop.GetChoiceText(dialogue, player, "").Contains("消耗2金币"), "second offer has independent product/price");
        Check(herbShop.TryExecute(dialogue, player) && inv.GetQuantity(herb.ItemId) == 1 && inv.GetQuantity(coins) == 9, "same transaction buys herbs");
        var battery = Stock(root, Template("BatteryPickupItem"), "Used Battery") as BatteryPickupItem;
        battery.ApplyInventoryCharge(true, 2); string identity = battery.UniqueInstanceId;
        var batteryShop = Shop(npc, root, Template("BatteryPickupItem"), 4);
        Check(batteryShop.TryExecute(dialogue, player), "same transaction buys unique charged item");
        var slot = inv.Slots.Single(s => s.ItemId == "battery");
        Check(slot.ItemInstanceId == identity && slot.StoredCharge == 2 && slot.ItemState.Contains("5:2"), "battery identity/state/charge preserved");
        var savedBag = JsonUtility.ToJson(inv.CaptureRuntimeState()); inv.ClearAll();
        inv.ApplyRuntimeState(JsonUtility.FromJson<PersistentInventory.RuntimeState>(savedBag));
        Check(inv.GetQuantity(coins) == 5 && inv.GetQuantity(guns) == 3 && inv.GetQuantity("battery") == 1, "purchased items survive inventory JSON save/restore");
        VerifyToolShop(inv, npc, dialogue, player);
        yield return null;
        var weaponSuite = VerifyWeaponShop(inv, npc, dialogue, player);
        while (weaponSuite.MoveNext()) yield return weaponSuite.Current;
        VerifyArmorShop(inv, npc, dialogue, player);
        yield return null;

        // Streaming-owned inactive stock is available; deliberately hidden stock isn't.
        var item = Stock(root, gun, "Streamed Gun");
        var streamGo = new GameObject("Streaming Camera");
        var streamer = streamGo.AddComponent<CameraVisionObjectStreaming>(); streamer.enabled = false;
        var managedType = typeof(CameraVisionObjectStreaming).GetNestedType("ManagedObject", BindingFlags.NonPublic);
        var managed = Activator.CreateInstance(managedType, true);
        Set(managed, "gameObject", item.gameObject); Set(managed, "suspendedByManager", true);
        ((IDictionary)Get(streamer, "managedLookup"))[item.gameObject] = managed;
        item.gameObject.SetActive(false);
        Check(shop.ReadStockCount() == 1 && !item.gameObject.activeSelf, "streamed stock counted without awakening it");
        Set(managed, "suspendedByManager", false);
        Check(shop.ReadStockCount() == 0, "deliberately disabled stock not for sale");
        Set(managed, "suspendedByManager", true);
        root.gameObject.SetActive(false);
        Check(shop.ReadStockCount() == 0, "disabled ancestor still excludes a streamed child");
        root.gameObject.SetActive(true);
        inv.ClearAll(); inv.SetSlot(0, coins, 3);
        Check(shop.TryExecute(dialogue, player) && shop.ReadStockCount() == 0, "streamed item can be sold and is not counted again before Destroy");
        Object.Destroy(streamGo); yield return null;

        // Actual modal: dynamic text, gray disabled labels, keyboard/click rejection.
        item = Stock(root, gun, "UI Gun");
        Set(dialogue, "nodes", new[] { new CharacterDialogue.Node { id = "start", text = "商店测试", choices = new[] {
            new CharacterDialogue.Choice { text = "购买", action = CharacterDialogue.ChoiceAction.Continue, nextNodeId = "start", effect = shop },
            new CharacterDialogue.Choice { text = "再见", action = CharacterDialogue.ChoiceAction.Exit }
        } } });
        inv.ClearAll(); inv.SetSlot(0, coins, 2);
        dialogue.BeginDialogue(); Check(CharacterDialogueWindow.IsOpen, "shop dialogue opens");
        var window = Object.FindObjectOfType<CharacterDialogueWindow>();
        var labels = (Text[])Get(window, "labels"); var buttons = (Button[])Get(window, "buttons");
        Check(!buttons[0].interactable && labels[0].color != ZeldaUiPalette.Primary, "unavailable button and text are gray");
        Check(labels[0].text.Contains("购买一次性手铳X1（消耗3金币，剩余1）"), "actual UI uses dynamic purchase label");
        yield return null;
        Call(window, "Select", 0);
        Check(inv.GetQuantity(coins) == 2 && shop.ReadStockCount() == 1, "number-key path cannot buy disabled offer");
        inv.SetSlot(0, coins, 3); Call(window, "RefreshChoices");
        Check(buttons[0].interactable && labels[0].color == ZeldaUiPalette.Primary, "eligible offer returns to normal cyan");
        yield return null;
        Call(window, "Select", 0);
        Check(inv.GetQuantity(coins) == 0 && inv.GetQuantity(guns) == 1, "actual selection performs purchase");
        Check(!buttons[0].interactable && labels[0].text.Contains("剩余0"), "looping conversation refreshes stock and disables sold-out offer");
        Check(((string)Get(window, "history")).Contains("剩余1"), "transcript retains selected pre-purchase label");
        CharacterDialogueWindow.CloseFor(dialogue); yield return null;

        // Real world-state restore hides a purchased scene object on revisit/load.
        var manager = SceneTravelStateManager.GetOrCreate();
        item = Stock(root, gun, "Saved Stock");
        manager.CaptureDiskWorld();
        string path = item.GetComponent<SceneTravelStableId>().StableId;
        inv.ClearAll(); inv.SetSlot(0, coins, 3);
        Check(shop.TryExecute(dialogue, player), "purchase before scene snapshot");
        yield return null;
        string json = manager.CaptureDiskWorld();
        var snapshots = (IDictionary)Get(manager, "sceneStates");
        var scene = root.gameObject.scene;
        object snapshot = snapshots[scene.name];
        var replacement = Stock(root, gun, "Saved Stock");
        replacement.gameObject.AddComponent<SceneTravelStableId>().Initialize(path);
        Check(shop.ReadStockCount() == 1, "simulate fresh scene reloading sold stock");
        Check((bool)Call(manager, "RestoreSceneSnapshot", scene, snapshot, false), "actual cross-scene snapshot restored");
        Check(shop.ReadStockCount() == 0 && !replacement.gameObject.activeSelf, "sold item stays unavailable on revisit");
        replacement.gameObject.SetActive(true);
        object disk = SnapshotFromDisk(json, scene.name);
        Check((bool)Call(manager, "RestoreSceneSnapshot", scene, disk, false) && shop.ReadStockCount() == 0, "disk JSON snapshot also preserves sold stock");
        yield return null;
    }
    static void FullBag(PersistentInventory inv, int coins)
    {
        inv.ClearAll(); inv.SetSlot(0, GoldCoinPickupItem.DefaultItemId, coins);
        for (int i=1; i<5; i++) inv.SetSlot(i, "occupied_" + i, 1);
    }
    static IEnumerator VerifyWeaponShop(PersistentInventory inv, GameObject owner, CharacterDialogue dialogue, ZeldaFourWayMover player)
    {
        const string coins = GoldCoinPickupItem.DefaultItemId;
        var root = new GameObject("Weapon Shop Stock").transform;
        var bomb = Template("Bombs/BombPickupItem"); var proximity = Template("Bombs/ProximityBombPickupItem");
        for (int i=0; i<7; i++) Stock(root, bomb, "Bomb " + i);
        for (int i=0; i<2; i++) Stock(root, proximity, "Proximity " + i);
        var normalShop = Shop(owner, root, bomb, 3); var proximityShop = Shop(owner, root, proximity, 5);
        inv.ClearAll(); inv.SetSlot(0, coins, 3);
        Check(normalShop.CanSelect(dialogue, player, out _) && !proximityShop.CanSelect(dialogue, player, out _),
            "three coins enable only the cheaper bomb");
        Set(dialogue, "nodes", new[] { new CharacterDialogue.Node { id = "start", text = "武器店测试", choices = new[] {
            new CharacterDialogue.Choice { text = "购买", action = CharacterDialogue.ChoiceAction.Continue, nextNodeId = "start", effect = proximityShop },
            new CharacterDialogue.Choice { text = "购买", action = CharacterDialogue.ChoiceAction.Continue, nextNodeId = "start", effect = normalShop },
            new CharacterDialogue.Choice { text = "再见", action = CharacterDialogue.ChoiceAction.Exit }
        } } });
        dialogue.BeginDialogue(); Check(CharacterDialogueWindow.IsOpen, "two-product dialogue opens");
        var window = Object.FindObjectOfType<CharacterDialogueWindow>();
        var labels = (Text[])Get(window, "labels"); var buttons = (Button[])Get(window, "buttons");
        Check(!buttons[0].interactable && buttons[1].interactable && labels[0].color != ZeldaUiPalette.Primary &&
            labels[1].color == ZeldaUiPalette.Primary, "two purchase buttons have independent disabled colors");
        Check(labels[0].text.Contains("感应炸弹X1（消耗5金币，剩余2）") &&
            labels[1].text.Contains("炸弹X1（消耗3金币，剩余7）"), "two UI labels resolve correct products and counts");
        yield return null;
        Call(window, "Select", 0);
        Check(inv.GetQuantity(coins) == 3 && proximityShop.ReadStockCount() == 2, "disabled costly option cannot consume money");
        Call(window, "Select", 1);
        Check(inv.GetQuantity(coins) == 0 && inv.GetQuantity("bomb") == 1 && normalShop.ReadStockCount() == 6 &&
            proximityShop.ReadStockCount() == 2, "normal bomb costs three and changes only its stock");
        CharacterDialogueWindow.CloseFor(dialogue); yield return null;

        // Existing world pickups carry no visual tint; full-bag shop purchases must stack with them.
        FullBag(inv, 10); inv.SetSlot(1, "bomb", 4);
        Check(normalShop.CanSelect(dialogue, player, out _) && normalShop.TryExecute(dialogue, player), "normal bomb can fill a preexisting stack in a full bag");
        Check(inv.GetSlot(1).Quantity == 5 && inv.GetQuantity(coins) == 7 && normalShop.ReadStockCount() == 5,
            "normal bomb preserves five-per-slot limit");
        Check(!normalShop.CanSelect(dialogue, player, out _) && !normalShop.TryExecute(dialogue, player) && inv.GetQuantity(coins) == 7,
            "full bomb stack cannot consume money or overflow");
        FullBag(inv, 10); inv.SetSlot(1, "proximity_bomb", 4);
        Check(proximityShop.TryExecute(dialogue, player) && inv.GetSlot(1).Quantity == 5 && inv.GetQuantity(coins) == 5,
            "proximity bomb stacks with world pickups and costs five");
        Check(normalShop.ReadStockCount() == 5 && proximityShop.ReadStockCount() == 1, "proximity purchase does not consume ordinary bombs");
        inv.SetSlot(0, coins, 6); // Paying five leaves the coin slot occupied.
        Check(!proximityShop.TryExecute(dialogue, player) && inv.GetQuantity(coins) == 6,
            "full proximity stack rejects purchase if payment does not free a slot");
        FullBag(inv, 5);
        Check(proximityShop.TryExecute(dialogue, player) && inv.GetSlot(0).ItemId == "proximity_bomb" && inv.GetQuantity(coins) == 0,
            "five coins can free a slot for a proximity bomb");
        inv.ClearAll(); inv.SetSlot(0, coins, 20);
        Check(!proximityShop.CanSelect(dialogue, player, out var reason) && reason.Contains("售罄") &&
            normalShop.CanSelect(dialogue, player, out _), "sold-out proximity offer does not disable available normal bombs");
        for (int i=0; i<5; i++) Check(normalShop.TryExecute(dialogue, player), "buy remaining ordinary bomb " + i);
        Check(inv.GetQuantity("bomb") == 5 && inv.GetQuantity(coins) == 5 && normalShop.ReadStockCount() == 0,
            "five ordinary bombs use one stack and cost fifteen coins");
        Check(!normalShop.TryExecute(dialogue, player) && inv.GetQuantity(coins) == 5, "empty normal stock cannot charge again");
        string saved = JsonUtility.ToJson(inv.CaptureRuntimeState()); inv.ClearAll();
        inv.ApplyRuntimeState(JsonUtility.FromJson<PersistentInventory.RuntimeState>(saved));
        Check(inv.GetQuantity("bomb") == 5 && inv.GetQuantity(coins) == 5, "purchased bomb stack saves and restores");
        yield return null;
    }
    static void VerifyArmorShop(PersistentInventory inv, GameObject owner, CharacterDialogue dialogue, ZeldaFourWayMover player)
    {
        const string coins = GoldCoinPickupItem.DefaultItemId, id = "cardboard_box";
        var root = new GameObject("Armor Shop Stock").transform;
        var template = Template("CardboardBoxPickupItem");
        var first = (CardboardBoxPickupItem)Stock(root, template, "Box 0");
        for (int i=1; i<6; i++) Stock(root, template, "Box " + i);
        var shop = Shop(owner, root, template, 3);
        Check(shop.ReadStockCount() == 6 && template.MaxStackSize == 3, "armor stock and stack limit");
        // A player can store an item in a shop box before buying it. Keep the
        // normal pickup semantics: release its contents, don't silently destroy them.
        var battery = Template("BatteryPickupItem");
        inv.ClearAll();
        Check(inv.TryAddUniqueItem("battery", "armor-contained-battery", hasStoredCharge: true, storedCharge: 2), "prepare stored item");
        Check(first.TryStoreItem(inv.GetSlot(0), battery, player.GetComponent<ZeldaCharacterData>()), "shop box accepts stored item as usual");
        inv.ClearAll(); inv.SetSlot(0, coins, 2);
        Check(!shop.CanSelect(dialogue, player, out var reason) && reason.Contains("金币") && !shop.TryExecute(dialogue, player) &&
            inv.GetQuantity(coins) == 2 && shop.ReadStockCount() == 6 && first.HasStoredItem, "insufficient coins leave box and contents untouched");
        FullBag(inv, 4);
        Check(!shop.CanSelect(dialogue, player, out reason) && reason.Contains("空间") && !shop.TryExecute(dialogue, player) &&
            inv.GetQuantity(coins) == 4 && first.HasStoredItem, "full bag leaves payment and box untouched");
        inv.SetSlot(0, coins, 3);
        Check(shop.TryExecute(dialogue, player) && inv.GetSlot(0).ItemId == id && inv.GetQuantity(coins) == 0 && shop.ReadStockCount() == 5,
            "three coins free a slot for a box");
        var released = Object.FindObjectsOfType<BatteryPickupItem>().Single(b => b.ConfiguredUniqueInstanceId == "armor-contained-battery");
        Check(!first.HasStoredItem && released.RemainingCharge == 2 && released.gameObject.activeInHierarchy,
            "purchase releases stored battery with its identity and charge");
        FullBag(inv, 10); inv.SetSlot(1, id, 2);
        Check(shop.TryExecute(dialogue, player) && inv.GetSlot(1).Quantity == 3 && inv.GetQuantity(coins) == 7,
            "box purchase stacks with an existing world-pickup stack");
        Check(!shop.CanSelect(dialogue, player, out reason) && reason.Contains("空间") && !shop.TryExecute(dialogue, player) &&
            inv.GetQuantity(coins) == 7 && shop.ReadStockCount() == 4, "full box stack cannot overflow or charge");
        inv.ClearAll(); inv.SetSlot(0, coins, 20);
        for (int i=0; i<4; i++) Check(shop.TryExecute(dialogue, player), "buy remaining box " + i);
        var boxes = inv.Slots.Where(s => s.ItemId == id).ToArray();
        Check(inv.GetQuantity(id) == 4 && inv.GetQuantity(coins) == 8 && boxes.Length == 2 && boxes[0].Quantity == 3 && boxes[1].Quantity == 1,
            "four boxes form three-plus-one stacks for twelve coins");
        Check(shop.ReadStockCount() == 0 && !shop.CanSelect(dialogue, player, out reason) && reason.Contains("售罄") &&
            !shop.TryExecute(dialogue, player) && inv.GetQuantity(coins) == 8, "empty armor shop disables purchase without charging");
        Check(shop.GetChoiceText(dialogue, player, "购买") == "购买纸箱X1（消耗3金币，剩余0）", "armor remaining label updates");
        string saved = JsonUtility.ToJson(inv.CaptureRuntimeState()); inv.ClearAll();
        inv.ApplyRuntimeState(JsonUtility.FromJson<PersistentInventory.RuntimeState>(saved));
        Check(inv.GetQuantity(id) == 4 && inv.GetQuantity(coins) == 8, "purchased box stacks survive save/restore");
    }

    static void VerifyToolShop(PersistentInventory inv, GameObject owner, CharacterDialogue dialogue, ZeldaFourWayMover player)
    {
        const string coins = GoldCoinPickupItem.DefaultItemId, id = ClockworkPuppetPickupItem.DefaultItemId;
        var root = new GameObject("Tool Shop Stock").transform;
        var template = Template("ClockworkPuppetPickupItem");
        var first = (ClockworkPuppetPickupItem)Stock(root, template, "Puppet 1");
        first.ApplyInventoryCharge(true, 7.5f); string identity = first.UniqueInstanceId;
        Stock(root, template, "Puppet 2"); Stock(root, template, "Puppet 3");
        var shop = Shop(owner, root, template, 9);
        inv.ClearAll(); inv.SetSlot(0, coins, 8);
        Check(!shop.CanSelect(dialogue, player, out var reason) && reason.Contains("金币"), "puppet needs nine coins");
        Check(!shop.TryExecute(dialogue, player) && shop.ReadStockCount() == 3 && inv.GetQuantity(coins) == 8, "insufficient puppet purchase is atomic");
        FullBag(inv, 10);
        Check(!shop.CanSelect(dialogue, player, out reason) && reason.Contains("空间"), "puppet requires its own slot");
        inv.SetSlot(0, coins, 9);
        Check(shop.TryExecute(dialogue, player) && inv.GetQuantity(coins) == 0 && shop.ReadStockCount() == 2, "nine coins replaced with one scene puppet");
        var slot = inv.Slots.Single(s => s.ItemId == id);
        Check(slot.Quantity == 1 && slot.ItemInstanceId == identity && slot.HasStoredCharge && slot.StoredCharge == 7.5f,
            "puppet purchase retains unique identity and magic");
        string saved = JsonUtility.ToJson(inv.CaptureRuntimeState()); inv.ClearAll();
        inv.ApplyRuntimeState(JsonUtility.FromJson<PersistentInventory.RuntimeState>(saved));
        slot = inv.Slots.Single(s => s.ItemId == id);
        Check(slot.ItemInstanceId == identity && slot.StoredCharge == 7.5f, "puppet inventory save restores unique state");
        // Keep the existing puppet and a full bag: another puppet cannot stack into it.
        inv.SetSlot(1, coins, 10);
        Check(!shop.CanSelect(dialogue, player, out reason) && reason.Contains("空间"), "distinct puppets never stack");
        inv.ClearAll(); inv.SetSlot(0, coins, 20);
        Check(shop.TryExecute(dialogue, player) && shop.TryExecute(dialogue, player), "two more puppets cost eighteen coins");
        var puppets = inv.Slots.Where(s => s.ItemId == id).ToArray();
        Check(puppets.Length == 2 && puppets.All(s => s.Quantity == 1) && puppets[0].ItemInstanceId != puppets[1].ItemInstanceId &&
            inv.GetQuantity(coins) == 2, "purchased puppets have independent identities and slots");
        Check(shop.ReadStockCount() == 0 && !shop.TryExecute(dialogue, player) && inv.GetQuantity(coins) == 2,
            "tool shop sold out immediately; no duplicate purchase");
        Check(shop.GetChoiceText(dialogue, player, "购买") == "购买机关魔像X1（消耗9金币，剩余0）", "remaining tool stock label updates");
    }
    static object SnapshotFromDisk(string json, string name)
    {
        var type = typeof(SceneTravelStateManager);
        object world = JsonUtility.FromJson(json, type.GetNestedType("DiskWorld", BindingFlags.NonPublic));
        object saved = ((IList)Get(world, "scenes")).Cast<object>().Single(s => (string)Get(s, "name") == name);
        object snapshot = Activator.CreateInstance(type.GetNestedType("SceneState", BindingFlags.NonPublic), true);
        Set(snapshot, "sceneName", name); var objects = (IDictionary)Get(snapshot, "objects");
        foreach (var entry in (IList)Get(saved, "objects")) objects.Add(Get(entry, "path"), entry);
        return snapshot;
    }
}
