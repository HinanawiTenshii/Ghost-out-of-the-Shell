# 短音效起音对齐

2026-09-19 检测全部 12 个短音效，未修改背景音乐。

在 -40dB 阈值下，9 个文件开头存在可测空白，裁剪记录如下（秒）：

| 音效 | 裁掉头部 |
|---|---:|
| cncl01 | 0.0681383 |
| cncl02 | 0.0499524 |
| cncl04 | 0.0415170 |
| cncl05 | 0.0227188 |
| cncl06 | 0.0520612 |
| cncl07 | 0.0613356 |
| Hit | 0.0809728 |
| MGS 驚嘆號音效 | 0.1214720 |
| NpcAwarenessAlert | 0.0128549 |

裁剪后保留约 1ms 起音余量，另做 0.5ms 防爆音淡入；没有移除中间停顿或尾音，没有改变音高、声道数或采样率。CharacterDamage、CharacterDeath、glassbrokechanged 没有明显头部空白，未裁剪波形。

`Assets/SoundEffect` 原 MP3 改为同名 PCM16 WAV，原 `.meta` 随文件迁移，GUID 全部保留。全部短音效使用 PCM、Decompress On Load、preloadAudioData，关闭后台加载；无代码硬编码旧文件路径。场景/预制体的 AudioClip GUID 引用无需重绑。未改视觉动画或播放触发逻辑。

原始音频及导入设置备份：`.codex-temp/Audio/OnsetTrim-20260919-124956-590`。恢复时应同时恢复原音频和对应 meta，不能让旧、新音频同时使用同一个 GUID。该目录不在 Assets 内，不会随游戏构建分发。

`Tools/TrimSoundEffectOnsets.ps1` 是本次一次性迁移记录，不要对已裁剪的结果再次运行。`Tools/TestSoundEffectOnsets.ps1` 检查起音空白、导入设置及 GUID。

代码搜索未发现 PlayDelayed/PlayScheduled 人为延迟。文件头部空白已处理，但尚未在 Unity 和实际声卡上测量最终视听同步；DSP 缓冲、操作系统和蓝牙输出仍可能产生无法靠裁剪消除的延迟。本次未更改项目全局音频缓冲设置。
