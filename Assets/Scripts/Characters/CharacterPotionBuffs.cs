using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Temporary, body-owned potion modifiers. Authored character stats are never overwritten.
/// Drinking Spirit uses the player reserve; NPC splash reserves belong to the body.
/// </summary>
[DisallowMultipleComponent]
public sealed class CharacterPotionBuffs : MonoBehaviour
{
    [Serializable]
    public struct RuntimeState
    {
        public float[] remaining;
        public float[] ordinaryDurations;
        public Vector3 echoPosition;
        public int echoHealth;
        public string echoScene;
        public float immunityRemaining;
        public bool hasAreaPotion;
        public AlchemyPotionKind areaKind;
        public float areaRemaining;
        public bool areaFrozen;
        public string[] areaSourceIds;
        public int[] areaSourceKinds;
        public Vector3 areaEchoPosition;
        public int areaEchoHealth;
        public string areaEchoScene;
        public bool latestEchoIsArea;
        public string areaSpiritToken;
        public int bodySpiritCurrent;
    }

    private const int KindCount = 10;
    private readonly float[] expiresAt = new float[KindCount];
    private readonly float[] ordinaryDurations = new float[KindCount];
    private ZeldaCharacterData character;
    private int previousMask;
    private int bodySpiritCurrent;
    public int BodySpiritMaximum => character != null && !character.IsDead &&
        OrdinaryRemaining(AlchemyPotionKind.Spirit) > 0f ? 2 : 0;
    public int BodySpiritCurrent => BodySpiritMaximum > 0 ? bodySpiritCurrent : 0;
    public int RestoreBodySpirit(int amount)
    {
        int restored = Mathf.Min(Mathf.Max(0, amount), BodySpiritMaximum - BodySpiritCurrent);
        bodySpiritCurrent = BodySpiritCurrent + restored;
        return restored;
    }
    public int SpendBodySpirit(int amount)
    {
        int spent = Mathf.Min(Mathf.Max(0, amount), BodySpiritCurrent);
        bodySpiritCurrent = BodySpiritCurrent - spent;
        return spent;
    }
    private Vector3 echoPosition;
    private int echoHealth;
    private string echoScene;
    private float immuneUntil;
    // Drinking and areas have independent ownership. Replacing an area never
    // erases an unrelated drink, skill, or authored character attribute.
    private readonly Dictionary<TemporaryCharacterEffectArea, AlchemyPotionKind> areaSources =
        new Dictionary<TemporaryCharacterEffectArea, AlchemyPotionKind>();
    private readonly Dictionary<string, AlchemyPotionKind> restoredAreaSources =
        new Dictionary<string, AlchemyPotionKind>();
    private readonly List<TemporaryCharacterEffectArea> staleAreaSources = new List<TemporaryCharacterEffectArea>();
    private bool hasAreaPotion, areaFrozen, restoreAreaOverlaps, latestEchoIsArea;
    private AlchemyPotionKind areaKind;
    private float areaExpiresAt, areaFrozenRemaining;
    private Vector3 areaEchoPosition;
    private int areaEchoHealth;
    private string areaEchoScene, areaSpiritToken;
    public float ImmunityRemaining => Mathf.Max(0f, immuneUntil - Time.time);
    public float AreaPotionRemaining => !hasAreaPotion ||
        (areaKind == AlchemyPotionKind.Echo && areaEchoScene != SceneKey(gameObject)) ? 0f :
        (areaFrozen ? areaFrozenRemaining : Mathf.Max(0f, areaExpiresAt - Time.time));
    public bool IsAreaPotionFrozen => areaFrozen && AreaPotionRemaining > 0f;
    public bool OwnsAreaPotionEnergy(string token) => !string.IsNullOrEmpty(token) && token == areaSpiritToken;
    private static readonly Collider2D[] overlaps = new Collider2D[64];

    public static bool IsRestoringWorld => GameSaveSystem.IsLoading ||
        (SceneTravelStateManager.Instance != null &&
            (SceneTravelStateManager.Instance.IsQuickRestartPending ||
             SceneTravelStateManager.Instance.IsSceneTravelRestoreInProgress));

