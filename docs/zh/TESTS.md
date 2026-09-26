# TESTS(测试文档)

> **English (source of truth)**: [../TESTS.md](../TESTS.md)

本文说明如何**在游戏内**通过自带的开发者控制台(默认按键 **`~`** 或 **F1**)测试 HSRTimer 的每一个功能。

## 这是什么

HSRTimer 在插件加载时向游戏的 `Shell` 控制台注册了一套 `hsr ...` 命令。你可以查看实时状态、切换设置、修改配置、开关标签(tags)、编辑 HUD 布局、管理预设(presets)、操作分段(subsegment)与标记(markers)、以及模拟有效性标记(validity flags)——全部无需重启游戏或手工编辑文件。

所有 `hsr` 命令都可以安全地在控制台中执行。大多数修改会走与设置面板相同的 `ConfigService.SaveSettings()` 流程,因此会正常持久化到 `settings.ini` / `tags.ini` / `layout.ini`。

## 打开控制台

1. 安装 HSRTimer 后启动《人类一败涂地》。
2. 按 **`~`**(波浪线 / 反引号)或 **F1** 打开游戏自带的开发者控制台。
3. 输入 `hsr` 并回车查看命令列表。
4. 输入 `hsr help <topic>` 查看某个命令的详细帮助。

> 如果控制台没有出现,可能是该游戏版本的按键有变动。插件启动时仍会在 BepInEx 日志中输出 `HSRTimer: dev-console commands registered`。

> **关于大小写:** 游戏控制台会把整行输入转为小写后再执行命令。因此 HSRTimer 会把标识符(标签 id、语言代码、预设名、排行榜模式)按大小写不敏感方式解析并规范化为标准写法。自由文本(例如自定义文本或标记名称)会按控制台传入的内容保存,即通常是小写。

## 命令参考

| 命令 | 说明 |
|---|---|
| `hsr` | 打印完整命令摘要 |
| `hsr help [topic]` | 打印某个主题的帮助 |
| `hsr status` | 输出计时器 / 配置 / HUD / 分段 / 标记 / LC 的实时状态 |
| `hsr clock [status\|history [n]\|clear]` | 查看纯 tick 游戏时钟与分段边界(R1.11) |
| `hsr keys` | 列出所有可设置的 settings/layout 键 |
| `hsr get <key>` / `hsr get all` | 读取单个配置值,或全部配置值 |
| `hsr set <key> <value>` | 设置并保存一个配置值 |
| `hsr reload` | 从磁盘重新读取所有配置文件与语言文件 |
| `hsr save` | 保存当前内存中的配置 |
| `hsr reset` | 整局重置(等同于重置键) |
| `hsr retry` | 一键重试(等同于重试键,R6) |
| `hsr pass [real]` | 模拟过关流程:默认设置 `Game.passedLevel` 后触发 `Game.Fall`;`real` 清除动量、取消抓取并把玩家传送到判定箱,由游戏自身的触发器完成过关。均不写入 subsegment/marker 的 PB |
| `hsr hud [on\|off\|toggle\|status]` | 控制计时 HUD 的显示 |
| `hsr panel [open\|close\|toggle\|status]` | 控制设置面板 |
| `hsr leaderboard [cycle\|show\|hide\|mode <Subsegment\|Markers>\|status]` | 控制排行榜 HUD |
| `hsr layout [status\|row ...\|text ...\|get <key>\|set <key> <value>]` | 查看 / 编辑 HUD 布局 |
| `hsr tag [list\|label <status\|on\|off\|auto>\|enable <id>\|disable <id>\|set <id> <on\|off>]` | 开关启用的标签规则;查看/强制自动 Co-op 标签 |
| `hsr lang [list\|set <code>\|reload\|current]` | 管理本地化 |
| `hsr preset [list\|current\|create <name>\|apply [name]\|save\|delete <name>]` | 管理预设(R11) |
| `hsr sub [status\|entries\|clear]` | 查看 / 清空分段模块 |
| `hsr marker [list\|feed\|add ...\|remove <id>\|toggle <id>\|pb <ms>\|pbclear\|clear\|save\|reload]` | 查看 / 编辑标记(R10) |
| `hsr flags [list\|raise <Reason>\|clear [forgivable\|soft\|all]]` | 查看 / 修改有效性标记(R5) |
| `hsr lc [status\|restart]` | 查看 LevelCollections 集成或触发 `lc restart` |
| `hsr config [path\|files]` | 打印 HSRTimer 配置路径 |
| `hsr about` | 打印插件名称、版本、许可证声明与仓库 URL(R12) |
| `hsr update [status\|check\|apply\|cancel\|base [url]]` | 从 GitHub releases 检测 / 安装插件更新(R13) |

