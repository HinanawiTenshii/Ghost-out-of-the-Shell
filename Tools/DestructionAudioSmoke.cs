using System;
using UnityEditor;
using UnityEngine;

public static class DestructionAudioSmoke
{
    public static void Run()
    {
        try
        {
            Verify("DoorBreak", .52f, "3f22ed120148407796e6087c9db90849");
            Verify("GlassBreak", .64f, "e29d6d35122645fdaca034b6e11b8966");
            var glass = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Decorations/Glass.prefab");
            Require(glass != null, "Glass prefab present in smoke project");
            var data = glass.GetComponentInChildren<DoorData>(true);
            Require(data != null, "Glass DoorData component");
            var config = new SerializedObject(data);
            Require(config.FindProperty("breakMaterial").enumValueIndex == 1, "Glass material");
            Require(config.FindProperty("breakSound").objectReferenceValue == Resources.Load<AudioClip>("Audio/GlassBreak"), "Glass existing clip binding");
            Debug.Log("DESTRUCTION_AUDIO_PASS: Unity PCM import, GUIDs, Resources, start/end samples and prefab binding.");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    static void Verify(string name, float duration, string guid)
    {
        var path = "Assets/Resources/Audio/" + name + ".wav";
        var importer = (AudioImporter)AssetImporter.GetAtPath(path);
        Require(importer.preloadAudioData && !importer.loadInBackground, name + " synchronous preload");
        Require(importer.defaultSampleSettings.compressionFormat == AudioCompressionFormat.PCM, name + " PCM");
        Require(AssetDatabase.AssetPathToGUID(path) == guid, name + " GUID");
        var clip = Resources.Load<AudioClip>("Audio/" + name);
        Require(clip != null && clip.channels == 1 && clip.frequency == 44100, name + " Unity format");
        Require(Mathf.Abs(clip.length - duration) < .001f, name + " duration");
        Require(clip.LoadAudioData(), name + " load");
        var samples = new float[clip.samples];
        Require(clip.GetData(samples, 0), name + " decode");
        int first = Array.FindIndex(samples, x => Mathf.Abs(x) > .01f);
        Require(first >= 0 && first < clip.frequency * .003f, name + " instant onset");
        Require(samples[0] == 0 && samples[samples.Length - 1] == 0, name + " clean endpoints");
    }
}
