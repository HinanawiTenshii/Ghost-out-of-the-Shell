# Pixel Adventures Vol. 1 — 已选音乐

取得及许可核对日期：2026-10-30。

作者：Domi.wav（Dominic Sandefur）。

官方来源：https://domiwav.itch.io/8-bit-rpg-adventure-and-fantasy-music-pack-pixel-adventures-vol-1

## 用途与状态

| 原曲 | 项目路径 | 最终选定用途 | 状态 |
|---|---|---|---|
| Adventure's Spirit — Title Theme | `Assets/BGMS/Domi/AdventuresSpirit.wav` | 致敬初代塞尔达的假标题界面 | 已接入 FakeTitleScreen，2D 循环，音量 0.18 |
| A Great Journey — Overworld | `Assets/BGMS/Domi/AGreatJourney.wav` | 勇者新手关；替代此前 Windy Bluffs 选曲 | 仅暂存，尚未接入 |

假标题音乐使用现有 `LoopingBackgroundMusicPlayer`，在第一档标题调色板出现时播放，等待 Enter 及打开选项不重播，离场停止；仅修改假标题场景。新手关曲目仍为暂存，尚未改变新手关现有音轨。

## 授权与来源证据

作者的发布页明确允许将音乐免费用于商业与非商业项目；署名受到欢迎但不是必需。页面没有声明使用某个 Creative Commons 许可证，不将其自行改标为 CC0/CC BY。

`PixelAdventuresVol1.html` 是下载时保存的官方完整页面，保留作者的许可、原曲目录与格式说明。作者说明专辑包含可无缝循环的曲目；本次没有改动音频内容。

推荐日后在游戏鸣谢中使用：

```
Adventure's Spirit / A Great Journey
Music by Domi.wav (Dominic Sandefur)
Pixel Adventures Vol. 1
https://domiwav.itch.io/8-bit-rpg-adventure-and-fantasy-music-pack-pixel-adventures-vol-1
Used with the composer's permission for commercial and non-commercial projects.
```

## 下载与校验

- 来源：itch.io 官方免费直接下载流程，没有付款或使用用户账户；game ID 3592783，upload ID 13814198。
- 原始包：`Pixel Adventures Vol. 1.zip`。
- ZIP SHA-256：`7FC11103730D7A18F410B9BC73CE781E68D0FECE20FDB9E77EB3C7CD3CF920F0`。
- 原始包临时缓存：`.codex-temp/domi-pixel-adventures/Pixel Adventures Vol. 1.zip`，不在 Unity Assets 中，不作为唯一授权证据。
- 仅提取两个精确条目；未导入其他音乐、Mac 元数据或隐藏文件。
- `Vol. 1 Zip/Adventure's Spirit - Title Theme.wav`：27,648,044 字节，96 秒，SHA-256 `116F35157DBBB3F594EF72D79C6691E74368D9360000B0D48400263558B4A01D`。
- `Vol. 1 Zip/A Great Journey - Overworld.wav`：28,947,740 字节，100.512833 秒，SHA-256 `6A5D687A59BF1F18FC0566C9E5448B05D427FDE2674770C16054132932969924`。
- 两首均为原始 stereo PCM24 / 48 kHz WAV，仅重命名，完整解码无错误。
- Unity `.meta`：Streaming、Vorbis quality 0.8、保留采样率、不预加载、后台载入；假标题运行时播放音量为 0.18，新手关曲目尚未接入。

对应资产清单：`Docs/AudioAssetInventory.json`；整体来源登记：`Docs/AudioSources.md`。
