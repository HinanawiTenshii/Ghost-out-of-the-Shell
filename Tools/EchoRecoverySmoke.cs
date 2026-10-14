using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Copy into the isolated full-script Unity smoke project and run EchoRecoverySmoke.Run.
// Creates runtime fixtures only; never modifies authored scenes, prefabs or save files.
[InitializeOnLoad]
public static class EchoRecoverySmoke
{
    const string Pending = "EchoRecoverySmoke.Pending";
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static IEnumerator test;
    static int checks, frame = -1;
    static GameObject attackPrefab;

    static EchoRecoverySmoke() { EditorApplication.update += Tick; }
    public static void Run() { SessionState.SetBool(Pending, true); EditorApplication.EnterPlaymode(); }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(Pending, false)) { SessionState.SetBool(Pending, false); test = Test(); }
            if (test != null && !test.MoveNext())
            {
                test = null;
                Debug.Log("ECHO_RECOVERY_SMOKE_PASS checks=" + checks);
                EditorApplication.Exit(0);
            }
        }
        catch (Exception error) { test = null; Debug.LogException(error); EditorApplication.Exit(1); }
    }

    static void Check(bool value, string label) { checks++; if (!value) throw new Exception(label); }
    static FieldInfo Field(object owner, string name)
    {
        for (Type type = owner.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(name, Flags | BindingFlags.DeclaredOnly);
            if (field != null) return field;
        }
        throw new Exception("Missing fixture field: " + name);
    }
    static void Set(object owner, string name, object value) => Field(owner, name).SetValue(owner, value);
    static object Call(object owner, string name, params object[] args)
    {
        for (Type type = owner.GetType(); type != null; type = type.BaseType)
        {
            var method = type.GetMethod(name, Flags | BindingFlags.DeclaredOnly);
            if (method != null) return method.Invoke(owner, args);
        }
        throw new Exception("Missing fixture method: " + name);
    }
    static ZeldaFourWayMover Mover(Component actor) => actor.GetComponent<ZeldaFourWayMover>();
    static void Move(Component actor, Vector2 position)
    {
        actor.transform.position = position;
        actor.GetComponent<Rigidbody2D>().position = position;
        Physics2D.SyncTransforms();
    }
    static ZeldaCharacterData Actor(string name, Vector2 position, Type dataType = null, Type aiType = null)
    {
        var go = new GameObject(name);
        go.SetActive(false);
        go.transform.position = position;
        go.AddComponent<BoxCollider2D>().size = Vector2.one * .45f;
        var data = (ZeldaCharacterData)go.AddComponent(dataType ?? typeof(SwordZeldaCharacterData));
        var mover = go.AddComponent<ZeldaFourWayMover>();
        mover.enabled = false;
        Set(data, "attackPrefab", attackPrefab);
        if (aiType != null) go.AddComponent(aiType);
        go.SetActive(true);
        Reset(data);
        if (aiType != null) Set(go.GetComponent<ZeldaCharacterAiBase>(), "enableAwarenessSound", false);
        return data;
    }
    static ZeldaCharacterAiBase Guard(string name, Vector2 home, Type dataType = null, Type aiType = null)
    {
        var data = Actor(name, home, dataType, aiType ?? typeof(ZeldaCharacterAiBase));
        // Recovery has a real journey to make rather than completing at home immediately.
        Move(data, home + Vector2.right * 6f);
        return data.GetComponent<ZeldaCharacterAiBase>();
    }
    static void Control(ZeldaCharacterData data)
    {
        var previous = ZeldaRuntimeRegistry.GetControlledMover();
        if (previous != null) previous.enabled = false;
        Mover(data).enabled = true;
        ZeldaRuntimeRegistry.ClaimControlledMover(Mover(data));
    }
    static void Reset(ZeldaCharacterData data, int health = 6)
    {
        var saved = data.CaptureRuntimeState();
        saved.health = 6; saved.currentHealth = health;
        saved.potionBuffs = default; saved.combatExpertiseRemaining = 0f;
        data.ApplyRuntimeState(saved);
    }
    static void Prime(ZeldaCharacterAiBase ai, ZeldaAiState state, ZeldaFourWayMover target, bool attack = false)
    {
        var saved = ai.CaptureSaveState();
        saved.state = state; saved.targetsPlayer = false; saved.royalCommandRemaining = 0f;
        saved.retaliation = saved.untilDeath = state == ZeldaAiState.Hostile;
        saved.regular = state == ZeldaAiState.Hostile;
        saved.suspicion = 72f; saved.warning = 2f; saved.lostSight = 3f;
        saved.lastKnownTarget = target != null ? (Vector2)target.transform.position : Vector2.zero;
        ai.ApplySaveState(saved);
        Set(ai, "targetMover", target);
        Check(ai.CurrentState == state && ai.CurrentTarget == target, ai.name + ": primed state and exact target");
        if (attack)
            Check(Mover(ai).TryPerformAttack(Vector2.right, target) && Mover(ai).IsAttacking,
                ai.name + ": real attack starts before lethal damage");
    }
    static void CheckRecovery(ZeldaCharacterAiBase ai)
    {
        var saved = ai.CaptureSaveState();
        Check(ai.CurrentState == ZeldaAiState.Recovery && ai.CurrentTarget == null,
            ai.name + ": Recovery with no old target");
        Check(!Mover(ai).IsAttacking && !saved.retaliation && !saved.regular && !saved.untilDeath &&
            saved.suspicion == 0f && saved.warning == 0f && saved.lostSight == 0f,
            ai.name + ": attack and pursuit/warning memories cleared");
        Check(!ai.IsIgnoringPlayer && ai.RoyalCommandBuffRemaining == 0f,
            ai.name + ": no Royal Command/truce buff introduced");
    }
    static void CheckUnchanged(ZeldaCharacterAiBase ai, ZeldaAiState state, ZeldaFourWayMover target, string label)
    {
        Check(ai.CurrentState == state && ai.CurrentTarget == target, label);
    }
    static void ArmEcho(ZeldaCharacterData data, Vector2 anchor)
    {
        Reset(data, 4);
        Move(data, anchor);
        Check(CharacterPotionBuffs.TryApply(data, AlchemyPotionKind.Echo), data.name + ": Echo armed");
        Move(data, anchor + Vector2.right * 10f);
    }
    static ZeldaAttackHitbox Hitbox(ZeldaCharacterData owner, bool explosion = false, int power = 1)
    {
        var hit = new GameObject(owner.name + (explosion ? " independent explosion" : " old direct attack"))
            .AddComponent<ZeldaAttackHitbox>();
        hit.Configure(.2f, Vector2.one, Color.white, power, owner,
            explosion ? ZeldaAttackVisualShape.Shockwave : ZeldaAttackVisualShape.Sword,
            explosion, !explosion);
        return hit;
    }

    static IEnumerator Test()
    {
        while (DocumentReader.IsInputBlocked) yield return null;
        var scene = SceneManager.CreateScene("Echo Recovery Smoke A");
        SceneManager.SetActiveScene(scene);
        Time.timeScale = 0f;
        var growth = PlayerGrowthAttributes.Instance;
        if (growth == null) growth = new GameObject("Echo test growth").AddComponent<PlayerGrowthAttributes>();
        growth.ApplyRuntimeState(default);
        attackPrefab = new GameObject("Echo test inactive attack template");
        attackPrefab.AddComponent<ZeldaAttackHitbox>();
        attackPrefab.SetActive(false);

        var player = Actor("Echo controlled player", new Vector2(-100, 0));
        Control(player);
        var playerMover = Mover(player);
        var hostile = Guard("Hostile guard", new Vector2(20, 0));
        var alert = Guard("Alert guard", new Vector2(40, 0));
        var automaton = (AutomatonCharacterAi)Guard("Activated automaton", new Vector2(60, 0),
            typeof(AutomatonZeldaCharacterData), typeof(AutomatonCharacterAi));
        automaton.ActivateHostility();
        var skeleton = (SkeletonCharacterAi)Guard("Skeleton", new Vector2(80, 0),
            typeof(SkeletonZeldaCharacterData), typeof(SkeletonCharacterAi));
        var affected = new ZeldaCharacterAiBase[] { hostile, alert, automaton, skeleton };
        Prime(hostile, ZeldaAiState.Hostile, playerMover, true);
        Prime(alert, ZeldaAiState.Alert, playerMover);
        Prime(automaton, ZeldaAiState.Hostile, playerMover, true);
        Prime(skeleton, ZeldaAiState.Hostile, playerMover, true);
        var automatonHome = automaton.CaptureSaveState().home;
        var skeletonHome = skeleton.CaptureSaveState().home;

        var bystander = Actor("Other NPC combat target", new Vector2(300, 0));
        var otherFight = Guard("Unrelated NPC fight", new Vector2(100, 0));
        Prime(otherFight, ZeldaAiState.Hostile, Mover(bystander), true);
        var notAggressive = new ZeldaCharacterAiBase[4];
        var states = new[] { ZeldaAiState.Idle, ZeldaAiState.Suspicious, ZeldaAiState.Search, ZeldaAiState.Stunned };
        for (int i = 0; i < states.Length; i++)
        {
            notAggressive[i] = Guard(states[i] + " guard", new Vector2(120 + i * 20, 0));
            Prime(notAggressive[i], states[i], playerMover);
        }
        var dead = Guard("Dead guard", new Vector2(220, 0));
        Prime(dead, ZeldaAiState.Hostile, playerMover);
        Reset(dead.GetComponent<ZeldaCharacterData>(), 0);
        var dormant = (AutomatonCharacterAi)Guard("Dormant automaton", new Vector2(240, 0),
            typeof(AutomatonZeldaCharacterData), typeof(AutomatonCharacterAi));
        var foreign = Guard("Other scene guard", new Vector2(260, 0));
        var otherScene = SceneManager.CreateScene("Echo Recovery Smoke B");
        SceneManager.MoveGameObjectToScene(foreign.gameObject, otherScene);
        Prime(foreign, ZeldaAiState.Hostile, playerMover, true);

        var oldAttacks = new[] { Hitbox(player), Hitbox(hostile.GetComponent<ZeldaCharacterData>()),
            Hitbox(automaton.GetComponent<ZeldaCharacterData>()), Hitbox(skeleton.GetComponent<ZeldaCharacterData>()) };
        var explosion = Hitbox(hostile.GetComponent<ZeldaCharacterData>(), true);

        // The anchor is far from every fixture, so safe-position fallback cannot mask rewind success.
        Vector2 anchor = new Vector2(-100, 0);
        ArmEcho(player, anchor);
        Check(CharacterPotionBuffs.TryApply(hostile.GetComponent<ZeldaCharacterData>(), AlchemyPotionKind.Might),
            "Lethal attacker has Might's post-hit knockback");
        var lethalHit = Hitbox(hostile.GetComponent<ZeldaCharacterData>(), power: 100);
        Call(lethalHit, "TryDamage", player.GetComponent<Collider2D>());
        Check(!player.IsDead && player.CurrentHealth == 4 && Vector2.Distance(player.transform.position, anchor) < .01f,
            "Actual lethal melee consumes Echo and restores the exact anchor without delayed Might knockback");
        Check(!lethalHit.GetComponent<Collider2D>().enabled,
            "Lethal melee collider is cancelled before the in-flight contact resumes after Echo");
        Check(!CharacterPotionBuffs.Has(player, AlchemyPotionKind.Echo) && CharacterPotionBuffs.IsInvulnerable(player),
            "Original Echo consumption and one-second immunity remain intact");
        foreach (var ai in affected) CheckRecovery(ai);
        CheckUnchanged(otherFight, ZeldaAiState.Hostile, Mover(bystander), "NPC-versus-NPC fight is untouched");
        Check(Mover(otherFight).IsAttacking, "Unrelated NPC attack is not cancelled");
        for (int i = 0; i < states.Length; i++)
            CheckUnchanged(notAggressive[i], states[i], playerMover, states[i] + " NPC is untouched");
        CheckUnchanged(dead, ZeldaAiState.Hostile, playerMover, "Dead NPC is untouched");
        CheckUnchanged(foreign, ZeldaAiState.Hostile, playerMover, "Different gameplay scene is untouched");
        Check(Mover(foreign).IsAttacking, "Other scene's attack is not cancelled");
        Check(!dormant.HostilityActivated && dormant.CurrentState == ZeldaAiState.Idle, "Dormant automaton stays inactive");
        Check(automaton.HostilityActivated && automaton.IsUniversalThreat && !automaton.SharesHostilityWithOtherNpcs &&
            automaton.CaptureSaveState().home == automatonHome, "Automaton retains activation, faction and home");
        Check(skeleton.IsUniversalThreat && !skeleton.SharesHostilityWithOtherNpcs &&
            skeleton.CaptureSaveState().home == skeletonHome, "Skeleton retains independent hostility and home");
        foreach (var hit in oldAttacks)
        {
            Call(hit, "TryDamage", bystander.GetComponent<Collider2D>());
            Check(bystander.CurrentHealth == 6 && !hit.GetComponent<Collider2D>().enabled,
                hit.name + ": old direct hitbox cannot inflict delayed damage after rewind");
        }
        Call(explosion, "TryDamage", bystander.GetComponent<Collider2D>());
        Check(bystander.CurrentHealth == 5, "Already independent explosions remain active");

        // Exercise callbacks/perception after TakeDamage returns, still in the lethal hit's frame.
        // Move the player into each field of view to make an accidental reacquisition observable.
        foreach (var ai in affected)
        {
            Move(player, (Vector2)ai.transform.position + Vector2.right);
            Set(ai, "facingDirection", Vector2.right);
            ai.OnCharacterDamagedBy(player);
            Call(ai, "UpdateTargetAndState", 0f);
            CheckRecovery(ai);
        }
        Move(player, anchor);
        yield return null;

        foreach (var ai in new ZeldaCharacterAiBase[] { automaton, skeleton })
        {
            Call(ai, "Update");
            CheckRecovery(ai);
            var direction = (Vector2)Field(ai, "moveDirection").GetValue(ai);
            var toHome = ai.CaptureSaveState().home - (Vector2)ai.transform.position;
            Check(Vector2.Dot(direction, toHome) > 0f,
                ai.name + ": without fresh enemies, Recovery continues moving toward its original home");
        }

        // Echo is not a permanent alliance or a multi-second AI blindness effect.
        hostile.OnCharacterDamagedBy(player);
        CheckUnchanged(hostile, ZeldaAiState.Hostile, playerMover, "Fresh retaliation works on a later frame");
        foreach (var ai in new ZeldaCharacterAiBase[] { automaton, skeleton })
        {
            Move(player, (Vector2)ai.transform.position + Vector2.right);
            Set(ai, "facingDirection", Vector2.right);
            Call(ai, "UpdateTargetAndState", 0f);
            CheckUnchanged(ai, ZeldaAiState.Hostile, playerMover, ai.name + ": fresh visible perception can reacquire");
        }
        var freshAttack = Hitbox(hostile.GetComponent<ZeldaCharacterData>());
        Call(freshAttack, "TryDamage", bystander.GetComponent<Collider2D>());
        Check(bystander.CurrentHealth == 4, "A newly created direct attack works after the interrupted swing");
        Move(player, anchor);

        Prime(hostile, ZeldaAiState.Hostile, playerMover);
        ArmEcho(player, anchor);
        player.TakeDamage(1);
        Check(player.CurrentHealth == 3 && CharacterPotionBuffs.Has(player, AlchemyPotionKind.Echo),
            "Nonfatal damage does not consume Echo");
        CheckUnchanged(hostile, ZeldaAiState.Hostile, playerMover, "Nonfatal damage does not force Recovery");

        var npcEcho = Actor("Uncontrolled Echo body", new Vector2(-200, 0));
        ArmEcho(npcEcho, new Vector2(-200, 0));
        Prime(otherFight, ZeldaAiState.Hostile, Mover(npcEcho));
        npcEcho.TakeDamage(100);
        Check(!npcEcho.IsDead && npcEcho.CurrentHealth == 4, "Uncontrolled NPC can still use its own Echo");
        CheckUnchanged(otherFight, ZeldaAiState.Hostile, Mover(npcEcho), "Uncontrolled body's Echo does not reset its attacker");
        CheckUnchanged(hostile, ZeldaAiState.Hostile, playerMover, "Uncontrolled body's Echo does not reset player attackers");

        var noEcho = Actor("No Echo controlled death", new Vector2(-300, 0));
        Control(noEcho); Prime(hostile, ZeldaAiState.Hostile, Mover(noEcho));
        noEcho.TakeDamage(100);
        Check(noEcho.IsDead, "Lethal damage without Echo still kills");
        CheckUnchanged(hostile, ZeldaAiState.Hostile, Mover(noEcho), "Ordinary death does not emit Echo recovery");

        var poison = Actor("Poison controlled body", new Vector2(-400, 0));
        Control(poison); ArmEcho(poison, new Vector2(-400, 0));
        Prime(hostile, ZeldaAiState.Hostile, Mover(poison));
        CharacterPotionBuffs.TryApply(poison, AlchemyPotionKind.Death);
        Check(poison.IsDead, "Death potion bypasses Echo");
        CheckUnchanged(hostile, ZeldaAiState.Hostile, Mover(poison), "Poison death does not emit Echo recovery");

        var departing = Actor("Control-switch source", new Vector2(-500, 0));
        Control(departing); ArmEcho(departing, new Vector2(-500, 0));
        Prime(hostile, ZeldaAiState.Hostile, Mover(departing));
        Control(player);
        CheckUnchanged(hostile, ZeldaAiState.Hostile, Mover(departing), "Control switch alone does not reset former attackers");
        departing.DestroyAfterSuccessfulPossession();
        Check(departing.IsDead, "Control-switch death bypasses Echo");
        CheckUnchanged(hostile, ZeldaAiState.Hostile, Mover(departing), "Control-switch death does not emit Echo recovery");

        var blocked = Actor("Failed Echo controlled body", new Vector2(-600, 0));
        Control(blocked); ArmEcho(blocked, new Vector2(-600, 0));
        Prime(hostile, ZeldaAiState.Hostile, Mover(blocked));
        var wall = new GameObject("All Echo destination candidates blocked");
        wall.transform.position = new Vector3(-600, 0, 0);
        wall.AddComponent<BoxCollider2D>().size = Vector2.one * 12f;
        Physics2D.SyncTransforms();
        blocked.TakeDamage(100);
        Check(blocked.IsDead, "No safe Echo destination means no successful rewind");
        CheckUnchanged(hostile, ZeldaAiState.Hostile, Mover(blocked), "Failed Echo interception does not emit Recovery");
        Time.timeScale = 1f;
    }
}
