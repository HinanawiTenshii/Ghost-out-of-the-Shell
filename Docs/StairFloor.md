# 楼梯地板

沿用 CheckerTileFloor 的世界坐标程序化绘制：每片地板只需一个 SpriteRenderer，不生成每级台阶对象，不添加碰撞、通行或楼层切换逻辑。

- `Assets/Materials/StairFloor.mat`：默认向上。
- `Assets/Materials/StairFloor-Horizontal.mat`：默认向右。
- `Assets/Prefabs/Decorations/StairFloor.prefab`：可直接拖入场景的 3×6 地板，中心原点，默认排序 -10（原瓷砖为 -11）。未自动修改现有场景。

选中材质，在 Inspector 中设置：

- **Ascent Direction (World XY)**：Up / Down / Left / Right，表示上楼方向。Up、Down 的踏步边沿为水平线；Left、Right 为竖直线。相反方向会翻转台阶亮暗排列。
- **Step Depth (World Units)**：每级台阶间距，默认 0.6。非等比拉伸地板不会拉伸台阶间距。
- **Stone Tread / Riser Shadow / Step Edge**：踏面、立面阴影、边缘亮色。
- **Riser Fraction / Edge Fraction**：暗面与亮边占每级台阶的比例。
- **Pattern Origin**：世界坐标纹理起点，用于对齐入口、相邻楼梯的图案。

图案朝向以世界坐标为准，不随物体旋转。自带白色矩形精灵，也可将材质赋给已有地板的 SpriteRenderer；建议保持 Renderer Color 为白色以显示原配色。

## 每个对象独立切换方向

选中挂有 SpriteRenderer 的楼梯地面，添加 `Rendering → Stair Floor Direction` 组件，共用同一材质也可以单独设置：

- **Direction / 横向（左右上楼）**：默认向右，台阶边沿为竖直线。
- **Direction / 纵向（上下上楼）**：默认向上，台阶边沿为水平线。
- **Reverse**：反转为向左 / 向下，保留原材质四个上楼方向的能力。

支持编辑器、预制体模式和运行时；添加组件时继承材质当前方向。修改会随场景/预制体保存，支持撤销。禁用或移除组件恢复材质默认方向；没有组件的地面保持原效果，不自动修改场景或预制体。

使用 MaterialPropertyBlock，仅在设置或材质引用改变时更新，保留其他逐对象参数，不复制材质或增加精灵。运行时可设置组件的 `Direction`、`Reverse` 属性。其他颜色、尺寸参数仍由材质控制，需要独立配色时可复制材质。

美术为俯视宽踏面与少量窄色带，保持简单几何风格。踏面使用与棋盘地板、纯色地板协调的靛蓝色，暗面占比 12%，柔和蓝紫亮边占比 4.5%，避免原版粗亮边的灰色金属感；不添加砖缝、噪点或额外精灵。远处低于像素的纹理自动淡化为平均色，减轻扫描线叠加时的摩尔纹。保留精灵透明度裁切，使用与瓷砖相同的透明混合方式。
