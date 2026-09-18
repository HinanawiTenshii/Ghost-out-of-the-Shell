# 双开旋转门

预制体：`Assets/Prefabs/Decorations/DoubleDoorHinge.prefab`。直接拖入场景，未自动放置或替换原有门。

以 DoorHinge 的棕色门扇、铰链外观、碰撞体、运动学刚体与 DoorData 为基础复制两个门扇。两扇门闭合时在中央相接，左右铰链位于外侧。每扇门保持原门的长度和厚度，总宽约 4.34 世界单位，根节点原点位于门洞中央。

靠近任意门扇按一次 E，两扇门同时向相反方向旋转 90 度；再次按 E 同时关闭。根节点唯一的 DoorHingeInteraction 管理交互和提示，不会发生一侧先触发、另一侧重复切换。没有 HUD 的测试场景也可以交互。

根节点可统一配置 Rotation Angle、Rotation Speed、Clockwise（翻转两侧开门方向）、Is Locked、钥匙/密码等原有选项。拉杆应引用根节点 DoubleDoorHinge。AI 通行、锁定、存档恢复共用同一开关状态。左右 DoorData 独立承受伤害与破坏。

Primary Hinge 与 Secondary Hinge 分别引用 LeftHinge、RightHinge，两者必须为独立的子节点，不要互相嵌套。原有单门不设置这两个引用，仍旋转自身，行为不变。每帧同时更新两侧转动并刷新视野遮挡；碰撞及附近检测覆盖两侧门扇。
