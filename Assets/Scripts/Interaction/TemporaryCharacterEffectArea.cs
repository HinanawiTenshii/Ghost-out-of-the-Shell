using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>Temporary, overlap-scoped effects. Default: one health every 0.3 seconds while inside.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CameraVisionStreamingExempt))]
[AddComponentMenu("Interaction/Temporary Character Effect Area (临时效果区域)")]
public sealed class TemporaryCharacterEffectArea : MonoBehaviour
{
    [Serializable]
    public struct EffectSettings
    {
        public bool restoreHealth;
        [Min(1)] public int healthPerTick;
        [Min(.02f)] public float tickInterval;
        public Color bubbleColor;
        public bool applyPotion;
        public AlchemyPotionKind potionKind;
        public static EffectSettings Healing => new EffectSettings {
            restoreHealth = true, healthPerTick = 1, tickInterval = .3f, bubbleColor = new Color(.18f, .85f, .49f, 1)
        };
        public static EffectSettings ForPotion(AlchemyPotionKind kind) => new EffectSettings {
            applyPotion = true, potionKind = kind, restoreHealth = false,
            healthPerTick = 1, tickInterval = .3f, bubbleColor = AlchemyRecipeCatalog.GetColor(kind)
        }.Sanitized();
        public EffectSettings Sanitized()
        {
            var settings = this;
            settings.healthPerTick = Mathf.Max(1, healthPerTick);
            settings.tickInterval = float.IsNaN(tickInterval) || float.IsInfinity(tickInterval) ? .3f : Mathf.Max(.02f, tickInterval);
            if ((int)potionKind < 0 || (int)potionKind > (int)AlchemyPotionKind.Echo) settings.applyPotion = false;
            if (settings.applyPotion) settings.restoreHealth = false;
            return settings;
        }
    }
    [Serializable] public sealed class CharacterEvent : UnityEvent<ZeldaCharacterData> { }
    [Header("Trigger / 作用范围")]
    [Tooltip("本对象或子对象的 Trigger Collider2D；留空时自动寻找。本脚本不会把已有实体碰撞体改为 Trigger。")]
    [SerializeField] private Collider2D areaCollider;
    [Header("Recovery / 持续恢复")]
    [SerializeField, InspectorName("启用生命恢复")] private bool restoreHealth = true;
    [SerializeField, Min(1), InspectorName("每次恢复生命")] private int healthPerTick = 1;
    [SerializeField, Min(.02f), InspectorName("效果间隔（秒）")] private float tickInterval = .3f;
    [Header("Potion / 药水区域（进入时施加，离开后计时）")]
    [SerializeField, InspectorName("启用药水效果")] private bool applyPotion;
    [SerializeField, InspectorName("药水类型")] private AlchemyPotionKind potionKind;
    [Header("Cauldron Bubbles / 炼药锅气泡")]
    [SerializeField] private bool animateBubbles = true;
    [SerializeField] private Color bubbleColor = new Color(.18f, .85f, .49f, 1);
    [SerializeField] private Vector2 bubbleRadiusRange = new Vector2(.06875f, .10625f);
    [SerializeField] private Vector2 bubbleGrowthTime = new Vector2(.8f, 1.35f);
    [SerializeField, Min(.05f)] private float bubbleInterval = .3f;
    [SerializeField, Min(.05f)] private float bubblePopDuration = .22f;
    [SerializeField] private string bubbleSortingLayer = "Default";
    [SerializeField] private int bubbleSortingOrder = -2;
    [Header("Extension Events / 其他效果接口（参数为对应角色）")]
    [SerializeField] private CharacterEvent onCharacterEntered = new CharacterEvent();
    [SerializeField] private CharacterEvent onEffectTick = new CharacterEvent();
    [SerializeField] private CharacterEvent onCharacterExited = new CharacterEvent();

