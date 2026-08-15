# 架构

> **English (source of truth)**: [../ARCHITECTURE.md](../ARCHITECTURE.md)

本文档说明 HSRTimer 的结构与设计决策。需求规格见 [../../REQUIREMENTS.md](../../REQUIREMENTS.md)。

## 核心原则:轮询,而非 patch

HSRTimer 所需的几乎所有信号都是游戏类的**公共字段或属性**:

| 信号 | 来源 |
|------|------|
| 游戏状态 | `Game.state`(`GameState`: Inactive/Paused/LoadingLevel/PlayingLevel) |
| 应用/联机状态 | `App.state`(`AppSate` —— 大厅/加载转换) |
| 本地/主机/客户端 | `NetGame.isLocal`、`NetGame.isServer`、`NetGame.isClient` |
| 当前检查点 | `Game.currentCheckpointNumber` |
| 作弊 | `CheatCodes.climbCheat`、`CheatCodes.throwCheat` |
| 跳跃 | `Human.Localplayer.jump` |

由于计时 / 分段 / 重置 / 检查点 / 有效性的规则都定义在这些字段的*转换*上,单个轮询循环(`TimerCore.FixedUpdate`)通过比较当前帧与缓存上一帧即可算出一切 —— 既廉价,又对游戏更新中重命名或内联私有方法具有鲁棒性。

**Harmony 仅用于没有字段能直接暴露事件的地方**:两个旁白 hook(`NarrativeBlock.Play` 与 `SubtitleManager.PlayNarrative`,见 [VOICELINE.md](VOICELINE.md)),以及暂停菜单重启 hook(`PauseMenu.RestartClick`,触发 `restart_clears_forgivable` 选项)。

## 模块布局

```
Plugin.cs                 入口: 串联配置 + 规则 + patch + LC,生成单例
PluginInfo.cs             GUID/NAME/VERSION
Core/
  TimerCore.cs            引擎 MonoBehaviour(FixedUpdate=计时, Update=按键/校验)
  RunState.cs             唯一权威状态(时间、分段、标记、缓存)
  SegmentLogic.cs         纯函数的附录 B 真值表
  RetryAction.cs          一键重试(R6)
Validation/
  InvalidReason.cs        枚举 + 严重度映射
  ValidityFlags.cs        不可原谅 / 可原谅标记集合
  GenericValidators.cs    作弊码检测
Tags/
  ITagRule.cs             标签规则接口 + ValidationContext
  TagRuleRegistry.cs      扩展注册表(R3.7)
  CheckpointRules.cs      R4 跳关例外 + 终点检查点表
  VoicelineTracker.cs     场景扫描 + Easter 检测
  Rules/                  Checkpoint / NoCheckpoint / Jumpless / Voiceline
Patches/
  PatchModule.cs          Harmony.CreateAndPatchAll
  NarrativeBlockPatches.cs    NarrativeBlock.Play 后缀
  SubtitleManagerPatches.cs   SubtitleManager.PlayNarrative 后缀
  PauseMenuPatches.cs         PauseMenu.RestartClick 后缀
Hud/
  TimerHud.cs             IMGUI 面板(R2)
  GradientText.cs         颜色十六进制/透明度 + 渐变助手
  TemplateVars.cs         {date}/{time}/{version}/{collection}/{category}
Config/
  ConfigService.cs        门面
  PersistenceService.cs   容错 INI 读写
  SettingsModel.cs, EnabledTagsModel.cs, LayoutModel.cs
Localization/
  LocalizationService.cs, LanguageFile.cs
LcIntegration.cs          可选的 LevelCollections 软集成
```

## 计时真值表(附录 B)

每个物理帧(`FixedUpdate`)按顺序执行:

1. **转换** —— 将 `state`/`appState` 与缓存上一帧比较:
   - **分段终点(R1.4)**:`PlayingLevel → LoadingLevel`,或本地 `PlayingLevel → Inactive`。(在自动重置清零*之前*记录。)
   - **自动重置(R1.7)**:`Paused→Inactive`、`ServerLoadLobby→ServerLobby`、`ClientLoadLobby→ClientLobby`,或本地 `PlayingLevel→Inactive` —— 仅当开启 `AutoReset` 时。
   - **分段起点(R1.2)**:`LoadingLevel/Inactive → PlayingLevel`,且不在大厅。
   - **暂停恢复(R1.3)**:`Paused → PlayingLevel` 且此前计时已停止。
