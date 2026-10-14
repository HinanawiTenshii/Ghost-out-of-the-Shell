using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Isolated play-mode coverage; never opens or saves an authored level.
[InitializeOnLoad]
public static class SandwormDeathRewardSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static IEnumerator suite;
    static int frame = -1, checks;
    static double deadline;
    static SandwormDeathRewardSmoke() { EditorApplication.update += Tick; }
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/")) throw new Exception("Isolated project required");
        PlayerSettings.productName = "sandworm-death-reward-smoke";
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(nameof(SandwormDeathRewardSmoke), true);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(SandwormDeathRewardSmoke), false))
            {
                SessionState.SetBool(nameof(SandwormDeathRewardSmoke), false);
                suite = Verify(); deadline = EditorApplication.timeSinceStartup + 90;
            }
            if (suite == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Reward test timed out");
            if (suite.MoveNext()) return;
            suite = null; Debug.Log("SANDWORM_DEATH_REWARD_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void Check(bool condition, string label)
    { checks++; if (!condition) throw new Exception(label); Debug.Log("REWARD_CHECK " + label); }
    static object Call(object owner, string method, params object[] args) => owner.GetType().GetMethod(method, F).Invoke(owner, args);
    static SandwormZeldaCharacterData Worm()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Level2NPCS/Sandworm-Boss.prefab");
        var go = Object.Instantiate(prefab); go.name = "Reward Test Worm";
        go.transform.position = new Vector3(12, -7, 0);
        go.GetComponent<SandwormBossPrototype>().ResetPose();
        return go.GetComponent<SandwormZeldaCharacterData>();
    }
    static TrialProofPickupItem[] Proofs() => Object.FindObjectsOfType<TrialProofPickupItem>();
    static void ClearRemains()
    { foreach (var sequence in Object.FindObjectsOfType<SandwormDeathSequence>()) Object.DestroyImmediate(sequence.gameObject); }
    static object Snapshot(SceneTravelStateManager manager, Scene scene)
    {
        var snapshot = Call(manager, "CreateSceneSnapshot", scene);
        var states = (IDictionary)snapshot.GetType().GetField("objects", F).GetValue(snapshot);
        foreach (var key in states.Keys.Cast<object>().ToArray())
        { var state = states[key]; states[key] = JsonUtility.FromJson(JsonUtility.ToJson(state), state.GetType()); }
        return snapshot;
    }
    static IEnumerator Verify()
    {
        var scene = SceneManager.CreateScene("Sandworm reward scene");
        var other = SceneManager.CreateScene("Other active scene");
        SceneManager.SetActiveScene(scene); Time.timeScale = 0;
        var template = Resources.Load<TrialProofPickupItem>("PickupItems/TrialProofPickupItem");
        Check(template != null && template.PickupQuantity == 1, "existing proof prefab resolves with quantity one");
        string previousIdentity = null;
        foreach (string mode in new[] { "Underground", "Ground", "Flight", "NoRemains" })
        {
            var data = Worm(); var motion = data.GetComponent<SandwormBossPrototype>();
            var attack = data.GetComponent<SandwormEruptionAttack>();
            if (mode == "Flight")
            {
                UnityEngine.Random.InitState(7491);
                Check(attack.TryLockAttack(new Vector2(20, -1)), "flight fixture locks target");
                for (int i = 0; i < 5000; i++)
                {
                    motion.Advance(.025f); attack.AdvanceAttack(.025f);
                    if (attack.EmergencePulses >= 4 && attack.Phase == SandwormEruptionAttack.AttackPhase.Traversing) break;
                }
                Check(attack.Phase == SandwormEruptionAttack.AttackPhase.Traversing && attack.EmergencePulses >= 4, "death fixture is airborne");
            }
            else
            {
                if (mode == "Ground") data.GetComponent<SandwormStateController>().EnterAboveGround();
                // Simulate a head far from the stationary root; drop must not use root position.
                motion.Head.position += new Vector3(9, 4, 0);
                if (mode == "NoRemains") typeof(ZeldaCharacterData).GetField("deathExplosionDuration", F).SetValue(data, 0f);
            }
            data.TakeDamage(1);
            Check(Proofs().Length == 0, mode + ": nonlethal damage does not drop reward");
            Vector3 head = motion.Head.position;
            Check(Vector3.Distance(head, data.transform.position) > 1f, mode + ": head differs from root");
            SceneManager.SetActiveScene(other);
            data.TakeDamage(999);
            Check(data.IsDead && Proofs().Length == 1, mode + ": one proof at confirmed death");
            var proof = Proofs().Single();
            Check(proof.gameObject.scene == scene && proof.transform.parent == null, mode + ": independent pickup in worm's scene");
            Check(Vector3.Distance(proof.transform.position, head) < .001f, mode + ": exact pre-cleanup head position");
            Check(proof.PickupQuantity == 1 && proof.transform.localScale == Vector3.one * template.ItemVisual.WorldScale, mode + ": normal single proof and size");
            Check(proof.UniqueInstanceId != previousIdentity && proof.GetComponent<SceneTravelStableId>() != null, mode + ": unique persistent reward");
            previousIdentity = proof.UniqueInstanceId;
            data.TakeDamage(999); Call(data, "Die");
            Check(Proofs().Length == 1, mode + ": repeated lethal calls do not duplicate");
            yield return null; yield return null;
            Check(data == null && proof != null, mode + ": reward survives boss destruction");
            ClearRemains();
            Check(proof != null, mode + ": clearing death effects does not remove reward");
            Object.DestroyImmediate(proof.gameObject); SceneManager.SetActiveScene(scene);
        }

        var manager = SceneTravelStateManager.Instance;
        if (manager == null) manager = new GameObject("Reward State Manager").AddComponent<SceneTravelStateManager>();
        var worm = Worm();
        Snapshot(manager, scene); // Assign the same authored-style identity used on scene return.
        string wormIdentity = worm.GetComponent<SceneTravelStableId>().StableId;
        worm.GetComponent<SandwormBossPrototype>().Head.position += new Vector3(6, 3, 0);
        Vector3 rewardPosition = worm.AttributeWindowAnchor.position;
        worm.TakeDamage(999);
        var reward = Proofs().Single(); string rewardIdentity = reward.UniqueInstanceId;
        string duringDeath = manager.CaptureDiskWorld();
        Check(duringDeath.Contains(rewardIdentity) && duringDeath.Contains("trial_proof"), "save during death animation already includes reward");
        yield return null; yield return null; ClearRemains();
        var saved = Snapshot(manager, scene);
        Object.DestroyImmediate(reward.gameObject);
        var revisited = Worm(); revisited.gameObject.AddComponent<SceneTravelStableId>().Initialize(wormIdentity);
        Check((bool)Call(manager, "RestoreSceneSnapshot", scene, saved, false), "JSON scene snapshot restored");
        reward = Proofs().Single();
        Check(!revisited.gameObject.activeSelf, "defeated boss remains absent on scene return");
        Check(reward.UniqueInstanceId == rewardIdentity && Vector3.Distance(reward.transform.position, rewardPosition) < .001f,
            "uncollected proof restores with same identity and position");
        Call(manager, "RestoreSceneSnapshot", scene, saved, false);
        Check(Proofs().Length == 1, "repeated restore does not duplicate reward");

        var collector = new GameObject("Reward collector").AddComponent<CivilianZeldaCharacterData>();
        collector.transform.position = rewardPosition;
        var mover = collector.gameObject.AddComponent<ZeldaFourWayMover>(); ZeldaRuntimeRegistry.ClaimControlledMover(mover);
        var invObject = new GameObject("Reward Inventory"); invObject.SetActive(false);
        var inventory = invObject.AddComponent<PersistentInventory>();
        typeof(PersistentInventory).GetField("saveToPlayerPrefs", F).SetValue(inventory, false);
        invObject.SetActive(true); inventory.enabled = false; inventory.ClearAll();
        Check(reward.TryPickUp() && inventory.GetQuantity(TrialProofPickupItem.DefaultItemId) == 1, "death reward is collectible");
        Check(!reward.TryPickUp(), "same-frame double pickup rejected");
        ItemDescriptionWindow.GetOrCreate().CloseWindow();
        var inventorySaved = JsonUtility.FromJson<PersistentInventory.RuntimeState>(JsonUtility.ToJson(inventory.CaptureRuntimeState()));
        inventory.ClearAll(); inventory.ApplyRuntimeState(inventorySaved);
        Check(inventory.ContainsItemInstance(rewardIdentity), "inventory JSON restore keeps reward identity");
        yield return null; yield return null;
        var collectedSnapshot = Snapshot(manager, scene);
        Call(manager, "RestoreSceneSnapshot", scene, collectedSnapshot, false);
        Check(Proofs().Length == 0 && inventory.GetQuantity(TrialProofPickupItem.DefaultItemId) == 1,
            "collected reward does not respawn on scene restore");
        var deadState = revisited.CaptureRuntimeState(); deadState.currentHealth = 0;
        revisited.ApplyRuntimeState(deadState); Call(revisited, "Die");
        Check(Proofs().Length == 0, "restoring already-dead state never awards again");
    }
}
