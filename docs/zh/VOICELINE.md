# 旁白检测

> **English (source of truth)**: [../VOICELINE.md](../VOICELINE.md)

**Voiceline** 标签(R3.6)要求玩家触发关卡内的全部剧情旁白。本页说明 HSRTimer 如何检测合规 —— 这是最微妙的标签,因为常见的跳过技巧会留下可检测的痕迹。

## 游戏侧行为

关卡内的旁白是放置在场景中的 `NarrativeBlock` 组件。玩家进入某 block 的触发器并停留超过其 `triggerDelay` 后,该 block 调用 `NarrativeBlock.Play()`,显示字幕并通过 `SubtitleManager.PlayNarrative(AudioClip)` 播放一段语音。播放后,block 设置其私有 `wasPlayed` 标志且不再重播。

另有一个特殊的剧情音频源,约定名为 **"Easter"**,播放某段特定剧情音频。

## HSRTimer 如何追踪(`VoicelineTracker`)

1. **进入关卡时** —— 扫描场景:
   - `Object.FindObjectsOfType<NarrativeBlock>()` 记录每个 block 的实例 id 为"待触发"(必须触发)。
   - 搜索名为 **"Easter"**(大小写不敏感)的 `AudioSource`。若存在,本局进入**可疑**状态(`satisfied = false`):Easter 音频源存在但尚未播放。

2. **游玩期间** —— 两个 Harmony 后缀馈入追踪器:
   - `NarrativeBlock.Play()` 后缀 → 标记该 block 为"已触发"(从待触发移除)。幂等(按实例 id 去重)。
   - `SubtitleManager.PlayNarrative(AudioClip)` 后缀 → 若该 clip 为 Easter clip,标记 Easter 为**已播放**,并把本局翻回 satisfied。

3. **离开关卡时** —— 以下任一情况本局**无效**(原因 `Voiceline`):
   - Easter 音频源存在但从未播放,或
   - 任一 `NarrativeBlock` 从未被触发。

全部旁白达成时,面板显示绿色"全部旁白已触发"提示(R3.6.3)。

## 为什么用两路信号

常见的旁白跳过技巧会**让 Easter AudioSource 留在场景中但从不播放**。仅检查"每个 NarrativeBlock 是否触发"会漏掉这一点,因为该技巧绕过了正常的触发路径。通过将"Easter 存在"视为可疑,直到观察到该 clip 的实际 `PlayNarrative` 调用,该检测可识破此技巧(R5.5)。

`NarrativeBlock.Play` 后缀是逐 block 的主信号;`SubtitleManager.PlayNarrative` 后缀是 Easter clip 的鲁棒信号(它针对实际音频播放触发,而跳过技巧恰好回避了播放)。

## 注意事项

- "Easter" 名称是场景/检查器约定(反编译代码中无字面量)。检测按 GameObject 名大小写不敏感匹配;若自定义关卡用不同名称,Voiceline 标签可能误判。在游戏内对自定义关卡复核后再视该标签为权威。
- 所有检测均有包裹,任何失败会记录警告而非崩溃游戏(N7)。
- 追踪器按关卡重置,不跨关卡携带状态。
