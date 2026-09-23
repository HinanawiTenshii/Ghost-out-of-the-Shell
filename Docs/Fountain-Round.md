# 圆形喷泉

预制体：`Assets/Prefabs/Decorations/Fountain-Round.prefab`。

基于当前 `喷泉.prefab` 制作独立版本，未改动原喷泉或场景摆放。

- 保留当前中性灰石边、深灰内沿、明亮蓝色水面和中央喷水效果。
- 外池、内沿、水面、中央台座与喷口采用 Unity 自带 Circle；四处水光保留简洁矩形。
- 共 11 个静态 SpriteRenderer（7 圆形 + 4 矩形），沿用原喷水组件与粒子上限，无新增造型脚本或自制贴图。
- 根节点位于圆心，默认直径 8 世界单位；使用半径为 0.5 的 CircleCollider2D，随根节点的 8 倍缩放贴合整个池体。
- 配色与缩放可通过子对象调整。建议保持 XY 等比缩放，以便圆形碰撞与外观一致。

`Tools/TestRoundFountain.ps1` 检查资产引用、几何数量、同心结构、配色、碰撞和喷水范围，并生成静态预览 `Docs/Fountain-Round-preview.png`。预览不模拟喷水；尚未进行 Unity Play Mode 验证。
