# 公告板

预制体：`Assets/Prefabs/Decorations/NoticeBoard.prefab`。

参考当前 Shelf 的深褐色外框、较亮褐色木面、深色内衬和金黄色小配件。三张告示纸使用淡米色、淡金色、青灰色；只以几条宽横线表示文字，不加入细碎字体或复杂木纹。

- 15 个引擎 Square SpriteRenderer、共享 Sprites/Default 材质，无新贴图、着色器或运行时脚本。
- 正面矩形板，默认 2.6×1.9 世界单位，根节点位于中心、缩放为 1，便于拖入与调整尺寸。
- 一个根节点 BoxCollider2D，覆盖外框；Default 层，不额外阻挡玩家视野。
- 子物件按 Frame、Board、Notice、Pin、Text 命名。配色可直接在各 SpriteRenderer 修改；每张告示纸是独立矩形。
- 目前是装饰物，不包含按键阅读、任务列表或公告 UI；未放入任何场景，原 Shelf 保持不变。

验证：`Tools/TestNoticeBoardPrefab.ps1` 校验 GUID、对象引用、几何数量、配色与碰撞范围，并由真实预制体矩形生成 `Docs/NoticeBoard-preview.png`。预览为平面数据渲染，不代表游戏内 CRT 最终效果。
