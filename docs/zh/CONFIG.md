# 配置

> **English (source of truth)**: [../CONFIG.md](../CONFIG.md)

所有配置位于 `<BepInEx 配置目录>/HSRTimer/`(典型安装下为
`~/Library/Application Support/Steam/steamapps/common/Human Fall Flat/BepInEx/config/HSRTimer/`)。
文件为人类可读的分节 `key = value` 文本,`#` 行为注释。

每个文件都**逐行容错解析**:格式错误的行会被跳过,并在日志中给出文件名与行号的警告。插件绝不会因一行错误而无法启动(规格 N6)。缺失的键回退到默认值。

## settings.ini

```ini
[settings]
count_in_pause = true
count_in_menu = false
auto_reset = true
restart_clears_forgivable = false
retry_min_dwell = 0.5
show_hud = true
language = en
reset_key = Backspace
retry_key = R
menu_key = Home
```

| 键 | 取值 | 默认 | 说明 |
|----|------|------|------|
| `count_in_pause` | true/false | true | R1.8 暂停时计时 |
| `count_in_menu` | true/false | false | R1.9 菜单/大厅时计时 |
| `auto_reset` | true/false | true | R1.7.2 |
| `restart_clears_forgivable` | true/false | false | R5.4.3 —— 在关卡内**暂停菜单**点击"重新开始"时清除可原谅的有效性标记(计时器继续计时,不重置)。一键重试则无条件清除(固定行为);整局重置会清除全部标记。 |
| `retry_min_dwell` | 秒(≥0) | 0.5 | R6 重试时在空场景强制停留的最短时间,从按下重试键开始计。若关卡重载快于该值,则在空场景内等待到该时间后再重载;`0` 表示不强制停留。 |
| `show_hud` | true/false | true | R2.5.1 |
| `language` | BCP-47 代码 | en | 对应一个 `lang/<code>.txt` |
| `reset_key` | KeyCode | Backspace | 重置成绩键 |
| `retry_key` | KeyCode | R | 重试关卡键 |
| `menu_key` | KeyCode | Home | 打开/关闭设置面板键 |

> **作弊 / 变速 / 漂移检测(R5.1)始终开启,阈值为硬编码,刻意不可配置** —— `settings.ini` 中没有 `drift_tolerance` 或任何其他反作弊选项。

> **提示:** 与其手改 `settings.ini`,不如在游戏内按 **设置面板键**(默认 `Home`)。所有选项都可在面板内编辑,改动实时生效,并在关闭面板 / 退出游戏时写盘。

键位为 Unity `KeyCode` 枚举名,如 `Backspace`、`Home`、`R`、`Keypad0`、`Alpha1`、`LeftControl`。

## tags.ini

HSRTimer **没有类别预设**。当前的规则集就是用户启用的标签集合(在设置面板的"类别"页中勾选)。

```ini
[tags]
enabled = Checkpoint, Jumpless
```

- `enabled` —— 逗号分隔的标签 id。内置 id:`Checkpoint`、`NoCheckpoint`、`Jumpless`、`Voiceline`。第三方插件的自定义标签用其自身的 id(见 [EXTENDING.md](EXTENDING.md))。留空即为纯任意%(仅受通用有效性约束)。

见 [CATEGORIES.md](CATEGORIES.md)。

## layout.ini

```ini
[text]
offset_x = 16
offset_y = 16
font_size = 18
color_a = FFD950FF
color_b = FFF299FF

[rows]
0 = GameTime
1 = CurrentSegment
2 = LastSegment

[custom.0]
x = 400
y = 50
text = {date} {time}
color_a = FFFFFFFF
color_b = CCCCCCCF

[custom.1]
x = 400
y = 80
text = Collection: {collection}
```

- `[text]` —— 主文本块直接绘制在屏幕上(无窗口、不可拖动)。`offset_x`/`offset_y` 为距屏幕左上角的像素偏移;`font_size` 为字号;`color_a`/`color_b` 为默认双色渐变(十六进制,见 [HUD.md](HUD.md))。
- `[rows]` —— 有序行;键为从 0 开始的索引。行类型:`GameTime`、`CurrentSegment`、`LastSegment`、`LastRun`、`CurrentState`。
- `[custom.<n>]` —— 位于 `(x, y)` 的任意屏上文本,各自带渐变。模板变量:`{date}`、`{time}`、`{version}`、`{collection}`、`{category}`、`{gametime}`。

整个计时器的显示/隐藏由 `settings.ini` 中的 `show_hud`(及切换面板键)控制,不在 `layout.ini` 中。

## lang/*.txt

见 [LOCALIZATION.md](LOCALIZATION.md)。
