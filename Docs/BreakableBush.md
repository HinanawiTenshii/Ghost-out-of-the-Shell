# 可破坏灌木

预制体：`Assets/Prefabs/Decorations/Plants/BreakableBush.prefab`。
拖入场景即可使用，不会自动替换已有装饰灌木。

- `DoorData / Durability = 3`：单次 `attackpower >= 3` 即破坏；较弱攻击只晃动，不累计伤害。
- 根节点 `BoxCollider2D` 为实体碰撞，默认大小 **2.15 × 1.25**，偏移 `(0, -0.025)`，可直接编辑。
- 分层绿色枝叶使用共享的原生精灵资源，不在运行时重新生成灌木图片。
- `BushDestructionEffects` 接管破坏反馈：绿叶抛散、断枝翻转坠落、少量尘土逐渐消失，无碎片碰撞。
- 专属 `BushBreak.wav` 为 0.78 秒程序合成音效（断枝与叶片沙沙声），默认音量 0.7，距离衰减至 18。在该组件中可调整声音、音量、距离和叶片数量。
- 特效与声音脱离已破坏的灌木播放，约 1.35 秒后回收，不进入存档。破坏状态沿用现有 `DoorData` 场景存档逻辑。
- 保留 `DoorData` 原有的属性需求窗口、破坏吸引与受击反馈；普通门和玻璃不使用此专属反馈。

验证脚本：`Tools/BreakableBushSmoke.cs`（隔离 Unity 项目）。
音效重生成/验证：`Tools/GenerateBushAudio.py`，加 `--check` 只验证不改写。
