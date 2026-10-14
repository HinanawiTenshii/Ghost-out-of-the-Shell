using UnityEngine;

/// <summary>A projected, reusable link to one clockwork puppet, not the automaton toggle remote.</summary>
public sealed class ClockworkPuppetRemotePickupItem : PickupItemBase
{
    protected override string AdditionalSkillRequirementDescription => CharacterSkillRequirements.PryDescription;
    public const string DefaultItemId = "clockwork_puppet_remote";
    public const string DefaultItemName = "魔力遥控器";
    public const string DefaultDescription = "由机关人偶的魔力凝成的临时遥控器。按R重新接管与之相连的人偶，再次按R退出控制。回收人偶时遥控器会还原为人偶；丢弃后会立即消散，人偶的魔力耗尽时也会随之消失。";
    public override bool CanBeStoredInCardboardBox(ZeldaCharacterData user) => false;
    protected override bool CanAttemptPickup => false;

    public bool TryReconnect(ZeldaCharacterData user, string puppetInstanceId)
    {
        if (!CharacterSkillRequirements.Require(user, RequiredUseSkill)) return false;
        if (user == null || user.IsDead || user.IsGhostLike || string.IsNullOrEmpty(puppetInstanceId) ||
            DocumentReader.IsInputBlocked || Time.timeScale <= 0f || ClockworkPuppetRuntime.BlocksCharacterInput) return false;
        if (GameSaveSystem.IsLoading ||
            (SceneTravelStateManager.Instance != null && SceneTravelStateManager.Instance.IsSceneTravelRestoreInProgress))
            return false;
        foreach (var puppet in ClockworkPuppetRuntime.ActivePuppets)
            if (puppet != null && puppet.PuppetInstanceId == puppetInstanceId && puppet.TryResumeRemoteControl(user))
            { PlayUseGesture(user);return true; }
        foreach (var driver in ClockworkPuppetBoxDriver.ActiveDrivers)
            if (driver != null && driver.PuppetInstanceId == puppetInstanceId && driver.TryResumeRemoteControl(user))
            { PlayUseGesture(user);return true; }
        // Absence may mean the target scene is unloaded, not that the puppet died.
        ZeldaHealthHeartsUI.Instance?.ShowNotificationPopup("当前场景无法连接该人偶，请返回放置人偶的场景后重试");
        return false;
    }

    public static void Disconnect(string puppetInstanceId)
    {
        var active = ClockworkPuppetRuntime.RemoteControlledPuppet;
        if (active != null && active.PuppetInstanceId == puppetInstanceId)
            ClockworkPuppetRuntime.EndActiveRemoteControl();
        foreach (var driver in ClockworkPuppetBoxDriver.ActiveDrivers)
            if (driver != null && driver.IsUnderRemoteControl && driver.PuppetInstanceId == puppetInstanceId)
            { ClockworkPuppetBoxDriver.EndActiveRemoteControl();break; }
    }

    protected override bool ApplyUseEffect(ZeldaCharacterData user)
    {
        TryReconnect(user,UniqueInstanceId);
        return false; // A control link is never consumed by R.
    }
    protected override void OnDropped()
    {
        Disconnect(UniqueInstanceId);
        gameObject.SetActive(false);
        Destroy(gameObject);
    }
}
