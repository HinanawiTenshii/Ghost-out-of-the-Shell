using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Run only in the isolated .codex-temp project; never saves gameplay scenes.
[InitializeOnLoad]
public static class NoticeBoardReadingSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static IEnumerator suite;
    static int frame = -1, checks;
    static double deadline;
    static NoticeBoardReadingSmoke() { EditorApplication.update += Tick; }
    public static void Run()
    {
        Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "isolated project only");
        PlayerSettings.productName = "notice-board-reading-smoke";
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(nameof(NoticeBoardReadingSmoke), true);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(NoticeBoardReadingSmoke), false))
            {
                SessionState.SetBool(nameof(NoticeBoardReadingSmoke), false);
                suite = Verify(); deadline = EditorApplication.timeSinceStartup + 90;
            }
            if (suite == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timeout");
            if (suite.MoveNext()) return;
            suite = null; Debug.Log("NOTICE_BOARD_READING_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    static T Get<T>(object o, string name) => (T)o.GetType().GetField(name, F).GetValue(o);
    static object Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, F).Invoke(o, args);
    static void SetText(DocumentReader reader, string text)
    {
        var serialized = new SerializedObject(reader);
        serialized.FindProperty("documentText").stringValue = text;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        Check(reader.DocumentText == text, "inspector edits preserve exact multiline text");
    }
    static IEnumerator Verify()
    {
        Time.timeScale = 1;
        while (DocumentReader.IsInputBlocked) yield return null;
        var scene = SceneManager.CreateScene("NoticeBoard Reading"); SceneManager.SetActiveScene(scene);
        new GameObject("Listener").AddComponent<AudioListener>();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Decorations/NoticeBoard.prefab");
        Check(prefab && prefab.GetComponent<DocumentReader>(), "existing board prefab has reader");
        Check(typeof(DocumentReader).GetField("documentText", F).GetCustomAttribute<TextAreaAttribute>() != null,
            "multiline inspector editing enabled");
        var root = Object.Instantiate(prefab);
        var reader = root.GetComponent<DocumentReader>();
        var solid = root.GetComponent<BoxCollider2D>();
        Check(Get<bool>(reader, "preserveExistingAppearance"), "board preservation mode enabled");
        Check(!solid.isTrigger && solid.size == new Vector2(2.6f, 1.9f), "Awake preserves original solid collider");
        Check(root.GetComponentsInChildren<SpriteRenderer>().Length == 15 && !root.GetComponent<SpriteRenderer>(),
            "original 15 sprites retained, no overlaid paper sprite");
        Check(Get<Texture2D>(reader, "documentTexture") == null && Get<Sprite>(reader, "documentSprite") == null,
            "no paper art allocated for board");
        var prompt = Get<GameObject>(reader, "interactionPromptObject");
        Check(prompt.GetComponent<TextMesh>().text == "按[E]阅读公告牌", "board-specific E prompt");
        Check(prompt.transform.parent == null && prompt.transform.lossyScale == Vector3.one, "prompt has independent world scale");

        var actor = new GameObject("Reader"); actor.AddComponent<SwordZeldaCharacterData>();
        var mover = actor.AddComponent<ZeldaFourWayMover>();
        mover.ReceiveControl(Vector2.up); ZeldaRuntimeRegistry.ClaimControlledMover(mover);
        foreach (Vector2 point in new[] { new Vector2(2, 0), new Vector2(-2, 0), new Vector2(0, 1.7f), new Vector2(0, -1.7f) })
        {
            actor.transform.position = point; Physics2D.SyncTransforms();
            Check(Call(reader, "GetControlledCharacterNearby") == mover, "reachable outside board edge " + point);
        }
        actor.transform.position = Vector3.right * 4; Physics2D.SyncTransforms();
        Check(Call(reader, "GetControlledCharacterNearby") == null, "out of range cannot read");
        Call(reader, "OpenDocument"); Check(!DocumentReader.IsDocumentOpen, "stale out-of-range request rejected");

        root.transform.localScale = new Vector3(2, .5f, 1);
        root.transform.rotation = Quaternion.Euler(0, 0, 90);
        actor.transform.position = Vector3.up * 3.3f; Physics2D.SyncTransforms();
        Check(Call(reader, "GetControlledCharacterNearby") == mover, "rotated and scaled board reachable from surface");
        Check(prompt.transform.lossyScale == Vector3.one, "scaling board does not scale prompt");
        root.transform.localScale = Vector3.one; root.transform.rotation = Quaternion.identity;
        actor.transform.position = Vector3.down * 1.7f; Physics2D.SyncTransforms();
        Call(reader, "Update");
        typeof(ZeldaInteractionArbiter).GetMethod("ResolveCurrentFrame", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        Check(ZeldaInteractionArbiter.IsSelected(reader, mover, KeyCode.E) && prompt.activeSelf,
            "normal facing and nearest-target arbitration selects board and shows prompt");

        const string notice = "要塞公告\n今日水道检修，请绕行。\n公告牌正文可以独立编辑。";
        SetText(reader, notice);
        Time.timeScale = .5f; Call(reader, "OpenDocument");
        var canvas = Get<GameObject>(reader, "readingCanvasObject");
        var content = Get<Text>(reader, "contentText");
        Check(DocumentReader.IsDocumentOpen && DocumentReader.IsInputBlocked && Time.timeScale == 0, "reading pauses gameplay and blocks input");
        Check(canvas.activeSelf && content.text == notice && content.color == ZeldaUiPalette.Primary, "shared reading window shows exact text and palette");
        Check(content.font == Get<Font>(reader, "documentFont") && content.font != null, "project font reused");
        Check(!prompt.activeSelf, "read prompt hidden while open");
        var other = Object.Instantiate(prefab); var otherReader = other.GetComponent<DocumentReader>();
        SetText(otherReader, "另一张公告"); Call(otherReader, "OpenDocument");
        Check(!Get<GameObject>(otherReader, "readingCanvasObject").activeSelf && content.text == notice, "cannot stack reading windows; instances keep independent text");
        Call(reader, "CloseDocument");
        Check(!DocumentReader.IsDocumentOpen && !canvas.activeSelf && Time.timeScale == .5f, "close restores original time scale");
        Call(reader, "OpenDocument"); Check(!DocumentReader.IsDocumentOpen, "close frame cannot reopen");
        yield return null;
        Call(reader, "OpenDocument"); Check(DocumentReader.IsDocumentOpen, "can reopen on later frame");
        Check(Get<GameObject>(reader, "readingCanvasObject") == canvas, "window reused, no duplicate canvas");
        root.SetActive(false);
        Check(!DocumentReader.IsDocumentOpen && Time.timeScale == .5f && !canvas.activeSelf, "distance unloading closes window and restores time");
        yield return null;
        root.SetActive(true); Time.timeScale = 1;
        mover.enabled = false;
        Check(Call(reader, "GetControlledCharacterNearby") == null, "inactive controlled body cannot read");
        mover.ReceiveControl(Vector2.up); ZeldaRuntimeRegistry.ClaimControlledMover(mover);
        var remoteScene = SceneManager.CreateScene("Other Level"); SceneManager.MoveGameObjectToScene(actor, remoteScene);
        Check(Call(reader, "GetControlledCharacterNearby") == null, "other scene actor cannot read");
        SceneManager.MoveGameObjectToScene(actor, scene);
        Object.DontDestroyOnLoad(actor);
        Check(Call(reader, "GetControlledCharacterNearby") == mover, "cross-scene persistent player can read active scene board");
        Call(reader, "OpenDocument"); Check(DocumentReader.IsDocumentOpen, "persistent player opens window");
        Object.Destroy(root); yield return null; yield return null;
        Check(!DocumentReader.IsDocumentOpen && Time.timeScale == 1 && !canvas && !prompt, "destroy closes and cleans detached UI");

        var document = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ReadableDocument.prefab"));
        var original = document.GetComponent<DocumentReader>();
        Check(!Get<bool>(original, "preserveExistingAppearance"), "existing documents keep original default mode");
        Check(document.GetComponent<BoxCollider2D>().isTrigger && document.GetComponent<BoxCollider2D>().size == new Vector2(.82f, 1.05f), "legacy document trigger unchanged");
        Check(document.GetComponent<SpriteRenderer>().sprite && Get<Texture2D>(original, "documentTexture").width == 14, "legacy document still generates paper");
        Check(Get<GameObject>(original, "interactionPromptObject").GetComponent<TextMesh>().text == "按[E]阅读文件", "legacy prompt unchanged");
        actor.transform.position = Vector3.down * .8f;
        SetText(original, "原文档仍然可读。\n第二行。"); Call(original, "OpenDocument");
        Check(DocumentReader.IsDocumentOpen && Get<Text>(original, "contentText").text == original.DocumentText, "legacy document reading works");
        Call(original, "CloseDocument");
        Check(Time.timeScale == 1, "legacy document close restores time");
        var sign = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Decorations/ReadableNoticeSign.prefab");
        Check(sign.GetComponent<SignpostInteraction>() && !sign.GetComponent<DocumentReader>(), "existing small notice sign not double-wired");
        Object.Destroy(other); Object.Destroy(document); Object.Destroy(actor);
        yield return null; yield return null;
    }
}
