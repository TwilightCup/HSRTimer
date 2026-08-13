# 扩展 HSRTimer(自定义标签)

> **English (source of truth)**: [../EXTENDING.md](../EXTENDING.md)

HSRTimer 的标签系统可扩展(R3.7)。任何 BepInEx 插件都可注册一个**自定义标签规则** —— 新的有效性逻辑,用户在设置面板中勾选该标签 id 即可启用(持久化到 `tags.ini`)。内置标签(`Checkpoint`、`NoCheckpoint`、`Jumpless`、`Voiceline`)本身就是以这种方式注册的规则,因此自定义规则与内置规则走完全相同的引擎路径。

## `ITagRule` 接口

```csharp
public interface ITagRule
{
    string Id { get; }                  // 稳定的标签 id,如 "NoFall"
    string DisplayNameKey { get; }      // 本地化键(可选)

    void OnLevelEnter(ValidationContext ctx);  // 关卡开始时一次
    void OnTick(ValidationContext ctx);        // 游戏中每物理帧
    void OnLevelExit(ValidationContext ctx);   // 关卡结束时一次
}
```

`ValidationContext` 携带规则所需的一切:`RunState`、当前 `Game`、当前与上一检查点编号、`ValidityFlags`(在此打无效标记)以及 `LocalizationService`。

引擎仅在**该标签 id 被启用**(在设置面板"类别"页勾选)时才会调用你的规则。

## 最小示例:"禁止坠落"标签

```csharp
using HSRTimer;
using UnityEngine;

public class NoFallRule : ITagRule
{
    public string Id => "NoFall";
    public string DisplayNameKey => "TAG_NO_FALL";

    private bool _fell;

    public void OnLevelEnter(ValidationContext ctx) => _fell = false;

    public void OnTick(ValidationContext ctx)
    {
        // (示意)本地玩家处于下落态时标记。
        var me = Human.Localplayer;
        if (me != null && (me.state == HumanState.Fall || me.state == HumanState.FreeFall))
            _fell = true;
    }

    public void OnLevelExit(ValidationContext ctx)
    {
        if (_fell)
            ctx.Flags.Raise((InvalidReason)100); // 自定义原因,或复用内置原因
    }
}
```

## 注册规则

在你的插件 `Awake` 中(HSRTimer 已加载之后 —— 声明依赖):

```csharp
[BepInDependency("HSRTimer")]
public class MyPlugin : BaseUnityPlugin
{
    private void Awake()
    {
        TagRuleRegistry.Instance.Register(new NoFallRule());
    }
}
```

重复的 id 会被拒绝(记录日志并忽略),以避免重复判罚。

## 启用该标签

注册后,`NoFall` 会作为复选框出现在设置面板的"类别"页,与内置标签并列。用户只需勾选即可。也可直接在 `tags.ini` 中启用:

```ini
[tags]
enabled = NoFall
```

## 自定义无效原因

内置 `InvalidReason` 枚举覆盖标准原因。若需完全自定义原因,可复用内置原因(如 `Drift`),或另行建模自己的标记,并通过面板自定义文本机制呈现。(未来版本将提供通用原因注册表;v1 暂请复用内置项。)

内置标签见 [CATEGORIES.md](CATEGORIES.md),引擎生命周期见 [ARCHITECTURE.md](ARCHITECTURE.md)。
