# Level1 Garden 荧光测试

仅 `Assets/Scenes/Level1/Level1-Garden.unity` 启用。公共预制体、全局 CRT 参数、主菜单与其他关卡未修改。

## 运行与对比

重新载入已保存的 Level1-Garden 场景，进入 Play Mode。在根对象 **Garden Fluorescence Test** 上：

- `Test Enabled`：完整新旧对照。关闭时恢复本轮缓存的原色并移除本组件创建的摄像机效果。
- `Adjust Palette`：独立开关配色调整。
- `Enable Bloom`：独立开关新增空间泛光；不关闭此前的灯光、UI glow 或 CRT。
- `Near Intensity / Far Intensity`：近、远光晕，默认 0.85 / 0.22。
- `Threshold / Soft Knee / Radius`：默认 0.66 / 0.12 / 1.5。半、四分之一分辨率模糊，半径随输出高度缩放。

调色仅运行时作用于绑定实例；不在编辑状态写入 SpriteRenderer，不覆盖 prefab。运行中关闭组件、禁用根对象或卸载 Garden 都会恢复颜色、释放效果（包括主摄像机跨场景保留的情况）。编辑状态可保存面板参数，运行模式下的临时参数修改遵循 Unity 的正常恢复规则。

## 范围

- Walls 下的近白色 Sprite 调为冷浅灰，依据既有 1.51 倍 CRT 亮度保留高光空间；不更改碰撞、视野判定或几何体。
- 圆形喷泉、四个花坛石框保持灰石材；水面改为饱和深青蓝，水纹、中心水口与四个反光片仍亮青蓝。
- 喷泉区域及 Decorations 中的 Flower / Flower Center 是新增泛光白名单；木材、土壤、植被与服装不统一染蓝。
- 不新增墙面几何体，不给墙体、地板、视野扇形或整个人物添加全屏阈值泛光。

## 渲染顺序与限制

世界摄像机原画面（已绘制视野遮罩）→ 白名单提取 → 双尺度模糊 → 精确视野裁切 → Blocks 覆盖摄像机 → UI → 现有最终 CRT。

白名单使用缓存的实际 Sprite 顶点、UV、透明度、翻转与当前变换，不使用包围盒代替形状。抽取的是白名单覆盖处的最终可见场景颜色，因而能包含喷泉动态水纹；这是**区域白名单**，不是全场景材质发光系统。在喷泉/花心覆盖范围内经过的其他高亮物体也可能获得少量泛光。若以后推广到所有动态人物，需要专门的材质发光缓冲，不宜直接扩大白名单区域。

两张全分辨率单通道遮罩（不支持 R8 时回退 ARGB32）保证视野裁切精度，其余模糊纹理采用半/四分之一分辨率。临时纹理在 finally 释放，缓存网格/材质/命令缓冲在禁用时释放，无附加摄像机、每帧全场景搜索或逐像素 GameObject。

`CameraCircularVision.TryGetPostEffectVisibilityMesh` 只读复用实际视野多边形，包含额外揭示源；转移遮挡期间或多边形尚未就绪时保守关闭新增光晕。模糊前后均裁切，避免隐藏源参与模糊或光晕越过视野边界。

目前只支持 Simple Sprite 白名单（Garden 绑定的精灵均符合）；不修改 Sprite Mask、Sliced/Tiled 或 UI 的材质。只有场景根对象绑定此测试配置时才启用。

## 验证

- `Tools/TestGardenFluorescence.ps1`：场景引用、开关恢复代码约束、调色亮度余量与遮罩/资源释放结构检查。
- `Tools/GardenFluorescence.Validation.targets`：真实项目 C# 编译。
- `Tools/GardenBloomShaderSmoke.cs`：可在独立 Unity 项目中用生产 shader 执行 GPU 冒烟测试：可见源、不可见源、未选中亮点、模糊扩散、模糊后视野裁切、水面不发光与非对称世界遮罩定位。

仍需在实际 Garden Play Mode 中验收：移动到视野边缘、进入/退出房间、附身缩放、打开 HUD 菜单、切换场景；确认视觉强度、遮罩边缘和实际 GPU 耗时。独立 shader 测试不等于完整关卡渲染验收。

本次验证结果：C# 编译 0 警告 / 0 错误；静态场景引用与渲染约束检查 2993 项通过。独立 Unity GPU 检查因启动授权界面未能执行，已结束该检查进程；未在用户已有的编辑器中运行或切换场景，因此不声称完成 GPU / Play Mode 验收。
