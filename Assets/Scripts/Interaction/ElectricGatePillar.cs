using System;
using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(BoxCollider2D), typeof(CameraVisionStreamingExempt))]
public sealed class ElectricGatePillar : MonoBehaviour
{
    public enum PanelSide { Down, Up, Left, Right }
    public enum PassageMode { Permission, Individual }
    [Serializable] public struct RuntimeState
    {
        public PassageMode mode;
        public int minimumPermission;
        public string individualId;
        public string individualName;
    }

    [SerializeField, InspectorName("安装控制面板")] private bool hasPanel = true;
    [SerializeField, InspectorName("面板所在面")] private PanelSide panelSide;
    [SerializeField, InspectorName("通过模式")] private PassageMode passageMode;
    [SerializeField, Range(-2, 9), InspectorName("最低通行权限")] private int minimumPermission = 3;
    [SerializeField, Min(.3f)] private float interactionDistance = 1.5f;
    [SerializeField] private Font panelFont;
    private string individualId, individualName;
    private TextMesh prompt;
    private Material promptMaterial;
    public bool HasPanel => hasPanel;
    public PanelSide Side => panelSide;
    public PassageMode Mode => passageMode;
    public int MinimumPermission => minimumPermission;
    public bool Powered => !hasPanel || BatterySocket.IsPowered(this);
    public ElectricGateRules.SignalState Signal { get; private set; }
    internal void ResetSignal() => Signal = ElectricGateRules.SignalState.Off;
    internal void ReportSignal(ElectricGateRules.SignalState state) => Signal = ElectricGateRules.MergeSignal(Signal, state);
    public Vector2 PanelDirection => panelSide == PanelSide.Up ? Vector2.up : panelSide == PanelSide.Left ? Vector2.left
        : panelSide == PanelSide.Right ? Vector2.right : Vector2.down;
    public Vector2 CablePort => transform.TransformPoint(PanelDirection * .61f);
    public Vector2 CableDirection
    {
        get
        {
            Vector2 d = transform.TransformVector(PanelDirection);
            return Mathf.Abs(d.x) > Mathf.Abs(d.y) ? new Vector2(Mathf.Sign(d.x), 0) : new Vector2(0, Mathf.Sign(d.y));
        }
    }

    private void Awake()
    {
        ZeldaRequirementWindow.AttachSkillRequirement(this, CharacterSkillRequirements.ElectricGatePanel);
        MapPointOfInterest.AttachMechanism(this);
    }
    private void OnEnable() { ElectricGateNetwork.Register(this); }
    private void OnDisable() { ResetSignal(); ElectricGateNetwork.Unregister(this); HidePrompt(); ElectricGatePanel.CloseFor(this); }
    public bool Allows(ZeldaCharacterData data)
    {
        return data != null && !data.IsGhostLike && ElectricGateRules.Allows(passageMode == PassageMode.Individual,
            minimumPermission, individualId, data.PermissionLevel, data.IndividualAccessId);
    }
    public RuntimeState CaptureRuntimeState() => new RuntimeState {
        mode = passageMode, minimumPermission = minimumPermission, individualId = individualId, individualName = individualName };
    public void ApplyRuntimeState(RuntimeState state)
    {
        passageMode = state.mode == PassageMode.Individual ? PassageMode.Individual : PassageMode.Permission;
        minimumPermission = Mathf.Clamp(state.minimumPermission, -2, 9);
        individualId = state.individualId; individualName = state.individualName;
    }

    private ZeldaFourWayMover Nearby()
    {
        if (!hasPanel || DocumentReader.IsInputBlocked || Time.timeScale <= 0 || ClockworkPuppetRuntime.BlocksCharacterInput) return null;
        var mover = ZeldaRuntimeRegistry.GetControlledMover();
        if (mover == null || !mover.isActiveAndEnabled || ZeldaRuntimeRegistry.GetGameplayScene(mover.gameObject) != gameObject.scene
            || Vector2.Distance(mover.transform.position, CablePort) > interactionDistance) return null;
        var data = mover.GetComponent<ZeldaCharacterData>();
        return data != null && !data.IsGhostLike && !data.IsDead ? mover : null;
    }
    private void Update()
    {
        var mover = Nearby();
        if (mover == null) { HidePrompt(); return; }
        ZeldaInteractionArbiter.OfferInteraction(this, mover, KeyCode.E, CablePort, ShowPrompt);
        if (Input.GetKeyDown(KeyCode.E)) ZeldaInteractionArbiter.Submit(this, mover, KeyCode.E, CablePort, OpenPanel);
    }
    private void OpenPanel()
    {
        var mover = Nearby();
        if (mover == null) return;
        HidePrompt();
        ElectricGatePanel.Open(this, mover.GetComponent<ZeldaCharacterData>(), panelFont);
    }
    private void ShowPrompt(bool visible)
    {
        var mover = visible ? Nearby() : null;
        if (mover == null) { HidePrompt(); return; }
        Font font = panelFont != null ? panelFont : ZeldaHealthHeartsUI.Instance?.PermissionLabelFont;
        if (font == null) return;
        if (prompt == null)
        {
            var go = new GameObject("Electric Gate Prompt", typeof(TextMesh)) { hideFlags = HideFlags.HideAndDontSave };
            go.transform.SetParent(transform, false); prompt = go.GetComponent<TextMesh>();
            prompt.font = font; prompt.fontSize = 72; prompt.characterSize = .035f;
            prompt.anchor = TextAnchor.MiddleCenter; prompt.color = ZeldaUiPalette.Primary;
            promptMaterial = new Material(font.material) { hideFlags = HideFlags.HideAndDontSave };
            var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterial = promptMaterial;
            renderer.sortingOrder = short.MaxValue - 2; ZeldaPossessionProgressBar.ConfigureOverlayRenderer(renderer);
        }
        prompt.text = CharacterSkillRequirements.Has(mover.CharacterData, CharacterSkillRequirements.ElectricGatePanel)
            ? "按[E]设置电门" : "能力值不足";
        font.RequestCharactersInTexture(prompt.text, 72, FontStyle.Normal); promptMaterial.mainTexture = font.material.mainTexture;
        prompt.transform.position = mover.GetOverheadWorldPosition(new Vector2(0, .9f));
        prompt.transform.rotation = Quaternion.identity; prompt.gameObject.SetActive(true);
    }
    private void HidePrompt() { if (prompt != null) prompt.gameObject.SetActive(false); }
    private void OnDestroy() { if (promptMaterial != null) Destroy(promptMaterial); }
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1, .2f, .15f, .35f); Gizmos.DrawWireSphere(transform.position, ElectricGateRules.Range);
        if (hasPanel) { Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(CablePort, .08f); }
    }
}
