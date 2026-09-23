using UnityEngine;

/// <summary>Reusable scene-wide toggle for enabled armed automatons.</summary>
public sealed class PuppetRemotePickupItem : PickupItemBase
{
    public const string DefaultItemId = "puppet_remote";
    public const string DefaultItemName = "人偶遥控器";
    public const string DefaultDescription = "用于控制人偶的遥控器，使用可以开启或关闭附近的人偶，注意：人偶索敌系统存在故障，会攻击附近所有的人。";

    // R must operate the remote even next to a box, rather than storing it in that box.
    public override bool CanBeStoredInCardboardBox(ZeldaCharacterData user) => false;

    protected override bool ApplyUseEffect(ZeldaCharacterData user)
    {
        if (user == null || user.IsDead || user.IsGhostLike) return false;
        int count = ToggleSceneAutomatons(user);
        ZeldaHealthHeartsUI.Instance?.ShowNotificationPopup(count > 0
            ? $"已切换 {count} 个人偶的状态"
            : "当前场景没有可控制的已启用人偶");
        // The shared inventory destroys this temporary use instance but retains the inventory item.
        return false;
    }

    public int ToggleSceneAutomatons(ZeldaCharacterData user)
    {
        if (user == null || user.IsDead || user.IsGhostLike) return 0;
        var scene = ZeldaRuntimeRegistry.GetGameplayScene(user.gameObject);
        if (!scene.IsValid() || !scene.isLoaded) return 0;

        int count = 0;
        foreach (AutomatonCharacterAi automaton in AutomatonCharacterAi.Instances)
        {
            if (automaton == null || !automaton.isActiveAndEnabled ||
                ZeldaRuntimeRegistry.GetGameplayScene(automaton.gameObject) != scene) continue;
            ZeldaCharacterData data = automaton.GetComponent<ZeldaCharacterData>();
            if (data == null || data.IsDead || data.IsGhostLike) continue;
            // Use the persistent activation latch, not transient states such as Stunned.
            // This also preserves normal cancel-attack, vision-color and save-state behavior.
            automaton.SetHostilityActive(!automaton.HostilityActivated);
            count++;
        }
        return count;
    }
}