2. **累计** —— 当 `PlayingLevel` 且不在大厅 / 不在等待主机时 `GameTime += Time.fixedDeltaTime`。
3. **菜单补回** —— 当 `CountInMenu` 开启时,在 `Inactive` 或大厅期间额外加 `fixedDeltaTime`。
4. **规则** —— 运行当前类别各标签规则的 `OnTick`。

`Update`(每渲染帧)负责:作弊码检查、**暂停补回**(因 `timeScale=0` 会使 `FixedUpdate` 停止,故在 `Paused` 期间加 `unscaledDeltaTime`),以及按键处理。

关卡加载屏(`LoadingLevel`)与客户端等待主机间隙(`ClientWaitServerLoad`)**始终不计入**,且无开关可补回。

## 为什么重试先卸载再重新启动关卡

R6.2 要求一次**完整的异步关卡重载**,含空过渡场景(R6.2.1.3)。HSRTimer 以引擎 MonoBehaviour 上的协程驱动:

1. `Game.instance.UnloadLevel()` 拆毁当前关卡 —— `AfterUnload` 把 `currentLevelNumber` 置为 `-1`、`state` 置为 `Inactive`,清空 `currentLevel` 与 `workshopLevel`。
2. `SceneManager.LoadScene("Empty")` 加载空的过渡场景。
3. **停留** —— 在空场景内停留至距按键已过 `retry_min_dwell` 秒(默认 0.5),用 `Time.unscaledTime` 计量。这让过快的重载得以喘息;`0` 表示不强制停留。空场景期间的时间绝不计入(`Retrying` 标志抑制累计)。
4. `App.instance.LaunchSinglePlayer(level, type, 0, 0)` 重新启动**重试目标关卡**(见下文 R6.4 —— 从菜单进入的战役运行中,该目标是本次运行的起点关卡,而非当前关卡)。其 `LoadLevel` 协程**仅当 `currentLevelNumber != levelNumber` 时**才重载场景 —— 因此第 1 步的卸载才是第 4 步真正重载的关键。协程依次执行 `SignalManager.BeginReset` → 重载场景 → `AfterLoad`(`state = PlayingLevel`、`RespawnAllPlayers`、`Level.Reset(0, 0)`)。全程不显示菜单(App 状态机即 R6.2.1.4 所述的隐藏流程跳板)。对 BuiltIn / EditorPick / Workshop 关卡通用。

省掉第 1 步,重新启动当前关卡会悄悄退化成检查点重生 —— 正是暂停菜单"Restart"按钮的行为,而这是 R6 明令禁止的。这里**不**用 `Game.RestartLevel(true)`(检查点重生、不重载场景),也**不**用 `Game.ReloadBundle()`(仅 Workshop 可用:它解引用 `workshopLevel.dataPath`,在内置关卡上抛异常,且在设置 `timeScale = 0` 之后崩溃,游戏卡在 "Empty" 场景、画面冻结)。

这是关卡级重启。重试即重新挑战当前关卡,因此两个实时计时器(游戏总时间与当前分段)都清零、关卡从头计时。它与 R1.7 整局重置相互独立,体现在它**不**清零本局的记录(已完成分段、`LastRun`)与无效标记。重载过程会让关卡经历 `PlayingLevel → Inactive → LoadingLevel → PlayingLevel`;为避免这被误判为整局退出,`RetryAction` 将 `GameTime`/`SegmentStart` 清零并置位 `RunState.Retrying`,引擎在重载期间据此处理:

