using System.Collections.Generic;
using UnityEngine;

namespace HSRTimer
{
    /// <summary>
    /// An IMGUI settings panel, organized into four tabbed pages (General,
    /// Interface, Category, Subsegment). Edits every user-tunable option and
    /// applies it live (the HUD/engine read from the shared models each frame,
    /// so changes take effect immediately). Changes are written to disk when the
    /// panel is closed or the game exits. Toggled by the configurable Menu key
    /// (default Home). Editing a keybind is done by focusing its field and
    /// pressing the desired key.
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        public static SettingsPanel Instance { get; private set; }

        private bool _visible;

        /// <summary>Whether the panel is currently shown on screen.</summary>
        public bool IsVisible => _visible;
        private Rect _rect = new Rect(60f, 60f, 460f, 580f);

        // Styles. _toggle (from GUI.skin.toggle) and _button (from GUI.skin.button)
        // are critical: passing a label-derived style to Toggle/SelectionGrid
        // makes the checkbox / radio indicator disappear, because the indicator
        // glyph is the style's background and label has none.
        private GUIStyle _label, _value, _section, _small, _toggle, _button, _textField;
        private Font _font;
        private bool _stylesReady;
        private Vector2 _scroll;

        // Active tab page.
        private int _tab;
        private string[] _tabDisplays;
        private static readonly string[] _tabKeys = { "PANEL_TAB_GENERAL", "PANEL_TAB_INTERFACE", "PANEL_TAB_CATEGORY", "PANEL_TAB_SUBSEGMENT" };

        // Keybind rebind state: which logical action is awaiting a keypress.
        private string _pendingRebind;

        // Per-color hex text buffers, keyed by an identity string ("A"/"B").
        // IMGUI text fields are stateless — we own the string each frame and
        // reconcile it with the live color so typing hex and dragging the
        // RGB sliders stay in sync both ways.
        private readonly Dictionary<string, string> _colorHexBuf = new Dictionary<string, string>();

