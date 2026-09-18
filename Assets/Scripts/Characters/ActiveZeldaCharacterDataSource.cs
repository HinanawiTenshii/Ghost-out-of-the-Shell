using System;
using UnityEngine;

/// <summary>
/// Exposes the currently controlled Zelda character's runtime data for UI code.
/// The controlled character is the active GameObject whose ZeldaFourWayMover is enabled.
/// </summary>
[DefaultExecutionOrder(100)]
public sealed class ActiveZeldaCharacterDataSource : MonoBehaviour
{
    [SerializeField] private bool persistBetweenScenes = true;

    private ZeldaFourWayMover activeMover;
    private ZeldaCharacterData activeCharacterData;
    private PermissionArea currentPermissionArea;

    public static ActiveZeldaCharacterDataSource Instance { get; private set; }

    public ZeldaFourWayMover ActiveMover => activeMover;
    public ZeldaCharacterData ActiveCharacterData => activeCharacterData;
    public PermissionArea CurrentPermissionArea => currentPermissionArea;
    public bool HasActiveCharacter => activeCharacterData != null;
    public int AttackPower { get; private set; }
    public int SkillValue { get; private set; }
    public int PermissionLevel { get; private set; }
    public int CurrentAreaPermissionLevel { get; private set; }
    public int PossessionCost { get; private set; }
    public int PossessionEnergy { get; private set; }
    public int CurrentHealth { get; private set; }
    public int MaxHealth { get; private set; }
    public bool IsGhostLike { get; private set; }

