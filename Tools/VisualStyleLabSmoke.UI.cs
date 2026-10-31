#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object=UnityEngine.Object;

public static partial class VisualStyleLabSmoke
{
    static VisualStyleLabTerminalWindow Window(string name) => Object.FindObjectsOfType<VisualStyleLabTerminalWindow>(true).Single(w=>w.name==name);
    static IEnumerator VerifyTerminal(ZeldaFourWayMover mover)
    {
        var profile=Object.FindObjectOfType<VisualStyleLabTerminalPresentation>();
        var hud=ZeldaHealthHeartsUI.Instance;
        Check(hud.GetComponent<VisualStyleLabTerminalScope>()!=null,"HUD has scene-local terminal treatment");
        Check(Object.FindObjectsOfType<VisualStyleLabTerminalChrome>(true).All(c=>!c.raycastTarget),"Corner chrome never intercepts UI input");
        Move(mover,new Vector2(0,-2));yield return Delay(.4f);
        Capture("VisualStyleLab-terminal-hud");
        var layers=hud.GetComponentsInChildren<RuntimeMiniMapLayerGraphic>();
        Check(layers.Single(m=>m.DrawWalls).GetComponent<VisualStyleLabTerminalInk>().Mode==VisualStyleLabTerminalInk.Treatment.MapWalls,"Minimap walls are muted independently of markers");
        Check(layers.Single(m=>!m.DrawWalls&&!m.DrawSurfaces).GetComponent<VisualStyleLabTerminalInk>()==null,"Player/quest/POI marker colours and geometry are preserved");
        var wall=layers.Single(m=>m.DrawWalls);int dirty=0;
        UnityEngine.Events.UnityAction dirtyCallback=()=>dirty++;
        wall.RegisterDirtyVerticesCallback(dirtyCallback);
        var scope=hud.GetComponent<VisualStyleLabTerminalScope>();
        for(int i=0;i<6;i++)
        {
            typeof(VisualStyleLabTerminalScope).GetField("lastFrame",Flags).SetValue(scope,-1);
            typeof(VisualStyleLabTerminalScope).GetMethod("Refresh",Flags).Invoke(scope,null);
        }
        wall.UnregisterDirtyVerticesCallback(dirtyCallback);
        Check(dirty==0,"Repeated terminal refreshes never dirty cached map geometry");
        var pause=Object.FindObjectOfType<PauseMenuController>();pause.OpenMenu();yield return Delay(.4f);
        var pw=Window("Pause Menu Panel");Check(!pw.IsOpening&&Time.timeScale==0,"Window opening finishes while paused");
        var button=pw.GetComponentsInChildren<Button>().First(b=>b.interactable);
        var bg=button.GetComponent<Image>();var normal=bg.color;
        ExecuteEvents.Execute(button.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerEnterHandler);
        yield return Delay(.1f);
        Check(bg.color!=normal,"Pause option retains hover feedback");
        Capture("VisualStyleLab-terminal-pause");
        pause.CloseMenu();yield return null;
        pause.OpenMenu();
        Check(pw.IsOpening&&pw.transform.GetChild(0).GetComponent<CanvasGroup>().alpha==0,"Reopen begins with content hidden behind corner-first reveal");
        yield return Delay(.4f);
        Check(pw.OpenCount==2,"A real reopen replays the brief sequence once");pause.CloseMenu();yield return null;

        var tab=Object.FindObjectOfType<TabJournalMenuController>();tab.OpenMenu();yield return Delay(.4f);
        var tw=Window("Empty Tab Content Panel");int openings=tw.OpenCount;
        var scale=tw.transform.localScale;var panelSize=((RectTransform)tw.transform).rect.size;
        Capture("VisualStyleLab-terminal-map");
        var journal=QuestJournalManager.GetOrCreate();
        journal.AddOrUpdateEntry("lab.ui.parent","视觉实验街区","查看街区内的机关、地图和交互窗口。\n\n当前目标：在测试场景中比较终端界面的显示效果。","VisualStyleLab");
        journal.AddOrUpdateEntry("lab.ui.child","水道控制","使用控制器切换水闸，观察水域变化。","VisualStyleLab","lab.ui.parent");
        journal.AddOrUpdateEntry("lab.ui.other","阅读实验说明","阅读南侧告示牌中的操作说明。","VisualStyleLab");
        journal.TrackEntry("lab.ui.parent");
        typeof(TabJournalMenuController).GetMethod("SetSelectedTab",Flags).Invoke(tab,new object[]{1});yield return Delay(.3f);
        journal.SetEntryCollapsed("lab.ui.parent",true);yield return null;
        journal.SetEntryCollapsed("lab.ui.parent",false);yield return null;
        Capture("VisualStyleLab-terminal-journal");
        typeof(TabJournalMenuController).GetMethod("RefreshQuestJournal",Flags).Invoke(tab,null);yield return null;
        Check(tw.OpenCount==openings&&!tw.IsOpening,"Tab and quest content refresh do not replay/flicker the parent window");
        Check(tw.transform.localScale==scale&&((RectTransform)tw.transform).rect.size==panelSize,"Terminal reveal never scales or reflows content");
        Check(tw.GetComponentsInChildren<Text>(true).Where(t=>t.fontSize<26).All(t=>!t.GetComponent<TitleScreenGlow>()||!t.GetComponent<TitleScreenGlow>().enabled),"Menu body and counters do not carry blur-producing halos");
        tab.CloseMenu();yield return null;

        var slot=PersistentInventory.Instance.Slots.First(s=>s.Quantity>0);
        var item=ItemDescriptionWindow.GetOrCreate();Check(item.OpenForSlot(slot),"Item information still opens");yield return Delay(.4f);
        var iw=Window("Item Description Panel");Check(!iw.IsOpening,"Item window completes short opening");
        var body=iw.GetComponentsInChildren<Text>(true).Single(t=>t.name=="Item Description");
        Check(!body.GetComponent<TitleScreenGlow>()||!body.GetComponent<TitleScreenGlow>().enabled,"Item description keeps crisp body text");
        Capture("VisualStyleLab-terminal-item");
        int itemOpenings=iw.OpenCount;
        string oldText=body.text;body.text=string.Concat(Enumerable.Repeat(oldText+"\n\n",30));
        typeof(ItemDescriptionWindow).GetMethod("RebuildDescriptionLayout",Flags).Invoke(item,new object[]{true});yield return null;
        typeof(ItemDescriptionWindow).GetMethod("ScrollDescription",Flags).Invoke(item,new object[]{-5f});yield return null;
        Check((float)typeof(ItemDescriptionWindow).GetField("scrollOffset",Flags).GetValue(item)>0,"Long descriptions still scroll");
        Check(iw.OpenCount==itemOpenings,"Text pagination/scrolling does not restart opening");
        Check(iw.GetComponentsInChildren<Text>(true).Where(t=>t.name.StartsWith("Description Block")).All(t=>!t.GetComponent<TitleScreenGlow>()||!t.GetComponent<TitleScreenGlow>().enabled),"Dynamically added long-description blocks remain crisp");
        item.CloseWindow();yield return null;

        var doc=Object.FindObjectsOfType<DocumentReader>(true).Single(d=>d.gameObject.scene.name=="VisualStyleLab");
        doc.gameObject.SetActive(true);Move(mover,(Vector2)doc.transform.position+Vector2.down*.8f);yield return Delay(.3f);
        typeof(DocumentReader).GetMethod("OpenDocument",Flags).Invoke(doc,null);yield return Delay(.4f);
        Check(DocumentReader.IsDocumentOpen,"Readable sign still opens through the gameplay interaction");
        Check(!Window("Document Panel").IsOpening,"Reading panel opens without scaling its glyphs");
        Check(Window("Document Panel").GetComponentsInChildren<Text>().All(t=>t.font!=null&&t.cachedTextGenerator.vertexCount>0),"Reading window has its UI font and visible text geometry");
        var documentText=Window("Document Panel").GetComponentsInChildren<Text>().Single(t=>t.name=="Document Content");
        Check(documentText.preferredHeight<=documentText.rectTransform.rect.height+1,"The full lab notice fits without truncating its final paragraph");
        Check(Object.FindObjectsOfType<VisualStyleLabTerminalChrome>().All(c=>c.canvasRenderer!=null&&!c.canvasRenderer.cull),"Every visible terminal corner has a live CanvasRenderer");
        Capture("VisualStyleLab-terminal-document");
        typeof(DocumentReader).GetMethod("CloseDocument",Flags).Invoke(doc,null);yield return null;

        profile.enabled=false;yield return null;yield return null;
        Check(hud.GetComponent<VisualStyleLabTerminalScope>()==null,"Leaving the experiment removes persistent HUD treatment");
        Check(hud.GetComponentsInChildren<VisualStyleLabTerminalInk>(true).Length==0,"Map/colour effects are restored on exit");
        Check(Object.FindObjectsOfType<VisualStyleLabTerminalChrome>(true).Length==0,"No corner overlays survive experiment teardown");
        item.OpenForSlot(slot);yield return Delay(.2f);
        Check(body.GetComponent<TitleScreenGlow>()&&body.GetComponent<TitleScreenGlow>().enabled,"Production glow scope resumes after leaving the lab");
        item.CloseWindow();yield return null;
    }
}
#endif
