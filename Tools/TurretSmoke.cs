using System;
using System.Collections;
using System.Reflection;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class TurretSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static IEnumerator suite; static int checks, frame = -1;
    static TurretSmoke() { EditorApplication.update += Tick; }
    public static void Run()
    {
        Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "isolated project");
        PlayerSettings.productName = "turret-smoke";
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(nameof(TurretSmoke), true); EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return; frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(TurretSmoke), false)) { SessionState.SetBool(nameof(TurretSmoke), false); suite = Verify(); }
            if (suite == null || suite.MoveNext()) return;
            suite = null; Debug.Log("TURRET_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    static FieldInfo Field(object o, string name)
    {
        for (Type t = o.GetType(); t != null; t = t.BaseType) { var f = t.GetField(name, F); if (f != null) return f; }
        throw new Exception("Missing field " + name);
    }
    static T Get<T>(object o, string name) => (T)Field(o, name).GetValue(o);
    static void Set(object o, string name, object value) => Field(o, name).SetValue(o, value);
    static object Call(object o, string name, params object[] args)
    {
        for (Type t = o.GetType(); t != null; t = t.BaseType)
        { var m = t.GetMethod(name, F); if (m != null) return m.Invoke(o, args); }
        throw new Exception("Missing method " + name);
    }
    static ZeldaFourWayMover Actor(string name, Vector2 position, bool player = false)
    {
        var go = new GameObject(name); go.transform.position = position;
        var data = go.AddComponent<SwordZeldaCharacterData>();
        var mover = go.AddComponent<ZeldaFourWayMover>(); mover.enabled = player;
        var state = data.CaptureRuntimeState(); state.health = state.currentHealth = 50; state.permissionLevel = 0; data.ApplyRuntimeState(state);
        if (player) ZeldaRuntimeRegistry.ClaimControlledMover(mover);
        return mover;
    }
    static void Sense(TurretCharacterAi ai, float dt = .13f) { Physics2D.SyncTransforms(); Call(ai, "UpdateTargetAndState", dt); }
    static IEnumerator Verify()
    {
        Time.timeScale = 1;
        while (DocumentReader.IsInputBlocked) yield return null;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Level2NPCS/Turret-level2.prefab");
        new GameObject("Turret Test Audio Listener").AddComponent<AudioListener>();
        Check(prefab != null, "prefab exists");
        var root = Object.Instantiate(prefab); root.name = "Test Turret";
        var data = root.GetComponent<TurretZeldaCharacterData>(); var ai = root.GetComponent<TurretCharacterAi>();
        var mover = root.GetComponent<ZeldaFourWayMover>();
        var player = Actor("Player", new Vector2(0, 4), true);
        Check(data != null && ai != null && !mover.enabled, "independent NPC controller");
        Check(data.AttackPower == 2 && data.MoveSpeed == 0, "stationary data, AP2");
        Check(Get<float>(ai, "visionAngle") == 360 && Get<float>(ai, "visionRadius") == 7, "omnidirectional vision");
        Check(Resources.Load<SaveGameCatalog>("SaveGameCatalog").FindCharacter(data.GetType().AssemblyQualifiedName, prefab.name) == prefab, "save catalog resolves turret body");
        var sound = Get<AudioClip>(data, "firingSound");
        Check(sound != null && sound.length < .4f && sound.channels == 1, "dedicated short heavy shot is linked");
        VerifyGhostRules(ai, data, mover, player);
        VerifyWitnessedOffences(ai, data, player);
        Sense(ai); Check(ai.CurrentState == ZeldaAiState.Idle, "legal player ignored");
        Vector3 origin = root.transform.position; Quaternion rotation = root.transform.rotation;
        Call(ai, "TickIdle", 3.1f); Call(ai, "FixedUpdate");
        Check(root.transform.position == origin && root.transform.rotation == rotation, "base never moves or rotates");
        data.AimAt(new Vector2(0, 4), 10f);
        data.AimAt(new Vector2(4, 0), .1f);
        Check(Mathf.Abs(Vector2.Angle(Vector2.up, data.AimDirection) - 12f) < .01f, "rotation limited to 120 degrees per second");
        root.transform.rotation = Quaternion.Euler(0, 0, 45);
        data.AimAt(new Vector2(4, 0), 10f);
        Check(Vector2.Angle(data.AimDirection, Vector2.right) < .01f && Mathf.Abs(data.MuzzlePosition.y) < .01f, "rotated prefab aim and muzzle agree");
        root.transform.rotation = Quaternion.identity;
        var area = new GameObject("Permission").AddComponent<PermissionArea>();
        Set(area, "permissionLevel", 1); Set(area, "areaSize", new Vector2(20, 20)); Call(area, "SynchronizeCollider");
        Sense(ai); Check(ai.CurrentState == ZeldaAiState.Alert && ai.CurrentTarget == player, "one permission level enters alert");
        for (int i = 0; i < 42; i++) Sense(ai, .1f);
        Check(ai.CurrentState == ZeldaAiState.Hostile, "warning escalates");
        Check(!ai.CaptureSaveState().sharedPlayerPhase.Equals(1), "turret does not join shared timer");
        player.transform.position = new Vector3(20, 0); Sense(ai);
        Check(ai.CurrentState == ZeldaAiState.Idle, "out of range immediately stops hostility");
        player.transform.position = new Vector3(0, -4); Set(area, "permissionLevel", 2); Sense(ai);
        Check(ai.CurrentState == ZeldaAiState.Hostile, "rear target detected at 360 degrees; two-level violation hostile");
        var wall = new GameObject("Blocks Wall"); wall.layer = LayerMask.NameToLayer("Blocks");
        wall.transform.position = new Vector3(0, -2); wall.AddComponent<BoxCollider2D>().size = new Vector2(3, .3f);
        Sense(ai); Check(ai.CurrentState == ZeldaAiState.Idle, "ordinary engagement respects wall occlusion");
        Object.Destroy(wall); Object.Destroy(area.gameObject); yield return null;
        player.transform.position = new Vector3(25, 0);
        var skeletonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Level2NPCS/Skeleton-level2.prefab");
        Check(skeletonPrefab != null, "skeleton reference prefab exists");
        var skeleton = Object.Instantiate(skeletonPrefab, new Vector3(3, 0), Quaternion.identity);
        Set(ai, "scanTimer", 0f); Sense(ai);
        Check(ai.CurrentState == ZeldaAiState.Hostile && ai.CurrentTarget == skeleton.GetComponent<ZeldaFourWayMover>(), "visible skeleton is an enemy without permission violation");
        skeleton.transform.position = new Vector3(30, 0); ZeldaRuntimeRegistry.InvalidateSpatialSnapshot(); Sense(ai);
        Check(ai.CurrentState == ZeldaAiState.Idle, "universal enemy leaving view ends normal engagement");
        player.transform.position = new Vector3(0, 4);
        ai.OnPlayerAttackedCharacter(player.CharacterData, skeleton.GetComponent<ZeldaCharacterData>());
        Check(ai.CurrentState == ZeldaAiState.Idle, "attacking a skeleton does not cause witness hostility");
        Object.Destroy(skeleton); yield return null;
        player.transform.position = new Vector3(25, 0);
        var protectedMover = Actor("Protected", new Vector2(30, 2));
        var attacker = Actor("Attacker", new Vector2(30, -2));
        ai.BindCharacter(protectedMover.CharacterData); Sense(ai);
        Check(ai.HasBinding && ai.RequiresVisionStreamingRetention && ai.CurrentState == ZeldaAiState.Idle, "bound sentry stays loaded, no automatic permission aggression");
        ai.StunForDuration(3f);
        protectedMover.CharacterData.TakeDamage(1, attacker.CharacterData);
        Check(ai.IsStunned && ai.StunRemaining == 3f, "protected damage never cancels a turret stun");
        Call(ai, "FinishStun"); Sense(ai);
        Check(ai.CurrentTarget == null && ai.CurrentState == ZeldaAiState.Idle &&
            ai.CaptureTurretState().target == attacker.CharacterData.IndividualAccessId,
            "out-of-view protection incident survives stun without attacking");
        protectedMover.CharacterData.TakeDamage(1, attacker.CharacterData);
        Check(ai.CurrentTarget == null && ai.CurrentState == ZeldaAiState.Idle, "outside-vision injury does not start firing");
        attacker.transform.position = new Vector3(4, 0); Sense(ai);
        Check(ai.CurrentTarget == attacker && ai.CurrentState == ZeldaAiState.Hostile, "remembered actual attacker engages on entering view, without another attack");
        Check(ai.IsActivelyHostileTo(attacker), "hostility is reported consistently to other gameplay systems");
        VerifyProtectionSight(ai, data, attacker);
        ai.BindCharacter(protectedMover.CharacterData);
        protectedMover.CharacterData.TakeHandgunDamage(1, attacker.transform.position, attacker.CharacterData);
        Check(ai.CurrentTarget == attacker, "anonymous handgun evidence reaches protector without normal witness changes");
        ai.BindCharacter(protectedMover.CharacterData);
        var attempt = Get<Action<ZeldaFourWayMover>>(protectedMover, "PossessionAttempted");
        Check(attempt != null, "bound target subscribes to possession attempts"); attempt(player);
        Sense(ai); Check(ai.CurrentTarget == null && ai.CaptureTurretState().target == player.CharacterData.IndividualAccessId,
            "possession incident is remembered but cannot fire beyond view");
        player.transform.position = new Vector3(0, 4); Sense(ai);
        Check(ai.CurrentTarget == player, "remembered possession offender reengages inside view");
        ai.BindCharacter(protectedMover.CharacterData);
        var guardAi = protectedMover.gameObject.AddComponent<ZeldaCharacterAiBase>();
        Call(guardAi, "BeginTemporaryHostility", attacker, false); Sense(ai);
        Check(ai.CurrentTarget == attacker, "bound actor's hostile target is inherited");
        ai.BindCharacter(protectedMover.CharacterData);
        protectedMover.CharacterData.TakeDamage(100, attacker.CharacterData);
        Check(ai.CurrentTarget == attacker, "lethal damage reaches sentry before victim cleanup");
        var saved = JsonUtility.FromJson<ZeldaCharacterData.RuntimeState>(JsonUtility.ToJson(data.CaptureRuntimeState()));
        data.ApplyRuntimeState(saved); Sense(ai);
        Check(ai.CurrentTarget == attacker && ai.HasBinding, "save roundtrip retains protection target and binding identity");
        // AI burst timing: three projectiles along current barrel direction, not a fan.
        ai.BindCharacter(attacker.CharacterData); attacker.CharacterData.TakeDamage(1, player.CharacterData);
        data.AimAt(player.transform.position, 10);
        Set(data, "shotCooldown", 0f); Set(ai, "burstTimer", 0f); Set(ai, "burstRemaining", 0);
        int startCount = Object.FindObjectsOfType<DisposableHandgunBullet>().Length;
        Call(ai, "TickHostile", .01f); Set(data, "shotCooldown", 0f);
        Call(ai, "TickHostile", .13f); Set(data, "shotCooldown", 0f);
        Call(ai, "TickHostile", .13f);
        Check(Object.FindObjectsOfType<DisposableHandgunBullet>().Length == startCount + 3 && Get<int>(ai, "burstRemaining") == 0,
            "three rapid shots per burst: count=" + Object.FindObjectsOfType<DisposableHandgunBullet>().Length + " start=" + startCount +
            " remaining=" + Get<int>(ai, "burstRemaining") + " target=" + ai.CurrentTarget + " forced=" + Get<ZeldaFourWayMover>(ai, "forcedTarget") +
            " dead=" + data.IsDead + " stun=" + ai.IsStunned + " input=" + DocumentReader.IsInputBlocked + " time=" + Time.timeScale);
        Set(data, "shotCooldown", 0f); Call(ai, "TickHostile", .5f);
        Check(Object.FindObjectsOfType<DisposableHandgunBullet>().Length == startCount + 3, "1.5 second burst interval prevents early fourth shot");
        Call(ai, "TickHostile", .75f);
        Check(Object.FindObjectsOfType<DisposableHandgunBullet>().Length == startCount + 4, "next burst starts after 1.5 seconds");
        var soundSource = Object.FindObjectsOfType<TurretShotAudio>().Last();
        Vector3 listenerPosition = player.transform.position;
        player.transform.position = soundSource.transform.position; Call(soundSource, "RefreshVolume");
        Check(Mathf.Abs(Get<AudioSource>(soundSource, "source").volume - .65f) < .001f, "nearby shot uses configured volume");
        player.transform.position += Vector3.right * 12; Call(soundSource, "RefreshVolume");
        Check(Mathf.Abs(Get<AudioSource>(soundSource, "source").volume - .325f) < .001f, "shot attenuates to half volume at 12 units");
        player.transform.position += Vector3.right * 10; Call(soundSource, "RefreshVolume");
        Check(Get<AudioSource>(soundSource, "source").volume == 0, "distant shot becomes inaudible");
        Time.timeScale = 0; Call(soundSource, "Update");
        Check(Get<bool>(soundSource, "paused") && !data.TryFire(), "pause silences and prevents firing");
        Time.timeScale = 1; Call(soundSource, "Update"); player.transform.position = listenerPosition;
        foreach (var b in Object.FindObjectsOfType<DisposableHandgunBullet>()) Object.Destroy(b.gameObject);
        ai.BindCharacter(null); player.transform.position = new Vector3(30, 30); ai.enabled = false;
        yield return null;
        // Reuse actual swept projectile: AP2, thin wall blocking and five-second timeout.
        var victim = Actor("Projectile Victim", new Vector2(0, 3)); Physics2D.SyncTransforms();
        data.AimAt(victim.transform.position, 10f); Set(data, "shotCooldown", 0f);
        Check(data.TryFire() && !data.TryFire(), "player shot cooldown is enforced");
        Check(Mathf.Abs(Get<float>(data, "shotCooldown") - .25f) < .001f, "player cooldown .25 seconds");
        var bullet = Object.FindObjectsOfType<DisposableHandgunBullet>().Single();
        int health = victim.CharacterData.CurrentHealth;
        for (int i = 0; i < 20; i++) Call(bullet, "FixedUpdate");
        Check(victim.CharacterData.CurrentHealth == health - 2, "single projectile deals one AP2 hit");
        yield return null;
        Set(data, "shotCooldown", 0f); data.AimAt(new Vector2(0, -10), 10f); data.TryFire();
        bullet = Object.FindObjectsOfType<DisposableHandgunBullet>().Single();
        Set(bullet, "elapsed", 4.999f); Call(bullet, "FixedUpdate");
        Check(Get<bool>(bullet, "finished"), "five-second lifetime inherited");
        yield return null;
        var barrier = new GameObject("Thin wall"); barrier.layer = LayerMask.NameToLayer("Blocks");
        barrier.transform.position = new Vector3(0, .7f); barrier.AddComponent<BoxCollider2D>().size = new Vector2(1, .04f);
        Physics2D.SyncTransforms(); Set(data, "shotCooldown", 0f); data.AimAt(new Vector2(0, 3), 10f); data.TryFire();
        bullet = Object.FindObjectsOfType<DisposableHandgunBullet>().Single();
        Check(Get<bool>(bullet, "finished"), "body-to-muzzle sweep cannot shoot through wall");
        Object.Destroy(barrier); yield return null;
        var regularGuard = Actor("Ordinary guard", new Vector2(50, 0));
        var regularAi = regularGuard.gameObject.AddComponent<ZeldaCharacterAiBase>();
        regularGuard.CharacterData.TakeHandgunDamage(2, player.transform.position, player.CharacterData);
        Check(regularAi.CurrentState == ZeldaAiState.Search && regularAi.CurrentTarget != player, "ordinary handgun hit still investigates rather than directly retaliating");
        Object.Destroy(regularGuard.gameObject);
        Preview(root);
        // Possession takes over input without enabling ordinary movement.
        mover.ReceiveControl(Vector2.up); ZeldaRuntimeRegistry.ClaimControlledMover(mover); Call(data, "Update");
        Check(!ai.enabled && TurretZeldaCharacterData.PlayerNeedsCursor && Cursor.visible, "possession disables AI and exposes cursor");
        Set(mover, "moveDirection", Vector2.right); Call(mover, "FixedUpdate");
        Check(root.transform.position == origin, "controlled turret ignores normal movement");
        Call(data, "AdvanceExitHold", true, 2f); Check(!data.IsDead, "incoming possession key cannot immediately self-destruct");
        Call(data, "AdvanceExitHold", false, .1f);
        Call(data, "AdvanceExitHold", true, .7f); data.CancelPlayerInput();
        Call(data, "AdvanceExitHold", true, .9f); Check(!data.IsDead, "interrupted hold resets exit timer");
        Call(data, "AdvanceExitHold", true, .7f);
        Check(data.IsDead && ZeldaRuntimeRegistry.GetControlledMover().CharacterData is GhostZeldaCharacterData, "held F kills turret and transfers to ghost");
        yield return null;
    }
    static void VerifyProtectionSight(TurretCharacterAi ai, TurretZeldaCharacterData data, ZeldaFourWayMover attacker)
    {
        string targetId = attacker.CharacterData.IndividualAccessId;
        int startCount = Object.FindObjectsOfType<DisposableHandgunBullet>().Length;
        data.AimAt(attacker.transform.position, 10);
        Set(data, "shotCooldown", 0f); Set(ai, "burstTimer", 0f);
        Call(ai, "TickHostile", .01f);
        Check(Object.FindObjectsOfType<DisposableHandgunBullet>().Length == startCount + 1 && Get<int>(ai, "burstRemaining") == 2,
            "visible remembered attacker starts normal burst");
        attacker.transform.position = new Vector3(30, 0); Physics2D.SyncTransforms();
        Set(data, "shotCooldown", 0f); Call(ai, "TickHostile", .13f);
        Check(Object.FindObjectsOfType<DisposableHandgunBullet>().Length == startCount + 1 && Get<int>(ai, "burstRemaining") == 0 &&
            ai.CurrentState == ZeldaAiState.Idle && ai.CaptureTurretState().target == targetId,
            "shot-time range check cancels pending burst but keeps identity even before next sensing tick");
        for (int i = 0; i < 100; i++) { Sense(ai, .2f); Call(ai, "TickHostile", .2f); }
        Check(ai.CurrentTarget == null && ai.CaptureTurretState().target == targetId &&
            Object.FindObjectsOfType<DisposableHandgunBullet>().Length == startCount + 1,
            "long absence neither fires nor expires protection memory");
        var saved = JsonUtility.FromJson<ZeldaCharacterData.RuntimeState>(JsonUtility.ToJson(data.CaptureRuntimeState()));
        data.ApplyRuntimeState(saved); Sense(ai);
        Check(ai.CurrentTarget == null && ai.CaptureTurretState().target == targetId, "out-of-view memory survives save roundtrip without starting attack");
        attacker.transform.position = new Vector3(4, 0); Sense(ai);
        Check(ai.CurrentTarget == attacker && ai.CurrentState == ZeldaAiState.Hostile, "saved attacker returning resumes hostility");
        var wall = new GameObject("Protection sight wall"); wall.layer = LayerMask.NameToLayer("Blocks");
        wall.transform.position = new Vector3(2, 0); wall.AddComponent<BoxCollider2D>().size = new Vector2(.4f, 3);
        Sense(ai); Call(ai, "TickHostile", 2f);
        Check(ai.CurrentTarget == null && ai.CurrentState == ZeldaAiState.Idle && ai.CaptureTurretState().target == targetId &&
            Object.FindObjectsOfType<DisposableHandgunBullet>().Length == startCount + 1,
            "Blocks occlusion suspends attack without forgetting target");
        Object.DestroyImmediate(wall); Sense(ai);
        Check(ai.CurrentTarget == attacker, "clearing wall reacquires remembered target");
        attacker.gameObject.SetActive(false); Sense(ai);
        Check(ai.CurrentTarget == null && ai.CaptureTurretState().target == targetId, "distance unloading suspends targeting but retains identity");
        attacker.gameObject.SetActive(true); Sense(ai);
        Check(ai.CurrentTarget == attacker && ai.CurrentState == ZeldaAiState.Hostile, "reloaded visible attacker automatically reacquired");
        // Restore a pending burst saved by an older build which ignored vision.
        var legacy = ai.CaptureTurretState(); legacy.remaining = 2; legacy.shot = 0; legacy.burst = 0;
        attacker.transform.position = new Vector3(30, 0); ai.ApplyTurretState(legacy); Sense(ai);
        Set(data, "shotCooldown", 0f); Call(ai, "TickHostile", .2f);
        Check(Object.FindObjectsOfType<DisposableHandgunBullet>().Length == startCount + 1 && Get<int>(ai, "burstRemaining") == 0 &&
            ai.CaptureTurretState().target == targetId, "legacy out-of-range pending burst is safely discarded, memory retained");
        attacker.transform.position = new Vector3(4, 0); Sense(ai);
        foreach (var bullet in Object.FindObjectsOfType<DisposableHandgunBullet>()) Object.DestroyImmediate(bullet.gameObject);
    }
    static void VerifyGhostRules(TurretCharacterAi ai, TurretZeldaCharacterData data,
        ZeldaFourWayMover turretMover, ZeldaFourWayMover player)
    {
        var ward = Actor("Ghost regression ward", new Vector2(3, 0));
        var ghostRoot = Object.Instantiate(turretMover.SoulTransferFallbackPrefab, new Vector3(2, 0), Quaternion.identity);
        var ghost = ghostRoot.GetComponent<ZeldaFourWayMover>();
        Check(ghost.CharacterData.IsGhostLike, "actual ghost prefab used for possession regression");
        ai.BindCharacter(ward.CharacterData);
        var attempt = Get<Action<ZeldaFourWayMover>>(ward, "PossessionAttempted");
        attempt(ghost); Sense(ai);
        Check(ai.CurrentState == ZeldaAiState.Idle && ai.CurrentTarget == null && !ai.CaptureTurretState().followPlayer,
            "ghost possession attempt does not create a defense incident");
        ward.ReceiveControl(Vector2.left); ZeldaRuntimeRegistry.ClaimControlledMover(ward);
        ai.OnPossessionWitnessed(ghost, ward); Sense(ai);
        Check(ai.CurrentState == ZeldaAiState.Idle && ai.CurrentTarget == null,
            "ghost possession completion never turns the protected body into a target");

        // Existing saves may already contain either half of the original bug.
        ai.ApplyTurretState(new TurretCharacterAi.PersistentState {
            bound = ward.CharacterData.IndividualAccessId, target = ghost.CharacterData.IndividualAccessId,
            followPlayer = true, remaining = 2 });
        Sense(ai);
        Check(ai.CurrentTarget == null && string.IsNullOrEmpty(ai.CaptureTurretState().target) && !ai.CaptureTurretState().followPlayer,
            "legacy ghost target clears rather than transferring to the possessed ward");
        ai.ApplyTurretState(new TurretCharacterAi.PersistentState {
            bound = ward.CharacterData.IndividualAccessId, target = ward.CharacterData.IndividualAccessId,
            followPlayer = true, remaining = 2 });
        Sense(ai);
        Check(ai.CurrentState == ZeldaAiState.Idle && ai.CurrentTarget == null && Get<int>(ai, "burstRemaining") == 0,
            "legacy protected-body target and pending burst are cleared on restore");

        // Real-body attempts still trigger protection. Death must stop the incident,
        // instead of following the newly spawned ghost or its later possession.
        ward.enabled = false;
        var offender = Actor("Real possession offender", new Vector2(4, 0), true);
        Set(offender, "deathGhostPrefab", turretMover.SoulTransferFallbackPrefab);
        ai.BindCharacter(ward.CharacterData);
        attempt = Get<Action<ZeldaFourWayMover>>(ward, "PossessionAttempted");
        attempt(offender); Sense(ai);
        Check(ai.CurrentState == ZeldaAiState.Hostile && ai.CurrentTarget == offender,
            "real-body possession attempts still trigger defense");
        attempt(ghost); Sense(ai);
        Check(ai.CurrentTarget == offender, "ignored ghost events do not erase an unrelated real attacker's hostility");
        offender.CharacterData.KillFromPotion();
        var deathGhost = ZeldaRuntimeRegistry.GetControlledMover();
        Check(deathGhost != null && deathGhost.CharacterData is GhostZeldaCharacterData,
            "offender death creates an actual controlled ghost");
        int bullets = Object.FindObjectsOfType<DisposableHandgunBullet>().Length;
        Sense(ai); Call(ai, "TickHostile", .5f);
        Check(ai.CurrentState == ZeldaAiState.Idle && ai.CurrentTarget == null &&
            !ai.CaptureTurretState().followPlayer && string.IsNullOrEmpty(ai.CaptureTurretState().target),
            "death fully clears forced target and player-follow latch");
        Check(Object.FindObjectsOfType<DisposableHandgunBullet>().Length == bullets,
            "no additional bullets are emitted at the death ghost");
        ward.ReceiveControl(Vector2.left); ZeldaRuntimeRegistry.ClaimControlledMover(ward);
        ai.OnPossessionWitnessed(deathGhost, ward); Sense(ai);
        Check(ai.CurrentTarget == null, "repossessing after death does not revive the old defense incident");

        // Guard against the original player-tracking transfer even without a death frame.
        ZeldaRuntimeRegistry.ClaimControlledMover(player);
        ai.BindCharacter(ward.CharacterData); Get<Action<ZeldaFourWayMover>>(ward, "PossessionAttempted")(player);
        ZeldaRuntimeRegistry.ClaimControlledMover(ward); Sense(ai);
        Check(ai.CurrentTarget == null && ai.CurrentState == ZeldaAiState.Idle,
            "control transfer cannot redirect defense toward its own ward");

        // Temporary ghost form follows the same rules as the standalone ghost.
        ZeldaRuntimeRegistry.ClaimControlledMover(player);
        ai.BindCharacter(ward.CharacterData); Get<Action<ZeldaFourWayMover>>(ward, "PossessionAttempted")(player);
        GhostFormRuntime.Restore(player.CharacterData, 5f, 5f);
        Check(player.CharacterData.IsGhostLike, "temporary ghost form active");
        Sense(ai); Get<Action<ZeldaFourWayMover>>(ward, "PossessionAttempted")(player);
        Check(ai.CurrentTarget == null && !ai.CaptureTurretState().followPlayer,
            "temporary ghost form drops targeting and does not trigger a possession incident");
        GhostFormRuntime.Restore(player.CharacterData, 0f);
        Sense(ai); Check(ai.CurrentTarget == null, "ghost form expiration does not resurrect stale hostility");

        // The unbound witness path must also ignore a ghost's completed possession.
        ai.BindCharacter(null); ai.OnPossessionWitnessed(ghost, ward);
        Check(ai.CurrentState == ZeldaAiState.Idle, "unbound turret ignores witnessed ghost possession");
        ai.OnPossessionWitnessed(player, ward);
        Check(ai.CurrentState == ZeldaAiState.Hostile && ai.CurrentTarget == ward,
            "unbound turret still notices real-body possession");

        ai.BindCharacter(null);
        Object.DestroyImmediate(ward.gameObject); Object.DestroyImmediate(ghostRoot);
        Object.DestroyImmediate(offender.gameObject); Object.DestroyImmediate(deathGhost.gameObject);
        ZeldaRuntimeRegistry.ClaimControlledMover(player); Sense(ai);
    }
    static void VerifyWitnessedOffences(TurretCharacterAi ai, TurretZeldaCharacterData data, ZeldaFourWayMover player)
    {
        var ward = Actor("Crime test protected body", new Vector2(30, 2));
        var victim = Actor("Crime test victim", new Vector2(1, 4));
        player.transform.position = new Vector3(0, 4);
        ZeldaRuntimeRegistry.ClaimControlledMover(player);
        foreach (bool bound in new[] { false, true })
        {
            ZeldaCharacterData binding = bound ? ward.CharacterData : null;
            ai.BindCharacter(binding); Sense(ai);
            Check(ai.CurrentState == ZeldaAiState.Idle, "legal player remains neutral bound=" + bound);
            victim.CharacterData.TakeDamage(1, player.CharacterData); Sense(ai);
            Check(ai.IsActivelyHostileTo(player), "real attack witness survives next targeting tick bound=" + bound);
            for (int i = 0; i < 3; i++) Sense(ai);
            Check(ai.CurrentTarget == player && ai.CurrentState == ZeldaAiState.Hostile,
                "crime engagement is not overwritten by permission/binding logic bound=" + bound);
            Check(ai.CaptureSaveState().sharedPlayerPhase == 0, "crime remains independent of shared pursuit timer");
            var saved = JsonUtility.FromJson<ZeldaCharacterData.RuntimeState>(JsonUtility.ToJson(data.CaptureRuntimeState()));
            data.ApplyRuntimeState(saved); Sense(ai);
            Check(ai.IsActivelyHostileTo(player) && !string.IsNullOrEmpty(ai.CaptureTurretState().witnessed),
                "witnessed crime roundtrips in save data bound=" + bound);
            player.transform.position = new Vector3(20, 0); Sense(ai);
            Check(ai.CurrentState == ZeldaAiState.Idle && string.IsNullOrEmpty(ai.CaptureTurretState().witnessed),
                "witness does not use beyond-vision protection pursuit bound=" + bound);
            player.transform.position = new Vector3(0, 4); Sense(ai);
            Check(ai.CurrentState == ZeldaAiState.Idle, "return without new crime remains neutral bound=" + bound);

            foreach (var material in new[] { DoorData.BreakMaterial.Wood, DoorData.BreakMaterial.Glass })
            {
                ai.BindCharacter(binding);
                var door = new GameObject("Crime test destructible").AddComponent<DoorData>();
                door.transform.position = new Vector3(1, 3);
                Set(door, "breakMaterial", material); Set(door, "investigationRadius", .1f);
                door.ReceiveAttack(8, player.CharacterData, true); Sense(ai);
                Check(door.IsDestroyed && ai.IsActivelyHostileTo(player),
                    "actual " + material + " destruction reaches turret even outside sound radius bound=" + bound);
                Object.DestroyImmediate(door.gameObject);
            }
            ai.BindCharacter(binding); ai.InvestigatePosition(player.transform.position); Sense(ai);
            Check(ai.CurrentState == ZeldaAiState.Idle, "unattributed sound alone does not incriminate player");

            Set(player, "pendingControlTarget", victim); Set(player, "controlSwitchHoldTimer", .1f);
            Sense(ai);
            Check(ai.IsActivelyHostileTo(player), "visible possession attempt immediately hostile bound=" + bound);
            Set(player, "pendingControlTarget", null); Set(player, "controlSwitchHoldTimer", 0f);
            ai.OnPossessionWitnessed(player, victim); Sense(ai);
            Check(ai.IsActivelyHostileTo(victim), "witness follows successful real-body possession bound=" + bound);

            ai.BindCharacter(binding);
            var wall = new GameObject("Crime witness wall"); wall.layer = LayerMask.NameToLayer("Blocks");
            wall.transform.position = new Vector3(0, 2); wall.AddComponent<BoxCollider2D>().size = new Vector2(4, .4f);
            Physics2D.SyncTransforms();
            victim.CharacterData.TakeDamage(1, player.CharacterData);
            Check(!ai.OnPlayerDestroyedDoor(player.CharacterData), "wall blocks destruction witness bound=" + bound);
            Set(player, "pendingControlTarget", victim); Set(player, "controlSwitchHoldTimer", .1f);
            Sense(ai); ai.OnPossessionWitnessed(player, victim); Sense(ai);
            Check(ai.CurrentState == ZeldaAiState.Idle, "wall blocks attack and possession witnesses bound=" + bound);
            Set(player, "pendingControlTarget", null); Set(player, "controlSwitchHoldTimer", 0f);
            Object.DestroyImmediate(wall);

            ai.BindCharacter(binding); ai.StunForDuration(3f);
            ai.OnPlayerAttackedCharacter(player.CharacterData, victim.CharacterData);
            Check(!ai.OnPlayerDestroyedDoor(player.CharacterData) && ai.IsStunned,
                "crime cannot cancel stun bound=" + bound);
            Call(ai, "FinishStun"); ai.BindCharacter(binding);
            GhostFormRuntime.Restore(player.CharacterData, 5f, 5f);
            ai.OnPlayerAttackedCharacter(player.CharacterData, victim.CharacterData);
            bool ghostDestruction = ai.OnPlayerDestroyedDoor(player.CharacterData);
            Set(player, "pendingControlTarget", victim); Set(player, "controlSwitchHoldTimer", .1f);
            Sense(ai); ai.OnPossessionWitnessed(player, victim); Sense(ai);
            Check(!ghostDestruction && ai.CurrentState == ZeldaAiState.Idle,
                "temporary ghost ignored for all offences bound=" + bound);
            Set(player, "pendingControlTarget", null); Set(player, "controlSwitchHoldTimer", 0f);
            GhostFormRuntime.Restore(player.CharacterData, 0f);
        }
        ai.BindCharacter(player.CharacterData);
        victim.CharacterData.TakeDamage(1, player.CharacterData);
        Check(!ai.OnPlayerDestroyedDoor(player.CharacterData), "owner cannot incriminate itself to its own turret");
        Set(player, "pendingControlTarget", victim); Set(player, "controlSwitchHoldTimer", .1f); Sense(ai);
        ai.OnPossessionWitnessed(player, victim); Sense(ai);
        Check(ai.CurrentState == ZeldaAiState.Idle, "player-owned turret ignores protected player's attacks and possession");
        Set(player, "pendingControlTarget", null); Set(player, "controlSwitchHoldTimer", 0f);
        ai.OnPlayerPlacedBomb(player.CharacterData); Sense(ai);
        Check(ai.CurrentState == ZeldaAiState.Idle && ai.CurrentTarget == null,
            "player-owned turret also ignores protected player's armed bomb placement");
        ai.BindCharacter(ward.CharacterData);
        victim.CharacterData.TakeDamage(100, player.CharacterData); Sense(ai);
        Check(ai.IsActivelyHostileTo(player), "lethal attack witness survives victim death");
        ai.BindCharacter(null);
        Object.DestroyImmediate(victim.gameObject); Object.DestroyImmediate(ward.gameObject);
        foreach (var fragment in Object.FindObjectsOfType<DoorFragmentVisual>()) Object.DestroyImmediate(fragment.gameObject);
        Sense(ai);
    }
    static void Preview(GameObject root)
    {
        foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>()) renderer.gameObject.layer = 30;
        var cam = new GameObject("Preview Camera").AddComponent<Camera>();
        cam.transform.position = new Vector3(0, 0, -10); cam.orthographic = true; cam.orthographicSize = 1.35f;
        cam.cullingMask = 1 << 30; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color32(22, 31, 43, 255);
        var rt = new RenderTexture(512, 512, 24); cam.targetTexture = rt; cam.Render();
        RenderTexture.active = rt; var texture = new Texture2D(512, 512, TextureFormat.RGBA32, false);
        texture.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); texture.Apply();
        File.WriteAllBytes(Application.dataPath + "/../Turret-preview.png", texture.EncodeToPNG());
        RenderTexture.active = null; cam.targetTexture = null;
        Object.Destroy(texture); Object.Destroy(rt); Object.Destroy(cam.gameObject);
    }
}
