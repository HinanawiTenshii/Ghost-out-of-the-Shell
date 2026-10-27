using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Tests the authored scene in a disposable copy; never rewrites its layout.
[InitializeOnLoad]
public static class DevlogPickupSmoke
{
    const string Key = nameof(DevlogPickupSmoke);
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static IEnumerator suite;
    static int frame = -1, checks;
    static double deadline;
    static DevlogPickupSmoke() { EditorApplication.update += Tick; }
    public static void RunBefore() => Begin(true);
    public static void Run() => Begin(false);
    static void Begin(bool before)
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/")) throw new Exception("Isolated project only");
        PlayerSettings.productName = "devlog-pickup-" + Guid.NewGuid().ToString("N");
        EditorSceneManager.OpenScene("Assets/Scenes/DevlogDemo.unity");
        SessionState.SetBool(Key, true); SessionState.SetBool(Key + ".before", before);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (suite == null) { suite = Verify(); deadline = EditorApplication.timeSinceStartup + 150; }
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Pickup test timeout");
            if (suite.MoveNext()) return;
            SessionState.SetBool(Key, false); Debug.Log("DEVLOG_PICKUP_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { SessionState.SetBool(Key, false); Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    static object Call(Type type, object target, string method, params object[] args) => type.GetMethod(method, F).Invoke(target, args);
    static IEnumerator Verify()
    {
        while (ZeldaRuntimeRegistry.GetControlledMover() == null || RetroSceneLoadReveal.IsBlockingInput ||
               RetroSceneLoadReveal.IsPreparingGameplay || GameSaveSystem.IsPreparingSceneSave) yield return null;
        for (int i = 0; i < 8; i++) yield return null;
        Debug.Log("PICKUP_BASE inventory=" + (PersistentInventory.Instance != null) + " instances=" + Object.FindObjectsOfType<PersistentInventory>(true).Length +
                  " blocked=" + DocumentReader.IsInputBlocked + " ghost=" + ZeldaRuntimeRegistry.GetControlledMover().GetComponent<ZeldaCharacterData>().IsGhostLike);
        var movers = Object.FindObjectsOfType<ZeldaFourWayMover>(true);
        var actor = movers.First(x => !x.GetComponent<ZeldaCharacterData>().IsGhostLike && x.gameObject.scene.IsValid());
        foreach (var ai in Object.FindObjectsOfType<ZeldaCharacterAiBase>()) ai.enabled = false;
        foreach (var mover in movers) mover.enabled = false;
        actor.gameObject.SetActive(true); actor.ReceiveControl(Vector2.right); ZeldaRuntimeRegistry.ClaimControlledMover(actor);
        ActiveZeldaCharacterDataSource.Instance.Refresh();
        Debug.Log("PICKUP_ACTOR " + actor.name + " pos=" + actor.transform.position + " source=" + ActiveZeldaCharacterDataSource.Instance.ActiveMover.name);
        var items = Object.FindObjectsOfType<PickupItemBase>(true).Where(x => x.gameObject.scene.IsValid()).ToArray();
        Debug.Log("PICKUP_ITEMS " + items.Length);
        foreach (var item in items)
        {
            bool active = item.gameObject.activeInHierarchy;
            item.gameObject.SetActive(true);
            bool reachable = false;
            foreach (Vector2 offset in new[] { Vector2.down, Vector2.up, Vector2.left, Vector2.right })
            {
                actor.transform.position = item.transform.position + (Vector3)(offset * 1.15f);
                actor.ReceiveControl(-offset); Physics2D.SyncTransforms();
                bool clear = ZeldaInteractionObstruction.HasClearPath(item, actor, item.transform.position);
                Call(typeof(PickupItemBase), item, "UpdatePickupPrompt", actor);
                Call(typeof(ZeldaInteractionArbiter), null, "ResolveCurrentFrame");
                bool selected = ZeldaInteractionArbiter.IsSelected(item, actor, KeyCode.E);
                Debug.Log("PICKUP_PROBE " + item.name + " active=" + active + " pos=" + item.transform.position + " side=" + offset + " clear=" + clear + " selected=" + selected);
                if (clear && selected) { reachable = true; break; }
                foreach (var hit in Physics2D.LinecastAll(actor.InteractionBodyCenter, item.transform.position))
                    if (hit.collider != null && !hit.collider.isTrigger)
                        Debug.Log("PICKUP_COLLIDER " + hit.collider.name + " layer=" + LayerMask.LayerToName(hit.collider.gameObject.layer) + " bounds=" + hit.collider.bounds);
            }
            bool picked = false;
            if (SessionState.GetBool(Key + ".before", false)) picked = item.TryPickUp();
            else
            {
                // Exercise the arbiter's final commit validation without
                // injecting keyboard input into the user's open editor.
                ZeldaInteractionArbiter.Submit(item, actor, KeyCode.E, item.transform.position, () => picked = item.TryPickUp());
                var requests = (IList)typeof(ZeldaInteractionArbiter).GetField("Requests", F).GetValue(null);
                Check(requests.Count == 1, "one pickup request queued");
                object request = requests[0]; requests.Clear();
                Check((bool)Call(typeof(ZeldaInteractionArbiter), null, "TryExecuteSelectedRequest", request), "reachable pickup request committed");
            }
            Debug.Log("PICKUP_RESULT " + item.name + " reachable=" + reachable + " picked=" + picked + " inventory=" + (PersistentInventory.Instance != null));
            if (!SessionState.GetBool(Key + ".before", false)) { Check(reachable, item.name + " has reachable prompt"); Check(picked, item.name + " enters inventory"); }
            yield return null;
            if (!SessionState.GetBool(Key + ".before", false)) Check(item == null, "picked item removed from scene");
            // First-time pickup opens the normal description window and blocks
            // E until it is dismissed. Simulate that dismissal before the next item.
            ItemDescriptionWindow.GetOrCreate().CloseWindow();
            while (DocumentReader.IsInputBlocked) yield return null;
        }
    }
}