## `hsr get` / `hsr set` 可接受的键

命令使用 `SettingsModel` 与 `LayoutModel` 公共字段的 snake_case 形式。常见示例:

- `auto_reset`、`restart_clears_forgivable`、`retry_min_dwell`
- `retry_level_override_enabled`、`retry_level_override`
- `show_hud`、`show_real_time`、`show_wake_up_time`
- `only_record_first_wake_up_time`、`center_loading_saving`、`language`
- `reset_key`、`retry_key`、`menu_key`
- `subsegment_enable`、`subsegment_pb_path`、`subsegment_load_path`、
  `subsegment_toggle_key`、`subsegment_multi_project`、`subsegment_debug_logging`
- `markers_enable`、`markers_edit_mode`、`markers_path`、`markers_debug_logging`、
  `markers_overlay_fill_color`、`markers_overlay_label_color`
- 布局: `offset_x`、`offset_y`、`font_size`、`color_a`、`color_b`、
  `leaderboard_font_size`、`leaderboard_offset_x`、`leaderboard_offset_y`、
  `leaderboard_mode`、`leaderboard_markers_time_mode`

布尔值接受 `true/false`、`1/0`、`on/off`、`yes/no`。键位接受 Unity `KeyCode`
名称(例如 `Backspace`、`R`、`Home`、`Tab`)。颜色接受 `RRGGBB` 或 `RRGGBBAA` 十六进制。

## 分功能测试指南

### 1. 计时核心 / 成绩控制

```text
hsr status                 # 查看游戏/应用状态、游戏时间、分段、现实时间
hsr clock                  # 纯 tick 时钟:PlayableTicks、分段起止 tick
hsr clock history          # 最近的分段起止 tick(抖动检查,R1.11)
hsr clock clear            # 清空边界历史
hsr reset                  # 验证计时器归零、标记被清除
hsr retry                  # 验证一键重试会重载关卡
hsr pass                   # 模拟通过当前关卡(过关)
hsr pass real              # 传送到判定箱,让游戏自身完成过关
```

在关卡内,`hsr status` 应显示 `segment=True`、`timing=True` 且 `gameTime` 持续增长。
执行 `hsr reset` 后,`gameTime` 应为 `0`,`inSegment` 应为 `False`(或过渡缓存被保留,关卡不会因此重新计时)。

`hsr pass` 无需走到终点落地区即可驱动真实的过关流程:它先设置
`Game.passedLevel`(等同于 `Game.EnterPassZone`),等待一帧让引擎锁定
`LevelPassed`,再调用 `Game.Fall` —— 内置战役关卡经 `PassLevel` → `StartNextLevel`
推进,Workshop / EditorPick 关卡经 `PauseLeave` 离开。之后 `hsr status` 应显示
本段用时,若为一场成绩的最后一关还应显示 `lastRun`。该命令需要处于分段内且存在
本地玩家,客户端与回放播放期间为无效操作。**subsegment 与 marker 的 PB 不会写入**
(这是测试过关),真实的 PB 文件保持不变。

`hsr pass real` 走真实的触发器链路而不是直接置标志:它把玩家动量清零
(所有身体部件的线速度与角速度)、取消双手抓取,并把本地玩家传送到本关
`LevelPassTrigger`(通关判定箱)的中心。随后由游戏自身流程接管 —— 判定箱触发
`passedLevel`,玩家落入下方的 `FallTrigger` 由 `Game.Fall` 完成过关。PB 抑制与
默认模式一致。若命令报告 `LevelPassed` 未锁定,说明玩家未能进入判定箱
(检查触发器的 collider / tag / 关卡布局)。

**边界确定性(R1.11)。** `hsr clock` 打印原始整数 tick 时钟(`playableTicks`、`segmentStartTicks`、`pendingEndTicks` 等)。`hsr clock history` 列出每次分段起止 tick:连续载入 + 通关同一关卡 50 次,相同操作下的 `dur=`(终点 tick − 起点 tick)必须**完全一致**(零 ±1 抖动)。测量前用 `hsr clock clear` 清空历史。

每条 `end` 记录带两个诊断字段:`src=hook|poll` 表示终点 tick 是由精确的 `Game.Fall` 边界 hook 锁定(真实通关)还是由轮询记录(中途退出);`step=` 是观察到该边界的全局物理帧。另有一条 `pass` 行标记权威过关 tick。二者合起来说明:**过关到状态翻转之间的物理帧没有被计入分段** —— 这正是旧版 ±1 抖动的来源。

起点侧同样成对出现:`load` 是 `Game.AfterLoad` hook 帧(权威分段起点),紧随其后的 `start` 行是轮询消费该闩锁的结果。`start` 的 tick 等于 `load` 的 tick 而 `step=` 更大,即证明起点边界不再取决于轮询何时发现它。

