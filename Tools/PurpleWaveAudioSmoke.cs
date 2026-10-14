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
public static class PurpleWaveAudioSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static readonly Stack<IEnumerator> steps = new Stack<IEnumerator>();
    static int checks, frame = -1;
    static double deadline;
    static PurpleWaveAudioSmoke() { EditorApplication.update += Tick; }
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    static T Get<T>(object o, string name) => (T)o.GetType().GetField(name, F).GetValue(o);
    static void Set(object o, string name, object value) => o.GetType().GetField(name, F).SetValue(o, value);
    static void Call(object o, string name) => o.GetType().GetMethod(name, F).Invoke(o, null);
    static IEnumerator Wait(float seconds) { float end = Time.time + seconds; while (Time.time < end) yield return null; }
    static PurpleEnergyWaveAudio[] Sounds() => Object.FindObjectsOfType<PurpleEnergyWaveAudio>();
    static PurpleEnergyWaveAudio Release() => Sounds().Single(s => s.name == "Purple Wave Release Audio");
    static PurpleEnergyWaveAudio Burst() => Sounds().Single(s => s.name == "Purple Wave Burst Audio");

    public static void Run()
    {
        Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "isolated project only");
        PlayerSettings.productName = "purple-wave-audio-smoke";
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        foreach (string name in new[] { "PurpleWaveRelease", "PurpleWaveBurst" })
        {
            var importer = (AudioImporter)AssetImporter.GetAtPath("Assets/Resources/Audio/" + name + ".wav");
            Check(importer != null && importer.preloadAudioData && !importer.loadInBackground, name + " preloaded");
            Check(importer.defaultSampleSettings.loadType == AudioClipLoadType.DecompressOnLoad &&
                importer.defaultSampleSettings.compressionFormat == AudioCompressionFormat.PCM && importer.forceToMono,
                name + " uncompressed mono import");
        }
        SessionState.SetBool(nameof(PurpleWaveAudioSmoke), true); EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return; frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(PurpleWaveAudioSmoke), false))
            {
                SessionState.SetBool(nameof(PurpleWaveAudioSmoke), false);
                deadline = EditorApplication.timeSinceStartup + 90; steps.Push(Verify());
            }
            if (steps.Count == 0) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("timeout");
            while (steps.Count > 0)
            {
                var step = steps.Peek(); if (!step.MoveNext()) { steps.Pop(); continue; }
                if (step.Current is IEnumerator nested) { steps.Push(nested); continue; } return;
            }
            Debug.Log("PURPLE_WAVE_AUDIO_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { steps.Clear(); Time.timeScale = 1; AudioListener.pause = false; Debug.LogException(e); EditorApplication.Exit(1); }
    }

    static DirectionalPurpleWave Cast(ZeldaFourWayMover mover)
    {
        mover.CancelAttackForStun();
        Check(mover.TryPerformAttack(Vector2.right), "native attack launches");
        var wave = Object.FindObjectOfType<DirectionalPurpleWave>();
        Check(wave != null && Sounds().Length == 1, "exactly one release source at attack start");
        return wave;
    }

    static IEnumerator Verify()
    {
        Time.timeScale = 1; AudioListener.pause = false;
        new GameObject("Audio test listener").AddComponent<AudioListener>();
        var camera = new GameObject("Audio test camera").AddComponent<Camera>(); camera.tag = "MainCamera";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Level2NPCS/Idle/OuterRealmVisitor-level2-Idle.prefab");
        var root = Object.Instantiate(prefab); var data = root.GetComponent<OuterRealmVisitorZeldaCharacterData>();
        var mover = root.GetComponent<ZeldaFourWayMover>(); root.GetComponent<ZeldaCharacterAiBase>().enabled = false;
        yield return null;
        var clips = new[] { Resources.Load<AudioClip>("Audio/PurpleWaveRelease"), Resources.Load<AudioClip>("Audio/PurpleWaveBurst") };
        for (int i = 0; i < clips.Length; i++)
        {
            var clip = clips[i]; Check(clip != null && clip.frequency == 44100 && clip.channels == 1, "mono PCM " + i);
            Check(Mathf.Abs(clip.length - (i == 0 ? .51f : .48f)) < .0001f, "separate phase length " + i);
            var samples = new float[clip.samples]; Check(clip.GetData(samples, 0), "readable PCM " + i);
            Check(Mathf.Abs(samples[0]) < .0001f && Mathf.Abs(samples[samples.Length - 1]) < .0001f &&
                samples.All(v => !float.IsNaN(v) && Mathf.Abs(v) <= .761f), "no clipping/click endpoints " + i);
        }
        var wave = Cast(mover); var release = Release(); var voice = release.GetComponent<AudioSource>();
        Check(voice.clip == clips[0] && voice.isPlaying && !voice.loop && !voice.playOnAwake, "release immediately plays once");
        Check(release.transform.parent == wave.transform && Vector3.Distance(release.transform.position, data.GetAttackHandPosition(Vector2.right)) < .001f,
            "release owned by attack, at the actual hand");
        Check(Mathf.Abs(voice.volume - PurpleEnergyWaveAudio.ReleaseVolume) < .001f, "near release level");
        var seen = new HashSet<PurpleEnergyWaveAudio>(); float started = Time.time;
        bool observedBurst = false, tailSurvived = false;
        while (Time.time - started < 1.15f)
        {
            foreach (var sound in Sounds())
            {
                seen.Add(sound);
                if (sound.name != "Purple Wave Burst Audio") continue;
                if (!observedBurst)
                {
                    observedBurst = true;
                    Check(Time.time - started >= .49f && wave != null && Get<bool>(wave, "burstStarted"), "burst tied to visual terminal event, not early");
                    Check(Vector3.Distance(sound.transform.position, Get<Vector3>(wave, "terminal")) < .001f, "burst localized at far end");
                    var s = sound.GetComponent<AudioSource>();
                    Check(s.clip == clips[1] && s.isPlaying && s.spatialBlend == 0 && s.dopplerLevel == 0, "distinct terminal clip plays");
                    Check(sound.GetComponent<CameraVisionStreamingExempt>() != null, "short tail streaming exempt");
                }
                if (wave == null && sound.GetComponent<AudioSource>().isPlaying) tailSurvived = true;
            }
            Check(Sounds().Length <= 2, "no frame-by-frame repeated sound emitters");
            yield return null;
        }
        Check(observedBurst && tailSurvived && seen.Count == 2 && seen.All(s => s == null), "two cues only, tail survives visual, all sources clean up");
        Check(data.CurrentHealth == 666 && data.AttackPower == 10 && data.AttackSize == new Vector2(1.25f, 3), "no gameplay data changes");

        // Pause and slow motion preserve the release phase, including sample cursor.
        wave = Cast(mover); release = Release(); voice = release.GetComponent<AudioSource>();
        Time.timeScale = 0; Call(release, "RenderAudio");
        Check(!voice.isPlaying, "time scale zero pauses release");
        float pausedAge = Get<float>(release, "age"); int sample = voice.timeSamples;
        for (int i = 0; i < 5; i++) yield return null;
        Check(Get<float>(release, "age") == pausedAge && voice.timeSamples == sample && Sounds().Length == 1, "pause does not advance or start burst");
        Time.timeScale = .5f; Call(release, "RenderAudio");
        Check(voice.isPlaying && Mathf.Abs(voice.pitch - .5f) < .001f, "slow-motion release speed matches visual");
        AudioListener.pause = true; Call(release, "RenderAudio"); Check(!voice.isPlaying, "listener pause respected");
        AudioListener.pause = false; Time.timeScale = 1; Call(release, "RenderAudio"); Check(voice.isPlaying, "resumes same voice");

        camera.transform.position = new Vector3(10, 0, -10); Call(release, "RenderAudio");
        Check(voice.volume > 0 && voice.volume < PurpleEnergyWaveAudio.ReleaseVolume && voice.panStereo < 0, "planar distance falloff and pan");
        camera.transform.position = new Vector3(30, 0, -10); Call(release, "RenderAudio"); Check(voice.volume == 0, "silent beyond range");
        var other = SceneManager.CreateScene("Purple audio other scene"); SceneManager.SetActiveScene(other);
        camera.transform.position = new Vector3(0, 0, -10); Call(release, "RenderAudio"); Check(voice.volume == 0, "no sound in unrelated scene");
        SceneManager.SetActiveScene(root.scene);
        // Death cancels the actual attack before the burst event, not just its sound.
        data.TakeDamage(999); yield return Wait(.12f);
        Check(wave == null && release == null, "early death cancels attack and owned release");
        yield return Wait(.6f); Check(Sounds().Length == 0, "no delayed burst after cancelled attack");

        // Burst audio is scene-owned, so unloading also removes its independent tail.
        PurpleEnergyWaveAudio.PlayBurst(other, Vector3.zero); var tail = Burst();
        Check(tail.gameObject.scene == other && tail.transform.parent == null, "burst belongs to origin scene");
        var unload = SceneManager.UnloadSceneAsync(other); while (!unload.isDone) yield return null;
        Check(tail == null && Sounds().Length == 0, "scene unload leaves no sound emitter");
        Object.Destroy(root); Object.Destroy(camera.gameObject);
    }
}
