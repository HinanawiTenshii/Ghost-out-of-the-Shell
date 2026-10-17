public sealed partial class QuestJournalManager
{
    public const string GravekeeperQuestId = "level2.gravekeeper";
    public const string GravekeeperQuestTitle = "有时候，死了更好";
    public const string GravekeeperCrystalTarget = "level2.gravekeeper.crystal";
    public const string GravekeeperReturnTarget = "level2.gravekeeper.return";
    public const string GravekeeperIntroduction = "守墓人说，埋骨地的镇抚水晶受到污染，导致死者不断复活。他愿意用教堂二楼圣物间的密码，换取水晶被摧毁的消息。";
    public const string GravekeeperAcceptedDetails = GravekeeperIntroduction + "\n\n当前目标：摧毁埋骨地被污染的镇抚水晶。";
    public const string GravekeeperCompletedDetails = "已摧毁埋骨地被污染的镇抚水晶，并向守墓人复命。教堂二楼圣物间的密码为7028，已记入已知密码。获取圣物还需要圣物间钥匙。";
    private bool graveyardCrystalDestroyed;
    public bool GraveyardCrystalDestroyed => graveyardCrystalDestroyed;

    // Evidence is saved even if the player destroys the crystal before meeting the keeper.
    public void ReportGraveyardCrystalDestroyed()
    {
        if (graveyardCrystalDestroyed) return;
        graveyardCrystalDestroyed = true;
        RefreshGravekeeperQuest();
    }

    private void RefreshGravekeeperQuest()
    {
        if (!TryGetEntry(GravekeeperQuestId, out var entry) || IsEntryCompleted(GravekeeperQuestId)) return;
        string details = graveyardCrystalDestroyed
            ? GravekeeperIntroduction + "\n\n被污染的镇抚水晶已被摧毁。\n\n当前目标：向守墓人复命，取得圣物间密码。"
            : GravekeeperAcceptedDetails;
        if (entry.Details != details)
            AddOrUpdateEntry(GravekeeperQuestId, GravekeeperQuestTitle, details, entry.SourceScene);
    }
}
