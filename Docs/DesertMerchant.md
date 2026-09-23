# Level2 沙漠商人

预制体：`Assets/Prefabs/Level2NPCS/Merchant-level2.prefab`

使用与当前平民相同的 16×16 四向像素规格、短腿、无脖子连接及侧面居中的手部。浅色包裹头巾、金色衣襟、褐色腰包用于区分商人和普通居民。默认服装为青绿色。

## 编辑器配色

在角色的 `Desert Merchant Zelda Character Data` 组件中：

- `Configurable Clothing > Clothing Color`：服装主色，直接继承平民的换色实现；明暗层次自动随主色变化。
- `Skin Color`、`Hair Color`：皮肤和鬓角/胡须。
- `Merchant Accessories > Headcloth Color`：头巾与内领。
- `Trim Color`：头巾装饰与衣服金边。
- `Leather Color`：腰带和腰包。

服装换色不改变配饰、皮肤或轮廓；修改后沿用平民的缓存失效机制，同步重建四向站立、攻击和行走图像。无需修改 SpriteRenderer 的整体 Tint。

## 范围

预制体以 Level2 平民结构为基础，保留其碰撞、NPC AI、移动、附身、攻击、摄像机 Size 7 和视野 13 等配置。没有修改原平民预制体，也没有自动放入场景。项目目前没有商人交易系统，本资产不包含商店 UI 或买卖逻辑。

## 验证

使用 PowerShell 7 运行 `Tools/TestDesertMerchant.ps1`：提取正式代码的像素绘制方法，验证 3 种服装色 × 4 方向 × 站立/攻击/两帧行走，共 48 组姿态，以及配饰独立换色、连通轮廓、短腿、手部去重、无脖子和预制体基础数据不变。

生成 `DesertMerchant-Idle-preview.png`、`DesertMerchant-Attack-preview.png`、`DesertMerchant-Walk0-preview.png` 和 `DesertMerchant-Walk1-preview.png`。这些是代码渲染预览，不包含游戏 CRT 滤镜，不等同于 Unity Play Mode 实测。

`Tools/Merchant.Validation.targets` 可用于本地 .NET 编译检查，将新脚本加入尚未被 Unity 重新生成的项目文件；不修改 Unity 生成的 csproj。
