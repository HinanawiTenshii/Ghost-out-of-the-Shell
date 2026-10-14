using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Run only in the isolated smoke project, never in the user's open level.
[InitializeOnLoad]
public static class SandwormDeathSequenceSmoke
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    const string Pending = "SandwormDeathSequenceSmoke.Pending";
    static readonly List<GameObject> owned = new List<GameObject>();
    static IEnumerator test;
    static int frame = -1, checks;
    static SandwormDeathSequenceSmoke() { EditorApplication.update += Tick; }
    public static void Run() { SessionState.SetBool(Pending, true); EditorApplication.EnterPlaymode(); }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(Pending, false)) { SessionState.SetBool(Pending, false); test = Verify(); }
            if (test == null || test.MoveNext()) return;
            test = null; Cleanup(); Debug.Log("SANDWORM_DEATH_SEQUENCE_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { test = null; Cleanup(); Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void Check(bool value, string label) { checks++; if (!value) throw new Exception(label); }
    static GameObject Own(GameObject go) { owned.Add(go); return go; }
    static void Cleanup() { foreach (var go in owned) if (go != null) Object.DestroyImmediate(go); owned.Clear(); }
    static object Field(object obj, string field) => obj.GetType().GetField(field, Flags).GetValue(obj);
    static void Call(object obj, string method) => obj.GetType().GetMethod(method, Flags).Invoke(obj, null);
    static List<ZeldaDeathExplosionVisual> Bursts(SandwormDeathSequence sequence) =>
        (List<ZeldaDeathExplosionVisual>)Field(sequence, "bursts");
    static void EndBurst(ZeldaDeathExplosionVisual burst)
    {
        typeof(ZeldaDeathExplosionVisual).GetField("age", Flags).SetValue(burst, 100f);
        Call(burst, "Update");
    }
    static SandwormZeldaCharacterData Worm(float scale = 1f)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Level2NPCS/Sandworm-BossPrototype.prefab");
        var go = Own(Object.Instantiate(prefab));
        go.transform.position = Vector3.zero; go.transform.localScale *= scale;
        go.GetComponent<SandwormBossPrototype>().ResetPose();
        return go.GetComponent<SandwormZeldaCharacterData>();
    }
    static Transform[] Ordered(SandwormBossPrototype motion)
    {
        var result = new List<Transform> { motion.Tail };
        for (int i = motion.BodySegments.Length - 1; i >= 0; i--) result.Add(motion.BodySegments[i]);
        result.Add(motion.Head); return result.ToArray();
    }
    static void Pose(SandwormZeldaCharacterData data, string mode)
    {
        var motion = data.GetComponent<SandwormBossPrototype>();
        var attack = data.GetComponent<SandwormEruptionAttack>();
        if (mode == "Ground") { data.GetComponent<SandwormStateController>().EnterAboveGround(); return; }
        if (mode == "Underground")
        {
            Check(motion.TrySetDestination(new Vector2(8, 6)), "roaming route");
            for (int i = 0; i < 40; i++) motion.Advance(.025f);
            return;
        }
        UnityEngine.Random.InitState(7491);
        Check(attack.TryLockAttack(new Vector2(8, 6)), "attack route for " + mode);
        if (mode != "Flight")
        {
            var bomb = Resources.Load<GameObject>("PickupItems/Bombs/BombPickupItem").GetComponent<BombPickupItem>();
            Own(bomb.CreateConfiguredPlacedBomb(attack.LockedAttackPoint, null).gameObject);
        }
        for (int n = 0; n < 5000; n++)
        {
            motion.Advance(.025f); attack.AdvanceAttack(.025f);
            if (mode == "Flight" && attack.EmergencePulses >= 4 && attack.Phase == SandwormEruptionAttack.AttackPhase.Traversing) return;
            if (mode == "Falling" && attack.Phase == SandwormEruptionAttack.AttackPhase.Falling) return;
            if (mode == "Stunned" && attack.Phase == SandwormEruptionAttack.AttackPhase.Stunned) return;
            if (mode == "Returning" && attack.Phase == SandwormEruptionAttack.AttackPhase.ReturningToBurrow && attack.RecoveryBurialPulses > 0) return;
        }
        throw new Exception("Could not reach death fixture phase " + mode);
    }
    static IEnumerator Preview()
    {
        var data = Worm(); data.GetComponent<SandwormStateController>().EnterAboveGround();
        var parts = Ordered(data.GetComponent<SandwormBossPrototype>());
        Bounds bounds = parts[0].GetComponent<SpriteRenderer>().bounds;
        foreach (var part in parts) bounds.Encapsulate(part.GetComponent<SpriteRenderer>().bounds);
        var camera = Own(new GameObject("Death render preview camera")).AddComponent<Camera>();
        camera.orthographic = true; camera.enabled = false;
        camera.transform.position = bounds.center + Vector3.back * 20;
        const int width = 1200, height = 280, count = 4;
        camera.orthographicSize = Mathf.Max(5f, (bounds.extents.x + 4) * height / width);
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.035f, .055f, .09f);
        var rt = new RenderTexture(width, height, 24); camera.targetTexture = rt;
        var strip = new Texture2D(width, height * count, TextureFormat.RGB24, false);
        data.TakeDamage(data.CurrentHealth + 1);
        var sequence = Object.FindObjectOfType<SandwormDeathSequence>(); Own(sequence.gameObject);
        int index = 0;
        for (int tick = 0; tick <= 65; tick++)
        {
            foreach (var burst in Bursts(sequence))
            {
                if (burst == null) continue;
                typeof(ZeldaDeathExplosionVisual).GetField("age", Flags).SetValue(burst, (float)Field(burst, "age") + .02f);
                Call(burst, "Update");
            }
            sequence.Advance(.02f);
            yield return null;
            if (tick != 4 && tick != 24 && tick != 54 && tick != 64) continue;
            camera.Render(); RenderTexture.active = rt;
            strip.ReadPixels(new Rect(0, 0, width, height), 0, height * (count - 1 - index++));
            RenderTexture.active = null;
        }
        strip.Apply();
        int bright = 0, magenta = 0;
        foreach (var pixel in strip.GetPixels32())
        {
            if (pixel.r > 120 && pixel.g > 80) bright++;
            if (pixel.r > 180 && pixel.b > 180 && pixel.g < 40) magenta++;
        }
        Check(bright > 1000 && magenta == 0, "GPU death frames contain visible effects and no missing shaders");
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(Application.dataPath, "../Sandworm-death-sequence-preview.png"), strip.EncodeToPNG());
        camera.targetTexture = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(strip);
        Cleanup(); yield return null;
    }
    static IEnumerator Verify()
    {
        Scene scene = SceneManager.CreateScene("Sandworm death sequence test");
        SceneManager.SetActiveScene(scene); Time.timeScale = 0;
        foreach (string mode in new[] { "Underground", "Ground", "Flight", "Falling", "Stunned", "Returning" })
        {
            var data = Worm(); var motion = data.GetComponent<SandwormBossPrototype>();
            Pose(data, mode);
            var parts = Ordered(motion); int count = parts.Length;
            var positions = new Vector3[count]; var rotations = new Quaternion[count];
            var scales = new Vector3[count]; var centers = new Vector3[count];
            var sprites = new Sprite[count]; var names = new string[count]; var materials = new Material[count];
            for (int i = 0; i < count; i++)
            {
                var renderer = parts[i].GetComponent<SpriteRenderer>();
                positions[i] = parts[i].position; rotations[i] = parts[i].rotation; scales[i] = parts[i].lossyScale;
                centers[i] = renderer.bounds.center; sprites[i] = renderer.sprite; names[i] = parts[i].name;
                materials[i] = renderer.sharedMaterial;
            }
            // Take a lethal hit while the whole-body hit material is already active.
            data.TakeDamage(1); Call(data, "LateUpdate");
            data.TakeDamage(data.CurrentHealth + 1);
            Check(data.IsDead && data.CurrentHealth == 0, "immediate death: " + mode);
            Check(!data.gameObject.activeInHierarchy, "no active body physics during visuals");
            Check(!data.ParticipatesInCharacterCollision, "dead body does not participate");
            Check(CameraTemporaryFraming.GetMultiplier(scene) == 1f, "camera framing released immediately");
            var allSequences = Object.FindObjectsOfType<SandwormDeathSequence>();
            Check(allSequences.Length == 1, "one custom death sequence, not generic root burst");
            var sequence = allSequences[0]; Own(sequence.gameObject);
            Check(sequence.gameObject.scene == scene, "death remains in owner's scene");
            Check(sequence.GetComponent<CameraVisionStreamingExempt>() != null, "death animation not distance unloaded");
            Check(sequence.GetComponentsInChildren<Collider2D>().Length == 0, "no colliders in remains or bursts");
            Check(sequence.GetComponentsInChildren<ZeldaCharacterData>().Length == 0, "no character/save/AI state in remains");
            Check(sequence.GetComponentsInChildren<ZeldaAttackHitbox>().Length == 0, "death is cosmetic, not an extra attack");
            Check(Bursts(sequence).Count == 1 && Vector3.Distance(Bursts(sequence)[0].transform.position, centers[0]) < .001f, "tail explodes first without initial delay");
            float largest = (float)Field(Bursts(sequence)[0], "maxScale");
            for (int i = 1; i < count; i++)
            {
                var copy = sequence.transform.Find("Frozen Death Pose/" + names[i] + " Death Remains");
                Check(copy != null && Vector3.Distance(copy.position, positions[i]) < .001f, mode + " death snapshots actual pose: " + names[i]);
                Check(Quaternion.Angle(copy.rotation, rotations[i]) < .01f && Vector3.Distance(copy.lossyScale, scales[i]) < .001f, "death keeps part rotation/scale");
                var renderer = copy.GetComponent<SpriteRenderer>();
                Check(renderer.enabled && renderer.sprite == sprites[i], "unexploded segment still visible with correct underground overlay");
                Check(renderer.sharedMaterial == materials[i], "no stale hit material copied");
            }
            data.TakeDamage(999);
            Check(Object.FindObjectsOfType<SandwormDeathSequence>().Length == 1, "duplicate damage cannot restart death");
            yield return null; yield return null;
            Check(data == null, "source character destroyed, not retained for VFX");
            Check(sprites[count - 1] != null && sprites[count - 1].texture != null, "shared sprites survive source destruction");
            float interval = (float)Field(sequence, "interval");
            sequence.Advance(0f); Check(Bursts(sequence).Count == 1, "paused game does not advance death");
            for (int i = 1; i < count; i++)
            {
                sequence.Advance(interval * .45f);
                Check(Bursts(sequence).Count == i, "wait between segment bursts");
                sequence.Advance(interval * .55f + .00001f);
                var bursts = Bursts(sequence);
                Check(bursts.Count == i + 1, "exactly one next segment burst");
                var burst = bursts[i];
                Check(Vector3.Distance(burst.transform.position, centers[i]) < .001f, "correct tail-to-head explosion location: " + names[i]);
                float size = (float)Field(burst, "maxScale");
                if (i < count - 1) largest = Mathf.Max(largest, size);
                else Check(size >= largest * 1.99f && burst.name == "Head Final Explosion", "head finale is twice largest body burst");
                yield return null;
                Check(sequence.transform.Find("Frozen Death Pose/" + names[i] + " Death Remains") == null, "burst consumes only its segment");
            }
            Check(sequence.GetComponentsInChildren<SandwormSegmentVisual>().Length == 0, "all body parts consumed");
            sequence.Advance(10f); Check(sequence != null, "sequence waits for final explosion to finish");
            foreach (var burst in Bursts(sequence)) if (burst != null) EndBurst(burst);
            yield return null; yield return null;
            if (sequence != null) sequence.Advance(0f);
            yield return null; yield return null;
            Check(sequence == null, "no orphaned visual root after finale");
            Cleanup(); yield return null;
        }

        // World scale must follow the victim, not the current player or camera.
        float normal = 0f;
        foreach (float scale in new[] { 1f, 2f })
        {
            var data = Worm(scale); data.TakeDamage(data.CurrentHealth + 1);
            var sequence = Object.FindObjectOfType<SandwormDeathSequence>(); Own(sequence.gameObject);
            sequence.Advance(100f);
            float finale = (float)Field(Bursts(sequence)[7], "maxScale");
            if (scale == 1f) normal = finale; else Check(Mathf.Abs(finale - normal * 2) < .001f, "death size tracks victim world scale");
            Cleanup(); yield return null;
        }

        // Actual Update/time-based completion without manual lifetime shortcuts.
        var shaking = Worm(); shaking.TakeDamage(shaking.CurrentHealth + 1);
        var shakingSequence = Object.FindObjectOfType<SandwormDeathSequence>(); Own(shakingSequence.gameObject);
        var camera = Own(new GameObject("Death shake camera")).AddComponent<Camera>();
        camera.orthographic = true; camera.orthographicSize = 20; camera.aspect = 16f / 9f;
        camera.transform.position = Vector3.back * 20;
        Check(CameraEnvironmentalShakeSource.SampleOffset(camera, null, .02f) == Vector2.zero, "tail burst does not shake screen");
        shakingSequence.Advance(.85f);
        Check(CameraEnvironmentalShakeSource.SampleOffset(camera, null, .02f) == Vector2.zero, "body bursts do not shake screen");
        shakingSequence.Advance(.14f);
        Vector2 firstShake = CameraEnvironmentalShakeSource.SampleOffset(camera, null, .02f);
        Check(firstShake.sqrMagnitude > 0 && firstShake.magnitude <= camera.orthographicSize * .0251f, "head finale starts bounded screen shake");
        Check(CameraEnvironmentalShakeSource.SampleOffset(camera, null, 0f) == Vector2.zero, "paused death does not move camera");
        camera.transform.position += Vector3.right * 500;
        Check(CameraEnvironmentalShakeSource.SampleOffset(camera, null, .02f) == Vector2.zero, "offscreen head finale does not shake");
        camera.transform.position -= Vector3.right * 500;
        var vision = camera.gameObject.AddComponent<CameraCircularVision>();
        camera.transform.position += Vector3.down * 12;
        vision.SetVisionMaskRadius(.3f); vision.RefreshSceneCameraSettings();
        Check(CameraEnvironmentalShakeSource.SampleOffset(camera, vision, .02f) == Vector2.zero, "on-screen but vision-masked finale does not shake");
        camera.transform.position -= Vector3.down * 12;
        vision.SetVisionMaskRadius(30f); vision.RefreshSceneCameraSettings();
        Check(CameraEnvironmentalShakeSource.SampleOffset(camera, vision, .02f).sqrMagnitude > 0, "visible finale enables shake");
        shakingSequence.Advance(.1f);
        Check(CameraEnvironmentalShakeSource.SampleOffset(camera, null, .02f) != firstShake, "head shake oscillates and decays");
        shakingSequence.Advance(.2f);
        Check(CameraEnvironmentalShakeSource.SampleOffset(camera, null, .02f) == Vector2.zero, "head shake ends after short duration");
        Cleanup(); yield return null;

        var realtime = Worm(); realtime.TakeDamage(realtime.CurrentHealth + 1);
        var liveSequence = Object.FindObjectOfType<SandwormDeathSequence>(); Own(liveSequence.gameObject);
        Time.captureFramerate = 60; Time.timeScale = 1;
        for (int n = 0; n < 180 && liveSequence != null; n++) yield return null;
        Check(liveSequence == null, "real-time sequence and all child explosions complete naturally");
        Time.captureFramerate = 0; Time.timeScale = 0;
        Cleanup(); yield return null;

        var preview = Preview(); while (preview.MoveNext()) yield return null;
        Scene other = SceneManager.CreateScene("Other active scene");
        SceneManager.SetActiveScene(scene);
        var scoped = Worm(); SceneManager.SetActiveScene(other);
        scoped.TakeDamage(scoped.CurrentHealth + 1);
        var scopedSequence = Object.FindObjectOfType<SandwormDeathSequence>();
        Check(scopedSequence.gameObject.scene == scene, "sequence does not leak into unrelated active scene");
        var unload = SceneManager.UnloadSceneAsync(scene);
        while (!unload.isDone) yield return null;
        Check(scopedSequence == null, "scene unload interrupts and cleans sequence");
        Cleanup(); yield return null;
    }
}
