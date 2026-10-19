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

// Copy into the disposable Unity project's Assets/Editor; never runs in the working scene.
[InitializeOnLoad]
public static class HerbMerchantDialogueSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static IEnumerator suite;
    static int frame = -1, checks;
    static double deadline;
    static HerbMerchantDialogueSmoke() { EditorApplication.update += Tick; }
    static void Check(bool ok, string label) { checks++; if (!ok) throw new Exception(label); }
    static object Get(object o, string field) => o.GetType().GetField(field, F).GetValue(o);
    static void Set(object o, string field, object value) => o.GetType().GetField(field, F).SetValue(o, value);
    static void Call(object o, string method, params object[] args) => o.GetType().GetMethod(method, F).Invoke(o, args);
    static CharacterDialogueWindow Window => Object.FindObjectOfType<CharacterDialogueWindow>();
    static CharacterDialogue.Node Current => (CharacterDialogue.Node)Get(Window, "current");
    static Button[] Buttons => (Button[])Get(Window, "buttons");
    static Text[] Labels => (Text[])Get(Window, "labels");
    static void Select(int index) => Call(Window, "Select", index);
    static int OfferIndex(DialogueRecipeExchangeEffect offer) => Array.FindIndex(Current.choices, c => c.effect == offer);

    public static void Run()
    {
        Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "isolated project required");
        PlayerSettings.productName = "herb-merchant-dialogue-smoke";
        var source = EditorSceneManager.OpenScene("Assets/Scenes/Level2/Level2-Floor2.unity");
        var owner = source.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CharacterDialogue>(true))
            .Single(c => c.name == "草药商人");
        var offers = owner.GetComponents<DialogueRecipeExchangeEffect>();
        Check(offers.Length == 3 && owner.GetComponent<DialogueShopPurchaseEffect>() == null, "three unlimited offers, no world stock");
        Check(owner.DisplayFont != null && owner.SpeakerName == "草药商人", "speaker and game font configured");
        Check(Mathf.Abs((float)Get(owner, "interactionDistance") - 3) < .01f, "counter interaction distance");
        Check(offers.Select(o => ((PickupItemBase)Get(o, "merchandise")).ItemId).OrderBy(s => s)
            .SequenceEqual(new[] { "blue_herb_powder", "green_herb_powder", "red_herb_powder" }), "three distinct correct products");
        foreach (var offer in offers)
        {
            var costs = (InventoryItemCost[])Get(offer, "costs");
            Check(costs.Length == 1 && costs[0].item.ItemId == "gold_coin" && costs[0].quantity == 10, "ten coins per herb");
            Check(Get(offer, "requiredQuest") == null, "no quest gating");
        }
        foreach (var node in (CharacterDialogue.Node[])Get(owner, "nodes"))
        {
            Check(!string.IsNullOrEmpty(node.text) && node.choices.Length == 4, "four visible choices within UI limit");
            Check(node.choices.Count(c => c.action == CharacterDialogue.ChoiceAction.Exit && c.effect == null) == 1, "goodbye always available");
            foreach (var offer in offers)
            {
                var choice = node.choices.Single(c => c.effect == offer);
                Check(choice.action == CharacterDialogue.ChoiceAction.Continue && choice.nextNodeId == "shop" && owner.FindNode("shop") != null,
                    "purchases continue in repeat node");
                Check(choice.text.Contains(((PickupItemBase)Get(offer, "merchandise")).ItemName) &&
                    choice.text.Contains("10金币") && choice.text.Contains("无限供应"), "accurate purchase label");
            }
        }
        var fixture = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        var clone = Object.Instantiate(owner.gameObject); clone.name = owner.name;
        clone.transform.SetParent(null); SceneManager.MoveGameObjectToScene(clone, fixture);
        clone.transform.position = Vector3.zero;
        foreach (var ai in clone.GetComponents<ZeldaCharacterAiBase>()) ai.enabled = false;
        EditorSceneManager.CloseScene(source, true);
        SessionState.SetInt("HerbMerchantAuthoredChecks", checks);
        SessionState.SetBool(nameof(HerbMerchantDialogueSmoke), true); EditorApplication.EnterPlaymode();
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(HerbMerchantDialogueSmoke), false))
            {
                SessionState.SetBool(nameof(HerbMerchantDialogueSmoke), false);
                checks = SessionState.GetInt("HerbMerchantAuthoredChecks", 0);
                deadline = EditorApplication.timeSinceStartup + 120; suite = Verify();
            }
            if (suite == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("test timeout");
            if (suite.MoveNext()) return;
            suite = null; Debug.Log("HERB_MERCHANT_DIALOGUE_PASS checks=" + checks); EditorApplication.Exit(0);
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
        var owner = Object.FindObjectsOfType<CharacterDialogue>().Single(c => c.name == "草药商人");
        actor.transform.position = owner.transform.position + Vector3.right;
        yield return null;
        CharacterDialogueWindow.Open(owner, player); yield return null;
        Check(Window != null && Current.id == "start", "authored shop opens");
        var window = Window;
        foreach (var offer in owner.GetComponents<DialogueRecipeExchangeEffect>())
        {
            var item = (PickupItemBase)Get(offer, "merchandise");
            inv.ClearAll(); inv.SetSlot(0, "gold_coin", 9); Call(window, "RefreshChoices");
            int index = OfferIndex(offer);
            Check(!Buttons[index].interactable && Labels[index].color != ZeldaUiPalette.Primary, "nine coins: disabled and gray");
            Select(index);
            Check(Window == window && inv.GetQuantity("gold_coin") == 9 && inv.GetQuantity(item.ItemId) == 0, "disabled option cannot charge");
            inv.SetSlot(0, "gold_coin", 20);
            for (int i = 1; i < inv.SlotCount; i++) inv.SetSlot(i, "filler_" + i, 1);
            Call(window, "RefreshChoices");
            Check(!Buttons[index].interactable && Labels[index].text.Contains("空间"), "full inventory disables purchase");
            Select(index);
            Check(inv.GetQuantity("gold_coin") == 20 && inv.GetQuantity(item.ItemId) == 0, "full bag does not charge");
            inv.SetSlot(0, "gold_coin", 10); Call(window, "RefreshChoices");
            Check(Buttons[index].interactable, "exact payment frees slot in full bag");
            yield return null; Select(index);
            Check(inv.GetQuantity("gold_coin") == 0 && inv.GetQuantity(item.ItemId) == 1, "exact price, one product");
            Check(Window == window && Current.id == "shop" && Time.timeScale == 0, "purchase retains paused dialogue");
            inv.ClearAll();
            // Twelve purchases per colour, replenishing only test payment: never consume scene stock.
            for (int purchased = 1; purchased <= 12; purchased++)
            {
                inv.SetSlot(0, "gold_coin", 20); Call(window, "RefreshChoices"); yield return null;
                index = OfferIndex(offer); Check(Buttons[index].interactable, "unlimited offer remains available " + purchased);
                Select(index);
                Check(Window == window && inv.GetQuantity(item.ItemId) == purchased && inv.GetQuantity("gold_coin") == 10,
                    "repeat purchase exact count/payment " + purchased);
                Check(Enumerable.Range(0, inv.SlotCount).All(i => inv.GetSlot(i).ItemId != item.ItemId || inv.GetSlot(i).Quantity <= 3),
                    "herb stack cap remains three");
            }
            inv.ClearAll(); inv.SetSlot(0, "gold_coin", 20); Call(window, "RefreshChoices");
            inv.SetSlot(0, "gold_coin", 9); yield return null; Select(OfferIndex(offer));
            Check(inv.GetQuantity("gold_coin") == 9 && inv.GetQuantity(item.ItemId) == 0, "stale enabled option revalidates payment");
        }
        yield return null; Select(0); yield return null;
        Check(!CharacterDialogueWindow.IsOpen && Time.timeScale == 1, "goodbye closes normally");
        owner.gameObject.SetActive(false); yield return null; owner.gameObject.SetActive(true); yield return null;
        inv.ClearAll(); inv.SetSlot(0, "gold_coin", 20);
        CharacterDialogueWindow.Open(owner, player); yield return null;
        Check(Current.id == "start" && Buttons.Skip(1).All(b => b.interactable), "re-enable/reopen retains all unlimited offers");
        CharacterDialogueWindow.CloseFor(owner); yield return null;
    }
}
