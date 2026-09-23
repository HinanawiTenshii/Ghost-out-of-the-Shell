# Visible Non Blocking 图层

Unity Layer 9：`Visible Non Blocking`。

用于围墙、栏杆等需要在视野暗部遮罩上显示、但不截断玩家视线的物体。
在 Inspector 顶部的 Layer 下拉框选择该层；如果渲染器或碰撞体在子对象上，
也应将相应子对象设为该层。只更改空父对象不会改变子对象的显示行为。

| 图层 | 在暗部遮罩上显示 | 阻挡玩家/机关人偶视线 |
| --- | --- | --- |
| Blocks | 是 | 是 |
| Hidden Blocks | 否 | 是 |
| Visible Non Blocking | 是 | 否 |
| Default（默认配置） | 否 | 否 |

新层自动接入现有叠加摄像机，不增加摄像机或材质。即使在 Block Layers 中
误勾选此层，也会在初始化、编辑器校验和场景设置刷新时将其排除。
保留当前灵魂转移时全黑覆盖的行为。对象仍遵循原有摄像机边界加载规则，
不会因为处于该层而全场景常驻。

此层不代表无碰撞：Collider2D、角色 Solid Collision Layers 和物理层碰撞矩阵
仍决定能否穿行。自定义移动碰撞掩码需要包含 Layer 9 才会与它碰撞。
NPC 感知、地图等独立掩码不自动更改。未批量更改已有场景/预制体的图层。

验证：`Tools/TestVisibleNonBlockingLayer.ps1` 执行实际掩码配置方法的独立测试。
