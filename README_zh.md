[English](README.md) | 中文

# HSRTimer

《人类一败涂地》（*Human: Fall Flat*）的速通自动计时器插件。

HSRTimer 维护一个精确的 **游戏时间** 时钟（以游戏物理帧为步进，不受卡顿或墙钟干扰作
弊影响），依据游戏侧权威状态机自动判定关卡起止，依据可配置的速通"类别 / 标签"持续校验
成绩有效性，渲染一个可编排的浮动信息面板，并提供完整的本地化支持。

若安装了 [Level Collections](https://github.com/HeyBlack233/LevelCollections)(`LevelCollections`)插件，HSRTimer 会
与之集成，将地图包的最后一关视为整局结束。HSRTimer 在没有该插件时也能正常工作(声明为
可选依赖 / soft dependency)。

## 配置

### 设置面板

所有功能都可通过游戏内设置面板配置。
打开面板的默认按键为 `HOME`。

### 配置文件

所有配置位于 `<BepInEx 配置目录>/HSRTimer/`:

| 文件 | 内容 |
|------|------|
| `settings.ini` | 各项开关、键位、语言、漂移容差 |
| `tags.ini` | 已启用的规则标签(即当前规则集 —— 无类别预设) |
| `layout.ini` | 面板行、自定义文本、面板位置、渐变颜色 |
| `lang/*.txt` | 本地化文件 |

每个键的说明见 [docs/zh/CONFIG.md](docs/zh/CONFIG.md)。配置文件均为人类可读文本；单行
格式错误会被跳过并在日志中给出文件名与行号的警告(不会因一行错误导致插件无法启动)。

## 默认键位

| 动作 | 默认键 |
|------|--------|
| 重置成绩 | `Backspace` |
| 重试关卡 | `R` |
| 打开/关闭设置面板 | `Home` |

所有设置均可在设置面板中实时调整(按 `Home`)。按键改绑也在面板内进行 ——
聚焦某个按键项后按下目标键。改动在关闭面板或退出游戏时写入磁盘。详见
[docs/zh/CONFIG.md](docs/zh/CONFIG.md)。

## 构建

需要 .NET SDK(`dotnet`)与通过 Steam 安装的游戏。

```bash
dotnet build src/HSRTimer/HSRTimer.csproj
```

构建会从 `GAME_MANAGED` / `BEPINEX_CORE` 解析游戏 / BepInEx 的 DLL 引用。
本机的这两个路径写在被 git 忽略的 `Directory.Build.user.props` 中；在其他机器上或
Steam 库不在默认路径时，请通过环境变量设置:

```bash
GAME_MANAGED="/路径/Human_Data/Managed" \
BEPINEX_CORE="/路径/BepInEx/core" \
dotnet build src/HSRTimer/HSRTimer.csproj
```

产物包含版本化副本 `src/HSRTimer/bin/Debug/netstandard2.0/HSRTimer-v{version}.dll`，部署时使用该文件；同时仍会生成不带版本号的 `HSRTimer.dll` 以兼容外部插件引用。

## 许可证

MIT，见 [LICENSE](LICENSE)。