    /// <summary>
    /// Raised when the controlled character changes or any exposed value changes.
    /// UI code can subscribe to this instead of polling every frame.
    /// </summary>
    public event Action DataChanged;
    public event Action ActiveCharacterMoved;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (persistBetweenScenes)
        {
            DontDestroyOnLoad(gameObject);
        }

    }

    private void OnEnable()
    {
        ZeldaRuntimeRegistry.ControlledMoverChanged += Refresh;
        ZeldaRuntimeRegistry.PermissionAreasChanged += Refresh;
        PlayerGrowthAttributes.InstanceChanged += HandleGrowthInstanceChanged;
        ConnectGrowthSource();
        Refresh();
        ConnectActiveSources();
    }

    private void OnDisable()
    {
        ZeldaRuntimeRegistry.ControlledMoverChanged -= Refresh;
        ZeldaRuntimeRegistry.PermissionAreasChanged -= Refresh;
        PlayerGrowthAttributes.InstanceChanged -= HandleGrowthInstanceChanged;
        DisconnectActiveSources();
        DisconnectGrowthSource();
    }

    private void OnDestroy()
    {
        DisconnectActiveSources();
        DisconnectGrowthSource();
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>Forces an immediate target lookup and value refresh.</summary>
    public void Refresh()
    {
        ZeldaFourWayMover nextMover = FindControlledMover();
        ZeldaCharacterData nextData = nextMover != null
            ? nextMover.GetComponent<ZeldaCharacterData>()
            : null;
        PermissionArea nextPermissionArea = nextData != null
            ? FindHighestPermissionArea(nextData.transform.position)
            : null;

        int nextAttackPower = nextData != null ? nextData.FinalAttackPower : 0;
        int nextSkillValue = nextData != null ? nextData.FinalSkillValue : 0;
        int nextPermissionLevel = nextData != null ? nextData.PermissionLevel : 0;
        int nextAreaPermissionLevel = nextPermissionArea != null
            ? nextPermissionArea.PermissionLevel
            : PermissionArea.DefaultPermissionLevel;
        int nextPossessionCost = nextData != null ? nextData.PossessionCost : 0;
        int nextPossessionEnergy = nextData != null ? nextData.FinalPossessionEnergy : 0;
        int nextEnergyThirds = nextData != null ? nextData.CrystalEnergyThirds : 0;
        int nextEnergyMax = nextData != null ? nextData.MaxPossessionEnergy : 0;
        int nextCurrentHealth = nextData != null ? nextData.CurrentHealth : 0;
        int nextMaxHealth = nextData != null ? nextData.Health : 0;
        bool nextIsGhostLike = nextData != null && nextData.IsGhostLike;

        bool changed = activeMover != nextMover ||
            activeCharacterData != nextData ||
            currentPermissionArea != nextPermissionArea ||
            AttackPower != nextAttackPower ||
            SkillValue != nextSkillValue ||
            PermissionLevel != nextPermissionLevel ||
            CurrentAreaPermissionLevel != nextAreaPermissionLevel ||
            PossessionCost != nextPossessionCost ||
            PossessionEnergy != nextPossessionEnergy ||
            cachedCrystalEnergyThirds != nextEnergyThirds ||
            cachedEnergyMax != nextEnergyMax ||
            CurrentHealth != nextCurrentHealth ||
            MaxHealth != nextMaxHealth ||
            IsGhostLike != nextIsGhostLike;

        if (activeMover != nextMover || activeCharacterData != nextData)
        {
            DisconnectActiveSources();
            activeMover = nextMover;
            activeCharacterData = nextData;
            ConnectActiveSources();
        }
        currentPermissionArea = nextPermissionArea;
        AttackPower = nextAttackPower;
        SkillValue = nextSkillValue;
        PermissionLevel = nextPermissionLevel;
        CurrentAreaPermissionLevel = nextAreaPermissionLevel;
        PossessionCost = nextPossessionCost;
        PossessionEnergy = nextPossessionEnergy;
        cachedCrystalEnergyThirds = nextEnergyThirds;
        cachedEnergyMax = nextEnergyMax;
        CurrentHealth = nextCurrentHealth;
        MaxHealth = nextMaxHealth;
        IsGhostLike = nextIsGhostLike;

        if (changed)
        {
            DataChanged?.Invoke();
        }
    }

    private PlayerGrowthAttributes connectedGrowthSource;
    private int cachedCrystalEnergyThirds, cachedEnergyMax;

    private void ConnectActiveSources()
    {
        if (activeMover != null)
        {
            activeMover.PositionChanged -= HandleActiveCharacterMoved;
            activeMover.PositionChanged += HandleActiveCharacterMoved;
        }

        if (activeCharacterData != null)
        {
            activeCharacterData.ValuesChanged -= Refresh;
            activeCharacterData.ValuesChanged += Refresh;
        }
    }

    private void DisconnectActiveSources()
    {
        if (activeMover != null)
        {
            activeMover.PositionChanged -= HandleActiveCharacterMoved;
        }

        if (activeCharacterData != null)
        {
            activeCharacterData.ValuesChanged -= Refresh;
        }
    }

    private void HandleActiveCharacterMoved()
    {
        Refresh();
        ActiveCharacterMoved?.Invoke();
    }

    private void HandleGrowthInstanceChanged()
    {
        ConnectGrowthSource();
        Refresh();
    }

    private void ConnectGrowthSource()
    {
        PlayerGrowthAttributes nextSource = PlayerGrowthAttributes.Instance;
        if (connectedGrowthSource == nextSource)
        {
            return;
        }

        DisconnectGrowthSource();
        connectedGrowthSource = nextSource;
        if (connectedGrowthSource != null)
        {
            connectedGrowthSource.AttributesChanged += Refresh;
        }
    }

    private void DisconnectGrowthSource()
    {
        if (connectedGrowthSource != null)
        {
            connectedGrowthSource.AttributesChanged -= Refresh;
            connectedGrowthSource = null;
        }
    }

    private ZeldaFourWayMover FindControlledMover()
    {
        // During possession the new mover is enabled before the previous mover
        // is disabled. The registry is the authority for that overlap frame;
        // returning the cached active mover here could keep the UI subscribed
        // to the old character indefinitely because its later disable is no
        // longer a controlled-mover change.
        return ZeldaRuntimeRegistry.GetControlledMover();
    }

    private PermissionArea FindHighestPermissionArea(Vector2 worldPosition)
    {
        PermissionArea highestPermissionArea = null;

        foreach (PermissionArea area in ZeldaRuntimeRegistry.PermissionAreas)
        {
            if (area == null || !area.isActiveAndEnabled || !area.Contains(worldPosition))
            {
                continue;
            }

            if (highestPermissionArea == null ||
                area.PermissionLevel > highestPermissionArea.PermissionLevel)
            {
                highestPermissionArea = area;
            }
        }

        return highestPermissionArea;
    }
}
