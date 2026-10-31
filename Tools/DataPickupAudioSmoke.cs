using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class DataPickupAudioSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic;
    static IEnumerator suite;
    static int frame = -1, checks;
    static double deadline;
    static DataPickupAudioSmoke() { EditorApplication.update += Tick; }
    static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/")) throw new Exception("Isolated test only");
        PlayerSettings.productName = "data-pickup-audio-smoke";
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var importer = (AudioImporter)AssetImporter.GetAtPath("Assets/Resources/Audio/DataPickupCrystal.wav");
        Check(importer.preloadAudioData && !importer.loadInBackground &&
            importer.defaultSampleSettings.compressionFormat == AudioCompressionFormat.PCM, "preloaded PCM audio");
        SessionState.SetBool(nameof(DataPickupAudioSmoke), true);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(DataPickupAudioSmoke), false))
            { SessionState.SetBool(nameof(DataPickupAudioSmoke), false); suite = Verify(); deadline = EditorApplication.timeSinceStartup + 90; }
            if (suite == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Pickup audio test timed out");
            if (suite.MoveNext()) return;
            suite = null; Debug.Log("DATA_PICKUP_RUNTIME_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static GrowthCollectible Spawn()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/GrowthCollectible.prefab");
        return Object.Instantiate(prefab).GetComponent<GrowthCollectible>();
    }
    static void Refresh(GrowthCollectibleAudio sound) => typeof(GrowthCollectibleAudio).GetMethod("Update", F).Invoke(sound, null);
    static IEnumerator Verify()
    {
        while (Time.frameCount < 3 || RetroSceneLoadReveal.IsPreparingGameplay) yield return null;
        Time.timeScale = 1; AudioListener.pause = false;
        new GameObject("Test listener").AddComponent<AudioListener>();
        var growth = PlayerGrowthAttributes.Instance ?? new GameObject("Test growth").AddComponent<PlayerGrowthAttributes>();
        var clip = Resources.Load<AudioClip>("Audio/DataPickupCrystal");
        Check(clip != null && clip.channels == 1 && clip.frequency == 44100 && Mathf.Abs(clip.length - .64f) < .001f, "crisp short mono asset loaded");
        float[] samples = new float[clip.samples];
        Check(clip.GetData(samples, 0) && samples[0] == 0 && samples.Last() == 0 && samples.All(v => Mathf.Abs(v) <= .701f), "clean endpoints with headroom");
        var item = Spawn();
        Check(Object.FindObjectsOfType<GrowthCollectibleAudio>().Length == 0, "spawn and preload are silent");
        var ghost = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Ghost.prefab"));
        var ghostMover = ghost.GetComponent<ZeldaFourWayMover>();
        ghostMover.enabled = true; ZeldaRuntimeRegistry.ClaimControlledMover(ghostMover);
        int before = growth.CollectibleCount;
        Check(!item.TryCollect() && growth.CollectibleCount == before && Object.FindObjectsOfType<GrowthCollectibleAudio>().Length == 0,
            "rejected ghost pickup has no sound or award");
        ghostMover.enabled = false; Object.Destroy(ghost);
        var actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Level0NPCs/Prisoner.prefab"));
        actor.GetComponent<ZeldaCharacterAiBase>().enabled = false;
        var mover = actor.GetComponent<ZeldaFourWayMover>(); mover.enabled = true; ZeldaRuntimeRegistry.ClaimControlledMover(mover);
        Check(item.TryCollect() && growth.CollectibleCount == before + 1, "successful pickup awards once");
        var sound = Object.FindObjectOfType<GrowthCollectibleAudio>();
        var source = sound.GetComponent<AudioSource>();
        Check(source.clip == clip && source.isPlaying && Mathf.Approximately(source.volume, .65f) &&
            source.spatialBlend == 0 && !source.loop && !source.playOnAwake && !source.ignoreListenerPause, "old prefab uses default pickup sound and safe gain");
        Check(!item.TryCollect() && growth.CollectibleCount == before + 1 && Object.FindObjectsOfType<GrowthCollectibleAudio>().Length == 1,
            "duplicate interaction never repeats sound or reward");
        Check(sound.GetComponent<CameraVisionStreamingExempt>() != null && sound.transform.parent == null, "sound survives pickup destruction and distance unloading");
        yield return null;
        Check(item == null && source != null && source.isPlaying, "pickup removal does not cut off tail");
        Time.timeScale = 0; Refresh(sound);
        Check(!source.isPlaying, "pause freezes sound");
        int pausedSample = source.timeSamples;
        float lifetime = (float)typeof(GrowthCollectibleAudio).GetField("remaining", F).GetValue(sound);
        for (int i = 0; i < 4; i++) yield return null;
        Check(source.timeSamples == pausedSample && (float)typeof(GrowthCollectibleAudio).GetField("remaining", F).GetValue(sound) == lifetime,
            "pause retains sample position and cleanup time");
        Time.timeScale = 1; Refresh(sound); Check(source.isPlaying, "resume continues tail");
        AudioListener.pause = true; Refresh(sound); Check(!source.isPlaying, "listener pause respected");
        AudioListener.pause = false; Refresh(sound); Check(source.isPlaying, "listener resume works");
        double end = EditorApplication.timeSinceStartup + 3;
        while (sound != null && EditorApplication.timeSinceStartup < end) yield return null;
        Check(sound == null && source == null, "temporary voice cleaned after playback");
        var silent = Spawn(); typeof(GrowthCollectible).GetField("pickupSoundVolume", F).SetValue(silent, 0f);
        Check(silent.TryCollect() && Object.FindObjectsOfType<GrowthCollectibleAudio>().Length == 0, "editable zero volume mutes without preventing pickup");
        growth.ApplyRuntimeState(growth.CaptureRuntimeState());
        Check(Object.FindObjectsOfType<GrowthCollectibleAudio>().Length == 0, "restoring growth state does not replay pickup audio");
        var other = SceneManager.CreateScene("Pickup audio lifetime test");
        GrowthCollectibleAudio.Play(other, Vector3.zero, null, .65f);
        sound = Object.FindObjectOfType<GrowthCollectibleAudio>();
        Check(sound != null && sound.gameObject.scene == other, "voice is owned by originating scene");
        var unloading = SceneManager.UnloadSceneAsync(other);
        while (!unloading.isDone) yield return null;
        Check(sound == null, "scene unload removes transient audio");
        Object.Destroy(actor); yield return null;
    }
}
