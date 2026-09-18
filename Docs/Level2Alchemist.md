# Level 2 炼金术士

预制体：`Assets/Prefabs/Level2NPCS/Alchemist-level2.prefab`。
角色脚本：`AlchemistZeldaCharacterData`，继承公共角色数据；外观和普通攻击参数可在 Inspector 编辑。

16×16 像素外观采用深青绿色兜帽长袍、黄铜护目镜/腰饰、工作围裙、棕色靴子，腰间的紫色药瓶与皮包是装饰。包含四向待机、两帧步行动画、四向普通攻击、非运行状态的蓝色编辑器边框。出手前清除对应的待机手臂并补回衣物，避免出现重复手掌。

未指定数值沿用现有 Blacksmith 工匠角色：生命 5、力量 1、技巧 3、权限 1、E.G.O 2、附身需求 2、移动速度 3；使用平民 AI、现有附身/碰撞系统以及普通近战攻击，没有新增炼金或药瓶投掷技能。摄像机 Size 7、玩家视野 13。原有角色和场景均未修改，也未自动将新角色放入场景。

运行 `Tools/TestAlchemist.ps1` 验证 16 个待机/行走/攻击姿势的连通性、手部消重、护目镜/药瓶色块以及预制体引用，并生成 `Docs/Alchemist-preview.png`。加上 `-AttackPreview` 可生成攻击姿势预览。静态预览不含 CRT，尚需在 Unity 场景内确认最终观感。
