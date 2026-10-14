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

// Run only in the disposable Unity project; uses the real authored dialogue nodes/effects.
[InitializeOnLoad]
public static class Floor1ShopDialogueSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly string[] Names = { "弓箭铺店主", "工具铺店主", "武器铺店主", "盔甲铺店主" };
    static IEnumerator suite;
    static int frame = -1, checks;
    static double deadline;
    static Floor1ShopDialogueSmoke() { EditorApplication.update += Tick; }
    static void Check(bool ok, string label) { checks++; if (!ok) throw new Exception(label); }
    static object Get(object o, string field) => o.GetType().GetField(field, F).GetValue(o);
    static void Set(object o, string field, object value) => o.GetType().GetField(field, F).SetValue(o, value);
    static void Call(object o, string method, params object[] args) => o.GetType().GetMethod(method, F).Invoke(o, args);
    static CharacterDialogueWindow Window => Object.FindObjectOfType<CharacterDialogueWindow>();
    static CharacterDialogue.Node Current => (CharacterDialogue.Node)Get(Window, "current");
    static Button[] Buttons => (Button[])Get(Window, "buttons");
    static Text[] Labels => (Text[])Get(Window, "labels");
    static void Select(int index) => Call(Window, "Select", index);
    static int OfferIndex(DialogueShopPurchaseEffect offer) => Array.FindIndex(Current.choices, c => c.effect == offer);

    public static void Run()
    {
        Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "isolated project required");
        PlayerSettings.productName = "floor1-shop-dialogue-smoke";
        var source = EditorSceneManager.OpenScene("Assets/Scenes/Level2/Level2-Floor1.unity");
        var owners = source.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CharacterDialogue>(true))
            .Where(c => Names.Contains(c.name)).ToArray();
        Check(owners.Length == 4, "exactly four existing shopkeepers");
        foreach (var owner in owners)
        {
            var offers = owner.GetComponents<DialogueShopPurchaseEffect>();
            var nodes = (CharacterDialogue.Node[])Get(owner, "nodes");
            Check(owner.DisplayFont != null && owner.FindNode("shop") != null, owner.name + " font and shop configured");
            Check(owner.GetComponent<DialogueRecipeExchangeEffect>() == null, owner.name + " retains finite-stock purchases");
            foreach (var node in nodes)
            {
                Check(!string.IsNullOrEmpty(node.text) && node.choices.Length <= 4, owner.name + " node text and choice limit");
                Check(node.choices.Any(c => c.action == CharacterDialogue.ChoiceAction.Exit && c.effect == null), "every node can say goodbye");
                foreach (var choice in node.choices)
                {
                    if (choice.action == CharacterDialogue.ChoiceAction.Continue)
                        Check(owner.FindNode(choice.nextNodeId) != null, "all links resolve");
                    if (choice.effect == null) continue;
                    Check(offers.Contains(choice.effect) && choice.action == CharacterDialogue.ChoiceAction.Continue &&
                        choice.nextNodeId == "shop", "all purchase branches stay open, preserving their configured offer");
                }
            }
            Check(offers.All(e => owner.FindNode("shop").choices.Count(c => c.effect == e) == 1), "repeat node offers every product exactly once");
        }
        var fixture = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        for (int i = 0; i < owners.Length; i++)
        {
            var clone = Object.Instantiate(owners[i].gameObject); clone.name = owners[i].name;
            clone.transform.SetParent(null); SceneManager.MoveGameObjectToScene(clone, fixture);
            clone.transform.position = new Vector3(i * 10, 0, 0);
            foreach (var ai in clone.GetComponents<ZeldaCharacterAiBase>()) ai.enabled = false;
            foreach (var offer in clone.GetComponents<DialogueShopPurchaseEffect>())
            {
                var root = new GameObject(clone.name + " Test Stock").transform;
                SceneManager.MoveGameObjectToScene(root.gameObject, fixture); root.position = new Vector3(100, 100, 0);
                Set(offer, "stockRoot", root);
                var item = (PickupItemBase)Get(offer, "merchandise");
                for (int j = 0; j < 2; j++) Object.Instantiate(item, root).name = item.name + j;
            }
        }
        EditorSceneManager.CloseScene(source, true);
        SessionState.SetBool(nameof(Floor1ShopDialogueSmoke), true); EditorApplication.EnterPlaymode();
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(Floor1ShopDialogueSmoke), false))
            { SessionState.SetBool(nameof(Floor1ShopDialogueSmoke), false); deadline = EditorApplication.timeSinceStartup + 120; suite = Verify(); }
            if (suite == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("test timeout");
            if (suite.MoveNext()) return;
            suite = null; Debug.Log("FLOOR1_SHOP_DIALOGUE_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }

    static IEnumerator Verify()
    {
        while (RetroSceneLoadReveal.IsBlockingInput || GameSaveSystem.IsPreparingSceneSave) yield return null;
        var bag = new GameObject("Test Inventory"); bag.SetActive(false);
        var inv = bag.AddComponent<PersistentInventory>(); Set(inv, "saveToPlayerPrefs", false);
        bag.SetActive(true); inv.ClearAll(); inv.enabled = false;
        var actor = new GameObject("Buyer"); actor.AddComponent<SwordZeldaCharacterData>();
        var player = actor.AddComponent<ZeldaFourWayMover>(); player.ReceiveControl(Vector2.left);
        ZeldaRuntimeRegistry.ClaimControlledMover(player);
        foreach (var name in Names)
        {
            var owner = Object.FindObjectsOfType<CharacterDialogue>().Single(c => c.name == name);
            actor.transform.position = owner.transform.position + Vector3.right;
            inv.ClearAll(); yield return null;
            CharacterDialogueWindow.Open(owner, player); yield return null;
            Check(Window != null && Current.id == "start", name + " actual modal opens");
            var window = Window;
            var first = owner.GetComponents<DialogueShopPurchaseEffect>()[0];
            int index = OfferIndex(first);
            Check(!Buttons[index].interactable && Labels[index].color != ZeldaUiPalette.Primary, name + " insufficient coins gray");
            Select(index);
            Check(Window == window && first.ReadStockCount() == 2 && inv.GetQuantity("gold_coin") == 0, "disabled purchase neither closes nor charges");
            inv.SetSlot(0, "gold_coin", 20);
            for (int i = 1; i < inv.SlotCount; i++) inv.SetSlot(i, "filler_" + i, 1);
            Call(window, "RefreshChoices");
            Check(!Buttons[index].interactable && Labels[index].text.Contains("空间"), name + " full inventory gray");
            Select(index); Check(first.ReadStockCount() == 2 && inv.GetQuantity("gold_coin") == 20, "full bag cannot consume payment or stock");
            inv.ClearAll(); inv.SetSlot(0, "gold_coin", 20); Call(window, "RefreshChoices"); yield return null;
            // Read each shop's new item explanation, then buy from that branch.
            int question = Array.FindIndex(Current.choices, c => c.effect == null && c.action == CharacterDialogue.ChoiceAction.Continue);
            Check(question >= 0, name + " has its own explanatory dialogue");
            Select(question); yield return null;
            Check(Current.id != "start" && Current.id != "shop", "question opens explanation without exiting");
            foreach (var offer in owner.GetComponents<DialogueShopPurchaseEffect>())
            {
                inv.ClearAll(); inv.SetSlot(0, "gold_coin", 20); Call(window, "RefreshChoices");
                var item = (PickupItemBase)Get(offer, "merchandise"); int price = (int)Get(offer, "coinCost");
                for (int purchased = 1; purchased <= 2; purchased++)
                {
                    yield return null;
                    index = OfferIndex(offer); Check(index >= 0 && Buttons[index].interactable, "offer enabled before repeat purchase");
                    Select(index);
                    Check(Window == window && Current.id == "shop" && Time.timeScale == 0, name + " same paused window survives purchase " + purchased);
                    Check(inv.GetQuantity(item.ItemId) == purchased && inv.GetQuantity("gold_coin") == 20 - price * purchased &&
                        offer.ReadStockCount() == 2 - purchased, "one exact item, one exact payment, one removed world item");
                    index = OfferIndex(offer);
                    Check(Labels[index].text.Contains("剩余" + (2 - purchased)), "remaining stock immediately refreshed");
                }
                Check(!Buttons[index].interactable && Labels[index].text.Contains("售罄"), "sold-out offer gray in the still-open window");
                yield return null; Select(index);
                Check(Window == window && inv.GetQuantity("gold_coin") == 20 - price * 2 && inv.GetQuantity(item.ItemId) == 2,
                    "sold-out selection cannot duplicate or charge");
            }
            yield return null;
            Select(Array.FindIndex(Current.choices, c => c.action == CharacterDialogue.ChoiceAction.Exit)); yield return null;
            Check(!CharacterDialogueWindow.IsOpen && Time.timeScale == 1, name + " goodbye closes and unpauses");
            CharacterDialogueWindow.Open(owner, player); yield return null;
            Check(Current.id == "start" && !Buttons[OfferIndex(first)].interactable, "reopening retains sold-out stock and greeting");
            CharacterDialogueWindow.CloseFor(owner); yield return null;
        }
    }
}
