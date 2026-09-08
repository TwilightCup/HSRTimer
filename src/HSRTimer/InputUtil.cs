using UnityEngine;

namespace HSRTimer
{
    /// <summary>
    /// Keyboard/mouse input helpers shared by the settings panel and the
    /// gameplay keybind handlers. Unity's <see cref="UnityEngine.KeyCode"/> enum
    /// models mouse buttons as Mouse0–Mouse6, but IMGUI reports them as
    /// <see cref="UnityEngine.EventType.MouseDown"/> events with a numeric
    /// <c>button</c> rather than a <c>keyCode</c>.
    /// </summary>
    internal static class InputUtil
    {
        public static bool IsMouseKeyCode(KeyCode key)
        {
            int value = (int)key;
            return value >= (int)KeyCode.Mouse0 && value <= (int)KeyCode.Mouse6;
        }

        /// <summary>
        /// Return the KeyCode for an IMGUI mouse-button index, or
        /// <see cref="KeyCode.None"/> when the button is outside the mouse range.
        /// </summary>
        public static KeyCode MouseKeyCodeForButton(int button)
        {
            if (button < 0 || button > (int)KeyCode.Mouse6 - (int)KeyCode.Mouse0)
                return KeyCode.None;
            return (KeyCode)((int)KeyCode.Mouse0 + button);
        }

        /// <summary>
        /// Which mouse buttons may be bound. Left (0) and right (1) stay
        /// reserved for normal UI/gameplay use; side buttons (3–6) are
        /// allowed. The middle/wheel button is intentionally not treated as a
        /// side button here.
        /// </summary>
        public static bool IsBindableMouseButton(int button)
        {
            return button >= 3 && button <= (int)KeyCode.Mouse6 - (int)KeyCode.Mouse0;
        }

        /// <summary>
        /// Works for both keyboard KeyCodes and the Mouse0–Mouse6 KeyCode values.
        /// Unity's <see cref="UnityEngine.Input.GetKeyDown"/> is not documented
        /// to handle the mouse KeyCode range, so route those through the mouse
        /// button API explicitly.
        /// </summary>
        public static bool GetKeyDown(KeyCode key)
        {
            if (IsMouseKeyCode(key))
                return Input.GetMouseButtonDown((int)key - (int)KeyCode.Mouse0);
            return Input.GetKeyDown(key);
        }
    }
}