    public static float Duration(AlchemyPotionKind kind) => AlchemyRecipeCatalog.GetDuration(kind);

    public static bool Has(ZeldaCharacterData user, AlchemyPotionKind kind)
    {
        if (user == null || user.IsDead) return false;
        if (kind == AlchemyPotionKind.Spirit)
            return (user.GetComponent<CharacterPotionBuffs>()?.BodySpiritMaximum ?? 0) > 0 ||
                (IsControlled(user) && PlayerGrowthAttributes.Instance != null &&
                 PlayerGrowthAttributes.Instance.PotionEnergyMaximum > 0);
        var buffs = user.GetComponent<CharacterPotionBuffs>();
        return buffs != null && buffs.Remaining(kind) > 0f;
    }

    public float Remaining(AlchemyPotionKind kind)
    {
        int index = (int)kind;
        if (index < 0 || index >= KindCount) return 0f;
        return Mathf.Max(OrdinaryRemaining(kind), hasAreaPotion && areaKind == kind ? AreaPotionRemaining : 0f);
    }

    private float OrdinaryRemaining(AlchemyPotionKind kind) =>
        kind == AlchemyPotionKind.Echo && echoScene != SceneKey(gameObject) ? 0f :
        Mathf.Max(0f, expiresAt[(int)kind] - Time.time);

    /// <summary>The displayed timer and its own duration; doubled areas must not use a drink denominator.</summary>
    public float DisplayRemaining(AlchemyPotionKind kind, out float duration)
    {
        float ordinary = OrdinaryRemaining(kind);
        float area = hasAreaPotion && areaKind == kind ? AreaPotionRemaining : 0f;
        bool displayArea = area > 0f && (areaFrozen || area >= ordinary);
        duration = displayArea ? Duration(kind) * 2f : Mathf.Max(Duration(kind), ordinaryDurations[(int)kind]);
        return displayArea ? area : ordinary;
    }

