using System;

// Copy migrations run only when a journal entry is created, updated or restored.
// Exact paragraph/line boundaries protect custom text and appended progress evidence.
public sealed partial class QuestJournalManager
{
    public const string Level2SandwormDescription = "城中公告记载，一名携带试炼证明的冒险者被沙虫吞下。击杀沙虫后可以取得这份证明，用于开启试炼大门。\n\n当前目标：前往城外沙漠中央，击杀沙虫并拾取试炼证明。";
    public const string SandwormBaitDetails = "诡异商人可以制作诱饵炸弹，但缺少合成工具。诱饵炸弹能引诱沙虫吞食，并在其体内爆炸。\n\n当前目标：前往城镇要塞取得合成工具，交给诡异商人。";
    public const string SandwormBaitCompletedDetails = "已将合成工具交给诡异商人，可以向他购买诱饵炸弹。\n\n购买方式：支付6金币，或提交1个普通炸弹和1金币。";
    public const string SandwormMortarCompletedDetails = "远程控制装置已安装，已取得配套遥控器。\n\n遥控器只能在城外沙漠使用。炮弹会落在发出信号时的位置，使用后需要及时离开落点。炮击可将地下的沙虫炸出。";
    public const string CounterfeitQuestDetails = "赝品工匠说明，试炼大门的祭坛通过金色宝石识别试炼证明，可以使用金色宝石制作同样有效的赝品。\n\n当前目标：前往行商市场二层，向穿深蓝色衣服的宝石商人询问金色宝石，并取得一颗交给赝品工匠。";
    public const string CounterfeitCompletedDetails = "已交付金色宝石并获得赝品试炼证明。\n\n将赝品供奉在试炼大门的祭坛上，即可打开通往试炼之地的大门。";
    public const string ArenaInjectionDescription = "向注液器注入药水，可以改变相连水池施加的效果。竞技场左侧水池供挑战者使用，右侧水池影响出场守护者的效果。\n\n当前目标：在物品栏选中任意药水，向竞技场旁任意一台注液器注入一瓶。";

    private readonly struct QuestDescriptionRevision
    {
        public readonly string Id, Previous, Replacement;
        public QuestDescriptionRevision(string id, string previous, string replacement)
        { Id = id; Previous = previous; Replacement = replacement; }
    }

