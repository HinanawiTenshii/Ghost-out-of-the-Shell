using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;

// Isolated native Unity validation. Extracts the actual serialized scene bindings,
// rather than recreating their events in code. Never opens/saves the production scene.
[InitializeOnLoad]
public static class SmallWaterwayGateSmoke
{
    static readonly Stack<IEnumerator> steps = new Stack<IEnumerator>();
    static int frame = -1, checks;
    static SmallWaterwayGateSmoke() { EditorApplication.update += Tick; }
    static string Argument(string name)
    {
        var args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, name);
        if (i < 0 || i + 1 == args.Length) throw new Exception("Missing " + name);
        return args[i + 1];
    }
    static Dictionary<string, string> Blocks(string text) => Regex.Matches(text.Replace("\r", ""),
        @"(?ms)^--- !u!\d+ &(\d+)[^\n]*\n.*?(?=^--- !u!|\z)")
        .Cast<Match>().ToDictionary(m => m.Groups[1].Value, m => m.Value.TrimEnd() + "\n");
    static void Check(bool ok, string label) { checks++; if (!ok) throw new Exception(label); }
    // Unity normalizes sibling indices when importing the edited hierarchy.
    // Ignore only these indices; protected water geometry/components/bindings stay intact.
    static string WithoutSiblingIndex(string block) => Regex.Replace(block,
        @"(?m)^  m_RootOrder: \d+\n", "");

    public static void Run()
    {
        try
        {
            var blocks = Blocks(File.ReadAllText(Argument("-sourceScene")));
            var before = Blocks(File.ReadAllText(Argument("-beforeScene")));
            var allowed = new HashSet<string> { "16903670", "413915280", "1081806942",
                "1159224587", "1159224588", "1978192927" };
            // Other parts of this in-progress level may be edited concurrently in Unity.
            // Protect the complete affected water/gate neighborhood, not unrelated edits.
            var protectedObjects = new HashSet<string> { "16903669", "413915279", "1159224587",
                "702626304", "1912401456", "654378124", "1867053254", "1978192926", "1081806941" };
            foreach (var entry in before)
            {
                string owner = Regex.Match(entry.Value, @"m_GameObject: \{fileID: (\d+)").Groups[1].Value;
                if (!protectedObjects.Contains(owner) && !protectedObjects.Contains(entry.Key)
                    && !entry.Key.StartsWith("9327") && !entry.Key.StartsWith("88310010")) continue;
                Check(blocks.ContainsKey(entry.Key) && (allowed.Contains(entry.Key)
                    || WithoutSiblingIndex(blocks[entry.Key]) == WithoutSiblingIndex(entry.Value)),
                    "Original scene block preserved: " + entry.Key);
            }
            Check(blocks["561464980"] == before["561464980"], "3B gate/events untouched");
            Check(blocks["702626305"] == before["702626305"], "Center pool transform untouched");

            var fixture = new Dictionary<string, string>();
            Action<string> add = id => { fixture[id] = blocks[id]; };
            Action<string> addObject = null;
            addObject = go =>
            {
                add(go);
                foreach (Match m in Regex.Matches(blocks[go], @"component: \{fileID: (\d+)"))
                {
                    string id = m.Groups[1].Value; add(id);
                    if (!blocks[id].StartsWith("--- !u!4 ")) continue;
                    foreach (Match child in Regex.Matches(blocks[id], @"(?m)^  - \{fileID: (\d+)"))
                    {
                        string childId = child.Groups[1].Value;
                        addObject(Regex.Match(blocks[childId], @"m_GameObject: \{fileID: (\d+)").Groups[1].Value);
                    }
                }
            };
            foreach (var entry in blocks.Where(p => p.Key.StartsWith("93881"))) add(entry.Key);
            foreach (string go in new[] { "1159224587", "16903669", "413915279", "702626304",
                "1912401456", "654378124", "1867053254", "9327000000" }) addObject(go);
            foreach (string id in new[] { "1978192926", "1978192927", "1081806941", "1081806942",
                "561464980", "561464981", "883100099" }) add(id);
            fixture["1978192927"] = FilterChildren(blocks["1978192927"], new[] { "9388100001", "9388100101",
                "702626305", "1912401457", "654378125", "1867053255", "9327000001" });
            fixture["1081806942"] = FilterChildren(blocks["1081806942"], new[] { "561464981",
                "9388110001", "9388110101", "9388110201", "9388120001", "9388120101", "9388120201" });
            const string path = "Assets/Scenes/SmallWaterwayGateFixture.unity";
            File.WriteAllText(path, "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n" + string.Concat(fixture.Values));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            SessionState.SetInt("SmallWaterwayGateChecks", checks);
            SessionState.SetBool("SmallWaterwayGateSmoke", true);
            EditorApplication.EnterPlaymode();
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static string FilterChildren(string block, string[] ids) => Regex.Replace(block,
        @"(?m)^  - \{fileID: (\d+)\}\n", m => ids.Contains(m.Groups[1].Value) ? m.Value : "");
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool("SmallWaterwayGateSmoke", false))
            {
                SessionState.SetBool("SmallWaterwayGateSmoke", false);
                checks = SessionState.GetInt("SmallWaterwayGateChecks", 0);
                steps.Push(Verify());
            }
            if (steps.Count == 0) return;
            while (steps.Count > 0)
            {
                var step = steps.Peek();
                if (!step.MoveNext()) { steps.Pop(); continue; }
                if (step.Current is IEnumerator nested) { steps.Push(nested); continue; }
                return;
            }
            Debug.Log("SMALL_WATERWAY_GATE_PASS checks=" + checks);
            EditorApplication.Exit(0);
        }
        catch (Exception e) { steps.Clear(); Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static T Find<T>(string name) where T : Component => Resources.FindObjectsOfTypeAll<T>()
        .Single(x => x.gameObject.scene.IsValid() && x.name == name);
    static object Field(object value, string name) => value.GetType().GetField(name,
        BindingFlags.NonPublic | BindingFlags.Instance).GetValue(value);
    static IEnumerator Until(Func<bool> ready, string label)
    {
        float deadline = Time.realtimeSinceStartup + 12;
        while (!ready())
        { if (Time.realtimeSinceStartup > deadline) throw new Exception("Timeout: " + label); yield return null; }
        Check(true, label);
    }
    static void Charge(BatterySocket socket, int charge)
    {
        socket.ApplyRuntimeState(new BatterySocket.RuntimeState { installed = true,
            battery = new PersistentInventory.RuntimeSlotState {
                itemId = BatteryPickupItem.DefaultItemId, quantity = 1,
                hasStoredCharge = true, storedCharge = charge } });
    }
    static IEnumerator Verify()
    {
        Time.timeScale = 1; Time.captureFramerate = 60;
        yield return null; yield return null;
        var left = Find<WaterwayGateTargetFade>("4B Left Water Branch");
        var right = Find<WaterwayGateTargetFade>("5B Right Water Branch");
        var center = Find<SpriteRenderer>("Square (6)");
        Color poolColor = center.color;
        var leftStrip = Find<SpriteRenderer>("4B Left Channel");
        var rightStrip = Find<SpriteRenderer>("5B Right Channel");
        Check(Mathf.Abs(leftStrip.bounds.max.x - center.bounds.min.x) < .001f,
            "Left split joins the original pool without a gap");
        Check(Mathf.Abs(rightStrip.bounds.min.x - center.bounds.max.x) < .001f,
            "Right split joins the original pool without a gap");
        Check(Mathf.Abs(leftStrip.bounds.min.x - (96.4503845f - 18.567781f)) < .001f
            && Mathf.Abs(rightStrip.bounds.max.x - (123.6770155f - 18.567781f)) < .001f,
            "Splitting preserves the original outer channel endpoints");
        Check(Mathf.Abs(leftStrip.bounds.size.y - 1f) < .001f
            && Mathf.Abs(rightStrip.bounds.size.y - 1f) < .001f, "One-unit water width unchanged");
        var banks = Find<Transform>("Wide Water Borders").GetComponentsInChildren<SpriteRenderer>();
        var bankColors = banks.Select(x => x.color).ToArray();
        var mainGate = Find<WaterwayGate>("3B");
        foreach (string number in new[] { "4B", "5B" })
        {
            var gate = Find<WaterwayGate>(number + " Small Water Gate");
            var controller = Find<WaterwayGateController>(number + " Controller");
            var socket = Find<BatterySocket>(number + " Battery Socket");
            var water = number == "4B" ? left : right;
            var other = number == "4B" ? right : left;
            Check(controller.LinkedGate == gate && socket.LinkedMechanism == controller, "Actual prefab references resolve");
            Check(gate.Identifier == number && gate.IsFullyOpen, "Initial identity/open state");
            Check(Mathf.Approximately(gate.transform.lossyScale.x, .625f), "Small gate scaled");
            Check(Mathf.Abs(gate.GetComponent<BoxCollider2D>().size.x * gate.transform.lossyScale.x - 1f) < .01f
                && Mathf.Abs(gate.transform.right.y) > .99f, "One-unit gate passage across the channel");
            Check(water.GetComponentsInChildren<SpriteRenderer>().Length == 2, "Horizontal and vertical water share fader");
            Check(water.GetComponent<CameraVisionStreamingExempt>(), "Transition not suspended by streaming");
            foreach (string eventName in new[] { "onOpened", "onClosed" })
            {
                var evt = (UnityEvent)Field(gate, eventName);
                Check(evt.GetPersistentEventCount() == 1 && evt.GetPersistentTarget(0) == water,
                    "Serialized callback targets this branch only");
                Check(evt.GetPersistentMethodName(0) == (eventName == "onOpened" ? "FadeIn" : "FadeOut"), "Fade callback name");
            }
            Check(!socket.HasPower && !controller.HasBatteryPower, "Empty socket blocks operation");
            controller.ToggleFromExternal(); yield return null;
            Check(gate.IsOpenRequested && water.Visibility == 1, "No battery cannot close gate");
            Charge(socket, 0); controller.ToggleFromExternal(); yield return null;
            Check(!controller.HasBatteryPower && gate.IsOpenRequested, "Exhausted battery cannot operate");
            Charge(socket, 5);
            Check(controller.HasBatteryPower && socket.GetComponent<BatterySocketCable>(), "Live battery powers controller and cable");
            controller.ToggleFromExternal();
            Check(!gate.IsOpenRequested && controller.IsTransitionLocked, "Powered close starts motion and lock");
            controller.ToggleFromExternal();
            Check(!gate.IsOpenRequested, "Cannot reverse during gate movement");
            yield return Until(() => water.Visibility < .85f && water.Visibility > 0, "Water fades after gate closes");
            Check(water.gameObject.activeSelf && controller.IsTransitionLocked, "Remain active/locked through fade");
            foreach (var sprite in water.GetComponentsInChildren<SpriteRenderer>())
                Check(sprite.color.a > 0 && sprite.color.a < 1, "Both channel legs visibly fade");
            controller.ToggleFromExternal();
            Check(!gate.IsOpenRequested, "Cannot reverse during water fade");
            Check(other.Visibility == 1 && other.gameObject.activeSelf, "Other branch unchanged");
            yield return Until(() => !water.gameObject.activeSelf, "Water disabled at zero opacity");
            foreach (var collider in water.GetComponentsInChildren<Collider2D>(true))
                Check(!collider.isActiveAndEnabled, "Dry channel collision disabled");
            Check(!controller.IsTransitionLocked && socket.RemainingCharge == 5, "Unlocks without consuming water-gate charge");
            Check(center.gameObject.activeSelf && center.color == poolColor && mainGate.IsOpenRequested, "Center pool/main gate unaffected");
            for (int i = 0; i < banks.Length; i++) Check(banks[i].color == bankColors[i], "Pale pool banks unchanged");
            socket.ApplyRuntimeState(default); controller.ToggleFromExternal();
            Check(!gate.IsOpenRequested, "Removing battery while closed prevents opening");
            Charge(socket, 5); controller.ToggleFromExternal();
            yield return Until(() => water.gameObject.activeSelf, "Reopen reactivates inactive branch");
            Check(water.Visibility < 1 && controller.IsTransitionLocked, "Reactivation fades in with lock");
            yield return Until(() => water.Visibility == 1, "Fade in completes");
            foreach (var sprite in water.GetComponentsInChildren<SpriteRenderer>()) Check(sprite.color.a == 1, "Original alpha restored");
            Check(!controller.IsTransitionLocked, "Ready for next operation");
        }
        mainGate.Close();
        yield return Until(() => mainGate.IsFullyClosed, "Original 3B still works");
        yield return Until(() => !Find<WaterwayGateTargetFade>("3BWater3").gameObject.activeSelf, "Original 3B water fades");
        Check(left.Visibility == 1 && right.Visibility == 1 && center.color == poolColor, "3B does not affect new independent branches/pool");
    }
}
