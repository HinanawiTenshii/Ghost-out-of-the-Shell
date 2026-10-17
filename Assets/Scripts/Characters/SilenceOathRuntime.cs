using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Player-owned targeted oath. AI owns both the effect and its isolated alert cycle.</summary>
public static class SilenceOathRuntime
{
    public const string SkillId = "silence_oath", SkillName = "缄口之誓";
    public const int EnergyCost = 2;
    public const float BaseDuration = 30f, MaximumDuration = 90f, Range = 3f;
    [Serializable]
    public struct State
    {
        public int rank;
        public float elapsed;
        public bool alertCycle, privateIncident;
    }

    private static readonly HashSet<ZeldaCharacterAiBase> marked = new HashSet<ZeldaCharacterAiBase>();
    private static readonly List<ZeldaFourWayMover> nearby = new List<ZeldaFourWayMover>(16);
    private static readonly Sprite[] icons = new Sprite[3];
    internal static void Register(ZeldaCharacterAiBase ai) => marked.Add(ai);
    internal static void Unregister(ZeldaCharacterAiBase ai) => marked.Remove(ai);
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { marked.Clear(); nearby.Clear(); }

    public static int Rank => PlayerGrowthAttributes.Instance == null ? 0 :
        PlayerGrowthAttributes.Instance.HasSkill("silence_oath_3") ? 3 :
        PlayerGrowthAttributes.Instance.HasSkill("silence_oath_2") ? 2 :
        PlayerGrowthAttributes.Instance.HasSkill("silence_oath") ? 1 : 0;
    public static int RankFor(ZeldaCharacterData owner) => Mathf.Max(Rank,
        owner != null && owner.NativeCharacterSkillId == SkillId ? 1 : 0);

    public static ZeldaCharacterAiBase FindTarget(ZeldaFourWayMover user)
    {
        if (user == null || user.CharacterData == null || user.CharacterData.IsDead || user.CharacterData.IsGhostLike) return null;
        ZeldaRuntimeRegistry.QueryNearbyMovers(user.transform.position, Range, nearby);
        ZeldaCharacterAiBase best = null;
        float bestScore = float.PositiveInfinity;
        foreach (var candidate in nearby)
        {
            if (candidate == null || candidate == user || candidate.enabled) continue;
            var ai = candidate.GetComponent<ZeldaCharacterAiBase>();
            if (!IsValidTarget(user, ai)) continue;
            Vector2 offset = candidate.transform.position - user.transform.position;
            // Prefer the aiming direction, then distance. Stable ID breaks ties.
            float score = offset.sqrMagnitude + Vector2.Angle(user.FacingDirection, offset) * .1f;
            if (score < bestScore || Mathf.Approximately(score, bestScore) && best != null && ai.GetInstanceID() < best.GetInstanceID())
            { best = ai; bestScore = score; }
        }
        return best;
    }

    public static bool IsValidTarget(ZeldaFourWayMover user, ZeldaCharacterAiBase target)
    {
        if (user == null || target == null || target.gameObject == user.gameObject || !target.CanReceiveSilenceOath ||
            ZeldaRuntimeRegistry.GetGameplayScene(user.gameObject) != ZeldaRuntimeRegistry.GetGameplayScene(target.gameObject)) return false;
        Vector2 origin = user.transform.position, point = target.transform.position, offset = point - origin;
        if (offset.sqrMagnitude > Range * Range || Vector2.Angle(user.FacingDirection, offset) > 60f) return false;
        var camera = Camera.main;
        var vision = camera != null ? camera.GetComponent<CameraCircularVision>() : null;
        if (vision != null && vision.isActiveAndEnabled && !vision.IsWorldPositionVisible(point, target.transform)) return false;
        // Even spirit sight cannot cast through a wall. Non-blocking fixtures do not obstruct the oath.
        var hit = Physics2D.Linecast(origin, point, LayerMask.GetMask("Blocks", "Hidden Blocks"));
        return hit.collider == null || hit.transform == target.transform || hit.transform.IsChildOf(target.transform);
    }

    public static bool TryUse(ZeldaCharacterData owner) => TryUse(owner,
        owner != null ? FindTarget(owner.GetComponent<ZeldaFourWayMover>()) : null);

