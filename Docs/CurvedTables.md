# 圆桌与四分之一圆弧桌

新增于 `Assets/Prefabs/Decorations/`：

- `Table-Round.prefab`：圆桌，默认半径 0.9，直径 1.8。
- `Table-QuarterArc.prefab`：90° 圆环段桌，默认外半径 1.6、桌面宽度 0.65、内半径 0.95。不是填满中心的扇形桌，弧内留空且没有碰撞。

沿用现有桌子的褐色木框、青绿色桌面和亮青绿色内缘。下沿以轻微下移的深褐色面表示厚度；无木纹或细碎装饰。

拖入场景即可使用。`Geometric Curved Table` 组件可编辑 Shape、Radius、Arc Width、Start Angle、木框/桌面颜色、Solid 和排序设置。圆弧桌的原点位于圆心，默认从右方向逆时针展开 90°；Start Angle 可改朝向，也可围绕该原点旋转或拼接四段。

一个低面数 MeshRenderer、一个 PolygonCollider2D；使用引擎内置 Sprites/Default 材质，无贴图或特殊着色器。网格只在启用/参数变更时生成，四层几何合并在一次渲染中，碰撞沿外轮廓生成，不使用包围盒堵住弧形凹口。编辑器也能显示。默认使用 Default 层，与原桌子一致，不额外加入视野阻挡层。

`Tools/TestCurvedTables.ps1` 从正式代码提取碰撞及三角面构造，检查范围、圆心空区、参数朝向和预制体链接，并渲染 `Docs/CurvedTables-preview.png`。另进行 Unity API 编译；未在 Play Mode 中实测碰撞或 CRT 最终画面。
