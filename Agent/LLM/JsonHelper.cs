using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace WController.Agent.LLM;

internal static class JsonHelper
{
    public static string SerializeObject(object obj)
    {
        var sb = new StringBuilder();
        WriteValue(sb, obj);
        return sb.ToString();
    }

    public static Dictionary<string, object?> ParseObject(string json)
    {
        int index = 0;
        return ReadObject(json, ref index);
    }

    public static List<object?> ParseArray(string json)
    {
        int index = 0;
        return ReadArray(json, ref index);
    }

    // ====== Writer ======

    private static void WriteValue(StringBuilder sb, object? value)
    {
        if (value == null)
        {
            sb.Append("null");
        }
        else if (value is string s)
        {
            WriteString(sb, s);
        }
        else if (value is bool b)
        {
            sb.Append(b ? "true" : "false");
        }
        else if (value is int i)
        {
            sb.Append(i.ToString(CultureInfo.InvariantCulture));
        }
        else if (value is long l)
        {
            sb.Append(l.ToString(CultureInfo.InvariantCulture));
        }
        else if (value is double d)
        {
            sb.Append(d.ToString(CultureInfo.InvariantCulture));
        }
        else if (value is float f)
        {
            sb.Append(f.ToString(CultureInfo.InvariantCulture));
        }
        else if (value is Dictionary<string, object?> dict)
        {
            WriteObject(sb, dict);
        }
        else if (value is List<object?> list)
        {
            WriteArray(sb, list);
        }
        else if (value is object[] arr)
        {
            sb.Append('[');
            for (int idx = 0; idx < arr.Length; idx++)
            {
                if (idx > 0) sb.Append(',');
                WriteValue(sb, arr[idx]);
            }
            sb.Append(']');
        }
        else
        {
            WriteString(sb, value.ToString() ?? "");
        }
    }

    private static void WriteString(StringBuilder sb, string s)
    {
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
                    if (c < 0x20)
                        sb.Append($"\\u{(int)c:X4}");
                    else
                        sb.Append(c);
                    break;
            }
        }
        sb.Append('"');
    }

    private static void WriteObject(StringBuilder sb, Dictionary<string, object?> obj)
    {
        sb.Append('{');
        bool first = true;
        foreach (var kv in obj)
        {
            if (!first) sb.Append(',');
            first = false;
            WriteString(sb, kv.Key);
            sb.Append(':');
            WriteValue(sb, kv.Value);
        }
        sb.Append('}');
    }

    private static void WriteArray(StringBuilder sb, List<object?> arr)
    {
        sb.Append('[');
        for (int i = 0; i < arr.Count; i++)
        {
            if (i > 0) sb.Append(',');
            WriteValue(sb, arr[i]);
        }
        sb.Append(']');
    }

    // ====== Parser ======

    private static void SkipWhitespace(string json, ref int index)
    {
        while (index < json.Length && char.IsWhiteSpace(json[index]))
            index++;
    }

    private static object? ReadValue(string json, ref int index)
    {
        SkipWhitespace(json, ref index);
        if (index >= json.Length) return null;

        char c = json[index];
        if (c == '"') return ReadString(json, ref index);
        if (c == '{') return ReadObject(json, ref index);
        if (c == '[') return ReadArray(json, ref index);
        if (c == 't' || c == 'f') return ReadBool(json, ref index);
        if (c == 'n') { index += 4; return null; }
        return ReadNumber(json, ref index);
    }

    private static string ReadString(string json, ref int index)
    {
        index++; // skip opening "
        var sb = new StringBuilder();
        while (index < json.Length)
        {
            char c = json[index++];
            if (c == '"') break;
            if (c == '\\' && index < json.Length)
            {
                char next = json[index++];
                switch (next)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u':
                        if (index + 4 <= json.Length)
                        {
                            string hex = json.Substring(index, 4);
                            sb.Append((char)int.Parse(hex, NumberStyles.HexNumber));
                            index += 4;
                        }
                        break;
                    default: sb.Append(next); break;
                }
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    private static Dictionary<string, object?> ReadObject(string json, ref int index)
    {
        var dict = new Dictionary<string, object?>();
        index++; // skip {
        SkipWhitespace(json, ref index);

        while (index < json.Length && json[index] != '}')
        {
            SkipWhitespace(json, ref index);
            if (index < json.Length && json[index] == '}') break;
            string key = ReadString(json, ref index);
            SkipWhitespace(json, ref index);
            if (index < json.Length && json[index] == ':') index++;
            SkipWhitespace(json, ref index);
            object? val = ReadValue(json, ref index);
            dict[key] = val;
            SkipWhitespace(json, ref index);
            if (index < json.Length && json[index] == ',') index++;
        }

        if (index < json.Length) index++; // skip }
        return dict;
    }

    private static List<object?> ReadArray(string json, ref int index)
    {
        var list = new List<object?>();
        index++; // skip [
        SkipWhitespace(json, ref index);

        while (index < json.Length && json[index] != ']')
        {
            SkipWhitespace(json, ref index);
            if (index < json.Length && json[index] == ']') break;
            list.Add(ReadValue(json, ref index));
            SkipWhitespace(json, ref index);
            if (index < json.Length && json[index] == ',') index++;
        }

        if (index < json.Length) index++; // skip ]
        return list;
    }

    private static bool ReadBool(string json, ref int index)
    {
        if (json.Substring(index, 4) == "true") { index += 4; return true; }
        index += 5; return false;
    }

    private static object ReadNumber(string json, ref int index)
    {
        int start = index;
        while (index < json.Length && (char.IsDigit(json[index]) || json[index] == '-' || json[index] == '.' || json[index] == 'e' || json[index] == 'E' || json[index] == '+'))
            index++;

        string numStr = json.Substring(start, index - start);
        if (numStr.Contains(".") || numStr.Contains("e") || numStr.Contains("E"))
            return double.Parse(numStr, CultureInfo.InvariantCulture);

        if (long.TryParse(numStr, out long l))
            return l < int.MinValue || l > int.MaxValue ? (object)l : (int)l;

        return double.Parse(numStr, CultureInfo.InvariantCulture);
    }

    // ====== Helpers ======

    public static string? GetString(Dictionary<string, object?> obj, string key)
    {
        if (obj.TryGetValue(key, out var val) && val != null) return val.ToString();
        return null;
    }

    public static Dictionary<string, object?>? GetObject(Dictionary<string, object?> obj, string key)
    {
        if (obj.TryGetValue(key, out var val) && val is Dictionary<string, object?> dict) return dict;
        return null;
    }

    public static List<object?>? GetArray(Dictionary<string, object?> obj, string key)
    {
        if (obj.TryGetValue(key, out var val) && val is List<object?> list) return list;
        return null;
    }

    public static int GetInt(Dictionary<string, object?> obj, string key, int defaultValue = 0)
    {
        if (obj.TryGetValue(key, out var val) && val is int i) return i;
        return defaultValue;
    }

    public static Dictionary<string, object?> AsDictionary(object? value)
    {
        return value as Dictionary<string, object?> ?? new Dictionary<string, object?>();
    }
}
