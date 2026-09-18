# 双开推拉门

预制体：`Assets/Prefabs/Decorations/DoubleSlidingDoor.prefab`。

根对象 `DoubleSlidingDoor_CenterAxis` 是不参与渲染、没有碰撞的中轴。两个矩形在闭合时以各自内侧短边的中心接触中轴，沿根对象本地 X 轴分别向负方向、正方向滑开。旋转根对象 Z 轴 90 度即可改成上下开合。

## 编辑器配置

- `Negative Size` / `Positive Size`：两片门板各自的长度 X、厚度 Y。Y 被限制为不大于 X，保证始终沿长边滑动。
- `Negative Color` / `Positive Color`：门板各自颜色。
- `Open`：初始打开状态；运行时更改也会驱动动画。编辑模式直接预览终点位置。
- `Move Duration`：完整开/关门行程所用秒数，默认 0.8。中途反向会从当前位置返回，不跳变。
- `Slide Distance`：每片门板的滑动距离；默认 0 表示使用各自长度，确保完整让开原门洞。
- `Extra Travel`：在上述基础上额外退让的距离。
- `Solid Leaves`：门板是否阻挡移动。默认开启，两块 BoxCollider2D 始终跟随门板并匹配尺寸；中轴不会挡路。
- `On Opened` / `On Closed`：动画到达终点时的事件。

尺寸、颜色、局部坐标由根组件管理，请通过根组件调整，不要单独移动门板子对象。编辑器中选中时显示的青色中轴辅助线不会出现在游戏画面中。

## 触发条件

1. **现有拉杆**：将推拉门的根对象拖到 `LeverData` 的任意 `Linked Mechanism` 栏位；每次成功操作拉杆会开/关门，而不是禁用整个门对象。魔像操作拉杆也使用该信号流程。
2. **其他机关/任务条件**：从已有 UnityEvent 绑定 `DoubleSlidingDoor.Open()`、`Close()`、`ToggleFromExternal()` 或动态布尔参数方法 `SetConditionSatisfied(bool)`。自定义脚本也可调用这些方法。
3. **测试**：运行时切换根组件的 `Open`，或使用组件右键菜单的 Open Door / Close Door。

目前不预设距离、权限、密码或自动感应条件，也不包含旧旋转门的受击破坏、撬锁和防夹逻辑。门两侧应留足退让空间。移动会通知已有视野系统刷新受影响区域；没有改变旧门预制体，也没有自动放入任何场景。新组件尚未接入存档恢复流程。
