# 角色受伤与死亡音效

所有继承 `ZeldaCharacterData` 的角色默认使用复古 8-bit 音效。无需批量修改角色预制体或场景。

- 受伤：`Assets/Resources/Audio/CharacterDamage.wav`，约 0.211 秒，短促电子冲击。
- 死亡：`Assets/Resources/Audio/CharacterDeath.wav`，约 0.265 秒，短促电子爆裂；已换用另一段素材，替代此前 0.738 秒的长衰减声音。

音色保留原始芯片/噪声质感，仅柔化极高频并添加短淡入淡出。没有加入写实惨叫、长混响或持续警报。

## 配置与触发

角色数据组件原有 `Damage Audio` / `Death Audio` 的 AudioClip 为空时自动读取并缓存默认音效。已指定的自定义片段优先，现有音量、音高、空间混合、最大距离设置不变；音量为 0 时静音。

非致命有效伤害播放一次受击音；零/负伤害、已死亡角色和被纸箱格挡的攻击不播放。致命伤害只播放死亡声，并停止尚在播放的受伤尾音。死亡逻辑重入不会重复发声。

幽灵（含幽灵形态）成功附身后的旧身体清理不播放死亡音效，包括自定义片段。只针对附身清理静音，不影响其他死亡原因、普通角色附身的死亡声音，或既有通知、消失特效与销毁流程。

死亡音源单独创建在角色实际所在的游戏场景中，带距离卸载豁免，不跟随角色销毁。片段播放完毕后按音高计算时间自动清理；离开关卡时仍随场景正常清理。伤害、死亡通知、附身中断和多米诺连锁逻辑保持原有流程。

## 免费素材来源与署名

**8-Bit Sound Effect Pack (Vol. 001), Deva / @Shades**

- 原始发布：https://opengameart.org/content/8-bit-sound-effect-pack-vol-001
- 作者：https://soundcloud.com/noshades
- 许可：CC0 1.0，https://creativecommons.org/publicdomain/zero/1.0/
- 下载：https://opengameart.org/sites/default/files/8-bit%20Sound%20Effects%20Pack%20001.zip
- 页面与包内 README 均明确允许用于免费或商业游戏，并请求署名 @Shades；本项目在此保留署名。
- 选用文件：`Hit 3.wav` 与 `Explosion 5.wav`。后一项用于角色死亡爆散反馈，并非修改项目中的炸弹音效。

建议正式游戏鸣谢沿用：**8-bit character feedback sound effects by @Shades (Deva), CC0, via OpenGameArt.org.**

加工记录：

- Hit 3：80Hz 高通、7kHz 低通、音量 ×0.85、2ms 淡入、末端约 31ms 淡出。
- Explosion 5：保持原音高；80Hz 高通、6kHz 低通、音量 ×0.55、2ms 淡入、末端约 60ms 淡出。
- 两个输出均为 44.1kHz 单声道 PCM16；无归一化放大，保留混音余量。

校验值（SHA-256）：

- 原包：`ADDFC9D2556FB632B666637EA4E4552F72AD99DEBC72A8F8A77A8EFCA07BB698`
- 受伤：`27751D0E18664025BDA91D394548B4410521B22E8DFE70D34E49FB858B6FD472`
- 死亡：`E6493FF432A9DB5F9EE69881841BFCA905B2A9CB0BB0F5F1AB11B35690BE902F`

## 验证

`Tools/TestCharacterFeedbackAudio.ps1` 使用正式伤害、死亡和音频方法进行 29 项数据替身测试，覆盖普通/致命/格挡/共享伤害、音源复用、死亡重入、幽灵附身静音、自定义片段、场景归属和音高对应清理时长，并检查两个 WAV 资源。另做 Unity API 编译和已有技能、NPC 提示音回归；不等同于 Unity Play Mode 内的听感与混音验证。
