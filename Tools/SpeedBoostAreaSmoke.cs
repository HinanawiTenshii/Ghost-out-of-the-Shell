using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class SpeedBoostAreaSmoke
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly List<GameObject> owned = new List<GameObject>();
    static int checks;
    static SpeedBoostAreaSmoke() { EditorApplication.update += Tick; }
    public static void Run() { SessionState.SetBool("SpeedBoostAreaSmoke", true); EditorApplication.EnterPlaymode(); }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || !SessionState.GetBool("SpeedBoostAreaSmoke", false)) return;
        SessionState.SetBool("SpeedBoostAreaSmoke", false);
        var oldSimulation = Physics2D.simulationMode;
        try
        {
            Physics2D.simulationMode = SimulationMode2D.Script; Time.timeScale = 1;
            Verify(); Debug.Log("SPEED_BOOST_ALL_CHARACTERS_PASS checks=" + checks);
            Clear(); Physics2D.simulationMode = oldSimulation; EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); Clear(); Physics2D.simulationMode = oldSimulation; EditorApplication.Exit(1); }
    }
    static void Clear() { for (int i = owned.Count - 1; i >= 0; i--) if (owned[i]) Object.DestroyImmediate(owned[i]); owned.Clear(); }
    static void Check(bool condition, string label) { checks++; if (!condition) throw new Exception(label); }
    static void Set(object obj, Type type, string field, object value) => type.GetField(field, Flags).SetValue(obj, value);
    static GameObject Make(string name) { var go = new GameObject(name); owned.Add(go); return go; }
    static GameObject Actor(string name, Vector2 position)
    {
        var go = Make(name); go.transform.position = position;
        go.AddComponent<CivilianZeldaCharacterData>(); go.AddComponent<ZeldaFourWayMover>(); return go;
    }
    static float Step(Component driver, Vector2 position)
    {
        var rb = driver.GetComponent<Rigidbody2D>();
        rb.velocity = Vector2.zero; rb.position = position; driver.transform.position = position;
        Type type = driver is ZeldaCharacterAiBase ? typeof(ZeldaCharacterAiBase) : typeof(ZeldaFourWayMover);
        Set(driver, type, "moveDirection", Vector2.right);
        if (driver is ZeldaCharacterAiBase)
        {
            Set(driver, type, "previousFixedPosition", position); Set(driver, type, "cornerStuckTimer", 0f);
        }
        Physics2D.SyncTransforms();
        type.GetMethod("FixedUpdate", Flags).Invoke(driver, null);
        Physics2D.Simulate(Time.fixedDeltaTime);
        return rb.position.x - position.x;
    }
    static void Verify()
    {
        var player = Actor("Player", Vector2.zero).GetComponent<ZeldaFourWayMover>();
        var npc = Actor("NPC", new Vector2(0, 3)); npc.GetComponent<ZeldaFourWayMover>().enabled = false;
        var ai = npc.AddComponent<ZeldaCharacterAiBase>();
        Set(ai, typeof(ZeldaCharacterAiBase), "solidCollisionLayers", (LayerMask)0);
        Set(ai, typeof(ZeldaCharacterAiBase), "enableAwarenessSound", false);
        var zone = Make("Area").AddComponent<SpeedBoostArea>();
        var config = new SerializedObject(zone); config.FindProperty("areaSize").vector2Value = Vector2.one * 20;
        config.ApplyModifiedPropertiesWithoutUndo(); Physics2D.SyncTransforms();
        float npcBase = Step(ai, new Vector2(40, 3));
        foreach (ZeldaAiState state in new[] { ZeldaAiState.Idle, ZeldaAiState.Hostile, ZeldaAiState.Search, ZeldaAiState.Recovery })
        {
            Set(ai, typeof(ZeldaCharacterAiBase), "currentState", state);
            float inside = Step(ai, new Vector2(0, 3));
            Check(npcBase > .001f && Mathf.Abs(inside - npcBase * 2) < .0001f, state + " AI actual movement doubles: " + inside + " / " + npcBase);
        }
        float playerBase = Step(player, new Vector2(40, 0));
        float playerBoosted = Step(player, Vector2.zero);
        Check(playerBase > .001f && Mathf.Abs(playerBoosted - playerBase * 2) < .0001f, "player is 2x, not 4x");
        Check(Mathf.Abs(Step(ai, new Vector2(40, 3)) - npcBase) < .0001f, "AI restores speed on exit");
        Check(SpeedBoostArea.GetSpeedMultiplier(npc.GetComponent<ZeldaFourWayMover>()) == 1, "outside query");
        npc.transform.position = new Vector2(0, 3); npc.GetComponent<Rigidbody2D>().position = npc.transform.position;
        Check(SpeedBoostArea.GetSpeedMultiplier(npc.GetComponent<ZeldaFourWayMover>()) == 2, "disabled player-input mover is eligible inside");
        var secondZone = Object.Instantiate(zone); owned.Add(secondZone.gameObject); Physics2D.SyncTransforms();
        Check(Mathf.Abs(Step(player, Vector2.zero) - playerBoosted) < .0001f, "overlapping areas do not stack");
        secondZone.enabled = false; zone.enabled = false;
        Check(Mathf.Abs(Step(player, Vector2.zero) - playerBase) < .0001f, "disabling zones restores original speed");
        zone.enabled = true;

        var puppet = Make("Puppet").AddComponent<ClockworkPuppetRuntime>();
        Set(puppet, typeof(ClockworkPuppetRuntime), "moveSpeed", 1.35f);
        float puppetSpeed = (float)typeof(ClockworkPuppetRuntime).GetProperty("AreaMoveSpeed", Flags).GetValue(puppet);
        Check(Mathf.Abs(puppetSpeed - 2.7f) < .0001f, "independently controlled puppet boosted");

        var worm = Make("Worm").AddComponent<SandwormBossPrototype>();
        var head = Make("Head").transform; head.SetParent(worm.transform);
        Set(worm, typeof(SandwormBossPrototype), "head", head);
        worm.transform.position = new Vector2(40, 0); head.position = Vector3.zero;
        Check(Mathf.Abs(worm.WorldMoveSpeed - 6f) < .0001f, "sandworm uses moving head, not fixed root, for area membership");
        head.position = new Vector2(40, 0); Check(Mathf.Abs(worm.WorldMoveSpeed - 3f) < .0001f, "sandworm restores speed on exit");

        var otherScene = SceneManager.CreateScene("OtherFloorBoostTest");
        SceneManager.MoveGameObjectToScene(npc, otherScene);
        Check(SpeedBoostArea.GetSpeedMultiplier(ai) == 1, "another loaded scene cannot boost NPC");
        Object.DontDestroyOnLoad(npc);
        Check(SpeedBoostArea.GetSpeedMultiplier(ai) == 2, "travel-persistent character uses active gameplay scene");
        npc.SetActive(false); Check(SpeedBoostArea.GetSpeedMultiplier(ai) == 1, "inactive body ignored");
    }
}