    private sealed class Occupant
    {
        public float elapsed;
        public int appliedRevision = -1;
        public Matrix4x4 bodyPose, areaPose;
    }
    private readonly Dictionary<ZeldaCharacterData, Occupant> occupants = new Dictionary<ZeldaCharacterData, Occupant>();
    private readonly List<Collider2D> hits = new List<Collider2D>(16);
    private readonly HashSet<ZeldaCharacterData> seen = new HashSet<ZeldaCharacterData>();
    private readonly List<ZeldaCharacterData> targets = new List<ZeldaCharacterData>(8);
    private readonly List<ZeldaCharacterData> departed = new List<ZeldaCharacterData>(8);
    private TriggerAreaBubbleEffect bubbles;
    private int effectRevision;
    public Collider2D AreaCollider { get { ResolveCollider(); return areaCollider; } }
    public int OccupantCount => occupants.Count;
    public bool AppliesPotion => applyPotion;
    public AlchemyPotionKind PotionKind => potionKind;
    public bool CanSustainPotion(ZeldaCharacterData character) => isActiveAndEnabled && AreaReady && applyPotion &&
        character != null && !character.IsDead && !character.IsGhostLike && occupants.ContainsKey(character) &&
        ZeldaRuntimeRegistry.GetGameplayScene(character.gameObject) == gameObject.scene;
    /// <summary>Used once when restoring saved membership, before the first physics tick.</summary>
    public bool IsPotionOverlapping(ZeldaCharacterData character)
    {
        ResolveCollider();
        if (!isActiveAndEnabled || !AreaReady || !applyPotion || character == null || character.IsDead || character.IsGhostLike ||
            ZeldaRuntimeRegistry.GetGameplayScene(character.gameObject) != gameObject.scene) return false;
        if (!character.gameObject.activeInHierarchy)
        {
            if (!HasSuspendedContact(character)) return false;
            foreach (var manager in FindObjectsOfType<CameraVisionObjectStreaming>())
                if (manager.IsSuspendedByStreamingOrAncestor(character.gameObject)) return true;
            return false;
        }
        Physics2D.SyncTransforms();
        foreach (var body in character.GetComponentsInChildren<Collider2D>())
            if (body.enabled && !body.isTrigger && body.gameObject.activeInHierarchy && areaCollider.Distance(body).isOverlapped)
                return true;
        return false;
    }
    private bool HasSuspendedContact(ZeldaCharacterData character)
    {
        // Preserve the last real shape overlap, including characters whose pivot is
        // outside the trigger while their body still touches its edge.
        if (occupants.TryGetValue(character, out var occupant) &&
            occupant.bodyPose == character.transform.localToWorldMatrix &&
            occupant.areaPose == areaCollider.transform.localToWorldMatrix) return true;
        return areaCollider.OverlapPoint(character.transform.position);
    }
    public float TickInterval => Mathf.Max(.02f, tickInterval);
    public EffectSettings CaptureEffectSettings() => new EffectSettings {
        restoreHealth = restoreHealth, healthPerTick = healthPerTick, tickInterval = tickInterval, bubbleColor = bubbleColor,
        applyPotion = applyPotion, potionKind = potionKind
    };
    public void ApplyEffectSettings(EffectSettings settings)
    {
        settings = settings.Sanitized();
        restoreHealth = settings.restoreHealth; healthPerTick = settings.healthPerTick;
        tickInterval = settings.tickInterval; bubbleColor = settings.bubbleColor;
        applyPotion = settings.applyPotion; potionKind = settings.potionKind;
        // On load the next physics query rebinds saved occupancy without applying an
        // instantaneous potion a second time. Characters also restore their own state.
        if (CharacterPotionBuffs.IsRestoringWorld) ClearOccupants(false);
        // Already-present actors receive the new effect without synthetic exit/enter
        // events or a leftover timer immediately firing the replacement effect.
        foreach (var occupant in occupants.Values) occupant.elapsed = 0f;
        effectRevision++;
    }

