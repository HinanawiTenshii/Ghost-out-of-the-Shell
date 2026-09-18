# 地下区域水光滤镜

组件：`WaterCausticsScreenEffect`，着色器：`Resources/Shaders/WaterCausticsScreen`。

在主摄像机上选择 Add Component → Rendering → Water Caustics Screen Effect 即可使用。不需要创建材质或真正的水面。本次没有修改场景或摄像机预制体。

## 表现与参数

- 冷青色、大片柔和渐变的随机明暗光斑。单一低频场决定亮斑的完整轮廓，较弱的变形让亮斑缓慢漂移、拉伸和消散；次级流动只改变内部亮度，不再切碎轮廓。亮部有较均匀的实心核心和柔和边缘，提高出现门槛，使暗部占据更大面积。不绘制细线网状焦散，也不扭曲角色、UI或场景轮廓。
- `Intensity`：整体强度；`Reflection Brightness`：反射光亮度；`Reflection Color`：反射光颜色。
- `Ambient Tint`：轻微冷色环境调色；设为零时不做基础调色，动态亮斑和暗部效果仍保留。
- `Pattern Scale`：相对光斑密度，越大光斑越小；默认 1.2。内部已将旧纹理尺寸放大，已有组件不需要重置就能使用大片光斑效果。
- `Gradient Softness`：明暗渐变柔和度，默认 0.7；代替旧版 `Line Width`，旧线宽数据不再参与渲染。
- `Shadow Strength`：流动暗部强度，默认 0.4；零表示只增加反射亮光。
- `Distortion`：光斑变形幅度；`Flow Direction`：主流动方向和相对速度，次级流动独立变化，避免整张纹理僵硬平移。
- `Animation Speed`：变化速度；零为静态。
- `Anchor To World`：默认启用，正交摄像机移动和缩放时，纹理保持世界坐标位置。关闭后按屏幕坐标绘制，`Pattern Scale` 的单位改为每屏幕高度，建议此时设为 5–8。透视摄像机使用屏幕坐标，不进行真实表面投影。
- `Darkness Threshold`：对近黑色像素衰减水光，避免让黑色视野遮罩发亮。不是场景可见性判断，不改变视野或卸载规则。
- `Animate While Paused`、`Preview In Editor`：暂停动画和编辑模式 Game 视图预览选项。

## 合成与性能

适用于本项目的 Built-in 渲染管线。可独立使用，也可与 CRT 共存：挂在同一个主摄像机上，由已有 CRT 最终合成摄像机统一执行一次水光处理，再执行 CRT。若同时启用沙漠滤镜，顺序为沙漠 → 水光 → CRT；地下场景通常只需水光和 CRT。

这是全屏风格化滤镜，并不识别真实天花板或水面，也不对对象产生物理照明。最终合成中的可见 UI 同样可能受到轻微水光影响，较亮像素会受到高光保护。

单次全屏着色、一次源图采样、四组平滑噪声计算，无逐帧随机跳变、粒子对象、全场景扫描或额外场景摄像机。不使用历史帧混合，避免运动拖影。启用后有额外 GPU 开销，关闭组件或将强度设为零时不执行水光着色。临时渲染纹理在合成后释放；材质在组件停用时释放。着色器放在 Resources 中以随构建包含。

可用 `Tools/TestWaterCausticsShader.ps1` 在 Windows 上检查片元 HLSL 的 Direct3D 编译。该检查仅替代 Unity 的输入结构体声明，不替代 Unity 的完整 Shader 导入或运行时视觉验证。