    public static void EnterPotionArea(ZeldaCharacterData user, TemporaryCharacterEffectArea source,
        AlchemyPotionKind kind, bool refreshExisting = false)
    {
        if (IsRestoringWorld || user == null || source == null || user.IsDead || user.IsGhostLike ||
            (int)kind < 0 || (int)kind >= KindCount) return;
        bool controlled = IsControlled(user);
        // Creating the player service can notify bootstrap listeners. Do it before
        // recording a new membership, so a save callback cannot capture half a grant.
        var growth = kind == AlchemyPotionKind.Spirit && controlled
            ? PlayerGrowthAttributes.GetOrCreate() : PlayerGrowthAttributes.Instance;
        var buffs = user.GetComponent<CharacterPotionBuffs>();
        if (buffs == null) buffs = user.gameObject.AddComponent<CharacterPotionBuffs>();
        buffs.character = user;
        string sourceId = SourceKey(source);
        bool restored = buffs.restoredAreaSources.TryGetValue(sourceId, out var savedKind) && savedKind == kind;
        bool existing = buffs.areaSources.TryGetValue(source, out var previousKind) && previousKind == kind;
        buffs.restoredAreaSources.Remove(sourceId);
        buffs.areaSources[source] = kind;
        if (!refreshExisting && (restored || existing))
        {
            buffs.RefreshAreaFreeze();
            return;
        }

        bool sameActive = buffs.hasAreaPotion && buffs.areaKind == kind && buffs.AreaPotionRemaining > 0f;
        bool healHealth = kind == AlchemyPotionKind.Vitality && user.CurrentHealth < user.Health;
        bool healEnergy = kind == AlchemyPotionKind.Spirit && controlled &&
            user.FinalPossessionEnergy < user.MaxPossessionEnergy;
        bool changedEnergy = controlled || !string.IsNullOrEmpty(buffs.areaSpiritToken);
        if (!sameActive) buffs.ClearAreaTimer(false);
        buffs.hasAreaPotion = true;
        buffs.areaKind = kind;
        // Any new player-owned area supersedes the previous area reserve, including
        // a reserve carried across possession; ordinary Spirit drinking is separate.
        if (controlled && kind != AlchemyPotionKind.Spirit && growth != null)
            growth.ClearAreaPotionEnergy(null, false);
        bool timed = kind != AlchemyPotionKind.Death;
        if (kind == AlchemyPotionKind.Spirit) timed = controlled && (sameActive || !healEnergy);
        else if (healHealth) timed = sameActive;
        buffs.areaFrozenRemaining = timed ? Duration(kind) * 2f : 0f;
        buffs.areaExpiresAt = Time.time + buffs.areaFrozenRemaining;
        buffs.areaFrozen = false;
        if (kind == AlchemyPotionKind.Echo)
        {
            buffs.areaEchoPosition = user.transform.position;
            buffs.areaEchoHealth = user.CurrentHealth;
            buffs.areaEchoScene = SceneKey(user.gameObject);
            buffs.latestEchoIsArea = true;
        }
        buffs.RefreshAreaFreeze(false);
        if (kind == AlchemyPotionKind.Spirit && timed)
        {
            if (string.IsNullOrEmpty(buffs.areaSpiritToken)) buffs.areaSpiritToken = Guid.NewGuid().ToString("N");
            growth.RefreshAreaPotionEnergy(buffs.areaSpiritToken, buffs, Duration(kind) * 2f, false);
            buffs.SynchronizeSpirit();
        }
        buffs.previousMask = buffs.ActiveMask();
        // Timers, membership, anchors and global reserve are fully staged before
        // any ValuesChanged callback can save the effect or reenter this API.
        if (kind == AlchemyPotionKind.Death) user.KillFromPotion();
        else if (healHealth) user.RestoreHealth(6);
        else if (healEnergy) user.RestorePossessionEnergy(4);
        if (growth != null && changedEnergy) growth.NotifyPotionEnergyChanged();
        user.RefreshPotionDerivedAttributes();
        if (!user.IsDead) PotionPerceptionEffects.Ensure(user);
    }

    public static void ExitPotionArea(ZeldaCharacterData user, TemporaryCharacterEffectArea source)
    {
        var buffs = user != null ? user.GetComponent<CharacterPotionBuffs>() : null;
        if (buffs == null || source == null) return;
        // Loading clears the area's runtime occupants. Saved memberships, restored
        // before or after that reset, must survive until physics has rebound them.
        if (IsRestoringWorld) return;
        buffs.areaSources.Remove(source);
        buffs.restoredAreaSources.Remove(SourceKey(source));
        buffs.RefreshAreaFreeze();
    }

    private static string SourceKey(TemporaryCharacterEffectArea source) =>
        SceneKey(source.gameObject) + "|" + SceneTravelStateManager.GetMapObjectId(source.transform);

    private void ClearAreaTimer(bool notify = true)
    {
        if (!string.IsNullOrEmpty(areaSpiritToken) && PlayerGrowthAttributes.Instance != null)
            PlayerGrowthAttributes.Instance.ClearAreaPotionEnergy(areaSpiritToken, notify);
        areaSpiritToken = null;
        areaExpiresAt = areaFrozenRemaining = 0f;
        areaFrozen = false;
    }

    private void RefreshAreaFreeze(bool synchronizeSpirit = true)
    {
        bool frozen = false;
        foreach (var pair in areaSources)
            if (pair.Value == areaKind && pair.Key != null &&
                (pair.Key.CanSustainPotion(character) || pair.Key.IsPotionOverlapping(character)))
            { frozen = true; break; }
        if (!frozen && restoreAreaOverlaps)
            foreach (var kind in restoredAreaSources.Values)
                if (kind == areaKind) { frozen = true; break; }
        if (frozen != areaFrozen)
        {
            float remaining = AreaPotionRemaining;
            areaFrozenRemaining = remaining;
            areaExpiresAt = Time.time + remaining;
            areaFrozen = frozen;
        }
        if (synchronizeSpirit) SynchronizeSpirit();
    }

