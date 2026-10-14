using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Isolated Unity play-mode audio integration plus requested three-skill cost regression.
[InitializeOnLoad]
public static class HolyLightAudioSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static IEnumerator suite; static int checks, frame = -1; static double deadline;
    static HolyLightAudioSmoke() { EditorApplication.update += Tick; }
    public static void Run()
    {
        Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "isolated project only");
        PlayerSettings.productName = "holy-light-audio-smoke";
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        foreach (string name in new[] { "HolyLightCentral", "HolyLightWave" })
        {
            var importer = (AudioImporter)AssetImporter.GetAtPath("Assets/Resources/Audio/" + name + ".wav");
            Check(importer != null && importer.preloadAudioData && !importer.loadInBackground, name + " preload");
            Check(importer.defaultSampleSettings.loadType == AudioClipLoadType.DecompressOnLoad &&
                importer.defaultSampleSettings.compressionFormat == AudioCompressionFormat.PCM, name + " PCM import");
        }
        SessionState.SetBool(nameof(HolyLightAudioSmoke), true); EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return; frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(HolyLightAudioSmoke), false))
            { SessionState.SetBool(nameof(HolyLightAudioSmoke), false); suite = Verify(); deadline = EditorApplication.timeSinceStartup + 90; }
            if (suite == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timeout");
            if (suite.MoveNext()) return;
            suite = null; Debug.Log("HOLY_LIGHT_AUDIO_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void Check(bool v, string m) { checks++; if (!v) throw new Exception(m); }
    static FieldInfo Field(object o, string n)
    {
        for (Type t = o.GetType(); t != null; t = t.BaseType) { var f = t.GetField(n, F); if (f != null) return f; }
        throw new Exception("Missing field " + n);
    }
    static T Get<T>(object o, string n) => (T)Field(o, n).GetValue(o);
    static void Set(object o, string n, object v) => Field(o, n).SetValue(o, v);
    static object Call(object o, string n, params object[] args)
    {
        for (Type t = o.GetType(); t != null; t = t.BaseType)
        { var m = t.GetMethod(n, F); if (m != null) return m.Invoke(o, args); }
        throw new Exception("Missing method " + n);
    }
    static void Energy(ZeldaCharacterData owner, int value)
    {
        var s = owner.CaptureRuntimeState(); s.possessionEnergy = value;
        s.spentBasePossessionEnergy = s.spentGrowthPossessionEnergy = s.crystalEnergyThirds = 0;
        owner.ApplyRuntimeState(s); owner.GetComponent<ZeldaFourWayMover>().EndItemAttack();
    }
    static HolyLightSkillRuntime Cast(ZeldaCharacterData owner, bool manual)
    {
        Energy(owner, 4); Check(owner.TryUseCharacterSkill(), "native cast");
        var effect = Object.FindObjectsOfType<HolyLightSkillRuntime>().Single();
        Check(effect.GetComponents<AudioSource>().Length == 1 && effect.GetComponent<AudioSource>().isPlaying,
            "cast immediately plays one central voice");
        if (manual) effect.enabled = false;
        return effect;
    }
    static void To(HolyLightSkillRuntime effect, float at) => Call(effect, "Advance", at - Get<float>(effect, "elapsed"));
    static IEnumerator Verify()
    {
        Time.timeScale = 1; AudioListener.pause = false; while (DocumentReader.IsInputBlocked) yield return null;
        var scene = SceneManager.CreateScene("Holy Light Audio"); SceneManager.SetActiveScene(scene);
        new GameObject("Audio test listener").AddComponent<AudioListener>();
        var growth = PlayerGrowthAttributes.GetOrCreate(); growth.ApplyRuntimeState(default);
        VerifyCosts(growth); growth.ApplyRuntimeState(default);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Level2NPCS/Idle/AdvancedPriest-level2-Idle.prefab");
        var root = Object.Instantiate(prefab); var owner = root.GetComponent<ZeldaCharacterData>();
        var mover = root.GetComponent<ZeldaFourWayMover>();
        mover.ReceiveControl(Vector2.down); ZeldaRuntimeRegistry.ClaimControlledMover(mover);
        var central = Resources.Load<AudioClip>("Audio/HolyLightCentral");
        var wave = Resources.Load<AudioClip>("Audio/HolyLightWave");
        Check(central && wave && central.channels == 1 && wave.channels == 1 && central.frequency == 44100 && wave.frequency == 44100, "mono 44100 assets");
        Check(Mathf.Abs(central.length - (HolyLightSkillRuntime.DescentDuration + HolyLightSkillRuntime.CentralDuration)) < .0001f &&
            Mathf.Abs(wave.length - (HolyLightSkillRuntime.DescentDuration + HolyLightSkillRuntime.SmallDuration)) < .0001f, "cue lengths align with descent and glow duration");
        foreach (var clip in new[] { central, wave })
        {
            var samples = new float[clip.samples]; Check(clip.GetData(samples, 0), "PCM readable");
            Check(Mathf.Abs(samples[0]) < .0001f && Mathf.Abs(samples[samples.Length - 1]) < .0001f &&
                samples.All(v => !float.IsNaN(v) && Mathf.Abs(v) <= .761f), "bounded wave with click-free endpoints");
        }
        var effect = Cast(owner, true); var audio = Get<HolyLightSkillAudio>(effect, "skillAudio");
        Check(!effect.GetComponent<AudioSource>().isPlaying, "disabling skill stops owned sound");
        To(effect, .06f); var first = effect.GetComponent<AudioSource>();
        Check(first.clip == central && first.isPlaying && Mathf.Abs(first.time - .06f) < .04f, "resume at current descent, not clip start");
        To(effect, .17f); Check(effect.GetComponents<AudioSource>().Length == 1, "landing does not duplicate central cue");
        Time.timeScale = 0; To(effect, .17f);
        Check(!first.isPlaying, "time scale zero pauses sound");
        int sample = first.timeSamples;
        for (int i = 0; i < 3; i++) { yield return null; To(effect, .17f); }
        Check(first.timeSamples == sample, "paused sample cursor stable");
        Time.timeScale = .5f; To(effect, .18f);
        Check(first.isPlaying && first.pitch == .5f, "slow motion resumes and matches skill speed");
        Time.timeScale = 1; AudioListener.pause = true; To(effect, .18f);
        Check(!first.isPlaying, "listener pause respected");
        AudioListener.pause = false; To(effect, .19f); Check(first.isPlaying, "listener resume works");
        root.transform.position = Vector3.right * 12; To(effect, .2f);
        Check(first.volume > 0 && first.volume < HolyLightSkillAudio.CentralVolume && first.panStereo < 0, "2D distance attenuation and direction");
        root.transform.position = Vector3.right * 60; To(effect, .21f); Check(first.volume == 0, "far away silent");
        root.transform.position = Vector3.zero; To(effect, 2.16f);
        Check(Get<int>(effect, "roundsIssued") == 1 && effect.GetComponents<AudioSource>().Length == 2 &&
            !first.isPlaying && effect.GetComponents<AudioSource>()[1].isPlaying, "first four columns share one wave voice");
        for (int r = 1; r <= 3; r++)
        {
            To(effect, 2.16f + (r - 1) * .36f + .17f);
            var voices = effect.GetComponents<AudioSource>();
            Check(voices.Length == r + 1 && voices.Skip(1).All(s => s.clip == wave && s.volume <= HolyLightSkillAudio.WaveVolume &&
                !s.loop && !s.playOnAwake), "one bounded source per wave " + r);
            Check(voices.Count(s => s.isPlaying) <= 3, "at most three overlapping wave cues");
        }
        int voiceCount = effect.GetComponents<AudioSource>().Length;
        for (int i = 0; i < 20; i++) To(effect, 3.06f);
        Check(effect.GetComponents<AudioSource>().Length == voiceCount, "repeated frames allocate no additional audio sources");
        To(effect, 3.7f); Check(effect.GetComponents<AudioSource>().All(s => !s.isPlaying), "audio ends before final visual fade cleanup");
        Object.DestroyImmediate(effect.gameObject);
        var g = growth.CaptureRuntimeState(); g.unlockedSkills = new[] { "holy_light", "holy_light_2", "holy_light_3" }; growth.ApplyRuntimeState(g);
        effect = Cast(owner, true);
        To(effect, 3.41f);
        Check(Get<int>(effect, "roundsIssued") == 4 && effect.GetComponents<AudioSource>().Count(s => s.isPlaying) == 2,
            "fourth upgraded wave plays; skipped expired waves are not replayed after hitch");
        var otherScene = SceneManager.CreateScene("Audio destination");
        SceneManager.MoveGameObjectToScene(root, otherScene); To(effect, 3.42f);
        Check(Get<bool>(effect, "cancelled") && effect.GetComponents<AudioSource>().All(s => !s.isPlaying), "scene travel stops old audio");
        Object.DestroyImmediate(effect.gameObject); SceneManager.MoveGameObjectToScene(root, scene);
        effect = Cast(owner, true); Set(owner, "isDead", true); To(effect, .1f);
        Check(effect.GetComponents<AudioSource>().All(s => !s.isPlaying), "death cancels sound and pending waves");
        Set(owner, "isDead", false); Object.DestroyImmediate(effect.gameObject);
        // Production Update: upgraded cast completes its four rounds and disposes every voice.
        effect = Cast(owner, false); var seen = new System.Collections.Generic.HashSet<AudioSource>();
        float started = Time.time; int maximum = 0;
        while (effect && Time.time - started < 6)
        {
            foreach (var s in effect.GetComponents<AudioSource>()) seen.Add(s);
            maximum = Mathf.Max(maximum, effect.GetComponents<AudioSource>().Count(s => s.isPlaying));
            yield return null;
        }
        Check(!effect && seen.Count == 5 && seen.All(s => s == null) && maximum <= 3, "live full upgraded cast cleans all five owned voices with bounded overlap");
        Check(owner.FinalPossessionEnergy == 0, "audio never changes skill energy");
        Energy(owner, 0); Check(!owner.TryUseCharacterSkill() && Object.FindObjectsOfType<HolyLightSkillRuntime>().Length == 0,
            "failed casts do not produce orphan sounds");
    }
    static void VerifyCosts(PlayerGrowthAttributes growth)
    {
        var root = new GameObject("Cost UI test", typeof(RectTransform), typeof(Canvas));
        var page = root.AddComponent<CharacterSkillPage>(); page.Build(Resources.GetBuiltinResource<Font>("Arial.ttf"));
        foreach (string id in new[] { "builder_2", "holy_light_2", "holy_light_3", "holy_shield_2", "holy_shield_3", "craftsmanship_1" })
        {
            var def = Array.Find(PlayerGrowthAttributes.Skills, s => s.id == id);
            int expected = id == "craftsmanship_1" ? 4 : 2;
            Check(def != null && def.cost == expected, "configured upgrade cost " + id);
            var s = new PlayerGrowthAttributes.RuntimeState {
                collectibleCount = expected - 1, unlockedSkills = new[] { def.parentId },
                discoveredCharacterSkills = new[] { "builder", "holy_light", "holy_shield" }
            };
            growth.ApplyRuntimeState(s); Set(page, "selectedId", id); Call(page, "Refresh");
            Check(Get<Text>(page, "upgradeText").text == "升级（消耗" + expected + "）" &&
                !Get<Button>(page, "upgradeButton").interactable, "UI displays exact cost and rejects insufficient points " + id);
            Check(!growth.TryUpgradeSkill(id) && growth.CollectibleCount == expected - 1, "no partial deduction on failed upgrade " + id);
            growth.AddCollectibles(1); Call(page, "Refresh");
            Check(Get<Button>(page, "upgradeButton").interactable, "exact budget enables upgrade " + id);
            Check(growth.TryUpgradeSkill(id) && growth.CollectibleCount == 0, "upgrade deducts exactly required points " + id);
            if (id == "craftsmanship_1") Check(growth.SkillValue == 1, "four-point node still grants one permanent skill value");
        }
        foreach (string id in new[] { "builder", "holy_light", "holy_shield" })
            Check(Array.Find(PlayerGrowthAttributes.Skills, s => s.id == id).cost == 4, "initial learn cost unchanged " + id);
        Object.DestroyImmediate(root);
    }
}