- `SegmentLogic.IsAutoReset` 在 `Retrying` 期间抑制其 `PlayingLevel/Paused → Inactive` 分支(R1.7.3 分支还额外要求 `App.state == Menu` —— 真正的整局退出经 `PauseLeave → EnterMenu` 会到 Menu,而重试始终停在 LoadLevel)。因此即便 `AutoReset` 开启,重试也不会清零整局。
- `SegmentLogic.ShouldAccumulateMenu` 在 `Retrying` 期间返回 false,重载时停留在 `Inactive` 的时间绝不计入(即便 `CountInMenu` 开启)。
- `EndSegment` / `StartSegment` 在 `Retrying` 期间跳过 LC 整局成绩捕获、`LastRun` 快照**以及 tag 规则的 `OnLevelExit` 回调** —— 关卡是中途放弃、并非完成。`OnLevelExit` 执行 R4.2 最终检查点校验与语音线完成校验,若对放弃状态运行会误判 `INVALID_CHECKPOINT_FINAL` / `Voiceline`。当前关卡的分段仍会在重载关卡到达 `PlayingLevel` 时干净地重新开始。

`Retrying` 在下一段分段开始时、以及整局重置时清零。

## 为什么地图包运行中的重试会委托给 Level Collections

当 Level Collections(LC)插件已加载**且**玩家正处于某次地图包运行中(`CollectionManager.IsInCollectionRun`)时,一键重试**不再**原地重载当前关卡 —— 而是通过游戏开发者控制台注册表派发 LC 自身的 `lc restart` 命令(`Shell.RawInvoke("lc restart")`),把整局地图包从第 1 关重启。即 `R6.3`。

重启地图包意味着整局从第 1 关重新计时,因此它必须优先于单关重载。委托给 `lc restart`(而非反射 LC 内部)使 HSRTimer 复用 LC 的场景重载强制(`ResetCurrentLevelIfSame`)、关卡校验与启动逻辑 —— 且对配置地图包与临时地图包(`lc random`)均适用。`Shell.RawInvoke` 正是控制台所走的代码路径,因此派发的命令与手动键入 `lc restart` 行为完全一致。

计时器对此的处理与单关重试完全一致:先将 `GameTime`/`SegmentStart` 清零并置位 `RunState.Retrying`,这样被放弃关卡的分段终点不会被记为 `LastRun`,重载进入第 1 关也不会被误判为整局退出 —— 同时保留本局记录与(非可原谅)无效标记(R6.2.2 对整局重置 R1.7 的独立性在此同样适用)。实现位于 `RetryAction.TryExecute`(LC 分支)与 `LcIntegration.RestartCollection`。

两个被拦截的情况都以 `NOTIFY_RETRY_BLOCKED_STATE` 提示,且不改动任何计时状态:当 LC 的某条延迟命令(`lc restart/skip/random <秒>`)正在倒计时时(`IsDelayedCommandPending`),LC 自身会拒绝新的 `lc restart`,HSRTimer 因此同样拒绝;若 `RestartCollection` 本身返回 false(调用时 LC 缺失、运行已结束),已被投机清零的计时器会被还原,HSRTimer 回退到单关重载。

## R6.4 —— 战役"从菜单进入"的重试目标

在官方战役关卡列表(Intro–Reprise)中、**从菜单选关进入**的一次运行里,一键重试回到的是玩家*开始本局的那一关*(`RunState.CampaignRetryLevel`),而不是当前正在游玩的那一关。战役每过一关就自动推进(`PassLevel → StartNextLevel → LaunchSinglePlayer`),若不如此,重试目标会随关卡一路前移 —— 无法反复练习选定的某一关。

**判定 —— `Menu → LoadLevel` 的 App 状态边沿。** 游戏自身的状态机使这一信号毫无歧义(已在反编译的 `App.cs` 中核实):

- 菜单进入:`App.state: Menu → LoadLevel`(经 `LaunchGame`);
- 战役自动推进:`PlayLevel → LoadLevel` —— 从不经过 `Menu`;
- 重试自身的重载:停留在 `LoadLevel`,且处于 `Retrying` 标志下;
- 多人联机启动:走 `ServerLoadLevel`/`ClientLoadLevel`。

