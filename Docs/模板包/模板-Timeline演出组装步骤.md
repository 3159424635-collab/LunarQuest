# 模板 · Timeline 演出组装步骤（模块四用）

> 以发射序列为例。操作以拖拽与时间轴编排为主，不需要写代码。本步骤先按文字执行，
> 工程建立后补充截图版。

## 前置

- Package Manager 安装：Timeline、Cinemachine（均为官方免费包）

## 组装步骤（发射序列示例）

1. 场景中新建空物体命名 `Cutscene_Launch`，Add Component → Playable Director
2. Playable Director → New Timeline，创建 `CT_Launch`（存入 `Assets/_Project/Cinematics/`）
3. 时间轴上添加轨道：
   - **Cinemachine Track**：绑定场景中的虚拟相机（发射远景、塔架特写、点火近景各一台），在时间轴上分段摆放
   - **Activation Track**：控制烟雾粒子、强光源等物体的启停
   - **Audio Track**：倒计时人声、轰鸣声，按秒对位
4. 倒计时与点火的节奏对位：T-10 秒开始倒计时，T-0 点火、震屏、Bloom 增强
5. 序列结束处放一个 **Signal**（信号），由模块一的脚本接收后切换到下一段场景
6. 保存后用 Playable Director 的 Play 按钮在编辑器内预览，逐段微调

## 三段演出清单

| 演出 | 时间轴资产 | 关键镜头 | 预计时长 |
|---|---|---|---|
| 发射序列 | CT_Launch | 倒计时特写→点火→塔架跟随→云层远景 | 约 60 秒 |
| 绕月"地出" | CT_Orbit | 舷窗内地球升起、日地月同框 | 约 30 秒 |
| 返回段 | CT_Return | 上升器发射→再入炫光→开伞→草原着陆 | 约 45 秒 |

## 验收标准

- 每段演出可在编辑器内独立预览，不依赖玩家操作
- 转场黑场 0.5 秒以内，音画对位无明显偏差
- 结束 Signal 正常发出（与模块一联调确认）
