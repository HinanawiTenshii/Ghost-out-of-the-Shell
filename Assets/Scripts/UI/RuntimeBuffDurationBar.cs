using UnityEngine;

/// <summary>Read-only, pooled HUD for potion buffs and active skill effects.</summary>
[DisallowMultipleComponent]
public sealed class RuntimeBuffDurationBar : MonoBehaviour
{
    private const int IconCount = 18;
    private const float IconSize = 40f;
    private const float Gap = 6f;
    private readonly BuffDurationIconGraphic[] icons = new BuffDurationIconGraphic[IconCount];
    private RectTransform root;
    private RectTransform propertyRow;
    private RuntimeSkillSlots skillSlots;
    private ZeldaCharacterData displayedCharacter;
    private CharacterPotionBuffs bodyBuffs;
    public int VisibleCount { get; private set; }
    public ZeldaCharacterData DisplayedCharacter => displayedCharacter;

    public void Build(RectTransform anchorRow, RuntimeSkillSlots skills = null)
    {
        root = (RectTransform)transform;
        propertyRow = anchorRow;
        skillSlots = skills;
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(0f, 1f);
        for (int i = 0; i < IconCount; i++)
        {
            if (i == (int)AlchemyPotionKind.Death || icons[i] != null) continue;
            var go = new GameObject("Buff " + (BuffDurationIcon)i, typeof(RectTransform), typeof(BuffDurationIconGraphic));
            go.layer = gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(root, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = Vector2.one * IconSize;
            icons[i] = go.GetComponent<BuffDurationIconGraphic>();
            icons[i].raycastTarget = false;
            go.SetActive(false);
        }
        RefreshNow();
    }

    public BuffDurationIconGraphic GetIcon(BuffDurationIcon kind)
    {
        int index = (int)kind;
        return index >= 0 && index < icons.Length ? icons[index] : null;
    }

    private void LateUpdate() => RefreshNow();
    private void OnEnable() => RefreshNow();

    public void RefreshNow()
    {
        if (root == null || propertyRow == null) return;
        var mover = ZeldaRuntimeRegistry.GetControlledMover();
        var nextCharacter = mover != null && mover.isActiveAndEnabled ? mover.GetComponent<ZeldaCharacterData>() : null;
        if (displayedCharacter != nextCharacter)
        {
            displayedCharacter = nextCharacter;
            bodyBuffs = null;
        }
        if (bodyBuffs == null && displayedCharacter != null)
            bodyBuffs = displayedCharacter.GetComponent<CharacterPotionBuffs>();

        var canvasRect = root.parent as RectTransform;
        float width = canvasRect != null ? canvasRect.rect.width : 1280f;
        int columns = Mathf.Clamp(Mathf.FloorToInt((width * .38f + Gap) / (IconSize + Gap)), 1, 10);
        Vector2 position = propertyRow.anchoredPosition + new Vector2(0f, -propertyRow.rect.height - 8f);
        if (root.anchoredPosition != position) root.anchoredPosition = position;
        int visible = 0;
        bool show = displayedCharacter != null && !displayedCharacter.IsDead && !CharacterPotionBuffs.IsRestoringWorld;
        for (int i = 0; i < icons.Length; i++)
        {
            var icon = icons[i];
            if (icon == null) continue;
            bool isSkill = i >= (int)BuffDurationIcon.CombatExpertise;
            SkillBuffState skill = default;
            float duration = 0f;
            float remaining = show && !isSkill ? Remaining(i, out duration) : 0f;
            bool active = isSkill
                ? show && CharacterSkillBuffs.TryGet(displayedCharacter, (BuffDurationIcon)i, out skill)
                : remaining > 0f && duration > 0f;
            if (icon.gameObject.activeSelf != active) icon.gameObject.SetActive(active);
            if (!active) continue;
            if (isSkill) icon.SetSkillDisplay((BuffDurationIcon)i, skill.Icon, skill.NormalizedRemaining, skill.IsTimed);
            else icon.SetDisplay((BuffDurationIcon)i, Mathf.Clamp01(remaining / duration));
            Vector2 iconPosition = new Vector2((visible % columns) * (IconSize + Gap), -(visible / columns) * (IconSize + Gap));
            if (icon.rectTransform.anchoredPosition != iconPosition) icon.rectTransform.anchoredPosition = iconPosition;
            visible++;
        }
        VisibleCount = visible;
        int rows = visible == 0 ? 0 : (visible + columns - 1) / columns;
        Vector2 size = visible == 0 ? Vector2.zero : new Vector2(
            Mathf.Min(visible, columns) * (IconSize + Gap) - Gap, rows * (IconSize + Gap) - Gap);
        if (root.sizeDelta != size) root.sizeDelta = size;
        // Preserve the original centered skill column unless a narrow/short canvas
        // would place its top shortcut or selected name over the new buff strip.
        if (skillSlots != null) skillSlots.SetTopClearance(visible > 0 ? -position.y + root.sizeDelta.y + 8f : 0f);
    }

    private float Remaining(int index, out float duration)
    {
        if (index == (int)BuffDurationIcon.EchoImmunity)
        {
            duration = 1f;
            return bodyBuffs != null ? bodyBuffs.ImmunityRemaining : 0f;
        }
        var kind = (AlchemyPotionKind)index;
        duration = AlchemyRecipeCatalog.GetDuration(kind);
        if (kind == AlchemyPotionKind.Spirit)
        {
            float player = PlayerGrowthAttributes.Instance != null
                ? PlayerGrowthAttributes.Instance.PotionEnergyDisplayRemaining(out duration) : 0f;
            float bodyDuration = AlchemyRecipeCatalog.GetDuration(kind);
            float body = bodyBuffs != null ? bodyBuffs.DisplayRemaining(kind, out bodyDuration) : 0f;
            if (body > player) { duration = bodyDuration; return body; }
            return player;
        }
        return bodyBuffs != null ? bodyBuffs.DisplayRemaining(kind, out duration) : 0f;
    }

    private void OnDisable()
    {
        VisibleCount = 0;
        for (int i = 0; i < icons.Length; i++)
            if (icons[i] != null) icons[i].gameObject.SetActive(false);
        if (skillSlots != null) skillSlots.SetTopClearance(0f);
    }
}
