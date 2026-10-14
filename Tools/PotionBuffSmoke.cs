using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Copy to the isolated Unity smoke project; never modifies authored scenes or prefabs.
[InitializeOnLoad]
public static class PotionBuffSmoke
{
    const string Pending = "PotionBuffSmoke.Pending";
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static IEnumerator test;
    static int checks, frame = -1;
    static PotionBuffSmoke() { EditorApplication.update += Tick; }
    public static void Run() { SessionState.SetBool(Pending, true); EditorApplication.EnterPlaymode(); }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(Pending, false)) { SessionState.SetBool(Pending, false); test = Test(); }
            if (test != null && !test.MoveNext()) { Debug.Log("POTION_BUFF_SMOKE_PASS checks=" + checks); test = null; EditorApplication.Exit(0); }
        }
        catch (Exception error) { test = null; Debug.LogException(error); EditorApplication.Exit(1); }
    }
    static void Check(bool value, string label) { checks++; if (!value) throw new Exception(label); }
    static FieldInfo Field(object owner, string name)
    {
        for (Type type = owner.GetType(); type != null; type = type.BaseType)
        { var field = type.GetField(name, Flags); if (field != null) return field; }
        throw new Exception(name);
    }
    static void Set(object owner, string name, object value) => Field(owner, name).SetValue(owner, value);
    static T Get<T>(object owner, string name) => (T)Field(owner, name).GetValue(owner);
    static object Call(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, Flags).Invoke(owner, args);
    static SwordZeldaCharacterData Actor(string name, Vector3 position, bool controlled = false)
    {
        var go = new GameObject(name); go.transform.position = position;
        go.AddComponent<BoxCollider2D>().size = Vector2.one * .45f;
        var data = go.AddComponent<SwordZeldaCharacterData>();
        var mover = go.AddComponent<ZeldaFourWayMover>();
        mover.enabled = controlled;
        if (controlled) ZeldaRuntimeRegistry.ClaimControlledMover(mover);
        return data;
    }
    static void Reset(ZeldaCharacterData actor, int current = 6, int maximum = 6)
    {
        var state = actor.CaptureRuntimeState(); state.health = maximum; state.currentHealth = current;
        state.potionBuffs = default; state.combatExpertiseRemaining = 0f;
        state.possessionEnergy = 3; state.spentBasePossessionEnergy = 0;
        state.spentGrowthPossessionEnergy = 0; state.crystalEnergyThirds = 0;
        actor.ApplyRuntimeState(state);
    }
    static IEnumerator Test()
    {
        while (DocumentReader.IsInputBlocked) yield return null;
        var scene = SceneManager.CreateScene("Potion Buff Smoke A"); SceneManager.SetActiveScene(scene);
        Time.timeScale = 0f;
        var growth = PlayerGrowthAttributes.Instance;
        if (growth == null) growth = new GameObject("Potion test player growth").AddComponent<PlayerGrowthAttributes>();
        growth.ApplyRuntimeState(default);
        var actor = Actor("Potion controlled body", Vector3.zero, true);
        var mover = actor.GetComponent<ZeldaFourWayMover>();
        Reset(actor, 2);
        Check(CharacterPotionBuffs.TryApply(actor, AlchemyPotionKind.Vitality) && actor.CurrentHealth == 6 && actor.Health == 6,
            "Pure red heals six, capped, without converting excess to max HP");
        CharacterPotionBuffs.TryApply(actor, AlchemyPotionKind.Vitality);
        Check(actor.Health == 9 && actor.CurrentHealth == 6, "Full-health red grants only maximum, not current health");
        actor.RestoreHealth(3); CharacterPotionBuffs.TryApply(actor, AlchemyPotionKind.Vitality);
        Check(actor.Health == 9 && actor.CurrentHealth == 9, "Repeated vitality refreshes without stacking cap");
        Check(Mathf.Abs(actor.GetComponent<CharacterPotionBuffs>().Remaining(AlchemyPotionKind.Vitality) - 30f) < .05f, "Vitality duration 30 seconds");
        Check(actor.CaptureRuntimeState().health == 6, "Saved authored health excludes temporary bonus");
        Reset(actor); Check(actor.Health == 6 && !CharacterPotionBuffs.Has(actor, AlchemyPotionKind.Vitality), "Old/default buff state clears preexisting effects");

        actor.TrySpendPossessionEnergy(2); CharacterPotionBuffs.TryApply(actor, AlchemyPotionKind.Spirit);
        Check(actor.FinalPossessionEnergy == 3 && actor.MaxPossessionEnergy == 3, "Blue restores four but never overflows base energy");
        CharacterPotionBuffs.TryApply(actor, AlchemyPotionKind.Spirit);
        Check(actor.MaxPossessionEnergy == 5 && actor.FinalPossessionEnergy == 3, "Full energy grants two unfilled temporary capacity");
        CharacterPotionBuffs.TryApply(actor, AlchemyPotionKind.Spirit);
        Check(actor.FinalPossessionEnergy == 5 && actor.MaxPossessionEnergy == 5, "Second blue fills temporary reserve without stacking");
        CharacterPotionBuffs.TryApply(actor, AlchemyPotionKind.Spirit);
        Check(actor.MaxPossessionEnergy == 5, "Repeated full blue only refreshes");
        Check(actor.TrySpendPossessionEnergy(4) && actor.FinalPossessionEnergy == 1 && growth.PotionEnergyCurrent == 1, "Energy spend crosses base then temporary pools safely");
        var growthSaved = JsonUtility.ToJson(growth.CaptureRuntimeState());
        growth.ApplyRuntimeState(default); Check(actor.MaxPossessionEnergy == 3, "Clearing player state removes temporary energy");
        growth.ApplyRuntimeState(JsonUtility.FromJson<PlayerGrowthAttributes.RuntimeState>(growthSaved));
        Check(actor.MaxPossessionEnergy == 5 && growth.PotionEnergyCurrent == 1, "Player energy state survives save JSON");
        var nextBody = Actor("Next controlled body", new Vector3(20, 0, 0));
        mover.enabled = false; nextBody.GetComponent<ZeldaFourWayMover>().enabled = true;
        ZeldaRuntimeRegistry.ClaimControlledMover(nextBody.GetComponent<ZeldaFourWayMover>());
        Check(nextBody.MaxPossessionEnergy == 5 && nextBody.FinalPossessionEnergy == 4 && actor.MaxPossessionEnergy == 3,
            "Temporary energy follows player, not previously controlled body");
        nextBody.GetComponent<ZeldaFourWayMover>().enabled = false; mover.enabled = true; ZeldaRuntimeRegistry.ClaimControlledMover(mover);
        growth.ApplyRuntimeState(default); Reset(actor);

        float baseSpeed = actor.MoveSpeed; int basePower = actor.FinalAttackPower;
        CharacterPotionBuffs.TryApply(actor, AlchemyPotionKind.Might);
        Check(actor.FinalAttackPower == basePower + 3, "Might uses existing strength/attack attribute +3");
        CharacterPotionBuffs.TryApply(actor, AlchemyPotionKind.Might);
        Check(actor.FinalAttackPower == basePower + 3, "Might does not stack");
        CharacterPotionBuffs.TryApply(actor, AlchemyPotionKind.Berserk);
        Check(actor.FinalAttackPower == basePower + 5 && Mathf.Abs(actor.MoveSpeed - baseSpeed * .8f) < .001f, "Berserk adds two strength and reduces speed 20 percent");
        Check(CharacterPotionBuffs.AttackIntervalMultiplier(actor) == .65f, "Berserk attack interval multiplier");
        CharacterPotionBuffs.TryApply(actor, AlchemyPotionKind.Veil);
        Check(Mathf.Abs(actor.MoveSpeed - baseSpeed * .8f * 1.4f) < .001f, "Veil speed combines without changing authored movement");
        CharacterPotionBuffs.NotifyAction(actor);
        Check(!CharacterPotionBuffs.Has(actor, AlchemyPotionKind.Veil), "Successful offensive/item action breaks invisibility");
        CharacterPotionBuffs.TryApply(actor, AlchemyPotionKind.Weakness);
        Check(actor.Health == 3 && actor.CurrentHealth == 3 && actor.FinalAttackPower == 1, "Weakness clamps HP and overrides all offensive buffs");
        var state = actor.CaptureRuntimeState(); state.health = 2; state.currentHealth = 2; actor.ApplyRuntimeState(state);
        Check(actor.Health == 1 && actor.CurrentHealth == 1, "Weakness minimum HP cap is one");
        var saved = JsonUtility.ToJson(actor.CaptureRuntimeState()); Reset(actor);
        actor.ApplyRuntimeState(JsonUtility.FromJson<ZeldaCharacterData.RuntimeState>(saved));
        Check(actor.Health == 1 && actor.FinalAttackPower == 1 && CharacterPotionBuffs.Has(actor, AlchemyPotionKind.Might), "Body buffs restore from JSON without modifying base values");

        Reset(actor);
        var hitPrefab = new GameObject("Potion test melee prefab"); hitPrefab.AddComponent<ZeldaAttackHitbox>(); hitPrefab.SetActive(false);
        Set(actor, "attackPrefab", hitPrefab);
        CharacterPotionBuffs.TryApply(actor, AlchemyPotionKind.Berserk);
        CharacterPotionBuffs.TryApply(actor, AlchemyPotionKind.Veil);
        Check(mover.TryPerformAttack(Vector2.right), "Normal attack still works with buffs");
        Check(!CharacterPotionBuffs.Has(actor, AlchemyPotionKind.Veil) && Mathf.Abs(Get<float>(mover, "attackTimer") - actor.AttackDuration * .65f) < .001f,
            "Attack breaks Veil and shortens attack lock by 35 percent");
        mover.CancelAttackForStun();
        var hit = new GameObject("Berserk contact region").AddComponent<ZeldaAttackHitbox>();
        hit.Configure(.2f, actor.AttackSize, Color.white, 1, actor, ZeldaAttackVisualShape.Sword, false, true);
        Check(hit.GetComponent<BoxCollider2D>().size.x > 1f && Mathf.Abs(hit.transform.localScale.x - actor.AttackSize.x) < .001f,
            "Berserk expands contact region but keeps weapon rendering scale");
        var victim = Actor("Sweep target", Vector3.right);
        Call(hit, "TryDamage", victim.GetComponent<Collider2D>());
        Call(hit, "TryDamage", victim.GetComponent<Collider2D>());
        Check(victim.CurrentHealth == 5, "Same swing damages each target once");
        UnityEngine.Object.Destroy(hit.gameObject);

        Reset(actor); Reset(victim); actor.transform.position = Vector3.zero; victim.transform.position = Vector3.right;
        CharacterPotionBuffs.TryApply(actor, AlchemyPotionKind.Might); Physics2D.SyncTransforms();
        CharacterPotionBuffs.TryMeleeKnockback(actor, victim);
        Check(victim.transform.position.x > 1.4f, "Might pushes ordinary target approximately one body width");
        victim.transform.position = Vector3.right;
        var wall = new GameObject("Knockback blocking wall"); wall.transform.position = new Vector3(1.4f, 0, 0); wall.AddComponent<BoxCollider2D>().size = new Vector2(.1f, 2f);
        Physics2D.SyncTransforms(); CharacterPotionBuffs.TryMeleeKnockback(actor, victim);
        Check(victim.transform.position.x < 1.15f, "Knockback cannot push through a solid obstacle");
        wall.GetComponent<Collider2D>().isTrigger = true; wall.layer = LayerMask.NameToLayer("Blocks");
        victim.transform.position = Vector3.right; Physics2D.SyncTransforms();
        CharacterPotionBuffs.TryMeleeKnockback(actor, victim);
        Check(victim.transform.position.x < 1.15f, "Knockback also respects Blocks-layer trigger walls");
        UnityEngine.Object.Destroy(wall); UnityEngine.Object.Destroy(victim.gameObject);

        Reset(actor, 4); actor.transform.position = new Vector3(0, 10, 0);
        CharacterPotionBuffs.TryApply(actor, AlchemyPotionKind.Echo);
        Check(Mathf.Abs(actor.GetComponent<CharacterPotionBuffs>().Remaining(AlchemyPotionKind.Echo) - 120f) < .05f, "Echo retains requested original 120 seconds");
        actor.transform.position = new Vector3(4, 10, 0); actor.TakeDamage(100);
        Check(!actor.IsDead && actor.CurrentHealth == 4 && Vector3.Distance(actor.transform.position, new Vector3(0, 10, 0)) < .01f,
            "Lethal damage recalls and restores HP before death lifecycle");
        Check(!CharacterPotionBuffs.Has(actor, AlchemyPotionKind.Echo) && CharacterPotionBuffs.IsInvulnerable(actor), "Echo is consumed and grants one second protection");
        actor.TakeDamage(100); Check(!actor.IsDead && actor.CurrentHealth == 4, "Protection blocks repeated same-frame lethal hit");
        Reset(actor, 4); CharacterPotionBuffs.TryApply(actor, AlchemyPotionKind.Echo);
        var original = actor.transform.position;
        var occupied = new GameObject("Occupied echo point"); occupied.transform.position = original; occupied.AddComponent<BoxCollider2D>().size = Vector2.one;
        occupied.layer = LayerMask.NameToLayer("Hidden Blocks"); occupied.GetComponent<Collider2D>().isTrigger = true;
        actor.transform.position += Vector3.right * 4; actor.TakeDamage(100);
        Check(!actor.IsDead && Vector3.Distance(actor.transform.position, original) > .5f && Vector3.Distance(actor.transform.position, original) < 3f,
            "Anchor occupied by Hidden Blocks trigger uses a nearby clear position");
        UnityEngine.Object.Destroy(occupied);
        Reset(actor); CharacterPotionBuffs.TryApply(actor, AlchemyPotionKind.Echo);
        mover.enabled = false;
        var foreign = SceneManager.CreateScene("Potion Buff Smoke B"); SceneManager.MoveGameObjectToScene(actor.gameObject, foreign);
        Call(actor.GetComponent<CharacterPotionBuffs>(), "Update");
        Check(!CharacterPotionBuffs.Has(actor, AlchemyPotionKind.Echo), "Echo cannot teleport across gameplay scenes");
        Time.timeScale = 1f; Call(actor.GetComponent<CharacterPotionBuffs>(), "Update"); Time.timeScale = 0f;
        SceneManager.MoveGameObjectToScene(actor.gameObject, scene);
        Check(!CharacterPotionBuffs.Has(actor, AlchemyPotionKind.Echo), "Returning to scene does not rearm spent travel anchor");
        Reset(actor); mover.enabled = true; ZeldaRuntimeRegistry.ClaimControlledMover(mover);

        CharacterPotionBuffs.TryApply(actor, AlchemyPotionKind.Weakness);
        var timed = actor.GetComponent<CharacterPotionBuffs>().CaptureRuntimeState(); timed.remaining[(int)AlchemyPotionKind.Weakness] = .15f;
        actor.GetComponent<CharacterPotionBuffs>().ApplyRuntimeState(timed);
        float before = actor.GetComponent<CharacterPotionBuffs>().Remaining(AlchemyPotionKind.Weakness);
        yield return null; yield return null;
        Check(Mathf.Abs(actor.GetComponent<CharacterPotionBuffs>().Remaining(AlchemyPotionKind.Weakness) - before) < .001f, "Pause freezes potion timers");
        actor.gameObject.SetActive(false); Time.timeScale = 1f;
        float end = Time.time + .3f; while (Time.time < end) yield return null;
        Check(!CharacterPotionBuffs.Has(actor, AlchemyPotionKind.Weakness), "Distance-unloaded body still expires timers");
        actor.gameObject.SetActive(true); Time.timeScale = 0f;
        Check(actor.Health == 6 && actor.CurrentHealth == 3, "Expired weakness restores cap without free healing");
        var poisonous = Actor("Poison death body", new Vector3(30, 20, 0));
        CharacterPotionBuffs.TryApply(poisonous, AlchemyPotionKind.Death);
        Check(poisonous.IsDead, "Triple green remains immediately lethal");
        var ghost = new GameObject("Potion ghost").AddComponent<GhostZeldaCharacterData>();
        Check(!CharacterPotionBuffs.TryApply(ghost, AlchemyPotionKind.Might), "Ghosts cannot drink body potions");
        Time.timeScale = 1f;
    }
}