    private bool AreaReady => areaCollider != null && areaCollider.enabled && areaCollider.isTrigger &&
        areaCollider.gameObject.activeInHierarchy && areaCollider.transform.IsChildOf(transform);
    private void Reset()
    {
        ResolveCollider();
        if (areaCollider == null)
        {
            var box = gameObject.AddComponent<BoxCollider2D>();
            box.size = new Vector2(3f, 3f); box.isTrigger = true; areaCollider = box;
        }
    }
    private void ResolveCollider()
    {
        if (areaCollider != null) return;
        // Prefer a trigger when the same object also has a solid body collider.
        foreach (var candidate in GetComponentsInChildren<Collider2D>(true))
        {
            if (candidate.isTrigger) { areaCollider = candidate; return; }
            if (areaCollider == null) areaCollider = candidate;
        }
    }
    private void Awake()
    {
        ResolveCollider();
        if (!AreaReady) Debug.LogWarning("临时效果区域需要本对象或子对象上启用的 Trigger Collider2D。", this);
    }
    private void FixedUpdate()
    {
        if (Time.timeScale <= 0f || CharacterPotionBuffs.IsRestoringWorld) return;
        TickEffects(Time.fixedDeltaTime);
    }
    private void TickEffects(float deltaTime)
    {
        if (CharacterPotionBuffs.IsRestoringWorld) return;
        ResolveCollider();
        if (!isActiveAndEnabled || !AreaReady) { ClearOccupants(); return; }
        if (deltaTime <= 0f) return;
        // Query the real collider, not its rectangular bounds. This also handles
        // loads/teleports into the area, multiple body colliders and missing exit callbacks.
        var filter = new ContactFilter2D { useTriggers = false };
        Physics2D.SyncTransforms();
        hits.Clear(); areaCollider.OverlapCollider(filter, hits);
        seen.Clear(); targets.Clear();
        foreach (var hit in hits)
        {
            if (hit == null || !hit.enabled || !hit.gameObject.activeInHierarchy || hit.transform.IsChildOf(transform)) continue;
            var character = hit.GetComponentInParent<ZeldaCharacterData>();
            if (!Eligible(character) || !seen.Add(character)) continue;
            targets.Add(character);
        }
        // Streaming suspension is not walking out of a zone. Retain its membership
        // while the stationary body is unloaded, without waking it or repeating healing.
        CameraVisionObjectStreaming[] streaming = null;
        foreach (var pair in occupants)
        {
            var character = pair.Key;
            if (character == null || character.gameObject.activeInHierarchy || character.IsDead || character.IsGhostLike ||
                ZeldaRuntimeRegistry.GetGameplayScene(character.gameObject) != gameObject.scene ||
                !HasSuspendedContact(character)) continue;
            if (streaming == null) streaming = FindObjectsOfType<CameraVisionObjectStreaming>();
            foreach (var manager in streaming)
                if (manager.IsSuspendedByStreamingOrAncestor(character.gameObject))
                {
                    seen.Add(character);
                    // Reconfiguration must also update a streamed occupant once,
                    // but ordinary per-tick healing must remain suspended.
                    if (pair.Value.appliedRevision != effectRevision) targets.Add(character);
                    break;
                }
        }
        departed.Clear();
        foreach (var pair in occupants) if (!seen.Contains(pair.Key)) departed.Add(pair.Key);
        foreach (var character in departed)
        {
            occupants.Remove(character);
            CharacterPotionBuffs.ExitPotionArea(character, this);
            if (character != null) onCharacterExited.Invoke(character);
            if (!isActiveAndEnabled || !AreaReady) return;
        }
        foreach (var character in targets)
        {
            bool suspended = character != null && !character.gameObject.activeInHierarchy && seen.Contains(character) && occupants.ContainsKey(character);
            if (!Eligible(character) && !suspended) continue;
            if (!occupants.TryGetValue(character, out var occupant))
            {
                occupant = new Occupant(); occupants.Add(character, occupant);
                onCharacterEntered.Invoke(character);
            }
            if (!isActiveAndEnabled || !AreaReady) return;
            if (!Eligible(character) && !suspended) continue;
            occupant.bodyPose = character.transform.localToWorldMatrix;
            occupant.areaPose = areaCollider.transform.localToWorldMatrix;
            if (occupant.appliedRevision != effectRevision)
            {
                bool reconfigured = occupant.appliedRevision >= 0;
                occupant.appliedRevision = effectRevision;
                if (applyPotion) CharacterPotionBuffs.EnterPotionArea(character, this, potionKind, reconfigured);
                else CharacterPotionBuffs.ExitPotionArea(character, this);
                if (!isActiveAndEnabled || !AreaReady) return;
                if (!Eligible(character)) continue;
            }
            if (suspended) continue;
            occupant.elapsed += deltaTime;
            float interval = TickInterval;
            int ticks = Mathf.Min(32, Mathf.FloorToInt((occupant.elapsed + .000001f) / interval));
            if (ticks == 0) continue;
            occupant.elapsed = Mathf.Max(0f, occupant.elapsed - ticks * interval);
            int revision = effectRevision;
            for (int i = 0; i < ticks; i++)
            {
                if (!Eligible(character) || revision != effectRevision) break;
                if (restoreHealth && !applyPotion) character.RestoreHealth(Mathf.Max(1, healthPerTick));
                onEffectTick.Invoke(character);
                if (!isActiveAndEnabled || !AreaReady) return;
            }
        }
    }
    private bool Eligible(ZeldaCharacterData character)
    {
        return character != null && character.isActiveAndEnabled && !character.IsDead && !character.IsGhostLike &&
            ZeldaRuntimeRegistry.GetGameplayScene(character.gameObject) == gameObject.scene;
    }
    private void ClearOccupants(bool notify = true)
    {
        if (occupants.Count == 0) return;
        // Clear before callbacks, so callbacks can disable or destroy the zone safely.
        var leaving = new List<ZeldaCharacterData>(occupants.Keys);
        occupants.Clear();
        foreach (var character in leaving)
        {
            CharacterPotionBuffs.ExitPotionArea(character, this);
            if (notify && character != null) onCharacterExited.Invoke(character);
        }
    }
    private void Update()
    {
        ResolveCollider();
        if (!animateBubbles || !AreaReady)
        {
            bubbles?.Clear(); return;
        }
        if (CharacterPotionBuffs.IsRestoringWorld || Time.timeScale <= 0f || Time.deltaTime <= 0f) return;
        if (bubbles == null) bubbles = new TriggerAreaBubbleEffect(transform);
        bubbles.Tick(areaCollider, Time.deltaTime, bubbleColor, bubbleRadiusRange, bubbleGrowthTime,
            bubbleInterval, bubblePopDuration, SortingLayer.NameToID(bubbleSortingLayer), bubbleSortingOrder);
    }
    private void OnDisable()
    {
        ClearOccupants(); bubbles?.Dispose(); bubbles = null;
    }
    private void OnDestroy()
    {
        ClearOccupants(); bubbles?.Dispose(); bubbles = null;
    }
    private void OnValidate()
    {
        healthPerTick = Mathf.Max(1, healthPerTick); tickInterval = Mathf.Max(.02f, tickInterval);
        bubbleRadiusRange.x = Mathf.Max(.01f, bubbleRadiusRange.x);
        bubbleRadiusRange.y = Mathf.Max(bubbleRadiusRange.x, bubbleRadiusRange.y);
        bubbleGrowthTime.x = Mathf.Max(.1f, bubbleGrowthTime.x);
        bubbleGrowthTime.y = Mathf.Max(bubbleGrowthTime.x, bubbleGrowthTime.y);
        bubbleInterval = Mathf.Max(.05f, bubbleInterval); bubblePopDuration = Mathf.Max(.05f, bubblePopDuration);
    }
}
