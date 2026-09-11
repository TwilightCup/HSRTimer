using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace HSRTimer
{
    /// <summary>
    /// Explicit JSON writer/parser for marker set files (R10 §4.1).
    ///
    /// Deliberately does NOT use <c>UnityEngine.JsonUtility</c>: in this Unity
    /// version JsonUtility silently drops fields it cannot map — notably empty
    /// <c>List&lt;T&gt;</c> fields and element types containing <c>long</c>/<c>uint</c> —
    /// which produced marker files with no <c>markers</c> array at all (the data
    /// appeared to vanish on every save). This writer always emits every field,
    /// and the parser is tolerant: unknown keys are ignored, missing sections
    /// become empty lists, and malformed entries are reported so a hand-edited
    /// file can never break the timer (N6 / N-MARK3).
    ///
    /// This file intentionally depends on nothing but the BCL so the format can
    /// be unit-tested outside Unity.
    /// </summary>
    public static class MarkerJson
    {
        public const int FormatVersion = 1;

        // ── writing ────────────────────────────────────────────────────────

        public static string Write(MarkerSet set)
        {
            if (set == null) set = new MarkerSet();
            var sb = new StringBuilder(2048);
            sb.Append("{\n");
            AppendField(sb, 2, "format_version", set.format_version.ToString(CultureInfo.InvariantCulture), true);
            AppendField(sb, 2, "level_id", Str(set.level_id), true);
            AppendField(sb, 2, "level_source", Str(set.level_source), true);
            AppendField(sb, 2, "level_number", set.level_number.ToString(CultureInfo.InvariantCulture), true);
            AppendField(sb, 2, "category_key", Str(set.category_key), true);
            AppendField(sb, 2, "hsrtimer_version", Str(set.hsrtimer_version), true);
            AppendField(sb, 2, "updated_at", Str(set.updated_at), true);
            AppendPb(sb, set.pb);

            var markers = set.markers ?? new List<MarkerDef>();
            AppendArray(sb, 2, "markers", markers.Count, i => DefJson(markers[i]), true);

            var times = set.pbTimes ?? new List<MarkerPbEntry>();
            AppendArray(sb, 2, "pbTimes", times.Count, i => PbEntryJson(times[i]), false);

            sb.Append("}\n");
            return sb.ToString();
        }

        private static void AppendPb(StringBuilder sb, MarkerPb pb)
        {
            sb.Append(Pad(2)).Append("\"pb\": ");
            if (pb == null)
            {
                sb.Append("null,\n");
                return;
            }
            sb.Append("{\n");
            AppendField(sb, 4, "total_ms", pb.total_ms.ToString(CultureInfo.InvariantCulture), true);
            AppendField(sb, 4, "created_at", Str(pb.created_at), false);
            sb.Append(Pad(2)).Append("},\n");
        }

        /// <summary>
        /// One marker as JSON text. Indentation is RELATIVE to the caller's block
        /// (first line "{", inner fields at +2, closing "}" at 0); the array
        /// writer re-indents every line, so nesting stays correct.
        /// </summary>
        private static string DefJson(MarkerDef d)
        {
            if (d == null) return "null";
            var sb = new StringBuilder();
            sb.Append("{\n");
            AppendField(sb, 2, "id", Str(d.id), true);
            AppendField(sb, 2, "name", Str(d.name), true);
            AppendField(sb, 2, "type", Str(d.type), true);
            AppendField(sb, 2, "enabled", Bool(d.enabled), true);
            AppendField(sb, 2, "cx", Num(d.cx), true);
            AppendField(sb, 2, "cy", Num(d.cy), true);
            AppendField(sb, 2, "cz", Num(d.cz), true);
            AppendField(sb, 2, "sx", Num(d.sx), true);
            AppendField(sb, 2, "sy", Num(d.sy), true);
            AppendField(sb, 2, "sz", Num(d.sz), true);
            AppendField(sb, 2, "requireGrab", Bool(d.requireGrab), true);
            AppendField(sb, 2, "requireJump", Bool(d.requireJump), true);
            AppendField(sb, 2, "checkpointIndex", d.checkpointIndex.ToString(CultureInfo.InvariantCulture), true);
            AppendField(sb, 2, "triggerOnLoad", Bool(d.triggerOnLoad), true);
            AppendField(sb, 2, "objectSceneId", d.objectSceneId.ToString(CultureInfo.InvariantCulture), true);
            AppendField(sb, 2, "objectPath", Str(d.objectPath), true);
            AppendField(sb, 2, "objectName", Str(d.objectName), true);
            AppendField(sb, 2, "ox", Num(d.ox), true);
            AppendField(sb, 2, "oy", Num(d.oy), true);
            AppendField(sb, 2, "oz", Num(d.oz), false);
            sb.Append('}');
            return sb.ToString();
        }

        private static string PbEntryJson(MarkerPbEntry e)
        {
            if (e == null) return "null";
            var sb = new StringBuilder();
            sb.Append("{\n");
            AppendField(sb, 2, "id", Str(e.id), true);
            AppendField(sb, 2, "t_ms", e.t_ms.ToString(CultureInfo.InvariantCulture), false);
            sb.Append('}');
            return sb.ToString();
        }

        private static void AppendArray(StringBuilder sb, int indent, string key, int count,
            Func<int, string> element, bool comma)
        {
            string pad = Pad(indent);
            string pad2 = Pad(indent + 2);
            sb.Append(pad).Append('"').Append(key).Append("\": ");
            if (count == 0)
            {
                // Always emit "[]" — an omitted/empty array was exactly what the
                // old JsonUtility-based writer lost.
                sb.Append("[]");
            }
            else
            {
                sb.Append("[\n");
                for (int i = 0; i < count; i++)
                {
                    AppendIndentedBlock(sb, element(i), pad2);
                    sb.Append(i < count - 1 ? ",\n" : "\n");
                }
                sb.Append(pad).Append(']');
            }
            sb.Append(comma ? "," : "").Append('\n');
        }

        private static void AppendIndentedBlock(StringBuilder sb, string text, string pad)
        {
            string[] lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                sb.Append(pad).Append(lines[i]);
                if (i < lines.Length - 1)
                    sb.Append('\n');
            }
        }

        private static void AppendField(StringBuilder sb, int indent, string key, string rawValue, bool comma)
        {
            sb.Append(Pad(indent)).Append('"').Append(key).Append("\": ").Append(rawValue).Append(comma ? "," : "").Append('\n');
        }

        private static string Pad(int indent) => new string(' ', indent);

        private static string Bool(bool v) => v ? "true" : "false";

        private static string Num(float v)
            => v.ToString("R", CultureInfo.InvariantCulture);

        private static string Str(string s)
        {
            if (string.IsNullOrEmpty(s)) return "\"\"";
            var sb = new StringBuilder(s.Length + 2);
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < ' ')
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        // ── parsing ────────────────────────────────────────────────────────

        /// <summary>
        /// Parse a marker set file. Returns false only for a fatal problem
        /// (not an object, unsupported format version, malformed JSON);
        /// recoverable problems are appended to <paramref name="warnings"/>.
        /// </summary>
        public static bool TryParse(string json, out MarkerSet set, out string error, ICollection<string> warnings = null)
        {
            set = null;
            error = null;
            if (string.IsNullOrEmpty(json))
            {
                error = "empty file";
                return false;
            }
            object root;
            try
            {
                int i = 0;
                root = ParseValue(json, ref i);
            }
            catch (Exception ex)
            {
                error = "malformed JSON: " + ex.Message;
                return false;
            }
            var obj = root as Dictionary<string, object>;
            if (obj == null)
            {
                error = "root is not a JSON object";
                return false;
            }

            var result = new MarkerSet();
            result.format_version = GetInt(obj, "format_version", FormatVersion);
            if (result.format_version != FormatVersion)
            {
                error = $"unsupported format_version {result.format_version}";
                return false;
            }
            result.level_id = GetString(obj, "level_id", "");
            result.level_source = GetString(obj, "level_source", "");
            result.level_number = GetInt(obj, "level_number", -1);
            result.category_key = GetString(obj, "category_key", "Any");
            result.hsrtimer_version = GetString(obj, "hsrtimer_version", "");
            result.updated_at = GetString(obj, "updated_at", "");

            var pbObj = GetObject(obj, "pb");
            if (pbObj != null)
            {
                result.pb = new MarkerPb
                {
                    total_ms = GetLong(pbObj, "total_ms", 0L),
                    created_at = GetString(pbObj, "created_at", ""),
                };
            }

            result.markers = new List<MarkerDef>();
            var markerList = GetArray(obj, "markers");
            if (markerList != null)
            {
                for (int idx = 0; idx < markerList.Count; idx++)
                {
                    var d = markerList[idx] as Dictionary<string, object>;
                    if (d == null)
                    {
                        Warn(warnings, $"markers[{idx}] is not an object; skipped.");
                        continue;
                    }
                    var def = MapDef(d, idx, warnings);
                    if (def != null)
                        result.markers.Add(def);
                }
            }

            result.pbTimes = new List<MarkerPbEntry>();
            var timeList = GetArray(obj, "pbTimes");
            if (timeList != null)
            {
                for (int idx = 0; idx < timeList.Count; idx++)
                {
                    var t = timeList[idx] as Dictionary<string, object>;
                    if (t == null)
                    {
                        Warn(warnings, $"pbTimes[{idx}] is not an object; skipped.");
                        continue;
                    }
                    string id = GetString(t, "id", null);
                    if (string.IsNullOrEmpty(id))
                    {
                        Warn(warnings, $"pbTimes[{idx}] has no id; skipped.");
                        continue;
                    }
                    result.pbTimes.Add(new MarkerPbEntry { id = id, t_ms = GetLong(t, "t_ms", 0L) });
                }
            }

            set = result;
            return true;
        }

        private static MarkerDef MapDef(Dictionary<string, object> d, int idx, ICollection<string> warnings)
        {
            string id = GetString(d, "id", null);
            if (string.IsNullOrEmpty(id))
            {
                Warn(warnings, $"markers[{idx}] has no id; skipped.");
                return null;
            }
            var def = new MarkerDef
            {
                id = id,
                name = GetString(d, "name", ""),
                type = GetString(d, "type", "Range"),
                enabled = GetBool(d, "enabled", true),
                cx = GetFloat(d, "cx", 0f),
                cy = GetFloat(d, "cy", 0f),
                cz = GetFloat(d, "cz", 0f),
                sx = GetFloat(d, "sx", 0f),
                sy = GetFloat(d, "sy", 0f),
                sz = GetFloat(d, "sz", 0f),
                requireGrab = GetBool(d, "requireGrab", false),
                requireJump = GetBool(d, "requireJump", false),
                checkpointIndex = GetInt(d, "checkpointIndex", 0),
                triggerOnLoad = GetBool(d, "triggerOnLoad", false),
                objectSceneId = (uint)Math.Max(0L, GetLong(d, "objectSceneId", 0L)),
                objectPath = GetString(d, "objectPath", ""),
                objectName = GetString(d, "objectName", ""),
                ox = GetFloat(d, "ox", 0f),
                oy = GetFloat(d, "oy", 0f),
                oz = GetFloat(d, "oz", 0f),
            };
            if (!MarkerKindUtil.IsKnown(def.type))
                Warn(warnings, $"markers[{idx}] ('{id}') has unknown type '{def.type}'; it will stay inactive.");
            return def;
        }

        private static void Warn(ICollection<string> warnings, string message)
        {
            if (warnings != null) warnings.Add(message);
        }

        // ── minimal JSON value parser ──────────────────────────────────────

        private static object ParseValue(string s, ref int i)
        {
            SkipWs(s, ref i);
            if (i >= s.Length)
                throw new FormatException("unexpected end of input");
            char c = s[i];
            switch (c)
            {
                case '{': return ParseObject(s, ref i);
                case '[': return ParseArray(s, ref i);
                case '"': return ParseString(s, ref i);
                case 't': Expect(s, ref i, "true"); return true;
                case 'f': Expect(s, ref i, "false"); return false;
                case 'n': Expect(s, ref i, "null"); return null;
                default: return ParseNumber(s, ref i);
            }
        }

        private static Dictionary<string, object> ParseObject(string s, ref int i)
        {
            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            i++; // '{'
            SkipWs(s, ref i);
            if (i < s.Length && s[i] == '}')
            {
                i++;
                return result;
            }
            while (true)
            {
                SkipWs(s, ref i);
                if (i >= s.Length || s[i] != '"')
                    throw new FormatException($"expected a key at offset {i}");
                string key = ParseString(s, ref i);
                SkipWs(s, ref i);
                if (i >= s.Length || s[i] != ':')
                    throw new FormatException($"expected ':' at offset {i}");
                i++;
                object value = ParseValue(s, ref i);
                result[key] = value;
                SkipWs(s, ref i);
                if (i >= s.Length)
                    throw new FormatException("unterminated object");
                if (s[i] == ',')
                {
                    i++;
                    continue;
                }
                if (s[i] == '}')
                {
                    i++;
                    return result;
                }
                throw new FormatException($"expected ',' or '}}' at offset {i}");
            }
        }

        private static List<object> ParseArray(string s, ref int i)
        {
            var result = new List<object>();
            i++; // '['
            SkipWs(s, ref i);
            if (i < s.Length && s[i] == ']')
            {
                i++;
                return result;
            }
            while (true)
            {
                result.Add(ParseValue(s, ref i));
                SkipWs(s, ref i);
                if (i >= s.Length)
                    throw new FormatException("unterminated array");
                if (s[i] == ',')
                {
                    i++;
                    continue;
                }
                if (s[i] == ']')
                {
                    i++;
                    return result;
                }
                throw new FormatException($"expected ',' or ']' at offset {i}");
            }
        }

        private static string ParseString(string s, ref int i)
        {
            i++; // opening quote
            var sb = new StringBuilder();
            while (true)
            {
                if (i >= s.Length)
                    throw new FormatException("unterminated string");
                char c = s[i++];
                if (c == '"')
                    return sb.ToString();
                if (c != '\\')
                {
                    sb.Append(c);
                    continue;
                }
                if (i >= s.Length)
                    throw new FormatException("unterminated escape");
                char e = s[i++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (i + 4 > s.Length)
                            throw new FormatException("truncated \\u escape");
                        sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        i += 4;
                        break;
                    default:
                        throw new FormatException($"bad escape '\\{e}'");
                }
            }
        }

        private static double ParseNumber(string s, ref int i)
        {
            int start = i;
            if (i < s.Length && (s[i] == '-' || s[i] == '+')) i++;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.' || s[i] == 'e' || s[i] == 'E' || s[i] == '+' || s[i] == '-'))
                i++;
            if (i == start)
                throw new FormatException($"unexpected character '{s[start]}' at offset {start}");
            string token = s.Substring(start, i - start);
            double value;
            if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                throw new FormatException($"bad number '{token}'");
            return value;
        }

        private static void Expect(string s, ref int i, string literal)
        {
            if (i + literal.Length > s.Length || s.Substring(i, literal.Length) != literal)
                throw new FormatException($"expected '{literal}' at offset {i}");
            i += literal.Length;
        }

        private static void SkipWs(string s, ref int i)
        {
            while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\n' || s[i] == '\r'))
                i++;
        }

        // ── typed accessors ────────────────────────────────────────────────

        private static string GetString(Dictionary<string, object> o, string key, string fallback)
        {
            object v;
            if (o.TryGetValue(key, out v) && v is string)
                return (string)v;
            return fallback;
        }

        private static float GetFloat(Dictionary<string, object> o, string key, float fallback)
        {
            object v;
            if (!o.TryGetValue(key, out v) || v == null) return fallback;
            if (v is double) return (float)(double)v;
            if (v is string)
            {
                float parsed;
                if (float.TryParse((string)v, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
                    return parsed;
            }
            return fallback;
        }

        private static int GetInt(Dictionary<string, object> o, string key, int fallback)
            => (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, GetLong(o, key, fallback)));

        private static long GetLong(Dictionary<string, object> o, string key, long fallback)
        {
            object v;
            if (!o.TryGetValue(key, out v) || v == null) return fallback;
            if (v is double) return (long)Math.Round((double)v);
            if (v is string)
            {
                long parsed;
                if (long.TryParse((string)v, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
                    return parsed;
            }
            return fallback;
        }

        private static bool GetBool(Dictionary<string, object> o, string key, bool fallback)
        {
            object v;
            if (!o.TryGetValue(key, out v) || v == null) return fallback;
            if (v is bool) return (bool)v;
            if (v is string)
            {
                string t = ((string)v).Trim().ToLowerInvariant();
                if (t == "true" || t == "1" || t == "yes" || t == "on") return true;
                if (t == "false" || t == "0" || t == "no" || t == "off") return false;
            }
            return fallback;
        }

        private static Dictionary<string, object> GetObject(Dictionary<string, object> o, string key)
        {
            object v;
            return o.TryGetValue(key, out v) ? v as Dictionary<string, object> : null;
        }

        private static List<object> GetArray(Dictionary<string, object> o, string key)
        {
            object v;
            return o.TryGetValue(key, out v) ? v as List<object> : null;
        }
    }
}
