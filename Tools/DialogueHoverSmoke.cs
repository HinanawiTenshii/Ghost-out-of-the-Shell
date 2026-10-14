using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Exercise native uGUI pointer/selection states in the actual paused dialogue window.
[InitializeOnLoad]
public static class DialogueHoverSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly Stack<IEnumerator> steps = new Stack<IEnumerator>();
    static int frame = -1, checks;
    static double deadline;
    static DialogueHoverSmoke() { EditorApplication.update += Tick; }
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    static void Set(object o, string field, object value) => o.GetType().GetField(field, F).SetValue(o, value);
    static object Get(object o, string field) => o.GetType().GetField(field, F).GetValue(o);
    static void Call(object o, string method, params object[] args) => o.GetType().GetMethod(method, F).Invoke(o, args);
    static IEnumerator Fade() { double until = EditorApplication.timeSinceStartup + .2; while (EditorApplication.timeSinceStartup < until) yield return null; }
    static void Tint(Button b, Color expected, string message)
    {
        Color actual = b.targetGraphic.canvasRenderer.GetColor();
        Check(Mathf.Abs(actual.r - expected.r) + Mathf.Abs(actual.g - expected.g) + Mathf.Abs(actual.b - expected.b) < .005f, message);
    }
    static void Click(Button button, PointerEventData pointer)
    {
        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
    }
    public static void Run()
    {
        Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "isolated project required");
        PlayerSettings.productName = "dialogue-hover-smoke";
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(nameof(DialogueHoverSmoke), true); EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return; frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(DialogueHoverSmoke), false))
            { SessionState.SetBool(nameof(DialogueHoverSmoke), false); deadline = EditorApplication.timeSinceStartup + 90; steps.Push(Verify()); }
            if (steps.Count == 0) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("test timeout");
            while (steps.Count > 0)
            {
                var step = steps.Peek();
                if (!step.MoveNext()) { steps.Pop(); continue; }
                if (step.Current is IEnumerator nested) { steps.Push(nested); continue; }
                return;
            }
            Debug.Log("DIALOGUE_HOVER_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { steps.Clear(); Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static IEnumerator Verify()
    {
        while (RetroSceneLoadReveal.IsBlockingInput || GameSaveSystem.IsPreparingSceneSave) yield return null;
        var bag = new GameObject("Test Inventory"); bag.SetActive(false);
        var inv = bag.AddComponent<PersistentInventory>(); Set(inv, "saveToPlayerPrefs", false);
        bag.SetActive(true); inv.ClearAll(); inv.enabled = false; inv.SetSlot(0, "gold_coin", 6);
        var npc = new GameObject("Shopkeeper"); npc.AddComponent<SwordZeldaCharacterData>(); npc.AddComponent<ZeldaFourWayMover>();
        var owner = npc.AddComponent<CharacterDialogue>();
        var actor = new GameObject("Buyer"); actor.transform.position = Vector3.right;
        actor.AddComponent<SwordZeldaCharacterData>(); var player = actor.AddComponent<ZeldaFourWayMover>();
        player.ReceiveControl(Vector2.left); ZeldaRuntimeRegistry.ClaimControlledMover(player);
        var template = Resources.Load<GameObject>("PickupItems/DisposableHandgunPickupItem").GetComponent<PickupItemBase>();
        var stock = new GameObject("Test Stock").transform; stock.position = new Vector3(100, 100, 0);
        for (int i = 0; i < 3; i++) Object.Instantiate(template, stock);
        var offer = npc.AddComponent<DialogueShopPurchaseEffect>();
        Set(offer, "merchandise", template); Set(offer, "stockRoot", stock); Set(offer, "coinCost", 3);
        Set(owner, "nodes", new[] {
            new CharacterDialogue.Node { id = "start", text = "商店测试", choices = new[] {
                new CharacterDialogue.Choice { text = "购买", effect = offer, action = CharacterDialogue.ChoiceAction.Continue, nextNodeId = "start" },
                new CharacterDialogue.Choice { text = "看看其他商品", action = CharacterDialogue.ChoiceAction.Continue, nextNodeId = "other" },
                new CharacterDialogue.Choice { text = "再见", action = CharacterDialogue.ChoiceAction.Exit }
            } },
            new CharacterDialogue.Node { id = "other", text = "换一段对话", choices = new[] {
                new CharacterDialogue.Choice { text = "回到柜台", action = CharacterDialogue.ChoiceAction.Continue, nextNodeId = "start" }
            } }
        });
        yield return null; CharacterDialogueWindow.Open(owner, player); yield return null;
        var window = Object.FindObjectOfType<CharacterDialogueWindow>(); Check(window != null, "actual shop modal opens");
        // Dispatch real uGUI events deterministically, without the desktop mouse moving our test pointer.
        foreach (var module in EventSystem.current.GetComponents<BaseInputModule>()) module.enabled = false;
        var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        var buttons = (Button[])Get(window, "buttons"); var b = buttons[0];
        Check(Time.timeScale == 0, "feedback tested while the game is paused");
        ExecuteEvents.Execute(b.gameObject, pointer, ExecuteEvents.pointerEnterHandler); yield return Fade();
        Tint(b, b.colors.highlightedColor, "initial pointer enter uses existing hover color");
        Click(b, pointer); yield return Fade();
        Check(CharacterDialogueWindow.IsOpen && inv.GetQuantity("gold_coin") == 3 && offer.ReadStockCount() == 2, "purchase succeeds without closing");
        Tint(b, b.colors.highlightedColor, "hover survives purchase without moving the mouse");
        ExecuteEvents.Execute(b.gameObject, pointer, ExecuteEvents.pointerExitHandler); yield return Fade();
        Tint(b, b.colors.normalColor, "pointer exit restores normal color after click");
        ExecuteEvents.Execute(b.gameObject, pointer, ExecuteEvents.pointerEnterHandler); yield return Fade();
        Tint(b, b.colors.highlightedColor, "reentering a previously clicked option still highlights");
        ExecuteEvents.Execute(b.gameObject, pointer, ExecuteEvents.pointerDownHandler); yield return Fade();
        Tint(b, b.colors.pressedColor, "existing pressed feedback preserved");
        ExecuteEvents.Execute(b.gameObject, pointer, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(b.gameObject, pointer, ExecuteEvents.pointerClickHandler); yield return Fade();
        Check(!b.interactable && inv.GetQuantity("gold_coin") == 0 && offer.ReadStockCount() == 1, "second purchase disables unaffordable option");
        Tint(b, b.colors.disabledColor, "unaffordable offer retains disabled style under pointer");
        Click(b, pointer); yield return null; Check(offer.ReadStockCount() == 1, "disabled click never buys");
        inv.SetSlot(0, "gold_coin", 3); Call(window, "RefreshChoices"); yield return Fade();
        Check(b.interactable, "availability refresh reenables the option");
        Tint(b, b.colors.highlightedColor, "reenabling under stationary pointer restores hover");
        Click(b, pointer); yield return Fade();
        Check(offer.ReadStockCount() == 0 && !b.interactable, "third purchase sells out");
        Tint(b, b.colors.disabledColor, "sold-out offer never falsely highlights");
        ExecuteEvents.Execute(b.gameObject, pointer, ExecuteEvents.pointerExitHandler);
        ExecuteEvents.Execute(buttons[1].gameObject, pointer, ExecuteEvents.pointerEnterHandler); yield return Fade();
        Tint(buttons[1], buttons[1].colors.highlightedColor, "ordinary dialogue option also hovers");
        Click(buttons[1], pointer); yield return Fade();
        Check(((CharacterDialogue.Node)Get(window, "current")).id == "other" && !buttons[1].gameObject.activeSelf, "node change hides old options");
        ExecuteEvents.Execute(b.gameObject, pointer, ExecuteEvents.pointerEnterHandler); yield return Fade();
        Tint(b, b.colors.highlightedColor, "reused formerly disabled option gets normal hover");
        // This is the same path used by the existing number-key shortcuts.
        Call(window, "Select", 0); yield return null;
        Check(((CharacterDialogue.Node)Get(window, "current")).id == "start", "number-key selection remains functional");
        ExecuteEvents.Execute(buttons[2].gameObject, pointer, ExecuteEvents.pointerEnterHandler); yield return Fade();
        Tint(buttons[2], buttons[2].colors.highlightedColor, "reactivated goodbye option hovers");
        Click(buttons[2], pointer); yield return null;
        Check(!CharacterDialogueWindow.IsOpen && Time.timeScale == 1, "goodbye closes and restores time");
    }
}
