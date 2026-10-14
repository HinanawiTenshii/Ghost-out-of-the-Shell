using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

// Native runtime regression; execute only in the disposable smoke project.
[InitializeOnLoad]
public static class WaterwayGatePowerReturnSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static readonly Stack<IEnumerator> steps = new Stack<IEnumerator>();
    static int frame = -1, checks;
    static double deadline;
    static WaterwayGatePowerReturnSmoke() { EditorApplication.update += Tick; }
    static void Set(object target, string field, object value) => target.GetType().GetField(field, F).SetValue(target, value);
    static T Get<T>(object target, string field) => (T)target.GetType().GetField(field, F).GetValue(target);
    static void Check(bool condition, string label) { checks++; if (!condition) throw new Exception(label); }
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/")) throw new Exception("isolated project required");
        PlayerSettings.productName = "waterway-gate-power-return-smoke";
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(nameof(WaterwayGatePowerReturnSmoke), true);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(WaterwayGatePowerReturnSmoke), false))
            {
                SessionState.SetBool(nameof(WaterwayGatePowerReturnSmoke), false);
                deadline = EditorApplication.timeSinceStartup + 90;
                steps.Push(Verify());
            }
            if (steps.Count == 0) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("suite timed out");
            while (steps.Count > 0)
            {
                var step = steps.Peek();
                if (!step.MoveNext()) { steps.Pop(); continue; }
                if (step.Current is IEnumerator nested) { steps.Push(nested); continue; }
                return;
            }
            Debug.Log("WATERWAY_GATE_POWER_RETURN_PASS checks=" + checks);
            EditorApplication.Exit(0);
        }
        catch (Exception e) { steps.Clear(); Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static IEnumerator Until(Func<bool> ready, string label)
    {
        double timeout = EditorApplication.timeSinceStartup + 4;
        while (!ready())
        {
            if (EditorApplication.timeSinceStartup > timeout) throw new Exception("timeout: " + label);
            yield return null;
        }
        Check(true, label);
    }
    sealed class Fixture
    {
        public GameObject root;
        public WaterwayGate gate;
        public WaterwayGateController controller;
        public BatterySocket socket;
        public WaterwayGateTargetFade water;
        public int opened, closed;
    }
    static GameObject Child(GameObject root, string name)
    {
        var go = new GameObject(name); go.transform.SetParent(root.transform); return go;
    }
    static Fixture Make(bool open)
    {
        var f = new Fixture { root = new GameObject("Gate Power Fixture") };
        f.root.SetActive(false);
        f.gate = Child(f.root, "Gate").AddComponent<WaterwayGate>();
        Set(f.gate, "open", open); Set(f.gate, "moveDuration", .12f);
        f.controller = Child(f.root, "Controller").AddComponent<WaterwayGateController>();
        Set(f.controller, "linkedGate", f.gate);
        f.socket = Child(f.root, "Socket").AddComponent<BatterySocket>();
        Set(f.socket, "startsWithBattery", true); Set(f.socket, "linkedMechanism", f.controller);
        var water = Child(f.root, "Water"); water.AddComponent<SpriteRenderer>();
        f.water = water.AddComponent<WaterwayGateTargetFade>();
        Set(f.water, "sourceGate", f.gate); Set(f.water, "fadeDuration", .18f);
        Get<UnityEvent>(f.gate, "onOpened").AddListener(() => { f.opened++; f.water.FadeIn(); });
        Get<UnityEvent>(f.gate, "onClosed").AddListener(() => { f.closed++; f.water.FadeOut(); });
        f.root.SetActive(true); return f;
    }
    static BatterySocket.RuntimeState Power(int charge) => new BatterySocket.RuntimeState
    {
        installed = true,
        battery = new PersistentInventory.RuntimeSlotState
        {
            itemId = BatteryPickupItem.DefaultItemId, itemInstanceId = Guid.NewGuid().ToString("N"),
            quantity = 1, itemState = BatteryPickupItem.EncodeCharge(charge, 5)
        }
    };
    static IEnumerator Settled(Fixture f, bool open)
    {
        yield return Until(() => !f.gate.IsMoving && !f.water.IsFading, "gate and water animation complete");
        Check(f.gate.IsOpenRequested == open && f.water.VisibleRequested == open, "gate and water request agree");
        Check(f.water.gameObject.activeSelf == open && Mathf.Approximately(f.water.Visibility, open ? 1 : 0), "water enables/fades or fades/disables fully");
        Check(f.gate.GetComponent<Collider2D>().enabled != open, "gate collision matches state");
    }
    static IEnumerator Verify()
    {
        var inventoryObject = new GameObject("Gate Power Test Inventory"); inventoryObject.SetActive(false);
        var inv = inventoryObject.AddComponent<PersistentInventory>();
        Set(inv, "saveToPlayerPrefs", false); inventoryObject.SetActive(true); inv.ClearAll(); inv.enabled = false;
        foreach (bool initial in new[] { false, true })
        {
            var f = Make(initial); yield return null;
            Check(f.gate.InitialOpen == initial, "authored default remembered");
            yield return Settled(f, initial);
            f.controller.SetOpen(!initial); yield return Settled(f, !initial);
            Check(f.socket.RemainingCharge == 5, "normal water gate operation still consumes no charge");
            Check(f.socket.TryUninstall(inv), "battery removal succeeds");
            Check(f.gate.IsOpenRequested == initial && !f.controller.HasBatteryPower, "removal immediately springs to initial state");
            f.controller.ToggleFromExternal(); Check(f.gate.IsOpenRequested == initial, "unpowered controller cannot be used");
            yield return Settled(f, initial);
            yield return Until(() => Mathf.Approximately(Get<float>(f.controller, "pose"), initial ? 1 : 0), "handle returns to original pose");
            Check(f.socket.TryInstall(inv) && f.gate.IsOpenRequested == initial, "reinstall restores power without replaying old command");
            f.controller.SetOpen(!initial); yield return Settled(f, !initial);
            for (int i = 4; i >= 0; i--)
            {
                Check(f.socket.TryConsumeCharge() && f.socket.RemainingCharge == i, "charge consumption " + i);
                Check(f.gate.IsOpenRequested == (i == 0 ? initial : !initial), "only exhaustion causes spring return");
            }
            Check(f.socket.HasBattery && !f.socket.HasPower && !f.socket.TryConsumeCharge(), "empty battery remains installed, cannot underflow");
            yield return Settled(f, initial);
            f.socket.ApplyRuntimeState(Power(5));
            f.controller.SetOpen(!initial);
            yield return Until(() => f.gate.IsMoving, "gate starts moving");
            Check(f.controller.IsTransitionLocked, "transition lock active");
            f.socket.ApplyRuntimeState(default);
            Check(f.gate.IsOpenRequested == initial, "power loss bypasses movement lock");
            yield return Settled(f, initial);
            f.socket.ApplyRuntimeState(Power(5)); f.controller.SetOpen(!initial);
            yield return Until(() => f.water.IsFading, "water fading in progress");
            Check(f.controller.IsTransitionLocked, "fade lock active");
            f.socket.ApplyRuntimeState(Power(0));
            Check(f.gate.IsOpenRequested == initial, "power loss bypasses water fade lock");
            yield return Settled(f, initial);
            f.socket.ApplyRuntimeState(Power(5)); f.controller.SetOpen(!initial);
            yield return Settled(f, !initial);
            f.controller.gameObject.SetActive(false); f.controller.gameObject.SetActive(true);
            Check(f.gate.InitialOpen == initial && f.gate.IsOpenRequested != initial, "controller reload does not redefine initial position");
            f.controller.gameObject.SetActive(false);
            f.socket.TryConsumeExplosive(out int attack);
            Check(attack == 4 && f.gate.IsOpenRequested == initial, "destroying installed battery resets even an unloaded controller");
            yield return Settled(f, initial);
            f.controller.gameObject.SetActive(true);
            f.socket.ApplyRuntimeState(Power(5)); f.controller.SetOpen(!initial);
            yield return Settled(f, !initial);
            inv.ClearAll();
            for (int i = 0; i < inv.Slots.Count; i++) inv.TryAddUniqueItem("key", "gate-full-" + i);
            Check(!f.socket.TryUninstall(inv) && f.socket.HasPower && f.gate.IsOpenRequested != initial, "full inventory removal failure never resets gate");
            inv.ClearAll();
            int completed = f.opened + f.closed;
            f.socket.gameObject.SetActive(false); yield return null;
            Check(f.controller.HasBatteryPower && f.gate.IsOpenRequested != initial && completed == f.opened + f.closed, "socket distance unloading does not simulate power loss");
            f.socket.gameObject.SetActive(true);
            f.socket.BindMechanism(null);
            f.socket.ApplyRuntimeState(default);
            Check(f.controller.HasBatteryPower && f.gate.IsOpenRequested != initial, "unbound controllers retain legacy behavior");
            f.socket.BindMechanism(f.controller);
            Check(f.gate.IsOpenRequested == initial, "empty socket binding enforces original state");
            yield return Settled(f, initial);
            Object.Destroy(f.root); yield return null;
        }
        // Restore/control before the gate's first activation must not overwrite its default.
        var inactive = new GameObject("Initially Inactive Gate"); inactive.SetActive(false);
        var gate = inactive.AddComponent<WaterwayGate>(); Set(gate, "open", true);
        gate.SetOpenImmediately(false);
        Check(gate.InitialOpen && !gate.IsOpenRequested, "immediate restore captures original default before mutation");
        inactive.SetActive(true); yield return null;
        Check(gate.InitialOpen, "initial default survives first activation after restore");
        Object.Destroy(inactive);
    }
}
