# 俯视几何花坛

预制体：`Assets/Prefabs/Decorations/Plants/GeometricFlowerbed.prefab`。当前未放入场景。

- 默认大小为 4.4 × 2.2 世界单位，原点位于中心，严格 XY 平面俯视。
- `Rectangular Bed` 下由矩形石边、矩形内沿和矩形土壤组成，无斜面、圆角、台阶或透视立面。
- `Plant 1/2/3` 为三簇低饱和绿色植物，叶片使用少量旋转方形形成的菱形，只有两处小花色块点缀。
- 对应的独立植物及四种新增形态位于 `Assets/Prefabs/Decorations/Plants/`，可自由拖入花坛替换或组合，见 `Docs/GeometricPlants.md`；本花坛的现有摆放未改变。
- 根节点 Scale 控制整体大小；各子物体 Transform 和 SpriteRenderer Color 可独立调整形状、位置和颜色。
- 共 22 个使用引擎 Square 精灵的 SpriteRenderer，无动画、粒子、运行时生成脚本或自定义贴图。
- 默认是纯装饰，根节点 BoxCollider2D 已设置为外框大小但关闭，可按需启用。Default 层不会加入当前玩家视野的 Blocks/Hidden Blocks 遮挡检测。

运行 `pwsh -NoProfile -File Tools/TestGeometricFlowerbed.ps1` 可检查几何、植物范围、预制体引用及默认碰撞设置，并生成静态预览。预览非 Unity 实机截图。
