using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Build-time authored totals. Never loads other gameplay scenes to count collectibles.</summary>
public sealed class LevelSettlementCatalog : ScriptableObject
{
    [Serializable] public sealed class Level
    {
        public string title, entryScene, nextScene;
        public int skillPoints, inventoryExpansions;
        public string[] characterSkills = Array.Empty<string>();
    }
    public Font font;
    public MemoPickupItemVisual expansionVisual;
    public Level[] levels = new Level[3];
    public static int LevelIndex(string scene)
    {
        if (scene == "Level0") return 0;
        if (scene != null && scene.StartsWith("Level1-", StringComparison.OrdinalIgnoreCase)) return 1;
        if (scene != null && scene.StartsWith("Level2-", StringComparison.OrdinalIgnoreCase)) return 2;
        return -1;
    }
    public struct Quest
    {
        public string id, title;
        public int depth;
        public Quest(string id, string title, int depth = 0) { this.id=id; this.title=title; this.depth=depth; }
    }
    // Full authored quest list, including undiscovered branches; password notes are not quests.
    public static List<Quest> Quests(int level)
    {
        if (level == 0) return new List<Quest> {
            new Quest("level0.escape_prison", QuestJournalManager.Level0EscapeTitle),
            new Quest("level0.obtain_gate_key", QuestJournalManager.Level0GateKeyTitle) };
        if (level == 1) return new List<Quest> {
            new Quest("level1.escape_castle", QuestJournalManager.Level1EscapeTitle),
            new Quest("level1.craft_super_bomb", QuestJournalManager.Level1BombTitle),
            new Quest("level1.open_monster_cage", QuestJournalManager.Level1MonsterTitle),
            new Quest("level1.find_castle_gate_key", QuestJournalManager.Level1GateKeyTitle) };
        return new List<Quest> {
            new Quest(QuestJournalManager.Level2HeroProofId, "英雄之证"),
            new Quest(QuestJournalManager.Level2SandwormQuestId, QuestJournalManager.Level2SandwormQuestTitle),
            new Quest(QuestJournalManager.SandwormBaitQuestId, QuestJournalManager.SandwormBaitTitle, 1),
            new Quest(QuestJournalManager.SandwormMortarQuestId, QuestJournalManager.SandwormMortarTitle, 1),
            new Quest(QuestJournalManager.ArenaQuestId, QuestJournalManager.ArenaQuestTitle),
            new Quest(QuestJournalManager.ArenaInjectionQuestId, QuestJournalManager.ArenaInjectionQuestTitle, 1),
            new Quest(QuestJournalManager.CounterfeitQuestId, QuestJournalManager.CounterfeitQuestTitle),
            new Quest(QuestJournalManager.SummoningQuestId, QuestJournalManager.SummoningQuestTitle),
            new Quest(QuestJournalManager.FortressProofQuestId, QuestJournalManager.FortressProofQuestTitle),
            new Quest(QuestJournalManager.KnightAmbushQuestId, QuestJournalManager.KnightAmbushQuestTitle, 1),
            new Quest(QuestJournalManager.GravekeeperQuestId, QuestJournalManager.GravekeeperQuestTitle) };
    }
}
