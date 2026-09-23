# Level2 厨师

预制体：`Assets/Prefabs/Level2NPCS/Chef-level2.prefab`。

- 白色高顶厨师帽、灰白短外套、浅色围裙、红领巾，暖棕肤色与深褐色短靴。
- 保留普通平民体型、碰撞尺寸与脚底基准；仅为厨师帽增加画布高度。
- 四个朝向，每个朝向包含站立、两帧行走与攻击姿态。侧面手部居中；攻击时替换原攻击手，保留非攻击手。
- 使用现有平民巡逻 AI、附身控制与普通近身攻击，不新增烹饪或交易功能。
- Inspector 的 Chef Appearance 可以修改衣服、帽子、围裙、领巾、肤色、发色；带蓝色编辑器边框。
- 像素精灵按需缓存，调色时重建，销毁角色时释放；不增加逐像素 GameObject。

验证：`Tools/TestChefVisual.ps1` 检查 16 个实际生成姿态与 prefab 引用并生成预览；`Tools/Chef.Validation.targets` 用于 Unity 外 C# 编译验证。尚需在 Unity Play Mode 中确认实际场景的 CRT 后处理效果。
