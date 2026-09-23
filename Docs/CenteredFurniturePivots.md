# 桌椅中心原点

`Chair-Normal.prefab` 与 `Table-Normal.prefab` 的根 Transform 已归零，原点位于整体矩形外轮廓中心；子物件以该中心重新表达局部位置，包括清除原有的大幅 Z 偏移。预制体 GUID、对象 ID、尺寸、朝向、颜色、排序层及碰撞组件均不变。

调整掉的旧局部中心：

- Chair-Normal：`(2.0631511, -2.2760708, 10.787389)`。
- Table-Normal：`(0.2582, -0.4947, 4.338084)`。

同步补偿了 7 个场景中的 194 个已有实例，按各实例自身旋转和缩放计算 `新位置 = 旧位置 + 旋转 × 缩放 × 旧中心`；9 处直接子节点的位置覆盖也同步减去旧中心。场景父节点、颜色覆盖、尺寸覆盖等其他内容不变。

| 场景 | 实例数 |
|---|---:|
| Level0 | 78 |
| Level1-Floor-1 | 5 |
| Level1-Floor1 | 47 |
| Level1-Floor2 | 9 |
| Level1-Floor3 | 12 |
| Level1-Garden | 1 |
| Level2-Floor1 | 42 |

`Tools/CenterFurniturePivots.mjs` 生成迁移补丁并校验子节点中心及四角坐标在原场景父坐标系中一致；本次双精度计算最大误差约 `6.4e-11` 世界单位（Unity 存储为浮点数，实际会有正常舍入误差）。未发现需要额外处理的根节点下新增/嵌套物件。

本次备份、补丁与预期结果在 `.codex-temp/CenteredFurniture-1789801311770`。验证命令：`node Tools/CenterFurniturePivots.mjs verify .codex-temp/CenteredFurniture-1789801311770`；后续主动修改这些文件会使快照比对失败，不应据此覆盖用户新改动。不要再次对已归中的预制体执行迁移。

编辑时选中父对象即可围绕桌椅中心移动、旋转和缩放；Scene 工具栏设为 Pivot 可直接查看新原点。不需要新增运行时脚本。
