# 独立俯视几何植物

目录：`Assets/Prefabs/Decorations/Plants/`。全部可以独立拖入场景，也可作为花坛的子物体使用。

植物相关资源现统一放在该目录，共 11 个预制体：下列 7 种小型植物，以及 `GeometricTree`、`PixelTree`、`PixelSkyBlueBranchTree` 三种树木和 `GeometricFlowerbed` 花坛。整理时一并移动原 `.meta` 文件，GUID 和资源内容保持不变。

| 预制体 | 形态 | 几何数量 |
| --- | --- | --- |
| GeometricLeafCluster | 原花坛四叶簇 | 5 |
| GeometricCoralFlower | 原花坛橙色小花 | 7 |
| GeometricGoldFlower | 原花坛金色小花 | 7 |
| GeometricBroadleafShrub | 密集、宽阔的不规则灌丛 | 6 |
| GeometricGrassTuft | 细长、放射状草丛 | 7 |
| GeometricFern | 长条中轴与成对羽状叶片 | 12 |
| GeometricSucculent | 厚叶、内外两圈的莲座多肉 | 13 |

前三种提取原花坛植物的子物体几何，独立预制体的根节点位置/旋转归零、缩放为 1。原花坛及场景摆放未修改，也不自动替换成嵌套引用。

所有植物保持 XY 平面俯视、低饱和配色与几何轮廓。没有碰撞箱、脚本、动画或粒子，不会增加通行/视野遮挡。共用现有的引擎 Square 精灵和默认精灵材质，不新增贴图。

在 Inspector 修改子物体的 SpriteRenderer Color 可调整叶片、花朵颜色；根节点 Scale/Rotation 调整整体大小、方向。用作花坛子物体时，将局部 Z 保持为 0，并放在土壤范围内；默认渲染顺序高于现有花坛土壤。

验证：`pwsh -NoProfile -File Tools/TestGeometricPlants.ps1`。预览为基于预制体实际几何的静态渲染，非 Unity 游玩截图。