### 2. 有效性标记(R5)

```text
hsr flags list
hsr flags raise CheatCode            # 不可原谅
hsr flags raise CheckpointSkip       # 可原谅
hsr flags raise Ec                   # 软标记,计数递增
hsr flags clear forgivable
hsr flags clear soft
hsr flags clear all
```

`hsr status` 的 `flags:` 行应显示硬性原因,软标记显示为 `Ec xN`。

### 3. 标签 / 类别(R3)

```text
hsr tag list
hsr tag enable Checkpoint
hsr tag enable Jumpless
hsr tag set NoEC on
hsr tag disable Jumpless
hsr tag set NoCheckpoint off
hsr status          # 会列出已启用的标签
```

标签改动会立即持久化到 `tags.ini`。

**自动标签 `Co-op`(R3.10)。** `hsr tag list` 会在独立的 `labels (auto):` 行中列出标签型标签及其实时状态 —— 仅在多人会话期间(`hsr status` 中的 `server=` / `client=`)显示 `Co-op [on]`,单人模式下为 `[off]`。该标签无法像规则标签一样手动开关:

```text
hsr tag set Co-op on      # 会被拒绝:"is an auto label ... cannot be toggled manually"
```

无需真实多人会话即可测试标签显示,可用 `hsr tag label` 强制开关(仅本次会话有效;`auto` 恢复按多人模式自动):

```text
hsr tag label status      # effective / net / override
hsr tag label on          # 强制开启(HUD"规则标签"行与 {category} 显示 "Co-op")
hsr tag label off
hsr tag label auto        # 恢复自动(仅在多人会话期间开启)
```

验证真实周期:主持或加入合作游戏 → `hsr tag list` 显示 `Co-op [on]`,且 HUD 的"规则标签"行 / `{category}` 模板变量包含 "多人"(中文界面;英文界面为 "Co-op");离开会话 → 变回 `[off]`。该标签从不持久化:`hsr get all`(tags 段)与 `tags.ini` 中永远不会有 `Co-op`。

### 4. HUD / 布局(R2)

```text
hsr hud off
hsr hud on
hsr layout status
hsr layout row list
hsr layout row add CurrentState
hsr layout row remove 5
hsr layout text add 20 400 "Hello {gametime}"
hsr layout text list
hsr layout set font_size 24
hsr layout set offset_x 30
hsr layout set color_a FF0000FF
```

HUD 应在下一帧生效,且改动持久化到 `layout.ini`。

### 5. 设置面板 / 常规设置

```text
hsr panel open
hsr panel close
hsr set auto_reset false
hsr set auto_reset true
hsr set language zh-Hans
hsr lang list
hsr lang set en
hsr reload
```

`hsr panel open` 应弹出与 Home 键相同的 IMGUI 设置面板。
**关于** 标签页是面板导航的第一项;它显示插件名称、版本、MIT 许可证的前两行、一个 **GitHub 仓库** 按钮,以及一个 **检查更新** 按钮(R13);发现新版本时会显示发布标题、其日期 + Highlights 摘要,以及 **打开 Release 页面** / **更新** 按钮。`hsr about` 打印相同的身份 / 许可证 / 仓库文本,因此无需截图即可核对:

```text
hsr panel open
hsr about
```

### 6. 预设(R11)

```text
hsr preset list
hsr preset create test-preset
hsr layout set font_size 30
hsr preset save
hsr preset apply default
hsr preset apply test-preset
hsr preset delete test-preset
```

执行 `apply` 后,HUD 应反映该预设保存的布局 / 标记。

### 7. 分段模块(R8)

```text
hsr sub status
hsr sub entries
hsr sub clear
```

`hsr sub status` 打印启用标志、路径、多局状态与当前排行榜条目数。
`hsr sub entries` 列出每个已加载参考及其最新结算的差值。

### 8. 标记(R10)

进入关卡后:

```text
hsr marker list
hsr marker add range "Test Box"          # 使用玩家位置,2 米盒子
hsr marker add checkpoint "CP1" 1
hsr marker add grab "My Box"             # 需要当前恰好抓取一个物体
hsr marker toggle m1
hsr marker pb 12345
hsr marker list
hsr marker save
hsr marker reload
hsr marker clear
```

当编辑模式开启(`hsr set markers_edit_mode true`)时,标记覆盖层 / feed 应响应这些改动。

排行榜 feed 必须**按尝试**重置:`hsr pass` 过关后进入下一次尝试——即使下一关仍是同一关(剧情重复关卡)——`hsr marker feed` 应只列出新尝试的行。新尝试的首次触发会**替换**掉上一次尝试的残留行,而不是追加在其后。

