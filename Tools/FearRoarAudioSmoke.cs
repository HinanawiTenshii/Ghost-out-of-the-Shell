using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Run in the disposable full-script Unity validation project, not an authored scene.
[InitializeOnLoad]
public static class FearRoarAudioSmoke
{
    const BindingFlags F = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static IEnumerator suite;
    static int frame = -1, checks;
    static double deadline;
    static FearRoarAudioSmoke() { EditorApplication.update += Tick; }
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var importer = (AudioImporter)AssetImporter.GetAtPath("Assets/Resources/Audio/FearRoar.wav");
        Check(importer != null && importer.preloadAudioData && !importer.loadInBackground, "preloaded sound imports");
        Check(importer.defaultSampleSettings.loadType == AudioClipLoadType.DecompressOnLoad &&
              importer.defaultSampleSettings.compressionFormat == AudioCompressionFormat.PCM, "no compressed first-play delay");
        SessionState.SetBool(nameof(FearRoarAudioSmoke), true);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(FearRoarAudioSmoke), false))
            { SessionState.SetBool(nameof(FearRoarAudioSmoke), false); deadline = EditorApplication.timeSinceStartup + 45; suite = Verify(); }
            if (suite == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Audio test timeout");
            if (suite.MoveNext()) return;
            suite = null; Time.timeScale = 1; AudioListener.pause = false;
            Debug.Log("FEAR_ROAR_AUDIO_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void Check(bool value, string label) { checks++; if (!value) throw new Exception(label); }
    static object Get(object o, string name) => o.GetType().GetField(name, F).GetValue(o);
    static object Call(object o, string name) => o.GetType().GetMethod(name, F).Invoke(o, null);
    static FearRoarArea Roar(ZeldaCharacterData caster, int level)
    {
        var area = new GameObject("Audio test roar").AddComponent<FearRoarArea>(); area.Initialize(caster, level); return area;
    }
    static IEnumerator Verify()
    {
        Time.timeScale = 1; Time.captureFramerate = 0; AudioListener.pause = false;
        new GameObject("Test ear").AddComponent<AudioListener>();
        var caster = new GameObject("Caster").AddComponent<BehemothZeldaCharacterData>();
        var mover = caster.gameObject.AddComponent<ZeldaFourWayMover>();
        ZeldaRuntimeRegistry.ClaimControlledMover(mover);
        var clip = Resources.Load<AudioClip>("Audio/FearRoar");
        Check(clip != null && clip.channels == 1 && clip.samples == 44100 && clip.frequency == 44100, "one-second mono PCM asset");
        float duration = (float)typeof(FearRoarArea).GetField("Duration", F).GetRawConstantValue();
        Check(Mathf.Abs(clip.length - duration) < .0001f, "clip length exactly equals effect duration");
        foreach (int level in new[] { 1, 2, 3 })
        {
            var area = Roar(caster, level); var source = area.GetComponent<AudioSource>();
            Check(source && source.isPlaying && source.clip == clip && !source.loop && !source.playOnAwake, "rank " + level + " starts exactly one voice");
            Check(area.GetComponents<AudioSource>().Length == 1 && source.volume <= .7f && source.pitch == 1f, "moderate volume and no random duration shift");
            Check((float)Get(area, "stunDuration") == (level == 3 ? 5f : 3f), "stun upgrades unchanged");
            float start = Time.time;
            while (area && (float)Get(area, "elapsed") < .25f) yield return null;
            Check(area && source.isPlaying, "sustained roar still audible with visible field");
            while (area && area.IsOngoing) yield return null;
            Check(!source || !source.isPlaying, "voice stops at end of visual lifetime");
            Check(Time.time - start >= .96f && Time.time - start < 1.2f, "one second of game time, not stun duration");
            yield return null;
        }
        var paused = Roar(caster, 1); var pausedSource = paused.GetComponent<AudioSource>();
        while ((float)Get(paused, "elapsed") < .15f) yield return null;
        Time.timeScale = 0; Call(paused, "Update");
        float elapsed = (float)Get(paused, "elapsed");
        Check(!pausedSource.isPlaying && (bool)Get(paused, "audioPaused"), "time-scale pause also pauses sound");
        double until = EditorApplication.timeSinceStartup + .22;
        while (EditorApplication.timeSinceStartup < until) yield return null;
        Check((float)Get(paused, "elapsed") == elapsed && !pausedSource.isPlaying, "no audible tail while scene is paused");
        Time.timeScale = 1; Call(paused, "Update");
        Check(pausedSource.isPlaying && !(bool)Get(paused, "audioPaused"), "unpause resumes current voice");
        paused.enabled = false; Check(!pausedSource.isPlaying, "component disable stops voice even if object remains active");
        paused.enabled = true;
        Check(pausedSource.isPlaying && paused.GetComponents<AudioSource>().Length == 1, "reenable uses same voice, not stacked audio sources");
        Check(Mathf.Abs(pausedSource.time - (float)Get(paused, "elapsed")) < .05f, "reenable seeks to effect progress");
        Object.DestroyImmediate(paused.gameObject); Check(!pausedSource, "early destruction removes owned voice");

        Time.timeScale = .5f;
        var slow = Roar(caster, 3); var slowSource = slow.GetComponent<AudioSource>();
        Check(Mathf.Approximately(slowSource.pitch, .5f), "slow motion follows effect clock");
        double slowStart = EditorApplication.timeSinceStartup;
        while (slow && slow.IsOngoing) yield return null;
        Check(EditorApplication.timeSinceStartup - slowStart > 1.8 && (!slowSource || !slowSource.isPlaying), "half-speed voice spans approximately two real seconds");
        Time.timeScale = 1; yield return null;
        // The normal skill path (including learned use) must share the same area audio.
        Check(caster.TryUseFearRoar(), "native skill casts via common entry");
        var cast = Object.FindObjectOfType<FearRoarArea>();
        Check(cast && cast.GetComponent<AudioSource>().isPlaying, "normal cast has audio without prefab configuration");
        Check(!caster.TryUseFearRoar() && Object.FindObjectsOfType<FearRoarArea>().Length == 1, "rejected repeated cast cannot stack audio");
        Object.DestroyImmediate(cast.gameObject);
        Check(FearRoarArea.ActiveAreas.Count == 0, "area registry still cleans up");
    }
}
