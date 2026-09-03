# HSRTimer

《人类一败涂地》（*Human: Fall Flat*，`Human.exe` / `Human.app`）的速通自动计时器插件。

HSRTimer 维护一个精确的 **游戏时间** 时钟（以游戏物理帧为步进，不受卡顿或墙钟干扰作
弊影响），依据游戏侧权威状态机自动判定关卡起止，依据可配置的速通"类别 / 标签"持续校验
成绩有效性，渲染一个可编排的浮动信息面板，并提供完整的本地化支持。

> **English documentation**: [README.md](README.md)

---

## 功能特性 (v1)

- **游戏时间引擎** —— 以 `Time.fixedDeltaTime` 离散累计；按关自动开始 / 停止；分段与整局
  追踪；暂停期间始终计时，菜单 / 大厅期间始终不计时；进入菜单 / 大厅时自动重置。
- **现实时间计时器** —— 与整局同步开始的墙钟计时；过关加载期间不间断，通过最后一关时与
  游戏时间同时停表；默认显示在游戏总时间下方且始终在后台活跃，可在设置中切换显示。
- **标准风格面板** —— 可编排的有序行、逐字符双色渐变(含透明度)、屏幕任意位置的自定义
  文本、可拖动面板。
- **类别与标签** —— 通过标签定义速通规则集。内置: `检查点`、`无检查点`、`禁跳`、`旁白`。
  通过 `ITagRule` 接口可扩展。
- **检查点合规** —— 跳关检测(内置例外表)与终点检查点校验。
- **成绩有效性检测** —— 作弊码、游戏变速、游戏时钟篡改；区分"不可原谅"与"可原谅"标记。
- **一键重试** —— 瞬间重启当前关卡。
- **本地化** —— 社区可翻译的 `键名:译文` 文件；英文为内置基准；附带简体中文示例翻译。

完整规格见 [REQUIREMENTS.md](REQUIREMENTS.md)。

## 安装

1. 为《人类一败涂地》安装 **BepInEx**(已测试 BepInEx 5.x / HarmonyX)。
2. 自行构建(见下)或获取 `HSRTimer-v{version}.dll`。
3. 将 `HSRTimer-v{version}.dll` 复制到游戏的 `BepInEx/plugins/` 目录。
4. 将 `lang/*.txt` 复制到插件运行时目录
   `<BepInEx 配置目录>/HSRTimer/lang/`
   (插件首次运行时若该目录缺失会自动创建并写入默认文件)。
5. 启动游戏，在 BepInEx 控制台确认出现 `HSRTimer is loaded!`。

### 可选: Level Collections

若安装了 [Level Collections](https://github.com/HeyBlack233/LevelCollections)(`LevelCollections`)插件，HSRTimer 会
与之集成，将地图包的最后一关视为整局结束。HSRTimer 在没有该插件时也能正常工作(声明为
可选依赖 / soft dependency)。

## 构建

需要 .NET SDK(`dotnet`)与通过 Steam 安装的游戏。

```bash
dotnet build src/HSRTimer/HSRTimer.csproj
```

构建会从默认的 Steam 安装路径解析游戏 / BepInEx 的 DLL 引用(见 `Directory.Build.props`)。
若 Steam 库不在默认路径或使用其他平台，请通过环境变量覆盖路径:

```bash
GAME_MANAGED="/路径/Human_Data/Managed" \
BEPINEX_CORE="/路径/BepInEx/core" \
dotnet build src/HSRTimer/HSRTimer.csproj
```

产物包含版本化副本 `src/HSRTimer/bin/Debug/netstandard2.0/HSRTimer-v{version}.dll`，部署时使用该文件；同时仍会生成不带版本号的 `HSRTimer.dll` 以兼容外部插件引用。

## 配置

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

## 文档

- [架构](docs/zh/ARCHITECTURE.md)
- [类别与标签](docs/zh/CATEGORIES.md) · [检查点规则](docs/zh/CHECKPOINTS.md)
- [配置](docs/zh/CONFIG.md) · [面板](docs/zh/HUD.md)
- [设置面板](docs/zh/PANEL.md)
- [本地化](docs/zh/LOCALIZATION.md) · [扩展(自定义标签)](docs/zh/EXTENDING.md)
- [旁白检测](docs/zh/VOICELINE.md)

## 许可证

MIT，见 [LICENSE](LICENSE)。
