# 月征：绕·落·回（LunarQuest）

> 探月工程沉浸式体验 —— 虚拟现实设计课程设计 · 四人小组项目

以中国探月工程"绕、落、回"三步走为背景的第一人称沉浸式体验：发射场起飞 → 绕月飞行 → 月面着陆 → 月面科考（低重力行走 + 月球车越野 + 任务系统）→ 携带样本返回地球。

完整设计见 `Docs/月征-探月VR项目方案书.md`；课程原始要求见 `Docs/虚拟现实课程设计.pdf`。

## 环境要求（全员统一，别装错版本）

| 项目 | 要求 |
|---|---|
| Unity | **2022.3 LTS**，全员用同一个 2022.3.x 小版本（Unity Hub → Installs 安装） |
| 模板 | 3D (URP) |
| IDE | Visual Studio 2022 或 VS Code（写 C#） |
| Git | 命令行 Git + GitHub Desktop（不熟命令行的一律用 GitHub Desktop 提交） |

**克隆后必做（每人一次）：** Unity 编辑器 → Edit → Project Settings → Editor →
1. Asset Serialization = **Force Text**（必须，否则场景文件无法合并，等于给全组埋雷）
2. Version Control = Visible Meta Files（新版本默认已开，确认一下）

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

## 协作规则（避免把仓库搞炸）

1. **只在自己模块的目录里提交**；要动别人的目录，先在群里说一声。
2. 改共享场景（尤其 S4 月面主场景）前先 pull，改完尽快 push；同场景同时只能有一个人在改，改前群里报备。
3. 提交信息格式：`模块X: 简短描述`，例如 `模块一: 月球车转向手感初版`。
4. **超过 50MB 的文件不要提交**（大模型、视频先压缩，压不小就放群文件/网盘，仓库里只留链接）。
5. Library / Temp / Build 等垃圾目录已被 `.gitignore` 挡住，无需手动处理；拿不准的文件先问再提交。

## 如何开始

```bash
git clone <仓库地址>
```

然后用 Unity Hub → Open → Add project from disk，选中本文件夹打开。
