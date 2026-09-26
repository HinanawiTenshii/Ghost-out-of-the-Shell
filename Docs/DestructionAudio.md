# 门与玻璃破坏音效

`DoorData` 的实际破坏流程播放一次声音，和碎片生成处于同一帧；未达耐久的受击、重复攻击、普通开关门和非破坏性对象清理不播放。没有改变碰撞箱、耐久、旋转、AI 调查或碎片逻辑。

- 木门：`Assets/Resources/Audio/DoorBreak.wav`，1.334 秒，较厚的木材撞裂加锐利断裂声，保留碎裂尾音、自然音高。
- 玻璃：`Assets/Resources/Audio/GlassBreak.wav`，1.253 秒，实录玻璃砸碎及碎片落地尾音。
- 44.1kHz、单声道 PCM16，预加载、禁止后台加载；去除起音空白、短淡入和尾端淡出。
- 声音独立于被破坏物体，继承关卡归属并豁免视野卸载，片段结束后自动销毁。

## 编辑器配置

`DoorData > Destruction Audio`：

- **Break Material**：Wood / Glass，决定空片段时使用的默认声音。
- **Break Sound**：指定自定义片段时优先使用；清空恢复材质默认音效。
- **Break Sound Volume**：0–1，0 为静音。旧版本超出 1 的值在播放时安全限制。
- **Break Sound Spatial Blend / Max Distance**：默认 0.8 / 22，采用线性距离衰减和 3 单位最小距离。

现有木门不必逐一绑定音效；`Glass.prefab` 已设置 Glass 类型并绑定新玻璃片段，所有继承实例自动生效。`SampleScene` 两处旧玻璃音效覆盖已同步替换。旧 `glassbrokechanged.wav` 文件保留，没有删除用户资产。以后新建非木材破坏物可在 Break Sound 中指定自己的声音。

## 当前版本来源与许可（2026-09-25 第二版）

用户反馈第一版像互动音效，因此换用以下其他素材，而不是单纯调大原文件音量。

- 木门：**Independent.nu**，由 **qubodup** 发布的 [35 wooden cracks/hits/destructions](https://opengameart.org/content/35-wooden-crackshitsdestructions)，页面标为 [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/)。
  - [原始素材包](https://opengameart.org/sites/default/files/independent_nu_ljudbank-wood_crack_hit_destruction.7z)，使用 `wood_impact/impactwood24.mp3.flac` 和 `wood_impact/crack07.mp3.flac`。
  - 包 SHA-256：`2F9723BE0147C32EA07DB8EF7406229FB8F1A6CE8FEABDF0DBF2DC5EE2AACD1A`。
  - impactwood24 SHA-256：`259BC566A701602D2CA57CDB5963175A54E62FEB83BB22CE04C33021E74E7A1A`。
  - crack07 SHA-256：`9EFA670DAAEDF79E101FD3463EA6F585CCC1079DC7E23785947E68886B276A44`。
- 玻璃：**chewiesmissus** — [Glass Smash.wav](https://freesound.org/people/chewiesmissus/sounds/244238/)，页面标为 CC0；作者说明使用 Zoom H4N 实录。
  - 实际取得的是网站公开提供的 [HQ MP3 试听编码](https://cdn.freesound.org/previews/244/244238_3156517-hq.mp3)，不是需登录下载的原始 WAV。发布页所列素材许可为 CC0，保留实际编码来源以免混淆。
  - 下载 MP3 SHA-256：`F70DB0F3705B1C5344D2BF841CFB2E65287D7ECCA2C55D4692FA45E11D0EB456`。

两项下载及页面核对日期均为 2026-09-25，无强制署名；建议鸣谢 Independent.nu（via qubodup / OpenGameArt）与 chewiesmissus（Freesound）。

### 当前加工

`Tools/PrepareDestructionAudio.ps1` 从 `.codex-temp/Audio/DestructionV2` 重建当前成品并校验三个源文件哈希：

- 木门：impactwood24 取 0.086–1.420 秒，55Hz 高通、10.5kHz 低通、音量 ×0.72；crack07 取 0.013–0.730 秒，100Hz 高通、10.5kHz 低通、音量 ×0.42、尾部淡出，然后混合成单个音频，不增加运行时音源。
- 玻璃：公开 HQ MP3 取 0.187–1.440 秒，90Hz 高通、11kHz 低通、音量 ×0.95。
- 两者均限制峰值至 0.85 并补偿处理延迟，0.5ms 淡入；木门最后 234ms、玻璃最后 213ms 淡出。单声道 44.1kHz PCM16，文件 GUID 不变，不更改破坏判定或音量配置。
- 实测成品峰值：木门约 -1.4dBFS、玻璃约 -2.5dBFS，均无成品削波；起音检查在 -40dB 阈值下不超过 3ms。
- 木门成品 SHA-256：`5C478035E606D375CA3669ECF52CCAAC037053E6C87178BA8ED03D056D4F613B`。
- 玻璃成品 SHA-256：`7358C76A6598C95476789740394E071F79A0F7703865753B12F00A765926DC0F`。

验证：19 项生产破坏方法替身测试、预制体/场景引用、PCM 格式、1.2–1.5 秒时长、全部 15 项音效起音和 GUID、20 项音频来源清单一致性。尚未在 Unity Play Mode 内完成试听与混音验收。

## 历史第一版来源与许可（已替换，保留版权溯源）

两项发布页均标明 **CC0 1.0**，可用于商业项目，无强制署名。为便于溯源仍保留作者与链接：

1. **rubberduck — 75 CC0 breaking / falling / hit sfx**
   - 发布页：https://opengameart.org/content/75-cc0-breaking-falling-hit-sfx
   - 下载：https://opengameart.org/sites/default/files/sfx_breaking_and_falling.zip
   - 选用文件：`bfh1_wood_breaking_03.ogg`。
   - 原包 SHA-256：`E6EE04D91C5F4D30CFDA1260D2C9D1FAF96FDA36319287215FBD07BCB1A80451`。
2. **Till Behrend — Glass Break**，由 TinyWorlds 经作者允许发布。
   - 发布页：https://opengameart.org/content/glass-break
   - 下载：https://opengameart.org/sites/default/files/glass_breaking.wav
   - 原文件 SHA-256：`37D29069C885AFE3F7CA639293FA679FB6511A6A1E54316F0E403E4493F502B3`。

许可：https://creativecommons.org/publicdomain/zero/1.0/

## 历史第一版加工与验证

第一版曾从 `.codex-temp/Audio` 中的原始文件生成以下成品。当前处理脚本已改为第二版；旧成品备份位于 `.codex-temp/Audio/DestructionV2/Previous`：

- 木门：裁取 0.0085–0.30 秒，48kHz 原音以 40.8kHz 解释后重采样；65Hz 高通、6.5kHz 低通、音量 ×0.8、0.5ms 淡入、约 53ms 淡出。
- 玻璃：保留前 0.85 秒；150Hz 高通、8kHz 低通、音量 ×0.65、0.5ms 淡入、150ms 淡出。
- 门成品 SHA-256：`37A8D1F2ED4C2BB75E72268583086277B200F0C106F26726B1359068FDDB0345`。
- 玻璃成品 SHA-256：`3493974E4F61565255A5C6379B9AEBF7D84C3F1E07BE38125300A2634B4CCBF8`。

验证涵盖生产播放/破坏方法的 Unity 替身测试、音频格式与起音检查、Unity API 编译；并非 Unity Play Mode 内的最终试听与混音验收。
