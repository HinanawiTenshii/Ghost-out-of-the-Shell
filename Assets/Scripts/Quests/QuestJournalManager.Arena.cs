using UnityEngine;

public sealed partial class QuestJournalManager
{
    public const string ArenaQuestId = "level2.arena";
    public const string ArenaQuestTitle = "你们还不满意吗？";
    public const string ArenaInjectionQuestId = "level2.arena.injection";
    public const string ArenaInjectionQuestTitle = "为你的健康干杯";
    public const string ArenaInjectionObjective = "向任意一台注液器注入任意药水";
    public const string ArenaRegistrarTarget = "level2.arena.registrar";
    public const string ArenaEntranceTarget = "level2.arena.entrance";
    public const string ArenaCombatTarget = "level2.arena.combat";
    private const string ArenaIntroduction = "报名参加竞技场挑战后，需要依次击败所有出场的守护者。任意一方的生命值降至1点时，该方本轮挑战失败。\n\n全部获胜后，必须由参赛角色本人领取试炼证明。若该角色在领奖前死亡，需要重新报名挑战。领取证明后无法再次挑战。";

    public enum ArenaQuestStage { Register, Preparing, Fighting, RoundWon, Defeated, AwaitingReward, WinnerDied, Claimed }
    private ArenaQuestStage arenaQuestStage;
    private int arenaQuestWins, arenaQuestRounds;
    public ArenaQuestStage ArenaStage => arenaQuestStage;
    public int ArenaWins => arenaQuestWins;
    public int ArenaRounds => arenaQuestRounds;
    public bool ArenaChallengeWon => arenaQuestStage == ArenaQuestStage.AwaitingReward || arenaQuestStage == ArenaQuestStage.Claimed;

    // Called at arena state changes, never by HUD polling. The journal retains a
    // compact projection while the arena scene is unloaded, not a second layout.
    // Historical arena saves are also upgraded here; fresh idle arenas do not
    // create a quest merely because the player entered the scene.
    public void RecordArenaQuestProgress(ArenaChallengeController.RuntimeState state)
    {
        if (state == null || IsEntryCompleted(ArenaQuestId)) return;
        // Unity JSON can round-trip a null array as an empty array. Neither is
        // evidence of registration in an otherwise untouched idle arena.
        if (state.phase == ArenaChallengeController.Phase.Idle && (state.roster == null || state.roster.Length == 0) &&
            !state.claimed && !state.unclaimedWinnerDied) return;
        arenaQuestRounds = state.roster != null ? state.roster.Length : 0;
        arenaQuestWins = Mathf.Clamp(state.wins, 0, arenaQuestRounds);
        if (state.claimed) arenaQuestStage = ArenaQuestStage.Claimed;
        else if (state.unclaimedWinnerDied) arenaQuestStage = ArenaQuestStage.WinnerDied;
        else
        {
            switch (state.phase)
            {
                case ArenaChallengeController.Phase.Preparing: arenaQuestStage = ArenaQuestStage.Preparing; break;
                case ArenaChallengeController.Phase.Fighting: arenaQuestStage = ArenaQuestStage.Fighting; break;
                case ArenaChallengeController.Phase.Result:
                    arenaQuestStage = state.lastWon ? ArenaQuestStage.RoundWon : ArenaQuestStage.Defeated; break;
                case ArenaChallengeController.Phase.Completed: arenaQuestStage = ArenaQuestStage.AwaitingReward; break;
                default: arenaQuestStage = ArenaQuestStage.Register; arenaQuestWins = 0; break;
            }
        }
        RefreshArenaQuest(true);
    }

    public string ArenaCurrentObjective
    {
        get
        {
            switch (arenaQuestStage)
            {
                case ArenaQuestStage.Preparing: return "从大门进入竞技场，挑战下一名守护者";
                case ArenaQuestStage.Fighting: return "击败场内守护者";
                case ArenaQuestStage.RoundWon: return "本轮获胜，等待本轮结算";
                case ArenaQuestStage.Defeated: return "本轮挑战失败，返回报名官处重新报名";
                case ArenaQuestStage.AwaitingReward: return "由获胜角色向报名官领取试炼证明";
                case ArenaQuestStage.WinnerDied: return "原获胜角色已死亡，使用其他角色重新报名";
                case ArenaQuestStage.Claimed: return "已领取试炼证明";
                default: return "前往竞技场报名官处重新报名";
            }
        }
    }

    private void RefreshArenaQuest(bool allowCreate = false)
    {
        bool exists = TryGetEntry(ArenaQuestId, out var entry);
        if ((!exists && !allowCreate) || IsEntryCompleted(ArenaQuestId)) return;
        string details = ArenaIntroduction + "\n\n当前目标：" + ArenaCurrentObjective + "。";
        if (arenaQuestRounds > 0 && arenaQuestStage != ArenaQuestStage.Register)
            details += "\n本次挑战已击败守护者：" + arenaQuestWins + "/" + arenaQuestRounds + "。";
        if (arenaQuestStage == ArenaQuestStage.Register || arenaQuestStage == ArenaQuestStage.WinnerDied)
            details += "\n再次报名会重置本次挑战战绩。挑战中消耗的道具和生命值不会恢复。";
        if (arenaQuestStage == ArenaQuestStage.Claimed)
            details += "\n已完成竞技场挑战并领取试炼证明，无法再次报名。";
        if (!exists || entry.Details != details)
            AddOrUpdateEntry(ArenaQuestId, ArenaQuestTitle, details, "Level2-Floor-1");
        if (arenaQuestStage == ArenaQuestStage.Claimed) CompleteEntry(ArenaQuestId);
    }
}
