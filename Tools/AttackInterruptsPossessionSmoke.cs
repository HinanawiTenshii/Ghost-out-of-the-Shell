using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class AttackInterruptsPossessionSmoke
{
    const BindingFlags F = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
    static IEnumerator suite;
    static int frame = -1, checks;
    static double deadline;
    static AttackInterruptsPossessionSmoke() { EditorApplication.update += Tick; }
    public static void Run()
    {
        Check(Application.dataPath.Replace('\\','/').Contains("/.codex-temp/"), "isolated project required");
        PlayerSettings.productName = "attack-interrupts-possession-smoke";
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(nameof(AttackInterruptsPossessionSmoke), true); EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return; frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(AttackInterruptsPossessionSmoke), false))
            { SessionState.SetBool(nameof(AttackInterruptsPossessionSmoke), false); suite = Verify(); deadline = EditorApplication.timeSinceStartup + 60; }
            if (suite == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("timeout");
            if (suite.MoveNext()) return;
            suite = null; Debug.Log("ATTACK_INTERRUPTS_POSSESSION_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void Check(bool value, string label) { checks++; if (!value) throw new Exception(label); }
    static FieldInfo Field(object o, string name)
    {
        for (Type t = o.GetType(); t != null; t = t.BaseType) { var f = t.GetField(name, F); if (f != null) return f; }
        throw new Exception("missing field " + name);
    }
    static T Get<T>(object o, string name) => (T)Field(o,name).GetValue(o);
    static void Set(object o, string name, object value) => Field(o,name).SetValue(o,value);
    static object Call(object o, string name, params object[] args) => o.GetType().GetMethod(name,F).Invoke(o,args);
    static bool Advance(ZeldaFourWayMover mover, float dt, bool held) => (bool)Call(mover,"AdvanceControlSwitchProgress",dt,held);
    static ZeldaFourWayMover Actor<T>(string label, Vector2 position) where T : ZeldaCharacterData
    {
        var go = new GameObject(label); go.transform.position = position;
        var data = go.AddComponent<T>(); Set(data,"possessionCost",0);
        var mover = go.AddComponent<ZeldaFourWayMover>(); return mover;
    }
    static void Begin(ZeldaFourWayMover mover)
    {
        mover.CancelAttackForStun(); Advance(mover,0,false);
        mover.ReceiveControl(Vector2.right); ZeldaRuntimeRegistry.ClaimControlledMover(mover);
        Set(mover,"possessionInteractionSelected",true);
        Physics2D.SyncTransforms(); ZeldaRuntimeRegistry.InvalidateSpatialSnapshot();
        Check(!Advance(mover,.4f,true) && mover.IsPossessionInProgress, "channel begins normally");
    }
    static void Cancelled(ZeldaFourWayMover mover, ZeldaFourWayMover target, PossessionTransferParticles particles)
    {
        Check(!mover.IsPossessionInProgress && mover.PossessionProgress == 0f && Get<ZeldaFourWayMover>(mover,"pendingControlTarget") == null, "target and progress cleared");
        Check(!target.GetComponent<ZeldaCharacterAiBase>().IsPossessionLocked && !target.enabled && mover.enabled, "target AI unlocked, control unchanged");
        var bar = Get<ZeldaPossessionProgressBar>(mover,"possessionProgressBar");
        Check(!bar.GetComponent<SpriteRenderer>().enabled && !bar.IsFailureActive, "progress hidden without failure flash");
        Check(Get<PossessionTransferParticles>(mover,"possessionTransferParticles") == null && !Get<bool>(particles,"emitting") && Get<bool>(particles,"cancelled"), "particle stream stops and fades as cancelled");
        Check(Get<bool>(mover,"possessionAttemptRejected") && !Advance(mover,10,true) && !mover.IsPossessionInProgress, "held F cannot restart or complete interrupted channel");
    }
    static IEnumerator Verify()
    {
        while (DocumentReader.IsInputBlocked) yield return null;
        string source = File.ReadAllText(Path.Combine(Application.dataPath,"Scripts/Characters/ZeldaFourWayMover.cs"));
        int update = source.IndexOf("private void Update()");
        Check(source.IndexOf("TryPerformAttack(facingDirection);",update) < source.IndexOf("UpdateControlSwitchProgress(Time.deltaTime)",update), "attack input takes priority over same-frame channel completion");
        var player = Actor<SwordZeldaCharacterData>("Attacking Possessor",Vector2.zero);
        var attackPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ZeldaAttackHitbox.prefab");
        Set(player.CharacterData,"attackPrefab",attackPrefab);
        var target = Actor<CivilianZeldaCharacterData>("Possession Target",Vector2.right * 1.1f);
        target.enabled = false;
        var ai = target.gameObject.AddComponent<ZeldaCharacterAiBase>(); ai.enabled = false;
        for (int pass = 0; pass < 2; pass++)
        {
            Begin(player); Check(ai.IsPossessionLocked,"channel locks target AI");
            var particles = Get<PossessionTransferParticles>(player,"possessionTransferParticles");
            int energy = player.CharacterData.FinalPossessionEnergy;
            if (pass == 1) Set(player,"controlSwitchHoldTimer",2.999f);
            Check(player.TryPerformAttack(Vector2.up),"melee attack succeeds during possession");
            Cancelled(player,target,particles);
            Check(player.IsAttacking && player.CharacterData.FinalPossessionEnergy == energy,"swing continues and interruption consumes no possession energy");
            player.CancelAttackForStun(); Check(!Advance(player,10,true) && !player.IsPossessionInProgress,"still held F remains blocked after attack ends");
        }
        // Cancelling releases the latch only when F is released, not when the animation ends.
        Advance(player,0,false); Check(!Get<bool>(player,"possessionAttemptRejected"),"F release clears interruption latch");
        Set(player,"possessionInteractionSelected",true); player.ReceiveControl(Vector2.right);
        Check(!Advance(player,.2f,true) && player.IsPossessionInProgress && Get<float>(player,"controlSwitchHoldTimer") == .2f,"fresh press restarts from zero");
        player.InterruptPossessionByDamage(); Check(!player.IsPossessionInProgress && !ai.IsPossessionLocked,"existing damage interruption retained");
        Begin(player); var gunParticles = Get<PossessionTransferParticles>(player,"possessionTransferParticles");
        Check(DisposableHandgunAttackVisual.TryFire(player.CharacterData,player,10),"handgun fires during possession");
        Cancelled(player,target,gunParticles);
        player.EndItemAttack();
        Begin(player);
        Check(!DisposableHandgunAttackVisual.TryFire(player.CharacterData,player,0) && player.IsPossessionInProgress && ai.IsPossessionLocked,"failed gun attack does not interrupt");
        Set(player.CharacterData,"attackPrefab",null);
        Check(!player.TryPerformAttack(Vector2.right) && player.IsPossessionInProgress && ai.IsPossessionLocked,"missing melee prefab does not interrupt");
        Set(player.CharacterData,"attackPrefab",attackPrefab);
        Check(player.TryBeginItemAttack(.24f) && player.IsPossessionInProgress,"pickup/use reach gesture alone does not interrupt");
        Check(!Advance(player,.1f,true) && player.IsPossessionInProgress,"ordinary item pose does not cancel a channel");
        player.EndItemAttack(); Advance(player,0,false);
        Check(player.TryPerformAttack(Vector2.up),"normal attack outside possession unchanged");
        Set(player,"possessionInteractionSelected",true); player.ReceiveControl(Vector2.right);
        // ReceiveControl clears attack animation, so start another to test a new held-F channel.
        Check(player.TryPerformAttack(Vector2.right),"second normal attack begins");
        Check(!Advance(player,.3f,true) && !player.IsPossessionInProgress && !ai.IsPossessionLocked,"cannot begin channel during melee swing");
        player.CancelAttackForStun();
        foreach (var hit in Object.FindObjectsOfType<ZeldaAttackHitbox>()) Object.DestroyImmediate(hit.gameObject);
        foreach (var bullet in Object.FindObjectsOfType<DisposableHandgunBullet>()) Object.DestroyImmediate(bullet.gameObject);
        // Prove normal completion is not broken after the interrupt/retry sequence.
        Begin(player); Check(Advance(player,3.1f,true) && target.enabled && !player.enabled && !ai.IsPossessionLocked,"uninterrupted retry completes and releases lock");
        double until = EditorApplication.timeSinceStartup + .4;
        while (EditorApplication.timeSinceStartup < until) yield return null;
        Check(gunParticles == null,"cancelled particles clean themselves up");
    }
}
