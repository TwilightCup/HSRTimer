using System.Reflection;
using UnityEngine;

namespace HSRTimer
{
    /// <summary>
    /// When the <c>CenterLoadingSaving</c> setting is enabled, moves the game's
    /// own top-right "Loading"/"Saving" progress indicator (the
    /// <c>SubtitleManager.progressText</c> and <c>progressRing</c> pair) to the
    /// top-center of the screen. The manager can be recreated per scene, so this
    /// component polls in LateUpdate and captures the original local positions
    /// before moving them, restoring them when the setting is disabled.
    /// </summary>
    public class ProgressIndicatorMover : MonoBehaviour
    {
        private SubtitleManager _manager;
        private Transform _text;
        private Transform _ring;
        private Vector3 _textOriginalLocal;
        private Vector3 _ringOriginalLocal;
        private bool _captured;

        private void LateUpdate()
        {
            var cfg = ConfigService.Instance;
            if (cfg == null)
                return;

            var sm = SubtitleManager.instance;
            if (sm == null)
            {
                ResetCapture();
                return;
            }

            // If the scene was reloaded and the old UI was destroyed (or the
            // SubtitleManager instance was replaced), forget the captured
            // transforms so we can re-acquire the new indicator objects.
            if (_captured && (_manager != sm || _text == null))
                ResetCapture();

            if (!_captured && !TryCapture(sm))
                return;

            // The ring may be created slightly later than the text. Pick it up
            // without discarding the already-captured text position.
            if (_ring == null)
            {
                _ring = GetField<GameObject>(sm, "progressRing")?.transform;
                if (_ring != null)
                    _ringOriginalLocal = _ring.localPosition;
            }

            if (cfg.Settings.CenterLoadingSaving)
                Center();
            else
                Restore();
        }

        private void ResetCapture()
        {
            _captured = false;
            _manager = null;
            _text = null;
            _ring = null;
        }

        private bool TryCapture(SubtitleManager sm)
        {
            _text = GetField<Component>(sm, "progressText")?.transform;
            _ring = GetField<GameObject>(sm, "progressRing")?.transform;
            if (_text == null)
                return false;

            _manager = sm;
            _textOriginalLocal = _text.localPosition;
            if (_ring != null)
                _ringOriginalLocal = _ring.localPosition;
            _captured = true;
            return true;
        }

        private void Center()
        {
            // If the ring is unavailable, just center the text.
            if (_ring == null)
            {
                _text.localPosition = CenteredPosition(_text, _textOriginalLocal);
                return;
            }

            // If one indicator is parented under the other, only the ancestor
            // needs to move; the child follows automatically.
            if (_text == _ring.parent)
            {
                _text.localPosition = CenteredPosition(_text, _textOriginalLocal);
                return;
            }
            if (_ring == _text.parent)
            {
                _ring.localPosition = CenteredPosition(_ring, _ringOriginalLocal);
                return;
            }

            // Keep the two indicators in their original relative arrangement.
            // If they share a parent, shift the pair horizontally as a group so
            // their combined visual center lands on the parent's horizontal
            // center. Otherwise center each one independently.
            if (_text.parent == _ring.parent)
            {
                float delta = CenterDelta(_text, _ring, _textOriginalLocal, _ringOriginalLocal);
                _text.localPosition = _textOriginalLocal + new Vector3(delta, 0f, 0f);
                _ring.localPosition = _ringOriginalLocal + new Vector3(delta, 0f, 0f);
            }
            else
            {
                _text.localPosition = CenteredPosition(_text, _textOriginalLocal);
                _ring.localPosition = CenteredPosition(_ring, _ringOriginalLocal);
            }
        }

        private void Restore()
        {
            if (!_captured)
                return;
            if (_text != null)
                _text.localPosition = _textOriginalLocal;
            if (_ring != null)
                _ring.localPosition = _ringOriginalLocal;
        }

        /// <summary>Horizontal delta needed to move the visual center of the pair to the
        /// center of their shared parent, while keeping the vertical arrangement.</summary>
        private static float CenterDelta(Transform text, Transform ring,
            Vector3 textOriginal, Vector3 ringOriginal)
        {
            var parentRt = text.parent as RectTransform;
            if (parentRt == null)
                return 0f;

            float target = (0.5f - parentRt.pivot.x) * parentRt.rect.width;
            float current = (RectCenterX(text, textOriginal) + RectCenterX(ring, ringOriginal)) * 0.5f;
            return target - current;
        }

        private static Vector3 CenteredPosition(Transform tr, Vector3 original)
        {
            var parentRt = tr.parent as RectTransform;
            if (parentRt == null)
                return original;

            float target = (0.5f - parentRt.pivot.x) * parentRt.rect.width;
            float delta = target - RectCenterX(tr, original);
            return original + new Vector3(delta, 0f, 0f);
        }

        /// <summary>The transformative x position of a RectTransform's visual center in
        /// parent-local coordinates (accounts for pivot and width). The position
        /// is supplied separately so movement is always computed from the
        /// captured original layout, not from a possibly already-shifted value.</summary>
        private static float RectCenterX(Transform tr, Vector3 localPosition)
        {
            var rt = tr as RectTransform;
            if (rt == null)
                return localPosition.x;
            return localPosition.x + (0.5f - rt.pivot.x) * rt.rect.width;
        }

        private static T GetField<T>(object target, string name) where T : class
        {
            var field = target.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (field == null)
                return null;
            return field.GetValue(target) as T;
        }
    }
}
