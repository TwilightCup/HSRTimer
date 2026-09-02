# 设置面板

> **English (source of truth)**: [../PANEL.md](../PANEL.md)

设置面板(用 **设置键**,默认 `Home`,打开 / 关闭)可实时编辑所有可调选项 ——
改动即时生效,并在关闭面板或退出游戏时写入磁盘。面板分为三个标签页。

> 作弊 / 变速 / 漂移检测(R5.1)始终开启,阈值为硬编码,刻意**不**在面板的任何地方暴露。

## 常规

- **计时** —— `auto_reset`、`restart_clears_forgivable`(暂停菜单重新开始时清除可原谅标记;见 [CONFIG.md](CONFIG.md))。暂停期间始终计时,菜单 / 大厅期间始终不计时,二者均无开关。
- **语言** —— 从已加载的语言集合中单选当前语言。"重新加载语言文件" 会重新扫描 `lang/*.txt`。
- **按键绑定** —— 重置 / 重试 / 设置键。改绑方法:点击对应项,然后按下目标键。纯修饰键(Shift/Ctrl/Alt/Cmd)按下会被忽略。

## 界面

- **面板** —— `show_hud`;`show_real_time`(显示始终活跃的现实时间计时器);`show_wake_up_time`(在右侧列显示起身时间);`center_loading_saving`(将游戏自带的右上角"加载/保存"提示移到顶部居中);主文本块的横向 / 纵向偏移(`offset_x`、`offset_y`)、字号(`font_size`),以及双色渐变(`color_a`、`color_b`,每通道 RGBA 滑块)。

## 类别

- **规则标签** —— 所有已注册规则标签的多选(四个内置标签:`Checkpoint`、`NoCheckpoint`、`Jumpless`、`Voiceline`,以及其他插件注册的自定义标签 —— 见 [EXTENDING.md](EXTENDING.md))。勾选即启用该标签,取消即禁用。没有类别预设 —— 这个标签集合就是当前的规则集。改动即时生效,并在关闭 / 退出时写入 `tags.ini`。

各标签的作用见 [CATEGORIES.md](CATEGORIES.md),Checkpoint 标签的规则见 [CHECKPOINTS.md](CHECKPOINTS.md)。
