# 炸弹爆炸音效

普通炸弹与超级炸弹共用 `PlacedBomb` 的播放路径。在放置时解析默认片段，爆炸帧播放一次；连锁引爆、立即引爆与读档后的炸弹沿用相同逻辑。

默认资源：`Assets/Resources/Audio/BombExplosion.wav`，约 0.54 秒的复古电子爆破声。与角色死亡的 `Explosion 5` 短音不同，这里使用 `Explosion 3`，保留更厚的低频和噪声衰减。

- 原 `Explosion Audio` 字段保留：自定义片段优先、音高/空间混合/距离/音量设置不变，音量为 0 可静音。
- 空片段自动使用缓存的默认声音，无需逐一修改预制体。
- 独立音源位于炸弹所在场景，带距离卸载豁免；炸弹销毁后声音继续播放，按片段长度与音高自动清理。
- 同一炸弹的重复引爆不会重复播放。不改变伤害、引爆时机、AI 吸引范围与爆炸视觉。

## 来源

Deva / @Shades，**8-Bit Sound Effect Pack (Vol. 001)**，CC0 1.0。

- https://opengameart.org/content/8-bit-sound-effect-pack-vol-001
- https://creativecommons.org/publicdomain/zero/1.0/
- 原始素材及包哈希见 `CharacterFeedbackAudio.md`，使用包内 `Explosion 3.wav`。
- 加工：保留前 0.54 秒、55Hz 高通、4.8kHz 低通、音量 ×0.6、0.5ms 淡入、最后 60ms 淡出；44.1kHz 单声道 PCM16。无开头等待。

鸣谢：8-bit sound effects by @Shades (Deva), CC0, via OpenGameArt.org.

验证包括播放方法替身测试、起音/PCM 导入检查、炸弹卸载保留回归和 Unity API 编译。不等同于游戏内最终试听与混音验收。
