using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class CardboardBoxFeedbackSmoke
{
    const string Key = nameof(CardboardBoxFeedbackSmoke);
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static IEnumerator suite;
    static int frame = -1, checks;
    static double deadline;
    static CardboardBoxFeedbackSmoke() { EditorApplication.update += Tick; }
    static void Check(bool ok, string message)
    { if (!ok) throw new Exception(message); checks++; Debug.Log("BOX_FEEDBACK_CHECK " + message); }
    static void Set(object o, string name, object value) => o.GetType().GetField(name, F).SetValue(o, value);
    static T Get<T>(object o, string name) => (T)o.GetType().GetField(name, F).GetValue(o);
    static object Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, F).Invoke(o, args);
    // Transient FX deliberately carry DontSave and are omitted by ordinary FindObjectsOfType.
    static ParticleSystem[] Dust() => Resources.FindObjectsOfTypeAll<ParticleSystem>()
        .Where(p => p.gameObject.scene.IsValid() && p.gameObject.activeInHierarchy && p.name == "Cardboard Landing Dust").ToArray();
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/")) throw new Exception("Isolated only");
        PlayerSettings.productName = "cardboard-feedback-smoke";
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Key, true); EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(Key, false))
            { SessionState.SetBool(Key, false); suite = Verify(); deadline = EditorApplication.timeSinceStartup + 120; }
            if (suite == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Box test timed out");
            if (suite.MoveNext()) return;
            suite = null; Debug.Log("BOX_FEEDBACK_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static IEnumerator Verify()
    {
        while (Time.frameCount < 3 || RetroSceneLoadReveal.IsPreparingGameplay) yield return null;
        Time.timeScale = 0;
        var prefab = Resources.Load<CardboardBoxPickupItem>("PickupItems/CardboardBoxPickupItem");
        Check(prefab && prefab.LandingDustEnabled && prefab.WornBobHeight > 0, "existing prefab receives feedback defaults");
        var box = Object.Instantiate(prefab);
        Check(Dust().Length == 0, "authored/load-created boxes do not burst");
        box.gameObject.SetActive(false); box.gameObject.SetActive(true);
        Check(Dust().Length == 0, "distance reload does not burst");
        box.MarkAsDropped();
        Check(Dust().Length == 1 && Dust()[0].particleCount == 18,
            "Q placement emits one bounded particle burst: systems=" + Dust().Length +
            " particles=" + (Dust().Length > 0 ? Dust()[0].particleCount : -1));
        var puff = Dust()[0];
        var particles = new ParticleSystem.Particle[18]; puff.GetParticles(particles);
        Check(particles.Any(p => p.velocity.x < 0) && particles.Any(p => p.velocity.x > 0) &&
            particles.Any(p => p.velocity.y < 0) && particles.Any(p => p.velocity.y > 0), "dust expands to all four sides");
        Check(!puff.collision.enabled && puff.GetComponentsInChildren<Collider2D>().Length == 0 && !puff.GetComponent<AudioSource>(),
            "dust has no collisions, damage or attracting sound");
        Check(puff.main.maxParticles == 18 && !puff.main.loop && !puff.emission.enabled, "one-shot dust has no ongoing emission");
        Check(puff.GetComponent<Renderer>().sortingOrder < box.GetComponent<SpriteRenderer>().sortingOrder, "dust sits below box");
        RenderDustPreview(puff);
        Object.Destroy(box.gameObject); Object.Destroy(puff.gameObject); yield return null;

        var inventoryRoot = new GameObject("Feedback inventory"); inventoryRoot.SetActive(false);
        var inventory = inventoryRoot.AddComponent<PersistentInventory>(); Set(inventory, "saveToPlayerPrefs", false);
        inventoryRoot.SetActive(true); inventory.ClearAll(); inventory.enabled = false;
        var actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/VitalNPCS/Hero.prefab"));
        var mover = actor.GetComponent<ZeldaFourWayMover>(); mover.ReceiveControl(Vector2.down);
        var body = actor.GetComponent<BoxCollider2D>(); Vector2 size = body.size, offset = body.offset;
        var state = actor.AddComponent<CardboardBoxWearState>(); state.Configure(prefab, mover);
        var visual = Get<GameObject>(state, "visualObject"); var renderer = visual.GetComponent<SpriteRenderer>();
        Check(Dust().Length == 0 && Get<bool>(state, "enterAnimationPlaying"), "wearing begins with the existing falling animation");
        Set(state, "enterAnimationElapsed", .2f); Call(state, "UpdateEnterAnimation");
        Check(Dust().Length == 1 && !Get<bool>(state, "enterAnimationPlaying"), "wearing emits only when the box lands");
        Call(state, "UpdateEnterAnimation"); Check(Dust().Length == 1, "landing is not repeated next frame");
        Vector3 rest = visual.transform.localPosition;
        var clip = typeof(CardboardBoxPickupItem).Assembly.GetType("CardboardBoxNpcVision").GetMethod("Clip", F);
        float baseline = (float)clip.Invoke(null, new object[] { new Vector2(-2, .02f), Vector2.right, 4f,
            null, null, actor.transform, renderer, Vector2.zero });
        float maximum = 0f;
        for (int i = 0; i < 50; i++)
        {
            actor.transform.position += Vector3.right * .035f;
            Vector3 position = actor.transform.position;
            Call(state, "UpdateMovementVisual", .02f);
            maximum = Mathf.Max(maximum, visual.transform.localPosition.y - rest.y);
            Check(actor.transform.position == position && body.size == size && body.offset == offset,
                "bob leaves movement and collider unchanged " + i);
        }
        Check(maximum > .015f && maximum <= prefab.WornBobHeight / actor.transform.lossyScale.y + .001f,
            "actual walking creates bounded visible bob");
        Vector2 visualShift = visual.transform.position - actor.transform.TransformPoint(rest);
        float bobClip = (float)clip.Invoke(null, new object[] { (Vector2)actor.transform.position + new Vector2(-2, .02f),
            Vector2.right, 4f, null, null, actor.transform, renderer, visualShift });
        Check(Mathf.Abs(baseline - bobClip) < .001f, "NPC vision bounds ignore visual-only bob");
        float pausedHeight = visual.transform.localPosition.y;
        Call(state, "UpdateMovementVisual", 0f);
        Check(visual.transform.localPosition.y == pausedHeight, "paused gameplay freezes bob");
        for (int i = 0; i < 30; i++) Call(state, "UpdateMovementVisual", .02f);
        Check(visual.transform.localPosition == rest, "stationary box smoothly settles at original height");
        actor.transform.position += Vector3.right * 100;
        Call(state, "UpdateMovementVisual", .02f);
        Check(visual.transform.localPosition == rest, "teleport does not animate a huge stride");
        Check(state.RemainingBlockedAttacks == 2 && state.MovementSpeedMultiplier == .5f, "protection and speed rules unchanged");
        Set(state, "enterAnimationPlaying", true); Set(state, "landingDustPending", true);
        var saved = SavedScalarFields.Capture(state, true); int count = Dust().Length;
        state.RestoreSavedFields(saved); Call(state, "UpdateEnterAnimation");
        Check(Dust().Length == count && !Get<bool>(state, "enterAnimationPlaying") && visual.transform.localPosition == rest,
            "save restore skips landing FX, including mid-entry old saves");
        actor.SetActive(false); actor.SetActive(true);
        Check(Dust().Length == count, "worn streaming reactivation does not burst");
        state.TryBlockAttack(null); Check(state.RemainingBlockedAttacks == 1, "wearing still blocks first attack");
        state.TryBlockAttack(null); yield return null;
        Check(mover.ActiveCardboardBox == null && actor.transform.Find("Visual").GetComponent<SpriteRenderer>().enabled,
            "breaking still restores the character's actual child renderer");
        Object.Destroy(actor); inventory.ClearAll(); Object.Destroy(inventoryRoot);
        Time.timeScale = 1f; float stop = Time.time + 1.1f;
        while (Time.time < stop) yield return null;
        Check(Dust().Length == 0, "all transient dust self-cleans");

        // Run the existing visibility regression against the changed shared clip helper.
        var regression = (IEnumerator)typeof(CardboardBoxNpcVisionSmoke).GetMethod("Verify", F).Invoke(null, null);
        while (regression.MoveNext()) yield return regression.Current;
        Debug.Log("BOX_EXISTING_NPC_VISION_PASS");
    }
    static void RenderDustPreview(ParticleSystem dust)
    {
        const int side = 256;
        var cameraObject = new GameObject("Dust preview camera");
        var camera = cameraObject.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = .95f;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.09f, .12f, .17f);
        camera.transform.position = new Vector3(0, 0, -10);
        var target = new RenderTexture(side, side, 16); camera.targetTexture = target;
        var strip = new Texture2D(side * 4, side, TextureFormat.RGB24, false);
        var tile = new Texture2D(side, side, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        float[] steps = { 0f, .12f, .18f, .25f };
        for (int i = 0; i < 4; i++)
        {
            if (steps[i] > 0f) dust.Simulate(steps[i], false, false, false);
            camera.Render(); RenderTexture.active = target;
            tile.ReadPixels(new Rect(0, 0, side, side), 0, 0); tile.Apply();
            strip.SetPixels(i * side, 0, side, side, tile.GetPixels());
        }
        strip.Apply(); System.IO.File.WriteAllBytes("CardboardLandingDust-preview.png", strip.EncodeToPNG());
        RenderTexture.active = previous; camera.targetTexture = null;
        Object.Destroy(cameraObject); Object.Destroy(target); Object.Destroy(tile); Object.Destroy(strip);
    }
}
