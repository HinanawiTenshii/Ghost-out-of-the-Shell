using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Run in the isolated smoke project. Fixture strings intentionally retain the previous copy.
[InitializeOnLoad]
public static class QuestDescriptionCopySmoke
{
    const string Key = nameof(QuestDescriptionCopySmoke);
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static int checks;
    static readonly string[][] Revisions = {
        new[] { "level0.escape_prison", "原本的肉体已不再听从我的意志，我只能以游魂的形态寻找出路。监狱的高墙与铁门依旧森严，但守卫认得的是面孔，未必认得藏在其中的意识。\n\n借用另一具身体，避开守卫的注意，寻找离开这里的机会。\n\n当前目标：设法从大门逃出监狱。", "原本的肉体已失去控制，目前只能以游魂形态行动。需要附身其他角色，寻找离开监狱的方法。\n\n当前目标：设法从大门逃出监狱。" },
        new[] { "level0.obtain_gate_key", "墙上的通知说，离开监狱必须先向典狱长报备，经批准后才能领取大门钥匙。门就在眼前，自由却要等一个点头。\n\n钥匙目前放在典狱长的房间里。与其站在门前等待许可，不如亲自寻找出路——毕竟，能打开这扇门的是钥匙，而不是批准。\n\n当前目标：设法进入典狱长的房间，取得监狱大门钥匙。", "监狱规定，离开前必须向典狱长报备，经批准后才能领取大门钥匙。钥匙目前存放在典狱长的房间内。\n\n当前目标：进入典狱长的房间，取得监狱大门钥匙。" },
        new[] { "level1.escape_castle", "监狱已被甩在身后，城堡的大门却又拦住了去路。换过几张面孔，绕过几道关卡，离外面的世界似乎只差最后一扇门。\n\n总有办法让它打开。至于城堡里的诸位——假如再也见不到，就把这当作告别吧。\n\n当前目标：设法从大门逃出城堡。", "已离开监狱，目前被困在城堡内。城堡大门处于关闭状态，需要找到开启或破坏大门的方法。\n\n当前目标：从大门离开城堡。" },
        new[] { "level1.craft_super_bomb", "一封密信留下了一场刺杀阴谋的后手：集齐[引信]、[超级炸弹外壳]与[魔力火药]，就能造出足以炸毁城门的超级炸弹。\n\n写信的人打算送国王上天。我只需要让城门让路——希望这份配方比他们上一次的计划可靠。\n\n当前目标：收集三种材料，在信件旁的工作台合成超级炸弹。", "信件记载了超级炸弹的制作方法。需要收集[引信]、[超级炸弹外壳]和[魔力火药]，并在信件旁的工作台完成合成。超级炸弹可用于破坏城堡大门。\n\n当前目标：收集三种材料，在工作台合成超级炸弹。" },
        new[] { "level1.open_monster_cage", "城堡用钥匙与密码给巨兽上了双重保险，却没能锁住它撕裂城墙的力量。守卫眼中的危险，也许正是我离开的机会。\n\n先找到[牢笼密码]与[怪物牢笼钥匙]，解除束缚，再设法附身它。只要能接管这具庞大的身体，坚固的大门也未必还是一道难题。\n\n当前目标：打开双重锁，附身被囚禁的巨兽。", "城堡内的巨兽曾破坏过城墙，可能具备破坏大门的能力。它被钥匙锁与密码锁束缚，需要[怪物牢笼钥匙]和[牢笼密码]才能解除。\n\n当前目标：找到钥匙和密码，解除束缚并附身巨兽。" },
        new[] { "level1.find_castle_gate_key", "城堡的大门紧锁着，但锁存在，就意味着有人握着让它开启的答案。\n\n不必把门炸上天，也不必请巨兽代劳。找到[大门钥匙]，让齿纹与锁芯对上，这道难题或许只需要轻轻一转。\n\n当前目标：设法找到城堡大门钥匙。", "城堡大门已上锁，需要对应的[大门钥匙]才能正常开启。\n\n当前目标：找到城堡大门钥匙。" },
        new[] { "level2.hero_proof", "勇者为了磨炼自己、获得更强的力量，已经踏入边境城市的试炼之地。若想追上他的脚步，我也必须设法进入那里。", "勇者已前往边境城市的试炼之地提升能力。需要先抵达边境城市，再进入试炼之地寻找勇者。" },
        new[] { "level2.hero_proof", "已抵达边境城市。试炼大门仍挡在前方，需要找到开启它的方法。\n当前目标：打开试炼大门，并通过大门后的入口进入试炼之地。", "已抵达边境城市，需要找到开启试炼大门的方法。\n当前目标：打开试炼大门，通过场景入口进入试炼之地。" },
        new[] { "level2.hero_proof", "已打开通往试炼之地的道路，并踏入其中。勇者就在前方，我必须继续追上他。", "已打开试炼大门并进入试炼之地。" },
        new[] { "level2.sandworm", "城中的告示提到，一名持有试炼资格的冒险者已被沙虫吞噬，他随身的试炼证明也落入了巨兽腹中。若能设法击杀沙虫，或许就能取回那件证明，打开通往试炼之地的大门。\n\n当前目标：前往城外沙漠中央，设法击杀沙虫，并拾取它吞下的试炼证明。", "城中公告记载，一名携带试炼证明的冒险者被沙虫吞下。击杀沙虫后可以取得这份证明，用于开启试炼大门。\n\n当前目标：前往城外沙漠中央，击杀沙虫并拾取试炼证明。" },
        new[] { "level2_strange_merchant_synthesis_tools", "诡异商人说，击败城外的沙虫需要让它吞下诱饵炸弹。他可以制作炸弹，但需要一套合成工具。前往城镇的要塞取回合成工具，再交给诡异商人。", "诡异商人可以制作诱饵炸弹，但缺少合成工具。诱饵炸弹能引诱沙虫吞食，并在其体内爆炸。\n\n当前目标：前往城镇要塞取得合成工具，交给诡异商人。" },
        new[] { "level2_strange_merchant_synthesis_tools", "已将合成工具交给诡异商人。他现在可以制作诱饵炸弹：每颗6金币，或用1个普通炸弹加1金币改造。", "已将合成工具交给诡异商人，可以向他购买诱饵炸弹。\n\n购买方式：支付6金币，或提交1个普通炸弹和1金币。" },
        new[] { "level2.sandworm.mortar", "大工程师设计了一套臼炮遥控装置，能够把城墙上的炮火引向沙漠，将沙虫从地下炸出。守军不愿充当炮弹落点的活靶子，我却接下了这个危险的试验。\n\n当前目标：从工程师旁的保险柜取得远程控制装置（密码：1551），前往城墙西南角城楼，将其安装在臼炮上。", "大工程师的远程控制装置可安装在城墙臼炮上，用于从沙漠呼叫炮击。炮击可将沙虫从地下炸出。\n\n当前目标：从工程师旁的保险柜取得远程控制装置（密码：1551），将其安装在城墙西南角城楼的臼炮上。" },
        new[] { "level2.sandworm.mortar", "远程控制装置已经安装在城墙西南角的臼炮上，配套的遥控器也已到手。\n\n前往城外沙漠，在沙虫靠近时发出信号，然后尽快离开原地。炮击瞄准的是按下遥控器那一刻的位置——人肉信标最好不要等到炮弹落下。", "远程控制装置已安装，已取得配套遥控器。\n\n遥控器只能在城外沙漠使用。炮弹会落在发出信号时的位置，使用后需要及时离开落点。炮击可将地下的沙虫炸出。" },
        new[] { "level2_counterfeit_trial_proof", "赝品工匠透露，试炼大门的祭坛真正检测的是证明中的金色宝石。前往行商市场二层，向穿深蓝色衣服的宝石商人打听金色宝石，再将一颗宝石交给工匠制作赝品试炼证明。", "赝品工匠说明，试炼大门的祭坛通过金色宝石识别试炼证明，可以使用金色宝石制作同样有效的赝品。\n\n当前目标：前往行商市场二层，向穿深蓝色衣服的宝石商人询问金色宝石，并取得一颗交给赝品工匠。" },
        new[] { "level2_counterfeit_trial_proof", "已将金色宝石交给赝品工匠，获得赝品试炼证明。将它供奉在试炼大门的祭坛上，同样能够打开通往试炼之地的大门。", "已交付金色宝石并获得赝品试炼证明。\n\n将赝品供奉在试炼大门的祭坛上，即可打开通往试炼之地的大门。" },
        new[] { "level2.summoning", "召唤需知中记载，通往域外的门需要六把钥匙，而每把钥匙都是一个指定之人的灵魂：城中的大主教、穿金色服装的守卫士官、赫赫有名的大厨、大工程师、诡异商人，以及来自遥远之地的隐居剑客。\n\n这些人必须在法阵的矩形范围内死亡，灵魂才会被水晶吸收；在别处杀死他们无法完成仪式。六枚水晶尽数亮起，域外来客便会现身于法阵中央。", "召唤仪式需要六名指定角色的灵魂：大主教、穿金色服装的守卫士官、大厨、大工程师、诡异商人和隐居剑客。\n\n这些角色必须在法阵的矩形范围内死亡，才能由水晶吸收灵魂。六枚水晶全部点亮后，法阵中央会召唤域外来客。" },
        new[] { "level2.summoning", "召唤需知中记载，通往域外的门需要六把钥匙，而钥匙便是六名指定之人的灵魂。须在法阵的矩形范围内献上他们的灵魂，点亮六枚水晶，迎来域外来客。", "召唤仪式需要六名指定角色的灵魂：大主教、穿金色服装的守卫士官、大厨、大工程师、诡异商人和隐居剑客。\n\n这些角色必须在法阵的矩形范围内死亡，才能由水晶吸收灵魂。六枚水晶全部点亮后，法阵中央会召唤域外来客。" },
        new[] { "level2.summoning", "当前目标：在召唤法阵内献上六名指定之人的灵魂，点亮六枚水晶并完成召唤。", "当前目标：使六名指定角色在法阵范围内死亡，点亮全部水晶并完成召唤。" },
        new[] { "level2.summoning", "六枚水晶已全部点亮，域外来客已经在法阵中央现身。仪式完成。", "六枚水晶已全部点亮，域外来客已被召唤至法阵中央。" },
        new[] { "level2.arena", "竞技场的报名官许诺：逐一击败守护者，便能获得通往试炼之地的证明。这里的胜负以一方生命值降至一点为准，不必杀死对手。\n\n胜利属于报名参赛的那具身体，证明也只能由获胜者本人领取；若在领奖前失去这具身体，就必须重新证明自己。", "报名参加竞技场挑战后，需要依次击败所有出场的守护者。任意一方的生命值降至1点时，该方本轮挑战失败。\n\n全部获胜后，必须由参赛角色本人领取试炼证明。若该角色在领奖前死亡，需要重新报名挑战。领取证明后无法再次挑战。" },
        new[] { "level2.arena", "再次报名会重新计算本次挑战的战绩，已消耗的道具和体力不会返还。", "再次报名会重置本次挑战战绩。挑战中消耗的道具和生命值不会恢复。" },
        new[] { "level2.arena", "我已赢下竞技场的试炼，并拿到了试炼证明。报名官已关闭挑战，不再重复颁发奖励。", "已完成竞技场挑战并领取试炼证明，无法再次报名。" },
        new[] { "level2.arena", "当前目标：原获胜角色已死亡，换一具身体重新报名。", "当前目标：原获胜角色已死亡，使用其他角色重新报名。" },
        new[] { "level2.arena", "当前目标：本轮失利，返回报名官处重新报名。", "当前目标：本轮挑战失败，返回报名官处重新报名。" },
        new[] { "level2.arena.injection", "注液器说明揭示了竞技场水池的另一种用途：向装置注入药水，就能改变相连水池施加的效果。左侧水池供挑战者休整，右侧水池供守护者备战；也许，胜负并不只取决于场内的刀剑。\n\n当前目标：选中任意药水，向竞技场旁任意一台注液器注入一瓶，试着改变池水的效果。", "向注液器注入药水，可以改变相连水池施加的效果。竞技场左侧水池供挑战者使用，右侧水池影响出场守护者的效果。\n\n当前目标：在物品栏选中任意药水，向竞技场旁任意一台注液器注入一瓶。" },
    };
    static readonly string[] Ids = { "level0.escape_prison", "level0.obtain_gate_key", "level1.escape_castle", "level1.craft_super_bomb", "level1.open_monster_cage", "level1.find_castle_gate_key", "level2.hero_proof", "level2.sandworm", "level2_strange_merchant_synthesis_tools", "level2.sandworm.mortar", "level2_counterfeit_trial_proof", "level2.summoning", "level2.arena", "level2.arena.injection" };
    static readonly string[] Titles = { "只有死人才能离开", "在法的门前", "假如再也见不到你", "数到三，不许数到五", "最后的破坏者", "制钥师的答案", "英雄之证", "香料与獠牙", "它不喜欢烟雾", "→→↑", "假作真时", "六把钥匙", "你们还不满意吗？", "为你的健康干杯" };
    static readonly string[][] SceneLines = {
        new[] { "Assets/Scenes/Level0.unity", "      value: \"\\u76D1\\u72F1\\u89C4\\u5B9A\\uFF0C\\u79BB\\u5F00\\u524D\\u5FC5\\u987B\\u5411\\u5178\\u72F1\\u957F\\u62A5\\u5907\\uFF0C\\u7ECF\\u6279\\u51C6\\u540E\\u624D\\u80FD\\u9886\\u53D6\\u5927\\u95E8\\u94A5\\u5319\\u3002\\u94A5\\u5319\\u76EE\\u524D\\u5B58\\u653E\\u5728\\u5178\\u72F1\\u957F\\u7684\\u623F\\u95F4\\u5185\\u3002\\n\\n\\u5F53\\u524D\\u76EE\\u6807\\uFF1A\\u8FDB\\u5165\\u5178\\u72F1\\u957F\\u7684\\u623F\\u95F4\\uFF0C\\u53D6\\u5F97\\u76D1\\u72F1\\u5927\\u95E8\\u94A5\\u5319\\u3002\"" },
        new[] { "Assets/Scenes/Level0.unity", "    details: \"\\u539F\\u672C\\u7684\\u8089\\u4F53\\u5DF2\\u5931\\u53BB\\u63A7\\u5236\\uFF0C\\u76EE\\u524D\\u53EA\\u80FD\\u4EE5\\u6E38\\u9B42\\u5F62\\u6001\\u884C\\u52A8\\u3002\\u9700\\u8981\\u9644\\u8EAB\\u5176\\u4ED6\\u89D2\\u8272\\uFF0C\\u5BFB\\u627E\\u79BB\\u5F00\\u76D1\\u72F1\\u7684\\u65B9\\u6CD5\\u3002\\n\\n\\u5F53\\u524D\\u76EE\\u6807\\uFF1A\\u8BBE\\u6CD5\\u4ECE\\u5927\\u95E8\\u9003\\u51FA\\u76D1\\u72F1\\u3002\"" },
        new[] { "Assets/Scenes/Level1/Level1-Garden.unity", "      value: \"\\u57CE\\u5821\\u5927\\u95E8\\u5DF2\\u4E0A\\u9501\\uFF0C\\u9700\\u8981\\u5BF9\\u5E94\\u7684[\\u5927\\u95E8\\u94A5\\u5319]\\u624D\\u80FD\\u6B63\\u5E38\\u5F00\\u542F\\u3002\\n\\n\\u5F53\\u524D\\u76EE\\u6807\\uFF1A\\u627E\\u5230\\u57CE\\u5821\\u5927\\u95E8\\u94A5\\u5319\\u3002\"" },
        new[] { "Assets/Scenes/Level1/Level1-Garden.unity", "    details: \"\\u5DF2\\u79BB\\u5F00\\u76D1\\u72F1\\uFF0C\\u76EE\\u524D\\u88AB\\u56F0\\u5728\\u57CE\\u5821\\u5185\\u3002\\u57CE\\u5821\\u5927\\u95E8\\u5904\\u4E8E\\u5173\\u95ED\\u72B6\\u6001\\uFF0C\\u9700\\u8981\\u627E\\u5230\\u5F00\\u542F\\u6216\\u7834\\u574F\\u5927\\u95E8\\u7684\\u65B9\\u6CD5\\u3002\\n\\n\\u5F53\\u524D\\u76EE\\u6807\\uFF1A\\u4ECE\\u5927\\u95E8\\u79BB\\u5F00\\u57CE\\u5821\\u3002\"" },
        new[] { "Assets/Scenes/Level1/Level1-Floor-1.unity", "      value: \"\\u4FE1\\u4EF6\\u8BB0\\u8F7D\\u4E86\\u8D85\\u7EA7\\u70B8\\u5F39\\u7684\\u5236\\u4F5C\\u65B9\\u6CD5\\u3002\\u9700\\u8981\\u6536\\u96C6[\\u5F15\\u4FE1]\\u3001[\\u8D85\\u7EA7\\u70B8\\u5F39\\u5916\\u58F3]\\u548C[\\u9B54\\u529B\\u706B\\u836F]\\uFF0C\\u5E76\\u5728\\u4FE1\\u4EF6\\u65C1\\u7684\\u5DE5\\u4F5C\\u53F0\\u5B8C\\u6210\\u5408\\u6210\\u3002\\u8D85\\u7EA7\\u70B8\\u5F39\\u53EF\\u7528\\u4E8E\\u7834\\u574F\\u57CE\\u5821\\u5927\\u95E8\\u3002\\n\\n\\u5F53\\u524D\\u76EE\\u6807\\uFF1A\\u6536\\u96C6\\u4E09\\u79CD\\u6750\\u6599\\uFF0C\\u5728\\u5DE5\\u4F5C\\u53F0\\u5408\\u6210\\u8D85\\u7EA7\\u70B8\\u5F39\\u3002\"" },
        new[] { "Assets/Scenes/Level2/Level2-Floor-1.unity", "    details: \"\\u5927\\u5DE5\\u7A0B\\u5E08\\u7684\\u8FDC\\u7A0B\\u63A7\\u5236\\u88C5\\u7F6E\\u53EF\\u5B89\\u88C5\\u5728\\u57CE\\u5899\\u81FC\\u70AE\\u4E0A\\uFF0C\\u7528\\u4E8E\\u4ECE\\u6C99\\u6F20\\u547C\\u53EB\\u70AE\\u51FB\\u3002\\u70AE\\u51FB\\u53EF\\u5C06\\u6C99\\u866B\\u4ECE\\u5730\\u4E0B\\u70B8\\u51FA\\u3002\\n\\n\\u5F53\\u524D\\u76EE\\u6807\\uFF1A\\u4ECE\\u5DE5\\u7A0B\\u5E08\\u65C1\\u7684\\u4FDD\\u9669\\u67DC\\u53D6\\u5F97\\u8FDC\\u7A0B\\u63A7\\u5236\\u88C5\\u7F6E\\uFF08\\u5BC6\\u7801\\uFF1A1551\\uFF09\\uFF0C\\u5C06\\u5176\\u5B89\\u88C5\\u5728\\u57CE\\u5899\\u897F\\u5357\\u89D2\\u57CE\\u697C\\u7684\\u81FC\\u70AE\\u4E0A\\u3002\"" },
        new[] { "Assets/Scenes/Level2/Level2-Floor-1.unity", "      value: \"\\u5411\\u6CE8\\u6DB2\\u5668\\u6CE8\\u5165\\u836F\\u6C34\\uFF0C\\u53EF\\u4EE5\\u6539\\u53D8\\u76F8\\u8FDE\\u6C34\\u6C60\\u65BD\\u52A0\\u7684\\u6548\\u679C\\u3002\\u7ADE\\u6280\\u573A\\u5DE6\\u4FA7\\u6C34\\u6C60\\u4F9B\\u6311\\u6218\\u8005\\u4F7F\\u7528\\uFF0C\\u53F3\\u4FA7\\u6C34\\u6C60\\u5F71\\u54CD\\u51FA\\u573A\\u5B88\\u62A4\\u8005\\u7684\\u6548\\u679C\\u3002\\n\\n\\u5F53\\u524D\\u76EE\\u6807\\uFF1A\\u5728\\u7269\\u54C1\\u680F\\u9009\\u4E2D\\u4EFB\\u610F\\u836F\\u6C34\\uFF0C\\u5411\\u7ADE\\u6280\\u573A\\u65C1\\u4EFB\\u610F\\u4E00\\u53F0\\u6CE8\\u6DB2\\u5668\\u6CE8\\u5165\\u4E00\\u74F6\\u3002\"" },
        new[] { "Assets/Scenes/Level1/Level1-Floor1.unity", "      value: \"\\u57CE\\u5821\\u5185\\u7684\\u5DE8\\u517D\\u66FE\\u7834\\u574F\\u8FC7\\u57CE\\u5899\\uFF0C\\u53EF\\u80FD\\u5177\\u5907\\u7834\\u574F\\u5927\\u95E8\\u7684\\u80FD\\u529B\\u3002\\u5B83\\u88AB\\u94A5\\u5319\\u9501\\u4E0E\\u5BC6\\u7801\\u9501\\u675F\\u7F1A\\uFF0C\\u9700\\u8981[\\u602A\\u7269\\u7262\\u7B3C\\u94A5\\u5319]\\u548C[\\u7262\\u7B3C\\u5BC6\\u7801]\\u624D\\u80FD\\u89E3\\u9664\\u3002\\n\\n\\u5F53\\u524D\\u76EE\\u6807\\uFF1A\\u627E\\u5230\\u94A5\\u5319\\u548C\\u5BC6\\u7801\\uFF0C\\u89E3\\u9664\\u675F\\u7F1A\\u5E76\\u9644\\u8EAB\\u5DE8\\u517D\\u3002\"" },
        new[] { "Assets/Scenes/Level1/Level1-Floor3.unity", "      value: \"\\u57CE\\u5821\\u5185\\u7684\\u5DE8\\u517D\\u66FE\\u7834\\u574F\\u8FC7\\u57CE\\u5899\\uFF0C\\u53EF\\u80FD\\u5177\\u5907\\u7834\\u574F\\u5927\\u95E8\\u7684\\u80FD\\u529B\\u3002\\u5B83\\u88AB\\u94A5\\u5319\\u9501\\u4E0E\\u5BC6\\u7801\\u9501\\u675F\\u7F1A\\uFF0C\\u9700\\u8981[\\u602A\\u7269\\u7262\\u7B3C\\u94A5\\u5319]\\u548C[\\u7262\\u7B3C\\u5BC6\\u7801]\\u624D\\u80FD\\u89E3\\u9664\\u3002\\n\\n\\u5F53\\u524D\\u76EE\\u6807\\uFF1A\\u627E\\u5230\\u94A5\\u5319\\u548C\\u5BC6\\u7801\\uFF0C\\u89E3\\u9664\\u675F\\u7F1A\\u5E76\\u9644\\u8EAB\\u5DE8\\u517D\\u3002\\n\\u7262\\u7B3C\\u7684\\u5BC6\\u7801\\u4E3A[0451]\"" },
        new[] { "Assets/Scenes/Level2/Level2-Floor1.unity", "      value: \"\\u57CE\\u4E2D\\u516C\\u544A\\u8BB0\\u8F7D\\uFF0C\\u4E00\\u540D\\u643A\\u5E26\\u8BD5\\u70BC\\u8BC1\\u660E\\u7684\\u5192\\u9669\\u8005\\u88AB\\u6C99\\u866B\\u541E\\u4E0B\\u3002\\u51FB\\u6740\\u6C99\\u866B\\u540E\\u53EF\\u4EE5\\u53D6\\u5F97\\u8FD9\\u4EFD\\u8BC1\\u660E\\uFF0C\\u7528\\u4E8E\\u5F00\\u542F\\u8BD5\\u70BC\\u5927\\u95E8\\u3002\\n\\n\\u5F53\\u524D\\u76EE\\u6807\\uFF1A\\u524D\\u5F80\\u57CE\\u5916\\u6C99\\u6F20\\u4E2D\\u592E\\uFF0C\\u51FB\\u6740\\u6C99\\u866B\\u5E76\\u62FE\\u53D6\\u8BD5\\u70BC\\u8BC1\\u660E\\u3002\"" },
        new[] { "Assets/Scenes/Level2/Level2-Floor1.unity", "      value: \"\\u53EC\\u5524\\u4EEA\\u5F0F\\u9700\\u8981\\u516D\\u540D\\u6307\\u5B9A\\u89D2\\u8272\\u7684\\u7075\\u9B42\\uFF1A\\u5927\\u4E3B\\u6559\\u3001\\u7A7F\\u91D1\\u8272\\u670D\\u88C5\\u7684\\u5B88\\u536B\\u58EB\\u5B98\\u3001\\u5927\\u53A8\\u3001\\u5927\\u5DE5\\u7A0B\\u5E08\\u3001\\u8BE1\\u5F02\\u5546\\u4EBA\\u548C\\u9690\\u5C45\\u5251\\u5BA2\\u3002\\n\\n\\u8FD9\\u4E9B\\u89D2\\u8272\\u5FC5\\u987B\\u5728\\u6CD5\\u9635\\u7684\\u77E9\\u5F62\\u8303\\u56F4\\u5185\\u6B7B\\u4EA1\\uFF0C\\u624D\\u80FD\\u7531\\u6C34\\u6676\\u5438\\u6536\\u7075\\u9B42\\u3002\\u516D\\u679A\\u6C34\\u6676\\u5168\\u90E8\\u70B9\\u4EAE\\u540E\\uFF0C\\u6CD5\\u9635\\u4E2D\\u592E\\u4F1A\\u53EC\\u5524\\u57DF\\u5916\\u6765\\u5BA2\\u3002\"" },
        new[] { "Assets/Scenes/Level2/Level2-Floor1.unity", "  questDetails: \"\\u8BE1\\u5F02\\u5546\\u4EBA\\u53EF\\u4EE5\\u5236\\u4F5C\\u8BF1\\u9975\\u70B8\\u5F39\\uFF0C\\u4F46\\u7F3A\\u5C11\\u5408\\u6210\\u5DE5\\u5177\\u3002\\u8BF1\\u9975\\u70B8\\u5F39\\u80FD\\u5F15\\u8BF1\\u6C99\\u866B\\u541E\\u98DF\\uFF0C\\u5E76\\u5728\\u5176\\u4F53\\u5185\\u7206\\u70B8\\u3002\\n\\n\\u5F53\\u524D\\u76EE\\u6807\\uFF1A\\u524D\\u5F80\\u57CE\\u9547\\u8981\\u585E\\u53D6\\u5F97\\u5408\\u6210\\u5DE5\\u5177\\uFF0C\\u4EA4\\u7ED9\\u8BE1\\u5F02\\u5546\\u4EBA\\u3002\"" },
        new[] { "Assets/Scenes/Level2/Level2-Floor1.unity", "  completedDetails: \"\\u5DF2\\u5C06\\u5408\\u6210\\u5DE5\\u5177\\u4EA4\\u7ED9\\u8BE1\\u5F02\\u5546\\u4EBA\\uFF0C\\u53EF\\u4EE5\\u5411\\u4ED6\\u8D2D\\u4E70\\u8BF1\\u9975\\u70B8\\u5F39\\u3002\\n\\n\\u8D2D\\u4E70\\u65B9\\u5F0F\\uFF1A\\u652F\\u4ED86\\u91D1\\u5E01\\uFF0C\\u6216\\u63D0\\u4EA41\\u4E2A\\u666E\\u901A\\u70B8\\u5F39\\u548C1\\u91D1\\u5E01\\u3002\"" },
        new[] { "Assets/Scenes/Level2/Level2-Floor1.unity", "  questDetails: \"\\u8D5D\\u54C1\\u5DE5\\u5320\\u8BF4\\u660E\\uFF0C\\u8BD5\\u70BC\\u5927\\u95E8\\u7684\\u796D\\u575B\\u901A\\u8FC7\\u91D1\\u8272\\u5B9D\\u77F3\\u8BC6\\u522B\\u8BD5\\u70BC\\u8BC1\\u660E\\uFF0C\\u53EF\\u4EE5\\u4F7F\\u7528\\u91D1\\u8272\\u5B9D\\u77F3\\u5236\\u4F5C\\u540C\\u6837\\u6709\\u6548\\u7684\\u8D5D\\u54C1\\u3002\\n\\n\\u5F53\\u524D\\u76EE\\u6807\\uFF1A\\u524D\\u5F80\\u884C\\u5546\\u5E02\\u573A\\u4E8C\\u5C42\\uFF0C\\u5411\\u7A7F\\u6DF1\\u84DD\\u8272\\u8863\\u670D\\u7684\\u5B9D\\u77F3\\u5546\\u4EBA\\u8BE2\\u95EE\\u91D1\\u8272\\u5B9D\\u77F3\\uFF0C\\u5E76\\u53D6\\u5F97\\u4E00\\u9897\\u4EA4\\u7ED9\\u8D5D\\u54C1\\u5DE5\\u5320\\u3002\"" },
        new[] { "Assets/Scenes/Level2/Level2-Floor1.unity", "  completedDetails: \"\\u5DF2\\u4EA4\\u4ED8\\u91D1\\u8272\\u5B9D\\u77F3\\u5E76\\u83B7\\u5F97\\u8D5D\\u54C1\\u8BD5\\u70BC\\u8BC1\\u660E\\u3002\\n\\n\\u5C06\\u8D5D\\u54C1\\u4F9B\\u5949\\u5728\\u8BD5\\u70BC\\u5927\\u95E8\\u7684\\u796D\\u575B\\u4E0A\\uFF0C\\u5373\\u53EF\\u6253\\u5F00\\u901A\\u5F80\\u8BD5\\u70BC\\u4E4B\\u5730\\u7684\\u5927\\u95E8\\u3002\"" },
    };
    static QuestDescriptionCopySmoke() { EditorApplication.update += Tick; }
    static void Check(bool ok, string label)
    { if (!ok) throw new Exception(label); checks++; Debug.Log("QUEST_COPY_CHECK " + label); }
    static string Upgrade(string id, string details) =>
        (string)typeof(QuestJournalManager).GetMethod("UpgradeLegacyQuestDetails", F).Invoke(null, new object[] { id, details });
    static string Constant(string name) => (string)typeof(QuestJournalManager).GetField(name, F).GetValue(null);
    static QuestJournalManager.SaveState RoundTrip(QuestJournalManager.SaveState state) =>
        JsonUtility.FromJson<QuestJournalManager.SaveState>(JsonUtility.ToJson(state));
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"))
            throw new Exception("Isolated project only");
        foreach (var group in SceneLines.GroupBy(l => l[0]))
        {
            var lines = new HashSet<string>(File.ReadLines(group.Key));
            foreach (var fixture in group)
                Check(lines.Contains(fixture[1]), group.Key + " contains updated authored journal field");
        }
        Check(Ids.Length == 14 && Ids.Distinct().Count() == 14, "all 14 quest IDs covered");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetInt(Key + ".checks", checks);
        SessionState.SetBool(Key, true);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || !SessionState.GetBool(Key, false)) return;
        SessionState.SetBool(Key, false);
        checks = SessionState.GetInt(Key + ".checks", 0);
        try { Verify(); Debug.Log("QUEST_DESCRIPTION_COPY_PASS checks=" + checks); EditorApplication.Exit(0); }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void Verify()
    {
        Time.timeScale = 0;
        const string note = "\r\n补充线索：保留自定义记录。\n牢笼的密码为[0451]";
        foreach (var r in Revisions)
        {
            Check(Upgrade(r[0], r[1]) == r[2], r[0] + " exact previous copy upgraded");
            Check(Upgrade(r[0], r[1] + note) == r[2] + note, r[0] + " appended clues retained");
            Check(Upgrade(r[0], r[2] + note) == r[2] + note, r[0] + " migration is idempotent");
            Check(Upgrade(r[0], r[1] + "（自定义改写）") == r[1] + "（自定义改写）", "custom paragraph suffix protected");
            Check(Upgrade("custom.quest", r[1]) == r[1], "unrelated ID protected");
            Check(Upgrade(r[0], "引述：" + r[1]) == "引述：" + r[1], "embedded quotation protected");
            Check(Upgrade(r[0], "阶段记录\r\n" + r[1] + note) == "阶段记录\r\n" + r[2] + note,
                "known later paragraph updates without replacing leading or trailing records");
        }
        Check(!QuestJournalManager.Level0EscapeDescription.Contains("死亡") &&
            QuestJournalManager.Level0EscapeDescription.Contains("肉体已失去控制"),
            "Level0 describes lost control, not bodily death");
        var journal = QuestJournalManager.GetOrCreate();
        foreach (bool complete in new[] { false, true })
        {
            var save = new QuestJournalManager.SaveState {
                tracked = "level1.open_monster_cage", initialTracked = "level0.escape_prison",
                initialized = new List<string> { "CopyFixture" },
                completed = complete ? Ids.ToList() : new List<string>(),
                level2HeroProofStage = complete ? 3 : 2,
                summoningLitCrystals = complete ? 6 : 2, summoningPerformed = complete,
                arenaQuestStage = complete ? QuestJournalManager.ArenaQuestStage.Claimed : QuestJournalManager.ArenaQuestStage.Fighting,
                arenaQuestWins = complete ? 3 : 1, arenaQuestRounds = 3
            };
            var expected = new Dictionary<string, string>();
            for (int i = 0; i < Ids.Length; i++)
            {
                string id = Ids[i];
                var r = Revisions.First(v => v[0] == id);
                string old = r[1] + note, next = r[2] + note;
                if (id == QuestJournalManager.Level2HeroProofId)
                {
                    var stage = Revisions.Where(v => v[0] == id).ElementAt(complete ? 2 : 1);
                    old = r[1] + "\n\n" + stage[1];
                    next = r[2] + "\n\n" + stage[2];
                }
                else if (id == QuestJournalManager.SummoningQuestId)
                {
                    var stage = Revisions.First(v => v[0] == id && v[1].StartsWith(complete ? "六枚" : "当前目标："));
                    string count = complete ? "" : "\n已点亮水晶：2/6。";
                    old = r[1] + "\n\n" + stage[1] + count;
                    next = r[2] + "\n\n" + stage[2] + count;
                }
                else if (id == QuestJournalManager.ArenaQuestId)
                {
                    string stage = complete ? "已领取试炼证明" : "击败场内守护者";
                    string rest = "\n\n当前目标：" + stage + "。\n本次挑战已击败守护者：" + (complete ? "3" : "1") + "/3。";
                    old = r[1] + rest; next = r[2] + rest;
                    if (complete)
                    {
                        var end = Revisions.First(v => v[0] == id && v[1].StartsWith("我已"));
                        old += "\n" + end[1]; next += "\n" + end[2];
                    }
                }
                save.entries.Add(new QuestJournalManager.SavedEntry {
                    id = id, title = Titles[i], details = old, scene = "CopyFixture",
                    isLevelObjective = id == "level0.escape_prison" || id == QuestJournalManager.Level2HeroProofId
                });
                expected[id] = next;
            }
            journal.ResetAllEntries(); journal.ApplySaveState(RoundTrip(save));
            for (int pass = 0; pass < 2; pass++)
            {
                Check(journal.Entries.Count == 14, "save restore does not add or remove tasks");
                for (int i = 0; i < Ids.Length; i++)
                {
                    string id = Ids[i];
                    Check(journal.TryGetEntry(id, out var e) && e.Details == expected[id],
                        id + " " + (complete ? "completed" : "active") + " save description pass " + pass);
                    Check(e.Title == Titles[i] && journal.IsEntryCompleted(id) == complete, id + " title and completion preserved");
                }
                Check(journal.TrackedEntryId == save.tracked && journal.InitialTrackedEntryId == save.initialTracked,
                    "tracked objective remains unchanged");
                Check(journal.EntryDetailsContain("level1.open_monster_cage", "0451"), "password remains discoverable");
                var captured = RoundTrip(journal.CaptureSaveState());
                Check(captured.level2HeroProofStage == save.level2HeroProofStage &&
                    captured.summoningLitCrystals == save.summoningLitCrystals &&
                    captured.arenaQuestWins == save.arenaQuestWins &&
                    captured.arenaQuestStage == save.arenaQuestStage, "mechanical stage counters preserved");
                journal.ResetAllEntries(); journal.ApplySaveState(captured);
            }
        }
        // Includes oldest save versions, pending discoveries, completion-before-acceptance and UI notifications.
        typeof(Level1QuestCopySmoke).GetMethod("Verify", F).Invoke(null, null);
        Check(true, "existing Level0 and Level1 copy/save regressions passed");
    }
}

