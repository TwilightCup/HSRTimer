using System.Collections.Generic;
using Multiplayer;
using UnityEngine;

namespace HSRTimer
{
    /// <summary>
    /// Edit-mode visualization for markers (R10.6). While the marker edit mode is
    /// on and a level is playing:
    /// <list type="bullet">
    /// <item>every enabled Range marker is drawn as a blue translucent cube with
    /// a white name label at its center (R10.6.1);</item>
    /// <item>every enabled GrabObject marker highlights its resolved target
    /// object's bounds with the same cube + label (R10.6.2).</item>
    /// </list>
    /// No colliders are created and no game materials are modified. The cube is
    /// rendered with <c>Graphics.DrawMesh</c> (depth-correct, camera-agnostic);
    /// if no usable transparent shader is found the overlay degrades once to an
    /// IMGUI wireframe projection so the feature still works (R10.6 degradation).
    /// </summary>
    public sealed class MarkerOverlay : MonoBehaviour
    {
        private Mesh _cubeMesh;
        private Material _cubeMaterial;
        private bool _shaderLookupDone;
        private bool _useWireframeFallback;

        private GUIStyle _labelStyle;
        private Font _labelFont;
        private int _labelFontSize = -1;

        // Per-level one-time warnings for unresolvable grab-object markers.
        private string _warnedLevelKey;
        private readonly HashSet<string> _warnedIds = new HashSet<string>();