        // Transient language/code lists.
        private string[] _langCodes;
        private string[] _langDisplays;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            // Game exit / plugin unload: persist any unsaved edits.
            if (ConfigService.Instance != null)
                ConfigService.Instance.SaveSettings();
        }

        private void OnApplicationQuit()
        {
            if (ConfigService.Instance != null)
                ConfigService.Instance.SaveSettings();
        }

        public void Toggle()
        {
            _visible = !_visible;
            if (!_visible && ConfigService.Instance != null)
                ConfigService.Instance.SaveSettings();
            if (_visible)
            {
                RefreshLanguageList();
                RefreshTabDisplays();
            }
        }

        private void EnsureStyles()
        {
            if (_stylesReady) return;
            try
            {
                _font = Font.CreateDynamicFontFromOSFont(new[]
                {
                    "PingFang SC", "Microsoft YaHei", "Noto Sans CJK SC",
                    "Noto Sans CJK", "Heiti SC", "Arial Unicode MS", "Arial",
                }, 14);
            }
            catch { _font = null; }
            _label = new GUIStyle(GUI.skin.label) { font = _font, fontSize = 13, wordWrap = false };
            _value = new GUIStyle(GUI.skin.label) { font = _font, fontSize = 13 };
            _section = new GUIStyle(GUI.skin.label) { font = _font, fontSize = 14, fontStyle = FontStyle.Bold };
            _small = new GUIStyle(GUI.skin.label) { font = _font, fontSize = 11, wordWrap = true };
            // Toggle style carries the checkbox glyph; SelectionGrid/Button carry the
            // button (radio/selected) background. All get the CJK-capable font.
            _toggle = new GUIStyle(GUI.skin.toggle) { font = _font, fontSize = 13, wordWrap = false };
            _button = new GUIStyle(GUI.skin.button) { font = _font, fontSize = 13, wordWrap = false };
            _textField = new GUIStyle(GUI.skin.textField) { font = _font, fontSize = 13 };
            _stylesReady = true;
        }

        private void OnGUI()
        {
            if (!_visible) return;
            EnsureStyles();
            _rect = GUI.Window(GetInstanceID(), _rect, Draw, "HSRTimer");
        }

        private void Draw(int id)
        {
            var cfg = ConfigService.Instance;
            if (cfg == null) { return; }
            var s = cfg.Settings;
            var loc = cfg.Localization;

            // Capture a keypress for an in-progress rebind before any widget
            // consumes the event.
            if (_pendingRebind != null && Event.current.type == EventType.KeyDown)
            {
                KeyCode pressed = Event.current.keyCode;
                // Ignore pure modifier presses so the user can press e.g. "H".
                if (pressed != KeyCode.LeftShift && pressed != KeyCode.RightShift
                    && pressed != KeyCode.LeftControl && pressed != KeyCode.RightControl
                    && pressed != KeyCode.LeftAlt && pressed != KeyCode.RightAlt
                    && pressed != KeyCode.LeftCommand && pressed != KeyCode.RightCommand)
                {
                    ApplyRebind(_pendingRebind, pressed);
                    _pendingRebind = null;
                    Event.current.Use();
                }
            }

            // Tab bar (kept outside the scroll view).
            RefreshTabDisplays();
            _tab = GUILayout.Toolbar(_tab, _tabDisplays, _button);

            _scroll = GUILayout.BeginScrollView(_scroll);

            switch (_tab)
            {
                case 0: DrawGeneral(cfg, s, loc); break;
                case 1: DrawInterface(cfg, loc); break;
                case 2: DrawCategory(cfg, loc); break;
                case 3: DrawSubsegment(cfg, s, loc); break;
            }

            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(loc.Get("PANEL_SAVE"), _button))
                cfg.SaveSettings();
            if (GUILayout.Button(loc.Get("PANEL_CLOSE"), _button))
            {
                cfg.SaveSettings();
                _visible = false;
            }
            GUILayout.EndHorizontal();

            GUILayout.Label(loc.Get("PANEL_FOOTER"), _small);

            GUILayout.EndScrollView();

            GUI.DragWindow(new Rect(0, 0, _rect.width, 20));
        }

        // ── Page: General (timing toggles, language, keybinds) ──
        private void DrawGeneral(ConfigService cfg, SettingsModel s, LocalizationService loc)
        {
            Section(loc.Get("PANEL_TIMING"));
            s.AutoReset = Toggle(loc.Get("SETTINGS_AUTO_RESET"), s.AutoReset);
            s.RestartClearsForgivable = Toggle(loc.Get("SETTINGS_RESTART_CLEARS_FORGIVABLE"), s.RestartClearsForgivable);
            // Free-form input (clamped ≥0 on apply); the slider's 5s cap was
            // artificial — RetryAction only needs Mathf.Max(0, dwell).
            s.RetryMinDwell = Mathf.Max(0f, FloatFieldRow(loc.Get("SETTINGS_RETRY_MIN_DWELL"), s.RetryMinDwell, "0.###"));

            Section(loc.Get("SETTINGS_LANGUAGE"));
            DrawLanguageSelector(cfg, loc);
            if (GUILayout.Button(loc.Get("PANEL_RELOAD_LANGUAGE"), _button))
            {
                cfg.ReloadLanguage();
                RefreshLanguageList();
                RefreshTabDisplays();
            }

            Section(loc.Get("PANEL_KEYBINDS"));
            KeybindRow(loc, "SETTINGS_RESET_KEY", () => s.ResetKey, k => s.ResetKey = k);
            KeybindRow(loc, "SETTINGS_RETRY_KEY", () => s.RetryKey, k => s.RetryKey = k);
            KeybindRow(loc, "SETTINGS_MENU_KEY", () => s.MenuKey, k => s.MenuKey = k);
        }

        // ── Page: Interface (HUD appearance) ──
        private void DrawInterface(ConfigService cfg, LocalizationService loc)
        {
            // (No Validity section: cheat/speed/drift detection is always on with
            //  hardcoded thresholds — intentionally not user-configurable.)
            Section(loc.Get("PANEL_HUD"));
            cfg.Settings.ShowHud = Toggle(loc.Get("SETTINGS_SHOW_HUD"), cfg.Settings.ShowHud);
            cfg.Settings.ShowRealTime = Toggle(loc.Get("SETTINGS_SHOW_REAL_TIME"), cfg.Settings.ShowRealTime);
            cfg.Settings.CenterLoadingSaving = Toggle(loc.Get("SETTINGS_CENTER_LOADING_SAVING"), cfg.Settings.CenterLoadingSaving);
            cfg.Layout.OffsetX = FloatFieldRow(loc.Get("PANEL_OFFSET_X"), cfg.Layout.OffsetX);
            cfg.Layout.OffsetY = FloatFieldRow(loc.Get("PANEL_OFFSET_Y"), cfg.Layout.OffsetY);
            cfg.Layout.FontSize = Mathf.RoundToInt(SliderRow(loc.Get("PANEL_FONT_SIZE"), cfg.Layout.FontSize, 8, 72));
            ColorRow(loc, "PANEL_COLOR_A", cfg.Layout.ColorA, c => cfg.Layout.ColorA = c);
            ColorRow(loc, "PANEL_COLOR_B", cfg.Layout.ColorB, c => cfg.Layout.ColorB = c);
        }

        // ── Page: Category (tag multi-select — no presets) ──
        private void DrawCategory(ConfigService cfg, LocalizationService loc)
        {
            Section(loc.Get("PANEL_TAGS"));
            DrawTagMultiSelect(cfg, loc);
        }

        // ── Page: Subsegment (R8) ──
        private void DrawSubsegment(ConfigService cfg, SettingsModel s, LocalizationService loc)
        {
            Section(loc.Get("PANEL_SUBSEGMENT"));
            s.SubsegmentEnable = Toggle(loc.Get("SETTINGS_SUBSEGMENT_ENABLE"), s.SubsegmentEnable);
            s.SubsegmentDebugLogging = Toggle(loc.Get("SETTINGS_SUBSEGMENT_DEBUG_LOGGING"), s.SubsegmentDebugLogging);

            s.SubsegmentPBPath = TextFieldRow(loc.Get("SETTINGS_SUBSEGMENT_PB_PATH"), s.SubsegmentPBPath);
            s.SubsegmentLoadPath = TextFieldRow(loc.Get("SETTINGS_SUBSEGMENT_LOAD_PATH"), s.SubsegmentLoadPath);

            Section(loc.Get("PANEL_KEYBINDS"));
            KeybindRow(loc, "SETTINGS_SUBSEGMENT_TOGGLE_KEY", () => s.SubsegmentToggleKey, k => s.SubsegmentToggleKey = k);

            Section(loc.Get("SETTINGS_SUBSEGMENT_MULTI_PROJECT"));
            string[] projects = { "Aztec%", "Dark%", "Steam%", "Any%" };
            int idx = System.Array.IndexOf(projects, s.SubsegmentMultiProject);
            if (idx < 0) idx = 3;
            int next = GUILayout.SelectionGrid(idx, projects, 2, _button);
            if (next != idx) s.SubsegmentMultiProject = projects[next];

            s.SubsegmentPlaneRadius = Mathf.Max(0f, FloatFieldRow(loc.Get("SETTINGS_SUBSEGMENT_PLANE_RADIUS"), s.SubsegmentPlaneRadius, "0.###"));
            s.SubsegmentMinMove = Mathf.Max(0f, FloatFieldRow(loc.Get("SETTINGS_SUBSEGMENT_MIN_MOVE"), s.SubsegmentMinMove, "0.###"));
            s.SubsegmentSampleInterval = Mathf.Max(0.01f, FloatFieldRow(loc.Get("SETTINGS_SUBSEGMENT_SAMPLE_INTERVAL"), s.SubsegmentSampleInterval, "0.###"));
            s.SubsegmentQuietSettleSeconds = Mathf.Max(0f, FloatFieldRow(loc.Get("SETTINGS_SUBSEGMENT_QUIET_SETTLE_SECONDS"), s.SubsegmentQuietSettleSeconds, "0.###"));
            s.SubsegmentPlaneDebounceSeconds = Mathf.Max(0f, FloatFieldRow(loc.Get("SETTINGS_SUBSEGMENT_PLANE_DEBOUNCE_SECONDS"), s.SubsegmentPlaneDebounceSeconds, "0.###"));
            s.SubsegmentRespawnJumpMeters = Mathf.Max(0f, FloatFieldRow(loc.Get("SETTINGS_SUBSEGMENT_RESPAWN_JUMP_METERS"), s.SubsegmentRespawnJumpMeters, "0.###"));
            s.SubsegmentMaxLeaderboardEntries = Mathf.Max(1, Mathf.RoundToInt(FloatFieldRow(loc.Get("SETTINGS_SUBSEGMENT_MAX_LEADERBOARD_ENTRIES"), s.SubsegmentMaxLeaderboardEntries, "F0")));

            Section(loc.Get("PANEL_HUD"));
            s.SubsegmentHudFontSize = Mathf.Clamp(Mathf.RoundToInt(SliderRow(loc.Get("SETTINGS_SUBSEGMENT_HUD_FONT_SIZE"), s.SubsegmentHudFontSize, 8, 72)), 8, 72);
            s.SubsegmentHudOffsetX = FloatFieldRow(loc.Get("SETTINGS_SUBSEGMENT_HUD_OFFSET_X"), s.SubsegmentHudOffsetX, "0.##");
            s.SubsegmentHudOffsetY = FloatFieldRow(loc.Get("SETTINGS_SUBSEGMENT_HUD_OFFSET_Y"), s.SubsegmentHudOffsetY, "0.##");
        }

        // ── widgets ──

        private void Section(string title)
        {
            GUILayout.Space(6);
            GUILayout.Label(title, _section);
        }

        // Uses the _toggle style so the checkbox glyph renders.
        private bool Toggle(string label, bool value)
        {
            return GUILayout.Toggle(value, label, _toggle);
        }

        private float SliderRow(string label, float value, float min, float max)
        {
            GUILayout.Label(label + ": " + value.ToString("0.##"), _value);
            return GUILayout.HorizontalSlider(value, min, max);
        }

        private float FloatFieldRow(string label, float value, string format = "F0")
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label + ":", _label, GUILayout.Width(110));
            string newText = GUILayout.TextField(value.ToString(format), _textField, GUILayout.Width(70));
            GUILayout.EndHorizontal();
            float parsed;
            if (float.TryParse(newText, out parsed)) return parsed;
            return value;
        }

        private string TextFieldRow(string label, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label + ":", _label, GUILayout.Width(110));
            string newText = GUILayout.TextField(value, _textField, GUILayout.Width(220));
            GUILayout.EndHorizontal();
            return newText;
        }

        private void ColorRow(LocalizationService loc, string key, Color c, System.Action<Color> set)
        {
            // Two editors, one color:
            //  - Sliders are the source of truth for the box: whenever a slider
            //    moves we set() the color AND overwrite the buffer, so the box
            //    always reflects the current color (even if it held garbage).
            //  - The box is the source of truth only while the user is typing
            //    into it: it can hold a half-typed/invalid string, and we set()
            //    the color only once it parses.
            // Because each branch keys off "did THIS control change", they can't
            // fight: a slider move drives the box, typing drives the sliders.

            // ── Hex input ──
            GUILayout.BeginHorizontal();
            GUILayout.Label(loc.Get(key), _label, GUILayout.Width(110));
            GUILayout.Label(loc.Get("PANEL_COLOR_HEX"), _label, GUILayout.Width(34));

            if (!_colorHexBuf.TryGetValue(key, out string buf))
                buf = GradientText.ToHex(c);
            string newText = GUILayout.TextField(buf, _textField, GUILayout.Width(90));
            _colorHexBuf[key] = newText;
            // Typing drives the color only when the text parses.
            if (newText != buf && GradientText.TryParseColor(newText, out Color fromText))
                set(fromText);
            GUILayout.EndHorizontal();

            // ── RGB sliders ──
            GUILayout.BeginHorizontal();
            float r = LabeledSlider("R", c.r); GUILayout.Space(4);
            float g = LabeledSlider("G", c.g); GUILayout.Space(4);
            float b = LabeledSlider("B", c.b); GUILayout.Space(4);
            float a = LabeledSlider("A", c.a);
            GUILayout.EndHorizontal();
            // A slider move is the authority: push the color into the box too.
            if (r != c.r || g != c.g || b != c.b || a != c.a)
            {
                Color next = new Color(r, g, b, a);
                set(next);
                _colorHexBuf[key] = GradientText.ToHex(next);
            }
        }

        private float LabeledSlider(string label, float value)
        {
            GUILayout.Label(label, GUILayout.Width(14));
            return GUILayout.HorizontalSlider(value, 0f, 1f, GUILayout.Width(70));
        }

        private void KeybindRow(LocalizationService loc, string key,
            System.Func<KeyCode> get, System.Action<KeyCode> set)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(loc.Get(key), _label, GUILayout.Width(200));
            bool pending = _pendingRebind == key;
            string btn = pending ? loc.Get("PANEL_PRESS_KEY") : get().ToString();
            if (GUILayout.Button(btn, _button, GUILayout.Width(140)))
                _pendingRebind = pending ? null : key;
            GUILayout.EndHorizontal();
        }

        private void ApplyRebind(string key, KeyCode pressed)
        {
            var s = ConfigService.Instance.Settings;
            if (key == "SETTINGS_RESET_KEY") s.ResetKey = pressed;
            else if (key == "SETTINGS_RETRY_KEY") s.RetryKey = pressed;
            else if (key == "SETTINGS_MENU_KEY") s.MenuKey = pressed;
            else if (key == "SETTINGS_SUBSEGMENT_TOGGLE_KEY") s.SubsegmentToggleKey = pressed;
        }

        // Multi-select of rule tags. There are no category presets — every
        // registered tag rule (built-in + any custom) is listed as a checkbox;
        // toggling adds/removes the tag from the enabled set, live.
        private void DrawTagMultiSelect(ConfigService cfg, LocalizationService loc)
        {
            var tags = cfg.EnabledTags;
            if (tags == null || TagRuleRegistry.Instance == null)
            {
                GUILayout.Label(loc.Get("PANEL_NO_TAGS"), _small);
                return;
            }

            foreach (var rule in TagRuleRegistry.Instance.All)
            {
                bool on = tags.HasTag(rule.Id);
                string display = string.IsNullOrEmpty(rule.DisplayNameKey)
                    ? rule.Id : loc.Get(rule.DisplayNameKey);
                bool next = GUILayout.Toggle(on, display + "  [" + rule.Id + "]", _toggle);
                if (next != on)
                {
                    if (next) tags.Enable(rule.Id);
                    else tags.Disable(rule.Id);
                }
            }
        }

        // Single-select language picker (SelectionGrid with the _button style so
        // the selected language is visually indicated).
        private void DrawLanguageSelector(ConfigService cfg, LocalizationService loc)
        {
            if (_langCodes == null) RefreshLanguageList();
            int current = System.Array.IndexOf(_langCodes, cfg.Localization.CurrentCode);
            if (current < 0) current = 0;
            int next = GUILayout.SelectionGrid(current, _langDisplays, 1, _button);
            if (next != current && _langCodes != null && next >= 0 && next < _langCodes.Length)
            {
                cfg.Localization.SetLanguage(_langCodes[next]);
                cfg.Settings.CurrentLang = cfg.Localization.CurrentCode;
            }
        }

        private void RefreshLanguageList()
        {
            var cfg = ConfigService.Instance;
            if (cfg == null) return;
            var codes = new List<string>();
            var displays = new List<string>();
            foreach (var lang in cfg.Localization.Languages)
            {
                codes.Add(lang.Code);
                displays.Add((lang.DisplayName ?? lang.Code) + "  [" + lang.Code + "]");
            }
            _langCodes = codes.ToArray();
            _langDisplays = displays.ToArray();
        }

        private void RefreshTabDisplays()
        {
            var cfg = ConfigService.Instance;
            if (cfg == null) return;
            if (_tabDisplays == null || _tabDisplays.Length != _tabKeys.Length)
                _tabDisplays = new string[_tabKeys.Length];
            for (int i = 0; i < _tabKeys.Length; i++)
                _tabDisplays[i] = cfg.Localization.Get(_tabKeys[i]);
        }
    }
}
