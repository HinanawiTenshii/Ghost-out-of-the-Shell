# 项目音效来源与版权记录

整理日期：2026-10-04。用于以后制作游戏鸣谢、核对素材出处和发行前检查；本文件不是新增的授权证明。本次将门与玻璃破坏音效替换为程序化合成（S11），此前沙虫合成音效为 S10。手铳发射音效已由 S1 替换为 S9 黑火药录音；电门通电、警告、电击音效（S8）及此前门与玻璃替换记录继续保留。

## 范围与证据

覆盖截至 2026-10-10 的 `Assets` 下全部 **34 个音效**，另附 **5 首背景音乐**，包括暂未检出引用的历史保留资源。含沙虫（S10）、能量水晶（S12）、恐惧咆哮（S13）、药水碎裂（S14）、炮台发射（S15）与圣光打击（S16）的项目内合成声音。已被替换且不再存在的旧版本只保留已有加工文档记录，不虚构其完整使用历史。

配套 [AudioAssetInventory.json](AudioAssetInventory.json) 保存每个现存文件的 GUID、SHA-256、文件大小、时长、内嵌标签及原始备份标签，便于未来替换或改名后追溯。临时备份目录不应作为唯一的长期来源证据。

证据分级：

- **已记录来源**：已有下载记录、原始文件名和发布页许可说明。
- **标签溯源**：文件内嵌作者/专辑标签可以指向作者或站点，但缺少原始下载记录，不等于已经完整确认授权链。
- **待确认**：无法确认来源或原作授权，发行前应补证或替换。

“未检出引用”仅指本次静态检查没有找到场景、预制体、资源文件的 GUID 引用或明确的加载路径，不证明从未使用，也不保证不会被打包。尤其 `Resources` 下的测试素材不能只靠取消引用来处理。

## 音效逐项清单

来源编号对应下文的作者、链接、许可及署名要求。

| 当前资源路径 | 原始素材 / 出处 | 使用情况 | 来源与确认程度 |
|---|---|---|---|
| `Assets/Resources/Audio/HandgunFire.wav` | `Black Powder.wav` | 一次性手铳发射，短促黑火药爆响 | S9，已记录来源 |
| `Assets/Resources/Audio/ElectricGate/CurrentLoop.wav` | `shieldloop.wav` | 电门有效通电时的循环底音 | S8，已记录来源 |
| `Assets/Resources/Audio/ElectricGate/WarningLoop.wav` | `snaploop.wav` | 电门警告增强时的电流层 | S8，已记录来源 |
| `Assets/Resources/Audio/ElectricGate/Discharge.wav` | `shieldhit.wav` | 电门攻击瞬间的电击声 | S8，已记录来源 |
| `Assets/Resources/Audio/CharacterDamage.wav` | `Hit 3.wav` | 角色受伤默认音效 | S1，已记录来源 |
| `Assets/Resources/Audio/CharacterDeath.wav` | `Explosion 5.wav` | 角色死亡默认音效；替换过较长版本 | S1，已记录来源 |
| `Assets/Resources/Audio/BombExplosion.wav` | `Explosion 3.wav` | 普通炸弹、超级炸弹爆炸 | S1，已记录来源 |
| `Assets/Resources/Audio/DoorBreak.wav` | 原创程序合成，GenerateDestructionAudio.py | 木门撞裂与碎屑声，0.52 秒 | S11；历史录音 S6 / S2 |
| `Assets/Resources/Audio/GlassBreak.wav` | 原创程序合成，GenerateDestructionAudio.py | 玻璃被砸碎；玻璃瓶碎裂也复用，0.64 秒 | S11；历史录音 S7 / S3 |
| `Assets/Resources/Audio/NpcAwarenessAlert.wav` | Metal Gear Solid Alert，QuickSounds 下载 | NPC 进入警告/敌对的提示；仅获用户同意用于项目测试 | S4，下载出处明确，原作授权待确认 |
| `Assets/SoundEffect/cncl01.wav` | 备份 `cncl01.mp3`，MusMus / watson | 历史保留，未检出当前引用 | S5，标签溯源 |
| `Assets/SoundEffect/cncl02.wav` | 备份 `cncl02.mp3`，MusMus / watson | 历史保留，未检出当前引用 | S5，标签溯源 |
| `Assets/SoundEffect/cncl04.wav` | 备份 `cncl04.mp3`，MusMus / watson | 历史保留，未检出当前引用 | S5，标签溯源 |
| `Assets/SoundEffect/cncl05.wav` | 备份 `cncl05.mp3`，MusMus / watson | 历史保留，未检出当前引用 | S5，标签溯源 |
| `Assets/SoundEffect/cncl06.wav` | 备份 `cncl06.mp3`，MusMus / watson | 历史保留，未检出当前引用 | S5，标签溯源 |
| `Assets/SoundEffect/cncl07.wav` | 备份 `cncl07.mp3`，MusMus / watson | 历史保留，未检出当前引用 | S5，标签溯源 |
| `Assets/SoundEffect/Hit.wav` | 备份 `Hit.mp3`，MusMus / watson；原站素材名称未确认 | 历史保留，未检出当前引用；不是 S1 的 `Hit 3.wav` | S5，标签溯源，具体条目待确认 |
| `Assets/SoundEffect/MGS 驚嘆號音效.wav` | 备份同名 MP3；原始下载网址、作者和许可未留存 | 历史保留，未检出当前引用 | 待确认；不能据名称认定与 S4 是同次下载或同一许可 |
| `Assets/SoundEffect/glassbrokechanged.wav` | 原始文件、作者、下载地址和许可均未确认 | 旧玻璃音效，相关已知引用已换为新 `GlassBreak.wav` | 待确认；不能套用 S3 的 CC0 许可 |

