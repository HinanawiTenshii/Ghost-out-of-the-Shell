using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEngine;

// Transition diagnostics are opt-in outside the Editor. Timings overlap where
// one phase contains another; they must not be summed as independent costs.
public static class SceneLoadMetrics
{
    public static bool Enabled = Application.isEditor || UnityEngine.Debug.isDebugBuild;
    public static string LastSummary { get; private set; }
    private static readonly Dictionary<string, double> milliseconds = new Dictionary<string, double>();
    private static string destination;
    private static long started, engineStarted;
    private static bool active;

    public static void Begin(string scene)
    {
        if (!Enabled) return;
        milliseconds.Clear(); destination = scene; active = true;
        started = Stopwatch.GetTimestamp(); engineStarted = 0;
    }

    public static void BeginEngineLoad() { if (active) engineStarted = Stopwatch.GetTimestamp(); }
    public static void SceneLoaded()
    {
        if (active && engineStarted != 0) Add("Unity.LoadAndActivate", engineStarted);
        engineStarted = 0;
    }

    private static readonly IDisposable inactiveSample = new Sample(null, false);
    public static IDisposable Measure(string name) => active && Enabled ? new Sample(name, true) : inactiveSample;
    private sealed class Sample : IDisposable
    {
        private readonly string name;
        private readonly long start;
        public Sample(string key, bool enabled) { name = key; start = enabled ? Stopwatch.GetTimestamp() : 0; }
        public void Dispose() { if (start != 0 && active) Add(name, start); }
    }
    private static void Add(string name, long start)
    {
        milliseconds.TryGetValue(name, out double previous);
        milliseconds[name] = previous + (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
    }
    public static void Complete()
    {
        if (!active) return;
        Add("Transition.Total", started);
        var text = new StringBuilder("[SceneLoad] " + destination);
        foreach (var pair in milliseconds) text.Append(" | ").Append(pair.Key).Append('=').Append(pair.Value.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)).Append("ms");
        LastSummary = text.ToString(); active = false;
        UnityEngine.Debug.Log(LastSummary);
    }
}
