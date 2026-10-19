using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Run in an isolated copy. Uses a unique product name, never the player's saves.
[InitializeOnLoad]
public static class SaveProfilesSmoke
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
    static int frame = -1, checks;
    static double deadline;
    static string previewFolder => Path.GetFullPath("../../.codex-temp/save-profiles-previews");
    static SaveProfilesSmoke() { EditorApplication.update += Tick; }
    static void Check(bool condition, string label)
    { checks++; if (!condition) throw new Exception(label); Debug.Log("SAVE_PROFILES_CHECK " + label); }
    static object Invoke(string method, params object[] args) => typeof(GameSaveSystem).GetMethod(method, Flags).Invoke(GameSaveSystem.Instance, args);
    static string Slot(string id, string file) => Path.Combine(GameSaveSystem.ProfilesDirectory, id, file);
    public static void Run()
    {
        Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "isolated project required");
        PlayerSettings.productName = "save-profiles-smoke-" + Guid.NewGuid().ToString("N");
        EditorSceneManager.OpenScene("Assets/Scenes/TitleScreen.unity");
        SessionState.SetBool(nameof(SaveProfilesSmoke), true);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(SaveProfilesSmoke), false))
            { SessionState.SetBool(nameof(SaveProfilesSmoke), false); stack.Push(Verify()); deadline = EditorApplication.timeSinceStartup + 420; }
            if (SessionState.GetBool("SaveProfilesResumePending", false))
            { SessionState.SetBool("SaveProfilesResumePending", false); stack.Push(VerifyResume()); deadline = EditorApplication.timeSinceStartup + 120; }
            if (SessionState.GetBool("SaveProfilesPreviewPending", false))
            { SessionState.SetBool("SaveProfilesPreviewPending", false); stack.Push(VerifyMenus()); deadline = EditorApplication.timeSinceStartup + 60; }
            if (SessionState.GetBool("SaveProfilesDeletionPending", false))
            { SessionState.SetBool("SaveProfilesDeletionPending", false); stack.Push(VerifyDeletion()); deadline = EditorApplication.timeSinceStartup + 180; }
            if (stack.Count == 0) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("save profiles timed out");
            var current = stack.Peek();
            if (current.MoveNext()) { if (current.Current is IEnumerator nested) stack.Push(nested); return; }
            stack.Pop();
            if (stack.Count == 0) { Debug.Log("SAVE_PROFILES_PASS checks=" + checks); EditorApplication.Exit(0); }
        }
        catch (Exception ex) { stack.Clear(); Debug.LogException(ex); EditorApplication.Exit(1); }
    }
    static IEnumerator Ready(string scene)
    {
        int stable = 0;
        while (stable < 3)
        {
            bool ready = SceneManager.GetActiveScene().name == scene && !RetroSceneLoadReveal.IsBlockingInput &&
                !GameSaveSystem.IsLoading && !GameSaveSystem.IsPreparingSceneSave &&
                !SceneTravelStateManager.GetOrCreate().IsSceneTravelRestoreInProgress;
            stable = ready ? stable + 1 : 0;
            yield return null;
        }
        if (scene != "TitleScreen") Time.timeScale = 0f;
    }
    static IEnumerator Title()
    {
        Check(RetroSceneLoadReveal.BeginTransition("TitleScreen", () => {
            SceneTravelStateManager.GetOrCreate().ResetAllSceneStates();
            Time.timeScale = 1f;
            RetroSceneLoadReveal.LoadSceneAsync("TitleScreen");
        }), "return to title accepted");
        yield return Ready("TitleScreen");
    }
    static TitleScreenController Menu => Object.FindObjectOfType<TitleScreenController>();
    static void Growth(int count)
    {
        var growth = PlayerGrowthAttributes.GetOrCreate();
        var state = growth.CaptureRuntimeState(); state.collectibleCount = count; growth.ApplyRuntimeState(state);
    }
    static IEnumerator Advance(string destination)
    {
        Time.timeScale = 1f;
        var exit = Object.FindObjectsOfType<SceneTransitionPoint>(true).FirstOrDefault(p =>
            (string)typeof(SceneTransitionPoint).GetField("targetSceneName", Flags).GetValue(p) == destination);
        Check(exit != null, "authored exit to " + destination);
        typeof(SceneTransitionPoint).GetMethod("LoadTargetScene", Flags).Invoke(exit, null);
        if (LevelSettlementScreen.IsOpen)
        {
            while (RetroSceneLoadReveal.IsBlockingInput) yield return null;
            Object.FindObjectOfType<LevelSettlementScreen>().NextLevel();
        }
        yield return Ready(destination);
    }
    static GameSaveSystem.LevelVisit Entry(string scene) => GameSaveSystem.Instance.ReplayEntries.Single(v =>
        string.Equals(v.id, GameSaveSystem.LevelIdForScene(scene), StringComparison.OrdinalIgnoreCase));
    static IEnumerator Verify()
    {
        yield return Ready("TitleScreen");
        var saves = GameSaveSystem.Instance;
        Check(saves.ListProfiles().Count == 0 && !saves.HasContinuableProgress, "new install has no progress");
        var geometry = Menu.GetComponentsInChildren<RectTransform>(true).Single(t => t.name == "Title Geometry");
        var options = Menu.GetComponentsInChildren<RectTransform>(true).Single(t => t.name == "Title Options");
        Check(geometry.anchoredPosition == new Vector2(-245,0) && geometry.sizeDelta == new Vector2(420,390), "left geometry unchanged");
        Check(options.anchoredPosition == new Vector2(255,0) && options.sizeDelta == new Vector2(330,390), "right geometry unchanged");
        var main = options.GetComponentsInChildren<Button>();
        Check(main.Select(b=>b.name).SequenceEqual(new[]{"开始新游戏","继续游戏","游戏进度","游戏设置（暂未开放）","结束游戏"}), "five main actions in requested order");
        Check(!main[1].interactable, "continue disabled without saves");
        for (int i=0;i<main.Length;i++)
        {
            var rect=(RectTransform)main[i].transform;
            Check(Mathf.Abs(rect.anchoredPosition.y)+rect.sizeDelta.y/2<=195, "main button fits panel " + i);
        }
        Render(Menu,"01-main");
        Menu.StartGame(); Render(Menu,"02-new-game");
        Check(Menu.GetComponentsInChildren<Button>().Count(b=>b.name.StartsWith("进度 "))==3, "new game always shows three slots");
        Check(!saves.StartNewGame(-1) && !saves.StartNewGame(3), "out of range profiles rejected");
        Menu.GetComponentsInChildren<Button>().Single(b=>b.name=="进度 1 · 空").onClick.Invoke();
        yield return Ready("TutorialLevel");
        string a=saves.ActiveProfileId;
        var first=saves.Read(0);
        Check(a=="slot-1" && first.version==4 && !string.IsNullOrEmpty(first.campaignId), "new game commits selected slot and generation");
        Check(first.outcomes.Count==0 && saves.ReplayEntries.Count==1 && saves.CanRestartCurrentLevel, "unfinished tutorial has an available entry");
        string tutorial=Entry("TutorialLevel").id;
        string tutorialCheckpoint=Entry("TutorialLevel").checkpointId;
        yield return Title();
        Check(saves.SelectProfile(a) && !saves.ReplayLevel("Level2"), "unvisited level cannot be selected");
        Menu.ShowProfileActions(); Render(Menu,"04-profile-actions");
        Check(Menu.GetComponentsInChildren<Button>().Select(b=>b.name).SequenceEqual(new[]{"载入游戏","选择关卡","返回"}), "selected progress has load and level selection only");
        typeof(TitleScreenController).GetMethod("ShowReplayLevels",Flags).Invoke(Menu,new object[]{0});
        Check(Menu.GetComponentsInChildren<Button>().Any(b=>b.name=="教学关卡"&&b.interactable), "level selection does not require completion");
        Check(saves.ReplayLevel(tutorial), "select unfinished tutorial");
        yield return Ready("TutorialLevel");
        Check(saves.ActiveProfileId==a && saves.ListProfiles().Count==1 && Entry("TutorialLevel").checkpointId==tutorialCheckpoint, "level selection stays in selected slot without recapturing entry");
        Growth(5);
        yield return Advance("Level0");
        var oldEntry=Entry("Level0");
        Check(saves.Read(0).outcomes.Count==0 && saves.ReplayEntries.Count==2, "uncompleted level0 recorded on actual arrival");
        Growth(17);
        Check(saves.Save(2), "save old level0 gameplay");
        string oldCheckpoint=oldEntry.checkpointId;
        yield return Title();
        Check(saves.SelectProfile(a) && saves.ReplayLevel(tutorial), "return to tutorial entry");
        yield return Ready("TutorialLevel");
        Growth(29);
        yield return Advance("Level0");
        string newCheckpoint=Entry("Level0").checkpointId;
        Check(newCheckpoint!=oldCheckpoint && saves.ReplayEntries.Count==2, "new arrival replaces one logical level entry");
        Check(saves.Load(2), "older manual save still loads");
        yield return Ready("Level0");
        Check(PlayerGrowthAttributes.Instance.CollectibleCount==17 && Entry("Level0").checkpointId==newCheckpoint, "old ordinary save does not roll back latest entry catalogue");
        yield return Title();
        Check(saves.SelectProfile(a) && saves.ReplayLevel(oldEntry.id), "select level0 with replaced entry");
        yield return Ready("Level0");
        Check(PlayerGrowthAttributes.Instance.CollectibleCount==29, "selection restores newer entry state");
        yield return Advance("Level1-Garden");
        var garden=saves.Read(0);
        var gardenEntry=Entry("Level1-Garden");
        string checkpointPath=Path.Combine(GameSaveSystem.CheckpointsDirectory,gardenEntry.checkpointId+".json");
        byte[] checkpointBytes=File.ReadAllBytes(checkpointPath);
        Growth(41);
        PersistentInventory.Instance.ApplyRuntimeState(new PersistentInventory.RuntimeState{slots=new PersistentInventory.RuntimeSlotState[8],selectedSlotIndex=6});
        PersistentInventory.Instance.SetSlot(6,GoldCoinPickupItem.DefaultItemId,17);
        var journal=QuestJournalManager.GetOrCreate(); journal.AddOrUpdateEntry("profile.fixture","Fixture quest","late progress"); journal.RecordQuestCompletion("profile.fixture");
        LevelRunStatistics.Current.deaths=4;
        Check(saves.Save(3)&&saves.Save(1), "save independent manual and quick states");
        SceneTravelStateManager.GetOrCreate().TravelToScene("Level1-Floor1",Vector2.zero);
        yield return Ready("Level1-Floor1");
        Check(Entry("Level1-Garden").checkpointId==gardenEntry.checkpointId && checkpointBytes.SequenceEqual(File.ReadAllBytes(checkpointPath)), "floor travel and ordinary saves preserve level entry");
        Check(saves.RestartCurrentLevel(), "restart from another floor");
        yield return Ready("Level1-Garden");
        Check(PlayerGrowthAttributes.Instance.CollectibleCount==garden.player.growth.collectibleCount && PersistentInventory.Instance.Slots.All(s=>s.IsEmpty), "restart restores entry growth and inventory");
        Check(!journal.ContainsEntry("profile.fixture") && LevelRunStatistics.Current.deaths==garden.player.levelRun.deaths, "restart restores entry quests and counters");
        Check(!saves.Read(0).world.Contains("Level1-Floor1"), "restart removes later floor cache");
        yield return Title();
        Check(saves.StartNewGame(1), "create second fixed slot");
        yield return Ready("TutorialLevel");
        string b=saves.ActiveProfileId, oldCampaign=saves.Read(0).campaignId;
        Check(b=="slot-2" && PlayerGrowthAttributes.Instance.CollectibleCount==0 && PersistentInventory.Instance.SlotCount==5, "second slot starts fresh tutorial");
        Check(saves.Save(1)&&saves.Save(2), "old second slot has quick and manual");
        byte[] oldManual=File.ReadAllBytes(Slot(b,"manual-1.json"));
        yield return Advance("Level0");
        yield return Title();
        Check(saves.StartNewGame(2), "create third fixed slot");
        yield return Ready("TutorialLevel");
        string c=saves.ActiveProfileId;
        Check(c=="slot-3" && saves.ListProfiles().Select(p=>p.id).SequenceEqual(new[]{a,b,c}), "three slots in fixed order");
        yield return Title();
        Check(saves.SelectProfile(a)&&saves.Load(3), "load first slot manual after newer slot creation");
        yield return Ready("Level1-Garden");
        Check(PlayerGrowthAttributes.Instance.CollectibleCount==41 && PersistentInventory.Instance.Slots[6].Quantity==17 && journal.IsEntryCompleted("profile.fixture"), "slots preserve independent gameplay");
        Growth(57); Check(saves.Save(1), "latest quick save is newer than auto");
        yield return Title();
        Check(saves.SelectProfile(c) && saves.ContinueLatest(), "global continue ignores selected and numbered slot order");
        yield return Ready("Level1-Garden");
        Check(saves.ActiveProfileId==a && PlayerGrowthAttributes.Instance.CollectibleCount==57, "global continue restores most recent quick save");
        yield return Title();
        var preserved=new[]{a,c}.SelectMany(id=>Directory.GetFiles(Path.Combine(GameSaveSystem.ProfilesDirectory,id))).ToDictionary(p=>p,p=>File.ReadAllBytes(p));
        Menu.StartGame(); Render(Menu,"06-overwrite");
        Menu.GetComponentsInChildren<Button>().Single(v=>v.name=="进度 2").onClick.Invoke();
        yield return Ready("TutorialLevel");
        Check(saves.ActiveProfileId==b && saves.Read(0).campaignId!=oldCampaign && saves.ListProfiles().Count==3, "overwrite replaces generation without fourth progress");
        Check(saves.Read(1)==null && saves.Read(2)==null && !File.Exists(Slot(b,"quick.json")) && !File.Exists(Slot(b,"manual-1.json")), "overwrite removes old quick and manual saves");
        Check(saves.ReplayEntries.Count==1 && saves.ReplayEntries[0].entryScene=="TutorialLevel", "overwrite removes old level catalogue");
        Check(PlayerGrowthAttributes.Instance.CollectibleCount==0 && PersistentInventory.Instance.SlotCount==5 && PersistentInventory.Instance.Slots.All(v=>v.IsEmpty), "overwrite resets player state");
        foreach(var file in preserved) Check(file.Value.SequenceEqual(File.ReadAllBytes(file.Key)), "other slot untouched: "+file.Key);
        File.WriteAllBytes(Slot(b,"manual-1.json"),oldManual);
        Check(saves.Read(2)==null, "campaign identity rejects leftover old save");
        Check(saves.Save(1)&&!File.Exists(Slot(b,"manual-1.json")), "next commit cleans leftover old generation");
        yield return Title();
        Menu.ShowProfiles(); Render(Menu,"03-profiles");
        Check(Menu.GetComponentsInChildren<Button>().Count(v=>v.name.StartsWith("进度 "))==3 && !Menu.GetComponentsInChildren<Button>().Any(v=>v.name=="下一页"), "profiles always display exactly three slots without paging");
        Check(saves.SelectProfile(b) && !saves.ReplayLevel(oldEntry.id), "selection cannot borrow another slot entry");
        // Old roots, imported profiles and unlimited GUID profiles are removed, never imported.
        File.WriteAllText(Path.Combine(GameSaveSystem.SaveDirectory,"auto.json"),"legacy");
        string[] obsolete={"legacy-slot-0",Guid.NewGuid().ToString("N")};
        foreach(var id in obsolete)
        {
            string dir=Path.Combine(GameSaveSystem.ProfilesDirectory,id);Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir,"profile.json"),"legacy");File.WriteAllText(Path.Combine(dir,"quick.json"),"legacy");
        }
        Invoke("RemoveObsoleteProgress");Invoke("RemoveObsoleteProgress");
        Check(!File.Exists(Path.Combine(GameSaveSystem.SaveDirectory,"auto.json")) && obsolete.All(id=>!Directory.Exists(Path.Combine(GameSaveSystem.ProfilesDirectory,id))) && saves.ListProfiles().Count==3, "legacy cleanup is repeatable and preserves fixed slots");
        Check(saves.SelectProfile(a), "select first for index recovery");
        File.WriteAllText(Slot(a,"profile.json"),"{}");
        Check(saves.ListProfiles().Any(p=>p.id==a) && saves.SelectProfile(a) && Entry("Level0").checkpointId==newCheckpoint, "metadata recovery retains newest level entry");
        File.WriteAllText(checkpointPath,"{\"payload\":\"corrupt\",\"checksum\":\"bad\"}");
        Check(!saves.ReplayLevel(gardenEntry.id) && saves.ListProfiles().Count==3, "corrupt entry is rejected without creating progress");
        File.WriteAllBytes(checkpointPath,checkpointBytes);
        Menu.ShowProfileActions();typeof(TitleScreenController).GetMethod("ShowReplayLevels",Flags).Invoke(Menu,new object[]{0});Render(Menu,"05-replay");
        Check(saves.Load(3), "load manual for final continue fixture");
        yield return Ready("Level1-Garden");
        Growth(63); Check(saves.Save(1), "new quick fixture");
        File.WriteAllText(Slot(a,"auto.json"),"corrupt");
        yield return Title();
        Check(saves.ContinueLatest(), "continue skips damaged auto save");
        yield return Ready("Level1-Garden");
        Check(PlayerGrowthAttributes.Instance.CollectibleCount==63 && saves.ActiveProfileId==a, "continue selects newest valid save");
        yield return Title();
        Menu.ShowMainMenu();Render(Menu,"07-main-with-saves");
        File.WriteAllText(Path.GetFullPath("../../.codex-temp/save-profiles-resume.json"),JsonUtility.ToJson(new ResumeData{product=PlayerSettings.productName,profile=a,checkpoint=newCheckpoint}));
    }

    [Serializable] public sealed class ResumeData { public string product,profile,checkpoint; }
    public static void Resume()
    {
        Check(Application.dataPath.Replace(Path.DirectorySeparatorChar,'/').Contains("/.codex-temp/"),"resume isolated only");
        var data=JsonUtility.FromJson<ResumeData>(File.ReadAllText("../../.codex-temp/save-profiles-resume.json"));
        PlayerSettings.productName=data.product;
        EditorSceneManager.OpenScene("Assets/Scenes/TitleScreen.unity");
        SessionState.SetString("SaveProfilesResume",JsonUtility.ToJson(data));
        SessionState.SetBool("SaveProfilesResumePending",true);
        EditorApplication.EnterPlaymode();
    }
    static IEnumerator VerifyResume()
    {
        yield return Ready("TitleScreen");
        var data=JsonUtility.FromJson<ResumeData>(SessionState.GetString("SaveProfilesResume",""));
        var saves=GameSaveSystem.Instance;
        Check(saves.ListProfiles().Count==3 && saves.ContinueLatest(), "three fixed profiles and continue after process restart");
        yield return Ready("Level1-Garden");
        Check(saves.ActiveProfileId==data.profile && PlayerGrowthAttributes.Instance.CollectibleCount==63 && Entry("Level0").checkpointId==data.checkpoint, "new process restores latest gameplay and overwritten entry catalogue");
        Check(saves.RestartCurrentLevel(),"restart after process restart");
        yield return Ready("Level1-Garden");
        Check(PlayerGrowthAttributes.Instance.CollectibleCount==29 && PersistentInventory.Instance.Slots.All(v=>v.IsEmpty),"entry survives process restart");
        Time.timeScale=1f;
        var pause=Object.FindObjectOfType<PauseMenuController>();
        Check(pause!=null,"authored pause controller");pause.OpenMenu();
        var restart=pause.GetComponentsInChildren<Button>().Single(v=>v.name=="重新开始本关");
        restart.onClick.Invoke();
        Check(!RetroSceneLoadReveal.IsBlockingInput && restart.GetComponentInChildren<Text>().text.Contains("确认"),"pause restart second click");
        restart.onClick.Invoke();yield return Ready("Level1-Garden");
        Check(!PauseMenuController.IsPaused,"pause restart closes menu");
        Check(LevelSettlementScreen.TryOpen("Level2-Desert"),"settlement restart fixture");
        while(RetroSceneLoadReveal.IsBlockingInput)yield return null;
        Object.FindObjectOfType<LevelSettlementScreen>().RestartLevel();yield return Ready("Level1-Garden");
        Check(!LevelSettlementScreen.IsOpen && saves.CanRestartCurrentLevel,"settlement restart retains entry");
        yield return Advance("Level2-Desert");
        Check(saves.ReplayEntries.Count==4,"four visited logical levels available in same profile");
        yield return Title();Check(saves.SelectProfile(data.profile),"select for level pagination");
        Menu.ShowProfileActions();typeof(TitleScreenController).GetMethod("ShowReplayLevels",Flags).Invoke(Menu,new object[]{1});
        Render(Menu,"08-levels-second-page");
        Check(Menu.GetComponentsInChildren<Button>().Any(v=>v.name=="Level 2-边境城市" && v.interactable),"fourth recorded level remains selectable");
        Check(saves.ReplayLevel(GameSaveSystem.LevelIdForScene("Level0")),"select overwritten level after process restart");
        yield return Ready("Level0");
        Check(PlayerGrowthAttributes.Instance.CollectibleCount==29 && saves.ListProfiles().Count==3,"selection restores latest entry in original slot");
    }
    public static void PreviewMenus()
    {
        Check(Application.dataPath.Replace(Path.DirectorySeparatorChar,'/').Contains("/.codex-temp/"),"preview isolated only");
        var data=JsonUtility.FromJson<ResumeData>(File.ReadAllText("../../.codex-temp/save-profiles-resume.json"));
        PlayerSettings.productName=data.product;
        EditorSceneManager.OpenScene("Assets/Scenes/TitleScreen.unity");
        SessionState.SetBool("SaveProfilesPreviewPending",true);
        EditorApplication.EnterPlaymode();
    }
    static IEnumerator VerifyMenus()
    {
        yield return Ready("TitleScreen");
        var saves=GameSaveSystem.Instance;
        Menu.ShowMainMenu();Render(Menu,"07-main-with-saves");
        Menu.StartGame();Render(Menu,"06-overwrite");
        Check(Menu.GetComponentsInChildren<Button>().Count(b=>b.name.StartsWith("进度 "))==3,"three simple progress buttons");
        Check(Menu.GetComponentsInChildren<Text>().All(t=>string.IsNullOrEmpty(t.text)||t.fontSize>=19),"no small caption text in new game menu");
        Menu.ShowProfiles();Render(Menu,"03-profiles");
        Check(saves.SelectProfile("slot-1"),"select progress for menu preview");
        Menu.ShowProfileActions();Render(Menu,"04-profile-actions");
        typeof(TitleScreenController).GetMethod("ShowReplayLevels",Flags).Invoke(Menu,new object[]{0});Render(Menu,"05-replay");
        Check(Menu.GetComponentsInChildren<Text>().All(t=>!t.text.Contains("载入最近")&&!t.text.Contains("覆盖")&&!t.text.Contains("创建新进度")),"explanatory subtitles removed");
        typeof(TitleScreenController).GetMethod("ShowReplayLevels",Flags).Invoke(Menu,new object[]{1});Render(Menu,"08-levels-second-page");
        Check(Menu.GetComponentsInChildren<Button>().Any(b=>b.name=="Level 2-边境城市"&&b.interactable),"level pagination still available");
    }
    public static void DeleteProfiles()
    {
        Check(Application.dataPath.Replace(Path.DirectorySeparatorChar,'/').Contains("/.codex-temp/"),"delete tests isolated only");
        PlayerSettings.productName="save-profile-delete-"+Guid.NewGuid().ToString("N");
        EditorSceneManager.OpenScene("Assets/Scenes/TitleScreen.unity");
        SessionState.SetBool("SaveProfilesDeletionPending",true);
        EditorApplication.EnterPlaymode();
    }
    static Button MenuButton(string name) => Menu.GetComponentsInChildren<Button>().Single(b=>b.name==name);
    static Dictionary<string,byte[]> ProfileFiles(params string[] ids) => ids
        .SelectMany(id=>Directory.GetFiles(Path.Combine(GameSaveSystem.ProfilesDirectory,id)))
        .ToDictionary(p=>p,p=>File.ReadAllBytes(p));
    static void FilesUnchanged(Dictionary<string,byte[]> files,string label) => Check(files.All(f=>File.Exists(f.Key)&&f.Value.SequenceEqual(File.ReadAllBytes(f.Key))),label);
    static IEnumerator VerifyDeletion()
    {
        yield return Ready("TitleScreen");
        var saves=GameSaveSystem.Instance;
        Check(!saves.DeleteProfile("../slot-1")&&!saves.DeleteProfile("slot-4"),"invalid deletion targets rejected");
        Menu.ShowProfiles();
        Check(Menu.GetComponentsInChildren<Button>().Count(b=>b.name.StartsWith("删除进度 "))==3 &&
            Menu.GetComponentsInChildren<Button>().Where(b=>b.name.StartsWith("删除进度 ")).All(b=>!b.interactable),"empty rows have disabled delete buttons");
        var entries=new string[3];
        for(int index=0;index<3;index++)
        {
            Check(saves.StartNewGame(index),"create deletion fixture "+index);
            yield return Ready("TutorialLevel");
            Check(!saves.DeleteProfile(saves.ActiveProfileId),"gameplay cannot delete a progress slot");
            entries[index]=saves.ReplayEntries[0].checkpointId;
            Growth(10+index);
            for(int slot=1;slot<GameSaveSystem.SlotCount;slot++)Check(saves.Save(slot),"fixture saves "+index+" / "+slot);
            yield return Title();
        }
        var a="slot-1";var b="slot-2";var c="slot-3";
        Check(saves.SelectProfile(a),"keep another slot selected");
        Menu.ShowProfiles();
        for(int index=0;index<3;index++)
        {
            var select=(RectTransform)MenuButton("进度 "+(index+1)).transform;
            var remove=(RectTransform)MenuButton("删除进度 "+(index+1)).transform;
            Check(select.anchoredPosition.y==remove.anchoredPosition.y && select.anchoredPosition.x+select.sizeDelta.x/2<remove.anchoredPosition.x-remove.sizeDelta.x/2 &&
                remove.anchoredPosition.x+remove.sizeDelta.x/2==160,"delete aligned right within original menu width "+index);
        }
        Render(Menu,"09-delete-progress-rows");
        var unchanged=ProfileFiles(a,b,c);
        MenuButton("删除进度 2").onClick.Invoke();
        Check(Menu.GetComponentsInChildren<Button>().Select(v=>v.name).SequenceEqual(new[]{"确认删除","取消"}),"minimal deletion confirmation");
        Render(Menu,"10-delete-confirmation");
        MenuButton("取消").onClick.Invoke();
        FilesUnchanged(unchanged,"cancel keeps all progress files unchanged");
        Check(saves.ActiveProfileId==a && saves.ListProfiles().Count==3,"cancel keeps selected progress");
        MenuButton("删除进度 2").onClick.Invoke();
        ((UnityEngine.Events.UnityAction)typeof(TitleScreenController).GetField("progressBack",Flags).GetValue(Menu))();
        Check(MenuButton("删除进度 2").interactable && saves.ListProfiles().Count==3,"Escape handler cancels confirmation");
        // Simulate an entrance snapshot still being held by another reader.
        // The slot disappears atomically; startup cleanup must not touch a new
        // campaign subsequently created in that same slot.
        string oldEntry=Path.Combine(GameSaveSystem.CheckpointsDirectory,entries[2]+".json");
        var preserved=ProfileFiles(a,b);
        using(var locked=new FileStream(oldEntry,FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            MenuButton("删除进度 3").onClick.Invoke();MenuButton("确认删除").onClick.Invoke();
            Check(saves.ListProfiles().Count==2 && !Directory.Exists(Path.Combine(GameSaveSystem.ProfilesDirectory,c)),"locked checkpoint cannot resurrect deleted slot");
        }
        FilesUnchanged(preserved,"delete does not modify other slots");
        Check(saves.ActiveProfileId==a && !MenuButton("删除进度 3").interactable,"deleting unselected slot preserves selection and empties row");
        Check(saves.ContinueLatest(),"continue after deleting latest progress");
        yield return Ready("TutorialLevel");
        Check(saves.ActiveProfileId==b && PlayerGrowthAttributes.Instance.CollectibleCount==11,"continue chooses newest surviving progress");
        yield return Title();
        Check(saves.StartNewGame(2),"reuse deleted slot while cleanup pending");
        yield return Ready("TutorialLevel");
        string replacement=Entry("TutorialLevel").checkpointId;
        yield return Title();
        Invoke("RemoveObsoleteProgress");
        Check(!File.Exists(oldEntry) && !Directory.GetDirectories(GameSaveSystem.ProfilesDirectory,"deleted-*").Any(),"startup completes interrupted deletion");
        Check(File.Exists(Path.Combine(GameSaveSystem.CheckpointsDirectory,replacement+".json")) && saves.SelectProfile(c),"cleanup preserves replacement campaign in same slot");
        // An entry no longer referenced by the latest catalogue must be removed too.
        Check(saves.SelectProfile(b),"select progress being deleted");
        var snapshot=saves.Read(0);
        string superseded=Guid.NewGuid().ToString("N");
        Invoke("WriteData",Path.Combine(GameSaveSystem.CheckpointsDirectory,superseded+".json"),snapshot);
        preserved=ProfileFiles(a,c);
        Menu.ShowProfiles();MenuButton("删除进度 2").onClick.Invoke();MenuButton("确认删除").onClick.Invoke();
        Check(saves.ActiveProfileId==null && saves.ReplayEntries.Count==0 && saves.Read(0)==null,"deleting selected progress clears active session");
        Check(!Directory.Exists(Path.Combine(GameSaveSystem.ProfilesDirectory,b)) &&
            !File.Exists(Path.Combine(GameSaveSystem.CheckpointsDirectory,entries[1]+".json")) &&
            !File.Exists(Path.Combine(GameSaveSystem.CheckpointsDirectory,superseded+".json")),"deletion removes all saves and superseded entries");
        FilesUnchanged(preserved,"other slots remain byte-identical after complete deletion");
        Render(Menu,"11-progress-after-delete");
        MenuButton("删除进度 1").onClick.Invoke();MenuButton("确认删除").onClick.Invoke();
        MenuButton("删除进度 3").onClick.Invoke();MenuButton("确认删除").onClick.Invoke();
        Check(saves.ListProfiles().Count==0 && !saves.HasContinuableProgress && !saves.ContinueLatest(),"deleting last progress leaves nothing to continue");
        Menu.ShowMainMenu();Check(!MenuButton("继续游戏").interactable,"continue button disables after final deletion");
        Object.Destroy(saves.gameObject);yield return null;
        saves=new GameObject("Reloaded save service").AddComponent<GameSaveSystem>();
        Check(saves.ListProfiles().Count==0 && !saves.HasContinuableProgress,"deleted slots stay empty after save service restart");
        Menu.StartGame();MenuButton("进度 2 · 空").onClick.Invoke();
        yield return Ready("TutorialLevel");
        Check(saves.ActiveProfileId==b && saves.ListProfiles().Count==1 && PlayerGrowthAttributes.Instance.CollectibleCount==0,"deleted slot can start a clean tutorial campaign");
    }
    static void Render(TitleScreenController title, string name)
    {
        var camera=new GameObject("Title Preview Camera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=360;
        camera.transform.position=new Vector3(0,0,-10);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
        var canvas=title.GetComponent<Canvas>();var oldCamera=canvas.worldCamera;var oldMode=canvas.renderMode;float oldDistance=canvas.planeDistance;
        canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
        var rt=new RenderTexture(1280,720,24);camera.targetTexture=rt;Canvas.ForceUpdateCanvases();camera.Render();
        var previous=RenderTexture.active;RenderTexture.active=rt;var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);
        texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();Directory.CreateDirectory(previewFolder);
        File.WriteAllBytes(Path.Combine(previewFolder,name+".png"),texture.EncodeToPNG());
        RenderTexture.active=previous;canvas.worldCamera=oldCamera;canvas.renderMode=oldMode;canvas.planeDistance=oldDistance;
        camera.targetTexture=null;rt.Release();Object.Destroy(rt);Object.Destroy(texture);Object.Destroy(camera.gameObject);
    }
}



