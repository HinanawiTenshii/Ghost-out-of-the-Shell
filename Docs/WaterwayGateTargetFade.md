# 水闸关联水面淡入淡出

场景为 `Assets/Scenes/Level2/Level2-Floor-1.unity`。用户已确认原请求中的 Floor2 实际指此场景；没有修改 Level2-Floor2。

沿用原来四个水闸的事件目标，没有更换水面位置、颜色、碰撞箱或开关初始状态：

| 水闸 | 关联对象 |
|---|---|
| 1A | LeftWingWater1、LeftWingWater2 |
| 2A | RightWingWater1、RightWingWater2 |
| 3A | DownWingWater1、DownWingWater2 |
| 3B | 3BWater1、3BWater2、3BWater3 |

## 行为

- 关闭：水闸关闸动画完成后，`On Closed` 调用关联对象的 `WaterwayGateTargetFade.FadeOut()`。透明度在默认 0.6 秒内平滑降至零，完成后才 `SetActive(false)`，期间保留对象原有碰撞。
- 开启：水闸开闸动画完成后，`On Opened` 调用 `FadeIn()`。先设为透明，再重新启用对象及其碰撞，随后在默认 0.6 秒内恢复原始透明度。
- 重复下发相同命令不重置进度；淡入淡出中收到相反的淡出/淡入命令，会从当前透明度反转。暂停时动画暂停。
- 每段水面额外保存对应水闸的 `Source Gate` 引用，用于首次进入运行时同步初始状态。当前四个水闸均沿用原场景的初始开启状态；以后配置为初始关闭也会同步隐藏水面。
- 使用缓存的原始 SpriteRenderer 颜色，保留 RGB 和原本的半透明程度，不修改共享材质。支持目标及其现有子对象的 SpriteRenderer；此组件不声明支持任意不透明 Mesh 材质或自定义粒子着色器。
- 水闸原有动画、控制器动作、碰撞时机与编号没有改动。

### 过渡期间控制器锁定

从水闸开始运动到所有绑定对象完全淡入或淡出，对应 `WaterwayGateController` 都不可操作，并隐藏按 E 提示。同一水闸的多个控制器共享此限制，不影响其他水闸；各水面时长不同时等待最后一段完成。锁定依据实际进度而非固定计时，暂停不会提前解锁。

锁定同时覆盖控制器的 `SetOpen`、`ToggleFromExternal` 接口，拒绝的命令不会排队。水闸本身的直接脚本/初始化接口仍保留，便于剧情或读档强制设置状态。淡入淡出组件停用或场景卸载时注销状态，避免残留锁定。

## 配置

每个水面上的 `Waterway Gate Target Fade > Fade Duration` 可单独调整时长，默认 0.6 秒。`Source Gate` 应与水闸事件来源保持一致。

原场景事件只有目标对象、没有选定方法。本次将这些事件接到目标上的淡入淡出组件：开启事件选 `FadeIn()`，关闭事件选 `FadeOut()`，保留原来的分组及事件槽位。

九个受控水面附有 `CameraVisionStreamingExempt`，让其启停由机关独占控制，避免距离卸载中断渐变或在关闸后自动恢复水面。这仅作用于现有九个简单水面对象，不改变全局加载机制。对象完全淡出后仍真正停用，不只是隐藏渲染器。

额外接口 `SetVisibleImmediately(bool)` 可供初始化/读档对接；本次未扩展全局存档系统。

## 验证

- `Tools/TestWaterwayGateTargetFade.ps1`：134 项生产方法及场景绑定检查，含平滑反转、原始半透明恢复、重复调用、初始状态、停用后重新启用、暂停、销毁的子渲染器、无渲染器对象、四闸九对象的所有事件引用与加载豁免，以及过渡锁定、最慢对象完成后解锁和停用清理。
- 原水闸 387 项、控制器 96 项回归检查通过，包含锁定期间拒绝操作、多个控制器共享锁定和完成后恢复交互。
- `Tools/WaterwayGateTargetFade.Validation.targets` 支持新增组件的 Unity API 编译。
- 尚未在 Unity Play Mode 内人工验收最终淡入淡出效果。
