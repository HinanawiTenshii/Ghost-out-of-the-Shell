using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Isolated full-script Unity validation; never loads or changes player saves/authored scenes.
[InitializeOnLoad]
public static class TrialProofAltarSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static IEnumerator suite;
    static int frame = -1, checks;
    static double deadline;
    static TrialProofAltarSmoke() { EditorApplication.update += Tick; }
    static void Check(bool ok, string label) { checks++; if (!ok) throw new Exception(label); }
    static void Set(object o, string n, object v) => o.GetType().GetField(n, F).SetValue(o, v);
    static T Get<T>(object o, string n) => (T)o.GetType().GetField(n, F).GetValue(o);
    static object Call(object o, string n, params object[] a) => o.GetType().GetMethod(n, F).Invoke(o, a);
    public static void Run()
    {
        Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "isolated project required");
        PlayerSettings.productName = "trial-proof-altar-smoke";
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(nameof(TrialProofAltarSmoke), true);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(TrialProofAltarSmoke), false))
            {
                SessionState.SetBool(nameof(TrialProofAltarSmoke), false);
                deadline = EditorApplication.timeSinceStartup + 90; suite = Verify();
            }
            if (suite == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("test timed out");
            if (suite.MoveNext()) return;
            suite = null; Debug.Log("TRIAL_PROOF_ALTAR_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static IEnumerator Verify()
    {
        while (DocumentReader.IsInputBlocked) yield return null;
        var scene = SceneManager.CreateScene("Altar Test"); SceneManager.SetActiveScene(scene);
        Time.timeScale = 1;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Decorations/TrialProofAltar.prefab");
        Check(prefab != null && prefab.GetComponent<TrialProofAltar>() != null, "prefab and script import");
        var altar = Object.Instantiate(prefab).GetComponent<TrialProofAltar>(); altar.name = "Trial Altar";
        var visual = altar.GetComponent<TrialProofAltarVisual>();
        Check(altar.ProofPrefab != null && altar.ProofPrefab.ItemId == TrialProofPickupItem.DefaultItemId, "proof template linked");
        Check(Get<Font>(altar, "promptFont") != null, "project pixel font configured");
        Check(altar.GetComponent<BoxCollider2D>() != null && !altar.GetComponent<BoxCollider2D>().isTrigger, "solid stone footprint");
        var doorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Decorations/DoubleSlidingDoor.prefab");
        var door = Object.Instantiate(doorPrefab).GetComponent<DoubleSlidingDoor>();
        door.transform.position = new Vector3(6, 0); door.SetOpenImmediately(false);
        var actor = new GameObject("Offering Actor").AddComponent<ZeldaCharacterData>();
        actor.transform.position = new Vector3(0, -1.3f);
        var mover = actor.gameObject.AddComponent<ZeldaFourWayMover>();
        ZeldaRuntimeRegistry.ClaimControlledMover(mover); mover.ReceiveControl(Vector2.up);
        var invObject = new GameObject("Altar Inventory"); invObject.SetActive(false);
        var inv = invObject.AddComponent<PersistentInventory>(); Set(inv, "saveToPlayerPrefs", false);
        invObject.SetActive(true); inv.ClearAll(); inv.enabled = false;
        yield return null;
        Check(!altar.IsComplete && !visual.DisplaysOffering, "default empty offering socket");
        Check(altar.InteractionMessage == "需要试炼证明", "missing-proof overhead prompt");
        Check(!altar.TryOffer(out var missing) && missing == "需要试炼证明" && !mover.IsAttacking, "no proof does not animate/complete");
        Check(inv.TryAddItem(TrialProofPickupItem.DefaultItemId, 2, 1), "two proofs in separate slots");
        inv.SelectSlot(4);
        Check(altar.InteractionMessage == "按[E]供奉试炼证明", "proof anywhere in inventory qualifies");
        Check(!altar.TryOffer(out var unbound) && unbound.Contains("尚未绑定") && inv.GetQuantity(TrialProofPickupItem.DefaultItemId) == 2,
            "missing door does not consume proof");
        Set(altar, "linkedDoor", door);
        Time.timeScale = 0; Check(!altar.TryOffer(), "pause blocks submission"); Time.timeScale = 1;
        actor.transform.position = Vector3.one * 100; Check(!altar.TryOffer(), "distant player rejected");
        actor.transform.position = new Vector3(0, -1.3f);
        var ghost = new GameObject("Ghost Operator").AddComponent<GhostZeldaCharacterData>();
        ghost.transform.position = actor.transform.position;
        var ghostMover = ghost.gameObject.AddComponent<ZeldaFourWayMover>();
        ZeldaRuntimeRegistry.ClaimControlledMover(ghostMover);
        Check(!altar.TryOffer(), "ghost cannot offer");
        Object.Destroy(ghost.gameObject); ZeldaRuntimeRegistry.ClaimControlledMover(mover); mover.ReceiveControl(Vector2.up);
        ZeldaInteractionArbiter.OfferInteraction(altar, mover, KeyCode.E, altar.transform.position, _ => { });
        typeof(ZeldaInteractionArbiter).GetMethod("ResolveCurrentFrame", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        Check(ZeldaInteractionArbiter.IsSelected(altar, mover, KeyCode.E), "altar uses shared E selection");
        bool committedBeforeEvent = false, reentered = false;
        Action observe = () => {
            committedBeforeEvent = altar.IsComplete && door.IsOpenRequested && inv.GetQuantity(TrialProofPickupItem.DefaultItemId) == 1;
            reentered |= altar.TryOffer();
        };
        inv.InventoryChanged += observe;
        Check(altar.TryOffer(out var success) && success == "你已证明了自己", "successful hand-in");
        inv.InventoryChanged -= observe;
        Check(committedBeforeEvent && !reentered, "save callbacks see atomic completed state and cannot reenter");
        Check(mover.IsUsingItemAttack, "raised-hand animation reused");
        Check(altar.IsComplete && visual.DisplaysOffering && door.IsOpenRequested, "offering opens linked sliding door");
        Check(inv.GetQuantity(TrialProofPickupItem.DefaultItemId) == 1 && inv.SelectedSlotIndex == 4, "exactly one proof consumed, selection unchanged");
        Check(!altar.TryOffer() && inv.GetQuantity(TrialProofPickupItem.DefaultItemId) == 1, "same-frame repeated interaction safe");
        mover.EndItemAttack();
        Check(!altar.TryOffer(out var done) && done == "你已证明了自己", "completed altar never accepts another proof");
        float end = Time.time + 1.1f; while (Time.time < end) yield return null;
        Check(door.IsFullyOpen, "door completes normal slide animation");
        Check(altar.InteractionMessage == "你已证明了自己", "permanent completion prompt");
        Check(altar.GetComponentsInChildren<SpriteRenderer>(true).Any(r => r.name == "Offered Trial Proof" && r.sprite != null), "proof ornament shown on altar");
        var prompt = Get<TextMesh>(altar, "prompt");
        Check(prompt != null && prompt.text == "你已证明了自己", "actual world prompt text updates");
        altar.transform.localScale = Vector3.one * .3f; Call(altar, "LateUpdate");
        Check(prompt.transform.lossyScale == Vector3.one, "prompt unaffected by altar scale");
        altar.transform.localScale = Vector3.one;

        // Serialize/restore real scene object records, not just the component's public boolean.
        var manager = SceneTravelStateManager.Instance;
        if (manager == null) manager = new GameObject("State Manager").AddComponent<SceneTravelStateManager>();
        var snapshot = Call(manager, "CreateSceneSnapshot", scene);
        var states = (IDictionary)snapshot.GetType().GetField("objects", F).GetValue(snapshot);
        foreach (var key in states.Keys.Cast<object>().ToArray())
        { var old = states[key]; states[key] = JsonUtility.FromJson(JsonUtility.ToJson(old), old.GetType()); }
        string disk = manager.CaptureDiskWorld();
        Check(disk.Contains("\"hasTrialProofAltar\":true") && disk.Contains("\"trialProofAltarState\":{\"offered\":true,"), "compact disk save retains offering state");
        Check(!disk.Contains("Generated Trial Altar") && !disk.Contains("Offered Trial Proof") && !disk.Contains("Trial Altar Interaction Prompt"), "transient visuals and prompt excluded from saves");
        altar.ApplyRuntimeState(default);
        Check(!altar.IsComplete && door.IsFullyClosed && !visual.DisplaysOffering, "fresh checkpoint restores unoffered state and closed gate");
        Check((bool)Call(manager, "RestoreSceneSnapshot", scene, snapshot, false), "scene-return snapshot restored");
        Call(manager, "FinalizeRestoredDevices", scene, snapshot);
        Check(altar.IsComplete && door.IsFullyOpen && visual.DisplaysOffering, "completed state and open gate survive JSON scene restore");
        Check(inv.GetQuantity(TrialProofPickupItem.DefaultItemId) == 1, "restoring completion does not consume another proof");
        altar.gameObject.SetActive(false); door.gameObject.SetActive(false);
        door.SetOpenImmediately(false);
        altar.ApplyRuntimeState(new TrialProofAltar.RuntimeState { offered = true });
        Check(door.IsOpenRequested && !altar.gameObject.activeSelf && !door.gameObject.activeSelf, "inactive streamed door restores without force-enabling either object");
        altar.gameObject.SetActive(true); door.gameObject.SetActive(true);
        Check(altar.IsComplete && door.IsFullyOpen && !altar.TryOffer(), "streaming reload keeps completion and prevents resubmission");
        yield return null;
        var empty = Object.Instantiate(prefab).GetComponent<TrialProofAltar>();
        empty.transform.position = new Vector3(-1.3f, 0); altar.transform.position = new Vector3(1.3f, 0);
        yield return null;
        Preview(empty.gameObject, altar.gameObject);
    }
    static void Preview(params GameObject[] roots)
    {
        foreach (var root in roots) foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 30;
        var cam = new GameObject("Altar Preview").AddComponent<Camera>();
        cam.transform.position = new Vector3(0, 0, -10); cam.orthographic = true; cam.orthographicSize = 1.3f;
        cam.cullingMask = 1 << 30; cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(.07f, .1f, .14f, 1);
        var rt = RenderTexture.GetTemporary(1040, 520, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt; cam.Render();
        var previous = RenderTexture.active; RenderTexture.active = rt;
        var image = new Texture2D(1040, 520, TextureFormat.RGBA32, false);
        image.ReadPixels(new Rect(0, 0, 1040, 520), 0, 0); image.Apply();
        File.WriteAllBytes(Path.Combine(Application.dataPath, "../TrialProofAltar-preview.png"), image.EncodeToPNG());
        RenderTexture.active = previous; cam.targetTexture = null; RenderTexture.ReleaseTemporary(rt);
        Object.Destroy(image); Object.Destroy(cam.gameObject);
    }
}
