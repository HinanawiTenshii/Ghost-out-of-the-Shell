# Level2 巫师

- 预制体：`Assets/Prefabs/Level2NPCS/Wizard-level2.prefab`
- 角色数据：`Assets/Scripts/Characters/WizardZeldaCharacterData.cs`

延续现有 16 PPU 像素规格，画布向上扩为 16×20：尖帽由 4 行加高至 8 行，帽檐由 8 像素加宽至 12 像素。保持原身体大小、脚底基准、手部位置与碰撞箱不变；蓝色编辑器边框同步扩高。紫靛色长袍延伸为连续宽下摆，遮住腿和靴子；行走改为褶皱交替及下摆边角轻微起伏，不再露出独立腿部。头部直接衔接衣领，不额外画裸露脖颈。配色可在 Inspector 的 Wizard Appearance 中调整。

四向各有待机、两帧行走、攻击姿态，共 16 个按需缓存的精灵。攻击姿态先恢复原手臂遮挡处，避免双手叠影；保留编辑器蓝色轮廓。

沿用神职人员的角色组件结构和碰撞：Kinematic Rigidbody2D、0.38×0.56 BoxCollider2D、SpriteRenderer、独立角色数据、附身移动组件和 NPC 巡逻 AI。默认相机 Size 7、玩家视野 13；生命 5、移动速度 3、权限 1。

基础攻击为现有 Shockwave 形状的青蓝色短程法术闪光：攻击力 1、时长 0.3 秒、范围 0.65×0.65；接入通用敌对攻击和命中反馈。未新增远程投射物、专属技能或自动放置场景。

验证：`Tools/TestWizard.ps1` 检查四向地图、全部姿态连通性、侧面镜像、手部位置、攻击手替换、帽檐宽度、所有姿态下摆遮盖、角色和攻击引用；加 `-AttackPreview` 或 `-WalkPreview` 输出攻击或行走姿态预览。`Tools/Wizard.Validation.targets` 可在 Unity 尚未刷新 csproj 时包含新脚本参与编译。预览不包含游戏内 CRT 后处理。