    private void SynchronizeSpirit()
    {
        if (!string.IsNullOrEmpty(areaSpiritToken) && PlayerGrowthAttributes.Instance != null)
            PlayerGrowthAttributes.Instance.SynchronizeAreaPotionEnergy(areaSpiritToken, this,
                AreaPotionRemaining, areaFrozen);
    }

    private void ReconcileAreaSources()
    {
        if (restoreAreaOverlaps)
        {
            foreach (var source in FindObjectsOfType<TemporaryCharacterEffectArea>(true))
            {
                string key = SourceKey(source);
                if (restoredAreaSources.TryGetValue(key, out var kind) && source.AppliesPotion &&
                    source.PotionKind == kind && source.IsPotionOverlapping(character))
                    areaSources[source] = kind;
            }
            restoredAreaSources.Clear();
            restoreAreaOverlaps = false;
        }
        staleAreaSources.Clear();
        foreach (var pair in areaSources)
            if (pair.Key == null || !pair.Key.AppliesPotion || pair.Key.PotionKind != pair.Value ||
                (!pair.Key.CanSustainPotion(character) && !pair.Key.IsPotionOverlapping(character)))
                staleAreaSources.Add(pair.Key);
        foreach (var source in staleAreaSources) areaSources.Remove(source);
        RefreshAreaFreeze();
    }

    public static int HealthModifier(ZeldaCharacterData user) =>
        (Has(user, AlchemyPotionKind.Vitality) ? 3 : 0) -
        (Has(user, AlchemyPotionKind.Weakness) ? 3 : 0);

    public static int AttackBonus(ZeldaCharacterData user) =>
        (Has(user, AlchemyPotionKind.Might) ? 3 : 0) +
        (Has(user, AlchemyPotionKind.Berserk) ? 2 : 0);

    public static float SpeedMultiplier(ZeldaCharacterData user) =>
        (Has(user, AlchemyPotionKind.Veil) ? 1.4f : 1f) *
        (Has(user, AlchemyPotionKind.Berserk) ? .8f : 1f);

    public static float AttackIntervalMultiplier(ZeldaCharacterData user) =>
        Has(user, AlchemyPotionKind.Berserk) ? .65f : 1f;

    public static bool IsInvulnerable(ZeldaCharacterData user)
    {
        var buffs = user != null ? user.GetComponent<CharacterPotionBuffs>() : null;
        return buffs != null && buffs.immuneUntil > Time.time;
    }

    /// <summary>Splash has ordinary drink durations, but can also grant Spirit to NPC bodies.</summary>
    public static bool TryApplySplash(ZeldaCharacterData user, AlchemyPotionKind kind)
    {
        if (user == null || user.IsDead || user.IsGhostLike) return false;
        if (kind != AlchemyPotionKind.Spirit || IsControlled(user)) return TryApply(user, kind);
        if (user.FinalPossessionEnergy < user.MaxPossessionEnergy) user.RestorePossessionEnergy(4);
        else
        {
            var buffs = user.GetComponent<CharacterPotionBuffs>();
            if (buffs == null) buffs = user.gameObject.AddComponent<CharacterPotionBuffs>();
            buffs.character = user;
            // Like the existing drink, the new capacity starts empty; a second dose can fill it.
            if (buffs.BodySpiritMaximum == 0) buffs.bodySpiritCurrent = 0;
            buffs.ordinaryDurations[(int)kind] = Duration(kind);
            buffs.expiresAt[(int)kind] = Time.time + Duration(kind);
            buffs.previousMask = buffs.ActiveMask();
            user.RefreshPotionDerivedAttributes();
        }
        return true;
    }

