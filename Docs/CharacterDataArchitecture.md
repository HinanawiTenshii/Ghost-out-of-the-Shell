# 角色数据结构

## 分层

- `ZeldaCharacterCommonData`：通用 Inspector 配置父类，包含生命、技巧、权限、E.G.O、附身需求、移动速度、受击/死亡反馈、音效、属性窗口配置和公共参数校验。不再包含角色技能开关。
- `ZeldaCharacterData`：共享运行行为，负责当前生命、能量消耗、成长加成、技能释放、受击死亡、存档状态和外部只读属性接口。
- 具体角色子类：`SwordZeldaCharacterData`、`StrongmanZeldaCharacterData`、`PrisonerZeldaCharacterData`、`CivilianZeldaCharacterData`、`BlacksmithZeldaCharacterData`、`NobleZeldaCharacterData`、`GhostZeldaCharacterData`、`BehemothZeldaCharacterData`。各自保存攻击形状/范围、外观配色、角色贴图生成和类型专属行为。

所有具体角色都直接继承 `ZeldaCharacterData`。巨兽不再继承剑士，以免在 Inspector 中出现实际未使用的剑士攻击和外观设置。

## 编辑与扩展

在原有角色预制体的数据组件中编辑即可。父类的 `[SerializeField] protected` 字段会在 Inspector 中公开显示，同时避免外部脚本绕过运行接口修改生命/能量。各角色的特有参数仍在同一组件的子类字段中编辑，不需要额外挂载父类组件。

共享外观的角色预制体变体（例如 King 使用 Noble 的外观）继续共用对应类型脚本，通过预制体覆写保存差异。新增外观或攻击机制不同的角色时，新建继承 `ZeldaCharacterData` 的子类，重写需要的视觉/攻击接口；若重写 `OnValidate`，须调用 `base.OnValidate()`。

## 控制角色的摄像机与视野

在角色数据组件的 `Player Camera / 控制时的摄像机与视野` 分组编辑：

- `Camera Orthographic Size`：控制角色时摄像机的正交半高度。普通角色统一为 7，Ghost 预制体为 5。
- `Player Vision Radius`：控制角色时遮罩的世界空间半径，基础值已导入 13；不是 AI 的侦测半径。
- `Scene Camera Overrides`：按完整场景路径匹配的可选例外。每项的大小或半径为 0 时继承基础值，删除条目即可让该场景也完全使用基础设置。

所有角色视野半径统一为 13。原迁移时保留的场景例外已清空，场景内角色实例的大小/半径覆写也已清除，现在各场景均使用预制体基础值。可选例外接口仍保留，便于之后按需配置。

22 个含角色数据的源预制体已写入配置，King 等变体继承源预制体配置。`CameraFollowActiveZeldaMover` 从当前角色读取大小，仍保留原本的附身聚焦、缩放平滑和灵魂转移跟随；`CameraCircularVision` 从当前角色读取半径，变化时立即刷新遮罩和加载边界，不会每帧无条件重建。无控制角色时使用摄像机原有的备用半径。

修改某个已有例外场景的效果时，需要修改对应例外条目，而不仅是基础值。场景路径变更后应同步更新条目。临时幽灵形态继续使用本体的镜头数据，真正 Ghost 使用 Ghost 预制体的数据，与迁移前的角色区分逻辑一致。机关人偶的独立揭示范围与远程镜头大小、NPC 的 AI 侦测范围均未改动。

## 原生角色技能

原生技能由所属角色自身提供，不再在通用父类中定义一组可勾选的技能变量：

- Warden-level0、LegendaryGuardian：挂载 `CombatExpertiseCharacterSkillData`，固定提供战斗专精。
- King：在 Noble 的预制体变体上额外挂载 `RoyalCommandCharacterSkillData`，固定提供发号施令；普通 Noble 不会获得此技能。
- Behemoth：继续由 `BehemothZeldaCharacterData` 自身提供恐惧咆哮及其专属参数。
- 其他角色不挂载原生技能数据组件，0 号栏保持为空。

`NativeCharacterSkillData` 是可扩展的抽象组件协议；新增可复用的原生技能时创建具体子组件，只挂载到拥有该技能的角色预制体。技能标识、名称、图标为只读属性，没有额外的启用布尔变量。同一角色最多挂载一个该协议的组件。角色专属行为也可以像巨兽一样重写角色数据的原生技能接口。

HUD、附身后的技能发现仍通过 `CharacterSkillName`、`CharacterSkillIcon`、`NativeCharacterSkillId`、`TryUseCharacterSkill` 访问。技能树学习及装备技能与角色原生技能独立：普通角色依然可以使用玩家永久学会并装备的技能。

## 兼容性

- 保留所有现有具体角色脚本 GUID 和组件引用，无需批量替换预制体或场景组件。
- 除已移除的原生技能开关外，迁入父类的字段保留名称、类型、默认值，原有预制体/场景覆写仍对应相同序列化路径。
- `SavedScalarFields.Apply` 将旧快照中声明在 `ZeldaCharacterData` 的通用字段映射至新父类。
- `ZeldaCharacterData.RuntimeState` 结构未改动，当前生命、能量消耗及技能状态仍由原有流程保存。
- `combatExpertiseSkill`、`commandSkill` 仅保留在旧存档兼容结构中，读取时不再改变原生技能归属。旧标量配置中的同名字段因已移除而被忽略；技能来源以迁移后的预制体为准，进行中的技能计时仍会恢复。

## 回归检查

1. 打开现有角色预制体，检查通用参数与外观、攻击范围是否保持原值。
2. 分别附身普通角色和幽灵，检查属性、技能、受击及死亡行为。
3. 检查巨兽的普通震波攻击和恐惧咆哮。
4. 读取重构前的存档，切换场景后再次存取，确认角色配置和运行状态保持一致。
