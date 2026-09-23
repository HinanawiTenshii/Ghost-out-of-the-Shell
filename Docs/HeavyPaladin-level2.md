# Level 2 重装圣骑士

预制体：`Assets/Prefabs/Level2NPCS/HeavyPaladin-level2.prefab`。
只新增普通版，不修改现有 Paladin、AdvancedPaladin 或 Strongman。

## 体型与外观

- 与 Level2 Strongman 一样使用 20×20 画布、16 PPU、脚下锚点 (0.5, 0.3)，
  根节点 XY 缩放 1.1。正面可见轮廓同为 14×16 像素，宽肩、厚躯干、短腿。
- 继承圣骑士的天蓝装甲配色、封闭式 T 形面甲、身体左侧蓝色披肩和护手。
- 四向待机、两帧行走、攻击共 16 个缓存姿态；侧面头盔平滑，手臂位于躯干中部。
  披肩优先遮挡手臂；攻击姿态不绘制同一只手的待机版本。
- 实体 BoxCollider2D 本地尺寸 (0.65, 0.79)、中心 (0, 0.3)，随整体缩放。
  保留蓝色编辑器边框。

## 战斗

沿用现有圣骑士的生命 10、伤害 3、移动速度 5、AI、附身与反馈设置。
使用共享攻击预制体和单次伤害判定，新增 BattleAxe 外观枚举（追加，不重排旧值）。
战斧为 16×16 粗像素双刃斧头、金属斧套和褐色木柄，随攻击方向旋转。
攻击尺寸 (0.95, 0.9)，前伸偏移 0.65，持续 0.3 秒。
没有额外赋予 Strongman 的职业技能，也未修改其他角色的武器。

## 验证

- `Tools/TestHeavyPaladinVisual.ps1` 执行实际姿态生成逻辑，检查全部姿态连通性、
  披肩遮挡、攻击手部替换、Strongman 体型匹配以及战斧左右对称与连通性。
  输出 `Docs/HeavyPaladin-level2-preview.png`，不包含游戏光照/CRT 后处理。
- `Tools/HeavyPaladin.Validation.targets` 用于 Unity 项目文件尚未刷新时编译新增脚本。
- 完整 C# 编译和既有攻击循环回归通过。未进行 Unity Play Mode 场景内对战验证。
