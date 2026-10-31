using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Run only in a disposable project. Reads all authored scenes without saving them;
// tiny derived fixtures exercise streaming playback and the real black-screen transition.
[InitializeOnLoad]
public static class LevelBackgroundMusicSmoke
{
    const string FixtureFolder = "Assets/Editor/MusicSmokeFixtures";
    const string Prefab = "Assets/Prefabs/Level Background Music.prefab";
    const string DesertPrefab = "Assets/Prefabs/Desert Encounter Audio.prefab";
    static readonly string[] Clips = { "EerieMausoleum", "RoyalEntrance", "PyramidLevel" };
    static readonly float[] Volumes = { .08f, .11f, .085f };
    static readonly float[] Durations = { 100f, 100.69932f, 112.5f };
    static readonly Stack<IEnumerator> routines = new Stack<IEnumerator>();
    static int checks, frame = -1;
    static double deadline;
    static LevelBackgroundMusicSmoke() { EditorApplication.update += Tick; }
    static void Check(bool value, string label)
    { checks++; if (!value) throw new Exception(label); }
    static string ClipPath(int i) => "Assets/BGMS/Visager/" + Clips[i] + "-Loop.mp3";
    static string Fixture(int i) => FixtureFolder + "/MusicSmoke" + i + ".unity";
    static LoopingBackgroundMusicPlayer[] Players(Scene s) => s.GetRootGameObjects()
        .SelectMany(o => o.GetComponentsInChildren<LoopingBackgroundMusicPlayer>(true)).ToArray();

