using UnityEngine;

/// <summary>The living advanced paladin sustains its own squad, never itself.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(SquadPatrolCharacterAi))]
public sealed class SquadRegenerationAura : MonoBehaviour
{
    [Min(.31f)] public float healInterval = .5f;
    [Min(1)] public int healthPerTick = 1;
    SquadPatrolCharacterAi squad;
    ZeldaCharacterData source;
    float elapsed;
    void Awake() { squad = GetComponent<SquadPatrolCharacterAi>(); source = GetComponent<ZeldaCharacterData>(); }
    void Update()
    {
        if (source == null || source.IsDead || source.CurrentHealth <= 0 || GameSaveSystem.IsLoading) return;
        elapsed += Time.deltaTime;
        if (elapsed < Mathf.Max(.31f, healInterval)) return;
        elapsed = 0;
        var roster = squad.SquadMembers;
        if (roster == null) return;
        foreach (var member in roster)
        {
            if (member == null || member == squad ||
                ZeldaRuntimeRegistry.GetGameplayScene(member.gameObject) != ZeldaRuntimeRegistry.GetGameplayScene(gameObject)) continue;
            member.GetComponent<ZeldaCharacterData>()?.RestoreHealth(Mathf.Max(1, healthPerTick));
        }
    }
}
