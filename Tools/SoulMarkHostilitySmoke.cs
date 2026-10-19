using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Run in the disposable Unity project; exercises the actual skill entry point.
[InitializeOnLoad]
public static class SoulMarkHostilitySmoke
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static GameObject root;
    static ZeldaFourWayMover player;
    static int checks;
    static SoulMarkHostilitySmoke() { EditorApplication.update += Tick; }
    public static void Run()
    {
        Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "isolated project required");
        PlayerSettings.productName = "soul-mark-hostility-smoke";
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(nameof(SoulMarkHostilitySmoke), true); EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || !SessionState.GetBool(nameof(SoulMarkHostilitySmoke), false)) return;
        SessionState.SetBool(nameof(SoulMarkHostilitySmoke), false);
        try { Verify(); Clear(); Debug.Log("SOUL_MARK_HOSTILITY_PASS checks=" + checks); EditorApplication.Exit(0); }
        catch (Exception ex) { Debug.LogException(ex); Clear(); EditorApplication.Exit(1); }
    }
    static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    static void Set(object target, string name, object value)
    {
        for (Type type = target.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(name, Flags);
            if (field != null) { field.SetValue(target, value); return; }
        }
        throw new Exception("Missing field " + name);
    }
    static void CallAi(ZeldaCharacterAiBase ai, string name, params object[] args) =>
        typeof(ZeldaCharacterAiBase).GetMethod(name, Flags).Invoke(ai, args);
    static void Clear()
    {
        foreach (var mark in Object.FindObjectsOfType<SoulMarkRuntime>(true)) Object.DestroyImmediate(mark.gameObject);
        if (root != null) Object.DestroyImmediate(root);
        foreach (var timer in Object.FindObjectsOfType<SharedPlayerHostilityTimer>(true)) Object.DestroyImmediate(timer.gameObject);
    }
    static ZeldaFourWayMover Body(string name, Vector2 position, bool controlled = false)
    {
        var go = new GameObject(name); go.transform.SetParent(root.transform); go.transform.position = position;
        var data = go.AddComponent<SwordZeldaCharacterData>();
        var mover = go.AddComponent<ZeldaFourWayMover>(); mover.enabled = controlled;
        var state = data.CaptureRuntimeState(); state.health = state.currentHealth = 50;
        state.permissionLevel = 0; state.possessionEnergy = 20; state.possessionCost = 2;
        data.ApplyRuntimeState(state);
        return mover;
    }
    static ZeldaCharacterAiBase Npc(string name, Vector2 position)
    {
        var mover = Body(name, position);
        var ai = mover.gameObject.AddComponent<ZeldaCharacterAiBase>();
        Set(ai, "enableAwarenessSound", false); Set(ai, "visionRadius", 10f); Set(ai, "visionAngle", 360f);
        Set(ai, "visionObstacleLayers", (LayerMask)(1 << LayerMask.NameToLayer("Blocks")));
        return ai;
    }
    static void Setup(int rank = 1)
    {
        Clear(); Time.timeScale = 0;
        root = new GameObject("Soul Mark Test Actors");
        PlayerGrowthAttributes.GetOrCreate().ApplyRuntimeState(new PlayerGrowthAttributes.RuntimeState {
            unlockedSkills = rank == 3 ? new[] { "soul_mark", "soul_mark_2", "soul_mark_3" } :
                rank == 2 ? new[] { "soul_mark", "soul_mark_2" } : new[] { "soul_mark" }
        });
        player = Body("Caster", Vector2.zero, true); Set(player, "facingDirection", Vector2.right);
    }
    static void Sync()
    {
        ZeldaRuntimeRegistry.ClaimControlledMover(player);
        Physics2D.SyncTransforms(); ZeldaRuntimeRegistry.InvalidateSpatialSnapshot();
    }
    static bool Pending => SoulMarkRuntime.HasPendingMarkInScene(SceneManager.GetActiveScene());
    static TurretCharacterAi Turret(Vector2 position, ZeldaCharacterData owner = null)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Level2NPCS/Turret-level2.prefab");
        var turret = Object.Instantiate(prefab, position, Quaternion.identity, root.transform).GetComponent<TurretCharacterAi>();
        Set(turret, "enableAwarenessSound", false);
        if (owner != null) turret.BindCharacter(owner);
        return turret;
    }
    static void Verify()
    {
        for (int rank = 1; rank <= 3; rank++)
        {
            Setup(rank);
            var victim = Npc("Marked target", Vector2.right);
            Set(victim, "visionAngle", 60f); Set(victim, "peripheralVisionAngle", 60f);
            Set(victim, "facingDirection", Vector2.right);
            var data = victim.GetComponent<ZeldaCharacterData>();
            var state = data.CaptureRuntimeState(); state.holyShield = new HolyShieldRuntime.State { charges = 3, capacity = 3 };
            data.ApplyRuntimeState(state);
            var nearby = Npc("Nearby ally", new Vector2(2, 2));
            var witness = Npc("Distant witness", new Vector2(0, 6));
            var blind = Npc("Looking away", new Vector2(0, -7));
            Set(blind, "visionAngle", 60f); Set(blind, "peripheralVisionAngle", 60f); Set(blind, "facingDirection", Vector2.down);
            var blocked = Npc("Behind wall", new Vector2(6, 0));
            var wall = new GameObject("Blocks wall"); wall.transform.SetParent(root.transform);
            wall.layer = LayerMask.NameToLayer("Blocks"); wall.transform.position = new Vector3(3.5f, 0);
            wall.AddComponent<BoxCollider2D>().size = new Vector2(.3f, 3);
            int attacks = 0, damageEvents = 0;
            data.AttackedBy += _ => attacks++;
            data.HealthDamaged += (d, amount, attacker) => damageEvents++;
            Sync(); Check(player.GetSoulMarkCandidate() == data.GetComponent<ZeldaFourWayMover>(), "rank " + rank + " valid real target");
            int before = player.CharacterData.FinalPossessionEnergy;
            SoulMarkRuntime.Use(player);
            Check(Pending && victim.IsActivelyHostileTo(player), "rank " + rank + " successful mark provokes victim even from behind");
            Check(nearby.IsActivelyHostileTo(player) && witness.IsActivelyHostileTo(player), "existing nearby alert and visible witness rules applied");
            Check(blind.CurrentState == ZeldaAiState.Idle && blocked.CurrentState == ZeldaAiState.Idle, "unaware distant and occluded NPCs unaffected");
            Check(data.CurrentHealth == 50 && data.HolyShieldCharges == 3 && damageEvents == 0 && !data.IsShowingDamageFeedback, "no damage, shield loss, hit feedback or damage counters");
            Check(attacks == 1, "one hostile-action notification per successful mark");
            Check(before - player.CharacterData.FinalPossessionEnergy == (rank == 3 ? 1 : 2), "existing rank energy cost retained");
            Check(!victim.IsInPossessableState && Pending, "retaliation does not invalidate established destination");
            var runtime = Object.FindObjectOfType<SoulMarkRuntime>();
            for (int i = 0; i < 4; i++) typeof(SoulMarkRuntime).GetMethod("LateUpdate", Flags).Invoke(runtime, null);
            Check(attacks == 1 && Pending, "mark maintenance does not repeat offence");
        }

        Setup(); var target = Npc("Target", Vector2.right); int events = 0;
        target.GetComponent<ZeldaCharacterData>().AttackedBy += _ => events++;
        player.CharacterData.TrySpendPossessionEnergy(20); Sync(); SoulMarkRuntime.Use(player);
        Check(!Pending && events == 0 && target.CurrentState == ZeldaAiState.Idle, "insufficient energy causes no hostility");

        Setup(); target = Npc("Out of range", new Vector2(20, 0)); Sync(); SoulMarkRuntime.Use(player);
        Check(!Pending && target.CurrentState == ZeldaAiState.Idle && player.CharacterData.FinalPossessionEnergy == 20, "missing target does not spend or notify");
        target.transform.position = Vector2.right; CallAi(target, "ChangeState", ZeldaAiState.Alert, false);
        Sync(); SoulMarkRuntime.Use(player);
        Check(!Pending && target.CurrentState == ZeldaAiState.Alert && player.CharacterData.FinalPossessionEnergy == 20, "ineligible warning target is not a successful hostile cast");

        Setup(); target = Npc("Silenced target", Vector2.right); var ally = Npc("Nearby ally", new Vector2(2, 2));
        Set(ally, "visionAngle", 30f); Set(ally, "peripheralVisionAngle", 30f); Set(ally, "facingDirection", Vector2.up);
        CallAi(target, "ApplySilenceOath", 1); Sync(); SoulMarkRuntime.Use(player);
        Check(Pending && target.IsActivelyHostileTo(player) && target.HasSilenceOath, "marked silenced victim can retaliate");
        Check(ally.CurrentState == ZeldaAiState.Idle, "Silence Oath still suppresses unwitnessed ally alert");

        Setup(); target = Npc("Protected victim", Vector2.right);
        var sentry = Turret(new Vector2(-5, 0));
        var protector = Turret(new Vector2(-5, -2), target.GetComponent<ZeldaCharacterData>());
        var friendly = Turret(new Vector2(-5, 2), player.CharacterData);
        Sync(); SoulMarkRuntime.Use(player);
        Check(sentry.IsActivelyHostileTo(player), "unbound turret witnesses soul marking");
        Check(protector.IsActivelyHostileTo(player), "victim's protector receives non-damaging attack evidence");
        Check(!friendly.IsActivelyHostileTo(player), "player-bound turret still ignores its owner's offences");

        // Transfer is a second use, not another marking attempt. Drive its unchanged
        // coroutine manually so batch mode does not stall at WaitForEndOfFrame.
        Setup(); target = Npc("Destination", Vector2.right); events = 0;
        var destination = target.GetComponent<ZeldaFourWayMover>();
        destination.CharacterData.AttackedBy += _ => events++;
        Sync(); SoulMarkRuntime.Use(player);
        Check(Pending && target.IsActivelyHostileTo(player), "transfer starts with hostile marked destination");
        target.transform.position = new Vector2(20, 0); Physics2D.SyncTransforms();
        var active = Object.FindObjectOfType<SoulMarkRuntime>();
        var transfer = (IEnumerator)typeof(SoulMarkRuntime).GetMethod("Transfer", Flags).Invoke(active, new object[] { player });
        int steps = 0; while (transfer.MoveNext()) Check(++steps < 10, "bounded transfer sequence");
        Check(destination.enabled && ZeldaRuntimeRegistry.GetControlledMover() == destination && !target.enabled, "transfer still controls marked hostile actor");
        Check(!Pending && !SoulMarkRuntime.IsTransferring && events == 1, "transfer clears mark without replaying cast offence");
    }
}
