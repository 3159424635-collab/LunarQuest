# 月征：绕·落·回（LunarQuest）

> 探月工程沉浸式体验 —— 虚拟现实设计课程设计 · 四人小组项目

以中国探月工程"绕、落、回"三步走为背景的第一人称沉浸式体验：发射场起飞 → 绕月飞行 → 月面着陆 → 月面科考（低重力行走 + 月球车越野 + 任务系统）→ 携带样本返回地球。

完整设计见 `Docs/月征-探月VR项目方案书.md`；课程原始要求见 `Docs/虚拟现实课程设计.pdf`。

## 环境要求（全员统一）

| 项目 | 要求 |
|---|---|
| Unity | **2022.3.62f1**（全员统一此版本）；在 Unity Hub 中为编辑器补装 **Android Build Support** 模块（手机端 AR 构建需要） |
| 模板 | 3D (URP) |
| IDE | Visual Studio 2022 或 VS Code（写 C#） |
| 手机 | 全组至少一台支持 AR 的设备：安卓需 ARCore 认证机型（搜索 "ARCore supported devices" 查询），iPhone 均可 |
| Git | 命令行 Git + GitHub Desktop（不熟悉命令行的成员统一使用 GitHub Desktop） |

**克隆后必做（每人一次）：** Unity 编辑器 → Edit → Project Settings → Editor →
1. Asset Serialization = **Force Text**（必须，否则场景文件无法合并）
2. Version Control = Visible Meta Files（新版本默认已开启，确认即可）

## 目录结构与模块归属

| 目录 | 归属 | 放什么 |
|---|---|---|
| `Assets/_Project/Scripts/` | 模块一 | 全部 C# 脚本 |
| `Assets/_Project/Prefabs/` | 共用 | 预制体 |
| `Assets/_Project/Art/` | 模块二 | 模型 / 材质 / 贴图 |
| `Assets/_Project/Scenes/` | 模块二 | 全部场景 |
| `Assets/_Project/Content/` | 模块三 | 对话数据、任务数据、素材来源表 |
| `Assets/_Project/Cinematics/` | 模块四 | Timeline 演出资源 |
| `Assets/_Project/Audio/` | 模块四 | BGM / 音效 |
| `Assets/_Project/UI/` | 模块四 | HUD 图标与界面素材 |
| `Docs/` | 全员 | 方案书、课程要求、会议记录 |

## 协作规则

1. 只在自己模块对应的目录中提交；需要修改其他模块的目录时，先在群里确认。
2. 修改共享场景（尤其 S4 月面主场景）前先 pull，修改完成尽快 push；同一场景同一时间只允许一人修改，修改前在群里报备。
3. 提交信息格式：`模块X: 简短描述`，例如 `模块一: 月球车转向手感初版`。
4. 超过 50MB 的文件不提交（大模型、视频先压缩，无法压缩的放群文件或网盘，仓库中只保留链接）。
5. Library / Temp / Build 等生成目录已被 `.gitignore` 排除，无需手动处理；不确定是否应提交的文件，先确认再提交。

## 如何开始

```bash
git clone <仓库地址>
```

然后用 Unity Hub → Open → Add project from disk，选中本文件夹打开。
