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
public static class DataAbsorptionAudioSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic;
    static IEnumerator suite;
    static int frame = -1, checks;
    static double deadline;
    static DataAbsorptionAudioSmoke() { EditorApplication.update += Tick; }
    static object Get(object o, string name) => o.GetType().GetField(name, F).GetValue(o);
    static void Set(object o, string name, object value) => o.GetType().GetField(name, F).SetValue(o, value);
    static void Check(bool ok, string label) { checks++; if (!ok) throw new Exception(label); }
    static void Refresh(GrowthPickupFlightAudio sound) => typeof(GrowthPickupFlightAudio).GetMethod("Update", F).Invoke(sound, null);
    public static void Run()
    {
        Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "isolated project only");
        PlayerSettings.productName = "data-absorption-audio-smoke";
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        foreach (string name in new[] { "DataHomingLoop", "DataAbsorption" })
        {
            var importer = (AudioImporter)AssetImporter.GetAtPath("Assets/Resources/Audio/" + name + ".wav");
            Check(importer.preloadAudioData && !importer.loadInBackground &&
                importer.defaultSampleSettings.compressionFormat == AudioCompressionFormat.PCM, "preloaded PCM " + name);
        }
        SessionState.SetBool(nameof(DataAbsorptionAudioSmoke), true);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(DataAbsorptionAudioSmoke), false))
            { SessionState.SetBool(nameof(DataAbsorptionAudioSmoke), false); suite = Verify(); deadline = EditorApplication.timeSinceStartup + 70; }
            if (suite == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timeout");
            if (suite.MoveNext()) return;
            suite = null; Debug.Log("DATA_ABSORPTION_RUNTIME_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static GrowthCollectible Spawn()
        => Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/GrowthCollectible.prefab")).GetComponent<GrowthCollectible>();
    static IEnumerator Verify()
    {
        while (Time.frameCount < 3 || RetroSceneLoadReveal.IsPreparingGameplay) yield return null;
        Time.timeScale = 1; AudioListener.pause = false;
        new GameObject("Test listener").AddComponent<AudioListener>();
        var growth = PlayerGrowthAttributes.Instance ?? new GameObject("Test growth").AddComponent<PlayerGrowthAttributes>();
        var homing = Resources.Load<AudioClip>("Audio/DataHomingLoop");
        var absorption = Resources.Load<AudioClip>("Audio/DataAbsorption");
        Check(homing && absorption && Mathf.Abs(homing.length-.8f)<.001f && Mathf.Abs(absorption.length-.14f)<.001f,
            "both sound stages loaded");
        foreach (var clip in new[] { homing, absorption })
        {
            Check(clip.channels == 1 && clip.frequency == 44100, "mono native sample rate");
            var data = new float[clip.samples];
            Check(clip.GetData(data, 0) && data.All(v => Mathf.Abs(v) <= .401f), "no clipping");
        }
        var actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Level0NPCs/Prisoner.prefab"));
        actor.GetComponent<ZeldaCharacterAiBase>().enabled = false;
        actor.transform.position = new Vector3(1,0);
        var mover = actor.GetComponent<ZeldaFourWayMover>(); mover.ReceiveControl(Vector2.left);
        ZeldaRuntimeRegistry.ClaimControlledMover(mover);
        var item = Spawn(); Set(item,"pickupBurstParticleCount",64); Set(item,"pickupBurstLifetime",.4f);
        int before = growth.CollectibleCount;
        Check(Object.FindObjectsOfType<GrowthPickupFlightAudio>().Length == 0, "spawn/preload silent");
        Check(item.TryCollect() && growth.CollectibleCount == before+1, "currency awarded once immediately");
        Check(!item.TryCollect() && growth.CollectibleCount == before+1, "duplicate collection rejected");
        // A moving collector gives every randomized particle a real flight interval;
        // otherwise a scatter endpoint can already overlap the arrival threshold.
        actor.transform.position = new Vector3(3.5f, 0);
        var effect = Object.FindObjectOfType<GrowthCollectiblePickupEffect>();
        Check(effect != null && Object.FindObjectOfType<GrowthPickupFlightAudio>() == null, "scatter phase keeps original crack only");
        double start = EditorApplication.timeSinceStartup;
        GrowthPickupFlightAudio sound = null;
        while ((sound = Object.FindObjectOfType<GrowthPickupFlightAudio>()) == null && EditorApplication.timeSinceStartup-start<3)
            yield return null;
        Check(sound != null && (float)Get(effect,"elapsed") >= .4f, "flight sound starts at real homing boundary");
        var sources = sound.GetComponents<AudioSource>();
        Check(sources.Length == 5 && sources.All(s=>s.spatialBlend==0 && !s.playOnAwake && !s.ignoreListenerPause), "one homing voice and four pooled absorption voices");
        var loop = sources.Single(s=>s.loop); var arrivals = sources.Where(s=>!s.loop).ToArray();
        Check(loop.clip==homing && arrivals.All(s=>s.clip==absorption), "correct default clips on old prefab");
        float wait = Time.time+.1f; while(Time.time<wait) yield return null;
        Debug.Log("DATA_AUDIO_FLIGHT playing="+loop.isPlaying+" volume="+loop.volume+
            " cues="+Get(sound,"arrivalCueCount")+" elapsed="+Get(effect,"elapsed"));
        Check(loop.isPlaying && loop.volume>0 && arrivals.All(s=>!s.isPlaying), "quiet homing bed precedes absorption");
        Time.timeScale=0; Refresh(sound);
        float age=(float)Get(sound,"audibleAge"), effectAge=(float)Get(effect,"elapsed");
        int sample=loop.timeSamples;
        for(int i=0;i<4;i++) yield return null;
        Check(!loop.isPlaying && loop.timeSamples==sample && (float)Get(sound,"audibleAge")==age &&
            (float)Get(effect,"elapsed")==effectAge, "pause freezes sound and flight together");
        Time.timeScale=1; Refresh(sound); Check(loop.isPlaying,"loop resumes without restart");
        AudioListener.pause=true; Refresh(sound); Check(!loop.isPlaying,"listener pause respected");
        AudioListener.pause=false; Refresh(sound); Check(loop.isPlaying,"listener resume respected");
        start=EditorApplication.timeSinceStartup;
        while(sound!=null && (int)Get(sound,"arrivalCueCount")==0 && EditorApplication.timeSinceStartup-start<3) yield return null;
        Check(sound!=null && arrivals.Any(s=>s.isPlaying) && (int)Get(sound,"arrivalCueCount")==1,"actual particle arrival triggers chime");
        Check(effect.GetComponentsInChildren<ParticleSystem>().Single(p=>p.name=="Arrival Flashes").particleCount>0,
            "chime coincides with arrival flash");
        Time.timeScale=0; Refresh(sound);
        int cueCount=(int)Get(sound,"arrivalCueCount"), pending=(int)Get(sound,"pendingArrivals");
        var positions=arrivals.Select(s=>s.timeSamples).ToArray();
        for(int i=0;i<4;i++) yield return null;
        Check(arrivals.All(s=>!s.isPlaying) && positions.SequenceEqual(arrivals.Select(s=>s.timeSamples)) &&
            (int)Get(sound,"arrivalCueCount")==cueCount && (int)Get(sound,"pendingArrivals")==pending,
            "pause freezes all polyphonic voices and the pending queue");
        Time.timeScale=1; Refresh(sound);
        int maxCues=0, peakVoices=0;
        start=EditorApplication.timeSinceStartup;
        while((sound!=null || effect!=null) && EditorApplication.timeSinceStartup-start<4)
        {
            if(sound!=null) maxCues=Mathf.Max(maxCues,(int)Get(sound,"arrivalCueCount"));
            peakVoices=Mathf.Max(peakVoices,arrivals.Count(s=>s!=null && s.isPlaying));
            yield return null;
        }
        Debug.Log("DATA_ABSORPTION_BURST cues="+maxCues+" concurrentVoices="+peakVoices);
        Check(sound==null && effect==null && maxCues>=3 && maxCues<=64 && peakVoices<=4,
            "64 arrivals produce a rapid bounded stream, not just two chimes, and clean up");
        Check(growth.CollectibleCount==before+1,"arrival audio never awards again");

        // Losing the target must not produce a fake success sound.
        item=Spawn(); Set(item,"pickupBurstLifetime",.1f); Check(item.TryCollect(),"second pickup");
        start=EditorApplication.timeSinceStartup;
        while((sound=Object.FindObjectOfType<GrowthPickupFlightAudio>())==null && EditorApplication.timeSinceStartup-start<3) yield return null;
        Check(sound!=null,"second homing begins");
        effect=Object.FindObjectOfType<GrowthCollectiblePickupEffect>();
        Object.Destroy(effect.gameObject); yield return null;
        Check(sound==null || (int)Get(sound,"arrivalCueCount")==0,"cancellation does not synthesize arrival");
        start=EditorApplication.timeSinceStartup;
        while(sound!=null && EditorApplication.timeSinceStartup-start<1) yield return null;
        Check(sound==null,"cancelled loop fades and cleans up");

        // Trailing audio belongs to the scene and may outlive its short visual.
        var other=SceneManager.CreateScene("DATA audio scene test");
        var owner=new GameObject("Mock flight").AddComponent<GrowthCollectiblePickupEffect>();
        SceneManager.MoveGameObjectToScene(owner.gameObject,other);
        sound=GrowthPickupFlightAudio.Create(owner,null,null,.38f,.55f);
        Check(sound!=null && sound.gameObject.scene==other && sound.transform.parent==null,"scene-owned audio not parented to disposable particles");
        sound.ReportFrame(false,64,0,Vector3.zero);
        Check((int)Get(sound,"arrivalCueCount")==1 && (int)Get(sound,"pendingArrivals")<=8,"same-frame batch starts immediately with a bounded queue");
        wait=Time.time+.12f; while(Time.time<wait) yield return null;
        Check((int)Get(sound,"arrivalCueCount")>=2 && (int)Get(sound,"arrivalCueCount")<=3,
            "simultaneous particles spread into a short cluster rather than one chime");
        Check((int)Get(sound,"pendingArrivals")==0,"batch never leaves a long delayed sound queue");
        Object.Destroy(owner.gameObject); yield return null;
        Check(sound!=null && sound.GetComponents<AudioSource>().Any(s=>!s.loop && s.isPlaying),"short visual cleanup preserves chime tail");
        var operation=SceneManager.UnloadSceneAsync(other); while(!operation.isDone) yield return null;
        Check(sound==null,"scene unload removes residual sound");

        // A custom long chime must not bypass the hard voice cap through PlayOneShot.
        owner=new GameObject("Long clip mock").AddComponent<GrowthCollectiblePickupEffect>();
        sound=GrowthPickupFlightAudio.Create(owner,null,homing,0,.55f);
        for(int i=0;i<12;i++)
        {
            Set(sound,"audibleAge",i*.041f);
            sound.ReportFrame(true,10,100,Vector3.zero);
        }
        Check((int)Get(sound,"arrivalCueCount")==4 && sound.GetComponents<AudioSource>().Count(s=>!s.loop && s.isPlaying)==4,
            "long custom clips cannot exceed four overlapping arrival voices");
        sound.Finish();
        Check((int)Get(sound,"pendingArrivals")==0,"cancellation discards pending arrivals");
        Object.Destroy(sound.gameObject); Object.Destroy(owner.gameObject); yield return null;

        item=Spawn(); Set(item,"homingSoundVolume",0f); Set(item,"absorptionSoundVolume",0f); Set(item,"pickupBurstLifetime",.1f);
        Check(item.TryCollect(),"independent mute does not prevent pickup");
        wait=Time.time+1.5f; while(Time.time<wait) yield return null;
        Check(Object.FindObjectsOfType<GrowthPickupFlightAudio>().Length==0,"editable zero volumes allocate no audio source");
        growth.ApplyRuntimeState(growth.CaptureRuntimeState());
        Check(Object.FindObjectsOfType<GrowthPickupFlightAudio>().Length==0,"save restore never replays absorption");
        Object.Destroy(actor); yield return null;
    }
}
