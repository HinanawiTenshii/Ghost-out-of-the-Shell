using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Paused CRT-compatible permission editor. E commits, Escape cancels the draft.</summary>
public sealed class ElectricGatePanel : MonoBehaviour
{
    private static ElectricGatePanel active;
    private static int closedFrame = -1;
    public static bool IsOpen => active != null || Time.frameCount <= closedFrame;
    private ElectricGatePillar owner;
    private ZeldaCharacterData actor;
    private ElectricGatePillar.RuntimeState draft;
    private Canvas canvas;
    private RectTransform panel;
    private Font font;
    private Text modeText, valueText, statusText;
    private GameObject permissionControls, individualControls;
    private float previousTimeScale;
    private bool cursorVisible, restored;
    private CursorLockMode cursorLock;
    private int openedFrame;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { active = null; closedFrame = -1; }

    public static void Open(ElectricGatePillar pillar, ZeldaCharacterData character, Font preferredFont)
    {
        if (IsOpen || pillar == null || character == null) return;
        if (!CharacterSkillRequirements.Require(character, CharacterSkillRequirements.ElectricGatePanel)) return;
        var go = new GameObject("Electric Gate Access Panel") { hideFlags = HideFlags.HideAndDontSave };
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, pillar.gameObject.scene);
        var window = go.AddComponent<ElectricGatePanel>(); active = window;
        window.owner = pillar; window.actor = character; window.draft = pillar.CaptureRuntimeState();
        window.font = preferredFont != null ? preferredFont : ZeldaHealthHeartsUI.Instance?.PermissionLabelFont;
        if (window.font == null) window.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        window.previousTimeScale = Time.timeScale; window.cursorVisible = Cursor.visible; window.cursorLock = Cursor.lockState;
        window.openedFrame = Time.frameCount;
        Time.timeScale = 0; Cursor.visible = true; Cursor.lockState = CursorLockMode.None;
        window.Build(); window.Refresh();
    }
    public static void CloseFor(ElectricGatePillar pillar) { if (active != null && active.owner == pillar) active.Close(false); }
    private void Update()
    {
        if (owner == null || !owner.isActiveAndEnabled || !owner.HasPanel) { Close(false); return; }
        if (Time.frameCount <= openedFrame) return;
        if (Input.GetKeyDown(KeyCode.Escape)) Close(false);
        else if (Input.GetKeyDown(KeyCode.E)) Close(true);
        else if (draft.mode == ElectricGatePillar.PassageMode.Permission)
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.DownArrow)) ChangeLevel(-1);
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.UpArrow)) ChangeLevel(1);
        }
    }
    private void LateUpdate()
    {
        if (canvas == null) return;
        var camera = CRTScreenEffect.FindActiveUiCamera();
        canvas.overrideSorting = true; canvas.sortingOrder = short.MaxValue;
        int highest = int.MinValue;
        foreach (var layer in SortingLayer.layers) if (layer.value > highest) { highest = layer.value; canvas.sortingLayerID = layer.id; }
        canvas.renderMode = camera != null ? RenderMode.ScreenSpaceCamera : RenderMode.ScreenSpaceOverlay;
        canvas.worldCamera = camera;
        if (camera != null) canvas.planeDistance = camera.nearClipPlane + .01f;
        CRTScreenEffect.RegisterCanvas(canvas, enableGlow: true);
    }
    private void Build()
    {
        if (EventSystem.current == null)
        {
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(events);
        }
        var root = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false); canvas = root.GetComponent<Canvas>();
        var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280,720); scaler.matchWidthOrHeight = .5f;
        var backdrop = Box(root.transform, "Dimmer", Vector2.zero, Vector2.zero, new Color(0,0,0,.76f));
        backdrop.anchorMin = Vector2.zero; backdrop.anchorMax = Vector2.one; backdrop.offsetMin = backdrop.offsetMax = Vector2.zero;
        var border = Box(backdrop, "Border", Vector2.zero, new Vector2(704,414), ZeldaUiPalette.Ghost);
        panel = Box(border, "Panel", Vector2.zero, new Vector2(698,408), new Color(.025f,.07f,.12f,.98f));
        Label(panel, "电门 · 通行设置", new Vector2(0,158), new Vector2(620,46), 32);
        Button(panel, "权限通过", new Vector2(-150,90), new Vector2(250,48), () => SetMode(ElectricGatePillar.PassageMode.Permission));
        Button(panel, "个体通过", new Vector2(150,90), new Vector2(250,48), () => SetMode(ElectricGatePillar.PassageMode.Individual));
        modeText = Label(panel, "", new Vector2(0,38), new Vector2(630,38), 22);
        permissionControls = new GameObject("Permission controls", typeof(RectTransform)); permissionControls.transform.SetParent(panel,false);
        Button(permissionControls.transform, "◀", new Vector2(-155,-20), new Vector2(80,56), () => ChangeLevel(-1));
        valueText = Label(permissionControls.transform, "", new Vector2(0,-20), new Vector2(160,56), 40);
        Button(permissionControls.transform, "▶", new Vector2(155,-20), new Vector2(80,56), () => ChangeLevel(1));
        individualControls = new GameObject("Individual controls", typeof(RectTransform)); individualControls.transform.SetParent(panel,false);
        Button(individualControls.transform, "登记当前角色", new Vector2(-95,-20), new Vector2(270,56), () => {
            if (actor == null || actor.IsDead || actor.IsGhostLike) return;
            draft.individualId = actor.IndividualAccessId; draft.individualName = actor.gameObject.name; Refresh();
        });
        Button(individualControls.transform, "清除", new Vector2(170,-20), new Vector2(140,56), () => {
            draft.individualId = draft.individualName = string.Empty; Refresh();
        });
        statusText = Label(panel, "", new Vector2(0,-83), new Vector2(650,54), 20);
        Button(panel, "[E] 保存", new Vector2(-155,-151), new Vector2(230,46), () => Close(true));
        Button(panel, "[Esc] 取消", new Vector2(155,-151), new Vector2(230,46), () => Close(false));
        LateUpdate();
    }
    private void SetMode(ElectricGatePillar.PassageMode mode) { draft.mode = mode; Refresh(); }
    private void ChangeLevel(int delta) { draft.minimumPermission = Mathf.Clamp(draft.minimumPermission + delta,-2,9); Refresh(); }
    private void Refresh()
    {
        bool individual = draft.mode == ElectricGatePillar.PassageMode.Individual;
        permissionControls.SetActive(!individual); individualControls.SetActive(individual);
        modeText.text = individual ? "个体通过 · 仅登记身份可通行" : "权限通过 · 允许权限大于等于所选等级";
        valueText.text = draft.minimumPermission.ToString();
        statusText.text = individual ? (string.IsNullOrEmpty(draft.individualId) ? "尚未登记角色" : "已登记：" + draft.individualName)
            : "最低权限：" + draft.minimumPermission + "    " + (owner.Powered ? "电源就绪" : "电池未安装或电量耗尽");
    }
    private RectTransform Box(Transform parent, string name, Vector2 position, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent,false);
        var rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(.5f,.5f);
        rect.sizeDelta = size; rect.anchoredPosition = position; go.GetComponent<Image>().color = color; return rect;
    }
    private Text Label(Transform parent, string text, Vector2 position, Vector2 size, int fontSize)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent,false);
        var rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(.5f,.5f); rect.sizeDelta = size; rect.anchoredPosition = position;
        var label = go.GetComponent<Text>(); label.font = font; label.fontSize = fontSize; label.alignment = TextAnchor.MiddleCenter;
        label.color = ZeldaUiPalette.Ghost; label.text = text; label.raycastTarget = false; return label;
    }
    private void Button(Transform parent, string text, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction click)
    {
        var rect = Box(parent,text,position,size,Color.white);
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = rect.GetComponent<Image>();
        var colors = button.colors; colors.normalColor = new Color(.035f,.14f,.22f); colors.highlightedColor = new Color(.08f,.32f,.43f);
        colors.selectedColor = colors.normalColor; colors.pressedColor = new Color(.12f,.4f,.5f); colors.fadeDuration = .08f; button.colors = colors;
        button.onClick.AddListener(click); Label(rect,text,Vector2.zero,size,24);
    }
    private void Close(bool save)
    {
        if (save && owner != null && CharacterSkillRequirements.Require(actor, CharacterSkillRequirements.ElectricGatePanel))
            owner.ApplyRuntimeState(draft);
        Restore(); gameObject.SetActive(false); Destroy(gameObject);
    }
    private void Restore()
    {
        if (restored) return; restored = true;
        Time.timeScale = previousTimeScale; Cursor.visible = cursorVisible; Cursor.lockState = cursorLock;
        if (active == this) active = null; closedFrame = Time.frameCount;
    }
    private void OnDisable() => Restore();
    private void OnDestroy() => Restore();
}
