# 沙漠滤镜

组件：`Rendering/Desert Screen Effect`（`DesertScreenEffect`）。目前未挂载到任何场景或摄像机，也没有自动安装入口。

## 使用

需要启用时，在主摄像机上添加该组件即可。独立使用时通过 Built-in RP 的 `OnRenderImage` 工作；与现有 `CRTScreenEffect` 同时使用时，CRT 的最终合成摄像机先调用沙漠滤镜，再绘制 CRT。无需复制到动态生成的 UI 摄像机，也无需调整组件顺序。

参考效果采用暖褐阴影、砂金高光、降低饱和度、轻微漂移砂雾和细颗粒。视野遮挡几何和摄像机 size 不变，但整个合成画面（包括 UI 与遮罩暗部）会被暖色调处理。

扬沙是屏幕空间的两层程序化粒子，默认从右上往左下漂移，带独立位置、亮度、速度层次及小幅随机扰动，不参与碰撞/AI，也不会创建大量 GameObject。

## 参数

- `Intensity`：总体强度，设为 0 完全旁路。
- `Object Color Preservation`：新组件默认 0.8，保护经过暖光与轻度去饱和处理后的物品颜色，而非恢复原图。设为 0 使用全画面调色；设为 1 仍保留一定沙漠调色。旧组件已保存的数值不会被强制覆盖，但新版 Shader 同样适用。提高保护阈值后，暗蓝环境/视野不再轻易被当成鲜明物品保护。此机制不是物体分层遮罩，极暗物品仍可能受暖色影响，较亮环境也会受到保护。
- `Midtone Color / Highlight Protection`：环境中间色及高光压制；原色保护越强，这些参数对物品的影响越弱。
- `Shadow Color / Highlight Color / Desaturation`：暗部、高光和去饱和度。
- `Haze / Grain`：砂雾与颗粒感。
- `Sand Density / Sand Opacity / Sand Pixel Size`：扬沙密度、不透明度、像素尺寸。
- `Wind Speed / Turbulence`：风速及摆动幅度；主方向保持右上到左下。
- `Animate While Paused`：暂停时是否继续动画。
- `Preview In Editor`：非运行时在摄像机 Game 视图预览。

Shader 位于 Resources 中，避免仅通过名字引用而在构建时被裁剪。运行材质与临时渲染纹理会正确释放；关闭滤镜后不会改变原有 CRT 参数。

验证时建议检查：单独启用、与 CRT 共用、暂停、窗口缩放、切换场景和禁用组件。当前仅完成 C# 编译检查，需在 Unity 内验证 shader 编译与实际观感；该实现针对本项目 Built-in RP，不是 URP Renderer Feature。