### 9. 本地化(R7)

```text
hsr lang list
hsr lang set zh-Hans
hsr lang set en
hsr lang reload
```

设置面板与 HUD 文案应即时切换语言。

### 10. 排行榜 HUD

```text
hsr leaderboard status
hsr leaderboard show
hsr leaderboard mode Markers
hsr leaderboard mode Subsegment
hsr leaderboard hide
hsr leaderboard cycle
```

### 11. LevelCollections 集成(可选)

```text
hsr lc status
hsr lc restart
```

`hsr lc status` 报告 LC 集成是否启用、是否处于集合运行中,以及当前集合名称。
`hsr lc restart` 触发与重试委托相同的 `lc restart` 命令。

### 12. 配置文件位置

```text
hsr config path
hsr config files
```

打印插件使用的确切路径,方便你在磁盘上核对或编辑文件。

### 13. 更新检查(R13)

关于标签页的 **检查更新** 流程可以从控制台端到端测试(结果写入 BepInEx 日志,`hsr-cmd.sh` 可直接读取):

```text
hsr update status                        # 阶段 / 仓库基地址 / feed / 上次检测的版本 tag
hsr update check                         # 读取 github.com/{owner}/{repo}/releases.atom
hsr update status                        # 阶段应变 HasUpdate(或已最新 / 出错)
hsr update apply                         # 下载并安装发布 DLL
hsr update status                        # 阶段应变 RestartRequired
```

无网络时,可把检测指向本地 HTTP 服务器来分别走通成功与失败分支:

```text
hsr update base http://127.0.0.1:PORT/repo   # 仅本次会话的仓库基地址覆盖
hsr update check                              # feed 地址 = <base>/releases.atom
hsr update apply                              # 下载地址 = <base>/releases/download/<tag>/HSRTimer-v<ver>.dll
hsr update base clear                         # 恢复真实仓库基地址
```

说明:

- feed 必须是 GitHub Atom XML(`<feed>` 内含 `<entry>`;每个 entry 的 alternate 链接以 `/releases/tag/{tag}` 结尾,含 `<title>` 与可选的 `<content type="html">`)。取最新**非预发布** entry。
- 真实 apply 测试时,在推导出的下载路径下提供一个有效的 .NET 程序集(例如 `HSRTimer-v0.0.0.dll` 的副本)——安装器会拒绝零字节 / 无效下载,因此非程序集文件可用来走「无效插件 DLL」错误分支;完全不提供文件则可走「发布资产未找到」(404)分支。
- apply 成功后,在磁盘上核对 `BepInEx/plugins/` 中出现 `HSRTimer-v{新版本}.dll` 且不再有旧 `HSRTimer-v*.dll`(无法删除者变为 `HSRTimer-v*.dll.dis`,下次启动清理)。
- `hsr update cancel` 中止进行中的检测 / 下载。

## 测试清单

- [ ] `hsr` 打印命令摘要。
- [ ] 关卡内 `hsr status` 显示合理的实时值。
- [ ] `hsr reset` 将计时器归零并清除标记。
- [ ] `hsr clock` 显示整数 tick,且 `hsr clock history` 对重复的相同操作记录到一致的 `dur=`(R1.11)。
- [ ] `hsr retry` 重载当前关卡(或配置的重定向目标)。
- [ ] `hsr pass` 完成当前关卡;`hsr status` 显示记录的分段与(最后一关时)`lastRun`,且 subsegment/marker 的 PB 文件未变化。
- [ ] `hsr pass real` 把玩家传送到判定箱,由游戏自身的触发器链路完成过关(且 `LevelPassed` 已锁定);PB 同样不写入。
- [ ] `hsr hud off/on` 隐藏 / 显示计时 HUD。
- [ ] `hsr panel open/close` 打开 / 关闭设置面板。
- [ ] 关于标签页(导航第一项)显示名称 / 版本 / 许可证与仓库按钮;`hsr about` 与之匹配。
- [ ] `hsr update check` 报告已最新 / 显示更新版本 / 离线时显示一行错误,`hsr update apply` 安装 DLL(R13)。
- [ ] `hsr tag enable/disable` 改变启用的标签并持久化。
- [ ] `hsr set language zh-Hans` 切换界面语言。
- [ ] `hsr layout row add/remove` 改变 HUD 行。
- [ ] `hsr preset create/save/apply` 完整往返布局 + 标记。
- [ ] 有分段数据时 `hsr sub status/entries` 正常。
- [ ] 关卡内 `hsr marker add/list/toggle/pb` 正常。
- [ ] `hsr flags raise/clear` 显示预期的 HUD 横幅 / 软标记行。
- [ ] 安装或不安装 LevelCollections 时 `hsr lc status` 均正确报告。
