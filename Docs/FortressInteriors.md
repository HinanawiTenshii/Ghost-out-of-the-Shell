# 要塞室内装饰（第二组）

目录：`Assets/Prefabs/Decorations/Fortress/Interior/`。直接拖入地图使用，未自动修改或布置现有场景。

| 预制体 | 俯视内容 | 默认尺寸 | 默认碰撞 |
| --- | --- | --- | --- |
| Fortress-WritingDesk | 书记桌：展开的文书、蜡封、平放羽毛笔与墨水瓶口 | 2.2 × 1.5 | 有 |
| Fortress-ArchiveCabinet | 卷宗柜：开口格柜、平放书册及捆扎卷轴 | 1.8 × 1.3 | 有 |
| Fortress-MessTable | 餐食长桌：布制桌旗、四套餐具、中央面包托盘 | 2.8 × 1.6 | 有 |
| Fortress-WashStand | 盥洗台：灰石台面、水盆、陶壶口、毛巾与皂块 | 1.65 × 1.25 | 有 |
| Fortress-HeraldicRug | 纹章地毯：暗红织物、镶边、流苏及织入的城塔徽章 | 2.2 × 2.8 | 无 |

延续现有装饰的严格俯视视角与简单几何风格：不绘制侧立桌腿、柜子立面或正面水壶。地毯上的城塔只是平面织纹。
木材使用褐色、石材及餐具使用灰色，布料使用较饱和的红、绿、青色，少量羊皮纸和黄铜色用于细节点缀。

根对象的 `Geometric Fortress Interior Decoration` 可编辑：

- `Shape`：五种室内造型及新增的 Trapdoor 活板门；切换时可手动调整 Size。
- `Size`：实际几何与包围盒尺寸，根对象保持居中。
- `Wood Color / Metal Color / Accent Color`：木材、石材或金属、布料及书封配色；不会把整个对象一并染色。
- `Solid`：是否启用简单 BoxCollider2D；地毯默认关闭。
- `Sorting Order`：家具默认 -3；地毯 -4，低于家具并避免覆盖人物。

支持 Transform 缩放和 Z 轴旋转。使用 Default 层及 Sprites/Default 材质，沿用现有装饰的渲染方式。
每件装饰只生成一个顶点色网格与 MeshRenderer，分别为 104、112、260、124、120 个顶点；无逐细节精灵、粒子或持续网格重建。
仅为装饰，不添加阅读、储物、进食、治疗或交互功能。重建、停用、销毁遵守现有几何装饰的生命周期。

验证：`pwsh -NoProfile -File Tools/TestFortressInteriors.ps1`，检查实际生产几何、三角形、居中边界、顶点数量及五个预制体的脚本、碰撞和排序配置。
测试从生产几何生成 `Docs/FortressInteriors-preview.png`，这是无 CRT 的几何预览，不是游戏内截图。
编译验证使用 `Tools/FortressInteriors.Validation.targets`；关卡中的最终 CRT 观感需要在 Unity 内查看。

## 地面活板门

预制体：`Assets/Prefabs/Decorations/Trapdoor.prefab`。严格俯视的闭合木板门，配灰色嵌入式边框、两枚铰链、横向加固条及平放拉环，与已有箱子及要塞木制装饰共用配色和几何构建方式。

- 默认尺寸 1.8 × 1.8，根对象位于整体中心，支持缩放及旋转。
- `Wood Color`、`Metal Color` 分别调整木板与金属配色；`Accent Color` 对该造型无作用。
- 仅提供装饰外观，没有交互、开关、传送或暗道功能；默认关闭碰撞，不阻挡行走或视野。
- 使用 Default 层，排序 -5，位于普通地板之上、家具与角色之下；一个合并网格及渲染器，共 144 顶点，无持续网格重建。
- 未自动放入任何关卡，可直接从 Project 窗口拖入场景。

验证：`pwsh -NoProfile -File Tools/TestTrapdoor.ps1`，包括原有五种造型的回归检查；`Tools/TrapdoorSmoke.cs` 在隔离 Unity 项目验证真实预制体导入、渲染、旋转缩放和碰撞配置，共 19 项检查。`Docs/Trapdoor-preview.png` 为 Unity 相机实际渲染的无 CRT 预览。

## 俯视悬挂吊灯

