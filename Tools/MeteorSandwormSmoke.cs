using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Run only in the isolated Unity smoke project; exercises actual meteor landing dispatch.</summary>
[InitializeOnLoad]
public static class MeteorSandwormSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static readonly List<GameObject> owned = new List<GameObject>();
    static IEnumerator suite;
    static int frame = -1, checks;
    static double deadline;
    static MeteorSandwormSmoke() { EditorApplication.update += Tick; }
    public static void Run()
    {
        Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "isolated project only");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(nameof(MeteorSandwormSmoke), true); EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return; frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(MeteorSandwormSmoke), false))
            {
                SessionState.SetBool(nameof(MeteorSandwormSmoke), false);
                suite = Verify(); deadline = EditorApplication.timeSinceStartup + 120;
            }
            if (suite == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timeout");
            if (suite.MoveNext()) return;
            suite = null; Debug.Log("METEOR_SANDWORM_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
    static GameObject Own(GameObject go) { owned.Add(go); return go; }
    static void Set(object o, string field, object value) => o.GetType().GetField(field, F).SetValue(o, value);
    static void Advance(MeteorShowerSkillRuntime cast, float delta) =>
        typeof(MeteorShowerSkillRuntime).GetMethod("Advance", F).Invoke(cast, new object[] { delta });
    static SandwormEruptionAttack Worm()
    {
        var go = Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            AssetDatabase.GUIDToAssetPath("38c4b68a021e4ab1be698c671de42362"))));
        go.transform.position = Vector3.zero; go.GetComponent<SandwormBossPrototype>().ResetPose();
        return go.GetComponent<SandwormEruptionAttack>();
    }
    static MeteorShowerSkillRuntime Meteor(ZeldaCharacterData owner, Vector2 point, bool fire = false)
    {
        var cast = Own(new GameObject("Meteor sandworm integration test")).AddComponent<MeteorShowerSkillRuntime>();
        cast.enabled = false; Set(cast, "owner", owner); Set(cast, "fire", fire);
        typeof(MeteorShowerSkillRuntime).GetMethod("Add", F).Invoke(cast, new object[] { point, false });
        Advance(cast, 0f); return cast;
    }
    static void Clear()
    {
        foreach (var go in owned) if (go != null) Object.DestroyImmediate(go);
        owned.Clear();
        foreach (var death in Object.FindObjectsOfType<SandwormDeathSequence>()) Object.DestroyImmediate(death.gameObject);
    }
    static IEnumerator Verify()
    {
        Time.timeScale = 1; while (DocumentReader.IsInputBlocked) yield return null;
        var scene = SceneManager.CreateScene("Meteor sandworm tests"); SceneManager.SetActiveScene(scene);
        Time.timeScale = 0;
        var casterObject = new GameObject("Impact caster"); casterObject.transform.position = new Vector2(100, 100);
        var caster = casterObject.AddComponent<CivilianZeldaCharacterData>();
        var worm = Worm(); var motion = worm.GetComponent<SandwormBossPrototype>();
        var data = worm.GetComponent<SandwormZeldaCharacterData>();
        int initialHealth = data.CurrentHealth;
        var tail = motion.Tail.GetComponent<SpriteRenderer>();
        Vector2 point = tail.bounds.center;
        Check(Vector2.Distance(point, motion.HeadPosition) > MeteorShowerSkillRuntime.Radius + 3,
            "tail-only fixture keeps head outside blast");
        Check(Vector2.Distance(point, worm.transform.position) > MeteorShowerSkillRuntime.Radius,
            "stationary root is outside blast");
        foreach (var part in worm.GetComponentsInChildren<SandwormSegmentVisual>())
            Check(!part.GetComponent<Collider2D>().enabled, "buried collision is disabled");
        var cast = Meteor(caster, point);
        Advance(cast, .79f); Check(!worm.IsRoarRecovery && data.CurrentHealth == initialHealth, "warning/fall do not eject or damage");
        Advance(cast, .02f);
        Check(worm.Phase == SandwormEruptionAttack.AttackPhase.RoarRising, "tail impact ejects whole worm");
        Check(data.CurrentHealth == initialHealth - 10, "one AP10 impact despite overlapping several sections");
        Check(worm.EmergencePulses == 0 && worm.LandingPulses == 0, "ejection adds no offensive pulses");
        Check(Object.FindObjectsOfType<SandwormEruptionSite>().Length == 3, "existing dust animation spawned");
        Advance(cast, .01f); Check(data.CurrentHealth == initialHealth - 10, "same meteor cannot hit twice");
        worm.AdvanceAttack(.6f);
        Check(worm.Phase == SandwormEruptionAttack.AttackPhase.RoarStunned && worm.StunRemaining > 4.9f,
            "landing begins full five-second stun");
        Check(!worm.GetComponent<SandwormStateController>().IsUnderground &&
            worm.GetComponentInChildren<ZeldaAiStateIndicator>() != null, "exposed with existing stun icon");
        float left = worm.StunRemaining;
        var second = Meteor(caster, point); Advance(second, .81f);
        Check(data.CurrentHealth == initialHealth - 20, "above-ground follow-up meteor uses normal AP10 once");
        Check(Mathf.Approximately(left, worm.StunRemaining), "follow-up does not restart stun/ejection");
        var saved = data.CaptureRuntimeState();
        Check(saved.characterSpecificState.Contains("\"state\":0"), "save normalizes meteor ejection to underground");
        worm.AdvanceAttack(left + .01f);
        Check(worm.Phase == SandwormEruptionAttack.AttackPhase.RoarBurrowing, "stun expires into harmless burial");
        worm.AdvanceAttack(.5f);
        Check(!worm.IsRoarRecovery && worm.GetComponent<SandwormStateController>().IsUnderground,
            "returns underground after stun");
        Vector2 head = motion.HeadPosition; motion.Advance(.05f); worm.AdvanceAttack(.05f);
        Check(Vector2.Distance(head, motion.HeadPosition) > .001f, "prepared roaming resumes");
        Clear(); yield return null;

        // An out-of-range landing misses; later fire ticks must not invoke buried impact handling.
        worm = Worm(); motion = worm.GetComponent<SandwormBossPrototype>(); data = worm.GetComponent<SandwormZeldaCharacterData>();
        point = (Vector2)motion.Tail.GetComponent<SpriteRenderer>().bounds.center + Vector2.down * 20;
        cast = Meteor(caster, point, true); Advance(cast, .81f);
        Check(!worm.IsRoarRecovery && data.CurrentHealth == initialHealth, "out-of-range meteor misses");
        foreach (var part in worm.GetComponentsInChildren<SandwormSegmentVisual>()) part.transform.position += Vector3.down * 20;
        Advance(cast, 1f);
        Check(!worm.IsRoarRecovery && data.CurrentHealth == initialHealth, "entering lingering fire underground does not eject");
        Clear(); yield return null;

        worm = Worm(); motion = worm.GetComponent<SandwormBossPrototype>(); data = worm.GetComponent<SandwormZeldaCharacterData>();
        point = motion.HeadPosition;
        var other = SceneManager.CreateScene("Unrelated worm scene"); SceneManager.MoveGameObjectToScene(worm.gameObject, other);
        cast = Meteor(caster, point); Advance(cast, .81f);
        Check(!worm.IsRoarRecovery && data.CurrentHealth == initialHealth, "other-scene worm is untouched");
        SceneManager.MoveGameObjectToScene(worm.gameObject, scene); worm.gameObject.SetActive(false);
        cast = Meteor(caster, point); Advance(cast, .81f);
        Check(data.CurrentHealth == initialHealth, "inactive worm is untouched");
        Clear(); yield return null;

        worm = Worm(); data = worm.GetComponent<SandwormZeldaCharacterData>(); UnityEngine.Random.InitState(7491);
        Check(worm.TryLockAttack(new Vector2(8, 6)), "approaching attack fixture");
        cast = Meteor(caster, worm.GetComponent<SandwormBossPrototype>().HeadPosition); Advance(cast, .81f);
        Check(worm.IsRoarRecovery && !worm.IsTargetLocked, "impact interrupts underground approach and target lock");
        foreach (var site in Object.FindObjectsOfType<SandwormEruptionSite>()) Check(!site.HasWarning, "stale attack warning removed");
        Clear(); yield return null;

        worm = Worm(); UnityEngine.Random.InitState(7491); Check(worm.TryLockAttack(new Vector2(8, 6)), "flight fixture");
        motion = worm.GetComponent<SandwormBossPrototype>();
        for (int i = 0; i < 3000 && worm.Phase != SandwormEruptionAttack.AttackPhase.Traversing; i++)
        { motion.Advance(.025f); worm.AdvanceAttack(.025f); }
        Check(worm.Phase == SandwormEruptionAttack.AttackPhase.Traversing, "normal flight reached");
        cast = Meteor(caster, motion.HeadPosition); Advance(cast, .81f);
        Check(worm.Phase == SandwormEruptionAttack.AttackPhase.Traversing, "airborne attack is not forcibly reset");
        Clear(); yield return null;

        // Damage can remove registry entries. All impacted worms still resolve once.
        var first = Worm(); var firstData = first.GetComponent<SandwormZeldaCharacterData>();
        worm = Worm(); data = worm.GetComponent<SandwormZeldaCharacterData>();
        var health = firstData.CaptureRuntimeState(); health.currentHealth = 5; firstData.ApplyRuntimeState(health);
        cast = Meteor(caster, worm.GetComponent<SandwormBossPrototype>().HeadPosition); Advance(cast, .81f);
        Check(firstData.IsDead && !first.IsRoarRecovery, "lethal meteor invokes death, not ejection");
        Check(data.CurrentHealth == initialHealth - 10 && worm.IsRoarRecovery, "survivor still hit after registry mutation");
        Check(Object.FindObjectOfType<SandwormDeathSequence>() != null, "normal sequential death effects retained");
        Clear(); Object.DestroyImmediate(casterObject); yield return null;
    }
}