    public static bool TryApply(ZeldaCharacterData user, AlchemyPotionKind kind)
    {
        if (user == null || user.IsDead || user.IsGhostLike || (int)kind < 0 || (int)kind >= KindCount)
            return false;
        if (kind == AlchemyPotionKind.Death)
        {
            // Poison is explicitly lethal, not a mitigable attack or an Echo trigger.
            user.KillFromPotion();
            return true;
        }
        if (kind == AlchemyPotionKind.Spirit)
        {
            // Energy belongs to the player, never an NPC body which might later be possessed.
            if (!IsControlled(user)) return false;
            var growth = PlayerGrowthAttributes.GetOrCreate();
            if (user.FinalPossessionEnergy < user.MaxPossessionEnergy) user.RestorePossessionEnergy(4);
            else growth.RefreshPotionEnergy(Duration(kind));
            return true;
        }
        if (kind == AlchemyPotionKind.Vitality && user.CurrentHealth < user.Health)
        {
            user.RestoreHealth(6);
            return true;
        }
        var buffs = user.GetComponent<CharacterPotionBuffs>();
        if (buffs == null) buffs = user.gameObject.AddComponent<CharacterPotionBuffs>();
        buffs.character = user;
        buffs.ordinaryDurations[(int)kind] = Duration(kind);
        buffs.expiresAt[(int)kind] = Time.time + Duration(kind);
        if (kind == AlchemyPotionKind.Echo)
        {
            buffs.echoPosition = user.transform.position;
            buffs.echoHealth = user.CurrentHealth;
            buffs.echoScene = SceneKey(user.gameObject);
            buffs.latestEchoIsArea = false;
        }
        buffs.previousMask = buffs.ActiveMask();
        user.RefreshPotionDerivedAttributes();
        PotionPerceptionEffects.Ensure(user);
        return true;
    }

    /// <summary>Apply a body-owned dose with a save-safe duration multiplier, without an area membership.</summary>
    public static bool ApplyExtendedSplash(ZeldaCharacterData user, AlchemyPotionKind kind, float multiplier)
    {
        if (kind == AlchemyPotionKind.Death || !TryApplySplash(user, kind)) return false;
        var buffs = user.GetComponent<CharacterPotionBuffs>();
        if (buffs != null && buffs.OrdinaryRemaining(kind) > 0)
        {
            float duration = Duration(kind) * Mathf.Clamp(multiplier, 1f, 2f);
            buffs.ordinaryDurations[(int)kind] = duration;
            buffs.expiresAt[(int)kind] = Time.time + duration;
        }
        return true;
    }

    public static void NotifyAction(ZeldaCharacterData user)
    {
        var buffs = user != null ? user.GetComponent<CharacterPotionBuffs>() : null;
        if (buffs == null || buffs.Remaining(AlchemyPotionKind.Veil) <= 0f) return;
        buffs.expiresAt[(int)AlchemyPotionKind.Veil] = 0f;
        if (buffs.hasAreaPotion && buffs.areaKind == AlchemyPotionKind.Veil) buffs.ClearAreaTimer();
        buffs.previousMask = buffs.ActiveMask();
        user.RefreshPotionDerivedAttributes();
    }

    /// <summary>Call before a departing body's scene snapshots are captured.</summary>
    public static void InvalidateEchoForTravel(ZeldaCharacterData user)
    {
        var buffs = user != null ? user.GetComponent<CharacterPotionBuffs>() : null;
        if (buffs == null) return;
        buffs.expiresAt[(int)AlchemyPotionKind.Echo] = 0f;
        buffs.echoScene = null;
        if (buffs.hasAreaPotion && buffs.areaKind == AlchemyPotionKind.Echo) buffs.ClearAreaTimer();
        buffs.areaEchoScene = null;
        buffs.previousMask = buffs.ActiveMask();
    }

