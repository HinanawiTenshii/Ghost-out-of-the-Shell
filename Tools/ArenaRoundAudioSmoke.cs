using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class ArenaRoundAudioSmoke
{
    const string Key = nameof(ArenaRoundAudioSmoke);
    static IEnumerator suite;
    static int frame = -1, checks;
    static double deadline;
    static ArenaRoundAudioSmoke() { EditorApplication.update += Tick; }
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\','/').Contains("/.codex-temp/")) throw new Exception("Isolated project required");
        PlayerSettings.productName = "arena-round-audio-smoke";
        EditorSceneManager.OpenScene("Assets/Scenes/Level2/Level2-Floor-1.unity");
        var arena = Object.FindObjectOfType<ArenaChallengeController>();
        Check(arena.roundEndSound != null && Mathf.Approximately(arena.roundEndSoundVolume, .55f), "authored arena audio binding and gain");
        var importer = (AudioImporter)AssetImporter.GetAtPath("Assets/Resources/Audio/ArenaRoundEndBell.wav");
        Check(importer.preloadAudioData && !importer.loadInBackground && importer.defaultSampleSettings.loadType == AudioClipLoadType.DecompressOnLoad &&
            importer.defaultSampleSettings.compressionFormat == AudioCompressionFormat.PCM, "preloaded PCM, no delayed streaming");
        SessionState.SetInt(Key + "checks", checks); SessionState.SetBool(Key, true);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(Key, false))
            {
                SessionState.SetBool(Key, false); checks = SessionState.GetInt(Key + "checks", 0);
                suite = Verify(); deadline = EditorApplication.timeSinceStartup + 90;
            }
            if (suite == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Audio test timed out");
            if (suite.MoveNext()) return;
            suite = null; Debug.Log("ARENA_ROUND_AUDIO_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception ex) { suite = null; Debug.LogException(ex); EditorApplication.Exit(1); }
    }
    static void Check(bool value, string message)
    { checks++; if (!value) throw new Exception("ARENA AUDIO ASSERT: " + message); Debug.Log("ARENA_AUDIO_CHECK " + message); }
    static void Refresh(ArenaChallengeController arena) => typeof(ArenaChallengeController)
        .GetMethod("RefreshRoundEndSoundPause", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(arena, null);
    static void Fight(ArenaChallengeController arena, ZeldaFourWayMover player)
    {
        player.CharacterData.RestoreHealth(999);
        foreach (var guard in arena.guardians) { guard.RestoreHealth(999); guard.GetComponent<ZeldaCharacterAiBase>().enabled = false; }
        arena.ApplyRuntimeState(new ArenaChallengeController.RuntimeState {
            phase = ArenaChallengeController.Phase.Fighting,
            entrantId = player.CharacterData.IndividualAccessId, roster = new[] { 0, 1, 2 }
        });
        arena.CompleteRestoration();
    }
    static IEnumerator Verify()
    {
        while (DocumentReader.IsInputBlocked || GameSaveSystem.IsPreparingSceneSave) yield return null;
        var arena = Object.FindObjectOfType<ArenaChallengeController>();
        arena.resultPause = 120; // Exercise result feedback without leaving this fixture.
        var actor = arena.residents.First(ai => ai.gameObject != arena.registrar.gameObject && !arena.guardians.Any(g => g.gameObject == ai.gameObject));
        CameraVisionObjectStreaming.RestoreForGameplay(actor.gameObject); actor.gameObject.SetActive(true);
        var old = ZeldaRuntimeRegistry.GetControlledMover(); if (old != null) old.enabled = false;
        var player = actor.GetComponent<ZeldaFourWayMover>(); player.ReceiveControl(Vector2.right); ZeldaRuntimeRegistry.ClaimControlledMover(player);
        player.transform.position = arena.fightingArea.center; player.GetComponent<Rigidbody2D>().position = arena.fightingArea.center;
        Check(arena.GetComponents<AudioSource>().Length == 0, "idle arena has no playing voice");
        var clip = arena.roundEndSound;
        Check(clip != null && clip.channels == 1 && clip.frequency == 44100 && Mathf.Abs(clip.length - .72f) < .0001f, "short mono double bell asset");
        var samples = new float[clip.samples]; Check(clip.GetData(samples, 0), "PCM samples accessible");
        Check(samples[0] == 0 && samples[samples.Length - 1] == 0 && samples.All(s => Mathf.Abs(s) <= .701f), "clean endpoints and peak headroom");
        Fight(arena, player);
        arena.Opponent.TakeDamage(999, player.CharacterData);
        var voice = arena.GetComponent<AudioSource>();
        Check(arena.CurrentPhase == ArenaChallengeController.Phase.Result && arena.Wins == 1 && voice != null && voice.isPlaying, "player victory starts round bell");
        Check(voice.clip == clip && !voice.loop && !voice.playOnAwake && voice.spatialBlend == 0f && voice.pitch == 1f &&
            Mathf.Approximately(voice.volume, .55f), "single two-strike cue at stable arena-wide volume");
        double end = EditorApplication.timeSinceStartup + .08;
        while (EditorApplication.timeSinceStartup < end) yield return null;
        Time.timeScale = 0; Refresh(arena);
        Check(!voice.isPlaying, "game pause pauses cue");
        int pausedSample = voice.timeSamples;
        for (int i = 0; i < 3; i++) yield return null;
        Check(voice.timeSamples == pausedSample, "pause preserves the gap between strikes");
        Time.timeScale = 1; Refresh(arena); Check(voice.isPlaying, "game resume continues cue");
        AudioListener.pause = true; Refresh(arena); Check(!voice.isPlaying, "listener pause respected");
        AudioListener.pause = false; Refresh(arena); Check(voice.isPlaying, "listener resume continues cue");
        int sampleBefore = voice.timeSamples;
        player.CharacterData.TakeDamage(999, arena.guardians[0]); arena.guardians[0].TakeDamage(999, player.CharacterData);
        Check(voice.timeSamples >= sampleBefore && arena.Wins == 1 && arena.GetComponents<AudioSource>().Length == 1, "repeated result hits never restart or duplicate bell");
        end = EditorApplication.timeSinceStartup + 2;
        while (voice.isPlaying && EditorApplication.timeSinceStartup < end) yield return null;
        Check(!voice.isPlaying, "double bell ends naturally without looping");
        Fight(arena, player);
        player.CharacterData.TakeDamage(999, arena.Opponent);
        Check(arena.CurrentPhase == ArenaChallengeController.Phase.Result && arena.Wins == 0 && voice.isPlaying, "opponent victory plays same bell");
        Check(arena.GetComponents<AudioSource>().Length == 1 && arena.GetComponent<AudioSource>() == voice, "rounds reuse one audio source");
        var result = arena.CaptureRuntimeState(); arena.ApplyRuntimeState(result); arena.CompleteRestoration();
        Check(!voice.isPlaying && arena.CurrentPhase == ArenaChallengeController.Phase.Result, "restoring result neither replays nor retains old cue");
        for (int i = 0; i < 3; i++) yield return null;
        Check(!voice.isPlaying, "restored result updates remain silent");
        Fight(arena, player); arena.Opponent.TakeDamage(999, player.CharacterData);
        arena.enabled = false; Check(!voice.isPlaying, "disabled controller stops cue");
        arena.enabled = true; Check(!voice.isPlaying, "reenabling does not replay result");
        arena.ApplyRuntimeState(new ArenaChallengeController.RuntimeState()); arena.CompleteRestoration();
        Check(!voice.isPlaying, "abandon or idle restoration stays silent");
        arena.roundEndSoundVolume = 0; Fight(arena, player); arena.Opponent.TakeDamage(999, player.CharacterData);
        Check(!voice.isPlaying, "editable zero volume disables round cue");
        arena.roundEndSoundVolume = .55f; Fight(arena, player); arena.Opponent.TakeDamage(999, player.CharacterData);
        Object.Destroy(arena.gameObject); yield return null;
        Check(voice == null, "owned sound cannot survive arena destruction or scene unload");
    }
}
