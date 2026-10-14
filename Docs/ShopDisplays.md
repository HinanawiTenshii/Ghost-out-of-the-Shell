# 店铺装饰预制体

位置：`Assets/Prefabs/Decorations/ShopDisplays/`。武器铺、工具铺、盔甲铺各两件，全部为严格俯视的平放陈列；不绘制竖直武器架、柜门、桌腿或盔甲人台正面。

| 预制体 | 展示内容 | 默认尺寸 |
| --- | --- | --- |
| WeaponShop-SwordDisplay | 三把不同长度的剑，暗红衬布 | 2.2 × 1.5 |
| WeaponShop-PolearmDisplay | 平放的长矛与双刃斧，暗红衬布 | 2.6 × 1.3 |
| ToolShop-Workbench | 锤子、手锯与钳子，青绿台面 | 2.2 × 1.5 |
| ToolShop-SupplyTray | 凿具、绳圈和铁钉的木制分格托盘 | 1.8 × 1.3 |
| ArmorShop-ShieldDisplay | 圆盾与尖底盾，紫色衬布 | 2.2 × 1.5 |
| ArmorShop-HelmetDisplay | 两只头盔的顶部轮廓与平放护手，紫色衬布 | 2.0 × 1.4 |

直接拖入地图使用，根对象位置为整体中心。支持旋转 Z 轴、缩放，也可通过 Geometric Shop Decoration 的 Size 更改尺寸。
Wood Color、Metal Color 和 Accent Color 分别调整木材、金属与台面衬色，Solid 切换碰撞。
五个陈列台使用一个 BoxCollider2D；零件托盘默认禁用碰撞，方便放在桌面上。叠放时提高 Sorting Order，例如从 -3 改为 -2。

沿用既有家具的 Default 层、Sprites/Default 材质与 -3 排序；每件一个合并顶点色网格、60–132 个顶点，只有启用或配置变化时生成。无每帧动画、交易、拾取、伤害等游戏功能。未修改既有预制体或场景。

验证：`Tools/TestShopDisplays.ps1` 提取生产几何，验证边界、凸多边形三角方向、顶点预算、组件引用和预制体配置，并生成 `Docs/ShopDisplays-preview.png`（几何预览，不是 Unity 截图）。编译入口为 `Tools/ShopDisplays.Validation.targets`。
