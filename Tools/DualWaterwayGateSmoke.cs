using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

// Run only in the isolated validation project, not the working level.
[InitializeOnLoad]
public static class DualWaterwayGateSmoke
{
    static readonly Stack<IEnumerator> steps = new Stack<IEnumerator>();
    static readonly List<GameObject> owned = new List<GameObject>();
    static int frame = -1, checks;
    static DualWaterwayGateSmoke() { EditorApplication.update += Tick; }
    public static void Run()
    {
        UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
        SessionState.SetBool("DualWaterwayGateSmoke", true);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool("DualWaterwayGateSmoke", false))
            { SessionState.SetBool("DualWaterwayGateSmoke", false); steps.Push(Verify()); }
            if (steps.Count == 0) return;
            while (steps.Count > 0)
            {
                var step = steps.Peek();
                if (!step.MoveNext()) { steps.Pop(); continue; }
                if (step.Current is IEnumerator nested) { steps.Push(nested); continue; }
                return;
            }
            Clear(); Debug.Log("DUAL_WATERWAY_GATE_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { steps.Clear(); Clear(); Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void Check(bool ok, string label) { checks++; if (!ok) throw new Exception(label); }
    static void Clear() { foreach (var go in owned) if (go) Object.DestroyImmediate(go); owned.Clear(); }
    static GameObject Make(string name) { var go = new GameObject(name); owned.Add(go); return go; }
    static FieldInfo Field(object o, string name)
    {
        for (Type t = o.GetType(); t != null; t = t.BaseType)
        { var f = t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic); if (f != null) return f; }
        throw new Exception("Missing field: " + name);
    }
    static void Set(object o, string name, object value) => Field(o, name).SetValue(o, value);
    static UnityEvent Event(WaterwayGate gate, string name) => (UnityEvent)Field(gate, name).GetValue(gate);
    static WaterwayGate Gate(string name, bool open)
    {
        var gate = Make(name).AddComponent<WaterwayGate>();
        gate.SetOpenImmediately(open); Set(gate, "moveDuration", .1f); return gate;
    }
    static void Bind(WaterwayGate gate, WaterwayGateTargetFade fade)
    {
        // Persistent events use the same serialization and virtual dispatch as the scene.
        UnityEditor.Events.UnityEventTools.AddPersistentListener(Event(gate, "onOpened"), fade.FadeIn);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(Event(gate, "onClosed"), fade.FadeOut);
    }
    static DualWaterwayGateTargetFade Water(WaterwayGate a, WaterwayGate b)
    {
        var go = Make("DownWingWater3 test");
        var renderer = go.AddComponent<SpriteRenderer>(); renderer.color = new Color(.1f, .4f, .9f, .8f);
        var child = new GameObject("Water border", typeof(SpriteRenderer)); child.transform.SetParent(go.transform);
        child.GetComponent<SpriteRenderer>().color = new Color(.4f, .5f, .6f, .4f);
        go.AddComponent<BoxCollider2D>();
        var water = go.AddComponent<DualWaterwayGateTargetFade>();
        Set(water, "sourceGate", a); Set(water, "otherGate", b);
        Bind(a, water); Bind(b, water); return water;
    }
    static IEnumerator Until(Func<bool> ready, string label)
    {
        float deadline = Time.realtimeSinceStartup + 8;
        while (!ready())
        { if (Time.realtimeSinceStartup > deadline) throw new Exception("Timeout: " + label); yield return null; }
        Check(true, label);
    }
    static bool Full(WaterwayGateTargetFade water) => water.gameObject.activeSelf && water.Visibility == 1 && !water.IsFading;
    static IEnumerator Verify()
    {
        Time.timeScale = 1;
        // All authored initial combinations, including the base source closed but secondary open.
        foreach (bool first in new[] { false, true }) foreach (bool second in new[] { false, true })
        {
            var a = Gate("3A", first); var b = Gate("2A", second); var water = Water(a, b);
            yield return null; yield return null;
            Check(water.gameObject.activeSelf == (first || second), "initial OR visibility");
            Check(water.VisibleRequested == (first || second), "initial requested visibility");
            Check(water.Visibility == ((first || second) ? 1 : 0), "initial state is immediate");
            Check(water.GetComponent<CameraVisionStreamingExempt>(), "inherits streaming exemption");
            Check(water.GetComponents<WaterwayGateTargetFade>().Length == 1, "one fader component");
            Clear(); yield return null;
        }
        // Both close orders and reopening through either source. Observe actual Unity Update/events.
        for (int order = 0; order < 2; order++)
        {
            var a = Gate("3A", true); var b = Gate("2A", true); var unrelated = Gate("1A", true);
            var water = Water(a, b); yield return null; yield return null;
            WaterwayGate first = order == 0 ? a : b, last = order == 0 ? b : a;
            first.Close(); yield return Until(() => first.IsFullyClosed, "first gate closes"); yield return null;
            Check(Full(water), "closing one gate must not fade shared water");
            Check(!WaterwayGateTargetFade.IsGateTransitioning(first), "single closed gate unlocks when water stays visible");
            last.Close(); Check(Full(water), "keep water until second closing animation finishes");
            yield return Until(() => water.IsFading, "second close starts fade");
            Check(!water.VisibleRequested && water.gameObject.activeSelf, "fading out still active");
            Check(water.GetComponent<BoxCollider2D>().enabled, "collision stays until deactivation");
            Check(WaterwayGateTargetFade.IsGateTransitioning(a) && WaterwayGateTargetFade.IsGateTransitioning(b), "both controllers locked while shared water fades");
            Check(!WaterwayGateTargetFade.IsGateTransitioning(unrelated), "unrelated gate not locked");
            yield return Until(() => water.Visibility < .8f && water.Visibility > 0, "smooth intermediate alpha");
            Check(water.GetComponent<SpriteRenderer>().color.a > 0 && water.GetComponent<SpriteRenderer>().color.a < .8f, "native renderer alpha fades");
            float before = water.Visibility; Time.timeScale = 0; yield return null; yield return null;
            Check(water.Visibility == before, "pause freezes transition"); Time.timeScale = 1;
            yield return Until(() => !water.gameObject.activeSelf, "both shut disable after fade");
            Check(water.Visibility == 0 && !water.GetComponent<BoxCollider2D>().isActiveAndEnabled, "fully hidden has no collision");
            Check(!WaterwayGateTargetFade.IsGateTransitioning(a) && !WaterwayGateTargetFade.IsGateTransitioning(b), "inactive fader releases both locks");
            first.Open(); yield return Until(() => water.gameObject.activeSelf, "either gate reactivates inactive water");
            Check(water.VisibleRequested && water.IsFading && water.Visibility < 1, "reopen fades instead of snapping");
            Check(WaterwayGateTargetFade.IsGateTransitioning(a) && WaterwayGateTargetFade.IsGateTransitioning(b), "both controllers locked during fade in");
            yield return Until(() => Full(water), "water fully restored");
            Check(Mathf.Approximately(water.GetComponent<SpriteRenderer>().color.a, .8f), "original alpha survives reactivation");
            Check(Mathf.Approximately(water.transform.GetChild(0).GetComponent<SpriteRenderer>().color.a, .4f), "child alpha also preserved");
            Check(!WaterwayGateTargetFade.IsGateTransitioning(a) && !WaterwayGateTargetFade.IsGateTransitioning(b), "fade in completion releases locks");
            // External mechanisms may reverse gates even while manual controllers are locked.
            first.Close(); yield return Until(() => water.IsFading, "repeat close starts fade");
            yield return Until(() => water.Visibility < .8f, "partial fade before reversal");
            last.Open(); yield return Until(() => water.VisibleRequested, "other source reverses fade");
            Check(water.gameObject.activeSelf && water.Visibility > 0 && water.Visibility < 1, "reversal does not jump to endpoint");
            yield return Until(() => Full(water), "reverse reaches full opacity");
            // A stale close callback must not hide a water supplied by another opening gate.
            first.Open(); yield return Until(() => first.IsFullyOpen, "reset first source");
            last.SetOpenImmediately(false); first.Close(); last.Open();
            yield return Until(() => first.IsFullyClosed && last.IsFullyOpen, "opposing gate animations complete");
            Check(Full(water), "opposing events preserve OR state");
            // Closing both at once waits for the slower source before fading.
            first.SetOpenImmediately(true); last.SetOpenImmediately(true); Set(last, "moveDuration", .4f);
            first.Close(); last.Close(); yield return Until(() => first.IsFullyClosed, "fast source closes");
            Check(!last.IsFullyClosed && Full(water), "no fade while other source still closing");
            yield return Until(() => !water.gameObject.activeSelf, "parallel close eventually hides water");
            Clear(); yield return null;
        }
        // The ordinary component retains its original independent single-gate behavior.
        var singleGate = Gate("ordinary source", true);
        var ordinary = Make("ordinary water").AddComponent<WaterwayGateTargetFade>();
        Set(ordinary, "sourceGate", singleGate); Bind(singleGate, ordinary); yield return null;
        singleGate.Close(); yield return Until(() => !ordinary.gameObject.activeSelf, "single-gate close unchanged");
        singleGate.Open(); yield return Until(() => Full(ordinary), "single-gate reopen unchanged");
    }
}
