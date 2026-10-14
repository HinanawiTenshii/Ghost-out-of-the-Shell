using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class SandwormRoarSmoke
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    const string Pending = "SandwormRoarSmoke.Pending";
    static readonly List<GameObject> owned = new List<GameObject>();
    static IEnumerator test; static int frame = -1, checks;
    static SandwormRoarSmoke() { EditorApplication.update += Tick; }
    public static void Run() { SessionState.SetBool(Pending, true); EditorApplication.EnterPlaymode(); }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return; frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(Pending, false)) { SessionState.SetBool(Pending, false); test = Verify(); }
            if (test == null || test.MoveNext()) return;
            test = null; Cleanup(); Debug.Log("SANDWORM_ROAR_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { test = null; Cleanup(); Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void Check(bool value, string label) { checks++; if (!value) throw new Exception(label); }
    static GameObject Own(GameObject go) { owned.Add(go); return go; }
    static void Cleanup()
    {
        foreach (var go in owned) if (go != null) Object.DestroyImmediate(go); owned.Clear();
        foreach (var sequence in Object.FindObjectsOfType<SandwormDeathSequence>()) Object.DestroyImmediate(sequence.gameObject);
    }
    static void Set(object obj, string name, object value) => obj.GetType().GetField(name, Flags).SetValue(obj, value);
    static object Call(object obj, string name) => obj.GetType().GetMethod(name, Flags).Invoke(obj, null);
    static SandwormEruptionAttack Worm()
    {
        var go = Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Level2NPCS/Sandworm-BossPrototype.prefab")));
        go.transform.position = Vector3.zero; go.GetComponent<SandwormBossPrototype>().ResetPose();
        return go.GetComponent<SandwormEruptionAttack>();
    }
    static ZeldaCharacterData Actor(Vector2 position, bool ai = false)
    {
        var go = Own(new GameObject("Roar test actor")); go.SetActive(false); go.transform.position = position;
        var data = go.AddComponent<CivilianZeldaCharacterData>();
        if (ai)
        {
            go.AddComponent<BoxCollider2D>().size = Vector2.one * .45f;
            go.AddComponent<ZeldaCharacterAiBase>(); go.GetComponent<ZeldaFourWayMover>().enabled = false;
        }
        go.SetActive(true); return data;
    }
    static FearRoarArea Roar(Vector2 position, int level = 1)
    {
        var caster = Actor(position);
        var roar = Own(new GameObject("Test active roar")).AddComponent<FearRoarArea>();
        roar.Initialize(caster, level); return roar;
    }
    static Transform[] Parts(SandwormEruptionAttack attack)
    {
        var motion = attack.GetComponent<SandwormBossPrototype>();
        var parts = new List<Transform> { motion.Head }; parts.AddRange(motion.BodySegments); parts.Add(motion.Tail); return parts.ToArray();
    }
    static void Step(SandwormEruptionAttack attack, float dt)
    { attack.GetComponent<SandwormBossPrototype>().Advance(dt); attack.AdvanceAttack(dt); }
    static bool Overlaps(SandwormEruptionAttack attack, ZeldaCharacterData actor)
    {
        Physics2D.SyncTransforms();
        foreach (var part in Parts(attack))
        {
            var body = part.GetComponent<Collider2D>();
            if (body.enabled && body.Distance(actor.GetComponent<Collider2D>()).isOverlapped) return true;
        }
        return false;
    }
    static IEnumerator Preview()
    {
        var worm = Worm(); var parts = Parts(worm);
        Bounds bounds = parts[0].GetComponent<SpriteRenderer>().bounds;
        foreach (var part in parts) bounds.Encapsulate(part.GetComponent<SpriteRenderer>().bounds);
        var camera = Own(new GameObject("Roar ejection preview camera")).AddComponent<Camera>();
        camera.enabled = false; camera.orthographic = true;
        camera.transform.position = bounds.center + Vector3.up * .6f + Vector3.back * 30;
        camera.orthographicSize = Mathf.Max(5f, (bounds.extents.x + 3) / 4f);
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.035f, .055f, .09f);
        const int width = 1200, height = 300;
        var target = new RenderTexture(width, height, 24); camera.targetTexture = target;
        var image = new Texture2D(width, height * 4, TextureFormat.RGB24, false);
        int row = 0;
        for (int n = 0; n <= 46; n++)
        {
            if (n == 1) Roar(parts[0].position);
            if (n > 0) worm.AdvanceAttack(.02f);
            yield return null;
            if (n != 0 && n != 9 && n != 28 && n != 46) continue;
            camera.Render(); RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, width, height), 0, height * (3 - row++)); RenderTexture.active = null;
        }
        image.Apply(); int warm = 0, magenta = 0;
        foreach (var p in image.GetPixels32())
        {
            if (p.r > 120 && p.g > 75) warm++;
            if (p.r > 180 && p.b > 180 && p.g < 40) magenta++;
        }
        Check(warm > 1000 && magenta == 0, "actual GPU ejection and dust render without missing shaders");
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(Application.dataPath, "../Sandworm-roar-ejection-preview.png"), image.EncodeToPNG());
        camera.targetTexture = null; Object.DestroyImmediate(target); Object.DestroyImmediate(image);
        Cleanup(); yield return null;
    }
    static IEnumerator Verify()
    {
        Scene scene = SceneManager.CreateScene("Fear roar sandworm tests"); SceneManager.SetActiveScene(scene); Time.timeScale = 0;
        var worm = Worm(); var data = worm.GetComponent<SandwormZeldaCharacterData>(); var parts = Parts(worm);
        var tail = parts[parts.Length - 1].GetComponent<SpriteRenderer>();
        var roar = Roar((Vector2)tail.bounds.center + Vector2.down * (tail.bounds.extents.y + 4f));
        worm.AdvanceAttack(.01f); Check(!worm.IsRoarRecovery, "outside ongoing roar is not affected");
        Set(roar, "elapsed", .4f);
        foreach (var part in parts) part.position += Vector3.down * 1.25f;
        Check(Vector2.Distance(parts[0].position, roar.transform.position) > roar.Radius + 4, "head remains outside while tail enters");
        Check(Vector2.Distance(worm.transform.position, roar.transform.position) > roar.Radius + 4, "stationary root is not the detection anchor");
        var positions = new Vector3[parts.Length];
        for (int i = 0; i < parts.Length; i++) positions[i] = parts[i].position;
        int hp = data.CurrentHealth;
        worm.AdvanceAttack(.01f);
        Check(worm.Phase == SandwormEruptionAttack.AttackPhase.RoarRising, "entering an already ongoing roar via tail triggers ejection");
        Check(data.CurrentHealth == hp && worm.EmergencePulses == 0, "ejection adds no damage or offensive pulses");
        Check(Object.FindObjectsOfType<SandwormEruptionSite>().Length == 3, "three bounded dust sites, not one per part");
        foreach (var site in Object.FindObjectsOfType<SandwormEruptionSite>()) Check(!site.HasWarning && site.ActiveFragmentCount > 0, "ground opening/dust without attack warning");
        var paused = parts[0].position; worm.AdvanceAttack(0f); Check(parts[0].position == paused, "paused ejection stays still");
        float greatestLift = 0;
        for (int n = 0; n < 60 && worm.Phase == SandwormEruptionAttack.AttackPhase.RoarRising; n++)
        {
            Step(worm, .02f);
            greatestLift = Mathf.Max(greatestLift, parts[0].position.y - positions[0].y);
            for (int i = 1; i < parts.Length; i++)
                Check(Vector3.Distance(parts[i].position - parts[0].position, positions[i] - positions[0]) < .001f, "popping preserves connected curved pose");
        }
        Check(greatestLift > .5f, "visible upward launch before falling back");
        Check(worm.Phase == SandwormEruptionAttack.AttackPhase.RoarStunned && worm.StunRemaining > 4.97f, "full five-second stun starts after launch lands");
        Check(!data.GetComponent<SandwormStateController>().IsUnderground, "stunned worm is fully exposed");
        Check(data.GetComponentInChildren<ZeldaAiStateIndicator>() != null, "existing stun icon present");
        for (int i = 0; i < parts.Length; i++)
        {
            Check(Vector3.Distance(parts[i].position, positions[i]) < .001f, "stunned at exact original ground position");
            Check(parts[i].GetComponent<Collider2D>().enabled && !parts[i].GetComponent<SandwormSegmentVisual>().HasUndergroundOverlay, "exposed collision and unmasked appearance");
        }
        var trapped = Actor(parts[3].position, true); int actorHp = trapped.CurrentHealth;
        Check(Overlaps(worm, trapped), "clearance fixture starts overlapped"); Step(worm, .01f);
        Check(!Overlaps(worm, trapped) && trapped.CurrentHealth == actorHp, "emerged body does not trap nearby actors or deal damage");
        float left = worm.StunRemaining; Step(worm, left - .01f);
        Check(worm.Phase == SandwormEruptionAttack.AttackPhase.RoarStunned && worm.StunRemaining > 0, "stun is not shortened by repeated roar checks");
        for (int i = 0; i < parts.Length; i++) Check(Vector3.Distance(parts[i].position, positions[i]) < .001f, "five-second hold does not wander");
        Step(worm, .02f); Check(worm.Phase == SandwormEruptionAttack.AttackPhase.RoarBurrowing, "stun expires into harmless in-place burial");
        for (int n = 0; n < 60 && worm.IsRoarRecovery; n++) Step(worm, .01f);
        Check(!worm.IsRoarRecovery && data.GetComponent<SandwormStateController>().IsUnderground, "returns underground");
        var releasedPosition = parts[0].position; Step(worm, .03f);
        Check(Vector3.Distance(parts[0].position, releasedPosition) > .005f, "prepared route resumes immediately without another turn/pause");
        Check(!worm.IsRoarRecovery, "same ongoing roar cannot trigger twice");
        Check(data.CurrentHealth == hp && worm.LandingPulses == 0 && worm.SwallowedExplosiveCount == 0, "whole interruption is non-damaging");
        Cleanup(); yield return null;

        // An approaching attack is interrupted; no old warning survives.
        worm = Worm(); UnityEngine.Random.InitState(7491);
        Check(worm.TryLockAttack(new Vector2(8, 6)), "approach route fixture");
        roar = Roar(worm.GetComponent<SandwormBossPrototype>().HeadPosition);
        worm.AdvanceAttack(.02f);
        Check(worm.IsRoarRecovery && !worm.IsTargetLocked, "roar cancels pending attack lock");
        foreach (var site in Object.FindObjectsOfType<SandwormEruptionSite>()) Check(!site.HasWarning, "pending attack warning removed");
        var ground = (Vector3[])typeof(SandwormEruptionAttack).GetField("savedPositions", Flags).GetValue(worm);
        var saved = worm.GetComponent<SandwormZeldaCharacterData>().CaptureRuntimeState();
        Check(saved.characterSpecificState.Contains("\"state\":0"), "save normalizes transient ejection to underground");
        worm.GetComponent<SandwormZeldaCharacterData>().ApplyRuntimeState(saved);
        Check(!worm.IsRoarRecovery && worm.GetComponent<SandwormStateController>().IsUnderground, "restore cannot leave a permanently stunned above-ground body");
        Check(Vector3.Distance(Parts(worm)[0].localPosition, ground[0]) < .001f, "cancel restores ground pose, not suspended lift");
        Cleanup(); yield return null;

        // Surface/airborne/dead worms and expired or different-scene roars are ignored.
        worm = Worm(); data = worm.GetComponent<SandwormZeldaCharacterData>();
        roar = Roar(worm.GetComponent<SandwormBossPrototype>().HeadPosition, 3);
        worm.GetComponent<SandwormStateController>().EnterAboveGround(); worm.AdvanceAttack(.01f);
        Check(!worm.IsRoarRecovery, "already above-ground worm not re-ejected");
        worm.GetComponent<SandwormStateController>().EnterUnderground(); Set(roar, "elapsed", 1f); worm.AdvanceAttack(.01f);
        Check(!worm.IsRoarRecovery, "expired roar does not trigger");
        Set(roar, "elapsed", .2f); Scene other = SceneManager.CreateScene("Other roar scene"); SceneManager.MoveGameObjectToScene(roar.gameObject, other);
        worm.AdvanceAttack(.01f); Check(!worm.IsRoarRecovery, "cross-scene roar isolated");
        SceneManager.MoveGameObjectToScene(roar.gameObject, scene); roar.gameObject.SetActive(false);
        worm.AdvanceAttack(.01f); Check(!worm.IsRoarRecovery, "disabled roar ignored");
        roar.gameObject.SetActive(true); worm.AdvanceAttack(.01f); Check(worm.IsRoarRecovery, "new valid roar still works");
        data.TakeDamage(data.CurrentHealth + 1);
        Check(data.IsDead && Object.FindObjectOfType<SandwormDeathSequence>() != null, "death while popping uses existing sequential death safely");
        Cleanup(); yield return null;

        worm = Worm(); UnityEngine.Random.InitState(7491); Check(worm.TryLockAttack(new Vector2(8, 6)), "airborne fixture");
        for (int i = 0; i < 3000 && worm.Phase != SandwormEruptionAttack.AttackPhase.Traversing; i++) Step(worm, .025f);
        Check(worm.Phase == SandwormEruptionAttack.AttackPhase.Traversing, "reaches flight");
        roar = Roar(worm.GetComponent<SandwormBossPrototype>().HeadPosition); worm.AdvanceAttack(.01f);
        Check(!worm.IsRoarRecovery, "segmented flight is not misread as fully underground");
        Cleanup(); yield return null;

        // Existing ordinary AI behavior is unchanged.
        var npc = Actor(Vector2.zero, true); roar = Roar(Vector2.zero);
        Check(npc.GetComponent<ZeldaCharacterAiBase>().IsStunned, "ordinary AI still stunned by fear roar");
        Cleanup(); yield return null;
        Check(FearRoarArea.ActiveAreas.Count == 0, "roar registry cleans up on destruction");

        var preview = Preview(); while (preview.MoveNext()) yield return null;

        // Real Update/LateUpdate ordering, field expiration, and exact hold time.
        worm = Worm(); roar = Roar(worm.GetComponent<SandwormBossPrototype>().HeadPosition);
        Time.captureFramerate = 60; Time.timeScale = 1;
        bool popped = false, stunned = false, buried = false;
        float stunTime = -1;
        for (int n = 0; n < 540; n++)
        {
            yield return null;
            if (worm.Phase == SandwormEruptionAttack.AttackPhase.RoarRising) popped = true;
            if (worm.Phase == SandwormEruptionAttack.AttackPhase.RoarStunned && !stunned) { stunned = true; stunTime = Time.time; }
            if (worm.Phase == SandwormEruptionAttack.AttackPhase.RoarBurrowing && !buried)
            { buried = true; Check(Mathf.Abs(Time.time - stunTime - 5f) < .04f, "real-time stun lasts five seconds"); }
            if (buried && !worm.IsRoarRecovery && worm.Phase == SandwormEruptionAttack.AttackPhase.Roaming) break;
        }
        Check(popped && stunned && buried && !worm.IsRoarRecovery, "real-time full roar interruption and recovery");
        Check(roar == null && FearRoarArea.ActiveAreas.Count == 0, "expired real-time roar removed from registry");
        Time.captureFramerate = 0; Time.timeScale = 0; Cleanup(); yield return null;
    }
}
