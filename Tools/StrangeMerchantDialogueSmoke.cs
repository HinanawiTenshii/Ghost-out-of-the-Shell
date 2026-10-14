using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class StrangeMerchantDialogueSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const string Quest = "level2_strange_merchant_synthesis_tools";
    static readonly Stack<IEnumerator> steps = new Stack<IEnumerator>();
    static int checks, frame = -1;
    static double deadline;
    static StrangeMerchantDialogueSmoke() { EditorApplication.update += Tick; }
    static void Check(bool ok, string label) { checks++; if (!ok) throw new Exception(label); }
    static object Get(object target, string field) => target.GetType().GetField(field, F).GetValue(target);
    static void Set(object target, string field, object value) => target.GetType().GetField(field, F).SetValue(target, value);
    static CharacterDialogueWindow Window => Object.FindObjectOfType<CharacterDialogueWindow>();
    static Button[] Buttons => (Button[])Get(Window, "buttons");
    static string Node => ((CharacterDialogue.Node)Get(Window, "current")).id;
    static void Select(int index) => typeof(CharacterDialogueWindow).GetMethod("Select", F).Invoke(Window, new object[] { index });
    static PickupItemBase Item(string name) => Resources.Load<GameObject>("PickupItems/" + name).GetComponent<PickupItemBase>();
    public static void Run()
    {
        Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "isolated project required");
        PlayerSettings.productName = "strange-merchant-dialogue-smoke";
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Level2/Level2-Floor1.unity");
        var owner = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CharacterDialogue>(true))
            .Single(c => c.name == "诡异商人");
        Check(owner.GetComponent<StrangeMerchantZeldaCharacterData>() != null, "dialogue added to exact authored strange merchant");
        Check(owner.SpeakerName == "诡异商人" && owner.FindNode("before_hunt") != null, "initial speaker and gated node");
        Check(owner.FindNode("start").choices.Length == 3 && owner.FindNode("waiting").choices.Length == 2, "authored first and second phase choices");
        Check(owner.FindNode("shop").choices.Length == 3 && owner.FindNode("surprised").choices.Length == 3, "both final entries contain both repeatable purchases");
        Check(owner.FindNode("start").text.Contains("沙虫") && owner.FindNode("start").text.Contains("要塞") && owner.FindNode("start").text.Contains("合成工具"), "intro explains sandworm bait and tools location");
        Check(owner.GetComponent<DialogueQuestProgression>().QuestId == Quest, "stable quest ID wired");
        Check(owner.GetComponents<DialogueRecipeExchangeEffect>().Length == 2 && owner.GetComponents<DialogueQuestEffect>().Length == 3, "independent, configured reusable effects");
        Check(owner.DisplayFont != null, "project pixel font assigned");
        // Copy only the exact authored NPC into a small scene; never run or save the large level.
        var fixture = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        var clone = Object.Instantiate(owner.gameObject); clone.name = "诡异商人";
        clone.transform.SetParent(null); SceneManager.MoveGameObjectToScene(clone, fixture); clone.transform.position = Vector3.zero;
        EditorSceneManager.CloseScene(scene, true);
        SessionState.SetBool(nameof(StrangeMerchantDialogueSmoke), true); EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(StrangeMerchantDialogueSmoke), false))
            {
                SessionState.SetBool(nameof(StrangeMerchantDialogueSmoke), false);
                deadline = EditorApplication.timeSinceStartup + 120; steps.Push(Verify());
            }
            if (steps.Count == 0) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("test timeout");
            while (steps.Count > 0)
            {
                var step = steps.Peek();
                if (!step.MoveNext()) { steps.Pop(); continue; }
                if (step.Current is IEnumerator nested) { steps.Push(nested); continue; }
                return;
            }
            Debug.Log("STRANGE_MERCHANT_DIALOGUE_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { steps.Clear(); Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static IEnumerator Open(CharacterDialogue owner, ZeldaFourWayMover player)
    {
        yield return null;
        CharacterDialogueWindow.Open(owner, player);
        Check(Window != null, "actual modal opens"); yield return null;
    }
    static IEnumerator Close(CharacterDialogue owner)
    { CharacterDialogueWindow.CloseFor(owner); yield return null; }
    static void FullBag(PersistentInventory inventory, int coins, int bombs)
    {
        inventory.ClearAll(); inventory.SetSlot(0, "gold_coin", coins); inventory.SetSlot(1, "bomb", bombs);
        for (int i = 2; i < inventory.SlotCount; i++) inventory.SetSlot(i, "filler_" + i, 1);
    }
    static IEnumerator Verify()
    {
        while (RetroSceneLoadReveal.IsBlockingInput || GameSaveSystem.IsPreparingSceneSave) yield return null;
        var journal = QuestJournalManager.GetOrCreate(); journal.ApplySaveState(new QuestJournalManager.SaveState());
        journal.AddOrUpdateEntry(QuestJournalManager.Level2SandwormQuestId, QuestJournalManager.Level2SandwormQuestTitle, "hunt");
        var invObject = new GameObject("Test Inventory"); invObject.SetActive(false);
        var inventory = invObject.AddComponent<PersistentInventory>(); Set(inventory, "saveToPlayerPrefs", false);
        invObject.SetActive(true); inventory.ClearAll(); inventory.enabled = false;
        var owner = Object.FindObjectsOfType<CharacterDialogue>().Single(c => c.name == "诡异商人");
        owner.GetComponent<ZeldaCharacterAiBase>().enabled = false;
        var playerObject = new GameObject("Test Buyer"); playerObject.transform.position = Vector3.right;
        playerObject.AddComponent<SwordZeldaCharacterData>(); var player = playerObject.AddComponent<ZeldaFourWayMover>();
        player.ReceiveControl(Vector2.left); ZeldaRuntimeRegistry.ClaimControlledMover(player);
        var start = owner.FindNode("start"); var waiting = owner.FindNode("waiting"); var shop = owner.FindNode("shop");
        var accept = start.choices[1].effect; var direct = start.choices[2].effect; var deliver = waiting.choices[1].effect;
        var gold = shop.choices[1].effect; var barter = shop.choices[2].effect;
        yield return Open(owner, player);
        Check(Node == "start" && Buttons[0].gameObject.activeSelf && Buttons[1].gameObject.activeSelf && !Buttons[2].gameObject.activeSelf, "hidden hand-in absent without tools");
        Select(2); Check(!journal.ContainsEntry(Quest), "hidden option cannot be invoked by its hotkey");
        Select(0); yield return null; Check(owner.StartNode.id == "start", "saying goodbye does not accept quest");
        yield return Open(owner, player); Select(1); yield return null;
        Check(Node == "accepted" && journal.ContainsEntry(Quest) && !journal.IsEntryCompleted(Quest), "accept records quest and shows acknowledgement");
        Check(!accept.TryExecute(owner, player) && !gold.TryExecute(owner, player), "no double accept or purchase before delivery");
        yield return Close(owner);
        var acceptedSave = JsonUtility.FromJson<QuestJournalManager.SaveState>(JsonUtility.ToJson(journal.CaptureSaveState()));
        journal.ApplySaveState(new QuestJournalManager.SaveState()); journal.ApplySaveState(acceptedSave);
        yield return Open(owner, player);
        Check(Node == "waiting" && Buttons[1].gameObject.activeSelf && !Buttons[1].interactable, "accepted stage survives save and shows disabled hand-in");
        Select(1); Check(Node == "waiting" && !journal.IsEntryCompleted(Quest), "disabled hand-in does nothing");
        yield return Close(owner);
        inventory.SetSlot(0, "synthesis_tools", 1); inventory.SetSlot(1, "synthesis_tools", 1);
        yield return Open(owner, player);
        Check(Buttons[1].interactable, "hand-in enabled on demand once tools are held");
        Select(1); yield return null;
        Check(Node == "shop" && journal.IsEntryCompleted(Quest) && inventory.GetQuantity("synthesis_tools") == 1, "normal delivery consumes exactly one tool and enters final stage");
        Check(!deliver.TryExecute(owner, player) && inventory.GetQuantity("synthesis_tools") == 1, "completed quest never consumes another tool");
        yield return Close(owner);
        var completedSave = JsonUtility.FromJson<QuestJournalManager.SaveState>(JsonUtility.ToJson(journal.CaptureSaveState()));
        journal.ApplySaveState(new QuestJournalManager.SaveState()); journal.ApplySaveState(completedSave);
        owner.gameObject.SetActive(false); owner.gameObject.SetActive(true); owner.GetComponent<ZeldaCharacterAiBase>().enabled = false;
        Check(owner.StartNode.id == "shop", "completed stage survives save and distance-unload/reactivation");

        // Alternate route: tool obtained before the quest was accepted.
        journal.ApplySaveState(new QuestJournalManager.SaveState()); inventory.ClearAll(); inventory.SetSlot(0, "synthesis_tools", 1);
        journal.AddOrUpdateEntry(QuestJournalManager.Level2SandwormQuestId, QuestJournalManager.Level2SandwormQuestTitle, "hunt");
        yield return Open(owner, player);
        Check(Node == "start" && Buttons[2].gameObject.activeSelf && Buttons[2].interactable, "pre-owned tool reveals third choice");
        Select(2); yield return null;
        Check(Node == "surprised" && journal.IsEntryCompleted(Quest) && inventory.GetQuantity("synthesis_tools") == 0, "direct delivery shows surprise and completes without accepting first");
        Check(owner.FindNode(Node).text.Contains("竟然") && Buttons[2].gameObject.activeSelf, "surprise immediately exposes final purchase options");
        yield return Close(owner);
        yield return Open(owner, player); Check(Node == "shop", "next conversation goes straight to shop"); yield return Close(owner);

        inventory.SetSlot(0, "gold_coin", 5);
        Check(!gold.CanSelect(owner, player, out var reason) && reason.Contains("金币"), "gold price is six, not five");
        Check(!gold.TryExecute(owner, player) && inventory.GetQuantity("gold_coin") == 5, "insufficient money not consumed");
        inventory.SetSlot(0, "gold_coin", 6);
        yield return Open(owner, player);
        Check(Buttons[1].interactable && !Buttons[2].interactable, "real modal enables gold purchase, disables missing-material barter");
        Select(1); yield return null;
        Check(Node == "shop" && inventory.GetQuantity("gold_coin") == 0 && inventory.GetQuantity("decoy_bomb") == 1, "six coins gives exactly one bait bomb, stays open");
        Check(!Buttons[1].interactable && !Buttons[2].interactable, "buttons refresh after transaction");
        yield return Close(owner);

        inventory.ClearAll(); inventory.SetSlot(0, "gold_coin", 1); inventory.SetSlot(1, "bomb", 1);
        int notifications = 0; bool reentered = false, completeState = false;
        Action observer = () => { notifications++; completeState = inventory.GetQuantity("bomb") == 0 && inventory.GetQuantity("gold_coin") == 0 && inventory.GetQuantity("decoy_bomb") == 1; reentered |= barter.TryExecute(owner, player); };
        inventory.InventoryChanged += observer;
        Check(barter.TryExecute(owner, player), "one ordinary bomb plus one gold succeeds");
        inventory.InventoryChanged -= observer;
        Check(notifications == 1 && completeState && !reentered, "all materials and output settle atomically before one notification");
        inventory.ClearAll(); inventory.SetSlot(0, "gold_coin", 1); inventory.SetSlot(1, "proximity_bomb", 1); inventory.SetSlot(2, "purple_bomb", 1);
        Check(!barter.TryExecute(owner, player) && inventory.GetQuantity("gold_coin") == 1, "other bomb types cannot substitute ordinary bombs");
        FullBag(inventory, 20, 2);
        Check(!gold.TryExecute(owner, player) && !barter.TryExecute(owner, player), "full bag rejects both purchases when no slot will be freed");
        Check(inventory.GetQuantity("gold_coin") == 20 && inventory.GetQuantity("bomb") == 2, "full-bag failure preserves all payment");
        FullBag(inventory, 6, 2);
        Check(gold.TryExecute(owner, player) && inventory.GetSlot(0).ItemId == "decoy_bomb", "gold purchase uses slot freed by exact payment");
        FullBag(inventory, 20, 1);
        Check(barter.TryExecute(owner, player) && inventory.GetSlot(1).ItemId == "decoy_bomb" && inventory.GetQuantity("gold_coin") == 19, "barter uses slot freed by bomb");
        FullBag(inventory, 1, 2);
        Check(barter.TryExecute(owner, player) && inventory.GetSlot(0).ItemId == "decoy_bomb" && inventory.GetQuantity("bomb") == 1, "barter uses slot freed by gold");
        FullBag(inventory, 20, 2); inventory.SetSlot(2, "decoy_bomb", 4);
        Check(gold.TryExecute(owner, player) && inventory.GetSlot(2).Quantity == 5, "existing bait-bomb stack filled to prefab cap");
        Check(!gold.TryExecute(owner, player), "full stack and full bag still disallow overflow");
        inventory.ClearAll(); inventory.SetSlot(0, "gold_coin", 20); inventory.SetSlot(1, "gold_coin", 20); inventory.SetSlot(2, "gold_coin", 20);
        for (int i = 0; i < 9; i++) Check(gold.TryExecute(owner, player), "unlimited repeatable purchase " + i);
        Check(inventory.GetQuantity("gold_coin") == 6 && inventory.GetQuantity("decoy_bomb") == 9, "no hidden world-stock limit; exact repeated payment");
        var inventorySave = JsonUtility.FromJson<PersistentInventory.RuntimeState>(JsonUtility.ToJson(inventory.CaptureRuntimeState()));
        inventory.ClearAll(); inventory.ApplyRuntimeState(inventorySave);
        Check(inventory.GetQuantity("decoy_bomb") == 9 && owner.StartNode.id == "shop", "purchased bombs and unlocked shop survive inventory restore");

        // Reusable transaction rejects duplicate-cost underpayment and preserves unique remaining slots.
        inventory.ClearAll(); inventory.SetSlot(0, "gold_coin", 5);
        var coin = Item("GoldCoinPickupItem"); var bait = Item("Bombs/DecoyBombPickupItem");
        var duplicateCosts = new[] { new InventoryItemCost { item = coin, quantity = 3 }, new InventoryItemCost { item = coin, quantity = 3 } };
        Check(!inventory.TryExchangeItems(duplicateCosts, bait) && inventory.GetQuantity("gold_coin") == 5, "duplicate ingredient rows are aggregated");
        inventory.SetSlot(0, "gold_coin", 6); Check(inventory.TryExchangeItems(duplicateCosts, bait), "aggregated ingredients consume correct total");
        inventory.ClearAll();
        var before = journal.CaptureSaveState(); Check(before.completed.Contains(Quest), "quest completion uses existing save journal");

        // Hidden choices in the middle must not leave numbering gaps or select the wrong authored choice.
        yield return Open(owner, player);
        var hiddenMiddle = new CharacterDialogue.Node { id = "hidden_test", text = "Hidden choice test", choices = new[] {
            new CharacterDialogue.Choice { text = "visible first", action = CharacterDialogue.ChoiceAction.Exit },
            new CharacterDialogue.Choice { text = "hidden middle", effect = direct, action = CharacterDialogue.ChoiceAction.Exit },
            new CharacterDialogue.Choice { text = "visible last", action = CharacterDialogue.ChoiceAction.Exit } } };
        typeof(CharacterDialogueWindow).GetMethod("Enter", F).Invoke(Window, new object[] { hiddenMiddle }); yield return null;
        var labels = (Text[])Get(Window, "labels");
        Check(Buttons[1].gameObject.activeSelf && !Buttons[2].gameObject.activeSelf && labels[1].text == "2. visible last", "hidden middle choice compacts visible buttons and labels");
        Select(1); yield return null; Check(!CharacterDialogueWindow.IsOpen, "second visible hotkey selects original third choice");

        // Inventory may change between presentation and selection; hidden selection must revalidate.
        journal.ApplySaveState(new QuestJournalManager.SaveState()); inventory.SetSlot(0, "synthesis_tools", 1);
        journal.AddOrUpdateEntry(QuestJournalManager.Level2SandwormQuestId, QuestJournalManager.Level2SandwormQuestTitle, "hunt");
        yield return Open(owner, player); Check(Buttons[2].gameObject.activeSelf, "hidden option visible before stale-state test");
        inventory.ClearAll(); Select(2);
        Check(!journal.IsEntryCompleted(Quest) && Node == "start" && !Buttons[2].gameObject.activeSelf, "stale hidden option cannot turn in nonexistent tools");
        yield return Close(owner);
        Object.Destroy(playerObject); Object.Destroy(owner.gameObject); Object.Destroy(invObject); yield return null;
    }
}
