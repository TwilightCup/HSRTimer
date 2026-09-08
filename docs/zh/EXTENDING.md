# 扩展 HSRTimer

> **English (source of truth)**: [../EXTENDING.md](../EXTENDING.md)

HSRTimer 为其它 BepInEx 插件提供两个扩展点:

- **自定义标签规则**(R3.7)—— 新的有效性逻辑,用户在设置面板中勾选该标签 id 即可启用(持久化到 `tags.ini`)。
- **设置面板标签页** —— 把使用 Unity IMGUI 编写的配置界面注册为 HSRTimer 设置面板中的额外标签页(每个插件最多一个);标签页可选择跟随 HSRTimer 的语言选择(R9.2),并可在 HSRTimer 的 `SettingsSaved` 事件中保存自身配置(R9.3)。

下面分别说明。

## 自定义标签规则

标签系统可扩展(R3.7)。任何 BepInEx 插件都可注册一个**自定义标签规则** —— 新的有效性逻辑,用户在设置面板中勾选该标签 id 即可启用(持久化到 `tags.ini`)。内置标签(`Checkpoint`、`NoCheckpoint`、`Jumpless`、`Voiceline`)本身就是以这种方式注册的规则,因此自定义规则与内置规则走完全相同的引擎路径。

### `ITagRule` 接口

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

### 最小示例:"禁止坠落"标签

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

### 注册规则

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

### 启用该标签

注册后,`NoFall` 会作为复选框出现在设置面板的"类别"页,与内置标签并列。用户只需勾选即可。也可直接在 `tags.ini` 中启用:

```ini
[tags]
enabled = NoFall
```

### 自定义无效原因

内置 `InvalidReason` 枚举覆盖标准原因。若需完全自定义原因,可复用内置原因(如 `CheatCode`),或另行建模自己的标记,并通过面板自定义文本机制呈现。(未来版本将提供通用原因注册表;v1 暂请复用内置项。)

## 注册设置面板标签页

任何使用 Unity IMGUI 构建配置界面的 BepInEx 插件,都可以把该界面注册为 HSRTimer 设置面板中的**新标签页**(R9)。每个插件**最多注册一个**标签页 —— 注册表以插件所属的 BepInEx GUID 为键,同一插件重复注册会被拒绝并记录日志。

### `ISettingsPanelTab` 接口

```csharp
public interface ISettingsPanelTab
{
    string Title { get; }   // 设置面板导航中显示的标签页标题
    void Draw();            // 在 HSRTimer 设置面板内部绘制 IMGUI 内容
}
```

### 最小示例

```csharp
using HSRTimer;
using UnityEngine;

public class MyConfigTab : ISettingsPanelTab
{
    private bool _someOption;

    public string Title => "My Plugin";

    public void Draw()
    {
        GUILayout.Label("Options for My Plugin");
        _someOption = GUILayout.Toggle(_someOption, "Enable something");
    }
}
```

### 注册标签页

在你的插件 `Awake` 中(HSRTimer 已加载之后 —— 声明依赖):

```csharp
[BepInDependency("HSRTimer")]
public class MyPlugin : BaseUnityPlugin
{
    private void Awake()
    {
        SettingsPanelTabRegistry.Instance.Register(this, new MyConfigTab());
    }
}
```

`Draw` 会在 HSRTimer 的设置窗口和滚动视图内执行,因此可以像在其它 Unity IMGUI 面板中一样使用 `GUILayout.*` / `GUI.*`。标签页标题可以返回随插件自身语言设置实时变化的本地化字符串。同一插件 GUID 的第二次注册会被拒绝并记录日志。若希望由 HSRTimer 的语言选择器驱动标签页,请见下一节。

## 外部标签页本地化

外部标签页可以通过实现 `ILocalizableSettingsPanelTab`(而不是 `ISettingsPanelTab`)接入 HSRTimer 的语言选择(R9.2)。HSRTimer 会在标签页注册时调用一次 `SetLanguage`,之后每次用户在 **常规 → 语言** 中切换语言时再次调用。标签页的 `Title` 与 `Draw` 每帧重新读取,因此无需重启游戏或重新打开面板即可立即切换。

```csharp
using System.Collections.Generic;

namespace HSRTimer
{
    /// <summary>
    /// ISettingsPanelTab 的可选扩展:让标签页跟随 HSRTimer 的语言选择。
    /// </summary>
    public interface ILocalizableSettingsPanelTab : ISettingsPanelTab
    {
        // 该标签页内置的所有 BCP 47 语言代码。必须包含 "en"(英文基准)。
        IEnumerable<string> SupportedLanguages { get; }

        // 注册时、以及 HSRTimer 每次切换语言时调用。
        // 若当前语言受支持则传入当前代码,否则传入 "en"。
        void SetLanguage(string languageCode);
    }
}
```

### 约定

