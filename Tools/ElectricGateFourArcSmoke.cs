using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class ElectricGateFourArcSmoke
{
    const string Key = nameof(ElectricGateFourArcSmoke);
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static int checks, frame = -1;
    static double deadline;
    static readonly Stack<IEnumerator> steps = new Stack<IEnumerator>();
    static object field;
    static List<ZeldaFourWayMover> movers;
    static ElectricGateFourArcSmoke() { EditorApplication.update += Tick; }
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\','/').Contains("/.codex-temp/")) throw new Exception("Isolated project required");
        PlayerSettings.productName = "electric-gate-four-arc-smoke";
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Key, true); EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(Key, false))
            { SessionState.SetBool(Key, false); steps.Push(Verify()); deadline = EditorApplication.timeSinceStartup + 90; }
            if (steps.Count == 0) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Gate test timed out");
            while (steps.Count > 0)
            {
                var step = steps.Peek(); if (!step.MoveNext()) { steps.Pop(); continue; }
                if (step.Current is IEnumerator nested) { steps.Push(nested); continue; }
                return;
            }
            Debug.Log("ELECTRIC_GATE_FOUR_ARC_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception ex) { steps.Clear(); Debug.LogException(ex); EditorApplication.Exit(1); }
    }
    static void Check(bool ok, string message)
    { checks++; if (!ok) throw new Exception(message); Debug.Log("GATE_FOUR_ARC_CHECK " + message); }
    static T Get<T>(object owner, string name) => (T)owner.GetType().GetField(name, Flags).GetValue(owner);
    static void Call(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, Flags).Invoke(owner, args);
    static BatterySocket Supply(ElectricGatePillar pillar)
    {
        var socket = new GameObject("Gate test socket").AddComponent<BatterySocket>();
        socket.transform.position = pillar.transform.position + Vector3.up * 4;
        socket.BindMechanism(pillar); Charge(socket, 5); return socket;
    }
    static void Charge(BatterySocket socket, int units) => socket.ApplyRuntimeState(new BatterySocket.RuntimeState {
        installed = true, battery = new PersistentInventory.RuntimeSlotState {
            itemId = BatteryPickupItem.DefaultItemId, quantity = 1, itemState = BatteryPickupItem.EncodeCharge(units, 5) }
    });
    static void ResetBody(ZeldaCharacterData data, int permission = 0, int shields = 0, int health = 100)
    {
        var saved = data.CaptureRuntimeState(); saved.health = 100; saved.currentHealth = health; saved.permissionLevel = permission;
        saved.holyShield = new HolyShieldRuntime.State { charges = shields, capacity = shields };
        data.ApplyRuntimeState(saved);
    }
    static ZeldaFourWayMover Victim()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Level2NPCS/Idle/Civilian-level2-Idle.prefab");
        var victim = Object.Instantiate(prefab); victim.GetComponent<ZeldaCharacterAiBase>().enabled = false;
        var mover = victim.GetComponent<ZeldaFourWayMover>(); mover.enabled = false;
        ResetBody(mover.CharacterData); Move(mover, 2); return mover;
    }
    static void Move(ZeldaFourWayMover mover, float y)
    {
        mover.transform.position = new Vector3(4, y); mover.GetComponent<Rigidbody2D>().position = mover.transform.position;
        Physics2D.SyncTransforms();
    }
    static void Step() => Call(field, "Tick", movers);
    static void At(ZeldaFourWayMover mover, float y) { Move(mover, y); Step(); }
    static IEnumerator Delay(float seconds)
    {
        float until = Time.time + seconds;
        while (Time.time < until) yield return null;
        Step();
    }
    static IEnumerator Second() => Delay(ElectricGateRules.PairDelay + .02f);
    static IEnumerator Verify()
    {
        var a = new GameObject("Gate A").AddComponent<ElectricGatePillar>();
        var b = new GameObject("Gate B").AddComponent<ElectricGatePillar>(); b.transform.position = Vector3.right * 8;
        var supply = Supply(a);
        var network = (ElectricGateNetwork)typeof(ElectricGateNetwork).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        network.enabled = false; Physics2D.SyncTransforms(); Call(network, "Pair");
        var fields = Get<IDictionary>(network, "fields");
        Check(fields.Count == 1, "two pillars share one field");
        var e = fields.Values.GetEnumerator(); e.MoveNext(); field = e.Current;
        var arcs = Get<LineRenderer[]>(field, "arcs");
        var mover = Victim(); var data = mover.CharacterData; movers = new List<ZeldaFourWayMover> { mover };
        At(mover, 2); At(mover, 0);
        Check(data.CurrentHealth == 96 && supply.RemainingCharge == 4, "entry deals 2+2 and pays once");
        Check(arcs.Length == 4 && arcs[0].enabled && arcs[1].enabled && !arcs[2].enabled && !arcs[3].enabled, "only first two arcs appear immediately");
        Check((arcs[0].GetPosition(0) - arcs[1].GetPosition(0)).sqrMagnitude > 60, "first pair fires from different pillars");
        float started = Time.time;
        yield return Delay(.1f);
        Check(Time.time - started < ElectricGateRules.PairDelay && data.CurrentHealth == 96, "second pair never deals damage early");
        Time.timeScale = 0;
        for (int i = 0; i < 5; i++) { yield return null; Step(); }
        Check(data.CurrentHealth == 96 && supply.RemainingCharge == 4, "pause freezes pending pair without extra charge");
        Time.timeScale = 1;
        At(mover, 2); At(mover, 0);
        Check(data.CurrentHealth == 96 && supply.RemainingCharge == 4, "rapid reentry cannot duplicate a pending burst");
        yield return Second();
        Check(data.CurrentHealth == 84 && supply.RemainingCharge == 4, "delayed pair deals 6+6 with no second energy payment");
        Check(!arcs[0].enabled && !arcs[1].enabled && arcs[2].enabled && arcs[3].enabled, "second visual pair replaces expired first pair");
        Check(arcs[2].positionCount >= 3 && arcs[3].positionCount >= 3 &&
            (arcs[2].GetPosition(0) - arcs[3].GetPosition(0)).sqrMagnitude > 60, "second pair also fires from different pillars");
        Step(); Step(); Check(data.CurrentHealth == 84 && supply.RemainingCharge == 4, "completed pair cannot repeat on subsequent ticks");
        yield return Delay(ElectricGateRules.ArcLifetime + .02f);
        Check(arcs.All(line => !line.enabled), "both visual groups finish cleanly");
        At(mover, 2); ResetBody(data); Charge(supply, 1); At(mover, 0);
        Check(data.CurrentHealth == 96 && supply.RemainingCharge == 0, "first group consumes final battery unit");
        yield return Second();
        Check(data.CurrentHealth == 84 && supply.RemainingCharge == 0 && arcs[2].enabled && arcs[3].enabled, "last charge still completes delayed damage and visuals");
        At(mover, 2); At(mover, 0); yield return Second();
        Check(data.CurrentHealth == 84, "empty battery cannot start another burst");
        At(mover, 2); Charge(supply, 5); ResetBody(data, 9); At(mover, 0); yield return Second();
        Check(data.CurrentHealth == 100 && supply.RemainingCharge == 5, "permitted entrant remains unharmed and free");
        At(mover, 2); ResetBody(data, 0, 3); At(mover, 0);
        Check(data.CurrentHealth == 100 && data.HolyShieldCharges == 1, "first two small hits each consume one shield");
        yield return Second();
        Check(data.CurrentHealth == 94 && data.HolyShieldCharges == 0 && supply.RemainingCharge == 4, "three shields leave exactly one six-damage hit");
        At(mover, 2); ResetBody(data); Charge(supply, 5); var secondSupply = Supply(b); At(mover, 0); yield return Second();
        Check(data.CurrentHealth == 84 && supply.RemainingCharge == 4 && secondSupply.RemainingCharge == 4, "dual supply charges once each for both groups");
        At(mover, 2); Charge(supply, 5); Charge(secondSupply, 0); ResetBody(data); At(mover, 0); yield return Second();
        Check(data.CurrentHealth == 100 && supply.RemainingCharge == 5, "empty second supply blocks whole burst without partial payment");
        At(mover, 2); Charge(supply, 5); Charge(secondSupply, 5); ResetBody(data);
        var other = Victim(); var otherData = other.CharacterData; movers.Add(other); Step();
        Move(mover, 0); Move(other, 0); Step();
        Check(data.CurrentHealth == 96 && otherData.CurrentHealth == 96 && supply.RemainingCharge == 3, "simultaneous entrants each get first pair and one charge");
        yield return Second();
        Check(data.CurrentHealth == 84 && otherData.CurrentHealth == 84 && supply.RemainingCharge == 3, "one entrant never overwrites another delayed target");
        Move(mover, 2); Move(other, 2); Step(); ResetBody(otherData); At(other, 0);
        var originalScene = other.gameObject.scene;
        var destination = SceneManager.CreateScene("Other scene"); SceneManager.MoveGameObjectToScene(other.gameObject, destination);
        yield return Second();
        Check(otherData.CurrentHealth == 96 && Get<IList>(field, "pendingStrikes").Count == 0, "scene travel cancels delayed attack without retargeting");
        SceneManager.MoveGameObjectToScene(other.gameObject, originalScene); Move(other, 2); Step();
        ResetBody(data, health: 3); Charge(supply, 5); Charge(secondSupply, 5); At(mover, 0);
        Check(data.IsDead, "first group can kill a low-health target");
        yield return Second();
        Check(Get<IList>(field, "pendingStrikes").Count == 0, "dead target receives no delayed hit and no replacement ghost is targeted");
        movers.Remove(mover); ResetBody(otherData); At(other, 0);
        Check(otherData.CurrentHealth == 96, "final fixture starts a pending second group");
        b.enabled = false; Step(); yield return Second();
        Check(otherData.CurrentHealth == 96 && Get<IList>(field, "pendingStrikes").Count == 0 && arcs.All(line => !line.enabled), "disabled pillar cancels pending damage and visuals");
        Check(Get<LineRenderer[]>(field, "arcs").SequenceEqual(arcs), "all attacks reuse existing arc renderers");
        Call(field, "Dispose");
        Check(Get<IList>(field, "pendingStrikes").Count == 0, "field teardown leaves no delayed callback");
    }
}
