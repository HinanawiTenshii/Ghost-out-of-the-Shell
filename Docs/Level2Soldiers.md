# Level 2 普通士兵

三个预制体位于 `Assets/Prefabs/Level2NPCS`，使用独立的 `ArabSoldierZeldaCharacterData`。浅色缠头巾、灰色甲衣、沙色下摆和棕色靴子组成 16×16 四向外观，只有围巾及褶皱颜色随等级改变。

| 预制体 | 围巾 | 数值/行为来源 |
| --- | --- | --- |
| ArabSoldier-level2 | 灰 | SwordGuy-level1 |
| AdvancedArabSoldier-level2 | 黄 | AdvancedGuard-level1 |
| EliteArabSoldier-level2 | 紫红 | LegendaryGuardian |

各档沿用来源的生命、力量、技巧、权限、附身需求、AI、攻击与碰撞配置。紫红档也保留来源的战斗专精角色技能组件；未额外提高数值。摄像机 Size 为 7，玩家视野为 13。使用原普通士兵大小，不沿用圣骑士的 1.1 倍缩放。

自带四向待机、两帧行走、攻击姿势，以及编辑器非运行状态下的蓝色边框。各等级共享一套像素布局，围巾和阴影颜色可在角色数据组件内分别调整；不会改变原 SwordGuy 或圣骑士。

尚未放入场景。保存目录构建器会在进入 Play Mode / 构建时自动识别这些 ZeldaCharacterData 预制体。

静态检查与预览：运行 `Tools/TestArabSoldiers.ps1`，验证四向共 16 个姿势的像素连通性、左右镜像、三档围巾配色和预制体引用，生成 `Docs/ArabSoldiers-preview.png`。预览不含 CRT；最终场景效果仍需在 Unity 中确认。
