# Level2 Floor-1 水道边框

- 场景：`Assets/Scenes/Level2/Level2-Floor-1.unity`，层级位置为 `Water / Wide Water Borders`。
- 参考 Floor1、Floor2 现有水边矩形条：约 0.3 世界单位宽，颜色 `(0.46540877, 0.52170825, 0.6981132, 1)`，复用相同 Square 精灵和默认 Sprite 材质。
- 覆盖 12 处宽水道／水池；4 条宽约 1 单位的支流（Square (8)、Square (9)、Square (10)、3BWater3）未添加边框。连接水道处裁去横向封口，避免水道内部出现分隔线。
- 共 53 个静态矩形 SpriteRenderer，无额外碰撞体、粒子或逐帧生成脚本。Default 层、排序 -8，低于水面 -7 和角色，高于地面底色；不会扩大河流碰撞范围或遮挡视野。
- 岸边是独立静态地景，不作为水闸淡入淡出的目标。原来的水流对象、细支流、碰撞体、水闸事件绑定均未改动；水退去时保留水道轮廓。
- `Tools/TestLevel2BasementWaterBorders.ps1` 验证对象引用、几何宽度、颜色／精灵与参考场景一致、水流连接处不封口及细流排除。可用 `-BeforeScene` 对比修改前快照，核实所有原有场景块原样保留。`-PreviewPath` 可输出几何示意图（不含建筑遮盖，并非 Unity 实机截图）。
