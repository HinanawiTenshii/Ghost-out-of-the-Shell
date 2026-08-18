/// <summary>
/// Four-point civilian patrol. Civilians ignore permission warnings, but react
/// like other characters when the player enters a fully hostile area.
/// </summary>
public sealed class CivilianPatrolCharacterAi : ZeldaPatrolCharacterAi
{
    protected override bool EnforcesPermissionAreas => true;
    protected override int MinimumPermissionViolationDifference => 2;
}
