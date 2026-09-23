# NPC 发现提示音（测试素材）

## 行为

所有使用 `ZeldaCharacterAiBase` 或其子类的 NPC 均在状态切换入口播放提示，不需要逐一更新预制体。

- 普通/怀疑/搜索/恢复 → 警告（Alert）：播放一次。
- 普通/怀疑/搜索/恢复 → 敌对（Hostile）：播放一次。
- 警告 → 敌对：不播放。
- 重复设置当前状态：不播放，不在 Tick/Update 中循环播放。
- 敌对 → 警告：属于新的警告状态进入，播放一次。
- 恢复存档、被禁止的状态变化、晕眩后恢复原状态、单纯重新激活对象：不播放。

Inspector 的 `Awareness Sound / 发现提示音` 可配置开关、替换 AudioClip、音量（默认 0.55）及最远可听距离（默认 18 世界单位）。近处保持音量，距离外侧 35% 逐渐衰减；不同游戏场景以及范围外的 NPC 不播放。采用 2D 声音并按玩家平面距离衰减，不受摄像机 Z 位置或缩放干扰。

默认片段通过 `Resources/Audio/NpcAwarenessAlert` 加载并共享缓存，每个发声 NPC 复用自己的 AudioSource，不与攻击/受伤音源混用。

## 素材来源及限制

用户已于 2026-09-19 确认：使用《合金装备》同款音效，先用于当前项目测试。

- 来源页面：https://quicksounds.com/sound/281/metal-gear-solid-alert
- 下载：https://quicksounds.com/uploads/tracks/1798565998_1349385113_1451537983.mp3
- 网站许可页面：https://quicksounds.com/page/license-agreement
- 网站要求的署名：Sound effects from https://quicksounds.com
- 原始 MP3 SHA-256：`E71F6432EBF1392A9B6630C827EE291208610D423BCEEFD918E300E5508CC14B`
- 接入 WAV SHA-256（进一步裁掉 12.855ms 开头空白后）：`9A005B1700A9D0C47742E719136DF08ADE560B57BA22E67BBEA178C0EC96F9E5`

第三方页面将其标为 Metal Gear Solid Alert。虽然网站提供标准使用许可，但未确认该站拥有原游戏音效的再授权权利。本文件及音频导入器均标记为测试素材；**不能将“能下载”理解为已获原作权利人许可。对外发布游戏前应替换为获授权的素材或另行确认授权，并履行实际适用的署名要求。**

处理仅去除首尾静音并添加短淡入/淡出，未重制音色：取原音频 0.68 秒开始的 1.46 秒，4ms 淡入、最后 60ms 淡出，转为 44.1kHz 单声道 PCM16。默认素材位于 `Assets/Resources/Audio/NpcAwarenessAlert.wav`。替换单个 NPC 的片段不等于移除默认 Resources 测试素材；发行前还应替换或移除该默认文件。

2026-09-19 对齐更新：在上述 WAV 基础上进一步去除前 0.0128549 秒，保留约 1ms 起音余量和 0.5ms 防爆音淡入。总长约 1.447 秒，GUID、尾音和触发逻辑不变，详见 `SoundEffectOnsetTrimming.md`。

## 验证

`Tools/TestNpcAwarenessSound.ps1` 提取正式状态切换与音频方法，以数据替身验证全部状态组合、警告升级不重复、重入/重新发现、多 NPC、静音与音量、可听距离、实际场景和存档静默。编译验证 Unity API。未自动操作编辑器进入 Play Mode，仍应在游戏内检查听感及多人同时发现时的混音。
