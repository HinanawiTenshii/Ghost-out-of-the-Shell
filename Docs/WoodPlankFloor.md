# 极简木板地面

材质：`Assets/Materials/WoodPlankFloor.mat`  
着色器：`Cogitans/Wood Plank Floor`

沿用瓷砖地面的世界坐标程序化绘制：单个地面精灵即可铺满，不需要为每块木板建立对象。三种相近的深蓝色区分错缝长方形木板，明暗两档直接采用现有 Level2CorridorTiles 瓷砖配色，中间色取两者平均值，使地板色调及明度范围统一。不画砖缝、木纹和钉子。远距离缩小时渐变至平均色，减少细密条纹与 CRT 扫描线干扰。

## 使用

1. 将材质放入地面对象 SpriteRenderer 的 Material。可使用引擎自带 Square 精灵，拉伸至房间大小。
2. SpriteRenderer 的 Color 建议设为白色；材质会与该颜色相乘，原先蓝色的地面颜色会影响木板颜色。
3. 保持原地面的 Sorting Layer / Order，确保家具和角色位于地面上方。本材质不更改碰撞、视野或交互。
4. Board Direction 可选 Horizontal / Vertical；这是世界坐标方向，不随对象旋转改变。Plank Length / Width 控制世界单位尺寸，默认 1.84 × 0.46，与原 0.92 大小的瓷砖协调。
5. Wood / Dark Board / Light Board 调整三档颜色；Pattern Origin 调整排布起点。相邻地面使用相同材质和起点时自动接续。

## 每个地面独立设置方向

选中挂有 SpriteRenderer 的地面对象，添加 `Rendering → Wood Plank Floor Direction` 组件，在组件的 Direction 下拉框选择 **横向 / 纵向**。每个对象可以共用同一份 WoodPlankFloor 材质，修改方向不影响其他地面，不需要复制材质。编辑模式、预制体模式及运行时均支持，组件设置会随场景/预制体保存，也支持编辑器撤销。

禁用或移除组件后回到材质的 Board Direction 默认值。未添加组件的现有地面保持原行为。运行时可通过组件的 `Direction` 属性修改。实现使用 MaterialPropertyBlock，只在设置变化时更新，并保留其他逐对象参数，不创建材质实例或额外精灵。

不同房间需要不同颜色时仍可复制材质再修改。没有自动替换任何现有场景地面或给所有地面添加组件。

验证：`Tools/TestWoodPlankFloor.ps1` 检查材质引用、默认值、负坐标、错缝、横竖映射，并通过 Direct3D 编译顶点/片元程序。生成的 `Docs/WoodPlankFloor-preview.png` 是颜色排布示意，不包含 Unity 灯光、CRT 后处理。
