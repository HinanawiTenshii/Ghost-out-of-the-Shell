using System.Collections.Generic;
using UnityEngine;

/// <summary>Script-activated sentinel. Idle/Hostile are its only behaviour states.</summary>
public sealed class AutomatonCharacterAi : ZeldaCharacterAiBase
{
    private static readonly HashSet<AutomatonCharacterAi> instances = new HashSet<AutomatonCharacterAi>();
    public static IReadOnlyCollection<AutomatonCharacterAi> Instances => instances;
    [Header("Activation / 外部条件激活")]
    [SerializeField, Tooltip("默认关闭。关卡条件达成时调用 ActivateHostility 或 SetHostilityActive(true)。")]
    private bool hostilityActivated;
    [Header("Vision Appearance / 视野颜色")]
    [SerializeField, Tooltip("待机时使用普通角色的蓝色；激活后使用基础 Vision Color 中配置的敌对红色。")]
    private Color idleVisionColor = new Color(0.45f, 0.8f, 1f, 0.22f);
    private ZeldaFourWayMover ownMover;
    private ZeldaCharacterData ownData;
    public bool HostilityActivated => hostilityActivated;
    public override bool IsUniversalThreat => hostilityActivated && isActiveAndEnabled &&
        ownMover != null && !ownMover.isActiveAndEnabled && ownData != null && !ownData.IsDead;
    protected override bool RemainsStationary => !hostilityActivated || CurrentTarget == null;
    protected override bool EnforcesPermissionAreas => false;
    // Use the persisted activation state so temporary stun does not falsely signal deactivation.
    protected override Color VisionVisualColor => hostilityActivated ? base.VisionVisualColor : idleVisionColor;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetInstances() => instances.Clear();

    protected override void Awake()
    {
        ownMover = GetComponent<ZeldaFourWayMover>();
        ownData = GetComponent<ZeldaCharacterData>();
        ConfigureOmnidirectionalVision();
        base.Awake();
        instances.Add(this);
    }

    [ContextMenu("Activate Hostility / 激活敌对")]
    public void ActivateHostility() => SetHostilityActive(true);
    [ContextMenu("Deactivate Hostility / 恢复待机")]
    public void DeactivateHostility() => SetHostilityActive(false);
    public void SetHostilityActive(bool active)
    {
        if (hostilityActivated == active) return;
        hostilityActivated = active;
        // The activation latch can be set before Awake, while unloaded or while possessed.
        if (ownMover == null || ownMover.isActiveAndEnabled || !isActiveAndEnabled) return;
        ownMover.CancelAttackForStun();
        ResetPermissionResponse();
    }

    protected override void ChangeState(ZeldaAiState requested, bool playAwarenessCue = true)
    {
        // Status effects (stun/possession) still work; they do not activate the sentinel.
        base.ChangeState(requested == ZeldaAiState.Stunned ? ZeldaAiState.Stunned :
            hostilityActivated ? ZeldaAiState.Hostile : ZeldaAiState.Idle, playAwarenessCue);
    }

    protected override void UpdateTargetAndState(float deltaTime)
    {
        if (!hostilityActivated)
        {
            if (CurrentTarget != null || CurrentState != ZeldaAiState.Idle) ResetPermissionResponse();
            AiMoveDirection = Vector2.zero;
            return;
        }
        ClearFriendlyTarget();
        ZeldaFourWayMover target = CanAttackVisibleCharacter(CurrentTarget) ? CurrentTarget : null;
        if (target == null)
        {
            float nearest = float.MaxValue;
            foreach (ZeldaFourWayMover candidate in ZeldaRuntimeRegistry.Movers)
            {
                if (!CanAttackVisibleCharacter(candidate)) continue;
                float distance = ((Vector2)candidate.transform.position - AiPosition).sqrMagnitude;
                if (distance < nearest) { target = candidate; nearest = distance; }
            }
        }
        if (target != null) BeginTemporaryHostility(target, true);
        else
        {
            if (CurrentTarget != null) ownMover.CancelAttackForStun();
            ResetPermissionResponse(); // Remain activated/Hostile; wait for another visible target.
        }
    }

    private static bool IsFellowAutomaton(ZeldaFourWayMover candidate)
    {
        // Type identity is independent of activation, possession and whether the AI is enabled.
        return candidate != null && (candidate.GetComponent<AutomatonCharacterAi>() != null ||
            candidate.GetComponent<AutomatonZeldaCharacterData>() != null);
    }

    private bool CanAttackVisibleCharacter(ZeldaFourWayMover candidate) =>
        !IsFellowAutomaton(candidate) && CanSeeCharacter(candidate);

    private void ClearFriendlyTarget()
    {
        if (!IsFellowAutomaton(CurrentTarget)) return;
        if (ownMover != null) ownMover.CancelAttackForStun();
        ResetPermissionResponse();
    }

    protected override bool CanSeePlayer(ZeldaFourWayMover candidate) => CanAttackVisibleCharacter(candidate);
    public override void InvestigatePosition(Vector2 position) { }
    public override void OnCharacterDamagedBy(ZeldaCharacterData attacker) { }
    public override void OnPlayerAttackedCharacter(ZeldaCharacterData attacker, ZeldaCharacterData victim) { }
    public override void OnPossessionWitnessed(ZeldaFourWayMover from, ZeldaFourWayMover to) { }
    public override bool OnPlayerDestroyedDoor(ZeldaCharacterData attacker) => false;
    public override bool CanReceiveRoyalCommand(ZeldaFourWayMover player) => false;
    public override void ReceiveRoyalCommand() { }

    public override SaveState CaptureSaveState()
    {
        SaveState state = base.CaptureSaveState();
        state.automatonHostilityActivated = hostilityActivated;
        return state;
    }
    public override void ApplySaveState(SaveState state)
    {
        if (state == null) return;
        hostilityActivated = state.automatonHostilityActivated;
        base.ApplySaveState(state);
        ClearFriendlyTarget();
    }
    public override void RecoverAfterPersistentSceneReturn(ZeldaAiState capturedState)
    {
        // Activation is a persistent condition, not a transient guard pursuit.
        if (ownMover != null && !ownMover.isActiveAndEnabled) ResetPermissionResponse();
    }
    protected override void OnValidate()
    {
        base.OnValidate();
        ConfigureOmnidirectionalVision();
    }
    protected override void OnDestroy()
    {
        instances.Remove(this);
        base.OnDestroy();
    }
}
