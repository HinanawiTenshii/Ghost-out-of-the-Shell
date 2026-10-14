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
public static class MeteorShowerAudioSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static readonly Stack<IEnumerator> steps = new Stack<IEnumerator>();
    static int checks, frame = -1; static double deadline;
    static MeteorShowerAudioSmoke() { EditorApplication.update += Tick; }
    static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    static T Get<T>(object o, string field) => (T)o.GetType().GetField(field, F).GetValue(o);
    static IEnumerator Wait(float seconds) { float end = Time.time + seconds; while (Time.time < end) yield return null; }
    static AudioSource[] Voices(GameObject root, string name = null) => root.GetComponentsInChildren<AudioSource>().Where(s => name == null || s.name.Contains(name)).ToArray();
    static bool Playing(GameObject root, string name) => Voices(root, name).Any(s => s.isPlaying);
    public static void Run()
    {
        Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "isolated project only");
        PlayerSettings.productName = "meteor-shower-audio-smoke";
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        foreach (var name in new[] { "MeteorDescent", "MeteorImpact", "MeteorFireLoop" })
        {
            var importer = (AudioImporter)AssetImporter.GetAtPath("Assets/Resources/Audio/" + name + ".wav");
            Check(importer != null && importer.forceToMono && importer.preloadAudioData && !importer.loadInBackground, "preloaded mono " + name);
            Check(importer.defaultSampleSettings.loadType == AudioClipLoadType.DecompressOnLoad &&
                importer.defaultSampleSettings.compressionFormat == AudioCompressionFormat.PCM, "PCM " + name);
        }
        SessionState.SetBool(nameof(MeteorShowerAudioSmoke), true); EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return; frame = Time.frameCount;
        try {
            if (SessionState.GetBool(nameof(MeteorShowerAudioSmoke), false)) {
                SessionState.SetBool(nameof(MeteorShowerAudioSmoke), false); steps.Push(Verify()); deadline = EditorApplication.timeSinceStartup + 120;
            }
            if (steps.Count == 0) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("timeout");
            while (steps.Count > 0) {
                var step = steps.Peek(); if (!step.MoveNext()) { steps.Pop(); continue; }
                if (step.Current is IEnumerator nested) { steps.Push(nested); continue; } return;
            }
            Debug.Log("METEOR_AUDIO_PASS checks=" + checks); EditorApplication.Exit(0);
        } catch (Exception e) { steps.Clear(); Time.timeScale = 1; AudioListener.pause = false; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void Energy(ZeldaCharacterData data, int value)
    {
        var state = data.CaptureRuntimeState(); state.possessionEnergy = value;
        state.spentBasePossessionEnergy = state.spentGrowthPossessionEnergy = state.crystalEnergyThirds = 0;
        data.ApplyRuntimeState(state); data.GetComponent<ZeldaFourWayMover>().EndItemAttack();
    }
    static void Advance(MeteorShowerSkillRuntime cast, float to) => typeof(MeteorShowerSkillRuntime).GetMethod("Advance", F).Invoke(cast,
        new object[] { to - Get<float>(cast, "elapsed") });
    static IEnumerator Verify()
    {
        Time.timeScale = 1; AudioListener.pause = false;
        new GameObject("Test Audio Listener").AddComponent<AudioListener>();
        var camera = new GameObject("Test Camera").AddComponent<Camera>(); camera.tag = "MainCamera";
        camera.transform.position = new Vector3(0, 0, -10);
        string[] names = { "MeteorDescent", "MeteorImpact", "MeteorFireLoop" }; float[] lengths = { .8f, .82f, 1.6f };
        for (int i = 0; i < names.Length; i++) {
            var clip = Resources.Load<AudioClip>("Audio/" + names[i]);
            Check(clip != null && clip.channels == 1 && clip.frequency == 44100 && Mathf.Abs(clip.length - lengths[i]) < .0001f, "clip format/duration " + i);
            var samples = new float[clip.samples]; Check(clip.GetData(samples, 0), "PCM readable " + i);
            Check(samples.All(s => !float.IsNaN(s) && Mathf.Abs(s) <= .781f), "no clipped samples " + i);
            if (i == 2) Check(Mathf.Abs(samples[0] - samples[samples.Length - 1]) < .001f, "fire loop seamless endpoints");
            else Check(Mathf.Abs(samples[0]) < .0001f && Mathf.Abs(samples[samples.Length - 1]) < .0001f, "one-shot click-free endpoints " + i);
        }
        var root = new GameObject("Base Meteor Audio Test"); var audio = new MeteorShowerAudio(root, false);
        for (int i = 0; i < 12; i++) audio.AddLanding(new Vector2(i % 2 == 0 ? 1 : -1, 0), i * .28f);
        audio.RenderAt(0); Check(Playing(root, "Descent") && !Playing(root, "Impact") && !Playing(root, "Fire"), "descent starts immediately, no premature explosion or fire");
        Check(Voices(root).Length == 1 && Mathf.Abs(Voices(root)[0].volume - .32f) < .001f, "single near descent level");
        audio.RenderAt(.79f); Check(!Playing(root, "Impact"), "no impact before fall finishes");
        audio.RenderAt(.801f); Check(Playing(root, "Impact") && Voices(root, "Fire").Length == 0, "actual landing plays impact; base never creates fire voice");
        var fallIds = new HashSet<int>(); var impactIds = new HashSet<int>(); var sourceIds = new HashSet<int>();
        for (float t = 0; t < 5; t += .04f) {
            audio.RenderAt(t);
            foreach (var voice in Get<Array>(audio, "descents")) if (Get<bool>(voice, "started")) fallIds.Add(Get<int>(voice, "eventId"));
            foreach (var voice in Get<Array>(audio, "impacts")) if (Get<bool>(voice, "started")) impactIds.Add(Get<int>(voice, "eventId"));
            foreach (var source in Voices(root)) sourceIds.Add(source.GetInstanceID());
            Check(Voices(root, "Descent").Sum(s => s.volume) <= .321f && Voices(root, "Impact").Sum(s => s.volume) <= .601f,
                "nearby transients respect aggregate gain budget");
        }
        Check(fallIds.Count == 12 && impactIds.Count == 12 && sourceIds.Count <= 8, "every meteor gets both cues; sources reused, bounded to eight");
        Check(Voices(root).All(s => !s.isPlaying), "all one-shots end after last tail"); audio.Stop(); Object.DestroyImmediate(root);

        root = new GameObject("Upgraded Meteor Audio Test"); audio = new MeteorShowerAudio(root, true);
        for (int i = 0; i < 15; i++) audio.AddLanding(new Vector2(1, 0), i * .28f);
        audio.RenderAt(.79f); Check(Voices(root, "Fire").Length == 0, "fire does not precede impact");
        audio.RenderAt(1.01f); var fire = Voices(root, "Fire").Single();
        Check(fire.isPlaying && fire.loop && fire.clip.name == "MeteorFireLoop", "upgraded impact activates correct looping fire clip");
        int fireId = fire.GetInstanceID(); audio.RenderAt(4.5f);
        Check(fire.isPlaying && fire.GetInstanceID() == fireId && Mathf.Abs(fire.volume - .27f) < .001f, "all fifteen fields share one stable, capped fire voice");
        audio.RenderAt(5.81f); Check(fire.isPlaying, "first expired fire does not silence later fields");
        float lastEnd = 14 * .28f + .8f + 5f;
        audio.RenderAt(lastEnd - .1f); Check(fire.volume > 0 && fire.volume < .1f, "last field gently fades before ending");
        audio.RenderAt(lastEnd + .01f); Check(!fire.isPlaying && Voices(root).All(s => !s.isPlaying), "no sound after final fire expires");

        // Re-sample a living phase to test pause, pitch, planar attenuation and budgets.
        audio.RenderAt(1.3f); Time.timeScale = 0; audio.RenderAt(1.3f);
        Check(Voices(root).All(s => !s.isPlaying), "time-scale pause freezes every voice");
        int cursor = fire.timeSamples; for (int i = 0; i < 4; i++) yield return null;
        Check(cursor == fire.timeSamples, "paused fire sample cursor stable");
        Time.timeScale = .5f; audio.RenderAt(1.3f);
        Check(fire.isPlaying && Voices(root).Where(s => s.isPlaying).All(s => Mathf.Abs(s.pitch - .5f) < .001f), "slow motion pitch follows skill clock");
        AudioListener.pause = true; audio.RenderAt(1.3f); Check(Voices(root).All(s => !s.isPlaying), "listener pause respected");
        AudioListener.pause = false; Time.timeScale = 1; audio.RenderAt(1.3f);
        camera.transform.position = new Vector3(13, 0, -100); audio.RenderAt(1.3f);
        Check(fire.volume > 0 && fire.volume < .27f && fire.panStereo < 0, "fire attenuates from field edge and pans on XY plane");
        camera.transform.position = new Vector3(40, 0, -10); audio.RenderAt(1.3f);
        Check(Voices(root).All(s => !s.isPlaying && s.volume == 0), "far effects muted without creating more voices");
        camera.transform.position = Vector3.back * 10; audio.RenderAt(1.3f);
        Check(fire.isPlaying && Mathf.Abs(fire.time - .5f) < .03f, "return to audible range resumes current fire phase");
        var secondRoot = new GameObject("Concurrent Shower"); var second = new MeteorShowerAudio(secondRoot, true);
        second.AddLanding(Vector2.zero, 0); second.RenderAt(1.3f); audio.RenderAt(1.3f);
        Check(Voices(root, "Fire").Sum(s => s.volume) + Voices(secondRoot, "Fire").Sum(s => s.volume) <= .271f, "simultaneous casts share fire gain budget");
        second.Stop(); Object.DestroyImmediate(secondRoot); audio.RenderAt(1.3f);
        Check(Mathf.Abs(fire.volume - .27f) < .001f, "ending concurrent cast restores normal gain");
        var other = SceneManager.CreateScene("Other Meteor Audio Scene"); SceneManager.SetActiveScene(other); audio.RenderAt(1.3f);
        Check(Voices(root).All(s => !s.isPlaying), "foreign scene cannot hear these fields");
        SceneManager.SetActiveScene(root.scene); audio.Stop(); Object.DestroyImmediate(root);

        // Actual native skill integration: event timing, death/disable/travel cleanup, realtime lifetime.
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Level2NPCS/Idle/OuterRealmVisitor-level2-Idle.prefab");
        var actor = Object.Instantiate(prefab); var data = actor.GetComponent<ZeldaCharacterData>();
        actor.GetComponent<ZeldaCharacterAiBase>().enabled = false; var mover = actor.GetComponent<ZeldaFourWayMover>();
        mover.ReceiveControl(Vector2.down); ZeldaRuntimeRegistry.ClaimControlledMover(mover);
        var growth = PlayerGrowthAttributes.GetOrCreate(); growth.ApplyRuntimeState(default); Energy(data, 10);
        while (DocumentReader.IsInputBlocked) yield return null;
        Check(data.TryUseCharacterSkill(), "production native skill cast: native=" + data.NativeCharacterSkillId +
            " energy=" + data.FinalPossessionEnergy + " dead=" + data.IsDead + " ghost=" + data.IsGhostLike +
            " controlled=" + (mover == ZeldaRuntimeRegistry.GetControlledMover()) + " enabled=" + mover.isActiveAndEnabled +
            " attacking=" + mover.IsAttacking + " time=" + Time.timeScale + " loading=" + GameSaveSystem.IsLoading +
            " document=" + DocumentReader.IsInputBlocked + " puppet=" + ClockworkPuppetRuntime.BlocksCharacterInput +
            " scene=" + actor.scene.name + " loaded=" + actor.scene.isLoaded);
        var cast = Object.FindObjectOfType<MeteorShowerSkillRuntime>();
        Check(Playing(cast.gameObject, "Descent") && !Playing(cast.gameObject, "Impact"), "production first descent audible immediately");
        cast.enabled = false; Check(Voices(cast.gameObject).All(s => !s.isPlaying), "disabling sequence stops audio synchronously");
        Advance(cast, .81f); Check(Playing(cast.gameObject, "Impact") && Get<int>(cast, "impacts") == 1, "impact audio shares actual damage timestamp");
        var firstStrike = Get<IList>(cast, "strikes")[0];
        Check((Vector2)Voices(cast.gameObject, "Impact").Single(s => s.isPlaying).transform.position == Get<Vector2>(firstStrike, "point"), "impact localized at landing point");
        SceneManager.MoveGameObjectToScene(actor, other); Advance(cast, .82f);
        Check(Get<bool>(cast, "cancelled") && Voices(cast.gameObject).All(s => !s.isPlaying), "travel cancels audio before deferred destruction");
        Object.DestroyImmediate(cast.gameObject); SceneManager.MoveGameObjectToScene(actor, SceneManager.GetActiveScene());
        Energy(data, 10); while (DocumentReader.IsInputBlocked) yield return null;
        Check(data.TryUseCharacterSkill(), "second production cast"); cast = Object.FindObjectOfType<MeteorShowerSkillRuntime>();
        cast.enabled = false; data.TakeDamage(999); Advance(cast, .1f);
        Check(Get<bool>(cast, "cancelled") && Voices(cast.gameObject).All(s => !s.isPlaying), "death cancels pending impacts and audio");
        Object.DestroyImmediate(cast.gameObject); Object.DestroyImmediate(actor);
        actor = Object.Instantiate(prefab); data = actor.GetComponent<ZeldaCharacterData>(); mover = actor.GetComponent<ZeldaFourWayMover>();
        actor.GetComponent<ZeldaCharacterAiBase>().enabled = false; mover.ReceiveControl(Vector2.down); ZeldaRuntimeRegistry.ClaimControlledMover(mover);
        growth.ApplyRuntimeState(new PlayerGrowthAttributes.RuntimeState { unlockedSkills = new[] { "meteor_shower", "meteor_shower_2", "meteor_shower_3" } });
        Energy(data, 5); while (DocumentReader.IsInputBlocked) yield return null;
        Check(data.TryUseCharacterSkill(), "live upgraded cast"); cast = Object.FindObjectOfType<MeteorShowerSkillRuntime>();
        var ownedSounds = new HashSet<AudioSource>(); float start = Time.time; bool heardFire = false;
        while (cast != null && Time.time - start < 12) {
            foreach (var sound in Voices(cast.gameObject)) ownedSounds.Add(sound);
            heardFire |= Playing(cast.gameObject, "Fire");
            Check(Voices(cast.gameObject).Length <= 9, "bounded production audio pool"); yield return null;
        }
        Check(cast == null && heardFire && ownedSounds.Count <= 9 && ownedSounds.All(s => s == null), "live upgraded effect releases every audio source");
        Check(data.CurrentHealth == 666 && data.FinalPossessionEnergy == 0 && !CameraVisionStreamingRegion.HasActiveRegions, "sound leaves gameplay and cleanup unchanged");
        Energy(data, 5); Check(data.TryUseCharacterSkill(), "scene unload cast"); cast = Object.FindObjectOfType<MeteorShowerSkillRuntime>();
        var scene = cast.gameObject.scene; var soundBeforeUnload = Voices(cast.gameObject)[0];
        SceneManager.MoveGameObjectToScene(actor, other); SceneManager.MoveGameObjectToScene(camera.gameObject, other); SceneManager.SetActiveScene(other);
        var unload = SceneManager.UnloadSceneAsync(scene); while (!unload.isDone) yield return null;
        Check(cast == null && soundBeforeUnload == null, "scene unload leaves no orphan audio");
    }
}
