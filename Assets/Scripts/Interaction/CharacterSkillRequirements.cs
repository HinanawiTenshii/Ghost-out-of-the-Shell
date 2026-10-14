using System;
using UnityEngine;

/// <summary>Player-action skill gates. Scripted mechanisms and damage never use these checks.</summary>
public static class CharacterSkillRequirements
{
    public const int Bomb = 3, Handgun = 3, PuppetPry = 3, AutomatonRemote = 3;
    public const int WaterwayGate = 3, DoorUnlock = 2, ElectricGatePanel = 5, Alchemy = 4;
    public const int Cannon = 5;
    public static ZeldaCharacterData ControlledActor => ZeldaRuntimeRegistry.GetControlledMover()?.CharacterData;
    public static bool Has(ZeldaCharacterData actor, int required) => required <= 0 ||
        (actor != null && !actor.IsDead && !actor.IsGhostLike && actor.FinalSkillValue >= required);
    public static string FailureMessage(int required) => $"能力值不足（需要技巧值：{required}）";
    public static bool Require(ZeldaCharacterData actor, int required, bool notify = true)
    {
        if (Has(actor, required)) return true;
        if (notify) ZeldaHealthHeartsUI.Instance?.ShowNotificationPopup(FailureMessage(required));
        return false;
    }
    public static int PryEnergyCost(float baseCost, int skill)
    {
        if (baseCost <= 0f) return 0;
        double cost = baseCost * Math.Pow(0.8d, Math.Max(0, skill - PuppetPry));
        // Remove floating-point noise at integer boundaries (25 * .8^2 = 16, not 17).
        double integer = Math.Round(cost);
        if (Math.Abs(cost - integer) < 1e-10 * Math.Max(1d, Math.Abs(cost))) cost = integer;
        return (int)Math.Min(int.MaxValue, Math.Max(1d, Math.Ceiling(cost)));
    }
    public const string PryDescription = "撬锁需要技巧值：3。每多1点技巧，撬锁能量消耗乘以0.8，最终向上取整。";
}