预制体：`Assets/Prefabs/Decorations/HangingChandelier.prefab`。六灯环形灰铁吊灯，三根细悬挂支架汇于中央吊点；仅表现严格俯视的灯杯顶部，不绘制侧立灯杆。褐色灯杯和暖黄色烛光与现有中世纪装饰配色一致。没有交互或碰撞，可自由从下方通行；未自动放入现有地图。

根对象的 `Geometric Hanging Chandelier` 可调整灯体直径（默认 2）、金属/灯杯/蜡烛颜色、光晕颜色、强度、范围、呼吸周期、呼吸幅度与摇曳幅度。根轴心居中，可直接缩放和旋转。默认 3.2 秒缓慢明暗呼吸，叠加小幅平滑摇曳；每个实例起始相位不同。编辑器为静态带光晕预览，运行游戏才播放动态效果，暂停时冻结。

复用 `WarmLightGlow` 着色器，不依赖全局 Bloom，不添加真实灯光、不修改 CRT 参数。主体 492 顶点、外围光晕 28 顶点、灯芯泛光 48 顶点，共三个渲染器。动态只更新材质属性，不逐帧创建网格/材质/对象。默认排序 4–6，表现悬于人物上方，仍受视野遮罩覆盖；Default 层且无碰撞，不阻挡玩家视线。停用时隐藏，重建/销毁时释放资源。

编译：`Tools/Chandelier.Validation.targets`。隔离 Unity GPU 验证：`Tools/ChandelierSmoke.cs`，共 817 项检查，涵盖真实预制体、几何、动态亮度、光晕扩散、视野遮罩、编辑器重建与资源释放。真实 Unity 无 CRT 预览为 `Docs/HangingChandelier-preview.png`；动态预览为 `Docs/HangingChandelier-preview.gif`。

## 壁烛几何重制

沿用 `Assets/Prefabs/Decorations/PixelWallCandle.prefab`、`PixelGoldWallCandle.prefab` 及原有 `PixelWallCandleVisual` 脚本 GUID，无需替换场景实例。普通版为灰铁支架，金色版为黄铜支架，两者使用浅蜡色烛身。将旧位图改为合并的简单几何面，保留原始 24/16 坐标系、轴心、四向安装规则、`MountingDirection` / `SetMountingDirection` 接口、排序与无碰撞行为；普通版缩放仍为 (0.75, 0.75, 1)，金色版仍为 (0.5, 0.75, 1)。四向支架与烛身的外形边界均与旧版不透明像素边界一致。

火焰只参考提供的灵魂火焰图片的形状：不规则阶梯边缘、分叉火舌、亮芯与短暂上浮碎焰；颜色仍为原有橙红、橙黄和浅黄色亮芯，不改成蓝绿色。旧 `Outer Glow` / `Inner Glow` 大范围光圈及半径倍率已移除，现仅沿当前火焰轮廓产生小范围暖色泛光，消散的火焰不会残留光圈。不修改全局 CRT、摄像机或任何关卡文件。

火焰采用一个固定的 14 × 18 几何网格，通过颜色/透明度变化形成连续伸缩、摇曳及碎焰生灭，不创建逐像素对象；每行对应一片局部柔光，并随可见火焰范围更新。每件壁烛共三个有效渲染器；主体 56–64 顶点，火焰 1008 顶点，泛光 72 顶点。运行时最高 30 Hz 更新复用的缓冲区，不持续创建网格或材质，暂停时冻结；编辑器显示静态带泛光预览，停用/销毁时释放生成资源。

保留 `Flame Frame Duration` 作为动画快慢参数，并保留泛光强度及微弱脉动参数；新的「火焰边缘泛光宽度」默认 0.045，设定的是局部边缘宽度，而非原来的照明光圈。排序仍受玩家视野遮罩覆盖。

验证：`Tools/TestDecorativeGlow.ps1` 的 722 项检查保持路灯原有效果并检查壁烛新配置；`Tools/WallCandleSmoke.cs` 在隔离 Unity 内通过四向、两种材质、实际旧版像素边界对照、暖色调、动态轮廓、火焰消散、局部泛光、遮罩和资源清理检查。预览为 `Docs/WallCandles-preview.png` / `Docs/WallCandles-preview.gif`，不包含 CRT 后处理。
