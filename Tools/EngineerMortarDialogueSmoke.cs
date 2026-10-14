using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class EngineerMortarDialogueSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const string A = "EngineerDialogueA", B = "EngineerDialogueB";
    const string PlayerPrefab = "Assets/Prefabs/Level2NPCS/Idle/AdvancedPriest-level2-Idle.prefab";
    static readonly Stack<IEnumerator> steps = new Stack<IEnumerator>();
    static int frame = -1, checks;
    static double deadline;
    static EngineerMortarDialogueSmoke() { EditorApplication.update += Tick; }
    static void Check(bool ok, string label) { checks++; if (!ok) throw new Exception(label); }
    static object Get(object o, string n) => o.GetType().GetField(n, F).GetValue(o);
    static CharacterDialogueWindow Window => Object.FindObjectOfType<CharacterDialogueWindow>();
    static CharacterDialogue.Node Node => (CharacterDialogue.Node)Get(Window, "current");
    static CharacterDialogue Npc => Object.FindObjectsOfType<CharacterDialogue>(true).Single(d => d.name == "大工程师");
    static void Select(int index) => typeof(CharacterDialogueWindow).GetMethod("Select", F).Invoke(Window, new object[] { index });
    public static void Run()
    {
        Check(Application.dataPath.Replace('\\','/').Contains("/.codex-temp/"), "isolated project only");
        PlayerSettings.productName = "engineer-mortar-dialogue-smoke";
        var source = EditorSceneManager.OpenScene("Assets/Scenes/Level2/Level2-Floor-1.unity");
        var owner = source.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CharacterDialogue>(true)).Single(d => d.name == "大工程师");
        Check(owner.GetComponent<DialogueActorAgreement>() != null, "scene instance has agreement");
        Check(owner.FindNode("start").choices.Length == 2 && owner.FindNode("start").choices[1].effect == owner.GetComponent<DialogueActorAgreement>(), "authored accept option wired");
        Check(owner.FindNode("accepted").text.Contains("1551"), "safe code authored");
        Check(source.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MortarControlDevicePickupItem>(true)).Any(), "existing installation kit remains in scene");
        foreach (string id in new[] { "accepted", "reminder", "stranger" })
            Check(owner.FindNode(id).choices.Length == 1 && owner.FindNode(id).choices[0].action == CharacterDialogue.ChoiceAction.Exit, id + " exit only");
        var fixture = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        var clone = Object.Instantiate(owner.gameObject); clone.transform.SetParent(null); clone.name = "大工程师";
        SceneManager.MoveGameObjectToScene(clone, fixture); clone.transform.position = Vector3.zero;
        clone.SetActive(true); clone.GetComponent<ZeldaCharacterAiBase>().enabled = false;
        SceneManager.SetActiveScene(fixture); MakeCamera();
        for (int i = 0; i < 2; i++)
        {
            var actor = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab));
            actor.name = "Actor " + i; actor.transform.position = new Vector3(-4, -4 - i * 3);
            actor.GetComponent<ZeldaCharacterAiBase>().enabled = false;
        }
        EditorSceneManager.SaveScene(fixture, "Assets/" + A + ".unity"); EditorSceneManager.CloseScene(source, true);
        var next = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); MakeCamera();
        // The optimized save system omits empty scenes; use a normal saved NPC in the destination.
        var resident = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab));
        resident.name = "Destination Resident"; resident.transform.position = Vector3.right * 20;
        resident.GetComponent<ZeldaCharacterAiBase>().enabled = false;
        EditorSceneManager.SaveScene(next, "Assets/" + B + ".unity");
        EditorBuildSettings.scenes = EditorBuildSettings.scenes.Where(s => s.path != "Assets/" + A + ".unity" && s.path != "Assets/" + B + ".unity")
            .Concat(new[] { new EditorBuildSettingsScene("Assets/" + A + ".unity", true), new EditorBuildSettingsScene("Assets/" + B + ".unity", true) }).ToArray();
        EditorSceneManager.OpenScene("Assets/" + A + ".unity");
        SessionState.SetInt("EngineerDialogueChecks", checks); SessionState.SetBool(nameof(EngineerMortarDialogueSmoke), true);
        EditorApplication.EnterPlaymode();
    }
    static void MakeCamera()
    {
        var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)); go.tag = "MainCamera";
        go.transform.position = new Vector3(0, 0, -10); go.GetComponent<Camera>().orthographic = true;
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return; frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(EngineerMortarDialogueSmoke), false))
            {
                SessionState.SetBool(nameof(EngineerMortarDialogueSmoke), false); checks = SessionState.GetInt("EngineerDialogueChecks", 0);
                deadline = EditorApplication.timeSinceStartup + 150; steps.Push(Verify());
            }
            if (steps.Count == 0) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("timeout");
            while (steps.Count > 0)
            {
                var step = steps.Peek(); if (!step.MoveNext()) { steps.Pop(); continue; }
                if (step.Current is IEnumerator nested) { steps.Push(nested); continue; } return;
            }
            Debug.Log("ENGINEER_MORTAR_DIALOGUE_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { steps.Clear(); Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static IEnumerator Talk(ZeldaFourWayMover actor, string expected)
    {
        var old = ZeldaRuntimeRegistry.GetControlledMover(); if (old != null && old != actor) old.enabled = false;
        actor.ReceiveControl(Vector2.left); ZeldaRuntimeRegistry.ClaimControlledMover(actor);
        actor.transform.position = Npc.transform.position + Vector3.right * 1.4f; Physics2D.SyncTransforms(); yield return null;
        CharacterDialogueWindow.Open(Npc, actor);
        Check(Window != null && Node.id == expected, "open " + expected);
        Check(((string)Get(Window, "history")).Contains(Node.text), "correct authored transcript text");
        yield return null;
        CheckTranscriptRendering();
    }
    static void CheckTranscriptRendering()
    {
        Canvas.ForceUpdateCanvases();
        var texts = ((List<Text>)Get(Window, "transcriptBlocks")).Where(t => t.gameObject.activeSelf).ToArray();
        string history = (string)Get(Window, "history");
        Check(string.Concat(texts.Select(t => t.text)) == history, "render blocks preserve all text and whitespace");
        var reference = ((TextGenerator)Get(Window, "transcriptLayout")).verts;
        var expectedGlyphs = new List<Vector3>(); var actualGlyphs = new List<Vector3>();
        for (int i = 0; i + 3 < reference.Count; i += 4)
            if (Mathf.Abs(reference[i].position.x - reference[i+2].position.x) > .01f && Mathf.Abs(reference[i].position.y - reference[i+2].position.y) > .01f)
                expectedGlyphs.Add(reference[i].position / texts[0].pixelsPerUnit);
        float allocated = 0;
        foreach (var text in texts)
        {
            Check(text.text.Length <= 320, "each text stays below glow glyph budget");
            using (var mesh = new VertexHelper())
            {
                typeof(Text).GetMethod("OnPopulateMesh", F, null, new[] { typeof(VertexHelper) }, null).Invoke(text, new object[] { mesh });
                var original = new List<UIVertex>(); mesh.GetUIVertexStream(original);
                for (int i = 0; i + 5 < original.Count; i += 6)
                    if (Mathf.Abs(original[i].position.x - original[i+2].position.x) > .01f && Mathf.Abs(original[i].position.y - original[i+2].position.y) > .01f)
                        actualGlyphs.Add(original[i].position - Vector3.up * (text.rectTransform.rect.height * .5f + allocated));
                text.GetComponent<TitleScreenGlow>().ModifyMesh(mesh);
                Check(mesh.currentVertCount == original.Count * 25 && mesh.currentVertCount < 64000, "full three-ring glow survives after mesh expansion");
                if (original.Count > 0)
                {
                    var result = new List<UIVertex>(); mesh.GetUIVertexStream(result);
                    var core = result[result.Count - original.Count].color;
                    Check(core.b > core.r + 40 && core.g > core.r + 40, "text core remains fluorescent blue, not white");
                }
            }
            allocated += text.GetComponent<LayoutElement>().preferredHeight;
        }
        Check(expectedGlyphs.Count == actualGlyphs.Count, "all original glyphs rendered exactly once");
        float maxError = 0;
        for (int i = 0; i < expectedGlyphs.Count; i++) maxError = Mathf.Max(maxError, Vector3.Distance(expectedGlyphs[i], actualGlyphs[i]));
        Check(maxError <= 1.5f / texts[0].pixelsPerUnit, "glyph positions preserved across block joins (pixel rounding only): " + maxError);
        var settings = texts[0].GetGenerationSettings(new Vector2(texts[0].GetPixelAdjustedRect().width, 0));
        float fullHeight = texts[0].cachedTextGeneratorForLayout.GetPreferredHeight(history, settings) / texts[0].pixelsPerUnit;
        Check(Mathf.Abs(allocated - fullHeight) < .1f, "split does not change original transcript height");
    }
    static IEnumerator Close() { CharacterDialogueWindow.CloseFor(Npc); yield return null; }
    static bool Arrived(string scene) => SceneManager.GetActiveScene().name == scene && !DocumentReader.IsInputBlocked &&
        !SceneTravelStateManager.Instance.IsSceneTravelRestoreInProgress && !SceneTravelStateManager.Instance.IsQuickRestartPending &&
        !GameSaveSystem.IsLoading && !GameSaveSystem.IsPreparingSceneSave;
    static IEnumerator Verify()
    {
        while (DocumentReader.IsInputBlocked || GameSaveSystem.IsPreparingSceneSave) yield return null;
        var journal = QuestJournalManager.GetOrCreate(); journal.ResetAllEntries();
        var actor = GameObject.Find("Actor 0").GetComponent<ZeldaFourWayMover>();
        var other = GameObject.Find("Actor 1").GetComponent<ZeldaFourWayMover>();
        string identity = actor.CharacterData.IndividualAccessId;
        yield return Talk(actor, "before_hunt"); yield return Close();
        Check(!Npc.GetComponent<DialogueActorAgreement>().TryExecute(Npc, actor), "cannot accept before parent quest");
        journal.AddOrUpdateEntry(QuestJournalManager.Level2SandwormQuestId, QuestJournalManager.Level2SandwormQuestTitle, "hunt");
        int notifications = 0; journal.JournalChanged += () => notifications++;
        yield return Talk(actor, "start"); Select(0); yield return null;
        Check(!Npc.GetComponent<DialogueActorAgreement>().IsAccepted && journal.CaptureSaveState().dialogueActors.Count == 0, "goodbye does not accept or record");
        yield return Talk(other, "start"); yield return Close();
        yield return Talk(actor, "start"); Select(1); yield return null;
        Check(Node.id == "accepted" && Node.text.Contains("1551"), "accept immediately reveals password");
        CheckTranscriptRendering();
        CapturePreview();
        yield return null; yield return null;
        // Reproduce a long looping merchant conversation, including the history cap.
        typeof(CharacterDialogueWindow).GetField("history", F).SetValue(Window,
            string.Concat(Enumerable.Repeat(Npc.FindNode("start").text + "\n\n", 100)));
        typeof(CharacterDialogueWindow).GetMethod("Enter", F).Invoke(Window, new object[] { Npc.FindNode("accepted") });
        yield return null; CheckTranscriptRendering();
        Check(((string)Get(Window, "history")).Length == 24000, "long-history cap remains unchanged");
        Check(Npc.GetComponent<DialogueActorAgreement>().IsAccepted, "accept stored");
        Check(journal.ContainsEntry(QuestJournalManager.SandwormMortarQuestId) && notifications > 0 &&
            journal.GetParentEntryId(QuestJournalManager.SandwormMortarQuestId) == QuestJournalManager.Level2SandwormQuestId,
            "acceptance adds engineer child quest");
        Check(!Npc.GetComponent<DialogueActorAgreement>().TryExecute(Npc, other), "cannot overwrite acceptance");
        yield return Close(); yield return Talk(actor, "reminder"); yield return Close();
        yield return Talk(other, "stranger"); yield return Close();
        yield return Talk(other, "stranger"); yield return Close();
        Check(journal.CaptureSaveState().dialogueActors.Single().characterId == identity, "repeat stranger visits preserve accepting body");
        yield return Talk(actor, "reminder"); yield return Close();
        Npc.gameObject.SetActive(false); Npc.gameObject.SetActive(true); Npc.GetComponent<ZeldaCharacterAiBase>().enabled = false;
        yield return Talk(actor, "reminder"); yield return Close();
        var manager = SceneTravelStateManager.GetOrCreate(); manager.TravelToScene(B, Vector2.right * 1.4f);
        while (!Arrived(B)) yield return null;
        Check(ZeldaRuntimeRegistry.GetControlledMover() == actor, "real scene travel preserves body");
        var saveSystem = GameSaveSystem.Instance;
        Check(saveSystem.Save(2, false), "write real disk save");
        Check(saveSystem.Read(2).journal.dialogueActors.Single().characterId == identity, "acceptance serialized to disk");
        journal.ResetAllEntries(); Check(saveSystem.Load(2), "load disk save"); while (!Arrived(B)) yield return null;
        actor = ZeldaRuntimeRegistry.GetControlledMover(); Check(actor.CharacterData.IndividualAccessId == identity, "loaded body identity retained");
        manager.TravelToScene(A, Vector2.right * 1.4f); while (!Arrived(A)) yield return null;
        yield return Talk(actor, "reminder"); yield return Close();
        other = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab)).GetComponent<ZeldaFourWayMover>();
        yield return Talk(other, "stranger"); yield return Close();
        var legacy = journal.CaptureSaveState(); legacy.dialogueActors = null; journal.ApplySaveState(legacy);
        yield return Talk(actor, "start"); yield return Close();
        journal.ResetAllEntries(); Check(!Npc.GetComponent<DialogueActorAgreement>().IsAccepted, "new game resets agreement");
    }
    static void CapturePreview()
    {
        var canvas = Window.GetComponent<Canvas>(); var mode = canvas.renderMode; var oldCamera = canvas.worldCamera;
        float distance = canvas.planeDistance;
        var camera = new GameObject("Dialogue preview camera").AddComponent<Camera>();
        camera.enabled = false; camera.orthographic = true; camera.orthographicSize = 360;
        camera.transform.position = new Vector3(0, 0, -100); camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color32(15, 25, 40, 255); camera.cullingMask = 1 << 30;
        var objects = canvas.GetComponentsInChildren<Transform>(true); var layers = objects.Select(t => t.gameObject.layer).ToArray();
        foreach (var t in objects) t.gameObject.layer = 30;
        var rt = new RenderTexture(1280, 720, 24); camera.targetTexture = rt;
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10;
        Canvas.ForceUpdateCanvases();
        typeof(CharacterDialogueWindow).GetMethod("RefreshTranscript", F).Invoke(Window, null);
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)Get(Window, "content"));
        ((ScrollRect)Get(Window, "scroll")).verticalNormalizedPosition = 0;
        Canvas.ForceUpdateCanvases(); camera.Render();
        var previous = RenderTexture.active; RenderTexture.active = rt;
        var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply();
        System.IO.File.WriteAllBytes(System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../EngineerDialogueGlow-after.png")), texture.EncodeToPNG());
        RenderTexture.active = previous; camera.targetTexture = null;
        canvas.renderMode = mode; canvas.worldCamera = oldCamera; canvas.planeDistance = distance;
        for (int i = 0; i < objects.Length; i++) objects[i].gameObject.layer = layers[i];
        Object.Destroy(camera.gameObject); Object.Destroy(texture); Object.Destroy(rt);
    }
}
