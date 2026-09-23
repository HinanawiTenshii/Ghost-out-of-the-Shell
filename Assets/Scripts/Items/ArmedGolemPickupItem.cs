using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>A dormant automaton is both a character and a unique, placeable inventory item.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(AutomatonCharacterAi), typeof(AutomatonZeldaCharacterData))]
public sealed class ArmedGolemPickupItem : PickupItemBase
{
    public const string DefaultItemId = "armed_golem";
    public const string DefaultItemName = "武装魔像";
    public const string DefaultDescription = "尚在研发中的武装魔像，目前索敌仍存在故障，启动后会攻击目光所及的所有人。";
    public const string TooHeavyMessage = "太重了，以当前的力量无法举起";
    public const int RequiredStrength = 3;

    [Serializable]
    private sealed class StoredCharacter
    {
        public int version = 1;
        public ZeldaCharacterData.RuntimeState character;
        public List<SavedScalarFields.Value> characterFields;
        public List<SavedScalarFields.Value> aiFields;
        public Vector3 scale;
        public Vector2 facing;
    }

    private AutomatonCharacterAi ai;
    private AutomatonZeldaCharacterData character;
    private ZeldaFourWayMover mover;
    private bool collected;

    protected override bool UsesTriggerCollider => false;
    public override bool UsePlacesInWorld => true;
    public override bool CanBeStoredInCardboardBox(ZeldaCharacterData user) => false;
    // Inventory routes R through the drop path; never consume the character as a disposable effect.
    protected override bool ApplyUseEffect(ZeldaCharacterData user) => false;
    protected override bool CanAttemptPickup => !collected && ai != null && character != null &&
        mover != null && !mover.isActiveAndEnabled && !ai.HostilityActivated &&
        !ai.IsPossessionLocked && !character.IsDead && !character.IsGhostLike;

    protected override void Awake()
    {
        CacheComponents();
        base.Awake();
    }

    private void CacheComponents()
    {
        // Save restoration can inspect disabled scene objects before their Awake.
        if (ai == null) ai = GetComponent<AutomatonCharacterAi>();
        if (character == null) character = GetComponent<AutomatonZeldaCharacterData>();
        if (mover == null) mover = GetComponent<ZeldaFourWayMover>();
    }

    public override bool TryPickUp()
    {
        ZeldaFourWayMover controlled = ZeldaRuntimeRegistry.GetControlledMover();
        ZeldaCharacterData user = controlled != null ? controlled.GetComponent<ZeldaCharacterData>() : null;
        // Recheck at execution time: activation/possession may have changed since the E offer.
        if (!CanAttemptPickup || IsPickupLocked || DocumentReader.IsInputBlocked ||
            ClockworkPuppetRuntime.BlocksCharacterInput || controlled == null || user == null ||
            user.IsDead || user.IsGhostLike || controlled == mover ||
            ZeldaRuntimeRegistry.GetGameplayScene(controlled.gameObject) != ZeldaRuntimeRegistry.GetGameplayScene(gameObject) ||
            Vector2.Distance(controlled.transform.position, transform.position) > PickupDistance)
            return false;

        if (user.FinalAttackPower < RequiredStrength)
        {
            ZeldaHealthHeartsUI.Instance?.ShowNotificationPopup(TooHeavyMessage);
            return false;
        }
        if (!base.TryPickUp())
        {
            ZeldaHealthHeartsUI.Instance?.ShowNotificationPopup("物品栏已满，无法拾取");
            return false;
        }
        collected = true;
        // Destroy is deferred. Remove this character from interactions/AI immediately.
        gameObject.SetActive(false);
        return true;
    }

    public override string InventoryState
    {
        get
        {
            CacheComponents();
            return JsonUtility.ToJson(new StoredCharacter {
                character = character.CaptureRuntimeState(),
                characterFields = SavedScalarFields.Capture(character),
                aiFields = SavedScalarFields.Capture(ai),
                scale = transform.lossyScale,
                facing = ai.FacingDirection
            });
        }
    }

    public override void ApplyInventoryState(string state)
    {
        if (string.IsNullOrEmpty(state)) return;
        CacheComponents();
        StoredCharacter stored = JsonUtility.FromJson<StoredCharacter>(state);
        if (stored == null || stored.version != 1 || stored.character.health <= 0 ||
            stored.scale.x <= 0f || stored.scale.y <= 0f)
            throw new FormatException("Invalid armed golem inventory state.");
        SavedScalarFields.Apply(character, stored.characterFields);
        character.InvalidateVisuals();
        character.ApplyRuntimeState(stored.character);
        SavedScalarFields.Apply(ai, stored.aiFields);
        transform.localScale = stored.scale;
        // Placement is not activation. Existing external condition interfaces remain in charge.
        ai.DeactivateHostility();
        ZeldaCharacterAiBase.SaveState aiState = ai.CaptureSaveState();
        aiState.state = ZeldaAiState.Idle;
        aiState.facing = stored.facing;
        aiState.automatonHostilityActivated = false;
        ai.ApplySaveState(aiState);
    }

    protected override void OnDropped()
    {
        collected = false;
        ai.DeactivateHostility();
    }
}
