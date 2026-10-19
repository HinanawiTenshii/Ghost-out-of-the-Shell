using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public sealed partial class TitleScreenController
{
    private RectTransform progressOptionsRoot;
    private UnityAction progressBack;
    private Text progressStatus;
    private const int LevelsPerPage = 3;

    private void OnDestroy()
    {
        if (GameSaveSystem.Instance != null) GameSaveSystem.Instance.Changed -= RefreshSaveMessage;
    }

    private void HideOptions()
    {
        if (mainOptionsRoot != null) mainOptionsRoot.gameObject.SetActive(false);
        if (progressOptionsRoot != null) progressOptionsRoot.gameObject.SetActive(false);
    }

    private void BeginProgressMenu(string heading, UnityAction back)
    {
        HideOptions();
        if (progressOptionsRoot != null) Destroy(progressOptionsRoot.gameObject);
        progressOptionsRoot = CreateUiObject("Progress Options", mainOptionsRoot.parent).GetComponent<RectTransform>();
        ConfigureOptionsRoot(progressOptionsRoot);
        progressBack = back;
        CreateMenuLabel(progressOptionsRoot, heading, 210f, 20);
        progressStatus = CreateMenuLabel(progressOptionsRoot, "", -228f, 23);
        progressStatus.rectTransform.sizeDelta = new Vector2(320f, 84f);
    }

    private Text CreateMenuLabel(Transform parent, string value, float y, int size)
    {
        var text = CreateUiObject("Menu Caption", parent, typeof(Text)).GetComponent<Text>();
        var rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(320f, 30f);
        text.font = titleFont; text.fontSize = size; text.text = value;
        text.color = GhostBlue; text.alignment = TextAnchor.MiddleLeft;
        text.raycastTarget = false;
        TitleScreenGlow.Attach(text);
        return text;
    }

    public void ShowNewGameSlots() => ShowProfileSlots(true);
    public void ShowProfiles() => ShowProfileSlots(false);

    private void ShowProfileSlots(bool creating)
    {
        var saves = GameSaveSystem.Instance;
        if (saves == null) return;
        var profiles = saves.ListProfiles();
        BeginProgressMenu(creating ? "开始新游戏" : "游戏进度", ShowMainMenu);
        for (int i = 0; i < GameSaveSystem.ProfileCount; i++)
        {
            int slot = i;
            string id = GameSaveSystem.ProfileId(slot);
            var profile = profiles.Find(p => p.id == id);
            var select = CreateOption(progressOptionsRoot, "进度 " + (i + 1) + (profile == null ? " · 空" : ""),
                105f - i * 70f, creating || profile != null, () =>
            {
                if (creating) saves.StartNewGame(slot);
                else if (saves.SelectProfile(id)) ShowProfileActions();
            });
            if (!creating)
            {
                var selectRect = select.GetComponent<RectTransform>();
                selectRect.sizeDelta = new Vector2(224f, 56f);
                selectRect.anchoredPosition = new Vector2(-48f, 105f - i * 70f);
                var remove = CreateOption(progressOptionsRoot, "删除", 105f - i * 70f,
                    profile != null, () => ShowDeleteProfile(id));
                remove.name = "删除进度 " + (i + 1);
                var removeRect = remove.GetComponent<RectTransform>();
                removeRect.sizeDelta = new Vector2(88f, 56f);
                removeRect.anchoredPosition = new Vector2(116f, 105f - i * 70f);
            }
        }
        CreateOption(progressOptionsRoot, "返回", -105f, true, ShowMainMenu);
    }

    private void PageButtons(int page, int pages, UnityAction<int> show)
    {
        if (pages <= 1) return;
        var previous = CreateOption(progressOptionsRoot, "上一页", -112f, page > 0, () => show(page - 1));
        var next = CreateOption(progressOptionsRoot, "下一页", -112f, page + 1 < pages, () => show(page + 1));
        var left = previous.GetComponent<RectTransform>(); left.sizeDelta = new Vector2(154f, 32f); left.anchoredPosition = new Vector2(-83f, -112f);
        var right = next.GetComponent<RectTransform>(); right.sizeDelta = new Vector2(154f, 32f); right.anchoredPosition = new Vector2(83f, -112f);
        previous.GetComponentInChildren<Text>().fontSize = next.GetComponentInChildren<Text>().fontSize = 17;
    }

    public void ShowProfileActions()
    {
        var saves = GameSaveSystem.Instance;
        if (saves == null || string.IsNullOrEmpty(saves.ActiveProfileId)) { ShowProfiles(); return; }
        BeginProgressMenu(saves.ActiveProfileName, ShowProfiles);
        CreateOption(progressOptionsRoot, "载入游戏", 70f, true, OpenLoadSlots);
        CreateOption(progressOptionsRoot, "选择关卡", 0f, true, () => ShowReplayLevels(0));
        CreateOption(progressOptionsRoot, "返回", -70f, true, ShowProfiles);
    }

    private void ShowDeleteProfile(string id)
    {
        var saves = GameSaveSystem.Instance;
        var profile = saves != null ? saves.ListProfiles().Find(p => p.id == id) : null;
        if (profile == null) { ShowProfiles(); return; }
        BeginProgressMenu("删除" + profile.name + "？", ShowProfiles);
        CreateOption(progressOptionsRoot, "确认删除", 35f, true, () =>
        {
            if (!saves.DeleteProfile(id)) return;
            ShowProfiles();
            RefreshContinueButton();
        });
        CreateOption(progressOptionsRoot, "取消", -35f, true, ShowProfiles);
    }

    private void ShowReplayLevels(int page)
    {
        var entries = GameSaveSystem.Instance.ReplayEntries.Where(GameSaveSystem.HasCheckpoint).ToList();
        int pages = Mathf.Max(1, Mathf.CeilToInt(entries.Count / (float)LevelsPerPage));
        page = Mathf.Clamp(page, 0, pages - 1);
        BeginProgressMenu("选择关卡" + (pages > 1 ? "  " + (page + 1) + " / " + pages : ""), ShowProfileActions);
        for (int i = 0; i < LevelsPerPage; i++)
        {
            int index = page * LevelsPerPage + i;
            if (index >= entries.Count) break;
            var entry = entries[index];
            CreateOption(progressOptionsRoot, GameSaveSystem.LevelLabel(entry.entryScene), 105f - i * 70f, true,
                () => GameSaveSystem.Instance.ReplayLevel(entry.id));
        }
        if (entries.Count == 0) CreateMenuLabel(progressOptionsRoot, "暂无关卡", 55f, 23);
        PageButtons(page, pages, ShowReplayLevels);
        CreateOption(progressOptionsRoot, "返回", pages > 1 ? -162f : -105f, true, ShowProfileActions);
    }

    private void RefreshContinueButton()
    {
        if (continueButton == null) return;
        bool available = GameSaveSystem.Instance != null && GameSaveSystem.Instance.HasContinuableProgress;
        continueButton.interactable = available;
        var hover = continueButton.GetComponent<PauseMenuButtonHover>();
        if (hover != null) hover.enabled = available;
        continueButton.GetComponentInChildren<Text>().color = available ? GhostBlue : DisabledBlue;
        continueButton.GetComponent<Image>().color = available ? ButtonBlue : Color.clear;
    }

    private void RefreshSaveMessage()
    {
        if (!SaveSlotPanel.IsOpen && !mainOptionsRoot.gameObject.activeSelf &&
            progressOptionsRoot != null && !progressOptionsRoot.gameObject.activeSelf && !GameSaveSystem.IsLoading)
            ShowProfileActions();
        if (GameSaveSystem.Instance == null) return;
        if (mainOptionsRoot.gameObject.activeSelf && mainStatus != null)
            mainStatus.text = GameSaveSystem.Instance.LastMessage;
        if (progressStatus != null) progressStatus.text = GameSaveSystem.Instance.LastMessage;
    }
}
