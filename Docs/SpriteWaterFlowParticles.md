# Sprite 水流粒子组件

组件脚本：`Assets/Scripts/Rendering/SpriteWaterFlowParticles.cs`。

## 挂载与调节

1. 打开目标预制体，选择根对象或带 SpriteRenderer 的对象。
2. Add Component → Rendering → Sprite Water Flow Particles。
3. `Source Renderer` 可留空自动查找自身/子对象；含多个 SpriteRenderer 时建议明确拖入要作为水面的那个。
4. 进入 Play Mode 查看效果。组件只在运行时生成自己的粒子子对象，不会接管已有 ParticleSystem，不修改原 Sprite 或材质，也不会自动改动其他场景对象。

## 水纹表现

当前版本使用与几何场景一致的蓝青色短线带，不再使用固定 16×8 像素弧线。水纹随机混合四类轮廓：短直线、带斜向连接的折线、错位双线、带间隙的断线。边缘轻量抗锯齿并带有微弱暗边，不使用白色闪点或长尾迹。水纹朝向仍固定于配置的流向，位置随缓慢起伏的水流移动。

每条水纹通过粒子的 `StableRandomX` 顶点数据获得独立且稳定的形状、长度、折角、粗细与动画相位；移动、边界循环和粒子数组整理不会重新随机轮廓。水纹会连续缓慢伸缩、偏移和起伏亮度，而非整片同步变化或每帧跳变。`Shape Animation Speed` 默认 1，设为 0 停止形变但保留流动，暂停游戏时两者均停止。随机计算与周期动画在顶点阶段执行，不新增粒子数量、逐粒子材质或额外绘制通道。

默认长度倍率为 5、宽度倍率为 4，使用蓝青色而非近白色。默认颜色为 `(0.3, 0.75, 0.95, 0.42)`。本次几何轮廓重构保留现有组件的颜色、密度倍率、面积检测、数量自适应和流速；已有组件直接使用新着色器，不需要删除重挂。

`Ribbon Length Scale` / `Ribbon Width Scale` 调整水纹长度和展开宽度，`Ribbon Density Multiplier` 控制水纹密度，`Current Sway` 控制水流轻微横摆。新增着色器位于 `Assets/Resources/Shaders/SpriteWaterFlow.shader`，仅用于本组件的水纹，不改变整屏颜色或源 Sprite 材质。

`Horizontal Visibility Boost` 默认 0.65，用于补偿 CRT 横向扫描线：完全横向时亮纹厚度增加 65%，不透明度增加约 23%，不改变色相、长度、流速和数量。纵向保持原效果，斜向平滑过渡；判断依据是摄像机看到的方向，旋转水面或摄像机仍有效。设为 0 可关闭。此补偿在原有水纹着色器中完成，不增加绘制批次或额外粒子。实际均衡程度可根据 CRT 扫描线强度与游戏分辨率微调。

主要参数：

- `Flow Direction`：右 `(1,0)`、左 `(-1,0)`、上 `(0,1)`、下 `(0,-1)`；可使用任意斜向向量，零向量静止。
- `Local Direction`：默认关闭，流向固定在世界坐标中，上下流动不会被 Sprite 的旋转变成横向。开启时随 Sprite 物体旋转、缩放方向变化（旋转 90° 后，本地向上等于世界向左）。Sprite 的 Flip 影响形状采样，但不改变配置的流向。
- 组件右键菜单 `Flow Direction` 可快速设置世界坐标的上、下、左、右，同时关闭 `Local Direction`。运行时也可调用 `SetWorldFlowDirection(Vector2.up/down/left/right)`，存活水纹会在下一帧调整速度与朝向，不重新开始淡入。当前 Level2-Floor1 的水面已改为世界坐标配置，横向水道保持原流向，竖直水道改为向下。
- `Speed`、`Speed Randomness`、`Direction Spread`：流速、速度随机幅度、方向随机偏角。
- `Particles Per Square Unit`：密度基础值。目标同时存在数量 = 有效世界面积 × 此值 × Ribbon Density Multiplier × Lifetime，四舍五入为整数；默认每平方世界单位约 1.44 条存活水纹，实际可见亮度还受淡入淡出影响。
- `Distribution Cells`：打乱分区顺序并在格内随机采样，使分布较均匀而不呈现规则点阵。
- `Particle Safety Limit`：独立性能保护上限，默认 8192。缓冲区随面积自动扩容，旧的 256 数值仅作为初始容量，不再限制大水面数量。达到保护上限的超大水面仍会降低密度，可按性能预算调整。每帧最多生成 512 条，启用时短暂渐进补足。
- `Particle Color`、`Particle Size`、`Particle Length`、`Lifetime`：颜色、世界单位基础尺寸、沿流向的长度比例、寿命。实际尺寸还会乘上水纹长度/宽度倍率。默认淡蓝半透明、渐入渐出，无长尾迹。
- `Keep Inside Sprite`：默认开启，离开矩形区域的粒子从另一侧重新进入，保留年龄和淡入淡出状态；不规则区域会重选内部位置。这样小水面不会因边界流失而变稀疏。关闭则允许粒子流出水面后自然消失，不保证水面内部的存量密度。
- `Sorting Order Offset`：继承源 Sprite 的 Sorting Layer，在其排序上增加此偏移，默认 +1。继承源对象 Layer 和 SpriteMask 设置，使用正常场景渲染，可与 CRT/水光滤镜叠加。

## 形状支持

面积检测：矩形与 Sliced/Tiled 使用实际本地范围；Simple Sprite 使用网格三角形面积，可读取 Alpha 时用缓存的 32×32 采样估算非透明面积。再通过变换后 X/Y 轴的叉积换算世界面积，包含父物体缩放、负缩放和旋转，不使用旋转后膨胀的世界包围盒。透明纹理不可读时以网格面积为准；GPU 仍负责最终透明裁切。

缩放改变会自动更新目标数量，Sprite/Size 改变时重建区域，缩小面积或降低密度后也会减少多余粒子。脚本提供只读 `SurfaceWorldArea` / `TargetParticleCount`，可用于运行时诊断。极小面积的数量存在整数取整误差。

Simple Sprite 使用渲染器的实际本地范围及 Sprite 三角网格，支持不同 Pivot、物体旋转、非均匀缩放、Flip X/Y。开启 `Follow Sprite Shape` 时避免发射到网格轮廓外。

水纹着色器在 GPU 上读取 Sprite Alpha，因此开启 `Keep Inside Sprite` 与 `Follow Sprite Shape` 时，即使没有 Read/Write，也会将最终水纹裁切在可见轮廓内，包括透明镂空。UV 映射支持 Pivot、翻转和图集旋转。若希望连粒子生成位置也排除透明镂空，可开启纹理 Read/Write 并保留 `Check Readable Alpha`；未开启时 CPU 采样使用网格轮廓，不会复制纹理或自动改变导入设置。

Sliced/Tiled 使用 SpriteRenderer 实际大小构成的矩形（包括通过 Size 调整后的范围），不使用原始图片大小，也不进行透明像素裁剪。更换 Sprite、Size 或相关参数后会重建分布；也可以使用组件右键菜单 `Refresh Sprite Shape / 刷新区域`。

粒子速度以世界单位/秒计算，使用世界空间模拟；生成后不会被父对象移动拖走。游戏暂停时停止推进，组件或物体禁用时清空，销毁时释放运行时材质和粒子子对象。
