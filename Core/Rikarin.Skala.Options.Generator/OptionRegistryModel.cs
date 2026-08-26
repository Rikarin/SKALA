using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Rikarin.Skala.Options.Generator;

internal sealed record OptionEnumValue(string EditorConfigName, string MemberName, string Summary);

internal sealed record OptionEnum(string Name, IReadOnlyList<OptionEnumValue> Values, IReadOnlyList<KeyValuePair<string, string>> ValueAliases);

internal enum OptionValueKind { Bool, Int, String, Enum }

internal sealed record OptionEntry(
    string Key,
    IReadOnlyList<string> Aliases,
    string Language,
    OptionValueKind Kind,
    string? EnumName,
    string? Default,
    string DefaultSource,
    string Tier,
    string Construct,
    string Summary,
    string Since,
    string? Oracle,
    string? Docs,
    int? TemplateLine,
    bool SeveritySuffix,
    IReadOnlyList<string> Expands) {
    /// <summary>The <c>OptionId</c> member name and the group path from docs/plan/02 § "Naming".</summary>
    public string MemberName => Naming.Pascal(Key);

    public IReadOnlyList<string> GroupPath => Naming.GroupPath(Key);
}

internal sealed record OptionRegistry(IReadOnlyList<OptionEnum> Enums, IReadOnlyList<OptionEntry> Options);

internal static class Naming {
    static readonly string[] CSharpGroup = ["ReSharper", "CSharp"];
    static readonly string[] XmlDocGroup = ["ReSharper", "XmlDoc"];

    public static string Pascal(string editorConfigName) {
        var builder = new StringBuilder(editorConfigName.Length);
        var upper = true;
        foreach (var c in editorConfigName) {
            if (c is '_' or '.' or '-' or ' ') {
                upper = true;
                continue;
            }

            builder.Append(upper ? char.ToUpperInvariant(c) : c);
            upper = false;
        }

        var result = builder.ToString();
        return result.Length == 0 || char.IsDigit(result[0]) ? "_" + result : result;
    }

    /// <summary>
    /// The <c>resharper_</c>/<c>csharp_</c>/<c>dotnet_</c> prefix is retained as a group, so
    /// <c>resharper_csharp_wrap_arguments_style</c> reads as
    /// <c>Options.ReSharper.CSharp.WrapArgumentsStyle</c> (docs/plan/02 § "Naming").
    /// </summary>
    public static IReadOnlyList<string> GroupPath(string key) => key switch {
        _ when key.StartsWith("resharper_csharp_", StringComparison.Ordinal) => CSharpGroup,
        _ when key.StartsWith("resharper_xmldoc_", StringComparison.Ordinal) => XmlDocGroup,
        _ when key.StartsWith("resharper_", StringComparison.Ordinal) => ["ReSharper"],
        _ when key.StartsWith("csharp_", StringComparison.Ordinal) => ["CSharp"],
        _ when key.StartsWith("dotnet_", StringComparison.Ordinal) => ["DotNet"],
        _ => ["Standard"]
    };

    public static string LeafName(string key) {
        var path = GroupPath(key);
        var prefix = path[0] switch {
            "ReSharper" when path.Count == 2 && path[1] == "CSharp" => "resharper_csharp_",
            "ReSharper" when path.Count == 2 => "resharper_xmldoc_",
            "ReSharper" => "resharper_",
            "CSharp" => "csharp_",
            "DotNet" => "dotnet_",
            _ => ""
        };

        return Pascal(key.Substring(prefix.Length));
    }
}

internal static class OptionRegistryReader {
    public static OptionRegistry Read(string json) {
        var root = Json.Parse(json);

        var enums = new List<OptionEnum>();
        foreach (var member in root["enums"].Members.OrderBy(static m => m.Key, StringComparer.Ordinal)) {
            var values = new List<OptionEnumValue>();
            foreach (var value in member.Value["values"].Items) {
                var name = value["name"].AsString();
                if (name is null) {
                    continue;
                }

                values.Add(new OptionEnumValue(name, Naming.Pascal(name), value["summary"].AsString() ?? string.Empty));
            }

            var aliases = member.Value["valueAliases"].Members
                .Select(static a => new KeyValuePair<string, string>(a.Key, a.Value.AsString() ?? string.Empty))
                .OrderBy(static a => a.Key, StringComparer.Ordinal)
                .ToList();

            enums.Add(new OptionEnum(member.Key, values, aliases));
        }

        var options = new List<OptionEntry>();
        foreach (var item in root["options"].Items) {
            var key = item["key"].AsString();
            if (key is null) {
                continue;
            }

            var type = item["type"].AsString() ?? "string";
            var kind = type switch {
                "bool" => OptionValueKind.Bool,
                "int" => OptionValueKind.Int,
                _ when type.StartsWith("enum:", StringComparison.Ordinal) => OptionValueKind.Enum,
                _ => OptionValueKind.String
            };

            options.Add(new OptionEntry(
                key,
                item["aliases"].AsStringList(),
                item["language"].AsString() ?? "any",
                kind,
                kind == OptionValueKind.Enum ? type.Substring("enum:".Length) : null,
                item["default"].IsNull ? null : item["default"].AsString(),
                item["defaultSource"].AsString() ?? "unknown",
                item["tier"].AsString() ?? "D",
                item["construct"].AsString() ?? "Other",
                item["summary"].AsString() ?? string.Empty,
                item["since"].AsString() ?? "0.1",
                item["oracle"].IsNull ? null : item["oracle"].AsString(),
                item["docs"].IsNull ? null : item["docs"].AsString(),
                item["templateLine"].IsNull ? null : item["templateLine"].AsInt(),
                item["severitySuffix"].AsBool(),
                item["expands"].AsStringList()));
        }

        // ⚠ Dense and stable: ids are assigned by ordinal key order so that adding an option does
        // not renumber the ones after it (docs/plan/03 § "The option registry").
        options.Sort(static (a, b) => string.CompareOrdinal(a.Key, b.Key));
        return new OptionRegistry(enums, options);
    }

    public static string Literal(string? value) =>
        value is null ? "null" : "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r") + "\"";

    public static string IntLiteral(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "null";
}
