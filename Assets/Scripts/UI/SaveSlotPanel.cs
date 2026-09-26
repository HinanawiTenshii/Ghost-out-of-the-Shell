using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class SaveSlotPanel : MonoBehaviour
{
    public static bool IsOpen { get; private set; }
    public static int LastClosedFrame { get; private set; } = -1;
    private Font font;
    private bool saveMode;
    private Action closed;
    private RectTransform list;
    private Text status;
    private bool useTitleLayout;
    private int confirmingDeleteSlot = -1;

    public static void Open(Transform parent, Font font, bool saving, bool titleLayout, Action onClose)
    {
        if (IsOpen) return;
        var root = new GameObject("Save Slots", typeof(RectTransform), typeof(SaveSlotPanel));
        root.transform.SetParent(parent, false);
        var panel = root.GetComponent<SaveSlotPanel>();
        panel.font = font;
        panel.saveMode = saving;
        panel.closed = onClose;
        panel.useTitleLayout = titleLayout;
        IsOpen = true;
        panel.Build(titleLayout);
        var canvas = root.GetComponentInParent<Canvas>();
        if (canvas != null) CRTScreenEffect.RegisterCanvas(canvas);
    }
    private void Update()
    {
        if (RetroSceneLoadReveal.IsBlockingInput) return;
        if (Input.GetKeyDown(KeyCode.Escape)) Close();
    }
    private void OnDestroy() { IsOpen = false; LastClosedFrame = Time.frameCount; }
    private void Close()
    {
        IsOpen = false; LastClosedFrame = Time.frameCount;
        closed?.Invoke(); Destroy(gameObject);
    }
    private void Build(bool titleLayout)
    {
        var root = (RectTransform)transform;
        root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
        root.offsetMin = root.offsetMax = Vector2.zero;
        if (!titleLayout) root.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.76f);
        list = Rect(
            "Slot Panel",
            root,
            titleLayout ? new Vector2(255f, 0f) : Vector2.zero,
            new Vector2(titleLayout ? 330f : 510f, titleLayout ? 550f : 640f));
        if (!titleLayout)
        {
            list.gameObject.AddComponent<Image>().color =
                new Color(0.025f, 0.07f, 0.12f, 0.98f);
            Border(list);
            Label(
                list,
                saveMode ? "储存游戏" : "读取游戏",
                new Vector2(0f, 295f),
                new Vector2(330f, 30f),
                22);
        }
        RefreshSlots();
        if (titleLayout)
        {
            status = Label(
                list,
                "",
                new Vector2(0f, -260f),
                new Vector2(320f, 20f),
                13,
                TextAnchor.MiddleLeft);
            CreateTitleButton(
                list,
                "Back",
                "返回",
                new Vector2(0f, -220f),
                true,
                Close);
        }
        else
        {
            status = Label(
                list,
                "",
                new Vector2(0f, -277f),
                new Vector2(340f, 20f),
                13);
            var back = Rect(
                "Back",
                list,
                new Vector2(0f, -301f),
                new Vector2(180f, 26f));
            MakeButton(back, true, Close);
            Label(back, "返回 [Esc]", Vector2.zero, back.sizeDelta, 16);
        }
    }
    private void RefreshSlots()
    {
        var old = list.Find("Rows");
        if (old != null) { old.gameObject.SetActive(false); Destroy(old.gameObject); }
        if (useTitleLayout)
        {
            RefreshTitleSlots();
            return;
        }

        var rows = Rect("Rows", list, Vector2.zero, list.sizeDelta);
        float width = list.sizeDelta.x - 36;
        // Separate auto, quick and manual groups, with five slots in the last group.
        for (int group = 0; group < 3; group++)
        {
            var frame = Rect("Group " + group, rows, new Vector2(0, group == 0 ? 236 : group == 1 ? 158 : -73),
                new Vector2(width, group < 2 ? 72 : 382));
            Border(frame);
        }
        for (int slot = 0; slot < GameSaveSystem.SlotCount; slot++)
        {
            int index = slot;
            var data = GameSaveSystem.Instance.Read(slot);
            var row = Rect("Slot " + slot, rows, new Vector2(0, 236 - slot * 77), new Vector2(width - 16, 64));
            if (slot >= 2 && confirmingDeleteSlot == slot)
            {
                BuildDeleteConfirmation(row, index);
                continue;
            }
            if (slot >= 2 && data != null)
            {
                BuildManualSlot(row, index, data);
                continue;
            }
            bool enabled = saveMode ? slot >= 2 : data != null;
            MakeButton(row, enabled, () => ActivateSlot(index));
            if (slot >= 2)
            {
                Border(row);
            }
            string kind = slot == 0 ? "自动存档" : slot == 1 ? "快速存档" : "手动存档 " + (slot - 1);
            Label(row, data != null ? data.name : kind + "（空）", new Vector2(0, 21), new Vector2(width - 30, 20), 15);
            Line(row, 11, width - 18);
            Label(row, data != null ? data.levelName : kind, new Vector2(0, 0), new Vector2(width - 30, 19), 14);
            Line(row, -10, width * 0.65f);
            Label(row, data != null ? data.savedAt : "—", new Vector2(0, -22), new Vector2(width - 30, 18), 12);
        }
    }

    private void ActivateSlot(int slot)
    {
        if (saveMode)
        {
            GameSaveSystem.Instance.Save(slot);
            confirmingDeleteSlot = -1;
            RefreshSlots();
            status.text = GameSaveSystem.Instance.LastMessage;
        }
        else
        {
            if (GameSaveSystem.Instance.Load(slot)) Destroy(gameObject);
            else status.text = GameSaveSystem.Instance.LastMessage;
        }
    }

    private void BuildManualSlot(RectTransform row, int slot, GameSaveSystem.SaveData data)
    {
        row.gameObject.AddComponent<Image>().color = new Color(0.04f, 0.14f, 0.24f);
        Border(row);

        const float headerHeight = 21f;
        const float deleteWidth = 76f;
        var header = Rect("Save Name", row, new Vector2(0f, 21.5f),
            new Vector2(row.sizeDelta.x - 2f, headerHeight));
        MakeButton(header, true, () => ActivateSlot(slot));
        Label(header, data.name, Vector2.zero, header.sizeDelta, 15);
        Line(row, 11f, row.sizeDelta.x - 2f);

        float detailsWidth = row.sizeDelta.x - deleteWidth;
        var details = Rect("Save Details", row, new Vector2(-deleteWidth * 0.5f, -10.5f),
            new Vector2(detailsWidth, 41f));
        MakeButton(details, true, () => ActivateSlot(slot));
        Label(details, data.levelName, new Vector2(8f, 9f), new Vector2(detailsWidth - 18f, 18f), 14,
            TextAnchor.MiddleLeft);
        Line(details, 0f, detailsWidth - 10f);
        Label(details, data.savedAt, new Vector2(8f, -10f), new Vector2(detailsWidth - 18f, 17f), 12,
            TextAnchor.MiddleLeft);

        var remove = Rect("Delete Save", row,
            new Vector2(detailsWidth * 0.5f, -10.5f), new Vector2(deleteWidth, 41f));
        MakeButton(remove, true, () =>
        {
            confirmingDeleteSlot = slot;
            RefreshSlots();
        });
        var divider = Rect("Delete Divider", row,
            new Vector2(detailsWidth * 0.5f - deleteWidth * 0.5f, -10.5f), new Vector2(1f, 41f));
        var dividerImage = divider.gameObject.AddComponent<Image>();
        dividerImage.color = ZeldaUiPalette.Ghost;
        dividerImage.raycastTarget = false;
        Text cross = Label(remove, "×", Vector2.zero, remove.sizeDelta, 35);
        cross.color = Color.white;
    }

    private void BuildDeleteConfirmation(RectTransform row, int slot)
    {
        row.gameObject.AddComponent<Image>().color = new Color(0.04f, 0.14f, 0.24f);
        Border(row);
        Label(row, "删除存档？", new Vector2(0f, 20.5f), new Vector2(row.sizeDelta.x - 4f, 21f), 15);
        Line(row, 10f, row.sizeDelta.x - 2f);

        float buttonWidth = (row.sizeDelta.x - 18f) * 0.5f;
        var yes = Rect("Confirm Delete", row, new Vector2(-buttonWidth * 0.5f - 3f, -11f),
            new Vector2(buttonWidth, 36f));
        MakeButton(yes, true, () =>
        {
            if (GameSaveSystem.Instance.DeleteManualSave(slot))
            {
                confirmingDeleteSlot = -1;
                RefreshSlots();
            }
            status.text = GameSaveSystem.Instance.LastMessage;
        });
        Border(yes);
        Label(yes, "是", Vector2.zero, yes.sizeDelta, 16);

        var no = Rect("Cancel Delete", row, new Vector2(buttonWidth * 0.5f + 3f, -11f),
            new Vector2(buttonWidth, 36f));
        MakeButton(no, true, () =>
        {
            confirmingDeleteSlot = -1;
            RefreshSlots();
        });
        Border(no);
        Label(no, "否", Vector2.zero, no.sizeDelta, 16);
    }

    private void RefreshTitleSlots()
    {
        var rows = Rect("Rows", list, Vector2.zero, list.sizeDelta);
        for (int slot = 0; slot < GameSaveSystem.SlotCount; slot++)
        {
            int index = slot;
            var data = GameSaveSystem.Instance.Read(slot);
            string emptyName = slot == 0
                ? "自动存档（空）"
                : slot == 1
                    ? "快速存档（空）"
                    : "手动存档 " + (slot - 1) + "（空）";
            CreateTitleButton(
                rows,
                "Slot " + slot,
                data != null ? data.name : emptyName,
                new Vector2(0f, 198f - slot * 58f),
                data != null,
                () =>
                {
                    if (GameSaveSystem.Instance.Load(index))
                    {
                        Destroy(gameObject);
                    }
                    else if (status != null)
                    {
                        status.text = GameSaveSystem.Instance.LastMessage;
                    }
                });
        }
    }

    private void CreateTitleButton(
        Transform parent,
        string objectName,
        string label,
        Vector2 position,
        bool enabled,
        Action action)
    {
        var row = Rect(objectName, parent, position, new Vector2(320f, 56f));
        var image = row.gameObject.AddComponent<Image>();
        Color normalColor = enabled
            ? new Color(0.035f, 0.12f, 0.20f, 0.92f)
            : new Color(0f, 0f, 0f, 0f);
        image.color = normalColor;

        var button = row.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.interactable = enabled;
        button.transition = Selectable.Transition.None;
        if (enabled)
        {
            row.gameObject.AddComponent<PauseMenuButtonHover>().Configure(
                image,
                normalColor,
                Color.Lerp(normalColor, ZeldaUiPalette.Ghost, 0.42f));
        }
        if (action != null)
        {
            button.onClick.AddListener(() => action());
        }

        Text text = Label(
            row,
            label,
            Vector2.zero,
            new Vector2(296f, row.sizeDelta.y - 4f),
            enabled ? 23 : 19,
            TextAnchor.MiddleLeft);
        text.color = enabled
            ? ZeldaUiPalette.Ghost
            : new Color(0.18f, 0.28f, 0.35f, 1f);
    }
    private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size; rect.anchoredPosition = position; return rect;
    }
    private Text Label(
        Transform parent,
        string value,
        Vector2 pos,
        Vector2 size,
        int fontSize,
        TextAnchor alignment = TextAnchor.MiddleCenter)
    {
        var text = Rect("Label", parent, pos, size).gameObject.AddComponent<Text>();
        text.text = value; text.font = font; text.fontSize = fontSize;
        text.color = ZeldaUiPalette.Ghost; text.alignment = alignment;
        text.raycastTarget = false;
        if (useTitleLayout) TitleScreenGlow.Attach(text);
        return text;
    }
    private static void MakeButton(RectTransform rect, bool enabled, Action action)
    {
        var image = rect.gameObject.AddComponent<Image>();
        image.color = enabled ? new Color(0.04f, 0.14f, 0.24f) : new Color(0.06f, 0.09f, 0.13f);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image; button.interactable = enabled;
        button.transition = Selectable.Transition.None;
        if (enabled) rect.gameObject.AddComponent<PauseMenuButtonHover>().Configure(image, image.color,
            Color.Lerp(image.color, ZeldaUiPalette.Ghost, 0.4f));
        button.onClick.AddListener(() => action());
    }
    private static void Line(RectTransform parent, float y, float width)
    {
        var image = Rect("Line", parent, new Vector2(0, y), new Vector2(width, 1)).gameObject.AddComponent<Image>();
        image.color = ZeldaUiPalette.Ghost; image.raycastTarget = false;
    }
    private static void Border(RectTransform parent)
    {
        Line(parent, parent.sizeDelta.y * 0.5f, parent.sizeDelta.x);
        Line(parent, -parent.sizeDelta.y * 0.5f, parent.sizeDelta.x);
        foreach (int sign in new[] { -1, 1 })
        {
            var image = Rect("Edge", parent, new Vector2(sign * parent.sizeDelta.x * 0.5f, 0),
                new Vector2(1, parent.sizeDelta.y)).gameObject.AddComponent<Image>();
            image.color = ZeldaUiPalette.Ghost; image.raycastTarget = false;
        }
    }
}
