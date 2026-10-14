using System;
using System.Collections;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class SandwormAudioSmoke
{
    static IEnumerator run;
    static int frame = -1, checks;
    static SandwormAudioSmoke() { EditorApplication.update += Tick; }
    public static void Run() { SessionState.SetBool("SandwormAudioSmoke", true); EditorApplication.EnterPlaymode(); }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool("SandwormAudioSmoke", false))
            { SessionState.SetBool("SandwormAudioSmoke", false); run = Verify(); }
            if (run == null || run.MoveNext()) return;
            Debug.Log("SANDWORM_AUDIO_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void Check(bool b, string text) { checks++; if (!b) throw new Exception("Sandworm audio: " + text); }
    static int Count(string name) => Object.FindObjectsOfType<SandwormAudioTail>().Count(t => t.name == "Sandworm Audio " + name);
    static GameObject Worm()
    {
        UnityEngine.Random.InitState(7491);
        var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Level2NPCS/Sandworm-BossPrototype.prefab"));
        root.GetComponent<SandwormBossPrototype>().ResetPose(); return root;
    }
    static void Step(GameObject root, float dt = .037f)
    {
        root.GetComponent<SandwormBossPrototype>().Advance(dt);
        root.GetComponent<SandwormEruptionAttack>().AdvanceAttack(dt);
        root.GetComponent<SandwormAudio>().AdvanceAudio(dt);
    }
    static IEnumerator Verify()
    {
        Time.timeScale = 1;
        string[] names = { "MovementLoop", "EmergenceLoop", "BurrowLoop", "Breach", "BurrowImpact", "BlastFall", "GroundImpact", "DeathSegment", "DeathHead" };
        foreach (string name in names)
        {
            var clip = Resources.Load<AudioClip>("Audio/Sandworm/" + name);
            Check(clip != null && clip.channels == 1 && clip.frequency == 44100, "mono PCM resource " + name);
            var importer = (AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip));
            Check(importer.defaultSampleSettings.compressionFormat == AudioCompressionFormat.PCM && importer.preloadAudioData, "preloaded PCM " + name);
            var samples = new float[clip.samples]; Check(clip.GetData(samples, 0), "read samples " + name);
            Check(samples.Take(132).Any(v => Mathf.Abs(v) > .01f), "onset within 3ms " + name);
            Check(samples.Max(v => Mathf.Abs(v)) < .98f, "no clipping " + name);
        }
        var camera = new GameObject("Audio camera", typeof(Camera)); camera.tag = "MainCamera";
        camera.transform.position = new Vector3(1000, 1000, -10);
        var player = new GameObject("Audio player", typeof(CivilianZeldaCharacterData), typeof(ZeldaFourWayMover));
        player.GetComponent<Collider2D>().enabled = false;
        Check(ZeldaRuntimeRegistry.GetControlledMover().gameObject == player, "real controlled mover is listener");
        var root = Worm(); var move = root.GetComponent<SandwormBossPrototype>(); var sound = root.GetComponent<SandwormAudio>();
        Check(sound != null && root.GetComponents<AudioSource>().Length == 3, "prefab has three bounded loops");
        // Player distance, not Camera Z, prefab origin or head-only distance.
        player.transform.position = move.Tail.position; move.Head.position += Vector3.right * .01f; sound.AdvanceAudio(.2f);
        float near = sound.MovementGain; Check(near > .27f, "near tail has full rumble even with camera far away");
        player.transform.position = move.Tail.position + Vector3.up * 20; move.Head.position += Vector3.right * .01f; sound.AdvanceAudio(.2f);
        float middle = sound.MovementGain; Check(middle > 0 && middle < near, "movement attenuates with player distance");
        player.transform.position = new Vector3(1000, 1000); move.Head.position += Vector3.right * .01f; sound.AdvanceAudio(.2f);
        Check(sound.MovementGain == 0, "outside audible range is silent");
        player.transform.position = move.Head.position; move.Head.position += Vector3.right * .01f; sound.AdvanceAudio(.2f);
        Check(sound.MovementGain > 0, "resume near player");
        Time.timeScale = 0; sound.AdvanceAudio(.2f);
        Check(root.GetComponents<AudioSource>().All(s => !s.isPlaying), "paused loops do not play");
        Time.timeScale = 1; sound.AdvanceAudio(.2f); sound.enabled = false;
        Check(root.GetComponents<AudioSource>().All(s => !s.isPlaying && s.volume == 0), "disable stops all loops");
        Object.DestroyImmediate(root);
        // Same-frame deterministic attack events; do not yield until the full cycle has been sampled.
        root = Worm(); sound = root.GetComponent<SandwormAudio>(); var attack = root.GetComponent<SandwormEruptionAttack>();
        Check(attack.TryLockAttack(new Vector2(8, 6)), "normal attack planned");
        bool emerge = false, airborne = false, burial = false, ended = false;
        for (int i = 0; i < 6000; i++)
        {
            player.transform.position = attack.LandingPulses > 0 ? attack.LandingPoint : attack.LockedAttackPoint;
            Step(root);
            if (sound.EmergenceGain > 0) emerge = true;
            if (attack.EmergencePulses == 8 && attack.LandingPulses == 0 && attack.Phase == SandwormEruptionAttack.AttackPhase.Traversing)
            { airborne = true; Check(sound.MovementGain < .0001f, "airborne has no underground rumble"); }
            if (sound.BurrowGain > 0) burial = true;
            if (attack.LandingPulses == 8 && attack.Phase != SandwormEruptionAttack.AttackPhase.Traversing) { ended = true; break; }
        }
        Check(emerge && airborne && burial && ended, "full normal cycle with phase loops");
        Check(Count("Breach") == 1 && Count("BurrowImpact") == 1, "exactly one burst at each endpoint, not per body segment");
        Object.DestroyImmediate(root);
        foreach (var tail in Object.FindObjectsOfType<SandwormAudioTail>()) Object.DestroyImmediate(tail.gameObject);
        root = Worm(); sound = root.GetComponent<SandwormAudio>(); attack = root.GetComponent<SandwormEruptionAttack>();
        Check(attack.TryLockAttack(new Vector2(8, 6)), "swallow attack planned");
        var bomb = Object.Instantiate(Resources.Load<GameObject>("PickupItems/Bombs/BombPickupItem")); bomb.transform.position = attack.LockedAttackPoint;
        bool fallen = false;
        for (int i = 0; i < 6000; i++)
        {
            player.transform.position = attack.Phase == SandwormEruptionAttack.AttackPhase.Falling ? attack.FallGroundPoint : attack.LockedAttackPoint;
            Step(root);
            if (attack.Phase == SandwormEruptionAttack.AttackPhase.Stunned) { fallen = true; break; }
        }
        Check(fallen && Count("BlastFall") == 1 && Count("GroundImpact") == 1, "internal blast and touchdown each trigger once");
        sound.AdvanceAudio(.2f); Check(sound.MovementGain == 0 && sound.EmergenceGain == 0 && sound.BurrowGain == 0, "stunned is silent");
        Object.DestroyImmediate(root);
        Check(Count("BlastFall") == 1, "detached sound survives owner destruction");
        Time.timeScale = 0;
        yield return null; yield return null;
        Check(Object.FindObjectsOfType<SandwormAudioTail>().All(t => !t.GetComponent<AudioSource>().isPlaying), "one-shots pause");
        Time.timeScale = 1; Time.captureFramerate = 60;
        for (int i = 0; i < 70; i++) yield return null;
        Check(Object.FindObjectsOfType<SandwormAudioTail>().Length == 0, "one-shots expire after resume");
        // Death playback must survive destruction of the live SandwormAudio component.
        root = Worm(); move = root.GetComponent<SandwormBossPrototype>();
        player.transform.position = move.Head.position;
        int segmentCount = move.BodySegments.Length + 1;
        Vector3 tailPosition = move.Tail.GetComponent<SpriteRenderer>().bounds.center;
        Vector3 headPosition = move.Head.GetComponent<SpriteRenderer>().bounds.center;
        string ordinaryDeathName = root.name + " Death Sound";
        root.GetComponent<SandwormZeldaCharacterData>().TakeDamage(10000);
        var sequence = Object.FindObjectOfType<SandwormDeathSequence>();
        Check(sequence != null && !root.activeInHierarchy, "real lethal hit starts inert death sequence");
        Check(Count("DeathSegment") == 1 && Count("DeathHead") == 0, "tail plays immediately with first visual");
        Check(Vector3.Distance(Object.FindObjectsOfType<SandwormAudioTail>().First(t => t.name.EndsWith("DeathSegment")).transform.position, tailPosition) < .001f, "tail uses actual part position");
        Check(GameObject.Find(ordinaryDeathName) == null, "no duplicate ordinary character death sound");
        Object.DestroyImmediate(root);
        Time.timeScale = 0; sequence.Advance(0);
        Check(Count("DeathSegment") == 1, "paused sequence does not add bursts");
        Time.timeScale = 1;
        for (int i = 1; i < segmentCount; i++)
        {
            sequence.Advance(.14001f);
            Check(Count("DeathSegment") == i + 1 && Count("DeathHead") == 0, "one sound per body explosion " + i);
        }
        sequence.Advance(.14001f);
        Check(Count("DeathSegment") == segmentCount && Count("DeathHead") == 1, "head plays only unique finale");
        var headSound = Object.FindObjectsOfType<SandwormAudioTail>().First(t => t.name.EndsWith("DeathHead"));
        Check(Vector3.Distance(headSound.transform.position, headPosition) < .001f, "head sound is spatially aligned");
        Check(headSound.GetComponent<AudioSource>().clip.name == "DeathHead", "head uses different clip");
        Object.DestroyImmediate(sequence.gameObject);
        Check(Count("DeathHead") == 1, "finale survives visual sequence cleanup");
        for (int i = 0; i < 55; i++) yield return null;
        Check(Object.FindObjectsOfType<SandwormAudioTail>().Length == 0, "death voices clean up");
        Time.captureFramerate = 0; Object.DestroyImmediate(player); Object.DestroyImmediate(camera);
    }
}
