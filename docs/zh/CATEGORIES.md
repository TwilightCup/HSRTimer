# 标签

> **English (source of truth)**: [../CATEGORIES.md](../CATEGORIES.md)

HSRTimer **没有类别预设**。当前的规则集就是你启用的**标签**(R3)集合 ——
在设置面板的"类别"页(默认键 `Home`)勾选即可。每个标签就是一条规则,决定哪些游戏内行为会导致成绩无效。

标签**叠加**:任意组合,所有规则同时生效。

## 内置标签

| 标签 id | 意图 | 机制 |
|---------|------|------|
| `Checkpoint`(R3.3) | 依次通过全部检查点 | 跳关检测(R4.1)+ 终点检查点校验(R4.2);面板显示当前检查点 |
| `NoCheckpoint`(R3.4) | **不**触发任何检查点 | `currentCheckpointNumber > 0` 即判无效 |
| `Jumpless`(R3.5) | 全程不跳跃 | `Human.Localplayer.jump` 由假变真即判无效 |
| `Voiceline`(R3.6) | 触发全部旁白 | 遗漏任何 `NarrativeBlock` 或跳过 Easter 旁白即判无效(见 [VOICELINE.md](VOICELINE.md)) |

不启用任何标签(纯任意%)时,仅受通用有效性检测(R5.1:作弊、变速、漂移)约束 —— 成绩被标记时面板显示红色提示。

## 启用标签

在设置面板的"类别"页勾选想要的标签。改动即时生效,并在关闭面板 / 退出游戏时写入 `tags.ini`(见 [CONFIG.md](CONFIG.md))。也可直接编辑 `tags.ini`:

```ini
[tags]
enabled = Checkpoint, Jumpless
```

## 添加自定义标签

第三方插件可通过 `ITagRule` API 注册自己的标签规则;它们会与内置标签一同出现在"类别"页。见 [EXTENDING.md](EXTENDING.md)。