因此 `SegmentLogic.IsMenuEntry(prevApp, nowApp)` 只会在真正的菜单选关时触发。`TimerCore.HandleTransitions` 把它锁存进 `RunState.MenuEntryPending`,随后的 `StartSegment` 将关卡号记入 `CampaignRetryLevel` —— 但仅当它是可玩的战役关卡(`BuiltIn`、`0 <= number < levelCount`、非 Credits 尾声、不在 LC 地图包运行中;地图包关卡的重试由 R6.3 负责)。之后 `MenuEntryPending` 即被清除;该边沿只描述刚刚开始的那一关。

**持续性。** `CampaignRetryLevel` 在战役推进(不重新触发菜单边沿)、整局重置、乃至重试自身之间都保持不变 —— 它的含义是"玩家最近一次从菜单进入的关卡",在下一次菜单进入(换关卡开新局)改写它之前一直有效。`MenuEntryPending` 由 `RunState.Reset` 清除。

**重试行为。** 在 `RetryAction.TryExecute` 中,当 `CampaignRetryLevel >= 0` 时,重载以 `BuiltIn` 重新启动该关卡(提示 `NOTIFY_CAMPAIGN_RESTARTED`);否则 —— EditorPick、Workshop、或任何未被标记为菜单进入的运行 —— 原地重载当前关卡的行为与从前完全一致(提示 `NOTIFY_LEVEL_RESTARTED`)。两种情况的计时语义完全相同(见上文 R6.2.2)。

## 为什么分段终点在自动重置清零之前记录

本地 `PlayingLevel → Inactive` 这一转换*既是*分段终点(R1.4.1)*又是*自动重置触发(R1.7.3)。记录最后一段分段、执行 R4.2 终点检查点校验必须发生在整局清零之前。分段终点的**停表**是无条件的;但分段的**数值**(用时 / 总时 / 完成校验)**仅在真正完成关卡时才记录**(见下)。整局清零受 `AutoReset` 开关控制。见 `SegmentLogic` 与 `TimerCore.HandleTransitions`。

## 为什么分段数值仅在完成关卡时记录

关卡离开 `PlayingLevel` 不一定是完成 —— 可能是中途退出(`Esc → Exit → PauseLeave`),而且**Workshop/EditorPick 关卡的完成与中途退出走的是同一条 `PauseLeave` 路径**。因此仅凭状态转换无法区分二者。`RunState.LevelPassed` 就是这个信号:引擎每物理帧把 `Game.passedLevel` 锁存进 `LevelPassed`(取或,一旦进入通关区就保持为真 —— 游戏在完成/离开流程中、状态翻转*之前*就清掉了 `passedLevel`,所以必须在之前读到它)。`LevelPassed` 在分段开始时复位。

`EndSegment(completed: LevelPassed)` 只在 `completed` 为真时才记录 `LastSegment`/`TotalAtLastSegment`、tag 的 `OnLevelExit` 完成校验(R4.2、语音线)以及 LC 最后一关的 `LastRun` 捕获。中途退出(或重试,此时 `LevelPassed` 为假)不会改动上一段尝试的 `LastSegment`/`TotalAtLastSegment` —— 这正是想要的行为:"上一段"参照值反映的是上一关**打完**的成绩,而不是半途走出去的那一关。注意 `PlayingLevel → LoadingLevel`(内置关卡的 `StartNextLevel` 重载)本质上就是完成,且该处 `LevelPassed` 由更早的 `EnterPassZone` 置真。

## 为什么自动重置保留"上一段/上一局"快照

`LastSegment`、`TotalAtLastSegment`、`LastRun` 是 HUD 的参照值("上一段/上一局成绩对比")。它们**仅在新值记录时更新**(分段终点记录 `LastSegment`/`TotalAtLastSegment`;整局完成时记录 `LastRun`)—— 因此**自动重置(R1.7)不清零这三个值**,即便它清零实时计时器。`RunState.Reset(bool keepLastValues)` 体现这一点:自动重置路径(`HandleTransitions` → `DoFullReset(keepLastValues: true)`)保留这三个快照;**手动重置键**(`DoFullReset(keepLastValues: false)`)则清零它们,因为按重置键意味着"我要一个全新的对比基线"。(重试完全不碰 `Reset` —— 它走 `Retrying` 标志。)