    public static bool TryUse(ZeldaCharacterData owner, ZeldaCharacterAiBase target)
    {
        int rank = RankFor(owner);
        var mover = owner != null ? owner.GetComponent<ZeldaFourWayMover>() : null;
        if (owner == null || owner.IsDead || owner.IsGhostLike || mover == null || !mover.isActiveAndEnabled ||
            mover != ZeldaRuntimeRegistry.GetControlledMover() || mover.IsAttacking || mover.IsPossessionInProgress ||
            rank == 0 || Time.timeScale <= 0f || GameSaveSystem.IsLoading || DocumentReader.IsInputBlocked ||
            ClockworkPuppetRuntime.BlocksCharacterInput || SoulMarkRuntime.IsTransferring) return false;
        if (!IsValidTarget(mover, target))
        { ZeldaHealthHeartsUI.Instance?.ShowNotificationPopup("面前没有可以立誓的角色"); return false; }
        int count = 0;
        foreach (var ai in marked)
            if (ai != null && ai.HasSilenceOath) count++;
        if (!target.HasSilenceOath && count >= (rank >= 3 ? 2 : 1))
        { ZeldaHealthHeartsUI.Instance?.ShowNotificationPopup("缄口之誓已达到目标上限"); return false; }
        if (!owner.TrySpendPossessionEnergy(EnergyCost)) return false;
        target.ApplySilenceOath(rank);
        CharacterPotionBuffs.NotifyAction(owner);
        mover.TryBeginItemAttack(.24f);
        return true;
    }

    public static bool TryGetBuff(ZeldaCharacterData player, out SkillBuffState state)
    {
        state = default;
        var controlled = ZeldaRuntimeRegistry.GetControlledMover();
        if (player == null || controlled == null || controlled.gameObject != player.gameObject) return false;
        ZeldaCharacterAiBase soonest = null;
        foreach (var ai in marked)
            if (ai != null && ai.HasSilenceOath && ZeldaRuntimeRegistry.GetGameplayScene(ai.gameObject) ==
                ZeldaRuntimeRegistry.GetGameplayScene(player.gameObject) &&
                (soonest == null || ai.SilenceOathRemaining < soonest.SilenceOathRemaining)) soonest = ai;
        if (soonest == null) return false;
        state = new SkillBuffState(GetIcon(soonest.SilenceOathRank - 1), soonest.SilenceOathRemaining, soonest.SilenceOathDuration);
        return true;
    }

    // A hooded face with a sealed mouth; monochrome pixels inherit the existing cyan UI tint.
    private static readonly string[] Glyph = {
        ".....######.....", "...##########...", "..###......###..", ".###........###.",
        ".##..........##.", "##............##", "##..##....##..##", "##..##....##..##",
        "##............##", ".##..........##.", ".##..#..#..#.##.", "..############..",
        "..##.#..#..#.#..", "...##......##...", "....########....", ".....######....."
    };
    public static Sprite GetIcon(int rank)
    {
        rank = Mathf.Clamp(rank, 0, 2);
        if (icons[rank] != null) return icons[rank];
        const int size = 22;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) {
            name = "Silence Oath Glyph " + rank, filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        var pixels = new Color32[size * size];
        for (int y = 0; y < Glyph.Length; y++)
            for (int x = 0; x < Glyph[y].Length; x++)
                if (Glyph[y][x] == '#') pixels[(20 - y) * size + x + 2] = Color.white;
        if (rank > 0)
            for (int y = 0; y <= 4; y++) for (int x = 17; x <= 21; x++)
                if (y == 0 || y == 4 || (rank == 1 ? x == 19 : x == 18 || x == 20)) pixels[y * size + x] = Color.white;
        texture.SetPixels32(pixels); texture.Apply(false, true);
        icons[rank] = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, size);
        icons[rank].name = texture.name; icons[rank].hideFlags = HideFlags.HideAndDontSave;
        return icons[rank];
    }
}

/// <summary>One pooled preview, sampled at 10 Hz only while the skill slot is selected.</summary>
public sealed class SilenceOathPreview : MonoBehaviour
{
    private SpriteRenderer marker;
    private ZeldaCharacterAiBase target;
    private ZeldaFourWayMover lastUser;
    private float nextQuery;
    private Vector2 lastFacing;
    public void Hide() { if (marker != null) marker.enabled = false; target = null; lastUser = null; }
    public void Show(ZeldaFourWayMover user)
    {
        if (user != lastUser || user.FacingDirection != lastFacing || Time.time >= nextQuery)
        {
            target = SilenceOathRuntime.FindTarget(user);
            nextQuery = Time.time + .1f; lastUser = user; lastFacing = user.FacingDirection;
        }
        if (target == null || !target.CanReceiveSilenceOath) { if (marker != null) marker.enabled = false; return; }
        if (marker == null)
        {
            gameObject.layer = 2;
            marker = gameObject.AddComponent<SpriteRenderer>();
            marker.sprite = SilenceOathRuntime.GetIcon(0);
        }
        // Active targets already display a mark; the preview sits slightly above it.
        var mover = target.GetComponent<ZeldaFourWayMover>();
        transform.position = mover.GetOverheadWorldPosition(new Vector2(0f, target.HasSilenceOath ? 1.85f : 1.35f));
        transform.localScale = Vector3.one * .5f;
        marker.color = Color.Lerp(ZeldaUiPalette.Primary, Color.white, .35f + .15f * Mathf.Sin(Time.time * 6f));
        var visual = target.GetComponent<SpriteRenderer>();
        if (visual != null) { marker.sortingLayerID = visual.sortingLayerID; marker.sortingOrder = visual.sortingOrder + 9; }
        marker.enabled = true;
    }
}