        private void Awake()
        {
            _labelStyle = new GUIStyle
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white },
            };
        }

        private void OnDestroy()
        {
            if (_cubeMesh != null) Object.Destroy(_cubeMesh);
            if (_cubeMaterial != null) Object.Destroy(_cubeMaterial);
        }

        private void EnsureResources()
        {
            if (_cubeMesh != null) return;
            _cubeMesh = BuildUnitCube();
        }

        private void EnsureMaterial()
        {
            if (_shaderLookupDone) return;
            _shaderLookupDone = true;
            Color fill = Color.white;
            var cfg = ConfigService.Instance;
            if (cfg != null && cfg.Settings != null)
                fill = cfg.Settings.MarkersOverlayFillColor;
            // Sprites/Default (or UI/Default) is a transparent unlit shader that
            // is virtually always present in a shipped game; Unlit/Color as a
            // last resort. "Hidden/Internal-Colored" is deliberately not used:
            // it tints only via vertex colors, which this mesh does not set.
            foreach (var shaderName in new[] { "Sprites/Default", "UI/Default", "Unlit/Color" })
            {
                Shader shader;
                try { shader = Shader.Find(shaderName); } catch { shader = null; }
                if (shader == null) continue;
                try
                {
                    _cubeMaterial = new Material(shader) { color = fill };
                    return;
                }
                catch (System.Exception ex)
                {
                    Plugin.Logger.LogWarning($"HSRTimer[markers]: material creation with '{shaderName}' failed: {ex.Message}");
                }
            }
            Plugin.Logger.LogWarning("HSRTimer[markers]: no usable transparent shader found; marker overlay falls back to IMGUI wireframe.");
            _useWireframeFallback = true;
        }

        private void LateUpdate()
        {
            if (!ShouldDraw())
                return;
            EnsureResources();
            EnsureMaterial();
            if (_cubeMaterial == null || _cubeMesh == null)
                return; // wireframe fallback handles drawing in OnGUI

            var mgr = MarkersManager.Instance;
            var set = mgr != null ? mgr.CurrentSet : null;
            if (set == null || set.markers == null)
                return;

            Color fill = _cubeMaterial.color;
            foreach (var def in set.markers)
            {
                if (def == null || !def.enabled || !MarkerKindUtil.IsKnown(def.type))
                    continue;
                Vector3 center;
                Vector3 size;
                if (!TryGetBox(def, mgr, out center, out size))
                    continue;
                if (size.x <= 0f || size.y <= 0f || size.z <= 0f)
                    continue;
                Graphics.DrawMesh(_cubeMesh, Matrix4x4.TRS(center, Quaternion.identity, size), _cubeMaterial, 0);
            }
        }

        private void OnGUI()
        {
            if (!ShouldDraw())
                return;
            var mgr = MarkersManager.Instance;
            var set = mgr != null ? mgr.CurrentSet : null;
            if (set == null || set.markers == null)
                return;

            var cam = GetCamera();
            if (cam == null)
                return;

            var cfg = ConfigService.Instance;
            Color labelColor = cfg != null && cfg.Settings != null ? cfg.Settings.MarkersOverlayLabelColor : Color.white;
            int fontSize = 14;
            EnsureLabelFont(fontSize);
            _labelStyle.fontSize = fontSize;
            _labelStyle.normal.textColor = labelColor;

            foreach (var def in set.markers)
            {
                if (def == null || !def.enabled || !MarkerKindUtil.IsKnown(def.type))
                    continue;
                Vector3 center;
                Vector3 size;
                if (!TryGetBox(def, mgr, out center, out size))
                    continue;

                var sp = cam.WorldToScreenPoint(center);
                if (sp.z <= 0f)
                    continue;
                float sx = sp.x;
                float sy = Screen.height - sp.y;

                if (_useWireframeFallback)
                    DrawWireframe(cam, center, size);

                // Only draw the label when its anchor is on screen.
                if (sx >= -20f && sx <= Screen.width + 20f && sy >= -20f && sy <= Screen.height + 20f)
                {
                    string label = string.IsNullOrEmpty(def.name) ? def.id : def.name;
                    var content = new GUIContent(label);
                    var labelSize = _labelStyle.CalcSize(content);
                    GUI.Label(new Rect(sx - labelSize.x * 0.5f, sy - labelSize.y * 0.5f, labelSize.x, labelSize.y), content, _labelStyle);
                }
            }
        }

        /// <summary>Whether the overlay should draw at all (R10.6: edit mode + playing).</summary>
        private bool ShouldDraw()
        {
            var cfg = ConfigService.Instance;
            if (cfg == null || cfg.Settings == null)
                return false;
            if (!cfg.Settings.MarkersEnable || !cfg.Settings.MarkersEditMode)
                return false;
            var state = TimerCore.State;
            if (state == null || !state.InSegment)
                return false;
            var game = Game.instance;
            return game != null && game.state == GameState.PlayingLevel;
        }

        private bool TryGetBox(MarkerDef def, MarkersManager mgr, out Vector3 center, out Vector3 size)
        {
            center = Vector3.zero;
            size = Vector3.one;
            if (def.Kind == MarkerKind.Range)
            {
                center = new Vector3(def.cx, def.cy, def.cz);
                size = new Vector3(def.sx, def.sy, def.sz);
                return true;
            }
            if (def.Kind == MarkerKind.GrabObject)
            {
                var go = mgr != null ? mgr.ResolveObject(def) : null;
                if (go == null)
                {
                    WarnUnresolved(def);
                    return false;
                }
                Bounds? b = ObjectBounds(go);
                if (b.HasValue)
                {
                    center = b.Value.center;
                    size = b.Value.size;
                    if (size.x <= 0f) size.x = 0.1f;
                    if (size.y <= 0f) size.y = 0.1f;
                    if (size.z <= 0f) size.z = 0.1f;
                }
                else
                {
                    center = go.transform != null ? go.transform.position : Vector3.zero;
                    size = new Vector3(0.5f, 0.5f, 0.5f);
                }
                return true;
            }
            return false;
        }

        private void WarnUnresolved(MarkerDef def)
        {
            var mgr = MarkersManager.Instance;
            string levelKey = mgr != null ? mgr.CurrentLevelKey : null;
            if (_warnedLevelKey != levelKey)
            {
                _warnedLevelKey = levelKey;
                _warnedIds.Clear();
            }
            if (_warnedIds.Add(def.id))
                Plugin.Logger.LogWarning($"HSRTimer[markers]: grab-object marker '{def.name}' ({def.id}) target could not be resolved in this level; highlight skipped (R10.6.4).");
        }

        private static Bounds? ObjectBounds(GameObject go)
        {
            try
            {
                var renderers = go.GetComponentsInChildren<Renderer>(true);
                if (renderers == null || renderers.Length == 0)
                    return null;
                Bounds? acc = null;
                foreach (var r in renderers)
                {
                    if (r == null) continue;
                    if (!acc.HasValue) acc = r.bounds;
                    else acc = Union(acc.Value, r.bounds);
                }
                return acc;
            }
            catch
            {
                return null;
            }
        }

        private static Bounds Union(Bounds a, Bounds b)
        {
            var min = Vector3.Min(a.min, b.min);
            var max = Vector3.Max(a.max, b.max);
            return new Bounds((min + max) * 0.5f, max - min);
        }

        private Camera GetCamera()
        {
            try
            {
                var human = Human.Localplayer;
                if (human != null && human.player != null && human.player.cameraController != null)
                    return human.player.cameraController.gameCam;
            }
            catch
            {
                // fall through to Camera.main
            }
            return Camera.main;
        }

        // ── wireframe fallback (IMGUI, projected edges) ─────────────────────

        private static readonly int[,] CubeEdges =
        {
            { 0, 1 }, { 1, 2 }, { 2, 3 }, { 3, 0 },
            { 4, 5 }, { 5, 6 }, { 6, 7 }, { 7, 4 },
            { 0, 4 }, { 1, 5 }, { 2, 6 }, { 3, 7 },
        };

        private void DrawWireframe(Camera cam, Vector3 center, Vector3 size)
        {
            var half = size * 0.5f;
            Vector3[] corners =
            {
                center + new Vector3(-half.x, -half.y, -half.z),
                center + new Vector3( half.x, -half.y, -half.z),
                center + new Vector3( half.x,  half.y, -half.z),
                center + new Vector3(-half.x,  half.y, -half.z),
                center + new Vector3(-half.x, -half.y,  half.z),
                center + new Vector3( half.x, -half.y,  half.z),
                center + new Vector3( half.x,  half.y,  half.z),
                center + new Vector3(-half.x,  half.y,  half.z),
            };
            var screen = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                var sp = cam.WorldToScreenPoint(corners[i]);
                screen[i] = new Vector3(sp.x, Screen.height - sp.y, sp.z);
            }
            var prevColor = GUI.color;
            GUI.color = _cubeMaterial != null ? _cubeMaterial.color : new Color(0.25f, 0.5f, 1f, 0.9f);
            GUI.color = new Color(GUI.color.r, GUI.color.g, GUI.color.b, 0.9f);
            for (int e = 0; e < CubeEdges.GetLength(0); e++)
            {
                var a = screen[CubeEdges[e, 0]];
                var b = screen[CubeEdges[e, 1]];
                if (a.z <= 0f || b.z <= 0f)
                    continue;
                DrawScreenLine(a, b);
            }
            GUI.color = prevColor;
        }

        private void DrawScreenLine(Vector3 a, Vector3 b)
        {
            float dx = b.x - a.x;
            float dy = b.y - a.y;
            float len = Mathf.Sqrt(dx * dx + dy * dy);
            if (len < 0.5f) return;
            float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
            var prevMatrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, a);
            GUI.DrawTexture(new Rect(a.x, a.y - 1f, len, 2f), Texture2D.whiteTexture);
            GUI.matrix = prevMatrix;
        }

        private void EnsureLabelFont(int size)
        {
            if (_labelFont != null && _labelFontSize == size) return;
            try
            {
                _labelFont = Font.CreateDynamicFontFromOSFont(new[]
                {
                    "PingFang SC", "Microsoft YaHei", "Noto Sans CJK SC",
                    "Noto Sans CJK", "Heiti SC", "Arial Unicode MS", "Arial",
                }, size);
                _labelFontSize = size;
                _labelStyle.font = _labelFont;
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogWarning($"HSRTimer[markers]: overlay font creation failed: {ex.Message}");
                _labelFont = null;
            }
        }

        private static Mesh BuildUnitCube()
        {
            var mesh = new Mesh();
            Vector3[] vertices =
            {
                new Vector3(-0.5f, -0.5f, -0.5f), new Vector3( 0.5f, -0.5f, -0.5f),
                new Vector3( 0.5f,  0.5f, -0.5f), new Vector3(-0.5f,  0.5f, -0.5f),
                new Vector3(-0.5f, -0.5f,  0.5f), new Vector3( 0.5f, -0.5f,  0.5f),
                new Vector3( 0.5f,  0.5f,  0.5f), new Vector3(-0.5f,  0.5f,  0.5f),
            };
            int[] triangles =
            {
                0, 2, 1, 0, 3, 2,
                4, 5, 6, 4, 6, 7,
                0, 1, 5, 0, 5, 4,
                2, 3, 7, 2, 7, 6,
                0, 4, 7, 0, 7, 3,
                1, 2, 6, 1, 6, 5,
            };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