### S1 — Deva / @Shades：8-Bit Sound Effect Pack (Vol. 001)

- 作者/署名：**@Shades (Deva)**。
- [原始发布页](https://opengameart.org/content/8-bit-sound-effect-pack-vol-001)、[作者主页](https://soundcloud.com/noshades)、[原始 ZIP](https://opengameart.org/sites/default/files/8-bit%20Sound%20Effects%20Pack%20001.zip)。
- 许可：[CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/)。页面及包内 README 说明可用于免费或商业游戏；作者请求署名 @Shades，记录为尊重作者的鸣谢，不误写为 CC0 强制署名条件。
- 原包 SHA-256：`ADDFC9D2556FB632B666637EA4E4552F72AD99DEBC72A8F8A77A8EFCA07BB698`。
- 本项目有裁切、滤波、音量调整和短淡入淡出，输出单声道 PCM16。受伤约 0.211 秒，死亡约 0.265 秒，炸弹约 0.54 秒。
- 详细加工记录：[角色受伤与死亡](CharacterFeedbackAudio.md)、[炸弹爆炸](BombExplosionAudio.md)。此前较长的死亡音效已被替换，不应把旧版本时长误记为当前文件。
- 手铳历史版本曾使用 `Shoot 2.wav`，约 0.119 秒；2026-09-27 因电子激光感过强改用 S9。旧原件及包内 README 仍保存在 `Docs/AudioOriginals/Handgun/`，不再用于手铳成品。

### S2 — rubberduck：75 CC0 breaking / falling / hit sfx（历史第一版木门，已替换）

- 作者/署名：**rubberduck**。
- [原始发布页](https://opengameart.org/content/75-cc0-breaking-falling-hit-sfx)、[原始 ZIP](https://opengameart.org/sites/default/files/sfx_breaking_and_falling.zip)。
- 许可：[CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/)，无强制署名，仍建议保留作者。
- 选用 `bfh1_wood_breaking_03.ogg`，裁切、略降音高、滤波和淡出，成品约 0.343 秒。
- 原包 SHA-256：`E6EE04D91C5F4D30CFDA1260D2C9D1FAF96FDA36319287215FBD07BCB1A80451`。
- 详细记录：[门与玻璃破坏音效](DestructionAudio.md)。

### S3 — Till Behrend：Glass Break（历史第一版玻璃，已替换）

- 作者：**Till Behrend**；上传者 **TinyWorlds**，发布页说明经作者允许上传。不要把上传者误记为原作者。
- [原始发布页](https://opengameart.org/content/glass-break)、[原始 WAV](https://opengameart.org/sites/default/files/glass_breaking.wav)。
- 许可：[CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/)，无强制署名，仍建议保留作者。
- 裁切至约 0.850 秒，滤波、降低音量和短淡入淡出。
- 原文件 SHA-256：`37D29069C885AFE3F7CA639293FA679FB6511A6A1E54316F0E403E4493F502B3`。
- 详细记录：[门与玻璃破坏音效](DestructionAudio.md)。

### S4 — QuickSounds：Metal Gear Solid Alert（测试用途，授权未确认）

- [下载来源页](https://quicksounds.com/sound/281/metal-gear-solid-alert)、[原始 MP3](https://quicksounds.com/uploads/tracks/1798565998_1349385113_1451537983.mp3)、[网站许可页](https://quicksounds.com/page/license-agreement)。
- 关联作品：Metal Gear Solid。具体录音的原始作者/权利人及该站的转授权链未确认，**QuickSounds 是下载平台，不等于音效原作者或权利人**。
- 既有接入记录中的网站署名：`Sound effects from https://quicksounds.com`。本次未能重新读取许可页，以已有记录保留，不将其描述为本次重新确认的授权。
- 用户于 2026-09-19 同意先用于当前项目测试，不代表原作权利人的分发许可。**对外发布前应获得有效授权或替换；仅补上鸣谢不解决授权缺口。**
- 原始 MP3 SHA-256：`E71F6432EBF1392A9B6630C827EE291208610D423BCEEFD918E300E5508CC14B`。
- 去除首尾空白、短淡入淡出、转换 WAV，后续再裁掉约 12.855ms 起音空白；成品约 1.447 秒。
- 详细记录：[NPC 发现提示音](NpcAwarenessSound.md)。本条来源只对应 `NpcAwarenessAlert.wav`，不自动适用于旧同名 MGS 资源。

### S5 — MusMus / watson（旧音效标签溯源）

- 作者：**watson**；站点：**MusMus**。
- [官方音效页面](https://musmus.main.jp/se.html)、[官方使用条款](https://musmus.main.jp/info.html)。本次于 2026-09-25 核对官方页面。
- 原 MP3 备份的 `artist` / `album_artist` 为 `watson`，`album` 为 `MusMus`，年份为 2014。六个 cncl 和 Hit 均有这些标签；原始备份路径及标签已记录到配套 JSON。
- cncl 的名称与站点取消音类别相符，但未逐个完成官方原始文件比对。`Hit.mp3` 的原站条目名称也未确认。标签是来源线索，不是不可伪造的授权凭证。
- 使用站点自身条款，**不是 CC0**：按条款用于作品，须标示 MusMus；版权仍属 watson。允许剪裁、淡入淡出、音质等加工，不可将素材独立再分发或登记为自己的内容识别资产。官方条款对素材取得渠道有限定，现存文件的实际下载渠道仍需补证。
- 建议完整署名：`フリーBGM・音楽素材MusMus https://musmus.main.jp`。
- WAV 来自旧 MP3 转换与起音裁剪，GUID 保留；精确裁剪量见 [起音对齐记录](SoundEffectOnsetTrimming.md)。

### S6 — Independent.nu：35 wooden cracks/hits/destructions（历史第二版木门，已替换）

- 作者 **Independent.nu**，上传者 **qubodup**。
- [原始发布页](https://opengameart.org/content/35-wooden-crackshitsdestructions)、[原始素材包](https://opengameart.org/sites/default/files/independent_nu_ljudbank-wood_crack_hit_destruction.7z)。下载及许可核对日期：2026-09-25。
- 发布页许可：[CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/)，无强制署名，仍建议保留作者及上传者。
- 使用包内 `wood_impact/impactwood24.mp3.flac` 与 `wood_impact/crack07.mp3.flac`，裁掉起音空白、混合撞裂与断裂声、滤波、限峰与淡出，成品 1.334 秒。保持自然音高。
- 原包 SHA-256：`2F9723BE0147C32EA07DB8EF7406229FB8F1A6CE8FEABDF0DBF2DC5EE2AACD1A`。
- 源文件及成品哈希、加工参数：[门与玻璃破坏音效](DestructionAudio.md)。第一版 S2 来源保留，不再是当前成品作者。

### S7 — chewiesmissus：Glass Smash.wav（历史第二版玻璃，已替换）

- 作者 **chewiesmissus**，作者说明为 Zoom H4N 实录玻璃砸碎。
- [原始发布页](https://freesound.org/people/chewiesmissus/sounds/244238/)、[实际下载的公开 HQ MP3](https://cdn.freesound.org/previews/244/244238_3156517-hq.mp3)。下载及许可核对日期：2026-09-25。
- 发布页许可：[CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/)，无强制署名，仍建议保留作者及 Freesound 链接。
- 取得的是网站公开试听编码，**不是**需登录下载的原始 WAV。MP3 SHA-256：`F70DB0F3705B1C5344D2BF841CFB2E65287D7ECCA2C55D4692FA45E11D0EB456`。
- 截取 0.187–1.440 秒，轻度滤波、限峰和淡入淡出，转为单声道 PCM16；成品 1.253 秒。详细记录：[门与玻璃破坏音效](DestructionAudio.md)。第一版 S3 来源继续保留为历史条目。

### S8 — faxcorp：Electricity Game Sound Pack（电门）

- 作者/上传者：**faxcorp**；[原始发布页](https://opengameart.org/content/electricity-game-sound-pack)。2026-09-26 下载、核对页面 CC0 标记。
- 许可：[CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/)，无强制署名，仍建议鸣谢作者。不将平台标注描述为独立权利担保。
- 使用 `shieldloop.wav`、`snaploop.wav`、`shieldhit.wav`。滤波、动态范围调整、循环接缝交叉淡化；攻击声去掉约 52ms 前导并缩短到 0.44 秒；输出单声道 PCM16。
- 原件长期存于 `Docs/AudioOriginals/ElectricGate/`，来源页快照见 `Docs/AudioLicenses/ElectricityGameSoundPack.html`；精确 URL、哈希、加工脚本和运行逻辑见 [电门音效记录](ElectricGateAudio.md)。
- 另比较了同页 `crackleelectricityloop.wav`，未用于成品；原件保留但不放入 Assets，也不随游戏资源打包。

### S9 — kurt：Gunshots（手铳黑火药爆响）

- 作者/上传者：**kurt**；[发布页](https://opengameart.org/node/70780)，2026-09-27 核对 CC0 标记及无需署名说明。建议鸣谢作者；页面标注不是独立权利担保。
- 素材：[`Black Powder.wav`](https://opengameart.org/sites/default/files/Black%20Powder.wav)，截取首个爆响并滤波、压短尾音，输出 0.32 秒单声道 PCM16，不包含后续枪声。
- 来源页快照：`Docs/AudioLicenses/Gunshots-kurt.html`；原件：`Docs/AudioOriginals/Handgun/Black Powder.wav`。SHA-256 和处理步骤见 [手铳音效记录](HandgunAudioAndInvestigation.md)，可用 `Tools/PrepareHandgunAudio.ps1` 重建。

### S10 — 项目内程序化合成：沙虫音效

制作日期：2026-10-04。由 `Tools/GenerateSandwormAudio.py` 使用 Python 标准库生成，采用固定随机种子的颗粒噪声、低频波形、滤波及短促包络；无下载或采样第三方录音。角色受击、死亡与炸弹音效仅作为音色、时长与响度的参考，不混入其音频数据。无需新增第三方素材署名；不将本记录视为外部许可证明。

| 当前资源路径 | 用途 |
|---|---|
| `Assets/Resources/Audio/Sandworm/MovementLoop.wav` | 地下移动的低沉沙砾循环，按玩家与最近身体部位的距离衰减 |
| `Assets/Resources/Audio/Sandworm/Breach.wav` | 头部破土瞬间的破裂冲击 |
| `Assets/Resources/Audio/Sandworm/EmergenceLoop.wav` | 身体逐节出土的颗粒摩擦 |
| `Assets/Resources/Audio/Sandworm/BurrowImpact.wav` | 入土瞬间冲击，恢复入土时音量较低 |
| `Assets/Resources/Audio/Sandworm/BurrowLoop.wav` | 身体逐节入土的碎土声 |
| `Assets/Resources/Audio/Sandworm/BlastFall.wav` | 吞下爆炸物后的体内爆响与下坠 |
| `Assets/Resources/Audio/Sandworm/GroundImpact.wav` | 坠地重击 |
| `Assets/Resources/Audio/Sandworm/DeathSegment.wav` | 尾部及身体每节死亡爆炸的短促甲壳碎裂声，0.23 秒 |
| `Assets/Resources/Audio/Sandworm/DeathHead.wav` | 头部最终爆炸，较厚重的低频爆裂和碎屑尾音，0.72 秒 |

格式统一为单声道 44.1 kHz、16-bit PCM，Unity 预加载 PCM，不引入压缩解码首播延迟。循环做接缝混合；单次声音末端淡出。重新合成后应同步更新清单中的哈希。

## 背景音乐附录

虽然本次主要记录音效，也一并登记当前全部 BGM，避免以后制作版权页时遗漏。以下作者与标题由现存 MP3 标签确认；历史下载日期及下载凭据未留存。

| 当前资源路径 | 原曲标题 | 作者 / 来源 | 当前检出引用 |
|---|---|---|---|
| `Assets/BGMS/MusMus-BGM-157.mp3` | 世界は灰色だったのに | watson / MusMus | TitleScreen |
| `Assets/BGMS/MusMus-PKPK-002.mp3` | MusMus Quest ～城～ | watson / MusMus | 暂未检出静态引用 |
| `Assets/BGMS/m-art_UndergroundInvasion.mp3` | 地下攻略 | Napi / M-ART | Level Background Music 预制体 |
| `Assets/BGMS/m-art_RoyalGarden.mp3` | ロイヤルガーデン | Napi / M-ART | Level1-Garden、Level1-Floor-1、Floor1、Floor2、Floor3 |
| `Assets/BGMS/m-art_Jimejime.mp3` | じめじめ地面 | Napi / M-ART | Level0 |

- MusMus：[官方站点](https://musmus.main.jp/)、[使用条款](https://musmus.main.jp/info.html)，署名及取得渠道核对要求同 S5。
- M-ART：[官方站点](https://mart.kitunebi.com/)、[使用条款](https://mart.kitunebi.com/info.html)、[野外/地下城曲目页](https://mart.kitunebi.com/music_field.html)。文件 comment 标签包含官方站址，作者为 Napi。
- M-ART 条款于 2026-09-25 核对：可用于商业游戏/视频，通常不强制署名或报告；版权不放弃，允许常规音频加工。若制作编曲，须按条款标明作者并说明为编曲；不能以素材或原声带形式独立再分发。不是 CC0。为方便溯源，仍建议署名 `Music by Napi / M-ART — https://mart.kitunebi.com/`。

## 可用于鸣谢页的署名草稿

此段是作者信息汇总，不是全部现存素材已经具备发行许可的声明。按最终实际使用情况删减；先完成下方待确认事项。MGS 与未知旧素材不纳入这一常规署名段。

```text
Sound effects
Gunshots / Black Powder.wav — kurt
https://opengameart.org/node/70780
Electricity Game Sound Pack — faxcorp
https://opengameart.org/content/electricity-game-sound-pack
8-Bit Sound Effect Pack (Vol. 001) — @Shades (Deva)
https://opengameart.org/content/8-bit-sound-effect-pack-vol-001
35 wooden cracks/hits/destructions — Independent.nu (shared by qubodup)
https://opengameart.org/content/35-wooden-crackshitsdestructions
Glass Smash.wav — chewiesmissus (Freesound)
https://freesound.org/people/chewiesmissus/sounds/244238/
The above sound effects are released under CC0 1.0.
https://creativecommons.org/publicdomain/zero/1.0/
Edited for in-game playback.

Music / sound effects
フリーBGM・音楽素材MusMus https://musmus.main.jp
Composer: watson

Music
Napi / M-ART — https://mart.kitunebi.com/
```

## 发行前待办与后续维护

1. `NpcAwarenessAlert.wav`：补原作有效授权或换用明确可分发素材，且处理默认 Resources 文件；不是只给 NPC 换引用即可。
2. 旧 `MGS 驚嘆號音效.wav`：确认独立来源与许可，或确保不再随发行物提供。
3. 旧 `glassbrokechanged.wav`：补作者、下载页、许可，或替换/排除。当前记录为未知，不套用新玻璃音效作者。
4. MusMus 音效及 BGM：补官方下载记录；进一步核对 cncl 文件和 Hit 的原始条目。保留要求的站点署名。
5. M-ART BGM：保留作者信息，补下载日期/原始包记录，发行时复核适用条款。
6. 后续新增或替换音频时，同步更新本清单和 JSON：本地路径、原始文件名、作者与上传者、发布及下载 URL、取得日期、许可版本、署名要求、加工说明、原件与成品哈希。保存许可页或随包 README 证据，不能只保存搜索结果或临时下载地址。
7. 可运行 `Tools/TestAudioSourceInventory.ps1` 检查清单是否覆盖现存音频，以及资源是否在记录后被替换。该检查只能确认记录一致性，不能证明法律授权。

### S12 — 项目内程序合成：能量水晶碎裂

- 生成日期：2026-10-09；文件 `Assets/Resources/Audio/EnergyCrystalShatter.wav`。
- 脚本 `Tools/GenerateEnergyCrystalAudio.py`，仅使用 Python 标准库与项目原有合成辅助函数；未使用录音采样。
- 以短促矿物开裂、非谐波振荡和碎屑颗粒合成，保持现有 CRT 音效的干脆听感；时长 0.62 秒，起音小于 1 毫秒。
- 无新增第三方录音素材，不套用其他素材的 CC0 许可；无需新增第三方素材署名。
- `python Tools/GenerateEnergyCrystalAudio.py --check` 可校验确定性与音频格式；成品哈希记录于 JSON 清单。

### S13 — 项目内程序合成：恐惧咆哮

- 生成日期：2026-10-09；文件 `Assets/Resources/Audio/FearRoar.wav`。
- 由 `Tools/GenerateFearRoarAudio.py` 使用 Python 标准库与已有合成辅助函数生成；无第三方录音或音频采样。
- 采用低沉的声带谐波、缓慢变化的共振峰、气流噪声与轻微量化颗粒，保持现有 CRT 风格；音源 RMS 为 0.18，播放音量为 0.65。
- 单声道 44.1 kHz PCM16，预加载、不压缩；完整长度 1 秒，起音约 0.07 毫秒。与一至三级技能的咆哮范围持续时间一致，不与三级的 5 秒眩晕混淆。
- 声源归咆哮范围对象所有；暂停/读取时暂停，随时间倍率调整，范围结束、停用或场景卸载时停止。无范围外延长的混响尾音。
- 无需新增第三方素材署名；不套用其他音频的 CC0 许可。`python Tools/GenerateFearRoarAudio.py --check` 可检查长度、起音、峰值、端点及确定性；哈希记录于 JSON 清单。

### S14 — 项目内程序合成：药水瓶碎裂与飞溅

- 生成日期：2026-10-10；文件 `Assets/Resources/Audio/PotionShatter.wav`。
- `Tools/GeneratePotionShatterAudio.py` 使用 Python 标准库和项目现有合成函数生成，不使用第三方录音或音频采样。
- 短促玻璃脆裂、液体泼洒噪声、零散液滴与轻微量化颗粒；区别于炸弹低频爆炸，保持项目 CRT 音效风格。
- 单声道 44.1 kHz PCM16，预加载、不压缩；长度 0.58 秒，起音约 0.068 毫秒，RMS 0.1312，峰值 0.77，播放音量 0.65；随距离衰减，暂停时停止推进。
- 无需新增第三方素材署名，不套用其他音频的 CC0 许可。运行 `python Tools/GeneratePotionShatterAudio.py --check` 验证确定性与格式，成品哈希见 JSON 清单。

### S15 — 项目内程序合成：炮台重型发射

- 生成日期：2026-10-10；文件 `Assets/Resources/Audio/TurretFire.wav`。
- `Tools/GenerateTurretAudio.py` 使用 Python 标准库与项目现有合成辅助函数生成；没有第三方录音或采样。
- 低频下滑冲击、短促爆鸣、机械非谐波余响与轻微量化颗粒，突出比手铳更沉重的听感，并保持连发清晰。
- 单声道 44.1 kHz PCM16，预加载、不压缩；时长 0.36 秒，起音约 0.045 毫秒，RMS 0.16、峰值 0.7188；默认播放音量 0.65，随距离衰减。
- 无需新增第三方素材署名，不套用其他素材的 CC0 许可。运行 `python Tools/GenerateTurretAudio.py --check` 可校验确定性与格式；成品哈希记录在 JSON 清单。

### S16 — 项目内程序合成：圣光打击

- 生成日期：2026-10-10；脚本 `Tools/GenerateHolyLightAudio.py` 使用 Python 标准库与项目现有噪声/电平辅助函数生成，未使用第三方录音或采样。
- `Assets/Resources/Audio/HolyLightCentral.wav`：0.16 秒下降气流、低频魔法冲击与暖色谐波余响，总长 2.16 秒，RMS 0.0909、峰值 0.76，默认播放音量 0.55。
- `Assets/Resources/Audio/HolyLightWave.wav`：较轻的小光柱音效，同样在 0.16 秒处落地，随后 0.65 秒衰减，总长 0.81 秒，RMS 0.1131、峰值 0.68，默认播放音量 0.45。
- 每轮四根小光柱共用一个音源，按四个落点计算距离与声像，不将相同片段叠加四次；升级新增的第四轮也会发声。
- 单声道 44.1 kHz PCM16，预加载、不压缩；没有新增第三方素材署名要求，不套用其他音频的 CC0 许可。
- 声音跟随技能时间轴，支持暂停、慢速、距离衰减；中断/停用时停止，技能结束随对象清理。没有新增 AI 吸引或改变技能伤害。
- `python Tools/GenerateHolyLightAudio.py --check` 校验确定性、端点、起音、撞击位置和电平，资源哈希见 JSON 清单。

### S17 — 项目内程序合成：域外来客两段能量波

- 生成日期：2026-10-12；`Tools/GeneratePurpleWaveAudio.py` 使用 Python 标准库和现有合成辅助函数生成，没有使用第三方录音或音频采样。
- `Assets/Resources/Audio/PurpleWaveRelease.wav`：手部持续释放的低沉、空洞能量涌流与颗粒气流，0.51 秒，RMS 0.15、峰值 0.4573，默认播放音量 0.5。不是独立能量弹的短促发射声。
- `Assets/Resources/Audio/PurpleWaveBurst.wav`：最远端爆开时的魔力碎裂、低频冲击与短余响，0.48 秒，RMS 0.1371、峰值 0.76，默认播放音量 0.6。
- 两段分别由释放和实际末端爆开事件触发；第一段随攻击持续时间调整速率，提前中断不会产生第二段。末端余音不会因视觉对象先销毁而被截断。
- 单声道 44.1 kHz PCM16，预加载、不压缩；以角色所在平面计算距离衰减（2 单位内全音量、18 单位外静音）及声像，支持暂停/读取与时间倍率。场景卸载时清理，不跨场景残留，也不新增 AI 吸引或伤害。
- 无需新增第三方素材署名，不套用其他素材的 CC0 许可。`python Tools/GeneratePurpleWaveAudio.py --check` 校验确定性、格式、起音、端点与电平；哈希记录于 JSON 清单。

### S18 — 项目内程序合成：流星群

- 生成日期：2026-10-12；脚本 `Tools/GenerateMeteorShowerAudio.py` 使用 Python 标准库及项目已有合成函数生成，没有使用第三方录音或采样。
- `Assets/Resources/Audio/MeteorDescent.wav`：逐渐增强的破空气流与岩石震颤，0.8 秒，与陨石下坠时间一致；RMS 0.13，峰值 0.6303，下坠音量总预算 0.32。
- `Assets/Resources/Audio/MeteorImpact.wav`：落地时的低沉爆鸣、碎石颗粒与少量魔力余响，0.82 秒；RMS 0.1651，峰值 0.78，爆炸音量总预算 0.6。
- `Assets/Resources/Audio/MeteorFireLoop.wav`：低声轰鸣与细碎噼啪的紫焰燃烧循环，1.6 秒无缝片段；RMS 0.13，峰值 0.488，接缝差 0.00003。仅二阶升级的五秒燃烧区域启用，音量总预算 0.27，开始与结束淡入淡出。
- 所有燃烧区域汇入同一循环声，以区域距离和位置计算音量与声像，不将十五份相同片段叠加。下坠和爆炸各使用四个可复用声道；多个同时施放的流星群共用音量预算。常规距离衰减为 2 单位内全量、22 单位外静音；燃烧区域按距其边缘的距离衰减。
- 三个资源均为单声道 44.1 kHz PCM16，预加载、不压缩。声音跟随技能时间轴，暂停与慢速同步；死亡、中断、停用或场景卸载清理，不增加 AI 吸引或修改技能伤害。
- 无新增第三方素材署名要求，不套用其他素材的 CC0 许可。`python Tools/GenerateMeteorShowerAudio.py --check` 校验格式、确定性、电平及接缝；哈希记录于 JSON 清单。加 `--preview` 可在 `.codex-temp/MeteorShower-audio-preview.wav` 生成十五颗陨石的混音试听。

### S19 — 项目内程序合成：竞技场回合结束铃声

- 生成日期：2026-10-13；脚本 `Tools/GenerateArenaBellAudio.py` 使用 Python 标准库和项目现有合成辅助函数，不使用录音或第三方采样。
- `Assets/Resources/Audio/ArenaRoundEndBell.wav`：两次短促的金属敲铃，间隔 0.23 秒，总长 0.72 秒；非谐波金属共振与短衰减，加入轻微像素音色纹理。单声道 44.1 kHz PCM16，RMS 0.1495、峰值 0.7。
- 竞技场组件默认音量 0.55，可在 Inspector 修改音频与音量；作为回合提示使用 2D 播放，场内各处清晰可闻。双方胜利共用同一音效，每轮只触发一次，不增加 AI 吸引。
- 双击写在同一片段内，音源复用；暂停后续播、停用与场景卸载时停止，恢复结算存档不重新播放。`python Tools/GenerateArenaBellAudio.py --check` 校验确定性、两次敲击、电平、格式及端点，哈希记录于 JSON 清单。
- 无新增第三方素材署名要求，不套用其他音频的 CC0 许可。

### S11 — 项目内程序合成：木门与玻璃破坏

- 生成日期：2026-10-04；脚本 `Tools/GenerateDestructionAudio.py`，仅使用 Python 标准库和项目中的合成辅助函数。
- 使用固定种子噪声、非谐波振荡、衰减包络和轻量量化生成，不使用录音采样；受击、死亡及炸弹片段仅作音量/起音测量参考。
- 无新增第三方录音素材，不需因本次合成新增第三方素材署名。不将 S6 / S7 的 CC0 许可套用至新生成文件。
- 原资源 GUID 和引用保留；S6 / S7 旧成品存放于 `Docs/AudioOriginals/Destruction/RecordedV2/`，历史来源与哈希不删除。
- `python Tools/GenerateDestructionAudio.py --check` 可验证格式、确定性、峰值、起音、直流偏移与尾音。详细参数见 [门与玻璃破坏音效](DestructionAudio.md)。
