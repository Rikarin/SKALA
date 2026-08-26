using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Rikarin.Skala.Options.Generator;

/// <summary>
/// The smallest JSON reader that can read <c>options.json</c>.
/// </summary>
/// <remarks>
/// A source generator loads into <c>csc</c> and into Rider, so every assembly it drags with it is a
/// way for the analyzer load to fail (docs/plan/02 § "The project graph"). <c>System.Text.Json</c>
/// is not worth that risk for one file with no unusual shapes, so the reader is here.
/// </remarks>
internal static class Json {
    public static JsonValue Parse(string text) {
        var index = 0;
        var value = ParseValue(text, ref index);
        SkipWhitespace(text, ref index);
        if (index != text.Length) {
            throw new JsonException($"Trailing content at offset {index}.");
        }

        return value;
    }

    static JsonValue ParseValue(string text, ref int index) {
        SkipWhitespace(text, ref index);
        if (index >= text.Length) {
            throw new JsonException("Unexpected end of input.");
        }

        return text[index] switch {
            '{' => ParseObject(text, ref index),
            '[' => ParseArray(text, ref index),
            '"' => JsonValue.FromString(ParseString(text, ref index)),
            't' => Literal(text, ref index, "true", JsonValue.True),
            'f' => Literal(text, ref index, "false", JsonValue.False),
            'n' => Literal(text, ref index, "null", JsonValue.Null),
            _ => ParseNumber(text, ref index)
        };
    }

    static JsonValue Literal(string text, ref int index, string word, JsonValue value) {
        if (index + word.Length > text.Length || string.CompareOrdinal(text, index, word, 0, word.Length) != 0) {
            throw new JsonException($"Expected '{word}' at offset {index}.");
        }

        index += word.Length;
        return value;
    }

    static JsonValue ParseObject(string text, ref int index) {
        index++;
        var members = new Dictionary<string, JsonValue>(StringComparer.Ordinal);
        SkipWhitespace(text, ref index);
        if (index < text.Length && text[index] == '}') {
            index++;
            return JsonValue.FromObject(members);
        }

        while (true) {
            SkipWhitespace(text, ref index);
            var name = ParseString(text, ref index);
            SkipWhitespace(text, ref index);
            Expect(text, ref index, ':');
            members[name] = ParseValue(text, ref index);
            SkipWhitespace(text, ref index);
            if (index >= text.Length) {
                throw new JsonException("Unterminated object.");
            }

            if (text[index] == ',') {
                index++;
                continue;
            }

            Expect(text, ref index, '}');
            return JsonValue.FromObject(members);
        }
    }

    static JsonValue ParseArray(string text, ref int index) {
        index++;
        var items = new List<JsonValue>();
        SkipWhitespace(text, ref index);
        if (index < text.Length && text[index] == ']') {
            index++;
            return JsonValue.FromArray(items);
        }

        while (true) {
            items.Add(ParseValue(text, ref index));
            SkipWhitespace(text, ref index);
            if (index >= text.Length) {
                throw new JsonException("Unterminated array.");
            }

            if (text[index] == ',') {
                index++;
                continue;
            }

            Expect(text, ref index, ']');
            return JsonValue.FromArray(items);
        }
    }

    static string ParseString(string text, ref int index) {
        Expect(text, ref index, '"');
        var builder = new StringBuilder();
        while (true) {
            if (index >= text.Length) {
                throw new JsonException("Unterminated string.");
            }

            var c = text[index++];
            if (c == '"') {
                return builder.ToString();
            }

            if (c != '\\') {
                builder.Append(c);
                continue;
            }

            var escape = text[index++];
            switch (escape) {
                case '"': builder.Append('"'); break;
                case '\\': builder.Append('\\'); break;
                case '/': builder.Append('/'); break;
                case 'b': builder.Append('\b'); break;
                case 'f': builder.Append('\f'); break;
                case 'n': builder.Append('\n'); break;
                case 'r': builder.Append('\r'); break;
                case 't': builder.Append('\t'); break;
                case 'u':
                    builder.Append((char)ushort.Parse(text.Substring(index, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                    index += 4;
                    break;
                default: throw new JsonException($"Unknown escape '\\{escape}'.");
            }
        }
    }

    static JsonValue ParseNumber(string text, ref int index) {
        var start = index;
        while (index < text.Length && (char.IsDigit(text[index]) || "+-.eE".IndexOf(text[index]) >= 0)) {
            index++;
        }

        var slice = text.Substring(start, index - start);
        if (!double.TryParse(slice, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)) {
            throw new JsonException($"Invalid number '{slice}'.");
        }

        return JsonValue.FromNumber(number);
    }

    static void Expect(string text, ref int index, char expected) {
        if (index >= text.Length || text[index] != expected) {
            throw new JsonException($"Expected '{expected}' at offset {index}.");
        }

        index++;
    }

    static void SkipWhitespace(string text, ref int index) {
        while (index < text.Length && char.IsWhiteSpace(text[index])) {
            index++;
        }
    }
}

internal sealed class JsonException(string message) : Exception(message);

internal readonly struct JsonValue {
    public static readonly JsonValue Null = new(JsonKind.Null, null, 0, null, null);
    public static readonly JsonValue True = new(JsonKind.Boolean, null, 1, null, null);
    public static readonly JsonValue False = new(JsonKind.Boolean, null, 0, null, null);

    readonly JsonKind _kind;
    readonly string? _string;
    readonly double _number;
    readonly Dictionary<string, JsonValue>? _members;
    readonly List<JsonValue>? _items;

    JsonValue(JsonKind kind, string? text, double number, Dictionary<string, JsonValue>? members, List<JsonValue>? items) {
        _kind = kind;
        _string = text;
        _number = number;
        _members = members;
        _items = items;
    }

    public static JsonValue FromString(string value) => new(JsonKind.String, value, 0, null, null);
    public static JsonValue FromNumber(double value) => new(JsonKind.Number, null, value, null, null);
    public static JsonValue FromObject(Dictionary<string, JsonValue> members) => new(JsonKind.Object, null, 0, members, null);
    public static JsonValue FromArray(List<JsonValue> items) => new(JsonKind.Array, null, 0, null, items);

    public bool IsNull => _kind == JsonKind.Null;
    public IReadOnlyList<JsonValue> Items => _items ?? (IReadOnlyList<JsonValue>)Array.Empty<JsonValue>();
    public IEnumerable<KeyValuePair<string, JsonValue>> Members => _members ?? [];

    public JsonValue this[string name] =>
        _members is not null && _members.TryGetValue(name, out var value) ? value : Null;

    public string? AsString() => _kind switch {
        JsonKind.String => _string,
        JsonKind.Number => _number.ToString(CultureInfo.InvariantCulture),
        JsonKind.Boolean => _number != 0 ? "true" : "false",
        _ => null
    };

    public int? AsInt() => _kind == JsonKind.Number ? (int)_number : null;
    public bool AsBool() => _kind == JsonKind.Boolean && _number != 0;

    public IReadOnlyList<string> AsStringList() {
        if (_items is null) {
            return [];
        }

        var result = new List<string>(_items.Count);
        foreach (var item in _items) {
            var text = item.AsString();
            if (text is not null) {
                result.Add(text);
            }
        }

        return result;
    }

    enum JsonKind { Null, Boolean, Number, String, Object, Array }
}