    public static void Run()
    {
        try
        {
            Check(Application.dataPath.Replace('\\','/').Contains("/.codex-temp/"), "isolated project only");
            PlayerSettings.productName = "level-music-smoke";
            for (int i = 0; i < 3; i++)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPath(i));
                var importer = (AudioImporter)AssetImporter.GetAtPath(ClipPath(i));
                Check(clip != null && clip.channels == 2, "stereo clip " + Clips[i]);
                Check(Mathf.Abs(clip.length - Durations[i]) < .08f, "full loop duration " + clip.length);
                Check(importer.defaultSampleSettings.loadType == AudioClipLoadType.Streaming, "streaming import");
                Check(!importer.preloadAudioData && importer.loadInBackground, "no eager full-song decode");
                Check(importer.defaultSampleSettings.compressionFormat == AudioCompressionFormat.Vorbis, "Vorbis build import");
            }
            var paths = new[] { "Assets/Scenes/Level0.unity" }
                .Concat(Directory.GetFiles("Assets/Scenes/Level1", "*.unity"))
                .Concat(Directory.GetFiles("Assets/Scenes/Level2", "*.unity")).ToArray();
            Check(paths.Length == 10, "all ten Level0/1/2 scenes audited");
            foreach (var path in paths)
            {
                var scene = EditorSceneManager.OpenScene(path);
                var players = Players(scene);
                if (scene.name == "Level2-Desert")
                {
                    Check(players.Length == 0, "desert has no generic autoplay music player");
                    var desert = scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<DesertEncounterAudio>(true)).Single();
                    Check(desert.enabled && desert.gameObject.activeInHierarchy, "desert encounter service is active");
                    var missingCheck = typeof(SceneTravelStateManager).GetMethod("IsMissingStateRelevant", BindingFlags.NonPublic | BindingFlags.Static);
                    Check(!(bool)missingCheck.Invoke(null, new object[] { desert.gameObject }), "legacy saves retain new desert service");
                    Check(desert.GetComponentsInChildren<Renderer>(true).Length == 0 && desert.GetComponentsInChildren<Collider2D>(true).Length == 0,
                        "desert service cannot become a spatial streamed prop");
                    continue;
                }
                Check(players.Length == 1, scene.name + " exactly one player");
                int index = scene.name == "Level0" ? 0 : scene.name.StartsWith("Level1-") ? 1 : 2;
                var p = players[0]; var serialized = new SerializedObject(p);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPath(index));
                Check(p.gameObject.activeInHierarchy && p.enabled, "player active " + scene.name);
                Check(p.MusicClip == clip, "assigned track " + scene.name);
                Check(Mathf.Approximately(serialized.FindProperty("volume").floatValue, Volumes[index]), "quiet gain " + scene.name);
                Check(serialized.FindProperty("loop").boolValue && serialized.FindProperty("playOnSceneStart").boolValue, "autoplay loop");
                Check(p.GetComponent<AudioSource>().clip == clip, "no stale prefab AudioSource reference");
                Check(Mathf.Approximately(p.GetComponent<AudioSource>().volume, Volumes[index]), "source gain also quiet");
                Check(p.GetComponentsInChildren<Renderer>(true).Length == 0 && p.GetComponentsInChildren<Collider2D>(true).Length == 0,
                    "nonspatial service cannot become a distance-streamed scene prop");
                var missing = typeof(SceneTravelStateManager).GetMethod("IsMissingStateRelevant", BindingFlags.NonPublic | BindingFlags.Static);
                Check(!(bool)missing.Invoke(null, new object[] { p.gameObject }), "pre-music saves will not remove added player");
                Debug.Log("MUSIC_SCENE_OK " + scene.name + " " + clip.name + " volume=" + Volumes[index]);
            }
            var title = EditorSceneManager.OpenScene("Assets/Scenes/TitleScreen.unity");
            Check(Players(title).Single().MusicClip.name == "MusMus-BGM-157", "title music unchanged");
            Check(File.ReadAllText("Assets/StreamingAssets/Visager-Music-Credits.txt").Contains("CC BY 4.0"), "credits ship automatically");
            Directory.CreateDirectory(FixtureFolder);
            for (int i = 0; i < 4; i++)
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var camera = new GameObject("Music Test Camera", typeof(Camera), typeof(AudioListener));
                camera.tag = "MainCamera"; camera.transform.position = new Vector3(0,0,-10);
                if (i < 3)
                {
                    var obj = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab));
                    var s = new SerializedObject(obj.GetComponent<LoopingBackgroundMusicPlayer>());
                    s.FindProperty("musicClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPath(i));
                    s.FindProperty("volume").floatValue = Volumes[i]; s.ApplyModifiedPropertiesWithoutUndo();
                    obj.GetComponent<AudioSource>().clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPath(i));
                    obj.GetComponent<AudioSource>().volume = Volumes[i];
                }
                else PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(DesertPrefab));
                EditorSceneManager.SaveScene(scene, Fixture(i));
            }
            EditorBuildSettings.scenes = Enumerable.Range(0,4).Select(i => new EditorBuildSettingsScene(Fixture(i), true)).ToArray();
            EditorSceneManager.OpenScene(Fixture(0));
            SessionState.SetInt("LevelMusicChecks",checks);
            SessionState.SetBool(nameof(LevelBackgroundMusicSmoke),true);
            EditorApplication.EnterPlaymode();
        }
        catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(LevelBackgroundMusicSmoke),false))
            {
                SessionState.SetBool(nameof(LevelBackgroundMusicSmoke),false);
                checks = SessionState.GetInt("LevelMusicChecks",0);
                deadline = EditorApplication.timeSinceStartup + 100;
                routines.Push(Verify());
            }
            if (routines.Count == 0) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("music test timeout");
            while (routines.Count > 0)
            {
                var r = routines.Peek(); if (!r.MoveNext()) { routines.Pop(); continue; }
                if (r.Current is IEnumerator nested) { routines.Push(nested); continue; }
                return;
            }
            Debug.Log("LEVEL_BACKGROUND_MUSIC_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch(Exception e) { routines.Clear(); Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static IEnumerator Until(Func<bool> ready, string label)
    {
        double end = EditorApplication.timeSinceStartup + 15;
        while(!ready()) { if(EditorApplication.timeSinceStartup > end) throw new Exception(label); yield return null; }
        Check(true,label);
    }
    static IEnumerator Delay(float seconds)
    {
        float until = Time.realtimeSinceStartup + seconds;
        while(Time.realtimeSinceStartup < until) yield return null;
    }
    static IEnumerator Transition(int i)
    {
        var previous = Object.FindObjectsOfType<LoopingBackgroundMusicPlayer>(true);
        Check(RetroSceneLoadReveal.BeginTransition("MusicSmoke" + i, () => RetroSceneLoadReveal.LoadSceneAsync("MusicSmoke" + i)), "real transition accepted");
        foreach(var p in previous) Check(!p.AudioSource.isPlaying, "outgoing music stops at first black band");
        yield return Until(() => SceneManager.GetActiveScene().name == "MusicSmoke" + i && !RetroSceneLoadReveal.IsBlockingInput, "scene transition completes");
        foreach(var p in previous) Check(p == null, "old scene player destroyed, not persistent");
        var all = Object.FindObjectsOfType<LoopingBackgroundMusicPlayer>(true);
        Check(all.Length == (i == 3 ? 0 : 1), "one player only / silence in desert fixture");
        if (i < 3) Check(all[0].MusicClip.name == Clips[i]+"-Loop", "destination track correct");
        Check(Object.FindObjectsOfType<DesertEncounterAudio>(true).Length == (i == 3 ? 1 : 0), "desert service remains scene local");
    }
    static IEnumerator Verify()
    {
        yield return Until(() => !RetroSceneLoadReveal.IsBlockingInput, "initial reveal completes");
        for(int i = 0; i < 3; i++)
        {
            if(i > 0) yield return Transition(i);
            var p = Object.FindObjectOfType<LoopingBackgroundMusicPlayer>();
            yield return Until(() => p.AudioSource != null && p.AudioSource.isPlaying, "stream starts " + Clips[i]);
            var source = p.AudioSource;
            Check(source.loop && source.spatialBlend == 0 && source.dopplerLevel == 0, "2D looping source");
            Check(Mathf.Approximately(source.volume,Volumes[i]), "runtime quiet gain");
            int sample = source.timeSamples;
            p.transform.position = new Vector3(10000,10000);
            yield return Delay(.4f);
            Check(source.isPlaying && source.timeSamples != sample, "stream advances away from player");
            source.time = source.clip.length - .15f;
            yield return Delay(.65f);
            Check(source.isPlaying && source.time < 2f, "loop wraps without stopping");
            p.Stop(); Check(!source.isPlaying, "explicit stop");
            p.Play(); yield return Until(() => source.isPlaying, "explicit restart");
            // Streaming seek/play cursor updates are asynchronous on the audio thread.
            yield return Delay(.5f);
            Check(source.time < 2f, "restart resets cursor");
            Debug.Log("MUSIC_PLAYBACK_OK " + Clips[i]);
        }
        yield return Transition(3); // Town -> desert wind only until first actual sight.
        yield return VerifyDesert();
        yield return Transition(2); // Desert -> town starts the intended music again.
        yield return Transition(2); // Same-scene reload / save-load cannot duplicate it.
    }

    static IEnumerator VerifyDesert()
    {
        var mix = Object.FindObjectOfType<DesertEncounterAudio>();
        yield return Delay(1f);
        Check(mix.WindSource.isPlaying && !mix.BossSource.isPlaying, "exploration wind only");
        Check(Mathf.Approximately(mix.WindSource.volume,.12f), "quiet exploration gain");
        Check(mix.WindSource.clip.name == "SandstormLooping" && mix.BossSource.clip.name == "MinibossFight-Loop", "selected assets wired");
        foreach(var source in new[] {mix.WindSource, mix.BossSource})
        {
            Check(source.loop && source.spatialBlend == 0 && source.dopplerLevel == 0, "desert 2D looping source");
            var importer = (AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(source.clip));
            Check(importer.defaultSampleSettings.loadType == AudioClipLoadType.Streaming && !importer.preloadAudioData && importer.loadInBackground,
                "long desert tracks stream rather than eagerly decode");
        }
        Check(Mathf.Abs(mix.WindSource.clip.length - 403.46f) < .15f && Mathf.Abs(mix.BossSource.clip.length - 179f) < .15f, "full-length clips imported");
        mix.WindSource.time = mix.WindSource.clip.length - .15f;
        yield return Delay(.65f);
        Check(mix.WindSource.isPlaying && mix.WindSource.time < 2f, "wind loop wraps");

        var camera = Camera.main;
        camera.orthographic = true; camera.orthographicSize = 12f;
        camera.transform.position = new Vector3(1000,1000,-30);
        var actor = new GameObject("Desert music controlled actor"); actor.SetActive(false);
        actor.AddComponent<CivilianZeldaCharacterData>(); var mover = actor.AddComponent<ZeldaFourWayMover>();
        actor.SetActive(true); ZeldaRuntimeRegistry.ClaimControlledMover(mover);
        var wormObject = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Level2NPCS/Sandworm-Boss.prefab"));
        var worm = wormObject.GetComponent<SandwormZeldaCharacterData>();
        wormObject.GetComponent<SandwormEruptionAttack>().enabled = false;
        var movement = wormObject.GetComponent<SandwormBossPrototype>();
        movement.ResetPose(); movement.SetMovementEnabled(false);
        var ui = new GameObject("Desert music HUD",typeof(RectTransform),typeof(UnityEngine.Canvas));
        var status = new GameObject("Area status",typeof(RectTransform)); status.transform.SetParent(ui.transform,false);
        var barObject = new GameObject("Boss bar",typeof(RectTransform),typeof(RuntimeSandwormBossBar));
        barObject.transform.SetParent(ui.transform,false);
        var bar = barObject.GetComponent<RuntimeSandwormBossBar>();
        bar.Build((RectTransform)status.transform, AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/ZLabsPixel_12px_M_CN.ttf"));
        bar.enabled = false; // Advance explicitly so camera/restore assertions are deterministic.
        bar.Advance(0);
        Check(!bar.IsEncounterVisible && !mix.IsInEncounter, "no encounter merely because worm exists");
        camera.transform.position = movement.Head.position + Vector3.back * 30f;
        mover.transform.position = movement.Head.position;
        // Exact existing save-restore guard; no encounter before saved health is applied.
        var manager = SceneTravelStateManager.GetOrCreate();
        var restoring = typeof(SceneTravelStateManager).GetField("sceneTravelRestoreInProgress",BindingFlags.Instance|BindingFlags.NonPublic);
        restoring.SetValue(manager,true);
        bar.Advance(0);
        Check(!bar.IsEncounterVisible && !mix.IsInEncounter, "restore defers encounter");
        restoring.SetValue(manager,false);
        bar.Advance(0);
        Check(bar.IsEncounterVisible && mix.IsInEncounter, "real HUD discovery starts music encounter");
        yield return Delay(.4f);
        Check(mix.BossSource.isPlaying && mix.BossSource.volume > 0 && mix.BossSource.volume < .095f, "boss fades in smoothly");
        yield return Delay(1.3f);
        Check(Mathf.Approximately(mix.BossSource.volume,.095f) && Mathf.Approximately(mix.WindSource.volume,.035f), "final battle gain ducks wind");
        camera.transform.position = new Vector3(1000,1000,-30);
        wormObject.SetActive(false); bar.Advance(0);
        float cursor = mix.BossSource.time;
        yield return Delay(.5f);
        Check(mix.IsInEncounter && mix.BossSource.isPlaying && mix.BossSource.time > cursor, "offscreen and streaming disable retain uninterrupted music");
        wormObject.SetActive(true); movement.SetMovementEnabled(false);
        mix.BossSource.time = mix.BossSource.clip.length - .15f;
        yield return Delay(.65f);
        Check(mix.BossSource.isPlaying && mix.BossSource.time < 2f, "boss loop wraps");
        var saved = worm.CaptureRuntimeState(); saved.currentHealth = 0;
        worm.ApplyRuntimeState(saved); bar.Advance(0);
        yield return Delay(.3f);
        Check(!mix.IsInEncounter && mix.BossSource.volume > 0 && mix.BossSource.volume < .095f, "dead/restored-dead boss fades out");
        yield return Delay(1.4f);
        Check(!mix.BossSource.isPlaying && Mathf.Approximately(mix.WindSource.volume,.12f), "defeat returns to wind");
        Object.Destroy(wormObject); Object.Destroy(actor); Object.Destroy(ui);

        var beforeReload = mix;
        yield return Transition(3);
        Check(beforeReload == null, "same-scene save-load style reload destroys old audio service");
        mix = Object.FindObjectOfType<DesertEncounterAudio>();
        yield return Delay(1f);
        Check(mix.WindSource.isPlaying && !mix.BossSource.isPlaying && !mix.IsInEncounter, "reloaded scene waits for actual encounter; no stale static boss state");
        mix.StopForSceneTransition();
        Check(!mix.WindSource.isPlaying && !mix.BossSource.isPlaying, "both loops stop on black transition");
        yield return Delay(.3f);
        Check(!mix.WindSource.isPlaying && !mix.BossSource.isPlaying, "Update cannot restart outgoing loops");
        Debug.Log("DESERT_ENCOUNTER_AUDIO_PASS");
    }
}
