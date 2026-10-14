# 电门电流音效

## 素材与许可（2026-09-26）

作者/上传者 **faxcorp**，素材包 [Electricity Game Sound Pack](https://opengameart.org/content/electricity-game-sound-pack)，页面标注 [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/)。CC0 无强制署名，项目仍记录作者与修改。来源页快照 `AudioLicenses/ElectricityGameSoundPack.html`，SHA-256：`4574CCEDA558B64D543CA5E93644F1755E55C792563FB5B86B0637F83CA209C4`。

| 用途 | 原件下载 | 成品（Resources/Audio/ElectricGate） | 时长 |
|---|---|---|---|
| 通电底音 | [shieldloop.wav](https://opengameart.org/sites/default/files/shieldloop.wav) | CurrentLoop.wav | 0.982449 秒 |
| 增强警告 | [snaploop.wav](https://opengameart.org/sites/default/files/snaploop.wav) | WarningLoop.wav | 4.897415 秒 |
| 攻击电击 | [shieldhit.wav](https://opengameart.org/sites/default/files/shieldhit.wav) | Discharge.wav | 0.44 秒 |

原件长期保存在 `AudioOriginals/ElectricGate`，原件及成品哈希见 `AudioAssetInventory.json`。另下载并比较了 [crackleelectricityloop.wav](https://opengameart.org/sites/default/files/crackleelectricityloop.wav)，动态范围不适合作为此版警告层，未用于成品；其 SHA-256 为 `C872FF9719F9BDF503E5D87FE9B1B128255FE2F9506E3A380FF022B83E0B7BE8`。

可重复加工：`Tools/PrepareElectricGateAudio.ps1`，执行前校验源哈希。两个循环过滤过低/过高频，40ms 交叉淡化拼成连续周期；警告层额外压缩动态范围。电击截取原件 0.052–0.492 秒，0.3ms 淡入与 180ms 淡出，避免前导空白及过长尾音。全部 44.1kHz、单声道 PCM16、同步预载，无后台解码首播延迟。

## 运行行为

- 每条唯一电门连线一个 `ElectricGateAudio`，总共三个复用 AudioSource；两根柱子不会重复发出双份循环声。
- 电流实际启用时循环底音，权限不允许的角色靠近、视觉电流增强时叠加另一段警告电流循环；离开后平滑退出。幽灵和允许通行者不会触发警告声，沿用现有警告条件。
- 攻击方法内同步播放一次短电击，双向电弧共用一个声部；同一连线 80ms 内多名目标合并声音，伤害与电池扣电逻辑不变。最后一点电量耗尽仍完整播放电击尾音。
- 对角色到电流线段的最近 XY 距离做平滑衰减：1.5 世界单位内保持设定音量，16 单位外完全静音；攻击音按攻击点距离。轻度左右声像（最大 0.65），忽略摄像机 Z=-10 的深度差，避免 2D 游戏中错误的 3D 距离衰减。没有墙体声音遮挡/混响。
- 底音 80ms 渐入/渐出，警告层 120ms 过渡；超出听觉距离时停用循环声部。暂停、交互菜单和读取存档时暂停声音；跨场景不串音；柱子失效、断开配对和卸载时停止并随连线销毁。
- 默认运行时音量：底音 0.24、警告 0.42、电击 0.75。可在运行时连线对象的 `ElectricGateAudio` 上临时调试。
- 此音效是表现层，不额外触发 NPC 调查、不改变通行、伤害、警告距离、视野或电量。

测试：`Tools/TestElectricGateAudio.ps1`（生产代码配合 Unity stub）、`Tools/TestElectricGate.ps1`（既有电门回归）、`Tools/TestAudioSourceInventory.ps1`（23 个现存音频资源身份/来源登记）。完整 Unity Play Mode 的听感与混音仍需实际验收。