## 什么算"整局完成"(LastRun)

`LastRun`("上一局"总时间)在整局**完成**的那一刻记录,而它必须区分三种互不相关的收尾方式 —— 仅凭游戏状态无法区分。规则在 `EndSegment` 且 `completed` 为真时执行,使用分段开始时的快照 / 每帧锁存(因为此时游戏 / LC 的状态早已切换到后续内容):

- **官方战役进 Credits**:BuiltIn 关卡序号等于 `levelCount - 1`(最后一个可玩关卡;游戏随后加载索引 `levelCount` 的 Credits)。
- **单独的 EditorPick**:在 LC 地图包运行**之外**完成的 EditorPick 关卡(`InCollectionRunSegment` 锁存为假)。
- **地图包完成**:LC 地图包的最后一关(`OnCollectionLastLevel` 锁存)。这一条必须锁存,因为 LC 在 `Game.Fall` 内同步结束运行,早于引擎 FixedUpdate 观察到状态翻转。

(单独的 Workshop 关卡通关不算"整局完成" —— 在计时器的语义里它不结束一局。)过去那个"任何分段开始时时钟还在跑就记录 LastRun"的启发式已移除:它会在战役中途的每个关卡边界误触发,而 EditorPick/Workshop 的收尾又永远捕不到;现在 `LastRun` 只在上述真正的完成时更新。

`LastRun` 渲染在**紧挨计时列右侧新建的独立列**中(以主块最宽行为界),不与主计时同列,且仅空闲时(`!InSegment && GameTime == 0`)显示 —— 新局开始计时即隐藏,直到下次整局完成。唯一的例外是战役尾声:最后一个可玩关卡被通关后,游戏会把 Credits(BuiltIn 索引 == `levelCount`)当作普通关卡加载,该分段被标记为 `InEpilogueSegment` —— 它属于刚结束的那局,所以 Credits 期间列持续显示(且 Credits 自身不会记录任何东西:它没有通关区,其分段永远不会算作 `completed`)。**地图包运行中被列为关卡的 Credits 不算尾声**(运行仍在进行 —— collection 中途出现 Credits 只是一关普通关卡),因此 `InEpilogueSegment` 还要求"当前不在地图包运行中"。

## 配置检查与修复

`ConfigRepair.Run(cfg)` 在 `Plugin.Awake` 中、`ConfigService.Load` 之后、任何子系统读取配置之前运行一次,检测并补全缺失或错误的配置项。标量类设置 / 布局键本就会自愈到默认值(解析辅助函数在失败时回退到当前值),因此规则针对的是那些"清空后仅从磁盘重建"的集合字段——新加入的默认项对老用户会静默丢失(这正是 `TotalAtLastSegment` 行在引入此系统前不显示的原因)。

设计:**每次启动做幂等的结构性检查,仅在确有改动时写盘**(由一个 dirty 标志门控的 `ConfigService.SaveSettings`)。不引入配置版本号——一旦用户手工编辑文件,存储的版本号就会失真;而廉价的结构性检查能自愈手工编辑造成的损坏,且对干净文件零改动。修复时输出一行汇总日志,干净启动时静默。

第一条规则 `RepairLayoutRows`:当用户的行集合看起来是"默认派生"时(`IsDefaultDerived`:行集合等于按默认顺序排列、扣除缺失项后的默认集),插入任何缺失的默认 HUD 行。被重排、含额外或重复行的集合视为手工自定义,原样保留并仅给出提示。规范的默认顺序只有一个出处——`LayoutModel.DefaultRows`,被 `Rows` 字段初始化器与修复目标共用,二者不会漂移;新增默认行只需在此改一行。新增一个修复关注点只需写一个方法并在 `ConfigRepair.Rules` 数组加一项。

## 构建

```bash
dotnet build src/HSRTimer/HSRTimer.csproj
```

`Directory.Build.props` 指向默认 Steam 安装目录下的托管 DLL 与 BepInEx core。其他平台用 `GAME_MANAGED` / `BEPINEX_CORE` 覆盖。
