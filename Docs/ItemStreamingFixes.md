# 收集特效与炸弹加载保留

## 技能点收集特效

原来的 `GrowthCollectiblePickupEffect.TryGetTarget` 直接比较玩家与特效的 Unity Scene。经过楼层传送的玩家由 `SceneTravelStateManager` 放入 `DontDestroyOnLoad`，但收集特效仍属于所在关卡，因此被误判为玩家已离开关卡，首个更新便清除粒子。

现改为 `ZeldaRuntimeRegistry.GetGameplayScene`，与现有技能、附身粒子的场景判断保持一致。真正切换关卡仍会停止旧特效；同关卡附身换人继续追踪新角色。奖励发放、粒子数量、散开/追踪速度、尾迹和闪光参数均未改变。

## 炸弹

已配置的 `PlacedBomb` 自动持有 `CameraVisionStreamingRegion`，区域半径取吸引半径与伤害矩形半对角线的较大值，使用世界单位，不受炸弹精灵缩放影响。普通炸弹、超级炸弹及其即时/连锁引爆沿用同一实现。

- 区域持有者自动获得加载豁免，计时不会因离开摄像机而停止。
- 区域内已被摄像机停用的对象立即恢复，不受普通激活预算限制。
- 每帧按对象世界包围盒与圆形区域相交检测，支持移动对象及重叠的多枚炸弹。
- 仅恢复由加载管理器停用的对象，不开启关卡逻辑主动关闭的机关。
- 引爆前刷新区域，随后将保留责任交给短时爆炸伤害对象，避免炸弹销毁到下一次物理检测之间目标被停用。
- 伤害对象销毁后区域自动释放；AI 进入调查状态后按原有规则保持加载，直到返回空闲。
- 范围只作用于同一个实际游戏场景，退出/卸载时随对象清理，不添加永久的目标豁免。

## 验证

- `Tools/TestItemStreaming.ps1`：25 项正式方法逻辑测试，覆盖常驻玩家、实际换关卡、附身切换、圆形边界、深度、场景隔离、立即唤醒、机关不误开启、多炸弹重叠与伤害期接续，另有调用接线检查。
- `Tools/TestCameraViewportStreaming.ps1`：121 项摄像机加载回归。
- `Tools/TestSkillEffects.ps1`：67 项技能逻辑回归。
- `Tools/TestBombAndBoxVisuals.ps1`：炸弹图像及已放置/拾取物一致性回归。

这些脚本使用 Unity 数据替身执行正式方法，并非 Play Mode 测试。编译请将 `Tools/ItemStreaming.Validation.targets` 的**绝对路径**作为 `CustomAfterMicrosoftCommonTargets` 传入，避免 Unity 尚未重新生成 csproj 时漏掉新增脚本。

建议在编辑器中验收：从其他楼层进入 Level1-Floor-1 后拾取技能点；分别放置普通/超级炸弹后迅速离开镜头，确认定时爆炸、远处伤害与 AI 调查；再用相邻两枚炸弹检查连锁引爆及其中一枚爆炸后另一枚仍正常计时。
