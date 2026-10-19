using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class SummoningCircleSmoke
{
    const string CirclePath = "Assets/Prefabs/Decorations/SummoningCircle.prefab";
    const string ActorPath = "Assets/Prefabs/Level2NPCS/Idle/Merchant-level2-Idle.prefab";
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly Stack<IEnumerator> steps = new Stack<IEnumerator>();
    static int checks, frame = -1;
    static double deadline;
    static SummoningCircleSmoke() { EditorApplication.update += Tick; }
    static void Check(bool ok, string label) { checks++; if (!ok) throw new Exception(label); }
    public static void Run()
    {
        Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "isolated project required");
        PlayerSettings.productName = "summoning-circle-smoke";
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(nameof(SummoningCircleSmoke), true); EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(SummoningCircleSmoke), false))
            {
                SessionState.SetBool(nameof(SummoningCircleSmoke), false);
                deadline = EditorApplication.timeSinceStartup + 120; steps.Push(Verify());
            }
            if (steps.Count == 0) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("test deadline");
            while (steps.Count > 0)
            {
                var step = steps.Peek();
                if (!step.MoveNext()) { steps.Pop(); continue; }
                if (step.Current is IEnumerator nested) { steps.Push(nested); continue; }
                return;
            }
            Debug.Log("SUMMONING_CIRCLE_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { steps.Clear(); Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static IEnumerator Until(Func<bool> condition, string label)
    {
        double timeout = EditorApplication.timeSinceStartup + 12; // Includes the six-second summoning ritual.
        while (!condition()) { if (EditorApplication.timeSinceStartup > timeout) throw new Exception("timeout: " + label); yield return null; }
        Check(true, label);
    }
    static ZeldaCharacterData Actor(CharacterPersistentIdentity identity, Vector3 position)
    {
        var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ActorPath), position, Quaternion.identity);
        var data = go.GetComponent<ZeldaCharacterData>();
        typeof(ZeldaCharacterData).GetField("persistentIdentity", F).SetValue(data, identity);
        go.GetComponent<ZeldaCharacterAiBase>().enabled = false;
        return data;
    }
    static SummoningCircle Circle(string name, Vector3 position)
    {
        var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(CirclePath), position, Quaternion.identity);
        go.name = name;
        var circle = go.GetComponent<SummoningCircle>();
        typeof(SummoningCircle).GetField("absorptionDuration", F).SetValue(circle, .55f);
        return circle;
    }
    static IEnumerator Verify()
    {
        var scene = SceneManager.CreateScene("Summoning Circle Smoke"); SceneManager.SetActiveScene(scene);
        var circle = Circle("Summoning Circle", Vector3.zero);
        Check(circle && circle.BoundCharacters.Count == 6, "prefab with exactly six slots");
        Check(circle.GetComponent<CameraVisionStreamingExempt>() && !circle.GetComponent<Collider2D>(), "nonblocking and streaming exempt");
        foreach (var c in circle.GetComponentsInChildren<Component>(true)) Check(c, "no missing scripts");
        for (int i = 0; i < 6; i++)
        {
            Check(Mathf.Abs(circle.CrystalPosition(i).magnitude - 2.1f) < .01f, "six tips equally distant " + i);
            Check(!circle.IsCrystalLit(i), "crystal initially dark " + i);
        }
        CheckActivatedCrystalGlow(circle);
        circle.transform.SetPositionAndRotation(new Vector3(12, -8), Quaternion.Euler(0, 0, 35));
        circle.transform.localScale = new Vector3(2, .6f, 1); circle.AreaOffset = new Vector2(1, 1);
        Check(circle.Contains(circle.transform.TransformPoint(new Vector3(6.9f, 5.9f))), "scaled rotated rectangle accepts interior");
        Check(!circle.Contains(circle.transform.TransformPoint(new Vector3(7.1f, 1))), "scaled rotated rectangle rejects outside");
        circle.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); circle.transform.localScale = Vector3.one; circle.AreaOffset = Vector2.zero;
        var identities = Enumerable.Range(0, 6).Select(_ => ScriptableObject.CreateInstance<CharacterPersistentIdentity>()).ToArray();
        Check(identities.Select(id => id.StableId).Distinct().Count() == 6, "unique stable identities");
        for (int i = 0; i < 6; i++) circle.BindCharacter(i, identities[i]);
        var unbound = Actor(null, Vector3.zero); unbound.KillFromPotion();
        var outside = Actor(identities[0], new Vector3(20, 0)); outside.KillFromPotion();
        var otherScene = SceneManager.CreateScene("Other Ritual Scene");
        var elsewhere = Actor(identities[0], Vector3.zero); SceneManager.MoveGameObjectToScene(elsewhere.gameObject, otherScene); elsewhere.KillFromPotion();
        Check(circle.Absorptions.Count == 0, "unbound/outside/different scene deaths excluded");
        yield return null;
        var original = Actor(identities[0], new Vector3(-3, 1));
        var charState = JsonUtility.FromJson<ZeldaCharacterData.RuntimeState>(JsonUtility.ToJson(original.CaptureRuntimeState()));
        string individual = original.IndividualAccessId;
        Object.Destroy(original.gameObject); yield return null;
        var recreated = Actor(null, new Vector3(-3, 1)); recreated.ApplyRuntimeState(charState);
        Check(recreated.PersistentCharacterId == identities[0].StableId && recreated.IndividualAccessId == individual, "identity survives serialized body reconstruction");
        Object.DontDestroyOnLoad(recreated.gameObject);
        recreated.TakeDamage(100);
        Check(circle.Absorptions.Count == 1 && circle.LitCrystalCount == 0, "travelling body death creates pending charge, no early light");
        var repeat = Actor(identities[0], Vector3.zero); repeat.KillFromPotion();
        Check(circle.Absorptions.Count == 1, "same identity cannot charge twice while in flight");
        yield return null;
        var visual = circle.GetComponent<SummoningCircleVisual>(); visual.RefreshImmediately();
        var mesh = circle.transform.Find("Offering Fragments").GetComponent<MeshFilter>().sharedMesh;
        Check(mesh.vertexCount >= 72 * 4 && mesh.colors.Max(c => c.a) > .8f, "many visible purple fragments batched in one mesh");
        var partial = JsonUtility.FromJson<SummoningCircle.RuntimeState>(JsonUtility.ToJson(circle.CaptureRuntimeState()));
        Check(partial.absorptions.Length == 1 && partial.consumedIds.Length == 1, "pending flight and used identity serialized");
        circle.ApplyRuntimeState(default); circle.ApplyRuntimeState(partial);
        Check(circle.Absorptions.Count == 1 && circle.Absorptions[0].seed == partial.absorptions[0].seed, "mid-flight restore keeps same particle path");
        yield return Until(() => circle.LitCrystalCount == 1, "first crystal lights only after arrival");
        repeat = Actor(identities[0], Vector3.zero); repeat.DestroyAfterSuccessfulPossession();
        Check(circle.Absorptions.Count == 0, "consumed identity still excluded after arrival");

        // Multiple simultaneous deaths reserve different crystals, regardless of death cause.
        for (int i = 1; i < 6; i++)
        {
            var actor = Actor(identities[i], new Vector3(i - 3, -1));
            if (i == 1) actor.DestroyAfterSuccessfulPossession();
            else if (i == 2) actor.DieFromSoulDetonation();
            else if (i == 3) actor.KillFromPotion();
            else actor.TakeDamage(100);
        }
        Check(circle.Absorptions.Count == 5 && circle.Absorptions.Select(f => f.crystal).Distinct().Count() == 5, "five simultaneous unique destinations");
        Check(circle.Absorptions.All(f => !circle.IsCrystalLit(f.crystal)), "no pending destination uses an already lit crystal");
        var summonTemplate = AssetDatabase.LoadAssetAtPath<GameObject>(ActorPath).GetComponent<ZeldaCharacterData>();
        circle.SummonPrefab = summonTemplate;
        yield return Until(() => circle.HasSummoned, "six offerings summon once");
        var spawned = Object.FindObjectsOfType<SummonedCharacterOrigin>().Single();
        Check(circle.LitCrystalCount == 6 && circle.Absorptions.Count == 0, "all six crystals lit");
        Check(spawned.transform.position == circle.transform.position && spawned.PrefabName == summonTemplate.name, "summoned configured template at center");
        Check(spawned.GetComponent<ZeldaCharacterData>().Health == summonTemplate.Health, "summoned prefab stats retained");
        Check(!spawned.GetComponent<ZeldaFourWayMover>().enabled && spawned.GetComponent<ZeldaCharacterAiBase>().enabled, "NPC summoned without taking player control");
        Check(string.IsNullOrEmpty(spawned.GetComponent<ZeldaCharacterData>().PersistentCharacterId), "summoned clone not a bound offering");
        Check(spawned.GetComponent<SceneTravelStableId>().StableId.StartsWith("summoned:"), "spawn has unique persistent scene ID");

        // Exercise the real scene restore path after serializing each object state as disk saves do.
        var manager = SceneTravelStateManager.Instance;
        if (manager == null) manager = new GameObject("State Manager").AddComponent<SceneTravelStateManager>();
        var snapshot = typeof(SceneTravelStateManager).GetMethod("CreateSceneSnapshot", F).Invoke(manager, new object[] { scene });
        var dictionary = (IDictionary)snapshot.GetType().GetField("objects", F).GetValue(snapshot);
        foreach (var key in dictionary.Keys.Cast<object>().ToArray())
        {
            var value = dictionary[key]; dictionary[key] = JsonUtility.FromJson(JsonUtility.ToJson(value), value.GetType());
        }
        string disk = manager.CaptureDiskWorld();
        Check(disk.Contains("\"hasSummoningCircle\":true") && disk.Contains("\"litMask\":63") && disk.Contains("\"summonedPrefabName\":\"" + summonTemplate.name), "disk save includes circle and summon origin");
        Check(!disk.Contains("Summoning Hexagram") && !disk.Contains("Offering Fragments"), "generated visual meshes excluded from snapshots");
        string stable = spawned.GetComponent<SceneTravelStableId>().StableId;
        Object.Destroy(spawned.gameObject); yield return null;
        circle.ApplyRuntimeState(default);
        var restored = typeof(SceneTravelStateManager).GetMethod("RestoreSceneSnapshot", F).Invoke(manager, new object[] { scene, snapshot, false });
        Check((bool)restored, "scene restore successful");
        yield return null;
        spawned = Object.FindObjectsOfType<SummonedCharacterOrigin>().Single();
        Check(circle.HasSummoned && circle.LitCrystalCount == 6, "circle restored as complete");
        Check(spawned.GetComponent<SceneTravelStableId>().StableId == stable, "missing summoned NPC recreated with same identity");
        var summonState = JsonUtility.FromJson<ZeldaCharacterData.RuntimeState>(JsonUtility.ToJson(spawned.GetComponent<ZeldaCharacterData>().CaptureRuntimeState()));
        var rebuiltSummon = Actor(null, new Vector3(30, 0)); rebuiltSummon.ApplyRuntimeState(summonState);
        Check(rebuiltSummon.GetComponent<SummonedCharacterOrigin>().PrefabName == summonTemplate.name, "summon origin survives player-body reconstruction too");
        Object.Destroy(rebuiltSummon.gameObject);
        spawned.GetComponent<ZeldaCharacterData>().KillFromPotion(); yield return null;
        for (int i = 0; i < 5; i++) yield return null;
        Check(!Object.FindObjectsOfType<SummonedCharacterOrigin>().Any(), "summon death never triggers another summon");
        var deadSnapshot = typeof(SceneTravelStateManager).GetMethod("CreateSceneSnapshot", F).Invoke(manager, new object[] { scene });
        typeof(SceneTravelStateManager).GetMethod("RestoreSceneSnapshot", F).Invoke(manager, new object[] { scene, deadSnapshot, false });
        yield return null;
        Check(!Object.FindObjectsOfType<SummonedCharacterOrigin>().Any(), "return after summon death does not respawn");

        // Sacrifice still broadcasts normal player-death/ghost-return notifications.
        var playerCircle = Circle("Player Offering", new Vector3(30, 0)); playerCircle.BindCharacter(0, identities[0]);
        var player = Actor(identities[0], new Vector3(30, 0)); player.GetComponent<ZeldaFourWayMover>().enabled = true;
        yield return null;
        player.KillFromPotion(); yield return null;
        Check(playerCircle.Absorptions.Count == 1 && Object.FindObjectsOfType<GhostZeldaCharacterData>().Length > 0, "player sacrifice still returns a ghost");
        foreach (var ghost in Object.FindObjectsOfType<GhostZeldaCharacterData>())
        {
            typeof(ZeldaCharacterData).GetField("persistentIdentity", F).SetValue(ghost, identities[1]);
            playerCircle.BindCharacter(1, identities[1]);
            ghost.KillFromPotion();
        }
        Check(playerCircle.Absorptions.Count == 1, "ghost is never consumed even if assigned a bound identity");

        var overlap = Circle("Overlapping Circle", new Vector3(31, 0)); overlap.BindCharacter(1, identities[1]);
        var overlappingVictim = Actor(identities[1], new Vector3(30, 0)); overlappingVictim.KillFromPotion();
        Check(playerCircle.Absorptions.Count == 2 && overlap.Absorptions.Count == 0, "overlapping circles do not duplicate one offering");

        var skeleton = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Level2NPCS/Skeleton-level2.prefab"), new Vector3(30, 1), Quaternion.identity).GetComponent<SkeletonZeldaCharacterData>();
        skeleton.GetComponent<ZeldaCharacterAiBase>().enabled = false;
        var resurrection = new GameObject("Test Resurrection Anchor").AddComponent<EnergyCrystal>();
        Check(resurrection.BindSkeleton(skeleton), "skeleton has an active resurrection source");
        typeof(ZeldaCharacterData).GetField("persistentIdentity", F).SetValue(skeleton, identities[2]);
        playerCircle.BindCharacter(2, identities[2]); skeleton.TakeDamage(100);
        Check(playerCircle.Absorptions.Count == 3, "skeleton death consumed instead of beginning reassembly");
        yield return null;
        Check(skeleton == null, "consumed skeleton removed permanently");
        Object.Destroy(resurrection.gameObject); Object.Destroy(overlap.gameObject);

        // Recreate the authored circle while a flight is pending, as on a scene return.
        string flightJson = JsonUtility.ToJson(playerCircle.CaptureRuntimeState());
        var previousFlight = playerCircle.Absorptions[0];
        Object.Destroy(playerCircle.gameObject); yield return null;
        playerCircle = Circle("Player Offering", new Vector3(30, 0));
        for (int i = 0; i < 6; i++) playerCircle.BindCharacter(i, identities[i]);
        playerCircle.ApplyRuntimeState(JsonUtility.FromJson<SummoningCircle.RuntimeState>(flightJson));
        Check(playerCircle.Absorptions.Count == 3 && playerCircle.Absorptions[0].seed == previousFlight.seed, "authored circle recreated with pending charges intact");
        yield return Until(() => playerCircle.LitCrystalCount == 3, "pending charges finish normally after scene recreation");
        var repeatedAfterReturn = Actor(identities[1], new Vector3(30, 0)); repeatedAfterReturn.KillFromPotion();
        Check(playerCircle.Absorptions.Count == 0, "recreated circle preserves consumed-identity ledger");

        Preview();
        Object.Destroy(circle.gameObject); Object.Destroy(playerCircle.gameObject);
        yield return null;
    }
    static void CheckActivatedCrystalGlow(SummoningCircle circle)
    {
        var visual = circle.GetComponent<SummoningCircleVisual>();
        var draw = typeof(SummoningCircleVisual).GetMethod("DrawFrame", F);
        var bloomFilter = circle.transform.Find("Activated Crystal Bloom").GetComponent<MeshFilter>();
        var bloom = bloomFilter.sharedMesh;
        var halo = circle.transform.Find("Six Crystal Halos").GetComponent<MeshFilter>().sharedMesh;
        var gems = circle.transform.Find("Six Offering Crystals").GetComponent<MeshFilter>().sharedMesh;
        draw.Invoke(visual, new object[] { .75f });
        Check(bloom.vertexCount == 0 && halo.vertexCount == 0, "dormant crystals have no active halo/bloom");
        var darkColors = gems.colors;
        draw.Invoke(visual, new object[] { 2.25f });
        Check(gems.colors.SequenceEqual(darkColors), "dormant facets do not breathe");
        circle.ApplyRuntimeState(new SummoningCircle.RuntimeState { litMask = 1 });
        draw.Invoke(visual, new object[] { .75f });
        Check(bloom.vertexCount == 8 && halo.vertexCount == 4, "one active crystal has two local bloom lobes and one halo");
        float peakAlpha = bloom.colors.Sum(c => c.a), peakWidth = bloom.bounds.size.x;
        float peakFacet = gems.colors.Sum(c => c.r + c.g + c.b);
        draw.Invoke(visual, new object[] { 2.25f });
        Check(bloom.colors.Sum(c => c.a) < peakAlpha * .65f && bloom.bounds.size.x < peakWidth, "halo brightness and size breathe visibly");
        Check(gems.colors.Sum(c => c.r + c.g + c.b) < peakFacet, "lit crystal facets breathe with bloom");
        Check(bloom.colors.All(c => c.b > c.r && c.r > c.g), "glow retains purple hue");
        var renderer = bloomFilter.GetComponent<MeshRenderer>();
        Check(renderer.sortingOrder > circle.transform.Find("Six Offering Crystals").GetComponent<MeshRenderer>().sortingOrder && renderer.sortingOrder < 30000, "bloom above facets but below vision mask");
        Check(renderer.sharedMaterial.shader == Resources.Load<Shader>("WarmLightGlow") && renderer.sharedMaterial.shader.isSupported, "supported additive shader retained in builds");
        Check(renderer.sharedMaterial != circle.transform.Find("Offering Fragment Glow").GetComponent<MeshRenderer>().sharedMaterial, "crystal material isolated from offerings");
        circle.ApplyRuntimeState(new SummoningCircle.RuntimeState { litMask = 63, summoned = true });
        Check(bloom.vertexCount == 48 && halo.vertexCount == 24, "all six crystals bloom in shared meshes after saved-state restore");
        int meshId = bloom.GetInstanceID(), materialId = renderer.sharedMaterial.GetInstanceID();
        for (int i = 0; i < 60; i++) draw.Invoke(visual, new object[] { i * .05f });
        Check(bloomFilter.sharedMesh.GetInstanceID() == meshId && renderer.sharedMaterial.GetInstanceID() == materialId, "animation reuses mesh and material");
        circle.gameObject.SetActive(false); circle.gameObject.SetActive(true);
        Check(circle.transform.Find("Activated Crystal Bloom").GetComponent<MeshFilter>().sharedMesh == bloom && bloom.vertexCount == 48, "reactivation retains charged glow and buffers");
        Check(circle.GetComponentsInChildren<Light>(true).Length == 0, "no per-crystal dynamic lights");
        circle.ApplyRuntimeState(default);
        Check(bloom.vertexCount == 0 && halo.vertexCount == 0, "reset or uncharged save immediately removes all crystal glow");
    }
    static void Preview()
    {
        var stage = new GameObject("Preview Stage");
        for (int i = 0; i < 3; i++)
        {
            var circle = Circle("Preview Circle", new Vector3((i - 1) * 6, 1000, 0)); circle.transform.SetParent(stage.transform);
            circle.ApplyRuntimeState(new SummoningCircle.RuntimeState { litMask = i == 0 ? 0 : i == 1 ? 21 : 63 });
            if (i == 1)
                circle.ApplyRuntimeState(new SummoningCircle.RuntimeState { litMask = 21, absorptions = new[] {
                    new SummoningCircle.Absorption { crystal = 1, seed = 241, origin = new Vector3(-1.5f,997.7f), bodySize = new Vector2(.6f,.8f), duration = 2, elapsed = .75f } } });
            foreach (var t in circle.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 30;
            var label = new GameObject("Caption"); label.layer = 30; label.transform.SetParent(stage.transform);
            label.transform.position = circle.transform.position + new Vector3(0,-3,0);
            var text = label.AddComponent<TextMesh>(); text.text = new[] { "DORMANT", "ABSORBING", "CHARGED" }[i];
            text.fontSize = 36; text.characterSize = .1f; text.anchor = TextAnchor.MiddleCenter; text.color = new Color(.71f,.66f,.8f);
        }
        var camera = new GameObject("Preview Camera").AddComponent<Camera>(); camera.transform.position = new Vector3(0,999.7f,-10);
        camera.orthographic = true; camera.orthographicSize = 3.4f; camera.cullingMask = 1 << 30;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color32(18,26,40,255);
        var rt = new RenderTexture(1500, 560, 24); camera.targetTexture = rt; camera.Render();
        var previous = RenderTexture.active; RenderTexture.active = rt;
        var image = new Texture2D(1500, 560, TextureFormat.RGBA32, false); image.ReadPixels(new Rect(0,0,1500,560),0,0); image.Apply();
        File.WriteAllBytes(Application.dataPath + "/../SummoningCircle-preview.png", image.EncodeToPNG());
        RenderTexture.active = previous; camera.targetTexture = null;
        Object.Destroy(image); Object.Destroy(rt); Object.Destroy(camera.gameObject); Object.Destroy(stage);
    }
}
