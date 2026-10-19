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

/// <summary>Run in the disposable Unity project only; never writes to the game's save directory.</summary>
[InitializeOnLoad]
public static class SkillDiscoveryPersistenceSmoke
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Stack<IEnumerator> routines;
    static int frame = -1, checks;
    static double deadline;
    static SkillDiscoveryPersistenceSmoke() { EditorApplication.update += Tick; }

    public static void Run()
    {
        Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "isolated project only");
        PlayerSettings.productName = "skill-discovery-persistence-smoke";
        EditorSceneManager.OpenScene("Assets/Scenes/Level2/Level2-Floor-1.unity");
        SessionState.SetBool(nameof(SkillDiscoveryPersistenceSmoke), true);
        EditorApplication.EnterPlaymode();
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(SkillDiscoveryPersistenceSmoke), false))
            {
                SessionState.SetBool(nameof(SkillDiscoveryPersistenceSmoke), false);
                routines = new Stack<IEnumerator>(); routines.Push(Verify());
                deadline = EditorApplication.timeSinceStartup + 480;
            }
            if (routines == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("smoke test timeout");
            while (routines.Count > 0)
            {
                var step = routines.Peek();
                if (!step.MoveNext()) { routines.Pop(); continue; }
                if (step.Current is IEnumerator child) { routines.Push(child); continue; }
                return;
            }
            routines = null; Debug.Log("SKILL_DISCOVERY_PERSISTENCE_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception ex) { routines = null; Debug.LogException(ex); EditorApplication.Exit(1); }
    }

    static void Check(bool value, string label)
    {
        checks++;
        if (!value) throw new Exception(label);
    }

    static IEnumerator Ready(string expectedScene)
    {
        yield return null; yield return null;
        while (SceneManager.GetActiveScene().name != expectedScene || RetroSceneLoadReveal.IsBlockingInput ||
            GameSaveSystem.IsPreparingSceneSave || GameSaveSystem.IsLoading ||
            SceneTravelStateManager.GetOrCreate().IsSceneTravelRestoreInProgress || SceneTravelStateManager.GetOrCreate().IsQuickRestartPending)
            yield return null;
        yield return null;
    }

    static PlayerGrowthAttributes CheckService(string label)
    {
        var growth = PlayerGrowthAttributes.Instance;
        Check(growth != null && growth.isActiveAndEnabled, label + " growth available");
        Check(growth.transform.parent == null && growth.gameObject.scene.name == "DontDestroyOnLoad", label + " growth is persistent root");
        Check(Object.FindObjectsOfType<PlayerGrowthAttributes>(true).Length == 1, label + " no duplicate growth services");
        Check(!Object.FindObjectsOfType<Transform>(true).Any(t => t.name == "SceneAssets" && t.gameObject.scene.name == "DontDestroyOnLoad"), label + " scene container is not retained");
        return growth;
    }

    static string PossessSceneSkill(string requiredId)
    {
        var target = Object.FindObjectsOfType<ZeldaCharacterData>(true).FirstOrDefault(d =>
            d.gameObject.scene == SceneManager.GetActiveScene() && !d.IsDead && !string.IsNullOrEmpty(d.NativeCharacterSkillId) &&
            (requiredId == null || d.NativeCharacterSkillId == requiredId) && d.GetComponent<ZeldaFourWayMover>() != null &&
            !d.GetComponent<ZeldaFourWayMover>().enabled && (d.GetComponent<ZeldaCharacterAiBase>() == null || d.GetComponent<ZeldaCharacterAiBase>().IsInPossessableState));
        Check(target != null, "authored skill bearer exists: " + SceneManager.GetActiveScene().name);
        string id = target.NativeCharacterSkillId;
        Possess(target);
        var growth = CheckService("after possession");
        var skill = Array.Find(PlayerGrowthAttributes.Skills, s => s.id == id);
        Check(growth.IsSkillDiscovered(skill), id + " becomes learnable");
        Check(!growth.HasSkill(id), "discovery does not buy " + id);

        var panel = new GameObject("Smoke Skill Tree", typeof(RectTransform), typeof(Canvas)).AddComponent<CharacterSkillPage>();
        panel.Build(Resources.Load<LevelSettlementCatalog>("LevelSettlementCatalog").font);
        typeof(CharacterSkillPage).GetField("selectedId", Flags).SetValue(panel, id);
        typeof(CharacterSkillPage).GetMethod("Refresh", Flags).Invoke(panel, null);
        Check(((Text)typeof(CharacterSkillPage).GetField("title", Flags).GetValue(panel)).text == skill.title, "skill tree shows discovered title");
        Check(!((Button)typeof(CharacterSkillPage).GetField("upgradeButton", Flags).GetValue(panel)).interactable, "DATA cost still enforced");
        Object.Destroy(panel.gameObject);

        var state = growth.CaptureRuntimeState(); state.collectibleCount = 20; growth.ApplyRuntimeState(state);
        Check(growth.TryUpgradeSkill(id), "discovered skill can be purchased");
        Check(growth.CollectibleCount == 20 - skill.cost, "purchase spends authored DATA cost");
        Debug.Log("SKILL_DISCOVERY_CASE possessed " + target.name + " / " + id);
        return id;
    }

    static void Possess(ZeldaCharacterData target)
    {
        var player = ZeldaRuntimeRegistry.GetControlledMover();
        Check(player != null, "controlled actor exists");
        target.gameObject.SetActive(true);
        var ai = target.GetComponent<ZeldaCharacterAiBase>(); if (ai != null) ai.enabled = false;
        player.transform.position = new Vector3(1000, 1000, 0); target.transform.position = new Vector3(1000, 1001, 0);
        var bodyState = player.CharacterData.CaptureRuntimeState(); bodyState.possessionEnergy = 100; player.CharacterData.ApplyRuntimeState(bodyState);
        Physics2D.SyncTransforms();
        Check((bool)typeof(ZeldaFourWayMover).GetMethod("TrySwitchControl", Flags).Invoke(player, new object[] { target.GetComponent<ZeldaFourWayMover>() }), "real possession transfer succeeds");
    }

    static IEnumerator TravelAndCheck(string destination, PlayerGrowthAttributes original, string snapshot)
    {
        SceneTravelStateManager.GetOrCreate().TravelToScene(destination, Vector2.zero);
        yield return Ready(destination);
        var growth = CheckService(destination);
        Check(growth == original, destination + " retains the same growth object");
        Check(JsonUtility.ToJson(growth.CaptureRuntimeState()) == snapshot, destination + " retains discoveries, learned skills, DATA and equipment");
        Debug.Log("SKILL_DISCOVERY_CASE travel preserved " + destination);
    }

    static IEnumerator ColdLoadAndTravel(string savedScene, string nextScene, string snapshot)
    {
        Check(GameSaveSystem.Instance.Save(1, false), "save actual progress");
        SceneManager.LoadScene("TitleScreen"); yield return Ready("TitleScreen");
        Object.Destroy(PlayerGrowthAttributes.Instance.gameObject); yield return null;
        Check(PlayerGrowthAttributes.Instance == null, "simulate fresh application's missing service");
        Check(GameSaveSystem.Instance.Load(1), "load actual save without existing growth service");
        yield return Ready(savedScene);
        var restored = CheckService("cold load " + savedScene);
        Check(JsonUtility.ToJson(restored.CaptureRuntimeState()) == snapshot, "cold load restores exact growth state");
        yield return TravelAndCheck(nextScene, restored, snapshot);
    }

    static IEnumerator Verify()
    {
        yield return Ready("Level2-Floor-1");
        var direct = CheckService("direct Level2");
        PossessSceneSkill("builder");
        string snapshot = JsonUtility.ToJson(direct.CaptureRuntimeState());
        yield return TravelAndCheck("Level2-Floor1", direct, snapshot);
        yield return TravelAndCheck("Level2-Floor2", direct, snapshot);
        yield return TravelAndCheck("Level2-Desert", direct, snapshot);
        yield return TravelAndCheck("Level2-Floor-1", direct, snapshot);
        yield return ColdLoadAndTravel("Level2-Floor-1", "Level2-Floor1", snapshot);

        SceneManager.LoadScene("TitleScreen"); yield return Ready("TitleScreen");
        Check(GameSaveSystem.Instance.StartNewGame(0), "normal new-game entry");
        yield return Ready("TutorialLevel");
        var normal = CheckService("normal entry");
        Check(normal.CollectibleCount == 0 && normal.CaptureRuntimeState().discoveredCharacterSkills.Length == 0 && !normal.HasSkill("builder"), "new game clears previous growth");
        snapshot = JsonUtility.ToJson(normal.CaptureRuntimeState());
        yield return TravelAndCheck("Level2-Floor-1", normal, snapshot);
        PossessSceneSkill("builder"); snapshot = JsonUtility.ToJson(normal.CaptureRuntimeState());
        yield return TravelAndCheck("Level2-Floor1", normal, snapshot);

        // Direct scene startup without the previous session's persistent service.
        SceneManager.LoadScene("TitleScreen"); yield return Ready("TitleScreen");
        Object.Destroy(PlayerGrowthAttributes.Instance.gameObject); yield return null;
        SceneManager.LoadScene("Level1-Floor1"); yield return Ready("Level1-Floor1");
        var level1 = CheckService("direct Level1");
        PossessSceneSkill(null); snapshot = JsonUtility.ToJson(level1.CaptureRuntimeState());
        yield return TravelAndCheck("Level1-Floor2", level1, snapshot);
        yield return TravelAndCheck("Level1-Floor3", level1, snapshot);
        yield return TravelAndCheck("Level1-Floor-1", level1, snapshot);
        yield return TravelAndCheck("Level1-Garden", level1, snapshot);
        yield return ColdLoadAndTravel("Level1-Garden", "Level1-Floor1", snapshot);

        // A custom test scene without PlayerStates must still record possession.
        SceneManager.LoadScene("TitleScreen"); yield return Ready("TitleScreen");
        Object.Destroy(PlayerGrowthAttributes.Instance.gameObject); yield return null;
        var fixture = SceneManager.CreateScene("Growth Missing Service Fixture"); SceneManager.SetActiveScene(fixture);
        var source = new GameObject("Source"); source.AddComponent<SwordZeldaCharacterData>(); source.AddComponent<ZeldaFourWayMover>();
        var target = new GameObject("Builder"); var data = target.AddComponent<SwordZeldaCharacterData>();
        target.AddComponent<BuilderCharacterSkillData>(); target.AddComponent<ZeldaFourWayMover>().enabled = false;
        Check(PlayerGrowthAttributes.Instance == null, "fixture has no growth bootstrap");
        Possess(data);
        Check(CheckService("possession fallback").IsSkillDiscovered(Array.Find(PlayerGrowthAttributes.Skills, s => s.id == "builder")), "missing service is created and discovery recorded");
    }
}