- **必须内置英文。** `SupportedLanguages` 必须包含 `"en"`。若缺失,HSRTimer 会记录警告,并且对标签页不支持的当前语言仍然使用 `"en"` 作为兜底;因此无论标签页还提供哪些语言,英文基准都不可省略。
- **回退由 HSRTimer 自动完成。** HSRTimer 会把当前语言与 `SupportedLanguages` 比对:存在则原样传入;不存在则传入 `"en"`。标签页不会收到自己未声明的语言代码。
- **遵循 HSRTimer 的格式(R7)。** 外部标签页的翻译使用与 HSRTimer 相同的规则:每种语言一个 `<code>.txt` 文件(BCP 47)、`键名:译文` 行、`#` 注释、`__LANG_NAME__`、UTF-8(无 BOM),查找顺序为 **当前语言 → 英文 → 键名本身**。语言文件可放在插件自己的配置目录(例如 `<BepInEx 配置目录>/<你的插件GUID>/lang/`),也可作为 DLL 嵌入资源。
- **所有字符串都走同一个查找函数。** `Title` 与 `Draw` 绘制的每个标签都必须调用同一个取值函数,保证整个标签页同步切换。
- **HSRTimer 的语言列表仍是唯一入口。** 外部标签页不会向 HSRTimer 的语言下拉列表添加条目;它只跟随 HSRTimer 已选中的语言。

`LanguageFile.Parse(path, out displayName)` 与 `LanguageFile.ParseLines(lines, sourceName, out displayName)` 是公开方法,外部插件可以直接复用 HSRTimer 的容错解析器,而不必另写一套。

### 可本地化示例

```csharp
using System.Collections.Generic;
using HSRTimer;
using UnityEngine;

public class MyConfigTab : ILocalizableSettingsPanelTab
{
    // 真实插件应从自己的 lang/*.txt 或嵌入资源加载;English 必须始终存在。
    private static readonly Dictionary<string, Dictionary<string, string>> Texts =
        new Dictionary<string, Dictionary<string, string>>
        {
            ["en"] = new Dictionary<string, string>
            {
                ["TAB_MY_PLUGIN"] = "My Plugin",
                ["OPT_ENABLE"] = "Enable something",
            },
            ["zh-Hans"] = new Dictionary<string, string>
            {
                ["TAB_MY_PLUGIN"] = "我的插件",
                ["OPT_ENABLE"] = "启用某功能",
            },
        };

    private string _lang = "en";
    private bool _someOption;

    public IEnumerable<string> SupportedLanguages => Texts.Keys;

    public void SetLanguage(string languageCode)
    {
        // HSRTimer 通常会传入受支持的代码;这里仍保留兜底,便于直接调用。
        _lang = Texts.ContainsKey(languageCode) ? languageCode : "en";
    }

    public string Title => Get("TAB_MY_PLUGIN");

    public void Draw()
    {
        GUILayout.Label(Get("OPT_ENABLE"));
        _someOption = GUILayout.Toggle(_someOption, Get("OPT_ENABLE"));
    }

    private string Get(string key)
    {
        string value;
        if (Texts[_lang].TryGetValue(key, out value)) return value;
        if (Texts["en"].TryGetValue(key, out value)) return value;
        return key;
    }
}
```

注册方式不变;`SettingsPanelTabRegistry.Register` 可以接收该接口,因为它继承自 `ISettingsPanelTab`:

```csharp
[BepInDependency("HSRTimer")]
public class MyPlugin : BaseUnityPlugin
{
    private void Awake()
    {
        SettingsPanelTabRegistry.Instance.Register(this, new MyConfigTab());
    }
}
```

## 保存标签页自己的配置

外部标签页可能有独立于 HSRTimer `settings.ini`/`tags.ini`/`layout.ini` 的配置文件。若希望与 HSRTimer 在相同时机保存,请订阅 `SettingsPanelTabRegistry.SettingsSaved` 事件(R9.3):

```csharp
[BepInDependency("HSRTimer")]
public class MyPlugin : BaseUnityPlugin
{
    private void Awake()
    {
        SettingsPanelTabRegistry.Instance.SettingsSaved += SaveMyConfig;
        SettingsPanelTabRegistry.Instance.Register(this, new MyConfigTab());
    }

    private void OnDestroy()
    {
        // 注册表是进程级单例;插件卸载时取消订阅。
        SettingsPanelTabRegistry.Instance.SettingsSaved -= SaveMyConfig;
    }

    private void SaveMyConfig()
    {
        // 在此写入插件自己的配置。此时 HSRTimer 刚写完
        // settings.ini / tags.ini / layout.ini。
    }
}
```

约定:

- 事件在 HSRTimer 的 `ConfigService.SaveSettings()` 写完配置后触发,覆盖设置面板的**保存**与**关闭**按钮、用设置键关闭面板、退出游戏,以及 HSRTimer 内部的自动保存(例如重置成绩后)。
- 多个处理器依次执行。若某个处理器抛出异常,HSRTimer 会记录该异常并继续保存/通知其它插件;单个插件的错误不会阻断 HSRTimer 自身的持久化。
- 该事件仅用于持久化,不要在处理器里绘制 UI,也不要假设某个标签页当前可见。
- 若插件可能被卸载,请在 `OnDestroy` 中取消订阅,避免残留委托导致对象无法释放。

完整文件格式见 [LOCALIZATION.md](LOCALIZATION.md),内置标签见 [CATEGORIES.md](CATEGORIES.md),引擎生命周期见 [ARCHITECTURE.md](ARCHITECTURE.md)。
