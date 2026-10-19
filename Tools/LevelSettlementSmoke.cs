using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class LevelSettlementSmoke
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    static IEnumerator steps;static int checks,frame=-1;static double deadline;
    static LevelSettlementSmoke(){EditorApplication.update+=Tick;}
    static void Check(bool value,string label){checks++;if(!value)throw new Exception(label);}
    static void CheckCursorPolicy(bool visible,string label)
    {
        // Exercise the exact late-frame policy that used to hide the pointer
        // after the settlement Update had just made it visible.
        typeof(RuntimeUiVisibility).GetMethod("UpdateCursorVisibility",F).Invoke(null,null);
        Check(Cursor.visible==visible&&Cursor.lockState==CursorLockMode.None,label);
    }
    static void Set(object obj,string name,object value){for(var type=obj.GetType();type!=null;type=type.BaseType){var f=type.GetField(name,F);if(f!=null){f.SetValue(obj,value);return;}}throw new Exception(name);}
    public static void Run()
    {
        Check(Application.dataPath.Replace('\\','/').Contains("/.codex-temp/"),"isolated only");
        PlayerSettings.productName="level-settlement-smoke";
        var catalog=Resources.Load<LevelSettlementCatalog>("LevelSettlementCatalog");
        Check(catalog!=null&&catalog.font!=null&&catalog.expansionVisual!=null,"catalog actual font and pickup");
        Check(catalog.levels[0].skillPoints==2&&catalog.levels[1].skillPoints==6&&catalog.levels[2].skillPoints==19,"authored totals 2/6/19");
        Check(catalog.levels[0].inventoryExpansions==0&&catalog.levels[1].inventoryExpansions==0&&catalog.levels[2].inventoryExpansions==1,"MEMO totals 0/0/1");
        Check(catalog.levels[0].characterSkills.SequenceEqual(new[]{"combat_expertise"}),"Level0 actual character skill");
        Check(catalog.levels[1].characterSkills.SequenceEqual(new[]{"royal_command","fear_roar","combat_expertise"}),"Level1 actual character skills");
        Check(catalog.levels[2].characterSkills.SequenceEqual(new[]{"combat_expertise","builder","holy_light","holy_shield","meteor_shower","silence_oath"}),"Level2 includes summoned visitor and relic, without unrelated skills");
        foreach(var level in catalog.levels)Check(level.characterSkills.Distinct().Count()==level.characterSkills.Length,"multiple actors with same skill deduplicated");
        Directory.CreateDirectory("Assets/SettlementFixture");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),"Assets/SettlementFixture/Level2-Floor1.unity");
        SessionState.SetBool(nameof(LevelSettlementSmoke),true);EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if(!EditorApplication.isPlaying||frame==Time.frameCount)return;frame=Time.frameCount;
        try{
            if(SessionState.GetBool(nameof(LevelSettlementSmoke),false)){SessionState.SetBool(nameof(LevelSettlementSmoke),false);steps=Verify();deadline=EditorApplication.timeSinceStartup+120;}
            if(steps==null)return;if(EditorApplication.timeSinceStartup>deadline)throw new Exception("timeout");
            if(steps.MoveNext())return;steps=null;Debug.Log("LEVEL_SETTLEMENT_PASS checks="+checks);EditorApplication.Exit(0);
        }catch(Exception e){steps=null;Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static ZeldaFourWayMover Actor(string name,bool controlled=false)
    {
        var go=new GameObject(name);go.transform.position=Vector3.one*100;
        var data=go.AddComponent<SwordZeldaCharacterData>();var mover=go.AddComponent<ZeldaFourWayMover>();mover.enabled=controlled;
        var state=data.CaptureRuntimeState();state.health=state.currentHealth=50;data.ApplyRuntimeState(state);
        if(controlled)ZeldaRuntimeRegistry.ClaimControlledMover(mover);return mover;
    }
    static IEnumerator Verify()
    {
        Time.timeScale=1;while(RetroSceneLoadReveal.IsBlockingInput)yield return null;
        Check(LevelRunStatistics.Current.hasElapsedTime,"new level starts a recorded timer: scene="+SceneManager.GetActiveScene().name+" level="+LevelRunStatistics.Current.level+" restoring="+LevelRunStatistics.Restoring);
        string[] clockExpected={"00时 00分 00秒","00时 00分 59秒","00时 01分 00秒","00时 59分 59秒","01时 00分 00秒","23时 59分 59秒","00时 00分 00秒","00时 00分 01秒","01时 01分 01秒","00时 00分 00秒","00时 00分 01秒"};
        double[] clockSeconds={0,59.99,60,3599,3600,86399.99,86400,86401,90061,172800,172801};
        for(int i=0;i<clockSeconds.Length;i++)
            Check(LevelSettlementScreen.FormatClearTime(new LevelRunStatistics.State{hasElapsedTime=true,elapsedSeconds=clockSeconds[i]})==clockExpected[i],"clear time units/boundary "+clockSeconds[i]);
        Check(LevelSettlementScreen.FormatClearTime(JsonUtility.FromJson<LevelRunStatistics.State>("{\"level\":2}"))=="—","pre-timer save shows unknown instead of false zero");
        Check(LevelSettlementScreen.FormatClearTime(new LevelRunStatistics.State{hasElapsedTime=true,elapsedSeconds=double.NaN})=="—","invalid duration not displayed");
        LevelRunStatistics.Restore(new LevelRunStatistics.State{level=2,hasElapsedTime=true,elapsedSeconds=123.5});
        var timer=Object.FindObjectOfType<LevelRunStatistics>();
        var timerTick=typeof(LevelRunStatistics).GetMethod("LateUpdate",F);
        double delta=Time.unscaledDeltaTime;
        timerTick.Invoke(timer,null);
        Check(Math.Abs(LevelRunStatistics.Current.elapsedSeconds-(123.5+delta))<.00001,"active gameplay accumulates real seconds");
        double stoppedTime=LevelRunStatistics.Current.elapsedSeconds;
        Time.timeScale=0;timerTick.Invoke(timer,null);
        Check(LevelRunStatistics.Current.elapsedSeconds==stoppedTime,"paused gameplay does not count time");
        Time.timeScale=.5f;timerTick.Invoke(timer,null);Time.timeScale=1;
        Check(Math.Abs(LevelRunStatistics.Current.elapsedSeconds-(stoppedTime+delta))<.00001,"slow motion does not distort clear time");
        stoppedTime=LevelRunStatistics.Current.elapsedSeconds;
        typeof(RetroSceneLoadReveal).GetProperty("IsBlockingInput",F).SetValue(null,true);
        timerTick.Invoke(timer,null);
        typeof(RetroSceneLoadReveal).GetProperty("IsBlockingInput",F).SetValue(null,false);
        Check(LevelRunStatistics.Current.elapsedSeconds==stoppedTime,"black cover does not count time");
        var floor=SceneManager.CreateScene("Level2-Floor2");
        typeof(LevelRunStatistics).GetMethod("SceneLoaded",F).Invoke(timer,new object[]{floor,LoadSceneMode.Single});
        Check(LevelRunStatistics.Current.elapsedSeconds==stoppedTime,"same-level floor travel retains timer");
        Check(Object.FindObjectOfType<RuntimeUiVisibility>()!=null,"global cursor policy bootstrapped");
        CheckCursorPolicy(false,"normal gameplay still hides cursor without locking it");
        var player=Actor("first",true);var second=Actor("second");
        LevelRunStatistics.Restore(new LevelRunStatistics.State{level=2});
        LevelRunStatistics.RecordBody(player.CharacterData);LevelRunStatistics.RecordBody(player.CharacterData);
        Check(LevelRunStatistics.Current.switches==0,"initial possession not counted as switching");
        Check(LevelSettlementScreen.BuildReport(2,null).challenges.All(x=>x),"clean one-body challenges");
        LevelRunStatistics.RecordBody(second.CharacterData);LevelRunStatistics.RecordBody(second.CharacterData);
        Check(LevelRunStatistics.Current.switches==1,"body change once");
        Check(!LevelSettlementScreen.BuildReport(2,null).challenges[1],"second body fails one-body challenge");
        LevelRunStatistics.Restore(new LevelRunStatistics.State{level=2});LevelRunStatistics.RecordBody(player.CharacterData);
        var guard=Actor("guard").gameObject.AddComponent<CivilianIdleCharacterAi>();guard.enabled=false;
        var guard2=Actor("guard2").gameObject.AddComponent<CivilianIdleCharacterAi>();guard2.enabled=false;
        LevelRunStatistics.Awareness(guard,player,ZeldaAiState.Alert,true);
        LevelRunStatistics.Awareness(guard,player,ZeldaAiState.Alert,true);
        LevelRunStatistics.Awareness(guard2,player,ZeldaAiState.Alert,true);
        Check(LevelRunStatistics.Current.warnings==1,"group warning one episode, no repeated tick count");
        LevelRunStatistics.Awareness(guard,player,ZeldaAiState.Hostile,true);
        LevelRunStatistics.Awareness(guard2,player,ZeldaAiState.Hostile,true);
        Check(LevelRunStatistics.Current.hostilities==1,"propagated hostility one episode");
        var saved=LevelRunStatistics.Capture();LevelRunStatistics.Restore(JsonUtility.FromJson<LevelRunStatistics.State>(JsonUtility.ToJson(saved)));
        LevelRunStatistics.Awareness(guard,player,ZeldaAiState.Hostile,true);
        Check(LevelRunStatistics.Current.hostilities==1,"restored hostile encounter not counted twice");
        LevelRunStatistics.Awareness(guard,player,ZeldaAiState.Idle,true);LevelRunStatistics.Awareness(guard2,player,ZeldaAiState.Idle,true);
        LevelRunStatistics.Awareness(guard,player,ZeldaAiState.Hostile,true);
        Check(LevelRunStatistics.Current.hostilities==2,"new hostile episode counted");
        LevelRunStatistics.Awareness(guard2,second,ZeldaAiState.Alert,true);
        Check(LevelRunStatistics.Current.warnings==1,"NPC target not player warning");
        LevelRunStatistics.Awareness(guard,player,ZeldaAiState.Idle,true);
        LevelRunStatistics.Awareness(guard,player,ZeldaAiState.Alert,false);
        Check(LevelRunStatistics.Current.warnings==1,"restoring state does not count warning");
        Check(!LevelSettlementScreen.BuildReport(2,null).challenges[0],"awareness invalidates stealth challenge");
        var dataPickup=new GameObject("DATA").AddComponent<GrowthCollectible>();
        LevelRunStatistics.RecordSkill(dataPickup,2);LevelRunStatistics.RecordSkill(dataPickup,2);
        Check(LevelRunStatistics.Current.skillPoints==2,"DATA unique collection and amount");
        var memo=Object.Instantiate(Resources.Load<LevelSettlementCatalog>("LevelSettlementCatalog").expansionVisual.gameObject).GetComponent<MemoPickupItem>();
        LevelRunStatistics.RecordExpansion(memo);LevelRunStatistics.RecordExpansion(memo);
        Check(LevelRunStatistics.Current.expansions==1,"MEMO repickup not counted twice");
        LevelRunStatistics.RecordItemUse();Check(LevelRunStatistics.Current.itemUses==1,"item use event");
        LevelRunStatistics.RecordDeath(second.CharacterData,false,false);Check(LevelRunStatistics.Current.deaths==0,"unrelated death ignored");
        LevelRunStatistics.RecordDeath(second.CharacterData,true,true);Check(LevelRunStatistics.Current.deaths==0,"abandoned body ignored");
        LevelRunStatistics.RecordDeath(second.CharacterData,true,false);Check(LevelRunStatistics.Current.deaths==1,"player caused death");
        LevelRunStatistics.Current.hasElapsedTime=true;LevelRunStatistics.Current.elapsedSeconds=3661.75;
        var disk=SceneTravelStateManager.GetOrCreate().CaptureDiskPlayer();
        var roundtrip=JsonUtility.FromJson<SceneTravelStateManager.DiskPlayer>(JsonUtility.ToJson(disk));
        Check(roundtrip.levelRun.deaths==1&&roundtrip.levelRun.hostilities==2&&roundtrip.levelRun.skillPoints==2,"save includes all run statistics");
        Check(roundtrip.levelRun.hasElapsedTime&&roundtrip.levelRun.elapsedSeconds==3661.75,"save includes precise elapsed seconds");
        LevelRunStatistics.ResetSession();Check(LevelRunStatistics.Current.level==-1&&LevelRunStatistics.Current.itemUses==0,"new run reset");
        Check(LevelRunStatistics.Current.elapsedSeconds==0&&!LevelRunStatistics.Current.hasElapsedTime,"session reset clears timer");
        LevelRunStatistics.Restore(roundtrip.levelRun);Check(LevelRunStatistics.Current.warnings==1,"restore counters");
        Check(LevelRunStatistics.Current.elapsedSeconds==3661.75&&LevelRunStatistics.Current.hasElapsedTime,"restore resumes saved duration");
        LevelRunStatistics.Restore(null);Check(!LevelRunStatistics.Current.completeHistory,"legacy saves mark unknown history");
        Check(LevelSettlementScreen.BuildReport(2,null).challenges.All(x=>!x),"legacy saves cannot award unproven challenges");
        LevelRunStatistics.Restore(roundtrip.levelRun);
        Check(LevelSettlementCatalog.Quests(0).Count==2&&LevelSettlementCatalog.Quests(1).Count==4&&LevelSettlementCatalog.Quests(2).Count==11,"all actual level quests included");
        Check(LevelSettlementCatalog.Quests(2).All(q=>!q.title.Contains("已知密码")),"password page excluded");
        Check(!LevelSettlementScreen.TryOpen("Level2-Floor2"),"intra-level transition not settlement");
        Check(!LevelSettlementScreen.TryOpen(""),"invalid destination not completion");
        var catalog=Resources.Load<LevelSettlementCatalog>("LevelSettlementCatalog");
        var growth=PlayerGrowthAttributes.GetOrCreate();growth.ApplyRuntimeState(default);
        Check(LevelSettlementScreen.BuildReport(2,null).characterSkills.All(s=>!s.unlocked),"undiscovered skills stay dark");
        var bearer=Actor("Builder skill bearer");bearer.gameObject.AddComponent<BuilderCharacterSkillData>();
        growth.RecordPossessedCharacter(bearer.CharacterData);
        var skillReport=LevelSettlementScreen.BuildReport(2,null);
        Check(skillReport.characterSkills.Single(s=>s.id=="builder").unlocked&&!growth.HasSkill("builder"),"possession opens learning without spending DATA");
        Check(!skillReport.characterSkills.Single(s=>s.id=="holy_light").unlocked,"other skills stay locked");
        var relicBearer=Actor("Relic bearer");
        Check(relicBearer.CharacterData.TryGrantRelicCharacterSkill(SilenceOathRuntime.SkillId),"grant actual relic ability");
        growth.RecordPossessedCharacter(relicBearer.CharacterData);
        Check(LevelSettlementScreen.BuildReport(2,null).characterSkills.Single(s=>s.id=="silence_oath").unlocked,"relic discovery lights skill");
        var savedGrowth=JsonUtility.FromJson<PlayerGrowthAttributes.RuntimeState>(JsonUtility.ToJson(growth.CaptureRuntimeState()));
        growth.ApplyRuntimeState(default);growth.ApplyRuntimeState(savedGrowth);
        Check(LevelSettlementScreen.BuildReport(2,null).characterSkills.Count(s=>s.unlocked)==2,"discoveries survive save roundtrip without original actors");
        growth.ApplyRuntimeState(new PlayerGrowthAttributes.RuntimeState{unlockedSkills=new[]{"holy_shield_3"}});
        Check(LevelSettlementScreen.BuildReport(2,null).characterSkills.Single(s=>s.id=="holy_shield").unlocked,"older learned upgraded skill implies discovery");
        Check(!skillReport.characterSkills.Single(s=>s.id=="holy_shield").unlocked,"report is a frozen snapshot");
        LevelRunStatistics.Restore(null);
        Check(LevelSettlementScreen.BuildReport(2,null).characterSkills.Single(s=>s.id=="holy_shield").unlocked,"old run counters do not suppress known skill progress");
        foreach(var level in catalog.levels)foreach(var id in level.characterSkills)Check(SkillPageArt.GetAbilitySprite(id)!=null,"existing skill icon "+id);
        VerifySkillIcons();
        foreach(var level in new[]{0,1,2})
        {
            var scene=SceneManager.CreateScene(level==0?"Level0":level==1?"Level1-Floor1":"Level2-Desert");SceneManager.SetActiveScene(scene);
            LevelRunStatistics.Restore(new LevelRunStatistics.State{level=level,hasElapsedTime=true,elapsedSeconds=level==0?263:level==1?1432:5025,firstBody=player.CharacterData.IndividualAccessId,lastBody=player.CharacterData.IndividualAccessId,skillPoints=level+1,expansions=level==2?1:0,warnings=1,hostilities=2,itemUses=3});
            var discoveries=catalog.levels[level].characterSkills.Where((s,i)=>i%2==0).ToArray();
            growth.ApplyRuntimeState(new PlayerGrowthAttributes.RuntimeState{discoveredCharacterSkills=discoveries});
            Check(LevelSettlementScreen.TryOpen("LastScene"),"opens level "+level);
            var screen=Object.FindObjectOfType<LevelSettlementScreen>();
            Check(screen.DisplayedReport.characterSkills.Count==catalog.levels[level].characterSkills.Length,"all available skills shown in each level");
            Check(screen.DisplayedReport.characterSkills.Count(s=>s.unlocked)==discoveries.Length,"snapshot reads discovery not learned rank");
            growth.ApplyRuntimeState(default);
            Check(Time.timeScale==0&&DocumentReader.IsInputBlocked,"modal freezes game");
            CheckCursorPolicy(true,"settlement cursor remains visible during entry cover");
            Check(RetroSceneLoadReveal.IsBlockingInput&&screen.GetComponentsInChildren<Text>().Length==0,"cover starts before any settlement content appears");
            Check(LevelSettlementScreen.TryOpen("LastScene")&&Object.FindObjectsOfType<LevelSettlementScreen>().Length==1,"repeated entry cannot duplicate screen or cover");
            screen.NextLevel();screen.ReturnToTitle();
            Check(SceneManager.GetActiveScene()==scene&&Time.timeScale==0,"navigation blocked during entry cover");
            while(screen.GetComponentsInChildren<Text>().Length==0)yield return null;
            var reveal=Object.FindObjectOfType<RetroSceneLoadReveal>();
            var bands=(RectTransform[])typeof(RetroSceneLoadReveal).GetField("blackBands",F).GetValue(reveal);
            Check(RetroSceneLoadReveal.IsBlockingInput&&bands.All(b=>b.gameObject.activeSelf),"screen constructed only beneath fully black cover");
            var overlay=(Canvas)typeof(RetroSceneLoadReveal).GetField("overlayCanvas",F).GetValue(reveal);
            Check(overlay.sortingOrder>screen.GetComponent<Canvas>().sortingOrder,"black cover draws above settlement");
            while(RetroSceneLoadReveal.IsBlockingInput)yield return null;
            Check(SceneManager.GetActiveScene()==scene&&Time.timeScale==0&&DocumentReader.IsInputBlocked,"reveal finishes at zero time scale without reloading gameplay");
            for(int cursorFrame=0;cursorFrame<12;cursorFrame++)
            {
                typeof(LevelSettlementScreen).GetMethod("Update",F).Invoke(screen,null);
                Check(Cursor.visible,"settlement Update never hides cursor");
                CheckCursorPolicy(true,"global LateUpdate agrees with settlement every frame");
                yield return null;
            }
            Check(screen.DisplayedReport.quests.Count==LevelSettlementCatalog.Quests(level).Count,"all quests including undiscovered");
            Check(screen.DisplayedReport.quests[0].complete,"clearing level completes main quest even when entering via editor");
            var texts=screen.GetComponentsInChildren<Text>().Select(t=>t.text).ToArray();
            Check(texts.Contains("返回主界面")&&texts.Contains("下一关"),"requested navigation labels");
            Check(texts.Contains("D.A.T.A")&&texts.Contains("M.E.M.O")&&!texts.Contains("技能点")&&!texts.Contains("背包扩充"),"requested collectible names");
            Check(texts.Contains("警告 / 敌对")&&texts.Contains("1 / 2 次"),"awareness action record values");
            var timeLabel=screen.GetComponentsInChildren<Text>().Single(t=>t.name=="Clear Time");
            Check(texts.Contains("通关用时")&&timeLabel.text==LevelSettlementScreen.FormatClearTime(screen.DisplayedReport.statistics),"top-right frozen clear time");
            Check(timeLabel.rectTransform.anchoredPosition.x>=866&&-timeLabel.rectTransform.anchoredPosition.y+timeLabel.rectTransform.sizeDelta.y<141&&timeLabel.alignment==TextAnchor.MiddleRight,"timer fits above divider and aligns right");
            Check(LevelRunStatistics.Current.elapsedSeconds==screen.DisplayedReport.statistics.elapsedSeconds,"cover/reveal and settlement do not advance timer");
            var skillHeading=screen.GetComponentsInChildren<Text>().Single(t=>t.text=="角色技能");
            foreach(var skill in screen.DisplayedReport.characterSkills)
            {
                var tile=screen.GetComponentsInChildren<RectTransform>().Single(r=>r.name=="Character Skill "+skill.id);
                var art=tile.Find("Skill Icon").GetComponent<Image>();
                Check(art.sprite==LevelSettlementSkillIcons.Get(skill.id),"uses unranked collection icon "+skill.id);
                Check(art.preserveAspect&&art.rectTransform.anchoredPosition==Vector2.zero&&
                    art.rectTransform.pivot==Vector2.one*.5f&&art.rectTransform.anchorMin==Vector2.one*.5f&&art.rectTransform.anchorMax==Vector2.one*.5f&&
                    art.rectTransform.sizeDelta==tile.sizeDelta-Vector2.one*14,"centered icon frame "+skill.id);
                using(var vertices=new VertexHelper())
                {
                    typeof(Image).GetMethod("OnPopulateMesh",F,null,new[]{typeof(VertexHelper)},null).Invoke(art,new object[]{vertices});
                    var first=UIVertex.simpleVert;vertices.PopulateUIVertex(ref first,0);var bounds=new Bounds(first.position,Vector3.zero);
                    for(int v=1;v<vertices.currentVertCount;v++){vertices.PopulateUIVertex(ref first,v);bounds.Encapsulate(first.position);}
                    Check(bounds.center.sqrMagnitude<.0001f,"aspect-fitted glyph mesh centered "+skill.id);
                }
                Check(skill.unlocked?art.color.g>.8f:art.color.g<.4f,"lit/dim style "+skill.id);
                Check(-tile.anchoredPosition.y+tile.sizeDelta.y<383,"skill row does not overlap action records");
                ExecuteEvents.Execute(tile.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerEnterHandler);
                Check(skillHeading.text.Contains(skill.title),"hover gives skill name without opening another window");
                ExecuteEvents.Execute(tile.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerExitHandler);
            }
            Check(screen.GetComponentInChildren<SkillPageArt>().collectibleIcon,"actual DATA appearance");
            Check(screen.GetComponentsInChildren<Image>().Any(i=>i.sprite==MemoPickupItemVisual.SphereIcon),"actual MEMO sphere body");
            var collectibleArts=screen.GetComponentsInChildren<SkillPageArt>().Where(a=>a.collectibleIcon).ToArray();
            Check(collectibleArts.Length==2,"DATA and MEMO use shared animated collectible presentation");
            var dataGlow=collectibleArts[0].transform.Find("Breathing Glow").GetComponent<Image>();
            var memoGlow=collectibleArts[1].transform.Find("Breathing Glow").GetComponent<Image>();
            Check(dataGlow.sprite==memoGlow.sprite&&dataGlow.color==memoGlow.color&&memoGlow.color.r==1&&memoGlow.color.g==1&&memoGlow.color.b==1,"same white halo sprite and opacity");
            Check(dataGlow.rectTransform.sizeDelta==memoGlow.rectTransform.sizeDelta,"matching halo size");
            int orbitCount=collectibleArts[0].transform.Find("Rotating Orbit Points").childCount;
            Check(orbitCount==GrowthCollectible.GetIconAppearance().pointCount&&collectibleArts[1].transform.Find("Rotating Orbit Points").childCount==orbitCount,"UI orbits share DATA point count");
            float until=Time.realtimeSinceStartup+.4f,lo=float.MaxValue,hi=float.MinValue;
            while(Time.realtimeSinceStartup<until)
            {
                yield return null;float scale=memoGlow.transform.localScale.x;lo=Mathf.Min(lo,scale);hi=Mathf.Max(hi,scale);
            }
            Check(Time.timeScale==0&&hi-lo>.0001f,"MEMO glow keeps breathing while settlement pauses gameplay");
            Check(Vector3.Distance(dataGlow.transform.localScale,memoGlow.transform.localScale)<.0001f,"DATA and MEMO breathing stays synchronized");
            int uses=LevelRunStatistics.Current.itemUses;LevelRunStatistics.RecordItemUse();Check(LevelRunStatistics.Current.itemUses==uses,"frozen report does not collect extra events");
            yield return null;yield return null;
            foreach(var icon in screen.GetComponentsInChildren<LevelSettlementIcon>())Check(icon.GetComponent<CanvasRenderer>()!=null,"pixel icon has renderer");
            Render(screen,1280,720,"level"+level);if(level==2)Render(screen,1024,768,"level2-4x3");
            Object.Destroy(screen.gameObject);yield return null;Check(!LevelSettlementScreen.IsOpen&&Time.timeScale==1,"closing restores time");
            CheckCursorPolicy(false,"closing settlement restores normal gameplay cursor policy");
        }
        SceneManager.SetActiveScene(SceneManager.GetSceneByName("Level0"));
        LevelRunStatistics.Restore(new LevelRunStatistics.State{level=0,firstBody="fixture-body",lastBody="fixture-body"});
        Check(LevelSettlementScreen.TryOpen("Level1-Garden"),"open before actual next-level travel");
        while(RetroSceneLoadReveal.IsBlockingInput)yield return null;
        Object.FindObjectOfType<LevelSettlementScreen>().NextLevel();
        CheckCursorPolicy(true,"cursor stable during outgoing settlement cover");
        Check(Time.timeScale==0,"game stays frozen during outgoing cover");
        while(SceneManager.GetActiveScene().name!="Level1-Garden"||RetroSceneLoadReveal.IsBlockingInput)yield return null;
        Check(!LevelSettlementScreen.IsOpen&&Time.timeScale==1,"next level loaded and resumed");
        CheckCursorPolicy(false,"next level resumes hidden gameplay cursor");
        Check(LevelRunStatistics.Current.level==1&&LevelRunStatistics.Current.warnings==0,"new level resets run counters");
        Check(LevelRunStatistics.Current.hasElapsedTime&&LevelRunStatistics.Current.elapsedSeconds<1,"next level starts its own timer after loading");
        Check(LevelSettlementScreen.TryOpen("LastScene"),"next level can settle normally");
        while(RetroSceneLoadReveal.IsBlockingInput)yield return null;
        Object.FindObjectOfType<LevelSettlementScreen>().ReturnToTitle();
        while(SceneManager.GetActiveScene().name!="TitleScreen"||RetroSceneLoadReveal.IsBlockingInput)yield return null;
        Check(!LevelSettlementScreen.IsOpen&&Time.timeScale==1&&LevelRunStatistics.Current.level==-1,"return to main menu works and clears session");
        CheckCursorPolicy(true,"main menu keeps visible unlocked cursor after settlement");
    }
    static void VerifySkillIcons()
    {
        string[] ids={"combat_expertise","royal_command","fear_roar","builder","holy_light","holy_shield","meteor_shower","silence_oath"};
        Sprite[] native={ZeldaHealthHeartsUI.GetCombatExpertiseIcon(),ZeldaCharacterData.CommandIcon,FearRoarArea.Icon,
            BuilderCharacterSkillData.GetIcon(0),HolyLightCharacterSkillData.GetIcon(0),HolyShieldCharacterSkillData.GetIcon(0),
            MeteorShowerCharacterSkillData.GetIcon(0),SilenceOathRuntime.GetIcon(0)};
        for(int i=0;i<ids.Length;i++)
        {
            var ranked=SkillPageArt.GetAbilitySprite(ids[i]);var originalRect=ranked.rect;
            var icon=LevelSettlementSkillIcons.Get(ids[i]);
            Check(icon.texture==native[i].texture,"collection uses native glyph without numerals "+ids[i]);
            Check(icon==LevelSettlementSkillIcons.Get(ids[i]),"collection icon cached "+ids[i]);
            Check(ranked==SkillPageArt.GetAbilitySprite(ids[i])&&ranked.rect==originalRect,"skill-tree sprite not mutated "+ids[i]);
            var pixels=ReadIconPixels(native[i]);
            int width=native[i].texture.width,height=native[i].texture.height,minX=width,minY=height,maxX=-1,maxY=-1;
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)if(pixels[y*width+x].a>127)
            {minX=Math.Min(minX,x);minY=Math.Min(minY,y);maxX=Math.Max(maxX,x);maxY=Math.Max(maxY,y);}
            Check(icon.rect==new Rect(minX,minY,maxX-minX+1,maxY-minY+1),"complete glyph has symmetric zero padding "+ids[i]+" actual "+new Rect(minX,minY,maxX-minX+1,maxY-minY+1));
            Check(icon.pivot==icon.rect.size*.5f,"glyph pivot centered "+ids[i]);
        }
        for(int rank=0;rank<=2;rank++)
        {
            var pixels=ReadIconPixels(BuilderCharacterSkillData.GetIcon(rank));
            Check(pixels[11*22+17].a>127&&pixels[13*22+17].a>127&&pixels[12*22+20].a==0,"builder barrel and muzzle face right, rank "+rank);
            Check(pixels[5*22+3].a>127&&pixels[5*22+14].a>127,"builder keeps upright base, rank "+rank);
            for(int x=4;x<=12;x++)Check(pixels[8*22+x].a>127&&pixels[9*22+x].a>127,"builder housing rests directly on wide base, rank "+rank+" column "+x);
            Check(LevelSettlementSkillIcons.Get("builder").rect.height==11,"builder silhouette is low-profile");
            Check((pixels[1*22+19].a>127)==(rank>0),"builder rank retained only for ranked UI "+rank);
        }
    }
    static Color32[] ReadIconPixels(Sprite icon)
    {
        var source=icon.texture;var previous=RenderTexture.active;
        var rt=RenderTexture.GetTemporary(source.width,source.height,0,RenderTextureFormat.ARGB32);
        var copy=new Texture2D(source.width,source.height,TextureFormat.RGBA32,false);
        try
        {
            Graphics.Blit(source,rt);RenderTexture.active=rt;
            copy.ReadPixels(new Rect(0,0,source.width,source.height),0,0);copy.Apply();return copy.GetPixels32();
        }
        finally {RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);Object.Destroy(copy);}
    }
    static void Render(LevelSettlementScreen screen,int width,int height,string name)
    {
        var camera=new GameObject("Settlement Preview Camera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=360;
        camera.transform.position=new Vector3(0,0,-10);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
        var canvas=screen.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
        var rt=new RenderTexture(width,height,24);camera.targetTexture=rt;Canvas.ForceUpdateCanvases();camera.Render();
        var previous=RenderTexture.active;RenderTexture.active=rt;var texture=new Texture2D(width,height,TextureFormat.RGB24,false);
        texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();Directory.CreateDirectory("../../settlement-previews");
        File.WriteAllBytes("../../settlement-previews/"+name+".png",texture.EncodeToPNG());
        RenderTexture.active=previous;camera.targetTexture=null;rt.Release();Object.Destroy(rt);Object.Destroy(texture);Object.Destroy(camera.gameObject);
    }
}
