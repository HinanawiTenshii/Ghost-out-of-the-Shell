using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>Distributes the selected potion, or a condition-gated configured effect, to scene areas.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(BoxCollider2D), typeof(CameraVisionStreamingExempt))]
[AddComponentMenu("Interaction/Liquid Injector (注液器)")]
public sealed class LiquidInjector : MonoBehaviour
{
    [Serializable] public sealed class InjectionEvent : UnityEvent<ZeldaCharacterData> { }
    [Serializable] public struct RuntimeState
    {
        public bool conditionSatisfied;
        public TemporaryCharacterEffectArea.EffectSettings injection;
        public bool recolorBubbles;
        public Color currentColor, injectionColor;
    }
    [Header("Connected Areas / 绑定的临时效果区域")]
    [SerializeField] private TemporaryCharacterEffectArea[] linkedAreas = new TemporaryCharacterEffectArea[0];
    [Header("Condition / 交互条件接口")]
    [Tooltip("只控制预设效果注入；选中有效药水时可直接注入。场景事件可调用 SetConditionSatisfied(bool)，不会自动注液或消耗物品。")]
    [SerializeField] private bool conditionSatisfied;
    [SerializeField] private string unavailableMessage = "注液条件尚未满足";
    [Header("Injection / 注入后应用的效果")]
    [SerializeField] private TemporaryCharacterEffectArea.EffectSettings injection = TemporaryCharacterEffectArea.EffectSettings.Healing;
    [SerializeField, Tooltip("关闭时保留各区域现有气泡颜色；开启时使用注入配置中的 Bubble Color。")]
    private bool recolorBubbles;
    [Header("Indicator / 可变色液体窗口")]
    [SerializeField] private Color currentEffectColor = new Color(.18f,.22f,.24f,1);
    [SerializeField] private Color injectionIndicatorColor = new Color(.18f,.22f,.24f,1);
    [Header("Interaction / 靠近按 E")]
    [SerializeField, Min(.1f)] private float interactionDistance = 1.5f;
    [SerializeField, Min(.1f)] private float interactionCooldown = .5f;
    [SerializeField] private Font promptFont;
    [SerializeField] private InjectionEvent onInjected = new InjectionEvent();
    [Header("Optional Potion Quest / 注入药水的任务记录")]
    [SerializeField, Tooltip("仅成功消耗药水并注入后记录完成条件。未接取时保留记录，正常触发任务后立即完成；预设效果和读档不产生新记录。配置后该装置也作为任务地图目标。")]
    private string potionInjectionQuestId = string.Empty;
    public string PotionInjectionQuestId => potionInjectionQuestId;
    private readonly HashSet<TemporaryCharacterEffectArea> targets = new HashSet<TemporaryCharacterEffectArea>();
    private float nextInteractionTime;
    private bool injecting;
    private TextMesh prompt;
    private Material promptMaterial;
    public bool ConditionSatisfied => conditionSatisfied;
    public Color CurrentEffectColor => currentEffectColor;
    private bool InputBlocked => Time.timeScale <= 0f || CharacterPotionBuffs.IsRestoringWorld || DocumentReader.IsInputBlocked || ClockworkPuppetRuntime.BlocksCharacterInput;

