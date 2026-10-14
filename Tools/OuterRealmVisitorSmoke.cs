using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class OuterRealmVisitorSmoke
{
    const string Path = "Assets/Prefabs/Level2NPCS/Idle/OuterRealmVisitor-level2-Idle.prefab";
    const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static readonly Stack<IEnumerator> steps = new Stack<IEnumerator>();
    static readonly Vector2[] Directions = { Vector2.down, Vector2.up, Vector2.left, Vector2.right };
    static int checks, frame = -1;
    static double deadline;
    static OuterRealmVisitorSmoke() { EditorApplication.update += Tick; }
    static void Check(bool ok, string text) { checks++; if (!ok) throw new Exception(text); }
    static void Set(object o, string field, object value) => o.GetType().GetField(field, F).SetValue(o, value);
    static object Get(object o, string field) => o.GetType().GetField(field, F).GetValue(o);
    static IEnumerator Wait(float seconds) { float end = Time.time + seconds; while (Time.time < end) yield return null; }
    public static void Run()
    {
        Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "isolated project only");
        PlayerSettings.productName = "outer-realm-visitor-smoke";
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(nameof(OuterRealmVisitorSmoke), true); EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return; frame = Time.frameCount;
        try {
            if (SessionState.GetBool(nameof(OuterRealmVisitorSmoke), false)) {
                SessionState.SetBool(nameof(OuterRealmVisitorSmoke), false); deadline = EditorApplication.timeSinceStartup + 150; steps.Push(Verify());
            }
            if (steps.Count == 0) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("test timeout");
            while (steps.Count > 0) {
                var step = steps.Peek(); if (!step.MoveNext()) { steps.Pop(); continue; }
                if (step.Current is IEnumerator nested) { steps.Push(nested); continue; } return;
            }
            Debug.Log("OUTER_REALM_VISITOR_PASS checks=" + checks); EditorApplication.Exit(0);
        } catch (Exception e) { steps.Clear(); Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static OuterRealmVisitorZeldaCharacterData Spawn(GameObject prefab, Vector3 position)
    {
        var root = Object.Instantiate(prefab, position, Quaternion.identity);
        root.GetComponent<ZeldaCharacterAiBase>().enabled = false;
        return root.GetComponent<OuterRealmVisitorZeldaCharacterData>();
    }
    static IEnumerator Verify()
    {
        new GameObject("Test Audio Listener").AddComponent<AudioListener>();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Path);
        Check(prefab != null, "prefab imports");
        Check(AssetDatabase.FindAssets("OuterRealmVisitor t:Prefab", new[] { "Assets/Prefabs/Level2NPCS" }).Length == 1, "idle variant only");
        var root = Object.Instantiate(prefab); var data = root.GetComponent<OuterRealmVisitorZeldaCharacterData>();
        foreach (var component in root.GetComponents<Component>()) Check(component != null, "no missing components");
        Check(data.Health == 666 && data.CurrentHealth == 666 && data.SkillValue == 10 && data.AttackPower == 10 &&
            data.PossessionEnergy == 10 && data.PossessionCost == 1 && data.PermissionLevel == -2, "all requested stats");
        Check(root.GetComponent<CivilianIdleCharacterAi>() && !root.GetComponent<ZeldaPatrolCharacterAi>(), "normal idle AI");
        var ai = root.GetComponent<ZeldaCharacterAiBase>(); var mover = root.GetComponent<ZeldaFourWayMover>();
        var initialPosition = root.transform.position; yield return Wait(.2f);
        Check(root.transform.position == initialPosition && ai.CurrentState == ZeldaAiState.Idle, "idle remains stationary");
        ai.enabled = false;
        var body = root.transform.Find("Visual").GetComponent<SpriteRenderer>();
        var effects = root.GetComponent<OuterRealmVisitorVisual>();
        data.ApplyCharacterVisual(body, Vector2.down, false, false); yield return null;
        float y = body.transform.localPosition.y; yield return Wait(.3f);
        Check(Mathf.Abs(y - body.transform.localPosition.y) > .005f && root.transform.position == initialPosition, "visual bobs without moving collider/root");
        var smoke = root.GetComponentInChildren<ParticleSystem>();
        Check(smoke != null && smoke.main.maxParticles == 48 && smoke.main.loop && !smoke.collision.enabled, "bounded collision-free black smoke");
        Check(smoke.main.startColor.color.r < .03f && smoke.main.startColor.color.b < .03f, "smoke is black, not additive colored sparks");
        var eyes = root.GetComponentsInChildren<SpriteRenderer>().Where(s => s.name.Contains("Eye Halo")).ToArray();
        Check(eyes.Length == 2 && eyes.All(e => e.enabled), "paired front eye halos");
        for (int d = 0; d < 4; d++) {
            data.ApplyCharacterVisualWithMovement(body, Directions[d], true, false); yield return null;
            var sprite = body.sprite;
            Check(sprite.rect.size == new Vector2(24, 28) && sprite.pixelsPerUnit == 16 && sprite.texture.filterMode == FilterMode.Point, "padded reach canvas retains 16-PPU body size " + d);
            var hem = sprite.texture.GetPixels32();
            Check(Enumerable.Range(4, 8).All(x => hem[5 * 24 + x + 4].a == 255), "continuous robe hem without separate feet " + d);
            Check(root.GetComponent<PixelCharacterWalkAnimator>() == null, "floating robes have no boot stepping");
            Check(eyes.Count(e => e.enabled) == (d == 0 ? 2 : d == 1 ? 0 : 1), "directional eye count " + d);
            data.ApplyCharacterVisual(body, Directions[d], false, false); Check(body.sprite == sprite, "cached directional sprite");
            data.ApplyCharacterVisual(body, Directions[d], false, true); Check(body.sprite != sprite, "casting reach pose");
            data.SetCastProgress(Directions[d], 0); var startPalm = data.GetAttackHandPosition(Directions[d]);
            data.SetCastProgress(Directions[d], .18f); var halfReach = body.sprite;
            data.SetCastProgress(Directions[d], .5f); var fullReach = body.sprite;
            Check(halfReach != fullReach, "distinct intermediate and fully extended frames " + d);
            Check(Mathf.Abs(Vector3.Dot(data.GetAttackHandPosition(Directions[d]) - startPalm, Directions[d]) - .25f) < .001f,
                "compact four-pixel reach matches ordinary NPC scale " + d);
            var palmColor = (Color32)new Color(.45f, .38f, .52f, 1);
            Check(fullReach.texture.GetPixels32().Count(p => p.Equals(palmColor)) == (d < 2 ? 12 : 6),
                "flat six-pixel palm blocks without thumb or knuckle details " + d);
            data.SetCastProgress(Directions[d], .89f); Check(body.sprite == halfReach, "retraction reuses intermediate frame " + d);
            data.SetCastProgress(Directions[d], .5f); Check(body.sprite == fullReach, "extended frame is cached " + d);
            data.EndCastVisual(); Check(body.sprite == sprite, "animation returns to cached idle frame " + d);
        }
        data.ApplyCharacterVisual(body, Vector2.down, false, false); yield return null;
        var pixels = body.sprite.texture.GetPixels32();
        Check(Enumerable.Range(5, 6).All(x => pixels[18 * 24 + x + 4].Equals((Color32)Color.black)), "hood conceals face in pure black");
        Check(pixels[19 * 24 + 10].Equals(pixels[19 * 24 + 13]) && pixels[19 * 24 + 10].b > pixels[19 * 24 + 10].g, "symmetric purple eyes");
        body.maskInteraction = SpriteMaskInteraction.VisibleInsideMask; yield return null;
        Check(smoke.GetComponent<ParticleSystemRenderer>().maskInteraction == body.maskInteraction && eyes.All(e => e.maskInteraction == body.maskInteraction), "effects inherit visibility mask");
        body.maskInteraction = SpriteMaskInteraction.None;
        root.SetActive(false); root.SetActive(true); ai.enabled = false; yield return null;
        data.ApplyCharacterVisual(body, Vector2.down, false, false); yield return null;
        Check(root.GetComponentsInChildren<ParticleSystem>(true).Length == 1 && smoke.isPlaying, "streaming reenable reuses one emitter");
        var state = JsonUtility.FromJson<ZeldaCharacterData.RuntimeState>(JsonUtility.ToJson(data.CaptureRuntimeState()));
        data.TakeDamage(2); data.ApplyRuntimeState(state);
        Check(data.CurrentHealth == 666 && data.PermissionLevel == -2, "health and negative permission survive restore");
        Check(Resources.Load<SaveGameCatalog>("SaveGameCatalog").FindCharacter(state.characterType, root.name) == prefab, "summoned actor can be restored from catalog");
        mover.ReceiveControl(Vector2.right); Check(mover.enabled, "can receive player control"); mover.enabled = false;
        yield return Wait(.25f);
        var target = Spawn(prefab, Vector3.right * 2.75f); var distant = Spawn(prefab, Vector3.right * 4.5f);
        for (int d = 0; d < 4; d++) {
            target.transform.position = Directions[d] * 2.75f; distant.transform.position = Directions[d] * 4.5f;
            target.ApplyRuntimeState(state); distant.ApplyRuntimeState(state);
            mover.CancelAttackForStun(); Physics2D.SyncTransforms();
            Check(mover.TryPerformAttack(Directions[d], target), "cast along direction " + d);
            Vector3 expectedHand = data.GetAttackHandPosition(Directions[d]);
            Sprite raisedPose = body.sprite;
            var wave = Object.FindObjectOfType<DirectionalPurpleWave>(); Check(wave != null, "animated directional wave created");
            var beam = wave.transform.Find("Hand-fed Pixel Energy Wave").GetComponent<SpriteRenderer>();
            Check(Vector3.Distance(beam.transform.position, expectedHand) < .001f, "energy begins at casting palm " + d);
            Check(beam.sprite.texture.filterMode == FilterMode.Point && wave.GetComponentsInChildren<MeshRenderer>().Length == 0,
                "pixel raster animation replaces geometric crescents " + d);
            Vector3 terminal = (Vector3)Get(wave, "terminal");
            Check(Mathf.Abs(wave.transform.InverseTransformPoint(terminal).y - .5f) < .001f, "terminal is on original far range boundary " + d);
            var collider = wave.GetComponent<BoxCollider2D>();
            Check(collider.size.y < .1f && target.CurrentHealth == 666, "far end not damaged at cast start");
            yield return Wait(.18f); Check(target.CurrentHealth == 666 && collider.size.y > .1f, "wave advances before long-range hit");
            float reach = Vector3.Dot(data.GetAttackHandPosition(Directions[d]) - expectedHand, Directions[d]);
            Check(body.sprite != raisedPose && reach > .15f && reach < .35f,
                "short readable extension through pixel poses, including visual bob " + d);
            Check(Vector3.Distance(beam.transform.position, data.GetAttackHandPosition(Directions[d])) < .012f,
                "beam remains attached to moving palm and floating body " + d);
            Check(GameObject.Find("Pixel Energy Wave Terminal Burst") == null, "no premature burst during travel " + d);
            var beamPixels = beam.sprite.texture.GetPixels32(); int pixelWidth = beam.sprite.texture.width;
            int finalRow = Array.FindLastIndex(beamPixels, p => p.a != 0) / pixelWidth;
            Check(finalRow > 2 && Enumerable.Range(0, finalRow + 1).All(row =>
                Enumerable.Range(0, pixelWidth).Any(x => beamPixels[row * pixelWidth + x].a > 0)),
                "energy remains continuous from hand to wavefront, not a detached projectile " + d);
            if (d == 3) { Render(root, "OuterRealmVisitor-wave-preview.png", 1280, 720, new Vector3(1.4f, .25f, -10), 1.7f); }
            yield return Wait(.35f);
            var burst = GameObject.Find("Pixel Energy Wave Terminal Burst");
            Check(burst != null && Vector3.Distance(burst.transform.position, (Vector3)Get(wave, "terminal")) < .001f, "burst starts only at maximum range " + d);
            Check(Mathf.Abs(wave.transform.InverseTransformPoint(burst.transform.position).y - .5f) < .001f, "moving hand never extends gameplay range " + d);
            Check(burst.GetComponentsInChildren<Collider2D>().Length == 0, "terminal breakup is visual only " + d);
            if (d == 3) { Render(root, "OuterRealmVisitor-burst-preview.png", 1280, 720, new Vector3(1.4f, .25f, -10), 1.7f); }
            yield return Wait(.35f);
            Check(burst == null && wave == null, "beam and detached breakup both clean up " + d);
            var resting = body.sprite;
            data.ApplyCharacterVisual(body, Directions[d], false, false);
            Check(resting == body.sprite, "attack cleanup restores resting hands " + d);
            Check(target.CurrentHealth == 656 && distant.CurrentHealth == 666 && data.CurrentHealth == 666, "one 10-power hit at long range, no self or out-of-range hit " + d);
            Check(mover.LastAttackOutcome.HitTarget && mover.LastAttackOutcome.IsComplete, "AI receives attack outcome " + d);
        }
        Object.Destroy(target.gameObject); Object.Destroy(distant.gameObject); yield return null;
        // Four-way native runtime preview (including eye glows, float and smoke).
        var previews = new List<GameObject>(); root.SetActive(false);
        // Four orientations, each with idle / raise / reach / full extension.
        for (int d = 0; d < 4; d++) for (int pose = 0; pose < 4; pose++) {
            var clone = Spawn(prefab, new Vector3((pose - 1.5f) * 1.65f, (1.5f - d) * 1.8f, 0));
            previews.Add(clone.gameObject);
            clone.ApplyCharacterVisual(clone.transform.Find("Visual").GetComponent<SpriteRenderer>(), Directions[d], false, false);
            if (pose > 0) clone.SetCastProgress(Directions[d], pose == 1 ? 0 : pose == 2 ? .18f : .5f);
        }
        yield return null;
        Render(previews[0], "OuterRealmVisitor-hand-poses-preview.png", 1056, 1248, new Vector3(0, .5f, -10), 3.9f);
        foreach (var go in previews) Object.Destroy(go); previews.Clear(); yield return null;
        for (int d = 0; d < 4; d++) {
            var clone = Spawn(prefab, new Vector3((d - 1.5f) * 1.6f, 0, 0)); previews.Add(clone.gameObject);
            clone.ApplyCharacterVisual(clone.transform.Find("Visual").GetComponent<SpriteRenderer>(), Directions[d], false, false);
        }
        var camera = new GameObject("Smoke Preview Camera").AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 1.05f;
        camera.transform.position = new Vector3(0, .5f, -10); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color32(24, 31, 43, 255);
        yield return Wait(1.3f);
        foreach (var go in previews) {
            var particles = go.GetComponentInChildren<ParticleSystem>();
            // Batch mode does not continuously render a game camera, so warm the
            // culled emitter explicitly before capturing the production effect.
            particles.Simulate(1.3f, true, true, true);
            Check(particles.particleCount > 8, "black smoke actually emits live particles");
        }
        Render(previews[0], "OuterRealmVisitor-Idle-preview.png", 1280, 440, camera.transform.position, 1.05f);
        foreach (var go in previews) Object.Destroy(go); Object.Destroy(camera.gameObject); Object.Destroy(root); yield return null;
    }
    static void Render(GameObject root, string name, int width, int height, Vector3 position, float size)
    {
        var cam = new GameObject("Capture Camera").AddComponent<Camera>(); cam.transform.position = position; cam.orthographic = true; cam.orthographicSize = size;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color32(24, 31, 43, 255);
        var rt = new RenderTexture(width, height, 24); cam.targetTexture = rt; cam.Render();
        var previous = RenderTexture.active; RenderTexture.active = rt;
        var image = new Texture2D(width, height, TextureFormat.RGBA32, false); image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
        File.WriteAllBytes(Application.dataPath + "/../" + name, image.EncodeToPNG());
        RenderTexture.active = previous; cam.targetTexture = null;
        Object.DestroyImmediate(image); Object.DestroyImmediate(rt); Object.DestroyImmediate(cam.gameObject);
    }
}
