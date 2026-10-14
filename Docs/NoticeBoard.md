# 公告板

预制体：`Assets/Prefabs/Decorations/NoticeBoard.prefab`。

参考当前 Shelf 的深褐色外框、较亮褐色木面、深色内衬和金黄色小配件。三张告示纸使用淡米色、淡金色、青灰色；只以几条宽横线表示文字，不加入细碎字体或复杂木纹。

- 15 个引擎 Square SpriteRenderer、共享 Sprites/Default 材质，无新贴图或着色器。
- 正面矩形板，默认 2.6×1.9 世界单位，根节点位于中心、缩放为 1，便于拖入与调整尺寸。
- 一个根节点 BoxCollider2D，覆盖外框；Default 层，不额外阻挡玩家视野。
- 子物件按 Frame、Board、Notice、Pin、Text 命名。配色可直接在各 SpriteRenderer 修改；每张告示纸是独立矩形。
- 已接入 `DocumentReader`：选中公告牌根节点，在 Inspector 的 **Document Text** 中编辑正文（支持换行）；场景实例可以单独覆盖文字，现有场景内的预制体实例会继承阅读功能。
- 靠近并面向公告牌时显示“按[E]阅读公告牌”，按 E 打开与文档一致的阅读窗口，再按 E 关闭。阅读暂停游戏，关闭恢复此前的时间速度；其他弹窗打开时不能重复打开。
- **Preserve Existing Appearance** 已开启：不生成纸张、不修改公告牌原有碰撞。阅读距离从碰撞边缘计算，缩放或旋转后仍可在外围阅读；按键提示不跟随公告牌缩小。
- 继续保留文档已有的可选任务/日志接口，默认关闭；原 Shelf 与单独的 ReadableNoticeSign 不变。

验证：`Tools/TestNoticeBoardPrefab.ps1` 校验 GUID、对象引用、几何数量、配色与碰撞范围，并由真实预制体矩形生成 `Docs/NoticeBoard-preview.png`。预览为平面数据渲染，不代表游戏内 CRT 最终效果。

`Tools/NoticeBoardReadingSmoke.cs` 用于隔离 Unity 项目中的阅读、交互距离、关闭清理与原文档回归验证。