    public RuntimeState CaptureRuntimeState()
    {
        if (!IsRestoringWorld) ReconcileAreaSources();
        var remaining = new float[KindCount];
        for (int i = 0; i < KindCount; i++) remaining[i] = OrdinaryRemaining((AlchemyPotionKind)i);
        var savedSources = new Dictionary<string, AlchemyPotionKind>(restoredAreaSources);
        foreach (var pair in areaSources) if (pair.Key != null) savedSources[SourceKey(pair.Key)] = pair.Value;
        var sourceIds = new string[savedSources.Count];
        var sourceKinds = new int[savedSources.Count];
        int sourceIndex = 0;
        foreach (var pair in savedSources)
        { sourceIds[sourceIndex] = pair.Key; sourceKinds[sourceIndex++] = (int)pair.Value; }
        return new RuntimeState {
            remaining = remaining, ordinaryDurations = (float[])ordinaryDurations.Clone(), echoPosition = echoPosition, echoHealth = echoHealth,
            echoScene = echoScene, immunityRemaining = Mathf.Max(0f, immuneUntil - Time.time),
            hasAreaPotion = hasAreaPotion, areaKind = areaKind, areaRemaining = AreaPotionRemaining,
            areaFrozen = areaFrozen, areaSourceIds = sourceIds, areaSourceKinds = sourceKinds,
            areaEchoPosition = areaEchoPosition, areaEchoHealth = areaEchoHealth, areaEchoScene = areaEchoScene,
            latestEchoIsArea = latestEchoIsArea, areaSpiritToken = areaSpiritToken,
            bodySpiritCurrent = BodySpiritCurrent
        };
    }

    public void ApplyRuntimeState(RuntimeState state)
    {
        character = GetComponent<ZeldaCharacterData>();
        for (int i = 0; i < KindCount; i++)
        {
            float value = state.remaining != null && i < state.remaining.Length ? state.remaining[i] : 0f;
            float duration = state.ordinaryDurations != null && i < state.ordinaryDurations.Length
                ? SafeDuration(state.ordinaryDurations[i], Duration((AlchemyPotionKind)i) * 2f) : 0f;
            ordinaryDurations[i] = Mathf.Max(Duration((AlchemyPotionKind)i), duration);
            expiresAt[i] = Time.time + SafeDuration(value, ordinaryDurations[i]);
        }
        echoPosition = state.echoPosition;
        bodySpiritCurrent = BodySpiritMaximum > 0 ? Mathf.Clamp(state.bodySpiritCurrent, 0, 2) : 0;
        echoHealth = Mathf.Max(1, state.echoHealth);
        echoScene = state.echoScene;
        areaSources.Clear();
        restoredAreaSources.Clear();
        hasAreaPotion = state.hasAreaPotion && (int)state.areaKind >= 0 && (int)state.areaKind < KindCount;
        areaKind = hasAreaPotion ? state.areaKind : default;
        areaFrozenRemaining = hasAreaPotion ? SafeDuration(state.areaRemaining, Duration(areaKind) * 2f) : 0f;
        areaExpiresAt = Time.time + areaFrozenRemaining;
        areaFrozen = hasAreaPotion && state.areaFrozen;
        areaEchoPosition = state.areaEchoPosition;
        areaEchoHealth = Mathf.Max(1, state.areaEchoHealth);
        areaEchoScene = state.areaEchoScene;
        latestEchoIsArea = state.latestEchoIsArea;
        areaSpiritToken = hasAreaPotion && areaKind == AlchemyPotionKind.Spirit ? state.areaSpiritToken : null;
        if (state.areaSourceIds != null && state.areaSourceKinds != null)
            for (int i = 0; i < Mathf.Min(state.areaSourceIds.Length, state.areaSourceKinds.Length); i++)
                if (!string.IsNullOrEmpty(state.areaSourceIds[i]) && state.areaSourceKinds[i] >= 0 &&
                    state.areaSourceKinds[i] < KindCount)
                    restoredAreaSources[state.areaSourceIds[i]] = (AlchemyPotionKind)state.areaSourceKinds[i];
        restoreAreaOverlaps = restoredAreaSources.Count > 0;
        SynchronizeSpirit();
        immuneUntil = Time.time + SafeDuration(state.immunityRemaining, 1f);
        previousMask = ActiveMask();
        // Character data restores HP after this call, so it owns the final notification/clamp.
        if (character != null) PotionPerceptionEffects.Ensure(character);
    }

    private static float SafeDuration(float value, float maximum) =>
        float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Clamp(value, 0f, maximum);

