# 静态水面效果

脚本：`Assets/Scripts/Rendering/SpriteStillWaterSurface.cs`。

“静态”指没有定向水流：几何水纹在原地缓慢伸缩、明暗起伏并淡入淡出，而不是完全冻结。复用当前 `SpriteWaterFlowParticles` 的四类几何短纹、蓝青色配色、CRT 横线可见度补偿、轮廓裁切与分区分布，不增加着色器或替换源精灵材质。

## 使用

1. 在水面对象上添加 **Rendering → Sprite Still Water Surface**。
2. `Source Renderer` 指向水面的 SpriteRenderer。喷泉请指定 **Water** 子对象，而不是池沿或整个喷泉的根 Sprite。
3. 同一水面原有的 `Sprite Water Flow Particles` 若仍启用，会叠加流动效果；需要纯静水时请关闭原组件。本脚本不会自动修改其他组件。
4. 进入 Play Mode 观察。脚本未自动挂到场景或任何喷泉预制体上。

## 参数

- **Density**：每平方世界单位同时存在的水纹数，默认 0.65，不因寿命改变而改变密度。
- **Lifetime**：默认 3.5 秒，水纹淡入、停留、淡出。
- **Length / Width**：水纹绘制区域的世界尺寸，默认 0.48 / 0.18；实际亮纹比 Width 更细。
- **Animation Speed**：默认 0.3，只影响原地形变。0 停止形变，但仍按寿命淡入淡出。
- **Color**：默认蓝青半透明 `(0.3, 0.75, 0.95, 0.38)`。
- **Follow Sprite Shape / Check Readable Alpha**：沿用原水流的网格与透明轮廓支持；包括圆池、Sprite 翻转、旋转与缩放。Sliced/Tiled 使用实际矩形范围。
- **Particle Limit**：默认最多 2048 条，面积增大时自动扩容，达到上限后降低实际密度。
- **Sorting Order Offset**：默认比水面高 1 层，继承其 Sorting Layer、Layer 和 SpriteMask 设置。

水纹中心固定在水面局部位置；移动、旋转或缩放水面时跟随水面，不残留在旧位置。游戏暂停时停止推进。关闭/卸载组件时清空水纹，重新启用后渐进补足，销毁时清理私有粒子与材质。只读属性 `SurfaceWorldArea` 和 `TargetRippleCount` 可用于运行时检查。

原流动水组件的默认行为不变。新增静水入口只由新脚本的私有运行时子对象调用。

验证：`Tools/TestStillWaterSurface.ps1` 执行实际配置、数量计算和粒子更新方法；`Tools/TestSpriteWaterFlowShader.ps1` 检查共享着色器的 D3D 编译；`Tools/StillWater.Validation.targets` 用于 C# 编译。数值测试的引擎服务使用桩，尚未进行 Unity Play Mode 画面验证。
