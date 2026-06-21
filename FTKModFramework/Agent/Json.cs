using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace FTKModFramework.Agent
{
    /// <summary>
    /// Tiny net35-safe JSON codec. We deliberately avoid Newtonsoft (the game bundles a version whose
    /// API surface this framework has not vetted) and System.Text.Json (postdates .NET 3.5). This writes
    /// the JSON DOM the bridge builds (Dictionary&lt;string,object&gt; / IEnumerable / string / bool /
    /// integral / floating / null) and parses just enough to read a POST body of the form {action,args}.
    /// </summary>
    internal static class Json
    {
        // ---------------------------------------------------------------- writer ------------------------

        public static string Write(object value)
        {
            StringBuilder sb = new StringBuilder(256);
            WriteValue(sb, value);
            return sb.ToString();
        }

        private static void WriteValue(StringBuilder sb, object value)
        {
            if (value == null) { sb.Append("null"); return; }

            string s = value as string;
            if (s != null) { WriteString(sb, s); return; }

            if (value is bool) { sb.Append(((bool)value) ? "true" : "false"); return; }

            // Integral types.
            if (value is int || value is long || value is short || value is byte ||
                value is uint || value is ulong || value is ushort || value is sbyte)
            {
                sb.Append(Convert.ToInt64(value).ToString(CultureInfo.InvariantCulture));
                return;
            }

            // Floating types: emit a finite number or null (JSON has no NaN/Infinity).
            if (value is float || value is double || value is decimal)
            {
                double d = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                if (double.IsNaN(d) || double.IsInfinity(d)) { sb.Append("null"); return; }
                sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
                return;
            }

            // A nested object.
            IDictionary<string, object> map = value as IDictionary<string, object>;
            if (map != null) { WriteObject(sb, map); return; }

            // Any other dictionary (string-keyed): coerce keys to string.
            IDictionary genericMap = value as IDictionary;
            if (genericMap != null) { WriteLooseObject(sb, genericMap); return; }

            // An array / list. (string already handled above, so IEnumerable here is a real sequence.)
            IEnumerable seq = value as IEnumerable;
            if (seq != null) { WriteArray(sb, seq); return; }

            // Fallback: stringify anything else (e.g. an enum) so we never throw mid-serialize.
            WriteString(sb, value.ToString());
        }

        private static void WriteObject(StringBuilder sb, IDictionary<string, object> map)
        {
            sb.Append('{');
            bool first = true;
            foreach (KeyValuePair<string, object> kvp in map)
            {
                if (!first) sb.Append(',');
                first = false;
                WriteString(sb, kvp.Key);
                sb.Append(':');
                WriteValue(sb, kvp.Value);
            }
            sb.Append('}');
        }

        private static void WriteLooseObject(StringBuilder sb, IDictionary map)
        {
            sb.Append('{');
            bool first = true;
            foreach (DictionaryEntry e in map)
            {
                if (!first) sb.Append(',');
                first = false;
                WriteString(sb, e.Key == null ? "null" : e.Key.ToString());
                sb.Append(':');
                WriteValue(sb, e.Value);
            }
            sb.Append('}');
        }

        private static void WriteArray(StringBuilder sb, IEnumerable seq)
        {
            sb.Append('[');
            bool first = true;
            foreach (object item in seq)
            {
                if (!first) sb.Append(',');
                first = false;
                WriteValue(sb, item);
            }
            sb.Append(']');
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ')
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        // ---------------------------------------------------------------- reader ------------------------

        /// <summary>
        /// Parse a JSON value into object / Dictionary&lt;string,object&gt; / List&lt;object&gt; / string /
        /// bool / long / double / null. Returns null on any malformed input (the caller treats a null body
        /// as "no action"). Never throws to the HTTP layer.
        /// </summary>
        public static object Parse(string text)
        {
            if (text == null) return null;
            try
            {
                int pos = 0;
                object result = ParseValue(text, ref pos);
                return result;
            }
            catch
            {
                return null;
            }
        }

        private static object ParseValue(string s, ref int i)
        {
            SkipWhitespace(s, ref i);
            if (i >= s.Length) throw new FormatException("unexpected end");
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
            Dictionary<string, object> map = new Dictionary<string, object>();
            i++; // consume '{'
            SkipWhitespace(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return map; }
            while (true)
            {
                SkipWhitespace(s, ref i);
                string key = ParseString(s, ref i);
                SkipWhitespace(s, ref i);
                if (i >= s.Length || s[i] != ':') throw new FormatException("expected ':'");
                i++; // consume ':'
                object val = ParseValue(s, ref i);
                map[key] = val;
                SkipWhitespace(s, ref i);
                if (i >= s.Length) throw new FormatException("unterminated object");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; break; }
                throw new FormatException("expected ',' or '}'");
            }
            return map;
        }

        private static List<object> ParseArray(string s, ref int i)
        {
            List<object> list = new List<object>();
            i++; // consume '['
            SkipWhitespace(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return list; }
            while (true)
            {
                object val = ParseValue(s, ref i);
                list.Add(val);
                SkipWhitespace(s, ref i);
                if (i >= s.Length) throw new FormatException("unterminated array");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; break; }
                throw new FormatException("expected ',' or ']'");
            }
            return list;
        }

        private static string ParseString(string s, ref int i)
        {
            if (i >= s.Length || s[i] != '"') throw new FormatException("expected string");
            i++; // consume opening quote
            StringBuilder sb = new StringBuilder();
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c == '\\')
                {
                    if (i >= s.Length) throw new FormatException("bad escape");
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
                            if (i + 4 > s.Length) throw new FormatException("bad \\u");
                            string hex = s.Substring(i, 4);
                            i += 4;
                            sb.Append((char)int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            break;
                        default: throw new FormatException("bad escape char");
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            throw new FormatException("unterminated string");
        }

        private static object ParseNumber(string s, ref int i)
        {
            int start = i;
            bool isFloat = false;
            while (i < s.Length)
            {
                char c = s[i];
                if (c == '-' || c == '+' || (c >= '0' && c <= '9')) { i++; continue; }
                if (c == '.' || c == 'e' || c == 'E') { isFloat = true; i++; continue; }
                break;
            }
            string num = s.Substring(start, i - start);
            if (num.Length == 0) throw new FormatException("expected number");
            if (isFloat)
                return double.Parse(num, CultureInfo.InvariantCulture);
            long l;
            if (long.TryParse(num, NumberStyles.Integer, CultureInfo.InvariantCulture, out l))
                return l;
            return double.Parse(num, CultureInfo.InvariantCulture);
        }

        private static void Expect(string s, ref int i, string literal)
        {
            if (i + literal.Length > s.Length || s.Substring(i, literal.Length) != literal)
                throw new FormatException("expected '" + literal + "'");
            i += literal.Length;
        }

        private static void SkipWhitespace(string s, ref int i)
        {
            while (i < s.Length)
            {
                char c = s[i];
                if (c == ' ' || c == '\t' || c == '\n' || c == '\r') i++;
                else break;
            }
        }
    }
}