    private void Awake() { character = GetComponent<ZeldaCharacterData>(); }
    private void OnEnable()
    {
        if (character == null) character = GetComponent<ZeldaCharacterData>();
        if (!IsRestoringWorld) CheckExpiry();
    }
    private void Update()
    {
        if (IsRestoringWorld || Time.timeScale <= 0f) return;
        CheckExpiry();
    }
    private void CheckExpiry()
    {
        ReconcileAreaSources();
        // A streamed-out body does not keep a buff forever: timestamps keep advancing.
        // Echo cannot survive travel out and back, even if its 120 seconds have not elapsed.
        if (expiresAt[(int)AlchemyPotionKind.Echo] > 0f && echoScene != SceneKey(gameObject))
            expiresAt[(int)AlchemyPotionKind.Echo] = 0f;
        if (hasAreaPotion && areaKind == AlchemyPotionKind.Echo && areaEchoScene != SceneKey(gameObject))
            ClearAreaTimer();
        int mask = ActiveMask();
        if (mask == previousMask) return;
        previousMask = mask;
        if (character != null) character.RefreshPotionDerivedAttributes();
    }
    private int ActiveMask()
    {
        int mask = 0;
        for (int i = 0; i < KindCount; i++) if (Remaining((AlchemyPotionKind)i) > 0f) mask |= 1 << i;
        return mask;
    }
    private void OnCharacterDied(ZeldaCharacterData dead)
    {
        Array.Clear(expiresAt, 0, expiresAt.Length);
        // Like ordinary Spirit, the player reserve survives body deaths. Detach
        // its freeze before clearing body ownership so its countdown can run.
        if (!string.IsNullOrEmpty(areaSpiritToken) && PlayerGrowthAttributes.Instance != null)
            PlayerGrowthAttributes.Instance.SynchronizeAreaPotionEnergy(areaSpiritToken, this,
                AreaPotionRemaining, false);
        areaSpiritToken = null;
        ClearAreaTimer();
        hasAreaPotion = false;
        areaSources.Clear();
        restoredAreaSources.Clear();
        restoreAreaOverlaps = false;
        previousMask = 0;
        immuneUntil = 0f;
    }

    /// <summary>Called only by the normal damage pipeline, never control-switch deaths.</summary>
    public static bool TryPreventLethalDamage(ZeldaCharacterData user, out int restoredHealth)
    {
        restoredHealth = 0;
        var buffs = user != null ? user.GetComponent<CharacterPotionBuffs>() : null;
        if (buffs == null || !Has(user, AlchemyPotionKind.Echo)) return false;
        bool useArea = buffs.hasAreaPotion && buffs.areaKind == AlchemyPotionKind.Echo &&
            buffs.AreaPotionRemaining > 0f && (buffs.latestEchoIsArea || buffs.OrdinaryRemaining(AlchemyPotionKind.Echo) <= 0f);
        Vector3 destination;
        if (!FindSafeEchoPosition(user, useArea ? buffs.areaEchoPosition : buffs.echoPosition, out destination)) return false;
        buffs.expiresAt[(int)AlchemyPotionKind.Echo] = 0f;
        if (buffs.hasAreaPotion && buffs.areaKind == AlchemyPotionKind.Echo) buffs.ClearAreaTimer();
        buffs.immuneUntil = Time.time + 1f;
        restoredHealth = Mathf.Clamp(useArea ? buffs.areaEchoHealth : buffs.echoHealth, 1, user.Health);
        var mover = user.GetComponent<ZeldaFourWayMover>();
        if (mover != null) mover.CancelAttackForEcho();
        var body = user.GetComponent<Rigidbody2D>();
        user.transform.position = destination;
        if (body != null) { body.position = destination; body.velocity = Vector2.zero; }
        Physics2D.SyncTransforms();
        if (mover != null && mover.isActiveAndEnabled && ZeldaRuntimeRegistry.GetControlledMover() == mover)
            ZeldaCharacterAiBase.NotifyPlayerEchoRewind(mover);
        return true;
    }

