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
| `Jumpless`(R3.5) | 全程不跳跃 | `Human.Localplayer.jump` 由假变真即判无效;标签启用期间同时在游戏输入层强制禁用跳跃键(见 [ARCHITECTURE.md](ARCHITECTURE.md)) |
| `Voiceline`(R3.6) | 触发全部旁白 | 遗漏任何 `NarrativeBlock` 或跳过 Easter 旁白即判无效(见 [VOICELINE.md](VOICELINE.md)) |
| `Glitchless` | 全程不使用任何 glitch | SSG(半身不遂):当反爬墙分支提前返回后,某只手残留 `grabObject != null` 但没有 `grabJoint` 时判无效。Prop Fly(御物飞行):踩在手上抓着的可移动物体上跳跃即判无效。Footsie(水踩点):在 Water (River) 关卡的 Footsie Spot 范围内碰到通关点即判无效 |
| `NoEC` | 全程不爬墙 | 当玩家离开地面(`onGround` 变为 false)后,以本次离地期间第一次抓取的高度为基准;若离地时已经抓着物体,则取双手抓取点中较高的一个。离地期间任何高于基准 +0.2m 的新抓取即判 EC,并带 0.2 秒防抖(每 0.2 秒最多触发一次)。`Ec` 违规为 **soft flag**:以普通文本颜色独立成行显示并附带触发次数(如 `EC x3`),每次新触发时整行闪红,一键重试和计时器重置时清除,但暂停菜单重开不清除 |

不启用任何规则标签(纯任意%)时,仅受通用有效性检测(R5.1:作弊、变速、漂移)约束 —— 成绩被标记时面板显示红色提示。多人会话期间,即使没有开启任何规则标签,自动的 `Co-op` 标签(见下)也会处于启用状态。

## 启用标签

在设置面板的"类别"页勾选想要的标签。改动即时生效,并在关闭面板 / 退出游戏时写入 `tags.ini`(见 [CONFIG.md](CONFIG.md))。也可直接编辑 `tags.ini`:

```ini
[tags]
enabled = Checkpoint, Jumpless
```

## 自动标签:Co-op(多人)

`Co-op`(R3.10)是**标签型标签**,不是规则。与上面的规则型标签不同:

- **没有任何判定逻辑** —— 从不注册为 `ITagRule`,引擎的规则循环会忽略它,因此**永远不可能产生无效标记**;
- **不出现在"类别"页**中(该页只列出已注册的规则),因此无法手动勾选;
- 在多人会话期间(`NetGame.isServer` / `NetGame.isClient`,即主持或加入合作游戏时)**自动启用**,回到单人模式后自动移除。

启用期间,该标签会出现在所有展示启用标签集合的地方 —— HUD 的"规则标签"行与 `{category}` 模板变量;同时它参与 subsegment/marker PB 存储的类别键,使合作模式成绩与单人成绩分开存放。该标签**从不持久化**到 `tags.ini`。

## 添加自定义标签

第三方插件可通过 `ITagRule` API 注册自己的标签规则;它们会与内置标签一同出现在"类别"页。见 [EXTENDING.md](EXTENDING.md)。
