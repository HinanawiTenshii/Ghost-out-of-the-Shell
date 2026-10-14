# Level2 普通 NPC：静止与巡逻

16 种普通角色各有两套预制体：

- `Assets/Prefabs/Level2NPCS/Idle/`：静止待机；仍能警戒、追击、攻击、调查和返回。
- `Assets/Prefabs/Level2NPCS/Patrol/`：四点巡逻，原点为第一个点；另外三个点使用相对坐标，随实例位置平移。

自动人偶、骷髅和沙虫 Boss 保留在 Level2NPCS 根目录，专用 AI 不变。

## AI 与兼容

- 普通战斗角色：`ZeldaCharacterAiBase` / `ZeldaPatrolCharacterAi`。
- 民用角色（平民、商人、铁匠、厨师、炼金师、神职者、巫师）：`CivilianIdleCharacterAi` / `CivilianPatrolCharacterAi`，两者保留相同的权限差值规则。
- 没有修改任何角色的外观、属性、攻击、碰撞或附身组件。
- 原预制体只移动到符合其已有 AI 的文件夹，GUID、文件 ID、名称和内容保持不变，因此已摆放的实例不会突然改为另一种 AI。
- 新增的另一版本使用独立 GUID，并在名称后添加 `-Idle` 或 `-Patrol`，使保存系统可按角色名称区分。
- 对照清单：`Tools/Level2NpcVariants.json`。存档目录会由原有 SaveGameCatalogBuilder 在进入运行模式及构建前自动更新。

巡逻路线在 Inspector 的 Four Point Idle Patrol 中配置，停留时间在 Patrol Point Wait Times 中配置。新增巡逻版默认围绕原点走 2×2 的四点路线，每点停留 1 秒；原有巡逻角色的路线配置不变。

注意：7 种原有民用角色的巡逻点偏移均为零，所以拖入场景后仍会先留在原地；请设置 Point 2/3/4 Offset 以启用实际行走。保留零点配置是为了避免既有关卡中的商人、厨师等因预制体整理而突然离岗。测试仅在临时实例上配置路线，未更改这些原预制体或场景实例。

## 验证

- 16 个原预制体的内容和 GUID 与整理前一致，新增版本只修改根名称、AI 脚本和对应巡逻字段。
- 35 个 NPC 预制体 GUID 无重复（32 个普通版本 + 3 个特殊角色）。
- 编译零警告、零错误；隔离 Unity 运行测试通过 1137 项检查，包含组件引用、配对属性、民用权限规则、存档目录区分及实际静止/巡逻移动。
- 测试入口：`Tools/Level2NpcVariantsSmoke.cs`，测试工程需将配套 JSON 放入 `Assets/Editor/Level2NpcVariants.json`。
