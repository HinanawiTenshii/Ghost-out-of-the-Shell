using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class NearbyHostilitySmoke
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly List<GameObject> objects = new List<GameObject>();
    static ZeldaFourWayMover player;
    static int checks;
    static NearbyHostilitySmoke() { EditorApplication.update += Tick; }
    public static void Run() { SessionState.SetBool("NearbyHostilitySmoke", true); EditorApplication.EnterPlaymode(); }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || !SessionState.GetBool("NearbyHostilitySmoke", false)) return;
        SessionState.SetBool("NearbyHostilitySmoke", false);
        try { Verify(); Clear(); Debug.Log("NEARBY_HOSTILITY_PASS checks=" + checks); EditorApplication.Exit(0); }
        catch (Exception e) { Debug.LogException(e); Clear(); EditorApplication.Exit(1); }
    }
    static void Check(bool value, string label) { checks++; if (!value) throw new Exception(label); }
    static void Set(ZeldaCharacterAiBase ai, string field, object value) => typeof(ZeldaCharacterAiBase).GetField(field, Flags).SetValue(ai, value);
    static void State(ZeldaCharacterAiBase ai, ZeldaAiState value) => typeof(ZeldaCharacterAiBase).GetMethod("ChangeState", Flags).Invoke(ai, new object[] { value, true });
    static void Hostile(ZeldaCharacterAiBase ai, ZeldaFourWayMover target = null) => typeof(ZeldaCharacterAiBase).GetMethod("BeginTemporaryHostility", Flags).Invoke(ai, new object[] { target == null ? player : target, true });
    static void Clear() { for (int i = objects.Count - 1; i >= 0; i--) if (objects[i] != null) Object.DestroyImmediate(objects[i]); objects.Clear(); }
    static GameObject Actor(string name, Vector2 position)
    {
        var go = new GameObject(name, typeof(CivilianZeldaCharacterData), typeof(ZeldaFourWayMover));
        objects.Add(go); go.transform.position = position; return go;
    }
    static ZeldaCharacterAiBase Npc(float x, float y = 0, Type type = null)
    {
        var go = Actor("NPC", new Vector2(x, y)); go.GetComponent<ZeldaFourWayMover>().enabled = false;
        var ai = (ZeldaCharacterAiBase)go.AddComponent(type ?? typeof(ZeldaCharacterAiBase));
        Set(ai, "enableAwarenessSound", false); return ai;
    }
    static void Setup()
    {
        Clear(); Time.timeScale = 0;
        player = Actor("Player", new Vector2(20, 0)).GetComponent<ZeldaFourWayMover>();
        Check(ZeldaRuntimeRegistry.GetControlledMover() == player, "fixture controlled player");
    }
    static void Verify()
    {
        Setup(); var source = Npc(0); var near = Npc(2); var edge = Npc(4); var beyond = Npc(4.01f); var chain = Npc(7.9f);
        var diagonal = Npc(3, 3); var behind = Npc(-3);
        Hostile(source);
        Check(near.IsActivelyHostileTo(player) && edge.IsActivelyHostileTo(player) && behind.IsActivelyHostileTo(player), "all within circle join immediately without seeing player");
        Check(beyond.CurrentState == ZeldaAiState.Idle && diagonal.CurrentState == ZeldaAiState.Idle, "true circular radius 4 boundary");
        Check(chain.CurrentState == ZeldaAiState.Idle, "no recursive cascade");
        Set(near, "attackCooldownTimer", .31f);
        for (int i = 0; i < 50; i++) { State(source, ZeldaAiState.Hostile); State(edge, ZeldaAiState.Hostile); }
        Check(chain.CurrentState == ZeldaAiState.Idle, "recipient does not rebroadcast on later hostile ticks");
        Check((float)typeof(ZeldaCharacterAiBase).GetField("attackCooldownTimer", Flags).GetValue(near) == .31f, "existing hostile attack cycle not reset");
        var late = Npc(1); State(source, ZeldaAiState.Hostile);
        Check(late.CurrentState == ZeldaAiState.Idle, "no continuous radius aggro while remaining hostile");
        State(source, ZeldaAiState.Recovery); Hostile(source);
        Check(late.IsActivelyHostileTo(player), "genuine re-entry can notify again");

        Setup(); source = Npc(0); var disabled = Npc(1); disabled.enabled = false;
        var stunned = Npc(1); stunned.StunForDuration(5);
        var commanded = Npc(1); Set(commanded, "royalCommandRemaining", 4f);
        var locked = Npc(1); Set(locked, "possessionLocked", true);
        var skeleton = Npc(1, 0, typeof(SkeletonCharacterAi));
        var machine = Npc(1, 0, typeof(AutomatonCharacterAi));
        var otherScene = Npc(1); SceneManager.MoveGameObjectToScene(otherScene.gameObject, SceneManager.CreateScene("Unrelated NPC Scene"));
        Hostile(source);
        Check(disabled.CurrentState == ZeldaAiState.Idle && stunned.IsStunned && commanded.CurrentState == ZeldaAiState.Idle && locked.CurrentState == ZeldaAiState.Idle, "disabled and protected states untouched");
        Check(skeleton.CurrentState == ZeldaAiState.Idle && machine.CurrentState == ZeldaAiState.Idle, "no skeleton/automaton recruitment or activation");
        Check(otherScene.CurrentState == ZeldaAiState.Idle, "no cross-scene notification");

        Setup(); skeleton = Npc(0, 0, typeof(SkeletonCharacterAi)); near = Npc(1); Hostile(skeleton);
        Check(!near.IsActivelyHostileTo(player), "skeleton hostility does not incriminate player");
        Setup(); machine = Npc(0, 0, typeof(AutomatonCharacterAi)); near = Npc(1);
        ((AutomatonCharacterAi)machine).ActivateHostility(); Hostile(machine);
        Check(!near.IsActivelyHostileTo(player), "activated automaton does not share hostility");

        Setup(); source = Npc(0); near = Npc(1); var targetNpc = Npc(20);
        Hostile(source, targetNpc.GetComponent<ZeldaFourWayMover>());
        Check(near.CurrentState == ZeldaAiState.Idle, "NPC-target hostility does not alert against player");
        Hostile(source);
        Check(near.IsActivelyHostileTo(player), "hostile NPC switching target to player triggers once");

        Setup(); source = Npc(0); near = Npc(1); State(source, ZeldaAiState.Alert);
        Check(near.CurrentState == ZeldaAiState.Idle, "warning alone does not spread"); Hostile(source);
        Check(near.IsActivelyHostileTo(player), "warning to hostile spreads");

        Setup(); source = Npc(0); near = Npc(1);
        var saved = source.CaptureSaveState(); saved.state = ZeldaAiState.Hostile; saved.regular = true; saved.targetsPlayer = true;
        source.ApplySaveState(saved); State(source, ZeldaAiState.Hostile);
        Check(near.CurrentState == ZeldaAiState.Idle, "restored hostility does not replay notification");

        Setup(); source = Npc(0); near = Npc(3); beyond = Npc(4.1f);
        source.OnCharacterDamagedBy(player.CharacterData);
        Check(source.IsActivelyHostileTo(player) && near.IsActivelyHostileTo(player) && beyond.CurrentState == ZeldaAiState.Idle,
            "actual damage retaliation notifies only nearby NPCs");
        Setup(); source = Npc(0); near = Npc(3);
        player.transform.position = new Vector2(0, -2);
        Set(source, "visionObstacleLayers", (LayerMask)0);
        source.OnPlayerPlacedBomb(player.CharacterData);
        Check(source.IsActivelyHostileTo(player) && near.IsActivelyHostileTo(player), "actual witnessed bomb placement propagates");
    }
}