    private static bool FindSafeEchoPosition(ZeldaCharacterData user, Vector3 desired, out Vector3 result)
    {
        result = desired;
        Physics2D.SyncTransforms();
        var body = user.GetComponent<Collider2D>();
        Vector2 size = body != null ? (Vector2)body.bounds.size : new Vector2(.45f, .45f);
        Vector2 offset = body != null ? (Vector2)(body.bounds.center - user.transform.position) : Vector2.zero;
        size = Vector2.Max(size, new Vector2(.1f, .1f)) * .96f;
        for (int ring = 0; ring <= 16; ring++)
        {
            int samples = ring == 0 ? 1 : Mathf.Max(8, ring * 8);
            for (int i = 0; i < samples; i++)
            {
                float angle = i * Mathf.PI * 2f / samples;
                Vector3 candidate = desired + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * (ring * .25f);
                var filter = new ContactFilter2D { useTriggers = true };
                int count = Physics2D.OverlapBox((Vector2)candidate + offset, size, 0f, filter, overlaps);
                bool free = count < overlaps.Length;
                for (int j = 0; free && j < count; j++)
                {
                    var other = overlaps[j];
                    if (other == null || !BlocksMovement(other) || other.transform.IsChildOf(user.transform) ||
                        ZeldaRuntimeRegistry.GetGameplayScene(other.gameObject) != ZeldaRuntimeRegistry.GetGameplayScene(user.gameObject)) continue;
                    free = false;
                }
                if (free) { result = candidate; return true; }
            }
        }
        return false;
    }

    public static void TryMeleeKnockback(ZeldaCharacterData attacker, ZeldaCharacterData target)
    {
        if (target == null || target.IsDead || !Has(attacker, AlchemyPotionKind.Might) ||
            target is BehemothZeldaCharacterData) return;
        var collider = target.GetComponent<Collider2D>();
        if (collider == null || collider.isTrigger) return;
        // Oversized creatures are not ordinary bodies. Use the target's actual footprint.
        if (Mathf.Max(collider.bounds.size.x, collider.bounds.size.y) > 1.65f) return;
        Vector2 direction = (Vector2)(target.transform.position - attacker.transform.position);
        if (direction.sqrMagnitude < .001f) direction = Vector2.up;
        direction.Normalize();
        float distance = Mathf.Clamp(Mathf.Max(collider.bounds.size.x, collider.bounds.size.y), .45f, 1f);
        var hits = new RaycastHit2D[16];
        var filter = new ContactFilter2D { useTriggers = true };
        int count = collider.Cast(direction, filter, hits, distance);
        if (count == hits.Length) distance = 0f;
        for (int i = 0; i < count; i++)
        {
            var other = hits[i].collider;
            if (other == null || !BlocksMovement(other) || other.transform.IsChildOf(target.transform) ||
                other.transform.IsChildOf(attacker.transform) ||
                ZeldaRuntimeRegistry.GetGameplayScene(other.gameObject) != ZeldaRuntimeRegistry.GetGameplayScene(target.gameObject)) continue;
            distance = Mathf.Min(distance, Mathf.Max(0f, hits[i].distance - .025f));
        }
        Vector2 next = (Vector2)target.transform.position + direction * distance;
        target.transform.position = new Vector3(next.x, next.y, target.transform.position.z);
        var body = target.GetComponent<Rigidbody2D>();
        if (body != null) { body.position = next; body.velocity = Vector2.zero; }
        Physics2D.SyncTransforms();
    }

    private static bool IsControlled(ZeldaCharacterData user)
    {
        var mover = user.GetComponent<ZeldaFourWayMover>();
        return mover != null && mover.isActiveAndEnabled;
    }
    private static bool BlocksMovement(Collider2D collider) => !collider.isTrigger ||
        collider.gameObject.layer == LayerMask.NameToLayer("Blocks") ||
        collider.gameObject.layer == LayerMask.NameToLayer("Hidden Blocks");
    private static string SceneKey(GameObject source)
    {
        var scene = ZeldaRuntimeRegistry.GetGameplayScene(source);
        return string.IsNullOrEmpty(scene.path) ? scene.name : scene.path;
    }
}
