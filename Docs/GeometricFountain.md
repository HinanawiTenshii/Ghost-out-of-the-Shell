# 极简俯视喷泉

直接更新 `Assets/Prefabs/Decorations/喷泉.prefab`，保留原 GUID、根节点位置/缩放及原喷水发射器引用，不创建重复喷泉，也不修改场景摆放。

- 圆池改为方池，使用与花坛相同的灰米色石边和深灰内沿；水面改为低饱和蓝绿色。
- 中央为小型方形石台、喷口水面和喷嘴，四处短矩形水光点缀。11 个默认 Square 精灵，无新贴图或运行时造型脚本。
- 根节点仍为原来的 8 倍缩放，默认外轮廓为 8×8 世界单位。原实体圆形碰撞改为贴合方池的 BoxCollider2D；仍在 Default 层，不新增玩家视野遮挡。
- 保留原 RadialFadingParticleEmitter 嵌套预制体，只在喷泉实例内覆盖参数：浅灰青水滴、14 个/秒、最多 64 个、较早淡出；发射点居中。没有修改其他地方使用的粒子预制体或脚本。
- 可在各子物体 SpriteRenderer 修改颜色、Transform 修改比例；喷水参数在 WaterParticle 子预制体组件中调整。

验证：`pwsh -NoProfile -File Tools/TestGeometricFountain.ps1`。检查 GUID、原摆放参数、引用、矩形形状、石边配色、碰撞与喷水参数，并生成仅包含静态几何的预览。尚未做 Unity Play Mode 运行验证。
