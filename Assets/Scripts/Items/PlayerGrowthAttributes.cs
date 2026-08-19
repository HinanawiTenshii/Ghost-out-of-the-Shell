using System;
using UnityEngine;

/// <summary>
/// Stores character-independent player growth bonuses.
/// Add one instance to a scene; duplicate instances are discarded automatically.
/// </summary>
[DefaultExecutionOrder(-200)]
public sealed class PlayerGrowthAttributes : MonoBehaviour
{
    [Serializable]
    public struct RuntimeState
    {
        public int attackPower;
        public int skillValue;
        public int possessionEnergy;
        public int collectibleCount;
    }

    [SerializeField] private bool persistBetweenScenes = true;
    [SerializeField, Min(0)] private int attackPower;
    [SerializeField, Min(0)] private int skillValue;
    [SerializeField, Min(0)] private int possessionEnergy;
    [SerializeField, Min(0)] private int collectibleCount;

    public static PlayerGrowthAttributes Instance { get; private set; }
    public static event Action InstanceChanged;

    public int AttackPower => attackPower;
    public int SkillValue => skillValue;
    public int PossessionEnergy => possessionEnergy;
    public int CollectibleCount => collectibleCount;

    public event Action AttributesChanged;

    public RuntimeState CaptureRuntimeState()
    {
        return new RuntimeState
        {
            attackPower = attackPower,
            skillValue = skillValue,
            possessionEnergy = possessionEnergy,
            collectibleCount = collectibleCount
        };
    }

    public void ApplyRuntimeState(RuntimeState state)
    {
        attackPower = Mathf.Max(0, state.attackPower);
        skillValue = Mathf.Max(0, state.skillValue);
        possessionEnergy = Mathf.Max(0, state.possessionEnergy);
        collectibleCount = Mathf.Max(0, state.collectibleCount);
        AttributesChanged?.Invoke();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ClampValues();
        InstanceChanged?.Invoke();
        if (persistBetweenScenes)
        {
            DontDestroyOnLoad(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            InstanceChanged?.Invoke();
        }
    }

    public void SetAttributes(
        int newAttackPower,
        int newSkillValue,
        int newPossessionEnergy)
    {
        newAttackPower = Mathf.Max(0, newAttackPower);
        newSkillValue = Mathf.Max(0, newSkillValue);
        newPossessionEnergy = Mathf.Max(0, newPossessionEnergy);

        if (attackPower == newAttackPower &&
            skillValue == newSkillValue &&
            possessionEnergy == newPossessionEnergy)
        {
            return;
        }

        attackPower = newAttackPower;
        skillValue = newSkillValue;
        possessionEnergy = newPossessionEnergy;
        AttributesChanged?.Invoke();
    }

    public void AddAttackPower(int amount)
    {
        SetAttributes(
            Mathf.Max(0, attackPower + amount),
            skillValue,
            possessionEnergy);
    }

    public void AddSkillValue(int amount)
    {
        SetAttributes(
            attackPower,
            Mathf.Max(0, skillValue + amount),
            possessionEnergy);
    }

    public void AddPossessionEnergy(int amount)
    {
        SetAttributes(
            attackPower,
            skillValue,
            Mathf.Max(0, possessionEnergy + amount));
    }

    public void AddCollectibles(int amount)
    {
        int nextCount = Mathf.Max(0, collectibleCount + amount);
        if (nextCount == collectibleCount)
        {
            return;
        }

        collectibleCount = nextCount;
        AttributesChanged?.Invoke();
    }

    private void ClampValues()
    {
        attackPower = Mathf.Max(0, attackPower);
        skillValue = Mathf.Max(0, skillValue);
        possessionEnergy = Mathf.Max(0, possessionEnergy);
        collectibleCount = Mathf.Max(0, collectibleCount);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ClampValues();
    }
#endif
}