    // UnityEvent-compatible condition interface. Satisfying a condition never applies effects by itself.
    public void SetConditionSatisfied(bool satisfied) => conditionSatisfied = satisfied;
    public void SetIndicatorColor(Color color) { color.a = 1f; currentEffectColor = color; }
    public void ConfigureInjection(TemporaryCharacterEffectArea.EffectSettings settings, Color indicatorColor, bool changeBubbleColor = false)
    {
        injection = settings.Sanitized(); injectionIndicatorColor = indicatorColor; injectionIndicatorColor.a = 1f;
        recolorBubbles = changeBubbleColor;
    }
    public RuntimeState CaptureRuntimeState() => new RuntimeState {
        conditionSatisfied = conditionSatisfied, injection = injection, recolorBubbles = recolorBubbles,
        currentColor = currentEffectColor, injectionColor = injectionIndicatorColor
    };
    public void ApplyRuntimeState(RuntimeState state)
    {
        conditionSatisfied = state.conditionSatisfied;
        ConfigureInjection(state.injection, state.injectionColor, state.recolorBubbles);
        SetIndicatorColor(state.currentColor); nextInteractionTime = 0f;
        // Areas restore their own state; never re-inject or invoke gameplay events during load.
    }
    private ZeldaFourWayMover Nearby()
    {
        var mover = ZeldaRuntimeRegistry.GetControlledMover();
        if (mover == null || !mover.isActiveAndEnabled || ZeldaRuntimeRegistry.GetGameplayScene(mover.gameObject) != gameObject.scene ||
            Vector2.Distance(mover.transform.position, transform.position) > interactionDistance) return null;
        var data = mover.GetComponent<ZeldaCharacterData>();
        return data != null && !data.IsDead && !data.IsGhostLike ? mover : null;
    }
    private void Update()
    {
        var mover = InputBlocked || injecting || Time.time < nextInteractionTime ? null : Nearby();
        if (mover == null) { ShowPrompt(false); return; }
        ZeldaInteractionArbiter.OfferInteraction(this, mover, KeyCode.E, transform.position, ShowPrompt);
        if (Input.GetKeyDown(KeyCode.E)) ZeldaInteractionArbiter.Submit(this, mover, KeyCode.E, transform.position, Interact);
    }
    public bool TryInject() => TryInject(out _);
    public void Inject() => Interact();
    private void Interact()
    {
        TryInject(out string message);
        if (!string.IsNullOrEmpty(message)) ZeldaHealthHeartsUI.Instance?.ShowNotificationPopup(message);
    }
    private bool TryInject(out string reason)
    {
        reason = null;
        if (!isActiveAndEnabled || injecting || InputBlocked || Time.time < nextInteractionTime) return false;
        var mover = Nearby(); if (mover == null || mover.IsAttacking) return false;
        var inventory = PersistentInventory.Instance;
        bool hasPotion = TryGetSelectedPotion(inventory, out var potionKind, out string potionId);
        if (!hasPotion && !conditionSatisfied)
        {
            reason = "请在物品栏选中要注入的药水";
            if (!string.IsNullOrEmpty(unavailableMessage)) reason += "\n" + unavailableMessage;
            return false;
        }
        targets.Clear();
        if (linkedAreas != null) foreach (var area in linkedAreas)
            if (area != null && area.gameObject.scene == gameObject.scene) targets.Add(area);
        if (targets.Count == 0) { reason = "注液器尚未连接有效区域"; return false; }
        if (!mover.TryBeginItemAttack(.24f)) return false;
        injecting = true;
        try
        {
            if (hasPotion)
            {
                var settings = TemporaryCharacterEffectArea.EffectSettings.ForPotion(potionKind);
                // Commit inventory, areas and indicator before the inventory change/save
                // callbacks. Never ExecuteUse: the operator is not drinking this potion.
                bool exchanged = inventory.TryExchangeSelectedItem(potionId, GlassBottlePickupItem.DefaultItemId, () => {
                    ApplyToTargets(settings, settings.bubbleColor, true);
                    nextInteractionTime = Time.time + interactionCooldown;
                    // Commit completion/evidence with the inventory, even before the
                    // parent is accepted. Existing journal persistence retains the
                    // hidden evidence across travel/load until the normal read trigger.
                    if (!string.IsNullOrWhiteSpace(potionInjectionQuestId))
                        QuestJournalManager.GetOrCreate().RecordQuestCompletion(potionInjectionQuestId, true);
                });
                if (!exchanged)
                {
                    mover.EndItemAttack();
                    reason = "未能注入药水，请检查空瓶资源和物品栏空间；药水已保留";
                    return false;
                }
                reason = "已向绑定区域注入" + AlchemyRecipeCatalog.GetName(potionKind) + "，返还一个玻璃瓶";
            }
            else
            {
                ApplyToTargets(injection, injectionIndicatorColor, recolorBubbles);
                nextInteractionTime = Time.time + interactionCooldown;
                reason = "已向绑定区域注液";
            }
            onInjected.Invoke(mover.GetComponent<ZeldaCharacterData>());
            return true;
        }
        finally { injecting = false; }
    }
    private static bool TryGetSelectedPotion(PersistentInventory inventory, out AlchemyPotionKind kind, out string itemId)
    {
        kind = default; itemId = null;
        var slot = inventory != null ? inventory.GetSlot(inventory.SelectedSlotIndex) : null;
        if (slot == null || slot.IsEmpty) return false;
        var potion = inventory.ResolveItemPrefab(slot.ItemId) as PotionPickupItem;
        // A prefix or parsed integer is not sufficient: require the authored potion
        // component, a real enum value, and its exact stable catalog ID.
        if (potion == null || !Enum.IsDefined(typeof(AlchemyPotionKind), potion.Kind) ||
            slot.ItemId != AlchemyRecipeCatalog.GetItemId(potion.Kind)) return false;
        kind = potion.Kind; itemId = slot.ItemId; return true;
    }
    private void ApplyToTargets(TemporaryCharacterEffectArea.EffectSettings settings, Color indicatorColor, bool changeBubbleColor)
    {
        foreach (var area in targets)
        {
            var areaSettings = settings;
            if (!changeBubbleColor) areaSettings.bubbleColor = area.CaptureEffectSettings().bubbleColor;
            area.ApplyEffectSettings(areaSettings);
            // Disabled areas receive configuration without being forcibly activated.
        }
        SetIndicatorColor(indicatorColor);
    }
    private void ShowPrompt(bool visible)
    {
        var mover = visible && !InputBlocked ? Nearby() : null;
        Font font = promptFont != null ? promptFont : ZeldaHealthHeartsUI.Instance?.PermissionLabelFont;
        if (mover == null || font == null)
        { if (prompt != null) prompt.gameObject.SetActive(false); return; }
        if (prompt == null)
        {
            var go = new GameObject("Liquid Injector Prompt",typeof(TextMesh)) { hideFlags = HideFlags.HideAndDontSave };
            go.transform.SetParent(transform,false); prompt = go.GetComponent<TextMesh>();
            prompt.font = font; prompt.fontSize = 72; prompt.characterSize = .035f;
            prompt.anchor = TextAnchor.MiddleCenter; prompt.alignment = TextAlignment.Center; prompt.color = ZeldaUiPalette.Primary;
            promptMaterial = new Material(font.material) { hideFlags = HideFlags.HideAndDontSave };
            var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterial = promptMaterial;
            renderer.sortingOrder = short.MaxValue-2; ZeldaPossessionProgressBar.ConfigureOverlayRenderer(renderer);
        }
        prompt.text = TryGetSelectedPotion(PersistentInventory.Instance, out var kind, out _)
            ? "按[E]注入" + AlchemyRecipeCatalog.GetName(kind)
            : conditionSatisfied ? "按[E]注入预设效果" : "选中药水后按[E]注液";
        font.RequestCharactersInTexture(prompt.text,72,FontStyle.Normal); promptMaterial.mainTexture = font.material.mainTexture;
        prompt.transform.position = mover.GetOverheadWorldPosition(new Vector2(0,.9f)); prompt.transform.rotation = Quaternion.identity;
        var scale = transform.lossyScale;
        prompt.transform.localScale = new Vector3(1f/Mathf.Max(.001f,Mathf.Abs(scale.x)),1f/Mathf.Max(.001f,Mathf.Abs(scale.y)),1f/Mathf.Max(.001f,Mathf.Abs(scale.z)));
        prompt.gameObject.SetActive(true);
    }
    private void OnValidate()
    {
        interactionDistance = Mathf.Max(.1f,interactionDistance); interactionCooldown = Mathf.Max(.1f,interactionCooldown);
        injection = injection.Sanitized(); currentEffectColor.a = injectionIndicatorColor.a = 1f;
    }
    private void OnDisable() { if (prompt != null) prompt.gameObject.SetActive(false); }
    private void OnDestroy()
    {
        if (prompt != null) Destroy(prompt.gameObject);
        if (promptMaterial != null) Destroy(promptMaterial);
    }
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(.85f,.22f,.18f,.7f);
        if (linkedAreas == null) return;
        foreach (var area in linkedAreas)
            if (area != null && area.gameObject.scene == gameObject.scene) Gizmos.DrawLine(transform.position, area.AreaCollider != null ? area.AreaCollider.bounds.center : area.transform.position);
    }
}
