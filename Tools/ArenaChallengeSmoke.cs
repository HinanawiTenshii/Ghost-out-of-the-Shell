using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class ArenaChallengeSmoke
{
    const string Key = "ArenaChallengeSmoke";
    static readonly Stack<IEnumerator> steps = new Stack<IEnumerator>();
    static int checks, frame = -1;
    static double deadline;
    static ArenaChallengeSmoke() { EditorApplication.update += Tick; }
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\','/').Contains("/.codex-temp/")) throw new Exception("isolated only");
        PlayerSettings.productName = "arena-challenge-smoke";
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/Level2/Level2-Floor-1.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/Level2/Level2-Floor1.unity", true) };
        EditorSceneManager.OpenScene(EditorBuildSettings.scenes[0].path);
        var arena = UnityEngine.Object.FindObjectOfType<ArenaChallengeController>();
        Check(arena != null && arena.guardians.Length == 3 && arena.guardians.All(g => g != null), "three authored guardians");
        Check(arena.registrar != null && arena.scoreboard != null && arena.reward != null, "registrar/score/reward links");
        Check(arena.sealedDoors.Length == 5 && arena.entrance != null, "six arena doors bound");
        Check(arena.registrar.FindNode("arena_start").choices.Length == 2, "registration choices");
        Check(arena.GetComponent<CameraVisionStreamingExempt>() != null, "controller cannot be distance unloaded");
        foreach (string id in new[] { QuestJournalManager.ArenaRegistrarTarget, QuestJournalManager.ArenaEntranceTarget, QuestJournalManager.ArenaCombatTarget })
            Check(RuntimeMiniMapGraphic.TryGetSceneQuestTarget(arena.gameObject.scene, id, out _), "live arena map target " + id);
        typeof(RuntimeMiniMapSceneDataGenerator).GetMethod("GenerateSceneSnapshot",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,
            new object[] { EditorBuildSettings.scenes[0].path, "Assets/Resources/MiniMaps/Level2-Floor-1.asset", "Level2-Floor-1" });
        AssetDatabase.SaveAssetIfDirty(Resources.Load<RuntimeMiniMapSceneData>("MiniMaps/Level2-Floor-1"));
        SessionState.SetInt(Key+"checks", checks); SessionState.SetBool(Key,true);
        EditorApplication.EnterPlaymode();
    }
    static void Check(bool ok,string label) { checks++; if (!ok) throw new Exception("ARENA ASSERT: " + label); Debug.Log("ARENA_CHECK " + label); }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return; frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(Key,false))
            {
                SessionState.SetBool(Key,false); checks = SessionState.GetInt(Key+"checks",0);
                steps.Push(Test()); deadline = EditorApplication.timeSinceStartup + 650;
            }
            if (steps.Count == 0) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("ARENA timeout");
            while(steps.Count > 0)
            {
                var step = steps.Peek(); if (!step.MoveNext()) { steps.Pop(); continue; }
                if(step.Current is IEnumerator nested) { steps.Push(nested); continue; } return;
            }
            Debug.Log("ARENA_CHALLENGE_PASS checks="+checks); EditorApplication.Exit(0);
        }
        catch(Exception ex) { steps.Clear(); Debug.LogException(ex); EditorApplication.Exit(1); }
    }
    static ArenaChallengeController A => UnityEngine.Object.FindObjectOfType<ArenaChallengeController>();
    static ZeldaFourWayMover P => ZeldaRuntimeRegistry.GetControlledMover();
    static QuestJournalManager J => QuestJournalManager.GetOrCreate();
    const string Quest = QuestJournalManager.ArenaQuestId;
    static void QuestStage(QuestJournalManager.ArenaQuestStage stage, int wins, int rounds, string target)
    {
        Check(J.ContainsEntry(Quest) && J.ArenaStage == stage && J.ArenaWins == wins && J.ArenaRounds == rounds,
            "quest projection " + stage + " " + wins + "/" + rounds);
        Check(QuestJournalManager.ResolveQuestMapTargetId(Quest) == target, "quest map stage " + stage);
        var texts = new List<string>(); var done = new List<bool>();
        typeof(ZeldaHealthHeartsUI).GetMethod("BuildQuestObjectiveState", BindingFlags.Static|BindingFlags.NonPublic)
            .Invoke(null, new object[] { Quest, J, texts, done });
        Check(texts.Count >= 1 && texts.Last().Contains("领取试炼证明") && done.Last() == J.IsEntryCompleted(Quest), "quest HUD reward objective " + stage);
        if (rounds > 0) Check(texts.Any(t => t.Contains(wins + "/" + rounds)), "quest HUD real roster count " + stage);
    }
    static IEnumerator Ready()
    {
        int stable=0;
        while(stable<3)
        {
            bool ready = !DocumentReader.IsInputBlocked && !CharacterPotionBuffs.IsRestoringWorld && !GameSaveSystem.IsPreparingSceneSave;
            stable=ready?stable+1:0; yield return null;
        }
    }
    static void Move(ZeldaFourWayMover mover,Vector2 position)
    { mover.transform.position=position; mover.GetComponent<Rigidbody2D>().position=position; Physics2D.SyncTransforms(); }
    static void HealPlayer() { P.CharacterData.RestoreHealth(999); }
    static IEnumerator EnterFight()
    {
        Move(P,A.fightingArea.center);
        yield return null; yield return null;
        Check(A.CurrentPhase==ArenaChallengeController.Phase.Fighting,"crossing entrance starts combat");
        Check(A.entrance.IsLocked&&!A.entrance.IsOpen,"entry seals during combat");
        QuestStage(QuestJournalManager.ArenaQuestStage.Fighting, A.Wins, A.RoundCount, QuestJournalManager.ArenaCombatTarget);
    }
    static IEnumerator Test()
    {
        yield return Ready();
        var arena=A;
        Check(!J.ContainsEntry(Quest), "fresh arena scene does not auto-accept quest");
        CameraVisionObjectStreaming.RestoreForGameplay(arena.registrar.gameObject);
        arena.registrar.gameObject.SetActive(true);
        var registrarAi = arena.registrar.GetComponent<ZeldaCharacterAiBase>(); bool registrarAiEnabled = registrarAi.enabled;
        registrarAi.enabled = false;
        yield return null;
        var questMarker = arena.registrar.GetComponent<QuestJournalInteractionMarker>();
        Check(questMarker != null && (bool)typeof(CharacterDialogue).GetProperty("HasAvailableQuest", BindingFlags.Instance|BindingFlags.NonPublic).GetValue(arena.registrar), "registrar has dialogue quest offer");
        registrarAi.enabled = registrarAiEnabled;
        J.AddOrUpdateEntry("arena-smoke-unrelated", "untouched", "outside arena");
        var actor=arena.residents.First(ai => ai.gameObject != arena.registrar.gameObject && !arena.guardians.Any(g=>g.gameObject==ai.gameObject));
        CameraVisionObjectStreaming.RestoreForGameplay(actor.gameObject); actor.gameObject.SetActive(true);
        var old=P; if(old!=null) old.enabled=false;
        var mover=actor.GetComponent<ZeldaFourWayMover>(); mover.ReceiveControl(Vector2.right); ZeldaRuntimeRegistry.ClaimControlledMover(mover);
        var health=mover.CharacterData.CaptureRuntimeState(); health.health=20;health.currentHealth=14; mover.CharacterData.ApplyRuntimeState(health);
        Move(mover,new Vector2(70,-11));
        string identity=mover.CharacterData.IndividualAccessId;
        foreach(var area in UnityEngine.Object.FindObjectsOfType<TemporaryCharacterEffectArea>(true))
            if(area.name.Contains("左侧")) { var effect=area.CaptureEffectSettings();effect.restoreHealth=false;area.ApplyEffectSettings(effect); }
        arena.guardianPool.ApplyEffectSettings(TemporaryCharacterEffectArea.EffectSettings.ForPotion(AlchemyPotionKind.Berserk));
        var bag=PersistentInventory.Instance;bag.ClearAll();bag.SetSlot(0,"gold_coin",9);bag.SetSlot(1,"disposable_handgun",2);
        var outside=UnityEngine.Object.FindObjectsOfType<DoorHingeInteraction>(true).First(d=>!arena.region.Contains(d.transform.position));
        outside.ApplyPersistentState(false,true); string outsideId=SceneTravelStateManager.GetMapObjectId(outside.transform);
        Check(arena.Register(mover),"register starts covered layout reload");
        Check(J.TrackedEntryId == Quest && J.TryGetEntry(Quest, out var entry) && entry.Title == "你们还不满意吗？", "registration starts and tracks named quest");
        yield return Ready(); arena=A;
        Check(P.CharacterData.IndividualAccessId==identity,"same participant identity after layout reload");
        Check(P.CharacterData.CurrentHealth==14,"registration does not restore player health");
        Check(Vector2.Distance(P.transform.position,arena.playerPool.transform.position)<.2f,"player appears in left pool");
        Check(Vector2.Distance(arena.Opponent.transform.position,arena.guardianPosition)<.2f,"guardian placed on arena right");
        Check(arena.CurrentPhase==ArenaChallengeController.Phase.Preparing&&arena.Wins==0,"preparation score zero");
        QuestStage(QuestJournalManager.ArenaQuestStage.Preparing, 0, 3, QuestJournalManager.ArenaEntranceTarget);
        Check(!(bool)typeof(CharacterDialogue).GetProperty("HasAvailableQuest", BindingFlags.Instance|BindingFlags.NonPublic).GetValue(arena.registrar), "accepted arena quest hides registrar offer");
        Check(arena.sealedDoors.All(d=>d.IsLocked&&!d.IsOpen&&!d.TryInteract()),"all other exits locked/noninteractive");
        var panel=arena.sealedDoors[0].GetComponentInChildren<DoorData>();panel.ReceiveAttack(999);
        Check(!panel.IsDestroyed,"protected exit cannot be destroyed");
        Check(CharacterPotionBuffs.Has(arena.Opponent,AlchemyPotionKind.Berserk),"guardian inherits right pool potion");
        var buff=arena.Opponent.GetComponent<CharacterPotionBuffs>();float remain=buff.DisplayRemaining(AlchemyPotionKind.Berserk,out float duration);
        Check(Mathf.Approximately(duration,CharacterPotionBuffs.Duration(AlchemyPotionKind.Berserk)*2)&&remain>duration-8,"guardian buff has doubled duration");
        Check(PersistentInventory.Instance.GetQuantity("gold_coin")==9,"inventory survives registration");
        J.AddOrUpdateEntry(QuestJournalManager.ArenaInjectionQuestId, QuestJournalManager.ArenaInjectionQuestTitle,
            "injection quest fixture", "Level2-Floor-1", Quest);
        Check(GameSaveSystem.Instance.Save(1,false),"save during preparation");
        Check(GameSaveSystem.Instance.Load(1),"load preparation save");yield return Ready();
        Check(A.CurrentPhase==ArenaChallengeController.Phase.Preparing&&A.RoundCount==3,"preparation resumes from disk");
        Check(J.ContainsEntry(QuestJournalManager.ArenaInjectionQuestId) && !J.IsEntryCompleted(QuestJournalManager.ArenaInjectionQuestId),
            "injection child remains pending through preparation reload");
        QuestStage(QuestJournalManager.ArenaQuestStage.Preparing, 0, 3, QuestJournalManager.ArenaEntranceTarget);
        Check(A.Opponent.GetComponent<CharacterPotionBuffs>().Remaining(AlchemyPotionKind.Berserk)>CharacterPotionBuffs.Duration(AlchemyPotionKind.Berserk),"doubled duration survives disk load");
        PersistentInventory.Instance.TryRemoveItem("disposable_handgun",1);
        Move(P,new Vector2(70,-11));P.CharacterData.TakeDamage(2,null);int hp=P.CharacterData.CurrentHealth;
        J.CompleteEntry(QuestJournalManager.ArenaInjectionQuestId);
        Check(A.Abandon(),"abandon accepted during preparation");yield return Ready();
        Check(A.CurrentPhase==ArenaChallengeController.Phase.Idle&&string.IsNullOrEmpty(A.CaptureRuntimeState().layoutSnapshot),"abandon restores layout and deletes snapshot");
        QuestStage(QuestJournalManager.ArenaQuestStage.Register, 0, 3, QuestJournalManager.ArenaRegistrarTarget);
        Check(J.ContainsEntry("arena-smoke-unrelated") && !J.IsEntryCompleted(Quest), "abandon preserves other quests and keeps challenge unfinished");
        Check(J.IsEntryCompleted(QuestJournalManager.ArenaInjectionQuestId), "abandon layout restoration keeps completed injection child");
        Check(PersistentInventory.Instance.GetQuantity("disposable_handgun")==1&&P.CharacterData.CurrentHealth==hp,"abandon preserves item and health expenditure");
        outside=UnityEngine.Object.FindObjectsOfType<DoorHingeInteraction>(true).Single(d=>SceneTravelStateManager.GetMapObjectId(d.transform)==outsideId);
        Check(outside.IsOpen&&!outside.IsLocked,"outside-arena door progress unchanged");

        HealPlayer();Check(A.Register(P),"challenge may restart after abandon");yield return Ready();
        yield return EnterFight();
        var ai=A.Opponent.GetComponent<ZeldaCharacterAiBase>();
        for(int i=0;i<8;i++) yield return null;
        Check(ai.CurrentState==ZeldaAiState.Hostile&&ai.CurrentTarget==P,"guardian attacks without permission warning");
        Check(A.residents.Where(r=>r!=ai&&r!=null&&r.enabled).All(r=>r.CurrentState!=ZeldaAiState.Hostile),"spectators do not chain hostility");
        Check(GameSaveSystem.Instance.Save(2,false),"save during fight");
        Check(GameSaveSystem.Instance.Load(2),"load fight save");yield return Ready();
        Check(A.CurrentPhase==ArenaChallengeController.Phase.Fighting&&A.entrance.IsLocked,"combat/closed gate resume");
        P.CharacterData.TakeDamage(999,A.Opponent);
        Check(P.CharacterData.CurrentHealth==1&&!P.CharacterData.IsDead,"lethal hit leaves player at one HP");
        Check(A.CurrentPhase==ArenaChallengeController.Phase.Result&&ArenaChallengeController.BlocksPlayerInput,"result freezes input");
        Check(A.scoreboard.display.text=="×"&&A.scoreboard.display.color.r>.9f,"failure red X");
        QuestStage(QuestJournalManager.ArenaQuestStage.Defeated, 0, 3, QuestJournalManager.ArenaRegistrarTarget);
        P.CharacterData.TakeDamage(999,A.Opponent);Check(P.CharacterData.CurrentHealth==1,"result blocks followup lethal hits");
        yield return Ready();
        Check(A.CurrentPhase==ArenaChallengeController.Phase.Idle&&P.CharacterData.CurrentHealth==1,"failure restores layout without healing");
        QuestStage(QuestJournalManager.ArenaQuestStage.Register, 0, 3, QuestJournalManager.ArenaRegistrarTarget);

        HealPlayer();Check(A.Register(P),"register after failure");yield return Ready();
        var fought=new HashSet<string>();
        for(int round=0;round<3;round++)
        {
            yield return EnterFight();
            var guard=A.Opponent;Check(fought.Add(guard.IndividualAccessId),"distinct guardian round "+round);
            guard.TakeDamage(999,P.CharacterData);
            Check(!guard.IsDead&&guard.CurrentHealth==1,"guardian lethal protection round "+round);
            Check(A.Wins==round+1,"score increments round "+round);
            QuestStage(QuestJournalManager.ArenaQuestStage.RoundWon, round + 1, 3, QuestJournalManager.ArenaCombatTarget);
            if(round==0)
            {
                Check(GameSaveSystem.Instance.Save(3,false),"save during result pause");
                Check(GameSaveSystem.Instance.Load(3),"load result save");
            }
            yield return Ready();
            if(round<2) Check(A.CurrentPhase==ArenaChallengeController.Phase.Preparing,"next round preparation");
        }
        Check(A.CurrentPhase==ArenaChallengeController.Phase.Completed,"all guardians produce completion");
        QuestStage(QuestJournalManager.ArenaQuestStage.AwaitingReward, 3, 3, QuestJournalManager.ArenaRegistrarTarget);
        Check(!J.IsEntryCompleted(Quest), "winning without claiming does not complete quest");
        Check(J.IsEntryCompleted(QuestJournalManager.ArenaInjectionQuestId), "round and victory layout reloads keep completed injection child");
        Check(A.registrar.GetStartNode(P).id=="arena_victory","same body gets victory dialogue");
        var different=A.registrar.GetComponent<ZeldaFourWayMover>();Check(A.registrar.GetStartNode(different).id=="arena_other","another body cannot claim");
        Check(!A.CanRegister(different,out _),"living unclaimed winner reserves the challenge");
        Check(GameSaveSystem.Instance.Save(1,false),"save unclaimed victory fixture");
        var unclaimedState=A.CaptureRuntimeState();
        A.ApplyRuntimeState(unclaimedState);A.CompleteRestoration();
        P.CharacterData.KillFromPotion();
        Check(A.CurrentPhase==ArenaChallengeController.Phase.Idle&&A.CaptureRuntimeState().unclaimedWinnerDied,"unclaimed winner real death reopens arena");
        QuestStage(QuestJournalManager.ArenaQuestStage.WinnerDied, 0, 0, QuestJournalManager.ArenaRegistrarTarget);
        Check(A.registrar.GetStartNode(different).id=="arena_retry"&&A.CanRegister(different,out _),"replacement gets retry dialogue and can register");
        Check(unclaimedState.phase==ArenaChallengeController.Phase.Completed,"live death does not mutate applied snapshot");
        Check(A.scoreboard.display.text=="等待挑战者","winner death resets scoreboard");
        Check(GameSaveSystem.Instance.Load(1),"reload unclaimed victory");yield return Ready();
        Check(A.CurrentPhase==ArenaChallengeController.Phase.Completed&&A.registrar.GetStartNode(P).id=="arena_victory","prior save restores living winner entitlement");
        SceneTravelStateManager.GetOrCreate().TravelToScene("Level2-Floor1",new Vector2(0,0));yield return Ready();
        P.CharacterData.KillFromPotion();yield return Ready();
        QuestStage(QuestJournalManager.ArenaQuestStage.WinnerDied, 0, 0, QuestJournalManager.ArenaRegistrarTarget);
        Check(GameSaveSystem.Instance.Save(2,false)&&GameSaveSystem.Instance.Load(2),"save/load after winner death in another scene");yield return Ready();
        SceneTravelStateManager.GetOrCreate().TravelToScene("Level2-Floor-1",new Vector2(70,-11));yield return Ready();
        different=A.registrar.GetComponent<ZeldaFourWayMover>();
        Check(A.CurrentPhase==ArenaChallengeController.Phase.Idle&&A.registrar.GetStartNode(different).id=="arena_retry","unloaded arena remembers winner death after disk restore");
        var replacement=A.residents.First(ai=>ai!=null&&ai.gameObject!=A.registrar.gameObject&&!A.guardians.Any(g=>g!=null&&g.gameObject==ai.gameObject)&&!ai.GetComponent<ZeldaCharacterData>().IsDead);
        CameraVisionObjectStreaming.RestoreForGameplay(replacement.gameObject);replacement.gameObject.SetActive(true);
        P.enabled=false;var nextBody=replacement.GetComponent<ZeldaFourWayMover>();nextBody.ReceiveControl(Vector2.right);ZeldaRuntimeRegistry.ClaimControlledMover(nextBody);HealPlayer();
        Check(A.Register(P),"replacement actually starts a new challenge");yield return Ready();
        Check(A.CurrentPhase==ArenaChallengeController.Phase.Preparing&&A.Wins==0&&!A.CaptureRuntimeState().unclaimedWinnerDied,"replacement starts with fresh score and roster");
        QuestStage(QuestJournalManager.ArenaQuestStage.Preparing, 0, 3, QuestJournalManager.ArenaEntranceTarget);
        Check(GameSaveSystem.Instance.Load(1),"restore original winner for reward checks");yield return Ready();
        bag=PersistentInventory.Instance;for(int i=0;i<bag.SlotCount;i++) bag.SetSlot(i,"gold_coin",20);
        Check(!A.CanClaim(P,out _)&&!A.Claim(P),"full inventory cannot consume reward");
        Check(!J.IsEntryCompleted(Quest), "full bag cannot falsely complete quest");
        bag.ClearSlot(4);Check(A.Claim(P),"claim proof");Check(bag.GetQuantity(TrialProofPickupItem.DefaultItemId)==1,"one proof granted");
        QuestStage(QuestJournalManager.ArenaQuestStage.Claimed, 3, 3, null);
        Check(J.IsEntryCompleted(Quest), "actual reward transaction completes quest");
        Check(!A.Claim(P)&&A.registrar.GetStartNode(P).id=="arena_claimed","reward cannot duplicate");
        different=A.registrar.GetComponent<ZeldaFourWayMover>();
        Check(!A.CanRegister(P,out _)&&!A.CanRegister(different,out _)&&A.registrar.GetStartNode(different).id=="arena_closed","claim permanently closes entry for all bodies with different dialogue");
        Check(GameSaveSystem.Instance.Save(4,false)&&GameSaveSystem.Instance.Load(4),"save/load claimed reward");yield return Ready();
        Check(!A.CanClaim(P,out _)&&PersistentInventory.Instance.GetQuantity(TrialProofPickupItem.DefaultItemId)==1,"reward flag and inventory persist together");
        Check(J.IsEntryCompleted(Quest) && J.ArenaStage == QuestJournalManager.ArenaQuestStage.Claimed, "completed task persists through disk load");
        string winningId=P.CharacterData.IndividualAccessId;
        SceneTravelStateManager.GetOrCreate().TravelToScene("Level2-Floor1",new Vector2(0,0));yield return Ready();
        Check(SceneManager.GetActiveScene().name=="Level2-Floor1","winner can leave arena scene");
        SceneTravelStateManager.GetOrCreate().TravelToScene("Level2-Floor-1",new Vector2(70,-11));yield return Ready();
        Check(P.CharacterData.IndividualAccessId==winningId&&A.registrar.GetStartNode(P).id=="arena_claimed","winner identity/reward survive round-trip scene travel");
        P.CharacterData.KillFromPotion();
        Check(A.CurrentPhase==ArenaChallengeController.Phase.Completed&&A.CaptureRuntimeState().claimed,"claimed winner local death never reopens arena");
        Check(GameSaveSystem.Instance.Load(4),"restore claimed victory for travel death");yield return Ready();
        SceneTravelStateManager.GetOrCreate().TravelToScene("Level2-Floor1",new Vector2(0,0));yield return Ready();
        P.CharacterData.KillFromPotion();yield return Ready();
        Check(GameSaveSystem.Instance.Save(2,false)&&GameSaveSystem.Instance.Load(2),"claimed winner cross-scene death persists through save");yield return Ready();
        SceneTravelStateManager.GetOrCreate().TravelToScene("Level2-Floor-1",new Vector2(70,-11));yield return Ready();
        Check(A.CaptureRuntimeState().claimed&&A.CurrentPhase==ArenaChallengeController.Phase.Completed&&A.registrar.GetStartNode(P).id=="arena_closed"&&!A.CanRegister(A.registrar.GetComponent<ZeldaFourWayMover>(),out _),"claimed death elsewhere cannot reopen on return");
        Check(GameSaveSystem.Instance.Load(4),"restore living character for poison checks");yield return Ready();

        // Separate fresh-state fixture for the poison roster rule; no scene-file mutation.
        J.ResetAllEntries();
        A.ApplyRuntimeState(new ArenaChallengeController.RuntimeState());A.CompleteRestoration();HealPlayer();
        foreach(var guard in A.guardians) guard.RestoreHealth(999);
        A.guardianPool.ApplyEffectSettings(TemporaryCharacterEffectArea.EffectSettings.ForPotion(AlchemyPotionKind.Death));
        Check(A.Register(P),"poison pool registration");yield return Ready();
        Check(A.RoundCount==2,"poison skips exactly one guardian");
        QuestStage(QuestJournalManager.ArenaQuestStage.Preparing, 0, 2, QuestJournalManager.ArenaEntranceTarget);
        Check(A.Opponent!=null&&!A.Opponent.IsDead,"poison does not kill selected guardian");
        yield return EnterFight();P.CharacterData.KillFromPotion();
        Check(!P.CharacterData.IsDead&&P.CharacterData.CurrentHealth==1&&A.CurrentPhase==ArenaChallengeController.Phase.Result,"direct poison death also resolves nonlethal loss");
        yield return Ready();

        // A pre-arena save lacks the controller/scoreboard, not a destroyed arena.
        Check(GameSaveSystem.Instance.Save(5,false),"capture legacy compatibility fixture");
        var saved=GameSaveSystem.Instance.Read(5);
        var type=typeof(SceneTravelStateManager).GetNestedType("DiskWorld",BindingFlags.NonPublic);
        var world=JsonUtility.FromJson(saved.world,type);
        var scenes=(IList)type.GetField("scenes").GetValue(world);
        string arenaPath=SceneTravelStateManager.GetMapObjectId(A.transform);
        foreach(var scene in scenes)
        {
            foreach(string collection in new[]{"objects","transforms"})
            {
                var list=(IList)scene.GetType().GetField(collection).GetValue(scene);
                for(int i=list.Count-1;i>=0;i--)
                {
                    string path=(string)list[i].GetType().GetField("path").GetValue(list[i]);
                    if(path==arenaPath||path.StartsWith(arenaPath+"/",StringComparison.Ordinal)) list.RemoveAt(i);
                }
            }
        }
        saved.world=JsonUtility.ToJson(world);
        Check(RetroSceneLoadReveal.BeginTransition(saved.scene,()=>typeof(GameSaveSystem).GetMethod("LoadCovered",BindingFlags.Instance|BindingFlags.NonPublic)
            .Invoke(GameSaveSystem.Instance,new object[]{saved})),"load legacy world without arena records");
        yield return Ready();
        Check(A!=null&&A.gameObject.activeInHierarchy&&A.scoreboard.display.gameObject.activeInHierarchy,"old save keeps newly installed arena/scoreboard");
        Check(A.CurrentPhase==ArenaChallengeController.Phase.Idle&&A.registrar.GetStartNode(P).id=="arena_start","old save starts fresh registration");
    }
}
