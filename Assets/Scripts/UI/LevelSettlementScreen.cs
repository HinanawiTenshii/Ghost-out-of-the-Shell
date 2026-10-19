using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Frozen end-of-level view. No scene reload/counting, quest polling, or gameplay runs behind it.</summary>
public sealed class LevelSettlementScreen : MonoBehaviour
{
    public struct QuestResult { public string title; public int depth; public bool complete; }
    public struct CharacterSkillResult { public string id,title; public bool unlocked; }
    public sealed class Report
    {
        public int level;
        public LevelRunStatistics.State statistics;
        public List<QuestResult> quests = new List<QuestResult>();
        public List<CharacterSkillResult> characterSkills = new List<CharacterSkillResult>();
        public bool[] challenges;
    }
    private static LevelSettlementScreen instance;
    public static bool IsOpen => instance != null;
    private readonly Color muted=new Color32(125,159,191,255), background=new Color32(4,10,18,255);
    private Canvas canvas;
    private Font font;
    private float previousTime;
    private bool entering = true, departing;
    private Report report;
    private LevelSettlementCatalog catalog;
    public Report DisplayedReport => report;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic() { instance=null; }
    public static bool TryOpen(string destination)
    {
        if(string.IsNullOrWhiteSpace(destination))return false;
        int level=LevelSettlementCatalog.LevelIndex(SceneManager.GetActiveScene().name);
        if(level<0 || LevelSettlementCatalog.LevelIndex(destination)==level)return false;
        if(IsOpen)return true;
        // Consume repeated interaction during another cover without starting a scene load.
        if(RetroSceneLoadReveal.IsBlockingInput)return true;
        var asset=Resources.Load<LevelSettlementCatalog>("LevelSettlementCatalog");
        if(asset==null || asset.levels==null || asset.levels.Length<=level)
        { Debug.LogError("Missing LevelSettlementCatalog. Use Tools/Level Settlement/Rebuild Authored Totals.");return false; }
        var journal=QuestJournalManager.GetOrCreate();
        if(level==2)journal.CompleteLevel2ForSettlement();
        else
        {
            string mainId=level==0?"level0.escape_prison":"level1.escape_castle";
            if(!journal.ContainsEntry(mainId))journal.AddOrUpdateEntry(mainId,
                level==0?QuestJournalManager.Level0EscapeTitle:QuestJournalManager.Level1EscapeTitle,
                level==0?QuestJournalManager.Level0EscapeDescription:QuestJournalManager.Level1EscapeDescription,
                SceneManager.GetActiveScene().name);
            journal.RecordQuestCompletion(mainId);
        }
        var go=new GameObject("Level Settlement",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        instance=go.AddComponent<LevelSettlementScreen>();
        instance.catalog=asset;instance.font=asset.font;
        instance.report=BuildReport(level,journal);
        GameSaveSystem.Instance?.RecordLevelCompletion(instance.report);
        instance.previousTime=Time.timeScale;Time.timeScale=0f;
        Cursor.visible=true;Cursor.lockState=CursorLockMode.None;
        // Freeze and snapshot immediately, but do not reveal the UI until the
        // existing scene-transition bands have covered the entire gameplay view.
        var screen=instance;
        if(!RetroSceneLoadReveal.BeginLocalTransition(()=>{
            if(screen==null)return;
            try { screen.Build();screen.entering=false; }
            catch { Destroy(screen.gameObject);throw; }
        })) Destroy(go);
        return true;
    }
    public static Report BuildReport(int level,QuestJournalManager journal)
    {
        var result=new Report{level=level,statistics=LevelRunStatistics.Capture()};
        foreach(var quest in LevelSettlementCatalog.Quests(level))result.quests.Add(new QuestResult{
            title=quest.title,depth=quest.depth,complete=journal!=null&&journal.IsEntryCompleted(quest.id)});
        var source=Resources.Load<LevelSettlementCatalog>("LevelSettlementCatalog");
        var growth=PlayerGrowthAttributes.Instance;
        var seen=new HashSet<string>();
        if(source!=null && level>=0 && level<source.levels.Length)
            foreach(var id in source.levels[level].characterSkills ?? Array.Empty<string>())
            {
                if(!seen.Add(id))continue;
                var definition=Array.Find(PlayerGrowthAttributes.Skills,s=>s.id==id&&s.requiredPossessedSkillId==id);
                if(definition==null)continue;
                // Collection means opening this character skill for learning, not
                // purchasing a rank. Use the persistent discovery record even when
                // the original body/relic/summoned actor no longer exists.
                bool unlocked=growth!=null && (growth.IsSkillDiscovered(definition) ||
                    Array.Exists(PlayerGrowthAttributes.Skills,s=>s.requiredPossessedSkillId==id&&growth.HasSkill(s.id)));
                result.characterSkills.Add(new CharacterSkillResult{id=id,title=definition.title,unlocked=unlocked});
            }
        var s=result.statistics;
        bool quiet=s.completeHistory&&s.warnings==0&&s.hostilities==0;
        bool single=s.completeHistory&&!string.IsNullOrEmpty(s.firstBody)&&!s.singleBodyFailed&&s.switches==0;
        result.challenges=new[]{quiet,single,quiet&&single};return result;
    }
    private void Build()
    {
        canvas=GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;
        int topValue=int.MinValue;
        foreach(var layer in SortingLayer.layers)if(layer.value>topValue){topValue=layer.value;canvas.sortingLayerID=layer.id;}
        var scaler=GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution=new Vector2(1280,720);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
        if(EventSystem.current==null)new GameObject("EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));
        var fill=Node("Background",transform,typeof(Image));Stretch(fill);fill.GetComponent<Image>().color=background;
        var surface=Node("Layout",fill);surface.anchorMin=surface.anchorMax=Vector2.one*.5f;surface.sizeDelta=new Vector2(1280,720);
        Border(surface,new Rect(38,30,1204,654),ZeldaUiPalette.Ghost,2);
        Label(surface,"关卡完成",new Rect(68,50,750,52),36,ZeldaUiPalette.Primary);
        Label(surface,"LEVEL 0"+report.level+"  /  "+catalog.levels[report.level].title,new Rect(70,103,750,26),17,muted);
        bool partialTime = report.statistics.elapsedTimeIncomplete;
        Label(surface,partialTime?"通关用时（部分记录）":"通关用时",new Rect(866,52,346,26),17,muted,TextAnchor.MiddleRight);
        var duration=Label(surface,FormatClearTime(report.statistics),new Rect(866,83,346,40),28,ZeldaUiPalette.Primary,TextAnchor.MiddleRight);
        duration.name="Clear Time";
        duration.horizontalOverflow=HorizontalWrapMode.Overflow;
        if(partialTime)
            Label(surface,"仅统计恢复计时后的已记录时段",new Rect(866,123,346,16),12,muted,TextAnchor.MiddleRight);
        Line(surface,new Rect(68,141,1144,2),ZeldaUiPalette.Ghost);
        BuildQuests(surface);BuildCollections(surface);BuildStatistics(surface);BuildChallenges(surface);
        Button(surface,"返回主界面",new Rect(844,624,168,42),ReturnToTitle,false);
        Button(surface,"下一关",new Rect(1030,624,182,42),NextLevel,true);
        if(GameSaveSystem.Instance != null && GameSaveSystem.Instance.CanRestartCurrentLevel)
            Button(surface,"重新开始本关",new Rect(640,624,186,42),RestartLevel,false);
        if(!report.statistics.completeHistory)Label(surface,"旧存档未记录的行动数据以 — 显示",new Rect(68,630,550,28),15,muted);
        EnsurePriority();
    }
    public static string FormatClearTime(LevelRunStatistics.State statistics)
    {
        if(statistics==null || !statistics.hasElapsedTime || double.IsNaN(statistics.elapsedSeconds) ||
            double.IsInfinity(statistics.elapsedSeconds))return "—";
        // Display cycles every 24 hours; retain the full saved duration. Keep
        // the last whole second without rounding into the next minute/day.
        double seconds=Math.Floor(Math.Max(0,statistics.elapsedSeconds))%86400;
        return string.Format("{0:00}时 {1:00}分 {2:00}秒",Math.Floor(seconds/3600),Math.Floor(seconds/60)%60,seconds%60);
    }
    private void BuildQuests(RectTransform parent)
    {
        int complete=0;foreach(var q in report.quests)if(q.complete)complete++;
        Label(parent,"任务完成情况",new Rect(68,163,350,28),22,ZeldaUiPalette.Primary);
        Label(parent,complete+" / "+report.quests.Count,new Rect(500,163,80,28),17,muted,TextAnchor.MiddleRight);
        float y=204;
        foreach(var quest in report.quests)
        {
            if(quest.complete)Icon(parent,0,new Rect(75+quest.depth*24,y+2,22,22),ZeldaUiPalette.Primary);
            Label(parent,quest.title,new Rect(106+quest.depth*24,y,485-quest.depth*24,26),18,
                quest.complete?ZeldaUiPalette.Primary:muted);
            y+=26;
        }
    }
    private void BuildCollections(RectTransform parent)
    {
        Label(parent,"收集进度",new Rect(648,163,260,28),22,ZeldaUiPalette.Primary);
        Label(parent,"已收集 / 本关总量",new Rect(922,163,290,28),16,muted,TextAnchor.MiddleRight);
        var level=catalog.levels[report.level];var stats=report.statistics;
        Collection(parent,"D.A.T.A",stats.skillPoints,level.skillPoints,648);
        Collection(parent,"M.E.M.O",stats.expansions,level.inventoryExpansions,942);
        var tetra=Node("D.A.T.A. item appearance",parent,typeof(SkillPageArt));Place(tetra,new Rect(660,217,38,38));
        tetra.GetComponent<SkillPageArt>().collectibleIcon=true;
        var memo=Node("M.E.M.O. item appearance",parent,typeof(SkillPageArt));Place(memo,new Rect(954,217,38,38));
        var memoArt=memo.GetComponent<SkillPageArt>();memoArt.collectibleIcon=true;
        memoArt.collectibleBodySprite=MemoPickupItemVisual.SphereIcon;
        BuildCharacterSkills(parent);
    }
    private void Collection(RectTransform parent,string title,int collected,int total,float x)
    {
        Line(parent,new Rect(x,202,270,74),new Color32(8,24,43,255));
        Label(parent,title,new Rect(x+60,207,195,25),19,ZeldaUiPalette.Primary);
        string count=report.statistics.completeHistory?collected.ToString():"—";
        Label(parent,count+" / "+total,new Rect(x+60,233,195,26),21,ZeldaUiPalette.Primary,TextAnchor.MiddleRight);
        Line(parent,new Rect(x+60,264,195,4),new Color32(19,43,66,255));
        float portion=total>0&&report.statistics.completeHistory?Mathf.Clamp01((float)collected/total):0f;
        if(portion>0)Line(parent,new Rect(x+60,264,195*portion,4),ZeldaUiPalette.Primary);
    }
    private void BuildCharacterSkills(RectTransform parent)
    {
        var heading=Label(parent,"角色技能",new Rect(648,288,445,24),18,ZeldaUiPalette.Primary);
        int count=report.characterSkills.Count,unlocked=0;
        foreach(var skill in report.characterSkills)if(skill.unlocked)unlocked++;
        Label(parent,unlocked+" / "+count,new Rect(1100,288,112,24),16,muted,TextAnchor.MiddleRight);
        if(count==0)
        {
            Label(parent,"本关无可解锁角色技能",new Rect(648,324,564,32),16,muted,TextAnchor.MiddleCenter);
            return;
        }
        float gap=12f,size=Mathf.Min(48f,(564f-gap*(count-1))/count);
        float start=648+(564-(size*count+gap*(count-1)))*.5f;
        for(int i=0;i<count;i++)
        {
            var skill=report.characterSkills[i];
            var tile=Node("Character Skill "+skill.id,parent,typeof(Image),typeof(EventTrigger));
            Place(tile,new Rect(start+i*(size+gap),320,size,size));
            tile.GetComponent<Image>().color=skill.unlocked?new Color32(12,37,56,255):new Color32(6,16,28,255);
            Border(tile,new Rect(0,0,size,size),skill.unlocked?ZeldaUiPalette.Ghost:new Color32(30,52,71,255),1);
            var rect=Node("Skill Icon",tile,typeof(Image));
            // A centered pivot also centers non-square glyphs when preserveAspect fits them.
            rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.one*.5f;
            rect.anchoredPosition=Vector2.zero;rect.sizeDelta=Vector2.one*(size-14);
            var art=rect.GetComponent<Image>();art.sprite=LevelSettlementSkillIcons.Get(skill.id);
            art.preserveAspect=true;art.raycastTarget=false;
            art.color=skill.unlocked?ZeldaUiPalette.Primary:new Color32(49,78,96,255);
            if(skill.unlocked)TitleScreenGlow.Attach(art);
            var events=tile.GetComponent<EventTrigger>();
            var enter=new EventTrigger.Entry{eventID=EventTriggerType.PointerEnter};
            enter.callback.AddListener(_=>heading.text="角色技能 · "+skill.title+(skill.unlocked?"（已解锁）":"（未解锁）"));
            var exit=new EventTrigger.Entry{eventID=EventTriggerType.PointerExit};
            exit.callback.AddListener(_=>heading.text="角色技能");
            events.triggers.Add(enter);events.triggers.Add(exit);
        }
    }
    private void BuildStatistics(RectTransform parent)
    {
        Line(parent,new Rect(648,383,564,1),new Color32(24,55,78,255));
        Label(parent,"行动记录",new Rect(648,394,300,27),22,ZeldaUiPalette.Primary);
        var s=report.statistics;
        Statistic(parent,"角色切换",s.switches+" 次",648,434);
        Statistic(parent,"造成角色死亡",s.deaths+" 名",942,434);
        Statistic(parent,"道具使用",s.itemUses+" 次",648,471);
        Statistic(parent,"警告 / 敌对",s.warnings+" / "+s.hostilities+" 次",942,471);
    }
    private void Statistic(RectTransform parent,string title,string value,float x,float y)
    {
        Line(parent,new Rect(x,y,2,24),new Color32(35,70,105,255));
        Label(parent,title,new Rect(x+12,y,130,24),15,muted);
        Label(parent,report.statistics.completeHistory?value:"—",new Rect(x+142,y-2,128,28),22,ZeldaUiPalette.Primary,TextAnchor.MiddleRight);
    }
    private void BuildChallenges(RectTransform parent)
    {
        Line(parent,new Rect(68,507,1144,2),ZeldaUiPalette.Ghost);
        Label(parent,"通关挑战",new Rect(68,517,400,28),21,ZeldaUiPalette.Primary);
        string[] titles={"无声无息","始终如一","无声独行"};
        string[] rules={"全程不被警告或敌对","仅使用首个附身角色通关","仅使用首个附身角色通关\n且全程不被警告或敌对"};
        for(int i=0;i<3;i++)
        {
            float x=68+i*388;bool achieved=report.challenges[i];
            var tint=achieved?ZeldaUiPalette.Primary:muted;
            Color panel=achieved?new Color32(16,44,67,255):new Color32(8,24,43,255);
            Line(parent,new Rect(x,550,368,62),panel);
            Icon(parent,i+1,new Rect(x+10,559,42,42),tint,panel);
            Label(parent,titles[i],new Rect(x+64,553,168,25),19,tint);
            Label(parent,report.statistics.completeHistory?(achieved?"已达成":"未达成"):"未记录",new Rect(x+255,553,100,25),16,tint,TextAnchor.MiddleRight);
            Label(parent,rules[i],new Rect(x+64,580,296,30),13,muted);
        }
    }
    private void Update()
    {
        if(entering || departing || RetroSceneLoadReveal.IsBlockingInput)return;
        // RuntimeUiVisibility owns the ongoing cursor policy, including during
        // entry/exit covers. Do not compete with it on every Update.
        if(Input.GetKeyDown(KeyCode.Escape))ReturnToTitle();
    }
    private void LateUpdate() { EnsurePriority(); }
    private void EnsurePriority()
    {
        if(canvas==null)return;
        canvas.overrideSorting=true;canvas.sortingOrder=short.MaxValue-1;
        var camera=CRTScreenEffect.FindActiveUiCamera();
        if(camera!=null) { canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=camera.nearClipPlane+.01f; }
        CRTScreenEffect.RegisterCanvas(canvas,enableGlow:true);
    }
    public void ReturnToTitle() { Depart("TitleScreen",false); }
    public void RestartLevel()
    {
        if(entering || departing || RetroSceneLoadReveal.IsBlockingInput)return;
        if(GameSaveSystem.Instance != null && GameSaveSystem.Instance.RestartCurrentLevel())departing=true;
    }
    public void NextLevel() { Depart(catalog.levels[report.level].nextScene,true); }
    private void Depart(string scene,bool preserveCapacity)
    {
        if(entering || departing || RetroSceneLoadReveal.IsBlockingInput || !Application.CanStreamedLevelBeLoaded(scene))return;
        departing=true;
        if(!RetroSceneLoadReveal.BeginTransition(scene,()=>{
            SceneTravelStateManager.GetOrCreate().ResetAllSceneStates();
            if(preserveCapacity)PersistentInventory.Instance?.PreserveCapacityForNextSceneLoad();
            Time.timeScale=previousTime;
            try { RetroSceneLoadReveal.LoadSceneAsync(scene); }
            catch {
                PersistentInventory.Instance?.PreserveCapacityForNextSceneLoad(false);
                departing=false;Time.timeScale=0;throw;
            }
        })) { departing=false;Time.timeScale=0; }
    }
    private void OnDestroy() { if(instance==this) { instance=null; if(!departing)Time.timeScale=previousTime; } }
    private RectTransform Node(string name,Transform parent,params Type[] types)
    {
        var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);
        foreach(var type in types)go.AddComponent(type);return go.GetComponent<RectTransform>();
    }
    private static void Stretch(RectTransform rect) { rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero; }
    private static void Place(RectTransform rect,Rect box)
    { rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(box.x,-box.y);rect.sizeDelta=box.size; }
    private Text Label(RectTransform parent,string text,Rect box,int size,Color tint,TextAnchor alignment=TextAnchor.MiddleLeft)
    {
        var rect=Node(text,parent,typeof(Text));Place(rect,box);var label=rect.GetComponent<Text>();label.text=text;label.font=font;
        label.fontSize=size;label.color=tint;label.alignment=alignment;label.raycastTarget=false;
        label.horizontalOverflow=HorizontalWrapMode.Wrap;label.verticalOverflow=VerticalWrapMode.Truncate;TitleScreenGlow.Attach(label);return label;
    }
    private void Line(RectTransform parent,Rect box,Color tint)
    { var rect=Node("Line",parent,typeof(Image));Place(rect,box);var image=rect.GetComponent<Image>();image.color=tint;image.raycastTarget=false; }
    private void Border(RectTransform parent,Rect r,Color tint,float t)
    { Line(parent,new Rect(r.x,r.y,r.width,t),tint);Line(parent,new Rect(r.x,r.yMax-t,r.width,t),tint);Line(parent,new Rect(r.x,r.y,t,r.height),tint);Line(parent,new Rect(r.xMax-t,r.y,t,r.height),tint); }
    private void Icon(RectTransform parent,int kind,Rect box,Color tint,Color? backing=null)
    { var rect=Node("Pixel Icon "+kind,parent,typeof(LevelSettlementIcon));Place(rect,box);var icon=rect.GetComponent<LevelSettlementIcon>();icon.kind=kind;icon.color=tint;if(backing.HasValue)icon.backingColor=backing.Value;icon.SetAllDirty();TitleScreenGlow.Attach(icon); }
    private void Button(RectTransform parent,string title,Rect box,UnityEngine.Events.UnityAction action,bool primary)
    {
        var rect=Node(title,parent,typeof(Image),typeof(Button));Place(rect,box);
        var image=rect.GetComponent<Image>();image.color=primary?new Color32(16,49,72,255):new Color32(9,26,48,255);
        var button=rect.GetComponent<Button>();button.targetGraphic=image;
        var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1.5f,1.7f,1.9f);colors.pressedColor=new Color(.65f,.8f,.9f);button.colors=colors;
        button.onClick.AddListener(action);Border(rect,new Rect(0,0,box.width,box.height),primary?ZeldaUiPalette.Primary:ZeldaUiPalette.Ghost,2);
        Label(rect,title,new Rect(4,2,box.width-8,box.height-4),21,ZeldaUiPalette.Primary,TextAnchor.MiddleCenter);
    }
}