    private static readonly QuestDescriptionRevision[] DescriptionRevisions = {
        new QuestDescriptionRevision("level0.escape_prison", "原本的肉体已不再听从我的意志，我只能以游魂的形态寻找出路。监狱的高墙与铁门依旧森严，但守卫认得的是面孔，未必认得藏在其中的意识。\n\n借用另一具身体，避开守卫的注意，寻找离开这里的机会。\n\n当前目标：设法从大门逃出监狱。", Level0EscapeDescription),
        new QuestDescriptionRevision("level0.obtain_gate_key", "墙上的通知说，离开监狱必须先向典狱长报备，经批准后才能领取大门钥匙。门就在眼前，自由却要等一个点头。\n\n钥匙目前放在典狱长的房间里。与其站在门前等待许可，不如亲自寻找出路——毕竟，能打开这扇门的是钥匙，而不是批准。\n\n当前目标：设法进入典狱长的房间，取得监狱大门钥匙。", Level0GateKeyDescription),
        new QuestDescriptionRevision("level1.escape_castle", "监狱已被甩在身后，城堡的大门却又拦住了去路。换过几张面孔，绕过几道关卡，离外面的世界似乎只差最后一扇门。\n\n总有办法让它打开。至于城堡里的诸位——假如再也见不到，就把这当作告别吧。\n\n当前目标：设法从大门逃出城堡。", Level1EscapeDescription),
        new QuestDescriptionRevision("level1.craft_super_bomb", "一封密信留下了一场刺杀阴谋的后手：集齐[引信]、[超级炸弹外壳]与[魔力火药]，就能造出足以炸毁城门的超级炸弹。\n\n写信的人打算送国王上天。我只需要让城门让路——希望这份配方比他们上一次的计划可靠。\n\n当前目标：收集三种材料，在信件旁的工作台合成超级炸弹。", Level1BombDescription),
        new QuestDescriptionRevision("level1.open_monster_cage", "城堡用钥匙与密码给巨兽上了双重保险，却没能锁住它撕裂城墙的力量。守卫眼中的危险，也许正是我离开的机会。\n\n先找到[牢笼密码]与[怪物牢笼钥匙]，解除束缚，再设法附身它。只要能接管这具庞大的身体，坚固的大门也未必还是一道难题。\n\n当前目标：打开双重锁，附身被囚禁的巨兽。", Level1MonsterDescription),
        new QuestDescriptionRevision("level1.find_castle_gate_key", "城堡的大门紧锁着，但锁存在，就意味着有人握着让它开启的答案。\n\n不必把门炸上天，也不必请巨兽代劳。找到[大门钥匙]，让齿纹与锁芯对上，这道难题或许只需要轻轻一转。\n\n当前目标：设法找到城堡大门钥匙。", Level1GateKeyDescription),
        new QuestDescriptionRevision("level2.hero_proof", "勇者为了磨炼自己、获得更强的力量，已经踏入边境城市的试炼之地。若想追上他的脚步，我也必须设法进入那里。", HeroProofIntroduction),
        new QuestDescriptionRevision("level2.hero_proof", "已抵达边境城市。试炼大门仍挡在前方，需要找到开启它的方法。\n当前目标：打开试炼大门，并通过大门后的入口进入试炼之地。", "已抵达边境城市，需要找到开启试炼大门的方法。\n当前目标：打开试炼大门，通过场景入口进入试炼之地。"),
        new QuestDescriptionRevision("level2.hero_proof", "已打开通往试炼之地的道路，并踏入其中。勇者就在前方，我必须继续追上他。", "已打开试炼大门并进入试炼之地。"),
        new QuestDescriptionRevision("level2.sandworm", "城中的告示提到，一名持有试炼资格的冒险者已被沙虫吞噬，他随身的试炼证明也落入了巨兽腹中。若能设法击杀沙虫，或许就能取回那件证明，打开通往试炼之地的大门。\n\n当前目标：前往城外沙漠中央，设法击杀沙虫，并拾取它吞下的试炼证明。", Level2SandwormDescription),
        new QuestDescriptionRevision("level2_strange_merchant_synthesis_tools", "诡异商人说，击败城外的沙虫需要让它吞下诱饵炸弹。他可以制作炸弹，但需要一套合成工具。前往城镇的要塞取回合成工具，再交给诡异商人。", SandwormBaitDetails),
        new QuestDescriptionRevision("level2_strange_merchant_synthesis_tools", "已将合成工具交给诡异商人。他现在可以制作诱饵炸弹：每颗6金币，或用1个普通炸弹加1金币改造。", SandwormBaitCompletedDetails),
        new QuestDescriptionRevision("level2.sandworm.mortar", "大工程师设计了一套臼炮遥控装置，能够把城墙上的炮火引向沙漠，将沙虫从地下炸出。守军不愿充当炮弹落点的活靶子，我却接下了这个危险的试验。\n\n当前目标：从工程师旁的保险柜取得远程控制装置（密码：1551），前往城墙西南角城楼，将其安装在臼炮上。", SandwormMortarDetails),
        new QuestDescriptionRevision("level2.sandworm.mortar", "远程控制装置已经安装在城墙西南角的臼炮上，配套的遥控器也已到手。\n\n前往城外沙漠，在沙虫靠近时发出信号，然后尽快离开原地。炮击瞄准的是按下遥控器那一刻的位置——人肉信标最好不要等到炮弹落下。", SandwormMortarCompletedDetails),
        new QuestDescriptionRevision("level2_counterfeit_trial_proof", "赝品工匠透露，试炼大门的祭坛真正检测的是证明中的金色宝石。前往行商市场二层，向穿深蓝色衣服的宝石商人打听金色宝石，再将一颗宝石交给工匠制作赝品试炼证明。", CounterfeitQuestDetails),
        new QuestDescriptionRevision("level2_counterfeit_trial_proof", "已将金色宝石交给赝品工匠，获得赝品试炼证明。将它供奉在试炼大门的祭坛上，同样能够打开通往试炼之地的大门。", CounterfeitCompletedDetails),
        new QuestDescriptionRevision("level2.summoning", "召唤需知中记载，通往域外的门需要六把钥匙，而每把钥匙都是一个指定之人的灵魂：城中的大主教、穿金色服装的守卫士官、赫赫有名的大厨、大工程师、诡异商人，以及来自遥远之地的隐居剑客。\n\n这些人必须在法阵的矩形范围内死亡，灵魂才会被水晶吸收；在别处杀死他们无法完成仪式。六枚水晶尽数亮起，域外来客便会现身于法阵中央。", SummoningQuestIntroduction),
        new QuestDescriptionRevision("level2.summoning", "召唤需知中记载，通往域外的门需要六把钥匙，而钥匙便是六名指定之人的灵魂。须在法阵的矩形范围内献上他们的灵魂，点亮六枚水晶，迎来域外来客。", SummoningQuestIntroduction),
        new QuestDescriptionRevision("level2.summoning", "当前目标：在召唤法阵内献上六名指定之人的灵魂，点亮六枚水晶并完成召唤。", "当前目标：使六名指定角色在法阵范围内死亡，点亮全部水晶并完成召唤。"),
        new QuestDescriptionRevision("level2.summoning", "六枚水晶已全部点亮，域外来客已经在法阵中央现身。仪式完成。", "六枚水晶已全部点亮，域外来客已被召唤至法阵中央。"),
        new QuestDescriptionRevision("level2.arena", "竞技场的报名官许诺：逐一击败守护者，便能获得通往试炼之地的证明。这里的胜负以一方生命值降至一点为准，不必杀死对手。\n\n胜利属于报名参赛的那具身体，证明也只能由获胜者本人领取；若在领奖前失去这具身体，就必须重新证明自己。", ArenaIntroduction),
        new QuestDescriptionRevision("level2.arena", "再次报名会重新计算本次挑战的战绩，已消耗的道具和体力不会返还。", "再次报名会重置本次挑战战绩。挑战中消耗的道具和生命值不会恢复。"),
        new QuestDescriptionRevision("level2.arena", "我已赢下竞技场的试炼，并拿到了试炼证明。报名官已关闭挑战，不再重复颁发奖励。", "已完成竞技场挑战并领取试炼证明，无法再次报名。"),
        new QuestDescriptionRevision("level2.arena", "当前目标：原获胜角色已死亡，换一具身体重新报名。", "当前目标：原获胜角色已死亡，使用其他角色重新报名。"),
        new QuestDescriptionRevision("level2.arena", "当前目标：本轮失利，返回报名官处重新报名。", "当前目标：本轮挑战失败，返回报名官处重新报名。"),
        new QuestDescriptionRevision("level2.arena.injection", "注液器说明揭示了竞技场水池的另一种用途：向装置注入药水，就能改变相连水池施加的效果。左侧水池供挑战者休整，右侧水池供守护者备战；也许，胜负并不只取决于场内的刀剑。\n\n当前目标：选中任意药水，向竞技场旁任意一台注液器注入一瓶，试着改变池水的效果。", ArenaInjectionDescription),
    };

    private static string UpgradeAuthoredQuestDescriptions(string entryId, string details)
    {
        if (string.IsNullOrEmpty(details)) return details;
        foreach (var revision in DescriptionRevisions)
        {
            if (!string.Equals(entryId, revision.Id, StringComparison.Ordinal)) continue;
            int start = details.IndexOf(revision.Previous, StringComparison.Ordinal);
            if (start < 0 || (start > 0 && details[start - 1] != '\n')) continue;
            int end = start + revision.Previous.Length;
            if (!revision.Previous.EndsWith("。", StringComparison.Ordinal) &&
                end < details.Length && details[end] == '。') end++;
            if (end < details.Length && details[end] != '\r' && details[end] != '\n') continue;
            details = details.Substring(0, start) + revision.Replacement + details.Substring(end);
        }
        return details;
    }
}
