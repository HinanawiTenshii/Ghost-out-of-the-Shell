/// <summary>
/// Stationary civilian. Retains the patrol civilian's permission response,
/// investigation, hostility and recovery; only the ordinary Idle tick differs.
/// </summary>
public sealed class CivilianIdleCharacterAi : ZeldaCharacterAiBase
{
    protected override bool EnforcesPermissionAreas => true;
    protected override int MinimumPermissionViolationDifference => 2;
}
